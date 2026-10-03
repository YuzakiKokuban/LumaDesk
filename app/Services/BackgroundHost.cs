using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using JiYaoChu.Interop;
using JiYaoChu.Model;

namespace JiYaoChu.Services;

/// <summary>Owns the tray icon and the visibility of the main window.</summary>
public static class BackgroundHost
{
    private static AppWindow? _window;
    private static nint _hwnd;
    internal static nint Handle => _hwnd;
    private static nint _icon;
    private static bool _exiting;
    private static bool _trayReady;
    private static bool _keyChangePending;
    private static long _lastKeyChange;
    private const uint TrayMessage = 0x8001;
    private static readonly uint TaskbarCreated = RegisterWindowMessage("TaskbarCreated");
    private static readonly uint ActivateMessage = RegisterWindowMessage("LumaDesk.Activate");
    // Native callbacks must stay rooted for the entire window lifetime.
    private static readonly SubclassProc Callback = WindowProc;

    public static void Attach(AppWindow window)
    {
        if (_window is not null) return;
        _window = window;
        _hwnd = Microsoft.UI.Win32Interop.GetWindowFromWindowId(window.Id);
        if (!SetWindowSubclass(_hwnd, Callback, 1, 0))
            throw new InvalidOperationException("无法安装托盘窗口回调。");
        ExtractIconEx(Environment.ProcessPath!, 0, null, out _icon, 1);
        _trayReady = AddIcon();
        // Never hide the only reachable UI if Explorer rejects the tray icon.
        window.Closing += (_, args) =>
        {
            if (_exiting || !_trayReady) return;
            args.Cancel = true;
            Hide();
        };
        window.Destroying += (_, _) => Cleanup();
        OsdOverlay.Start();
        var background = Environment.GetCommandLineArgs().Contains("--background");
        MachineStore.SetActive(!background || !_trayReady);
        if (background && _trayReady) window.Hide();
        _ = InitializeAsync();
    }

    private static async Task InitializeAsync()
    {
        try
        {
            var startup = await Backend.InitializeAsync();
            var config = startup.Config.Read<AppConfig>() ?? new();
            OsdOverlay.Configure(config.Osd);
            if (Backend.BackendName != "mock") SystemOsdEvents.Start(_hwnd);
            if (config.WinKeyLocked)
                await Backend.CallAsync("set_win_key_locked", new { locked = true });
            await Task.Run(Core.StartOemHotkeys);
            if (Environment.GetCommandLineArgs().FirstOrDefault(arg => arg.StartsWith("--verify-shell=")) is { } verification)
                await ShellVerification.RunAsync(verification["--verify-shell=".Length..]);
        }
        catch (Exception error) { StartupLog.Write(error); }
    }

    public static void Show()
    {
        if (_window is null) return;
        ShowWindow(_hwnd, 9); // Restore a minimized window.
        _window.Show(true);
        SetForegroundWindow(_hwnd);
        MachineStore.SetActive(true);
    }

    public static async void OnOemHotkey(uint code)
    {
        if ((code & 0xff) is 0xba or 0xcc) { Show(); return; }
        if ((code & 0xff) is not (0xce or 0xa5 or 0x40 or 0x41))
        {
            OsdOverlay.OnFirmwareNotice(code & 0xff);
            return;
        }
        if (_keyChangePending || Environment.TickCount64 - _lastKeyChange < 350) return;
        _lastKeyChange = Environment.TickCount64;
        _keyChangePending = true;
        try
        {
            var switches = await Backend.CallAsync<IReadOnlyList<DeviceSwitch>>("get_device_switches");
            var locked = switches.First(item => item.Id == "win_key_lock").Enabled;
            // A physical Fn+F3 press emits 40 followed by A5 on this model.
            // The OEM's EC lock state does not represent our software hook:
            // toggle once per packet group, even if 40 repeats on the next press.
            var desired = !locked;
            await Backend.CallAsync("set_device_switch", new { id = "win_key_lock", enabled = desired });
            EventBus.Raise(new BackendEvent("shell://win-key-changed", null));
            if (MachineStore.IsActive) MachineStore.Refresh();
        }
        catch (Exception error) { StartupLog.Write(error); }
        finally { _keyChangePending = false; }
    }

