using JiYaoChu.Interop;
using JiYaoChu.Model;

namespace JiYaoChu.Services;

/// <summary>The physical mode button changes EC state without a WMI notification.</summary>
internal static class ChassisProfileNotifications
{
    private static CancellationTokenSource? _stop;

    public static void Start()
    {
        if (_stop is not null || Backend.BackendName == "mock") return;
        _stop = new CancellationTokenSource();
        _ = WatchAsync(_stop.Token);
    }

    private static async Task WatchAsync(CancellationToken stop)
    {
        byte? previous = null;
        try
        {
            while (!stop.IsCancellationRequested)
            {
                if (!OsdOverlay.WatchPhysicalProfile)
                    previous = null;
                else if (MachineStore.IsActive)
                    previous = MachineStore.Snapshot.Status?.PowerMode;
                else
                {
                    try
                    {
                        // Only project ID + profile byte; no temperature, fan,
                        // battery, GPU or WMI telemetry in the background.
                        var mode = await Backend.CallAsync<byte>("get_power_mode");
                        if (previous.HasValue && mode != previous)
                            SystemOsdEvents.PublishPowerMode(mode);
                        previous = mode;
                    }
                    catch { previous = null; } // Unknown/unavailable firmware is not a toggle.
                }
                await Task.Delay(1000, stop);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    }

    public static void Stop()
    {
        _stop?.Cancel();
        _stop?.Dispose();
        _stop = null;
    }
}
