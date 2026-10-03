using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace JiYaoChu.Interop;

/// <summary>
/// A refusal reported by the Rust backend. The message is the backend's own
/// wording and is meant to be shown to the user as-is.
/// </summary>
public sealed class CoreException(string command, string message) : Exception(message)
{
    /// <summary>The command that was refused.</summary>
    public string Command { get; } = command;
}

/// <summary>
/// The C ABI exported by <c>jiyaochu_core.dll</c> (see <c>src/ffi.rs</c>).
/// </summary>
/// <remarks>
/// Every call returns a NUL-terminated UTF-8 JSON envelope
/// <c>{"ok":true,"data":…}</c> or <c>{"ok":false,"error":"…"}</c>, which the
/// caller owns and must hand back to <c>lumadesk_free</c>. This class is the
/// only place that knows about those pointers; everything else uses
/// <see cref="Backend"/>.
/// </remarks>
public static class Core
{
    private const string Library = "jiyaochu_core";
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void OemHotkeyCallback(uint code);
    private static readonly OemHotkeyCallback HotkeyCallback = code =>
    {
        try { Microsoft.UI.Reactor.ReactorApp.UIDispatcher?.TryEnqueue(() => JiYaoChu.Services.BackgroundHost.OnOemHotkey(code)); }
        catch (Exception error) { JiYaoChu.Services.StartupLog.Write(error); }
    };

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern nint lumadesk_start_oem_hotkeys(OemHotkeyCallback callback);
    public static void StartOemHotkeys()
    {
        if (Backend.BackendName == "mock") return;
        Envelope(lumadesk_start_oem_hotkeys(HotkeyCallback), "start_oem_hotkeys");
    }

    private static readonly OemHotkeyCallback BrightnessCallback = level =>
    {
        try { JiYaoChu.Services.SystemOsdEvents.PublishDisplayBrightness(level); }
        catch (Exception error) { JiYaoChu.Services.StartupLog.Write(error); }
    };
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern nint lumadesk_start_display_brightness(OemHotkeyCallback callback);
    public static void StartDisplayBrightness()
    {
        if (Backend.BackendName == "mock") return;
        Envelope(lumadesk_start_display_brightness(BrightnessCallback), "start_display_brightness");
    }

    /// <summary>Envelope version this build speaks.</summary>
    public const int AbiVersion = 1;

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern uint lumadesk_abi_version();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern nint lumadesk_init();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern nint lumadesk_call(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string command,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? payload);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern nint lumadesk_drain_events();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void lumadesk_free(nint value);

    /// <summary>Shared serializer settings: the backend speaks snake_case.</summary>
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    static Core()
    {
        // The backend spells variants the same way it spells fields:
        // GpuMode::Hybrid is "hybrid", BatteryMode::LongLife is "long_life".
        Json.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
    }

    /// <summary>Reads the version the loaded library actually reports.</summary>
    public static int LoadedAbiVersion => (int)lumadesk_abi_version();

    /// <summary>Builds the backend and returns the bootstrap document.</summary>
    public static JsonNode Initialize() => Envelope(lumadesk_init(), "init");

    /// <summary>Runs one command and returns its <c>data</c> member.</summary>
    public static JsonNode Call(string command, JsonNode? arguments = null)
        => Envelope(lumadesk_call(command, arguments?.ToJsonString()), command);

    /// <summary>Empties the backend's event queue.</summary>
    public static JsonNode DrainEvents() => Envelope(lumadesk_drain_events(), "drain_events");

    /// <summary>Copies an envelope out of unmanaged memory and frees it.</summary>
    private static JsonNode Envelope(nint raw, string command)
    {
        if (raw == 0)
        {
            throw new CoreException(command, "the backend returned nothing");
        }

        JsonNode? envelope;
        try
        {
            var text = Marshal.PtrToStringUTF8(raw);
            envelope = text is null ? null : JsonNode.Parse(text);
        }
        catch (JsonException error)
        {
            throw new CoreException(command, $"the backend sent malformed JSON ({error.Message})");
        }
        finally
        {
            // Exactly one free per returned pointer; the backend hands over
            // ownership, so this must happen even when parsing throws.
            lumadesk_free(raw);
        }

        if (envelope is null)
        {
            throw new CoreException(command, "the backend sent an empty envelope");
        }

        if (envelope["ok"]?.GetValue<bool>() != true)
        {
            throw new CoreException(
                command,
                envelope["error"]?.GetValue<string>() ?? "the backend gave no reason");
        }

        return envelope["data"] ?? JsonValue.Create((string?)null)!;
    }
}
