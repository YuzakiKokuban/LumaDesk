using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Microsoft.UI.Reactor;

namespace JiYaoChu.Services;

/// <summary>Observe RmSvc's system radio state without polling individual radios.</summary>
internal sealed class RadioStateNotifications : IDisposable
{
    private const string StatePath = @"SYSTEM\CurrentControlSet\Control\RadioManagement\SystemRadioState";
    private readonly ManualResetEvent _stop = new(false);
    private readonly Task _worker;

    public RadioStateNotifications()
    {
        _worker = Task.Factory.StartNew(Watch, CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    internal static bool? Read(RegistryKey? key)
        => key?.GetValue(null) is int value ? value switch { 0 => false, 1 => true, _ => null } : null;

    private void Watch()
    {
        try
        {
            // The radio-management service owns this value. Unlike per-device
            // radio state, it distinguishes airplane mode from Wi-Fi being off.
            using var key = Registry.LocalMachine.OpenSubKey(StatePath, false);
            if (key is null) return;
            using var changed = new AutoResetEvent(false);
            bool? previous = null;
            while (!_stop.WaitOne(0))
            {
                var error = RegNotifyChangeKeyValue(key.Handle, false, 4,
                    changed.SafeWaitHandle, true);
                if (error != 0) throw new Win32Exception(error, "无法订阅飞行模式状态。");
                var current = Read(key);
                if (previous.HasValue && current.HasValue && current != previous)
                {
                    var enabled = current.Value;
                    ReactorApp.UIDispatcher?.TryEnqueue(() => SystemOsdEvents.PublishAirplaneState(enabled));
                }
                previous = current;
                if (WaitHandle.WaitAny([_stop, changed]) == 0) break;
            }
        }
        catch (Exception error) { StartupLog.Write(error); }
        finally { _stop.Dispose(); }
    }

    public void Dispose()
    {
        if (_worker.IsCompleted) return;
        try { _stop.Set(); }
        catch (ObjectDisposedException) { }
    }

    [DllImport("advapi32.dll")]
    private static extern int RegNotifyChangeKeyValue(SafeRegistryHandle key,
        [MarshalAs(UnmanagedType.Bool)] bool subtree, uint filter, SafeWaitHandle notify,
        [MarshalAs(UnmanagedType.Bool)] bool asynchronous);
}
