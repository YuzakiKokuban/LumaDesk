using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using JiYaoChu.Interop;
using JiYaoChu.Model;
using JiYaoChu.Ui;

namespace JiYaoChu.Services;

internal static class SettingsVerification
{
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static IEnumerable<DependencyObject> Tree(DependencyObject root)
    {
        yield return root;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Tree(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private static async Task WaitAsync(Func<bool> ready, string reason)
    {
        var deadline = DateTime.UtcNow.AddSeconds(6);
        while (!ready()) { if (DateTime.UtcNow > deadline) throw new InvalidOperationException(reason + "\n" + (NativeLayout.VerificationNavigation is { } nav ? string.Join(" | ", Tree(nav).OfType<FrameworkElement>().Select(item => item is TextBlock text ? text.Text : item is Button button ? "Button:" + AutomationProperties.GetName(button) + ":" + button.IsEnabled : "").Where(text => text.Length > 0)) : "navigation missing")); await Task.Delay(30); }
    }
    private static void Invoke(Button button)
        => ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
    private static async Task CaptureAsync(ScrollViewer viewer, string path, string reportPath)
    {
        viewer.ChangeView(null, 0, null, disableAnimation: true);
        await Task.Delay(120);
        Require(viewer.ScrollableWidth < 1, "Page overflows horizontally at a narrow width");
        if (Environment.GetEnvironmentVariable("JIYAOCHU_VERIFY_SCREENSHOTS") == "0") return;
        GetWindowRect(BackgroundHost.Handle, out var bounds);
        await WritePreviewAsync(Path.ChangeExtension(reportPath, ".preview.json"), System.Text.Json.JsonSerializer.Serialize(new {
            page_capture_path = path, page_bounds = new { bounds.Left, bounds.Top, bounds.Right, bounds.Bottom } }));
        await WaitAsync(() => File.Exists(path), "Native page screenshot was not captured");
    }
    internal static async Task WritePreviewAsync(string path, string json)
    {
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, json);
        File.Move(temporary, path, overwrite: true);
    }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hwnd, out Rect rect);
    private static async Task<ScrollViewer> NavigateAsync(string tag, string heading)
    {
        var nav = NativeLayout.VerificationNavigation ?? throw new InvalidOperationException("Navigation control missing");
        nav.SelectedItem = nav.MenuItems.OfType<NavigationViewItem>().First(item => item.Tag?.ToString() == tag);
        ScrollViewer? viewer = null;
        await WaitAsync(() => {
            viewer = Tree(nav).OfType<ScrollViewer>().FirstOrDefault(scroll => scroll.Content is DependencyObject content && Tree(content).OfType<TextBlock>().Any(text => text.Text == heading && text.FontSize >= 25));
            return viewer is not null;
        }, "Page did not mount: " + heading);
        return viewer!;
    }
    public static async Task RunAsync(Dictionary<string, object> report, string reportPath)
    {
        Require(Backend.BackendName == "mock", "Settings checks require an isolated mock backend");
        var nav = NativeLayout.VerificationNavigation!;
        // Deliberately make the page short enough to scroll.
        var window = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(nav.XamlRoot.ContentIslandEnvironment.AppWindowId);
        var original = window.Size;
        var scale = nav.XamlRoot.RasterizationScale;
        window.Resize(new Windows.Graphics.SizeInt32((int)(760 * scale), (int)(600 * scale)));
        report["narrow_window_dip"] = new { width = 760, height = 600, scale };
        try
        {
            var lighting = await NavigateAsync("lighting", "键盘灯效");
            Button? brightness = null;
            await WaitAsync(() => {
                brightness = Tree(lighting).OfType<Button>().FirstOrDefault(button => AutomationProperties.GetName(button) == "亮度 低" && button.IsEnabled);
                return brightness is not null;
            }, "Lighting settings did not load: " + string.Join(" | ", Tree(lighting).OfType<TextBlock>().Select(text => text.Text)));
            lighting.ChangeView(null, Math.Min(120, lighting.ScrollableHeight), null, disableAnimation: true);
            await Task.Delay(150);
            var offset = lighting.VerticalOffset;
            var unloaded = false;
            brightness!.Unloaded += (_, _) => unloaded = true;
            brightness.Focus(FocusState.Programmatic);
            Invoke(brightness);
            await Task.Delay(25);
            Require(Tree(lighting).Contains(brightness) && !unloaded, "Applying brightness unmounted the controls");
            await WaitAsync(() => brightness.IsEnabled, "Brightness readback did not finish");
            await Task.Delay(100);
            Require(!unloaded && ReferenceEquals(lighting, Tree(nav).OfType<ScrollViewer>().First(scroll => scroll.Content is DependencyObject content && Tree(content).Contains(brightness))), "Lighting page was replaced");
            Require(Math.Abs(lighting.VerticalOffset - offset) < 2, "Applying lighting reset the scroll position");
            Require((await Backend.CallAsync<LightingState>("get_lighting_state")).KbBrightness == 1, "Brightness write/readback failed");
            report["lighting_controls_and_scroll_preserved"] = true;
            await CaptureAsync(lighting, reportPath + ".lighting.png", reportPath);

            var system = await NavigateAsync("system", "系统");
            ToggleSwitch? osdToggle = null;
            await WaitAsync(() => { osdToggle = Tree(system).OfType<ToggleSwitch>().LastOrDefault(); return osdToggle is not null && osdToggle.IsEnabled; }, "System settings did not load");
            // Prefer a stable, mounted numeric control whose value writes OSD configuration.
            var opacity = Tree(system).OfType<NumberBox>().First();
            system.ChangeView(null, Math.Min(300, system.ScrollableHeight), null, disableAnimation: true);
            await Task.Delay(150);
            offset = system.VerticalOffset;
            unloaded = false;
            opacity.Unloaded += (_, _) => unloaded = true;
            opacity.Value = 61;
            await Task.Delay(25);
            Require(!unloaded && Tree(system).Contains(opacity), "Applying OSD unmounted system settings");
            await WaitAsync(() => opacity.IsEnabled, "OSD readback did not finish");
            await Task.Delay(100);
            Require(!unloaded && Math.Abs(system.VerticalOffset - offset) < 2, "Applying OSD replaced the page or reset scrolling");
            Require((await Backend.CallAsync<OsdConfig>("get_osd_config")).Opacity == 61, "OSD opacity did not save");
            await Backend.CallAsync("save_osd_config", new { config = (await Backend.CallAsync<OsdConfig>("get_osd_config")) with { Opacity = 60 } });
            report["system_controls_and_scroll_preserved"] = true;
            await CaptureAsync(system, reportPath + ".system.png", reportPath);

            var tuning = await NavigateAsync("tuning", "电源管理");
            Button? office = null;
            await WaitAsync(() => { office = Tree(tuning).OfType<Button>().FirstOrDefault(button => AutomationProperties.GetName(button) == "办公" && button.IsEnabled); return office is not null; }, "Power settings did not load");
            unloaded = false;
            office!.Unloaded += (_, _) => unloaded = true;
            Invoke(office);
            await Task.Delay(25);
            Require(!unloaded, "Applying power mode unmounted the controls");
            await WaitAsync(() => office.IsEnabled, "Power readback did not finish");
            Require(!unloaded && (await Backend.CallAsync<PowerSettings>("get_power_settings")).PowerMode == 0, "Power mode did not update in place");
            report["power_controls_preserved"] = true;
            await CaptureAsync(tuning, reportPath + ".tuning.png", reportPath);

            var gpu = await NavigateAsync("gpu", "显卡模式");
            await WaitAsync(() => Tree(gpu).OfType<Button>().Any(button => AutomationProperties.GetName(button) == "保存显卡模式"), "GPU settings did not load");
            await CaptureAsync(gpu, reportPath + ".gpu.png", reportPath);
            var overview = await NavigateAsync("status", "状态概览");
            await CaptureAsync(overview, reportPath + ".overview.png", reportPath);
            report["all_pages_narrow_no_horizontal_overflow"] = true;

            // Failure keeps the last good resource rather than showing a blank page.
            var calls = 0;
            using var retained = new SettingsResource<string>(() => ++calls == 1 ? Task.FromResult("loaded") : Task.FromException<string>(new IOException("readback unavailable")));
            await retained.RefreshAsync();
            await retained.RefreshAsync();
            Require(retained.Snapshot.Value == "loaded" && retained.Snapshot.Error is not null, "Failed refresh discarded last good settings");
            report["failed_readback_keeps_settings"] = true;

            PowerPolicy.OnPowerSource(0);
            await Backend.CallAsync("set_power_automation", new { enabled = true, ac_mode = (byte)2, battery_mode = (byte)0 });
            await Task.Delay(200);
            Require((await Backend.CallAsync<PowerSettings>("get_power_settings")).PowerMode == 2, "Enabling AC rule did not apply it");
            PowerPolicy.OnPowerSource(1);
            await Task.Delay(200);
            Require((await Backend.CallAsync<PowerSettings>("get_power_settings")).PowerMode == 0, "Battery rule did not apply");
            await Backend.CallAsync("set_power_automation", new { enabled = false, ac_mode = (byte)2, battery_mode = (byte)0 });
            report["event_driven_power_rules"] = true;

            var backup = await SupportExport.ExportAsync(true, reveal: false);
            var diagnostics = await SupportExport.ExportAsync(false, reveal: false);
            using (var archive = System.IO.Compression.ZipFile.OpenRead(backup)) Require(archive.GetEntry("settings/config.json") is not null, "Backup omitted config");
            using (var archive = System.IO.Compression.ZipFile.OpenRead(diagnostics)) Require(archive.GetEntry("diagnostics.json") is not null, "Diagnostic report missing");
            report["backup_and_diagnostics_export"] = true;
            report["diagnostics_path"] = diagnostics;
            report["backup_path"] = backup;
            await NavigateAsync("status", "状态概览");
        }
        finally { window.Resize(original); }
    }
}
