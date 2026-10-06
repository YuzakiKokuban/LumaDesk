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
// The restored bounds are centered at half the monitor resolution by the
// native shell; normal opening maximizes the window.
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

// A second launch brings the resident instance forward instead of starting
// another poller or installing another keyboard hook.
var instanceName = "Local\\LumaDesk-" + System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
var isolatedShellVerification = Environment.GetEnvironmentVariable("JIYAOCHU_FORCE_MOCK") == "1"
    && Environment.GetCommandLineArgs().Any(arg => arg.StartsWith("--verify-shell="));
if (isolatedShellVerification)
    instanceName += "-verify-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JIYAOCHU_DATA_DIR") ?? "")))[..16];
using var instanceMutex = new System.Threading.Mutex(true, instanceName, out var firstInstance);
if (!firstInstance)
{
    if (!Environment.GetCommandLineArgs().Contains("--background"))
        WindowMetrics.ActivateExisting();
    return;
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
    if (!principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator) && !isolatedShellVerification)
        throw new UnauthorizedAccessException("机耀处需要管理员权限，请启动LumaDesk.exe 并接受权限请求。");
    NativeLayout.Configure();
    ReactorApp.Run<App>("机耀处", width: initialSize.Width, height: initialSize.Height);
}
catch (Exception error)
{
    StartupLog.Write(error);
    throw;
}

static class WindowMetrics
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll")]
    private static extern bool PostMessage(nint hwnd, uint message, nuint w, nint l);
    public static void ActivateExisting() => PostMessage(0xffff, RegisterWindowMessage("LumaDesk.Activate"), 0, 0);
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
    Tuning,
    Lighting,
    Display,
    System,
}

class App : Component
{

    static string RouteToTag(AppRoute route) => route switch
    {
        AppRoute.Tuning => "tuning",
        AppRoute.Lighting => "lighting",
        AppRoute.Display => "display",
        AppRoute.System => "system",
        _ => "status",
    };

    static AppRoute TagToRoute(string? tag) => tag switch
    {
        "gpu" => AppRoute.Display,
        "tuning" => AppRoute.Tuning,
        "lighting" => AppRoute.Lighting,
        "display" => AppRoute.Display,
        "system" => AppRoute.System,
        _ => AppRoute.Status,
    };

    static Element RouteToPage(AppRoute route) => route switch
    {
        AppRoute.Tuning => Component<TuningPage>(),
        AppRoute.Lighting => Component<LightingPage>(),
        AppRoute.Display => Component<DisplayPage>(),
        AppRoute.System => Component<SystemPage>(),
        _ => Component<OverviewPage>(),
    };


    public override Element Render()
    {
        // Idempotent; the first render starts the poll loop.
        MachineStore.Start();
        var shell = UseExternalStore(BackgroundHost.SubscribeVisibility, () => BackgroundHost.Visibility);
        var visible = shell.Visible;

        var nav = UseNavigation(AppRoute.Status);
        var (isPaneOpen, setIsPaneOpen) = UseState(true);
        var overviewVisible = visible && nav.CurrentRoute == AppRoute.Status;
        UseEffect(() => MachineStore.SetTrendActive(overviewVisible), overviewVisible);
        UseEffect(() => () => MachineStore.SetTrendActive(false), []);

        var items = new List<NavigationViewItemData>
        {
            NavItem("状态概览", icon: "Home", tag: "status"),
            NavItem("显示设置", icon: "\uE7F4", tag: "display"),
            NavItem("电源与电池", icon: "\uE83F", tag: "tuning"),
            NavItem("键盘灯效", icon: "\uE765", tag: "lighting"),
        };

        items.Add(NavItem("系统设置", icon: "Setting", tag: "system"));

        var titleBar = TitleBar("机耀处")
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
        // Unmount the current page while hidden so controls, page resources
        // and subscriptions can be collected; navigation selection is retained.
        var navView = (NavigationView([.. items], visible ? host : Grid(columns: [], rows: [])) with
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

        if (!visible && shell.Attached)
            return Grid(columns: [], rows: []);
        return Grid(columns: [GridSize.Star()], rows: [GridSize.Auto, GridSize.Star()],
            titleBar.Grid(row: 0, column: 0), navView.Grid(row: 1, column: 0))
            .Backdrop(BackdropKind.Mica);
    }
}
