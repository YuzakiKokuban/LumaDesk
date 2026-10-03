using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using System.Runtime.InteropServices;
using JiYaoChu.Interop;
using JiYaoChu.Model;

namespace JiYaoChu.Services;

/// <summary>A reusable, nonactivating native OSD window, updated only by events.</summary>
public static class OsdOverlay
{
    private static Window? _window;
    private static Border? _card;
    private static TextBlock? _title, _detail;
    private static DispatcherTimer? _timer;
    private static OsdConfig _config = new();
    private static HardwareStatus? _previous;
    private static bool _started;
    private static volatile bool _shown;
    internal static bool IsVisible => _shown;
    private static readonly Dictionary<string, long> SystemNoticeTimes = [];
    internal static (string Title, string Detail) VisibleNotice => (_title?.Text ?? "", _detail?.Text ?? "");
    internal static int BorderConfigurationResult { get; private set; }
    private static readonly SubclassProc FrameCallback = FrameWindowProc;

    public static void Start()
    {
        if (_started) return;
        _started = true;
        EventBus.Subscribe(OnEvent);
        MachineStore.Subscribe(Observe);
    }

    public static void Configure(OsdConfig config)
    {
        _config = config;
        if (!config.Enabled) Hide();
        else if (_window is not null) ApplyOpacity(WinRT.Interop.WindowNative.GetWindowHandle(_window));
    }

    public static void ResetObservation() { _previous = null; Hide(); }

    // Firmware supplies these notifications directly. The hidden application
    // displays their meaning without starting telemetry or reading the EC.
    public static void OnFirmwareNotice(uint code)
    {
        if (FirmwareOsdNotices.TryGetNotice(code, out var title, out var detail, out var kind))
        {
            // A firmware companion must not overwrite a more precise Windows
            // notification (for example "音量 40%") with a generic change hint.
            if (SystemNoticeTimes.TryGetValue(kind, out var last) && Environment.TickCount64 - last < 750) return;
            ShowNotice(title, detail, kind);
        }
    }

    private static void Observe()
    {
        if (!MachineStore.IsActive || MachineStore.Snapshot.Error is not null) return;
        var status = MachineStore.Snapshot.Status;
        if (status is null) return;
        if (_previous is { } previous && _config.ShowOnPowerChange)
        {
            if (status.PowerMode != previous.PowerMode)
                Show("性能模式", PowerModes.Label(status.PowerMode));
            else if (status.FanBoost != previous.FanBoost && status.FanBoost is { } fan)
                Show("一键强冷", fan ? "已开启" : "已关闭");
        }
        _previous = status;
    }

    private static void OnEvent(BackendEvent raised)
    {
        switch (raised.Name)
        {
            case "osd://config":
                if (raised.Payload.Read<OsdConfig>() is { } config) Configure(config);
                break;
            case "osd://hide": Hide(); break;
            case "osd://show": Show("机耀处", "屏幕提示"); break;
            case "osd://system":
                var systemKind = raised.Payload?["kind"]?.GetValue<string>() ?? "";
                SystemNoticeTimes[systemKind] = Environment.TickCount64;
                ShowNotice(raised.Payload?["title"]?.GetValue<string>() ?? "机耀处",
                    raised.Payload?["detail"]?.GetValue<string>() ?? "状态已变化",
                    systemKind);
                break;
            case "osd://preview":
                var kind = raised.Payload?["kind"]?.GetValue<string>();
                Show(kind == "brightness" ? "键盘背光" : "性能模式",
                    kind == "brightness" ? "亮度 3 / 4 · 预览" : "均衡 · 预览");
                break;
            case "command://applied":
                var command = raised.Payload?["command"]?.GetValue<string>();
                var args = raised.Payload?["arguments"];
                if (command == "set_device_switch" && args?["id"]?.GetValue<string>() == "win_key_lock")
                    Show("Win 键锁", args?["enabled"]?.GetValue<bool>() == true ? "已锁定" : "已解锁");
                else if (command == "apply_keyboard_lighting")
                {
                    var lighting = args?["lighting"].Read<LightingState>();
                    Show("键盘背光", lighting?.Enabled == true ? $"亮度 {lighting.KbBrightness} / 4" : "已关闭");
                }
                else if (command is "set_refresh_rate" && _config.ShowOnRefreshChange)
                    Show("屏幕刷新率", "设置已应用");
                else if (command == "set_display_brightness" && args?["level"]?.GetValue<uint>() is { } level)
                    ShowNotice("屏幕亮度", $"{Math.Min(level, 100)}%", "brightness");
                break;
        }
    }

    private static void ShowNotice(string title, string detail, string kind)
    {
        if (kind is "power" or "performance" && !_config.ShowOnPowerChange) return;
        Show(title, detail);
    }

    private static void ApplyOpacity(nint hwnd) =>
        SetLayeredWindowAttributes(hwnd, 0, (byte)(Math.Clamp(_config.Opacity, 20u, 100u) * 255 / 100), 2);

