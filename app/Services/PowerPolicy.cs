using JiYaoChu.Interop;
using JiYaoChu.Model;
using System.Runtime.InteropServices;

namespace JiYaoChu.Services;

/// <summary>Opt-in rules triggered by power events, with no background telemetry.</summary>
internal static class PowerPolicy
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static AppConfig _config = new();
    private static uint? _source;
    private static int _revision;
    private static bool _started;

    public static void Start(AppConfig config)
    {
        _config = config;
        if (_started) return;
        _started = true;
        EventBus.Subscribe(raised =>
        {
            if (raised.Name != "command://applied") return;
            var command = raised.Payload?["command"]?.GetValue<string>();
            if (command == "restore_app_settings" && raised.Payload?["arguments"]?["cfg"].Read<AppConfig>() is { } restored)
            {
                // Restoring preferences must not immediately apply a device rule.
                _config = restored;
                ++_revision;
                return;
            }
            if (command == "set_power_automation" && raised.Payload?["arguments"] is { } arguments)
            {
                _config = _config with {
                    AutoPowerMode = arguments["enabled"]!.GetValue<bool>(),
                    PowerModeAc = arguments["ac_mode"]!.GetValue<byte>(),
                    PowerModeBattery = arguments["battery_mode"]!.GetValue<byte>() };
                if (_config.AutoPowerMode)
                {
                    var source = _source ?? ReadCurrentSource();
                    if (source is { } known) OnPowerSource(known);
                }
            }
        });
    }
    private static uint? ReadCurrentSource()
    {
        if (Backend.BackendName == "mock") return 0;
        if (!GetSystemPowerStatus(out var status) || status.AcLineStatus == 255) return null;
        return status.AcLineStatus == 1 ? 0u : 1u;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct PowerStatus { public byte AcLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public uint BatteryLifeTime, BatteryFullLifeTime; }
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out PowerStatus status);
    public static void OnPowerSource(uint source)
    {
        if (source > 2) return;
        _source = source;
        var revision = ++_revision;
        _ = ApplyAsync(revision);
    }
    internal static async Task ApplyAsync(int revision)
    {
        await Gate.WaitAsync();
        try
        {
            if (revision != _revision || !_config.AutoPowerMode || _source is not { } source) return;
            var mode = Math.Min((byte)2, source == 0 ? _config.PowerModeAc : _config.PowerModeBattery);
            await Backend.CallAsync("set_power_mode", new { mode });
            if (MachineStore.IsActive) MachineStore.Refresh();
            OsdOverlay.Show("自动性能档位", PowerModes.Label(mode));
        }
        catch (Exception error) { StartupLog.Write(error); OsdOverlay.Show("自动档位未完成", "请打开机耀处检查硬件状态"); }
        finally { Gate.Release(); }
    }
}
