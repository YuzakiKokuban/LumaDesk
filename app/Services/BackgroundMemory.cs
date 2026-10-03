using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Reactor;

namespace JiYaoChu.Services;

/// <summary>Releases idle UI allocations once on hide, with no maintenance timer.</summary>
internal static class BackgroundMemory
{
    private static int _pending;

    public static void ReleaseWhenHidden()
    {
        if (BackgroundHost.IsVisible || OsdOverlay.IsVisible || Interlocked.Exchange(ref _pending, 1) != 0) return;
        // Let Reactor unmount the page before collecting its native wrappers.
        if (ReactorApp.UIDispatcher?.TryEnqueue(DispatcherQueuePriority.Low, () => _ = Task.Run(() =>
        {
            try
            {
                if (BackgroundHost.IsVisible || OsdOverlay.IsVisible) return;
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
                if (!BackgroundHost.IsVisible && !OsdOverlay.IsVisible)
                    SetProcessWorkingSetSize(-1, nuint.MaxValue, nuint.MaxValue);
            }
            finally { Volatile.Write(ref _pending, 0); }
        })) != true) Volatile.Write(ref _pending, 0);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessWorkingSetSize(nint process, nuint minimum, nuint maximum);
}
