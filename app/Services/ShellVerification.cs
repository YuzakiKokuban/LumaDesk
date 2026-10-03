using System.Runtime.InteropServices;
using System.Text.Json;
using JiYaoChu.Interop;
using JiYaoChu.Model;

namespace JiYaoChu.Services;

/// <summary>Opt-in integration checks, restricted to the isolated mock backend.</summary>
internal static class ShellVerification
{
    private static void Require(bool passed, string reason)
    {
        if (!passed) throw new InvalidOperationException(reason);
    }

    private static object MemorySample()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        return new { working_mib = Math.Round(process.WorkingSet64 / 1048576.0, 2), private_mib = Math.Round(process.PrivateMemorySize64 / 1048576.0, 2) };
    }

    public static async Task RunAsync(string reportPath)
    {
        var report = new Dictionary<string, object>();
        try
        {
            Require(Backend.BackendName == "mock", "Shell verification requires the mock backend");
            await Task.Delay(3500);
            var main = BackgroundHost.Handle;
            Require(main != 0 && !IsWindowVisible(main), "Background startup opened a window");
            Require(!MachineStore.IsActive && MachineStore.HardwareReads == 0, "Background startup scanned hardware");
            report["background_startup_reads"] = MachineStore.HardwareReads;
            report["background_memory"] = MemorySample();
            BackgroundHost.Show();
            await Task.Delay(3500);
            Require(MachineStore.IsActive && MachineStore.HardwareReads > 0, "Opening the page did not resume polling");
            report["visible_reads"] = MachineStore.HardwareReads;
            report["visible_memory"] = MemorySample();
            Require(main != 0 && IsWindowVisible(main), "Main window is missing");
            Require(IsZoomed(main), "Opening the window did not maximize it");
            ShowWindow(main, 9);
            await Task.Delay(350);
            GetWindowRect(main, out var restored);
            var screen = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(main), Microsoft.UI.Windowing.DisplayAreaFallback.Primary).OuterBounds;
            Require(restored.Right - restored.Left == screen.Width / 2 && restored.Bottom - restored.Top == screen.Height / 2,
                "Restored window is not half the display resolution");
            Require(Math.Abs(restored.Left - (screen.X + screen.Width / 4)) <= 1 && Math.Abs(restored.Top - (screen.Y + screen.Height / 4)) <= 1,
                "Restored window is not centered");
            report["maximized_and_half_screen_restore"] = true;
            BackgroundHost.Show();
            if (Environment.GetEnvironmentVariable("JIYAOCHU_VERIFY_SETTINGS") == "1")
                await SettingsVerification.RunAsync(report, reportPath);
            PostMessage(main, 0x10, 0, 0); // Close button / WM_CLOSE.
            await Task.Delay(1500);
            Require(!IsWindowVisible(main) && !MachineStore.IsActive, "Close did not hide to the tray");
            var reads = MachineStore.HardwareReads;
            await Task.Delay(3500);
            Require(MachineStore.HardwareReads == reads, "Hidden window kept scanning hardware");
            report["hidden_reads_before"] = reads;
            report["hidden_reads_after"] = MachineStore.HardwareReads;
            report["hidden_memory"] = MemorySample();
            PostMessage(main, 0x8001, 1, 0x203); // Native tray double-click callback.
            await Task.Delay(1000);
            Require(IsWindowVisible(main) && MachineStore.IsActive, "Tray double click did not reopen the page");
            var foreground = GetForegroundWindow();
            await Backend.CallAsync("trigger_osd_preview", new { kind = "power" });
            await Task.Delay(500);
            var osd = FindWindow(null, "LumaDesk OSD");
            Require(osd != 0 && IsWindowVisible(osd), "OSD preview did not create a visible window");
            Require(GetForegroundWindow() == foreground, "OSD stole keyboard focus");
            report["osd_nonactivating"] = true;
            var styles = GetWindowLongPtr(osd, -20).ToInt64();
            Require((styles & 0x08000000) != 0 && (styles & 0x20) != 0, "OSD is not nonactivating and click-through");
            report["osd_styles"] = styles;
            report["osd_frame_styles"] = GetWindowLongPtr(osd, -16).ToInt64();
            GetClientRect(osd, out var client);
            Require(GetLayeredWindowAttributes(osd, out _, out var alpha, out _) && alpha == 153,
                "Default OSD opacity is not 60 percent");
            Require(OsdOverlay.BorderConfigurationResult == 0, "OSD border configuration was rejected");
            report["osd_opacity_default"] = 60;
            report["osd_border_removed"] = true;
            GetWindowRect(osd, out var bounds);
            Require(client.Right == bounds.Right - bounds.Left && client.Bottom == bounds.Bottom - bounds.Top,
                "OSD retained a non-client inset at its edges");
            report["osd_client_fills_window"] = true;
            report["osd_bounds"] = new { bounds.Left, bounds.Top, bounds.Right, bounds.Bottom };
            // Give the external harness time to capture the actual rendered OSD.
            var previewReport = Path.ChangeExtension(reportPath, ".preview.json");
            await SettingsVerification.WritePreviewAsync(previewReport, JsonSerializer.Serialize(report));
            await Task.Delay(2500);
            Require(!IsWindowVisible(osd), "OSD did not disappear on its timer");
            ShowWindow(main, 6); // Minimize.
            await Task.Delay(1000);
            Require(!IsWindowVisible(main) && !MachineStore.IsActive, "Minimize did not pause monitoring");
            reads = MachineStore.HardwareReads;
            var config = await Backend.CallAsync<OsdConfig>("get_osd_config");
            await Backend.CallAsync("save_osd_config", new { config = config with { Opacity = 37 } });
            Require((await Backend.CallAsync<OsdConfig>("get_osd_config")).Opacity == 37,
                "Custom OSD opacity was not persisted");
            OsdOverlay.Show("自定义透明度", "37%");
            Require(GetLayeredWindowAttributes(osd, out _, out alpha, out _) && alpha == 94,
                "Custom OSD opacity was not applied to the window");
            report["osd_opacity_custom"] = 37;
            foreach (var key in new uint[] { 0x14, 0x90, 0x91 })
            {
                SystemOsdEvents.PublishLockState(key, true);
                Require(IsWindowVisible(osd) && OsdOverlay.VisibleNotice.Detail.Contains("已开启"),
                    "Lock-key event did not show its actual state");
                SystemOsdEvents.PublishLockState(key, false);
                Require(OsdOverlay.VisibleNotice.Detail.Contains("已关闭"), "Lock-key unlock was not displayed");
            }
            SystemOsdEvents.PublishAudioState(false, false, 0.42f);
            OsdOverlay.OnFirmwareNotice(0x36);
            Require(OsdOverlay.VisibleNotice.Detail == "42%", "Firmware companion replaced the actual volume");
            SystemOsdEvents.PublishDisplayBrightness(65);
            OsdOverlay.OnFirmwareNotice(0x14);
            Require(OsdOverlay.VisibleNotice.Title == "屏幕亮度" && OsdOverlay.VisibleNotice.Detail == "65%",
                "Brightness event did not display the actual percentage");
            report["brightness_percentage_osd"] = true;
            SystemOsdEvents.PublishPowerSource(0);
            SystemOsdEvents.PublishPowerSource(1);
            Require(OsdOverlay.VisibleNotice.Title == "供电状态" && OsdOverlay.VisibleNotice.Detail.Contains("电池"),
                "Battery transition did not show an OSD");
            SystemOsdEvents.PublishPowerSource(0);
            Require(OsdOverlay.VisibleNotice.Detail.Contains("电源"), "AC transition did not show an OSD");
            await Backend.CallAsync("save_osd_config", new { config = config with { ShowOnPowerChange = false } });
            await Backend.CallAsync("hide_osd_window");
            SystemOsdEvents.PublishPowerSource(1);
            Require(!IsWindowVisible(osd), "Power notice ignored its disabled preference");
            await Backend.CallAsync("save_osd_config", new { config = config with { Enabled = false } });
            SystemOsdEvents.PublishLockState(0x14, true);
            Require(!IsWindowVisible(osd), "Disabled OSD still displayed a lock event");
            await Task.Delay(1000);
            Require(MachineStore.HardwareReads == reads && !MachineStore.IsActive,
                "Event-driven OSD restarted hidden hardware scanning");
            report["hidden_event_reads"] = MachineStore.HardwareReads;
            report["lock_and_power_events"] = true;
            await Backend.CallAsync("set_device_switch", new { id = "win_key_lock", enabled = false });
            BackgroundHost.OnOemHotkey(0x40);
            await Task.Delay(150);
            BackgroundHost.OnOemHotkey(0xa5);
            await Task.Delay(400);
            Require((await Backend.CallAsync<IReadOnlyList<DeviceSwitch>>("get_device_switches")).First(item => item.Id == "win_key_lock").Enabled,
                "Fn+F3 companion packet reversed the lock");
            BackgroundHost.OnOemHotkey(0x40);
            await Task.Delay(150);
            BackgroundHost.OnOemHotkey(0xa5);
            await Task.Delay(400);
            Require(!(await Backend.CallAsync<IReadOnlyList<DeviceSwitch>>("get_device_switches")).First(item => item.Id == "win_key_lock").Enabled,
                "Repeating the Fn+F3 packet did not unlock");
            Require(MachineStore.HardwareReads == reads, "Hidden Win-key changes restarted scanning");
            report["fn_f3_packet_toggle"] = true;
            if (Environment.GetEnvironmentVariable("JIYAOCHU_VERIFY_SETTINGS") == "1")
            {
                var collections = BackgroundMemory.Collections;
                await Backend.CallAsync("save_osd_config", new { config = config with { Enabled = true, DurationMs = 600 } });
                for (var i = 0; i < 4; i++) { OsdOverlay.Show("连续提示", $"{i}"); await Task.Delay(650); }
                Require(BackgroundMemory.Collections == collections, "Each OSD timeout triggered a collection");
                report["osd_burst_does_not_collect"] = true;
                report["memory_collections"] = collections;
                report["last_collection_ms"] = BackgroundMemory.LastCollectionMs;
                var timings = new List<double>();
                for (var i = 0; i < 3; i++)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    BackgroundHost.Show();
                    var painted = new TaskCompletionSource();
                    Microsoft.UI.Reactor.ReactorApp.UIDispatcher!.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => painted.SetResult());
                    await painted.Task;
                    timings.Add(timer.Elapsed.TotalMilliseconds);
                    BackgroundHost.Hide();
                    await Task.Delay(100);
                }
                report["reopen_ui_queue_ms"] = timings;
                if (Environment.GetEnvironmentVariable("JIYAOCHU_VERIFY_MEMORY_IDLE") == "1")
                {
                    await Task.Delay(BackgroundMemory.IdleDelayMs + 700);
                    report["memory_after_idle_cooldown"] = MemorySample();
                    report["collections_after_idle"] = BackgroundMemory.Collections;
                }
            }
            using (var process = System.Diagnostics.Process.GetCurrentProcess())
            {
                var before = process.TotalProcessorTime;
                await Task.Delay(3000);
                process.Refresh();
                report["idle_cpu_seconds_over_3s"] = (process.TotalProcessorTime - before).TotalSeconds;
                report["final_hidden_memory"] = MemorySample();
            }
            report["passed"] = true;
        }
        catch (Exception error) { report["passed"] = false; report["error"] = error.ToString(); }
        finally
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            BackgroundHost.Exit();
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string? className, string title);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsZoomed(nint hwnd);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint hwnd, uint message, nuint w, nint l);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool GetLayeredWindowAttributes(nint hwnd, out uint color, out byte alpha, out uint flags);
}
