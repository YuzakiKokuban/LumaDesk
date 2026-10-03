using System.Runtime.InteropServices;

namespace JiYaoChu.Services;

/// <summary>One idle release per hide, delayed and cancelled when the window reopens.</summary>
internal static class BackgroundMemory
{
    private static readonly object Gate = new();
    private static CancellationTokenSource? _pending;
    private static long _lastRelease;
    internal static int Collections { get; private set; }
    internal static double LastCollectionMs { get; private set; }
    internal static int IdleDelayMs => _lastRelease == 0 ? 2000 : (int)Math.Max(2000, 60_000 - (Environment.TickCount64 - _lastRelease));

    public static void CancelPending()
    {
        lock (Gate) { _pending?.Cancel(); _pending = null; }
    }
    public static void ReleaseWhenHidden()
    {
        if (BackgroundHost.IsVisible || OsdOverlay.IsVisible) return;
        CancellationTokenSource pending;
        lock (Gate)
        {
            if (_pending is not null) return;
            pending = _pending = new CancellationTokenSource();
        }
        _ = ReleaseAsync(pending);
    }
    private static async Task ReleaseAsync(CancellationTokenSource pending)
    {
        try
        {
            await Task.Delay(IdleDelayMs, pending.Token);
            await Task.Run(() =>
            {
                if (pending.IsCancellationRequested || BackgroundHost.IsVisible || OsdOverlay.IsVisible) return;
                var timer = System.Diagnostics.Stopwatch.StartNew();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, blocking: true, compacting: false);
                GC.WaitForPendingFinalizers();
                if (!pending.IsCancellationRequested && !BackgroundHost.IsVisible && !OsdOverlay.IsVisible)
                    SetProcessWorkingSetSize(-1, nuint.MaxValue, nuint.MaxValue);
                LastCollectionMs = timer.Elapsed.TotalMilliseconds;
                Collections++;
                _lastRelease = Environment.TickCount64;
            });
        }
        catch (OperationCanceledException) { }
        finally
        {
            lock (Gate) { if (ReferenceEquals(_pending, pending)) _pending = null; }
            pending.Dispose();
        }
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessWorkingSetSize(nint process, nuint minimum, nuint maximum);
}
