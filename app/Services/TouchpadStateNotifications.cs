using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Microsoft.UI.Reactor;

namespace JiYaoChu.Services;

/// <summary>Read Windows touchpad setting changes without rebinding keys or writing device state.</summary>
internal sealed class TouchpadStateNotifications : IDisposable
{
    // Microsoft documents this per-user setting and read-only change consumption:
    // https://learn.microsoft.com/windows-hardware/design/component-guidelines/touchpad-legacy-touchpad-pc-settings-opt-in
    private const string SettingsPath = @"Software\Microsoft\Windows\CurrentVersion\PrecisionTouchPad";
    private readonly ManualResetEvent _stop = new(false);
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _worker;
    private int _disposed;
    internal Task Ready => _ready.Task;

    public TouchpadStateNotifications() : this(
        () => Registry.CurrentUser.OpenSubKey(SettingsPath, false),
        null) { }

    internal TouchpadStateNotifications(Func<RegistryKey?> open, Action<bool>? report)
    {
        report ??= enabled => ReactorApp.UIDispatcher?.TryEnqueue(() =>
        {
            if (Volatile.Read(ref _disposed) == 0) SystemOsdEvents.PublishTouchpadState(enabled);
        });
        _worker = Task.Factory.StartNew(() => Watch(open, report), CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    internal static bool? Decode(object? value) => value is int enabled ? enabled != 0 : null;

    private void Watch(Func<RegistryKey?> open, Action<bool> report)
    {
        try
        {
            using var parent = open();
            if (parent is null) return;
            using var changed = new AutoResetEvent(false);
            bool? previous = null;
            while (!_stop.WaitOne(0))
            {
                // Watch the parent so a recreated Status subkey stays observable.
                var error = RegNotifyChangeKeyValue(parent.Handle, true, 5, changed.SafeWaitHandle, true);
                if (error != 0) throw new Win32Exception(error, "无法订阅触摸板状态。");
                using var status = parent.OpenSubKey("Status", false);
                var current = Decode(status?.GetValue("Enabled"));
                if (previous.HasValue && current.HasValue && current != previous && Volatile.Read(ref _disposed) == 0)
                    report(current.Value);
                previous = current;
                _ready.TrySetResult();
                if (WaitHandle.WaitAny([_stop, changed]) == 0) break;
            }
        }
        catch (Exception error) { StartupLog.Write(error); }
        finally { _ready.TrySetResult(); _stop.Dispose(); }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0 || _worker.IsCompleted) return;
        try { _stop.Set(); }
        catch (ObjectDisposedException) { }
    }

    [DllImport("advapi32.dll")]
    private static extern int RegNotifyChangeKeyValue(SafeRegistryHandle key,
        [MarshalAs(UnmanagedType.Bool)] bool subtree, uint filter, SafeWaitHandle notify,
        [MarshalAs(UnmanagedType.Bool)] bool asynchronous);
}