    public static void Hide()
    {
        if (_window is null || !_trayReady) return;
        MachineStore.SetActive(false);
        OsdOverlay.ResetObservation();
        _window.Hide();
    }

    public static void Exit()
    {
        if (_exiting) return;
        _exiting = true;
        FinishExit();
    }

    private static void FinishExit()
    {
        MachineStore.SetActive(false);
        OsdOverlay.Close();
        Cleanup();
        _window?.Destroy();
        // Reactor owns a resident dispatcher beyond the native window lifetime.
        // All settings are persisted per command; finish after native cleanup.
        Environment.Exit(0);
    }

    private static void Cleanup()
    {
        SystemOsdEvents.Stop();
        var data = IconData();
        ShellNotifyIcon(2, ref data);
        _trayReady = false;
        if (_hwnd != 0) RemoveWindowSubclass(_hwnd, Callback, 1);
        if (_icon != 0) { DestroyIcon(_icon); _icon = 0; }
    }

    private static NotifyIconData IconData() => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyIconData>(), Window = _hwnd, Id = 1,
        Flags = 1 | 2 | 4, CallbackMessage = TrayMessage, Icon = _icon,
        Tip = "机耀处 · LumaDesk\n双击打开，右键菜单", Info = "", InfoTitle = "",
    };

    private static bool AddIcon()
    {
        var data = IconData();
        return ShellNotifyIcon(0, ref data);
    }

    private static nint WindowProc(nint hwnd, uint message, nuint w, nint l, nuint id, nuint reference)
    {
        try
        {
            SystemOsdEvents.HandleWindowMessage(message, (nint)w, l);
            if (message == ActivateMessage) { Show(); return 0; }
            if (message == TaskbarCreated)
            {
                _trayReady = AddIcon();
                if (!_trayReady) Show();
            }
            else if (message == TrayMessage)
            {
                if ((uint)l == 0x203) Show(); // WM_LBUTTONDBLCLK
                else if ((uint)l is 0x205 or 0x7b) ShowMenu();
                return 0;
            }
            else if (message == 0x0005 && w == 1 && _trayReady && !_exiting)
            {
                // Defer hiding until WinUI has finished the size notification.
                Microsoft.UI.Reactor.ReactorApp.UIDispatcher?.TryEnqueue(Hide);
            }
            else if (message == 0x0011) _exiting = true; // Restart Manager / logoff.
            else if (message == 0x0016)
            {
                if (w != 0) FinishExit();
                else _exiting = false; // The shutdown was canceled.
            }
        }
        catch (Exception error) { StartupLog.Write(error); }
        return DefSubclassProc(hwnd, message, w, l);
    }

    private static void ShowMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == 0) return;
        try
        {
            AppendMenu(menu, 0, 1, "打开机耀处");
            AppendMenu(menu, 0x800, 0, "");
            AppendMenu(menu, 0, 2, "退出机耀处");
            GetCursorPos(out var point);
            SetForegroundWindow(_hwnd);
            var selected = TrackPopupMenu(menu, 0x100 | 0x2, point.X, point.Y, 0, _hwnd, 0);
            if (selected == 1) Show();
            if (selected == 2) Exit();
            PostMessage(_hwnd, 0, 0, 0);
        }
        finally { DestroyMenu(menu); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size; public nint Window; public uint Id, Flags, CallbackMessage; public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint TimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags; public Guid Guid; public nint BalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    private delegate nint SubclassProc(nint hwnd, uint message, nuint w, nint l, nuint id, nuint reference);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(nint hwnd, SubclassProc callback, nuint id, nuint reference);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(nint hwnd, SubclassProc callback, nuint id);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint hwnd, uint message, nuint w, nint l);
    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode)] private static extern bool ShellNotifyIcon(uint action, ref NotifyIconData data);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern uint ExtractIconEx(string file, int index, nint[]? large, out nint small, uint count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(nint menu, uint flags, nuint id, string text);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint hwnd, nint rect);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint hwnd, uint message, nuint w, nint l);
}
