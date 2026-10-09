using JiYaoChu.Interop;
using JiYaoChu.Model;
using System.Runtime.InteropServices;

namespace JiYaoChu.Services;

/// <summary>AC/DC profile and internal-panel rules, with no background telemetry.</summary>
internal static class PowerPolicy
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static AppConfig _config = new();
    private static uint? _source;
    private static int _revision;
    private static bool _started;

    // Cancel older rules before waiting, then let any in-flight automatic write
    // finish before the manual command. The user's write is therefore last.
    internal static async Task<IDisposable> BeginManualChangeAsync()
    {
        ++_revision;
        await Gate.WaitAsync();
        return new ManualChangeLease();
    }
    private sealed class ManualChangeLease : IDisposable
    {
        public void Dispose() => Gate.Release();
    }

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
                ++_revision;
                if (_config.AutoPowerMode)
                {
                    var source = _source ?? ReadCurrentSource();
                    if (source is { } known)
                    {
                        _source = known;
                        _ = ApplyAsync(_revision);
                    }
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
        if (source > 2 || _source == source) return;
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
            await Backend.CallAsync("set_power_mode", new { mode, automatic = true });
            if (MachineStore.IsActive) MachineStore.Refresh();
            if (revision != _revision) return;
            var hz = source == 0 ? 240u : 90u;
            var displays = await Backend.CallAsync<DisplayInfo[]>("get_displays");
            var panels = displays.Where(display => display.IsInternal).ToArray();
            var unsupported = false;
            var appliedRates = new HashSet<uint>();
            foreach (var panel in panels)
            {
                if (revision != _revision) return;
                if (DisplayPolicy.AutomaticRate(panel, source == 0) is not { } rate) { unsupported = true; continue; }
                if (panel.CurrentHz != rate)
                    await Backend.CallAsync("set_internal_display_refresh_rate", new { device_name = panel.DeviceName, hz = rate });
                appliedRates.Add(rate);
            }
            if (revision != _revision) return;
            OsdOverlay.Show("自动档位与刷新率", PowerModes.Label(mode) +
                (unsupported ? $" · 内屏不支持目标刷新率（{hz} Hz）" : panels.Length == 0 ? " · 未找到独立内屏" : $" · {string.Join("/", appliedRates.Order())} Hz"));
        }
        catch (Exception error) { StartupLog.Write(error); if (revision == _revision) OsdOverlay.Show("自动档位与刷新率未完成", "请打开机耀处检查硬件状态"); }
        finally { Gate.Release(); }
    }
}
