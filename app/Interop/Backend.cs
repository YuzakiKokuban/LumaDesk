using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

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
                // First launch takes over by default. Explicit restore opts out.
                var preference = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JiYaoChu", "oem-auto-restore.optout");
                if (data["backend"]?.GetValue<string>() != "mock" && !Environment.GetCommandLineArgs().Any(argument => string.Equals(argument, "--restore-oem", StringComparison.OrdinalIgnoreCase)) && !File.Exists(preference))
                {
                    try { Core.Call("toggle_oem_service", JsonSerializer.SerializeToNode(new { enable = true }, Core.Json)); }
                    catch (Exception error) {
                        JiYaoChu.Services.StartupLog.Write(error);
                        data["config_error"] = "自动接管未完成：" + error.Message;
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
        return await Task.Run(() => Core.Call(command, payload)).ConfigureAwait(false);
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

    /// <summary>The 112 command names this build understands.</summary>
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
