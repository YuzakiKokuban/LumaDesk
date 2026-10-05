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
    private static async Task CaptureAsync(ScrollViewer viewer, string path, string reportPath, double offset = 0)
    {
        viewer.ChangeView(null, offset, null, disableAnimation: true);
        await Task.Delay(120);
        Require(viewer.ScrollableWidth < 1, "Page overflows horizontally at a narrow width");
        if (Environment.GetEnvironmentVariable("JIYAOCHU_VERIFY_SCREENSHOTS") == "0") return;
        GetWindowRect(BackgroundHost.Handle, out var bounds);
        await WritePreviewAsync(Path.ChangeExtension(reportPath, ".preview.json"), System.Text.Json.JsonSerializer.Serialize(new {
            page_capture_path = path, page_handle = BackgroundHost.Handle.ToInt64(),
            page_bounds = new { bounds.Left, bounds.Top, bounds.Right, bounds.Bottom } }));
        await WaitAsync(() => File.Exists(path), "Native page screenshot was not captured");
    }
    internal static async Task WritePreviewAsync(string path, string json)
    {
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, json);
        for (var attempt = 0; ; attempt++)
        {
            try { File.Move(temporary, path, overwrite: true); break; }
            catch (IOException) when (attempt < 5) { await Task.Delay(30); }
            catch (UnauthorizedAccessException) when (attempt < 5) { await Task.Delay(30); }
        }
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
    private static async Task MeasureTrendRefreshAsync(Dictionary<string, object> report)
    {
        var graphs = new[] { new TrendGraphControl(), new TrendGraphControl(), new TrendGraphControl() };
        var host = new Grid { Width = 600, Height = 140 };
        foreach (var graph in graphs) host.Children.Add(graph);
        var dialog = new ContentDialog { Title = "趋势绘制验证", Content = host, CloseButtonText = "关闭", XamlRoot = NativeLayout.VerificationNavigation!.XamlRoot };
        // This isolated benchmark must mount native graphs outside the application render tree.
#pragma warning disable REACTOR_DIALOG_001
        var showing = dialog.ShowAsync();
#pragma warning restore REACTOR_DIALOG_001
        try
        {
            await WaitAsync(() => graphs.All(graph => graph.IsLoaded && graph.ActualWidth >= 500), "Trend benchmark did not mount on the live UI");
            report["trend_benchmark_live_width"] = graphs[0].ActualWidth;
            var at = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
            var samples = Enumerable.Range(0, TrendHistory.Capacity).Select(i => new TrendSample(at.AddMilliseconds(i * 500),
                40 + i % 40, i % 100, i % 17 == 0 ? null : 50 + i % 30, i % 100, 2000 + i % 3000, 2500 + i % 2500, i % 31 == 0)).ToArray();
            var metrics = new[] { TrendMetric.Temperature, TrendMetric.Load, TrendMetric.Fan };
            for (var i = 0; i < graphs.Length; i++)
            {
                graphs[i].Show(new(samples, metrics[i]));
                graphs[i].UpdateLayout();
            }
            var originalChildren = graphs.Select(graph => Tree(graph).ToArray()).ToArray();
            var timings = new List<double>();
            for (var tick = 0; tick < 30; tick++)
            {
                var next = samples.Select(sample => sample with { At = sample.At.AddSeconds(tick + 1) }).ToArray();
                var timer = System.Diagnostics.Stopwatch.StartNew();
                for (var i = 0; i < graphs.Length; i++) { graphs[i].Show(new(next, metrics[i])); graphs[i].UpdateLayout(); }
                timings.Add(timer.Elapsed.TotalMilliseconds);
            }
            timings.Sort();
            report["trend_refresh_600_samples_three_graphs_ms"] = new { median = timings[15], p95 = timings[28], maximum = timings[^1] };
            Require(graphs.Select((graph, i) => Tree(graph).SequenceEqual(originalChildren[i])).All(value => value), "Trend refresh rebuilds the native visual tree");
            report["trend_visual_tree_retained"] = true;
            var gaps = new[]
            {
                new TrendSample(at, 40, null, null, null, null, null, true),
                new TrendSample(at.AddSeconds(1), 41, null, null, null, null, null, false),
                new TrendSample(at.AddSeconds(2), 42, null, null, null, null, null, true),
                new TrendSample(at.AddSeconds(3), null, null, null, null, null, null, false),
                new TrendSample(at.AddSeconds(4), 44, null, null, null, null, null, false),
            };
            graphs[0].Show(new(gaps, TrendMetric.Temperature));
            var paths = Tree(graphs[0]).OfType<Microsoft.UI.Xaml.Shapes.Path>().ToArray();
            Require(paths[0].Data is PathGeometry { Figures.Count: 3 } && paths[1].Data is null, $"Graph bridges missing/hidden samples or draws unavailable GPU readings as zero (CPU={paths[0].Data?.GetType().Name}, figures={(paths[0].Data as PathGeometry)?.Figures.Count}, GPU={paths[1].Data?.GetType().Name})");
            var geometry = paths[0].Data;
            graphs[0].Show(new(gaps, TrendMetric.Temperature));
            Require(ReferenceEquals(paths[0].Data, geometry), "Unchanged trend data regenerated geometry");
            report["trend_gaps_and_unchanged_data_preserved"] = true;
        }
        finally { dialog.Hide(); await showing; }
    }
    public static async Task RunAsync(Dictionary<string, object> report, string reportPath)
    {
        Require(Backend.BackendName == "mock", "Settings checks require an isolated mock backend");
        var startupLighting = await Backend.CallAsync<LightingState>("get_lighting_state");
        Require(!startupLighting.FirmwareManaged && !startupLighting.Enabled && startupLighting.KbBrightness == 2 && startupLighting.KbColor == "#123456"
            && startupLighting.KbEngine == LightingEngine.Hardware && startupLighting.KbEffect == 0, "Startup did not claim saved lighting or changed the saved off/brightness/colour settings");
        report["keyboard_lighting_claimed_at_startup_saved_off_preserved"] = true;
        foreach (var (input, expected) in new[] { ("#12ABEF", "#12abef"), (" 123456 ", "#123456"), ("000000", "#000000"), ("#FFFFFF", "#ffffff") })
            Require(KeyboardColor.TryParse(input, out var parsed) && parsed == expected, "Valid keyboard color was not parsed: " + input);
        foreach (var input in new[] { "", "#abc", "#12345", "#1234567", "#GG0011", "red", "#12 34EF", "##123456" })
            Require(!KeyboardColor.TryParse(input, out _), "Invalid keyboard color accepted: " + input);
        Require(!OsdOverlay.WatchPhysicalProfile, "Background profile polling is enabled by default");
        report["strict_keyboard_color_parser"] = true;
        report["background_profile_polling_default_off"] = true;
        await DisplayTrendVerification.RunAsync(report);
        await MeasureTrendRefreshAsync(report);
        var nav = NativeLayout.VerificationNavigation!;
        var menu = nav.MenuItems.OfType<NavigationViewItem>().ToArray();
        Require(menu.Length == 5 && menu.Count(item => item.Tag?.ToString() == "display") == 1 && menu.All(item => item.Tag?.ToString() != "gpu"), "Display and GPU navigation were not merged");
        var powerIcon = menu.First(item => item.Tag?.ToString() == "tuning").Icon as FontIcon;
        Require(powerIcon is { FontSize: >= 28, Width: >= 24, Height: >= 24 }, "Power navigation icon was not enlarged");
        report["display_gpu_navigation_merged"] = true;
        report["power_navigation_icon_enlarged"] = true;
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
            await WaitAsync(() => lighting.ScrollableHeight > 0, "Lighting page layout did not settle");
            brightness!.Focus(FocusState.Programmatic);
            await Task.Delay(100);
            lighting.ChangeView(null, Math.Min(120, lighting.ScrollableHeight), null, disableAnimation: true);
            await Task.Delay(150);
            var offset = lighting.VerticalOffset;
            var lightingUnloaded = false;
            brightness.Unloaded += (_, _) => lightingUnloaded = true;
            Invoke(brightness);
            await Task.Delay(25);
            Require(Tree(lighting).Contains(brightness) && !lightingUnloaded, "Applying brightness unmounted the controls");
            await WaitAsync(() => brightness.IsEnabled, "Brightness readback did not finish");
            await Task.Delay(100);
            Require(!lightingUnloaded && ReferenceEquals(lighting, Tree(nav).OfType<ScrollViewer>().First(scroll => scroll.Content is DependencyObject content && Tree(content).Contains(brightness))), "Lighting page was replaced");
            Require(Math.Abs(lighting.VerticalOffset - offset) < 2, $"Applying lighting reset the scroll position (before={offset}, after={lighting.VerticalOffset}, extent={lighting.ScrollableHeight})");
            Require((await Backend.CallAsync<LightingState>("get_lighting_state")).KbBrightness == 1, "Brightness write/readback failed");
            report["lighting_controls_and_scroll_preserved"] = true;
            var customHex = Tree(lighting).OfType<TextBox>().FirstOrDefault(box => AutomationProperties.GetName(box) == "键盘颜色代码");
            var applyHex = Tree(lighting).OfType<Button>().FirstOrDefault(button => AutomationProperties.GetName(button) == "应用自定义键盘颜色");
            Require(customHex is not null && applyHex is not null, "Custom keyboard color controls missing");
            customHex!.Text = "#12ABEF";
            await WaitAsync(() => applyHex!.IsEnabled, "Valid keyboard hex color was rejected");
            Invoke(applyHex!);
            await WaitAsync(() => applyHex!.IsEnabled && (Tree(lighting).OfType<TextBlock>().Any(text => text.Text == "当前颜色：#12ABEF")), "Custom keyboard color did not read back");
            Require((await Backend.CallAsync<LightingState>("get_lighting_state")).KbColor == "#12abef", "Custom keyboard color was not applied");
            customHex.Text = "#GG0011";
            await WaitAsync(() => !applyHex!.IsEnabled, "Invalid keyboard hex color can write hardware");
            Require((await Backend.CallAsync<LightingState>("get_lighting_state")).KbColor == "#12abef", "Invalid color altered hardware");
            report["custom_keyboard_hex_validated_and_applied"] = true;
            customHex.Text = "#12ABEF";
            await CaptureAsync(lighting, reportPath + ".lighting.png", reportPath);
            await CaptureAsync(lighting, reportPath + ".colors.png", reportPath, lighting.ScrollableHeight);

            var system = await NavigateAsync("system", "系统");
            ToggleSwitch? osdToggle = null;
            await WaitAsync(() => { osdToggle = Tree(system).OfType<ToggleSwitch>().LastOrDefault(); return osdToggle is not null && osdToggle.IsEnabled; }, "System settings did not load");
            // Prefer a stable, mounted numeric control whose value writes OSD configuration.
            var opacity = Tree(system).OfType<NumberBox>().First();
            await WaitAsync(() => system.ScrollableHeight > 0, "System page layout did not settle");
            var opacityOffset = opacity.TransformToVisual(system).TransformPoint(new Windows.Foundation.Point()).Y + system.VerticalOffset - 80;
            system.ChangeView(null, Math.Max(0, opacityOffset), null, disableAnimation: true);
            await Task.Delay(150);
            offset = system.VerticalOffset;
            var systemUnloaded = false;
            opacity.Unloaded += (_, _) => systemUnloaded = true;
            opacity.Value = 61;
            await Task.Delay(25);
            Require(!systemUnloaded && Tree(system).Contains(opacity), "Applying OSD unmounted system settings");
            await WaitAsync(() => opacity.IsEnabled, "OSD readback did not finish");
            await Task.Delay(100);
            Require(!systemUnloaded && Math.Abs(system.VerticalOffset - offset) < 2, $"Applying OSD replaced the page or reset scrolling (unloaded={systemUnloaded}, before={offset}, after={system.VerticalOffset})");
            Require((await Backend.CallAsync<OsdConfig>("get_osd_config")).Opacity == 61, "OSD opacity did not save");
            await Backend.CallAsync("save_osd_config", new { config = (await Backend.CallAsync<OsdConfig>("get_osd_config")) with { Opacity = 60 } });
            report["system_controls_and_scroll_preserved"] = true;
            await CaptureAsync(system, reportPath + ".system.png", reportPath);
            await CaptureAsync(system, reportPath + ".restore.png", reportPath, system.ScrollableHeight);

            var tuning = await NavigateAsync("tuning", "电源管理");
            Button? office = null;
            await WaitAsync(() => { office = Tree(tuning).OfType<Button>().FirstOrDefault(button => AutomationProperties.GetName(button) == "办公" && button.IsEnabled); return office is not null; }, "Power settings did not load");
            var powerUnloaded = false;
            office!.Unloaded += (_, _) => powerUnloaded = true;
            Invoke(office);
            await Task.Delay(25);
            Require(!powerUnloaded, "Applying power mode unmounted the controls");
            await WaitAsync(() => office.IsEnabled, "Power readback did not finish");
            Require((await Backend.CallAsync<PowerSettings>("get_power_settings")).PowerMode == 0, "Power mode write/readback differs");
            Require(!powerUnloaded && Tree(tuning).Contains(office), "Power mode update replaced the selected control");
            report["power_controls_preserved"] = true;
            await Backend.CallAsync("set_power_mode", new { mode = (byte)2 });
            MachineStore.Refresh();
            await WaitAsync(() => Tree(tuning).OfType<Button>().Any(button => AutomationProperties.GetName(button) == "狂暴" && Tree(button).OfType<TextBlock>().Any(text => text.Text == "✓ 当前")), "External power change did not update selected card");
            Require(!powerUnloaded && Tree(tuning).Contains(office) && Tree(nav).Contains(tuning), "External power change replaced mounted controls");
            report["external_power_selection_synchronised"] = true;
            await CaptureAsync(tuning, reportPath + ".tuning.png", reportPath);

            var display = await NavigateAsync("display", "显示设置");
            await WaitAsync(() => Tree(display).OfType<Button>().Any(button => AutomationProperties.GetName(button) == "保存显卡模式"), "Merged GPU settings did not load");
            var originalGpuMode = (await Backend.CallAsync<GpuModeInfo>("get_gpu_mode_info")).ConfiguredMode;
            ComboBox? rates = null;
            await WaitAsync(() => { rates = Tree(display).OfType<ComboBox>().FirstOrDefault(box => AutomationProperties.GetName(box) == "显示刷新率" && box.IsEnabled); return rates is not null; }, "Display settings did not load");
            rates!.SelectedIndex = 0;
            var applyRate = Tree(display).OfType<Button>().First(button => AutomationProperties.GetName(button) == "应用显示刷新率");
            await WaitAsync(() => applyRate.IsEnabled, "Refresh rate selection cannot be applied");
            Invoke(applyRate);
            await WaitAsync(() => Tree(display).OfType<TextBlock>().Any(text => text.Text == "刷新率调整已完成"), "Refresh rate did not finish with actual readback");
            Require((await Backend.CallAsync<DisplayInfo[]>("get_displays"))[0].CurrentHz == 60, "Display refresh was not applied");
            foreach (var hz in new uint[] { 90, 120 })
            {
                await WaitAsync(() => rates.IsEnabled, "Refresh-rate readback did not finish");
                rates.SelectedIndex = rates.Items.IndexOf($"{hz} Hz");
                await WaitAsync(() => applyRate.IsEnabled, $"{hz} Hz selection cannot be applied");
                Invoke(applyRate);
                await WaitAsync(() => rates.IsEnabled && !applyRate.IsEnabled && rates.SelectedIndex >= 0 && rates.SelectedItem?.ToString() == $"{hz} Hz", $"{hz} Hz write/readback did not settle");
                Require((await Backend.CallAsync<DisplayInfo[]>("get_displays"))[0].CurrentHz == hz, $"{hz} Hz shortcut readback differs");
            }
            Require(!Tree(display).OfType<Button>().Any(button => AutomationProperties.GetName(button).StartsWith("刷新率档位")), "Redundant refresh shortcuts remain");
            report["refresh_dropdown_90_120_apply_and_readback"] = true;
            var panelBrightness = Tree(display).OfType<Slider>().First(box => AutomationProperties.GetName(box) == "内置屏幕亮度滑块");
            var brightnessUnloaded = false;
            panelBrightness.Unloaded += (_, _) => brightnessUnloaded = true;
            panelBrightness.Value = 37;
            await WaitAsync(() => Tree(display).OfType<TextBlock>().Any(text => text.Text == "屏幕亮度调整已完成"), "Panel brightness did not finish with actual readback");
            Require(await Backend.CallAsync<uint>("get_display_brightness") == 37, "Panel brightness readback differs");
            panelBrightness.Value = 20;
            await Task.Delay(10);
            Require(panelBrightness.IsEnabled && !brightnessUnloaded, "Brightness write interrupts the slider");
            foreach (var level in new[] { 25, 45, 65, 83 }) panelBrightness.Value = level;
            await WaitAsync(() => Tree(display).OfType<TextBlock>().Any(text => text.Text == "83%"), "Continuous brightness adjustment lost the last value");
            Require(await Backend.CallAsync<uint>("get_display_brightness") == 83 && panelBrightness.Value == 83 && !brightnessUnloaded, "Continuous slider write/readback differs");
            // Returning to the starting level before a render must still supersede pending writes.
            panelBrightness.Value = 20;
            panelBrightness.Value = 25;
            panelBrightness.Value = 83;
            await Task.Delay(150);
            await WaitAsync(() => panelBrightness.Value == 83 && Tree(display).OfType<TextBlock>().Any(text => text.Text == "83%"), "Fast brightness drag back to the starting level lost the last request");
            Require(await Backend.CallAsync<uint>("get_display_brightness") == 83, "Fast return to original brightness left an intermediate level applied");
            Require(!Tree(display).OfType<NumberBox>().Any(box => AutomationProperties.GetName(box) == "内置屏幕亮度百分比") && !Tree(display).OfType<Button>().Any(button => AutomationProperties.GetName(button) == "应用内置屏幕亮度"), "Brightness still requires a separate apply control");
            report["brightness_slider_live_latest_value_preserved"] = true;
            Require((await Backend.CallAsync<GpuModeInfo>("get_gpu_mode_info")).ConfiguredMode == originalGpuMode, "Brightness or refresh adjustment changed GPU output mode");
            report["display_controls_apply_and_readback"] = true;
            await CaptureAsync(display, reportPath + ".display.png", reportPath);
            var gpuHeading = Tree(display).OfType<TextBlock>().First(text => text.Text == "显卡输出模式");
            var gpuOffset = gpuHeading.TransformToVisual(display).TransformPoint(new Windows.Foundation.Point()).Y + display.VerticalOffset - 30;
            await CaptureAsync(display, reportPath + ".gpu.png", reportPath, Math.Max(0, gpuOffset));
            var overview = await NavigateAsync("status", "状态概览");
            await CaptureAsync(overview, reportPath + ".overview.png", reportPath);
            var plots = Tree(overview).OfType<TrendGraphControl>().ToArray();
            Require(plots.Length == 3 && plots.All(plot => AutomationProperties.GetName(plot).Contains("最近五分钟趋势")), "Native trend graphs did not mount with accessible summaries");
            var plotOffset = plots[0].TransformToVisual(overview).TransformPoint(new Windows.Foundation.Point()).Y + overview.VerticalOffset - 70;
            await CaptureAsync(overview, reportPath + ".trends.png", reportPath, Math.Max(0, plotOffset));
            Require(plots.Any(plot => Tree(plot).OfType<Microsoft.UI.Xaml.Shapes.Path>().Any(path => path.Data is PathGeometry { Figures.Count: > 0 })), "Native trend graph did not draw sample lines");
            overview.ChangeView(null, 0, null, disableAnimation: true);
            await Task.Delay(150);
            var offscreenPaths = plots.SelectMany(plot => Tree(plot).OfType<Microsoft.UI.Xaml.Shapes.Path>()).ToArray();
            var offscreenData = offscreenPaths.Select(path => path.Data).ToArray();
            await Task.Delay(1200);
            Require(offscreenPaths.Select((path, i) => ReferenceEquals(path.Data, offscreenData[i])).All(value => value), "Offscreen trend graphs keep rebuilding geometry");
            overview.ChangeView(null, Math.Max(0, plotOffset), null, disableAnimation: true);
            await WaitAsync(() => offscreenPaths.Select((path, i) => !ReferenceEquals(path.Data, offscreenData[i])).Any(value => value), "Trend graphs did not redraw on returning to the viewport");
            report["offscreen_trends_deferred_and_resumed"] = true;
            Require(!Tree(overview).OfType<Button>().Any(button => AutomationProperties.GetName(button) == "清空最近五分钟趋势"), "Clear trends button remains");
            report["native_trends_render_without_clear"] = true;
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
            await RestoreVerification.RunAsync(report);
            await NavigateAsync("status", "状态概览");
        }
        finally { window.Resize(original); }
    }
}