    public static void Show(string title, string detail)
    {
        if (!_config.Enabled) return;
        EnsureWindow();
        _title!.Text = title;
        _detail!.Text = detail;
        var light = _config.Theme == "light";
        _card!.Background = new SolidColorBrush(light ? Windows.UI.Color.FromArgb(255, 246, 248, 252) : Windows.UI.Color.FromArgb(255, 26, 31, 42));
        _title.Foreground = _detail.Foreground = new SolidColorBrush(light ? Windows.UI.Color.FromArgb(255, 22, 29, 43) : Windows.UI.Color.FromArgb(255, 245, 248, 255));
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window!);
        var area = DisplayArea.GetFromPoint(CursorPoint(), DisplayAreaFallback.Primary).WorkArea;
        var scale = Math.Max(96u, GetDpiForWindow(hwnd)) / 96.0;
        var width = (int)(320 * scale); var height = (int)(104 * scale); var margin = (int)(28 * scale);
        var position = _config.Position.Replace('-', '_');
        var x = position.EndsWith("left") ? area.X + margin : position.EndsWith("right") ? area.X + area.Width - width - margin : area.X + (area.Width - width) / 2;
        var y = position.StartsWith("top") ? area.Y + margin : position == "center" ? area.Y + (area.Height - height) / 2 : area.Y + area.Height - height - margin;
        _window!.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, width, height));
        ApplyOpacity(hwnd);
        _window.AppWindow.Show(false);
        _shown = true;
        // Show() can restore WinUI's dialog frame. Remove its client inset
        // after showing so the XAML card fills the entire rounded window.
        SetWindowLongPtr(hwnd, -16, (nint)(GetWindowLongPtr(hwnd, -16).ToInt64() & ~0x00c40000L));
        SetWindowLongPtr(hwnd, -20, (nint)(GetWindowLongPtr(hwnd, -20).ToInt64() & ~0x00020300L));
        uint noBorder = 0xfffffffe, round = 2;
        BorderConfigurationResult = DwmSetWindowAttribute(hwnd, 34, ref noBorder, sizeof(uint));
        DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(uint));
        SetWindowPos(hwnd, -1, x, y, width, height, 0x10 | 0x40 | 0x20); // topmost, show, no activation, refresh frame
        _timer!.Stop();
        _timer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(_config.DurationMs, 600u, 15000u));
        _timer.Start();
    }

    private static Windows.Graphics.PointInt32 CursorPoint()
    {
        GetCursorPos(out var point);
        return new(point.X, point.Y);
    }

    private static void EnsureWindow()
    {
        if (_window is not null) return;
        _window = new Window { Title = "LumaDesk OSD" };
        _title = new TextBlock { FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        _detail = new TextBlock { FontSize = 23, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        var content = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(_title); content.Children.Add(_detail);
        _card = new Border { Padding = new Thickness(24, 16, 24, 16), Child = content };
        _window.Content = _card;
        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsAlwaysOnTop = true; presenter.IsResizable = false;
        presenter.IsMinimizable = false; presenter.IsMaximizable = false;
        _window.AppWindow.SetPresenter(presenter);
        _window.AppWindow.IsShownInSwitchers = false;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        if (!SetWindowSubclass(hwnd, FrameCallback, 2, 0))
            throw new InvalidOperationException("无法移除 OSD 的窗口边框。");
        var styles = GetWindowLongPtr(hwnd, -20).ToInt64();
        SetWindowLongPtr(hwnd, -20, (nint)(styles | 0x08000000 | 0x00080000 | 0x00000020 | 0x00000080));
        var frameStyles = GetWindowLongPtr(hwnd, -16).ToInt64();
        SetWindowLongPtr(hwnd, -16, (nint)(frameStyles & ~0x00c40000L)); // WS_CAPTION and WS_THICKFRAME
        // Suppress DWM's default light outline, including the rounded corners.
        // Do not add a second XAML radius: DWM owns the window's clipping.
        uint noBorder = 0xfffffffe, round = 2;
        BorderConfigurationResult = DwmSetWindowAttribute(hwnd, 34, ref noBorder, sizeof(uint));
        DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(uint));
        _timer = new DispatcherTimer();
        _timer.Tick += (_, _) => Hide();
    }

    private static void Hide()
    {
        _timer?.Stop(); _window?.AppWindow.Hide();
        _shown = false;
        BackgroundMemory.ReleaseWhenHidden();
    }
    public static void Close()
    {
        Hide();
        if (_window is not null) RemoveWindowSubclass(WinRT.Interop.WindowNative.GetWindowHandle(_window), FrameCallback, 2);
        _window?.Close(); _window = null;
    }
    private static nint FrameWindowProc(nint hwnd, uint message, nuint w, nint l, nuint id, nuint reference)
    {
        // WinUI retains a dialog-frame inset even with title bar/border disabled.
        // Make the complete native rectangle the client area, leaving DWM to
        // apply a single smooth rounded clip instead of two competing edges.
        if (message == 0x83 && w != 0) return 0; // WM_NCCALCSIZE
        return DefSubclassProc(hwnd, message, w, l);
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(nint hwnd, uint color, byte alpha, uint flags);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint hwnd, uint attribute, ref uint value, int size);
    private delegate nint SubclassProc(nint hwnd, uint message, nuint w, nint l, nuint id, nuint reference);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(nint hwnd, SubclassProc callback, nuint id, nuint reference);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(nint hwnd, SubclassProc callback, nuint id);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint hwnd, uint message, nuint w, nint l);
}
