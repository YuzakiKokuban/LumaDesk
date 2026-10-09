using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using JiYaoChu.Services;
using JiYaoChu.Model;

namespace JiYaoChu.Interop;

/// <summary>An event the backend queued for the UI.</summary>
public sealed record BackendEvent(string Name, JsonNode? Payload);

/// <summary>What <c>lumadesk_init</c> reports about the machine.</summary>
public sealed record BackendBootstrap(
    JsonNode? Config,
    string? ConfigError,
    string Backend,
    int AbiVersion);

/// <summary>
/// The typed face of the Rust backend.
/// </summary>
/// <remarks>
/// Hardware reads and writes block (WMI, device IOCTLs, the registry), so every
/// entry point is asynchronous and runs on the thread pool. Callers on the UI
/// thread must await them.
/// </remarks>
public static class Backend
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static BackendBootstrap? _bootstrap;

    /// <summary>True once the backend has been built at least once.</summary>
    public static bool IsReady => _bootstrap is not null;

    /// <summary>
    /// Builds the backend exactly once and remembers what it reported.
    /// </summary>
    public static async Task<BackendBootstrap> InitializeAsync()
    {
        if (_bootstrap is { } ready)
        {
            return ready;
        }

        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return _bootstrap ??= await Task.Run(() =>
            {
                var data = Core.Initialize();
                var restoringOem = Environment.GetCommandLineArgs().Any(argument => string.Equals(argument, "--restore-oem", StringComparison.OrdinalIgnoreCase));
                if (data["backend"]?.GetValue<string>() != "mock" && !restoringOem)
                {
                    // Update an older owned login task once, preserving its
                    // enabled state and all other task metadata.
                    try { Core.Call("migrate_autostart_task"); }
                    catch (Exception error) { StartupLog.Write(error); }
                }
                // First launch takes over by default. Explicit restore opts out.
                var preference = Path.Combine(StartupLog.DataDirectory, "oem-auto-restore.optout");
                if (data["backend"]?.GetValue<string>() != "mock" && !restoringOem && !File.Exists(preference))
                {
                    try
                    {
                        // Reuse an existing takeover instead of enumerating OEM
                        // processes and tasks again on every background launch.
                        var oem = Core.Call("get_oem_status");
                        if (oem["taken_over"]?.GetValue<bool>() != true)
                            Core.Call("toggle_oem_service", JsonSerializer.SerializeToNode(new { enable = true }, Core.Json));
                    }
                    catch (Exception error) {
                        JiYaoChu.Services.StartupLog.Write(error);
                        data["config_error"] = "自动接管未完成：" + error.Message;
                    }
                }
                if (!restoringOem)
                {
                    try
                    {
                        // Apply the saved on/off, brightness and colour once at startup,
                        // including background startup, before any lighting page is opened.
                        var lighting = (data["config"]?["lighting"].Read<LightingState>() ?? new()) with
                        {
                            FirmwareManaged = false, KbEngine = LightingEngine.Hardware, KbEffect = 0,
                        };
                        Core.Call("apply_keyboard_lighting", JsonSerializer.SerializeToNode(new { lighting }, Core.Json));
                        data["config"]!["lighting"] = JsonSerializer.SerializeToNode(lighting, Core.Json);
                    }
                    catch (Exception error)
                    {
                        StartupLog.Write(error);
                        var previous = data["config_error"]?.GetValue<string>();
                        data["config_error"] = (previous is null ? "" : previous + "；") + "键盘灯效自动接管未完成：" + error.Message;
                    }
                }
                return new BackendBootstrap(
                    data["config"],
                    data["config_error"]?.GetValue<string>(),
                    data["backend"]?.GetValue<string>() ?? "unknown",
                    data["abi_version"]?.GetValue<int>() ?? Core.LoadedAbiVersion);
            }).ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Configuration captured at startup.</summary>
    public static JsonNode? StartupConfig => _bootstrap?.Config;

    /// <summary>Why the configuration could not be read, if it could not.</summary>
    public static string? StartupConfigError => _bootstrap?.ConfigError;

    /// <summary>Which HAL the backend chose: a Windows backend name or "mock".</summary>
    public static string BackendName => _bootstrap?.Backend ?? "unknown";

    /// <summary>
    /// Runs one command and deserialises its payload.
    /// </summary>
    /// <param name="command">The wire name, e.g. <c>get_hardware_status</c>.</param>
    /// <param name="arguments">
    /// An anonymous object (or a dictionary) whose property names become the
    /// snake_case argument keys the dispatcher expects, or <c>null</c>.
    /// </param>
    public static async Task<T> CallAsync<T>(string command, object? arguments = null)
    {
        var node = await CallNodeAsync(command, arguments).ConfigureAwait(false);
        try
        {
            return Payload.Repair(node.Deserialize<T>(Core.Json)
                ?? throw new CoreException(command, "the backend sent an empty payload"));
        }
        catch (JsonException error)
        {
            throw new CoreException(
                command,
                $"the backend's reply did not match {typeof(T).Name} ({error.Message})");
        }
    }

    /// <summary>Runs one command and returns its raw payload.</summary>
    public static async Task<JsonNode> CallNodeAsync(string command, object? arguments = null)
    {
        var payload = arguments is null ? null : JsonSerializer.SerializeToNode(arguments, Core.Json);
        var manualPowerChange = command is "set_power_mode" or "set_display_monitor_refresh_rate" or "switch_refresh_rate";
        using var policyLease = manualPowerChange && payload?["automatic"]?.GetValue<bool>() != true
            ? await PowerPolicy.BeginManualChangeAsync() : null;
        // UI-originated writers publish their event before completing, so the page
        // can await the event-owned readback instead of scheduling a duplicate.
        var result = await Task.Run(() => Core.Call(command, payload));
        foreach (var raised in await DrainEventsAsync()) EventBus.Raise(raised);
        var appliedArguments = command == "restore_app_settings"
            ? new JsonObject { ["cfg"] = result.DeepClone() } : payload;
        if (!command.StartsWith("get_", StringComparison.Ordinal) && !command.StartsWith("list_", StringComparison.Ordinal)
            && !command.StartsWith("detect_", StringComparison.Ordinal) && !command.StartsWith("system.", StringComparison.Ordinal))
            EventBus.Raise(new BackendEvent("command://applied", JsonSerializer.SerializeToNode(new { command, arguments = appliedArguments }, Core.Json)));
        return result;
    }

    /// <summary>Runs one command that returns nothing useful.</summary>
    public static Task CallAsync(string command, object? arguments = null)
        => CallNodeAsync(command, arguments);

    /// <summary>Takes everything the backend has queued since the last poll.</summary>
    public static async Task<IReadOnlyList<BackendEvent>> DrainEventsAsync()
    {
        var data = await Task.Run(Core.DrainEvents).ConfigureAwait(false);
        if (data is not JsonArray array)
        {
            return [];
        }

        var events = new List<BackendEvent>(array.Count);
        foreach (var item in array)
        {
            var name = item?["name"]?.GetValue<string>();
            if (name is not null)
            {
                events.Add(new BackendEvent(name, item?["payload"]));
            }
        }

        return events;
    }

    /// <summary>The commands supported by the loaded backend.</summary>
    public static Task<IReadOnlyList<string>> CommandNamesAsync()
        => CallAsync<IReadOnlyList<string>>("system.commands");
}

/// <summary>Serializer helpers shared by the model records.</summary>
internal static class JsonExtensions
{
    public static T? Read<T>(this JsonNode? node) where T : class
        => node is null ? null : node.Deserialize<T>(Core.Json);

    public static T ReadValue<T>(this JsonNode? node, T fallback)
        => node is null ? fallback : node.Deserialize<T>(Core.Json) ?? fallback;
}
