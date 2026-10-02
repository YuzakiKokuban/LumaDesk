using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Reactor.Navigation;
using Microsoft.UI.Xaml.Controls;
using JiYaoChu.Interop;
using JiYaoChu.Pages;
using JiYaoChu.Services;
using JiYaoChu.Ui;
using static Microsoft.UI.Reactor.Factories;
using System.Runtime.InteropServices;

// To learn more about Reactor, the Reactor project structure, and more about
// our project templates, see: https://github.com/microsoft/microsoft-ui-reactor
//
// The width matters: WinUI's NavigationView switches to icon-only compact mode
// below 1008 px, and this sidebar carries Chinese labels that mean nothing as
// bare glyphs, so open at a width that keeps the pane expanded.
if (Environment.GetCommandLineArgs().Skip(1).Any(argument =>
        string.Equals(argument, "--restore-oem", StringComparison.OrdinalIgnoreCase)))
{
    try
    {
        await Backend.InitializeAsync();
        await Backend.CallAsync("restore_official_control_center");
        Environment.Exit(0);
    }
    catch (Exception error)
    {
        Console.Error.WriteLine($"JiYaoChu OEM restore failed: {error.Message}");
        Environment.Exit(1);
    }
}

AppDomain.CurrentDomain.UnhandledException += (_, args) =>
{
    if (args.ExceptionObject is Exception error) StartupLog.Write(error);
};
ReactorApplication.OnUnhandledException = error => { StartupLog.Write(error); return false; };
var initialSize = WindowMetrics.InitialSize();
try
{
    using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
    var principal = new System.Security.Principal.WindowsPrincipal(identity);
    if (!principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
        throw new UnauthorizedAccessException("机耀处需要管理员权限，请启动机耀处.exe 并接受权限请求。");
    NativeLayout.Configure();
    ReactorApp.Run<App>("机耀处 · LumaDesk", width: initialSize.Width, height: initialSize.Height);
}
catch (Exception error)
{
    StartupLog.Write(error);
    throw;
}

static class WindowMetrics
{
    private const int SpiGetWorkArea = 0x0030;
    private const int BaseWidth = 1180;
    private const int BaseHeight = 820;
    private const int MinimumWidth = 720;
    private const int MinimumHeight = 560;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, out Rect value, uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    public static (int Width, int Height) InitialSize()
    {
        if (!SystemParametersInfo(SpiGetWorkArea, 0, out var workArea, 0))
        {
            return (BaseWidth, BaseHeight);
        }

        var dpi = Math.Max(96u, GetDpiForSystem());
        var availableWidth = Math.Max(MinimumWidth, (workArea.Right - workArea.Left) * 96 / (int)dpi - 24);
        var availableHeight = Math.Max(MinimumHeight, (workArea.Bottom - workArea.Top) * 96 / (int)dpi - 24);
        return (Math.Min(BaseWidth, availableWidth), Math.Min(BaseHeight, availableHeight));
    }
}

/// <summary>
/// The application pages.
/// </summary>
enum AppRoute
{
    Status,
    Gpu,
    Tuning,
    Lighting,
    System,
}

class App : Component
{

    static string RouteToTag(AppRoute route) => route switch
    {
        AppRoute.Gpu => "gpu",
        AppRoute.Tuning => "tuning",
        AppRoute.Lighting => "lighting",
        AppRoute.System => "system",
        _ => "status",
    };

    static AppRoute TagToRoute(string? tag) => tag switch
    {
        "gpu" => AppRoute.Gpu,
        "tuning" => AppRoute.Tuning,
        "lighting" => AppRoute.Lighting,
        "system" => AppRoute.System,
        _ => AppRoute.Status,
    };

    static Element RouteToPage(AppRoute route) => route switch
    {
        AppRoute.Gpu => Component<GpuPage>(),
        AppRoute.Tuning => Component<TuningPage>(),
        AppRoute.Lighting => Component<LightingPage>(),
        AppRoute.System => Component<SystemPage>(),
        _ => Component<OverviewPage>(),
    };


    public override Element Render()
    {
        // Idempotent; the first render starts the poll loop.
        MachineStore.Start();

        var nav = UseNavigation(AppRoute.Status);
        var (isPaneOpen, setIsPaneOpen) = UseState(true);

        var items = new List<NavigationViewItemData>
        {
            NavItem("状态概览", icon: "Home", tag: "status"),
            NavItem("显卡模式", icon: "\uE7F4", tag: "gpu"),
            NavItem("电源与电池", icon: "\uE83F", tag: "tuning"),
            NavItem("键盘灯效", icon: "\uE765", tag: "lighting"),
        };

        items.Add(NavItem("系统设置", icon: "Setting", tag: "system"));

        var titleBar = TitleBar("机耀处 · LumaDesk")
            .WithNavigation(nav)
            .PaneToggleButtonVisible(true)
            .PaneToggleRequested(() => setIsPaneOpen(!isPaneOpen))
            .Tall()
            .Flex(shrink: 0);

        // WithNavigation is what keeps the pane's selection and the route in
        // step; setting SelectedTag by hand races the pane's own initial
        // selection and the app can come up on an arbitrary page.
        var host = NavigationHost(nav, RouteToPage) with
        {
            Transition = NavigationTransition.Fade(TimeSpan.FromMilliseconds(160)),
            CacheMode = NavigationCacheMode.Disabled,
        };
        var navView = (NavigationView([.. items], host) with
        {
            IsSettingsVisible = false,
            IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
            IsPaneToggleButtonVisible = false,
        })
        .WithNavigation(nav, RouteToTag, TagToRoute)
        .SelectedTagChanged(tag =>
        {
            var route = TagToRoute(tag);
            if (route != nav.CurrentRoute) nav.Replace(route);
        })
        .IsPaneOpen(isPaneOpen, setIsPaneOpen)
        .Flex(grow: 1, basis: 0);

        return Grid(columns: [GridSize.Star()], rows: [GridSize.Auto, GridSize.Star()],
            titleBar.Grid(row: 0, column: 0), navView.Grid(row: 1, column: 0))
            .Backdrop(BackdropKind.Mica);
    }
}
