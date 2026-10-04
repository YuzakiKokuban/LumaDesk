using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using JiYaoChu.Interop;
using JiYaoChu.Model;
using JiYaoChu.Services;
using JiYaoChu.Ui;
using DisplayInfo = JiYaoChu.Model.DisplayInfo;
using static Microsoft.UI.Reactor.Factories;

namespace JiYaoChu.Pages;

public sealed record DisplayBundle(IReadOnlyList<DisplayInfo> Displays, uint? Brightness, string? DisplayError = null, string? BrightnessError = null);

/// <summary>Device-specific Windows display modes and system built-in backlight.</summary>
public sealed class DisplayPage : SettingsPage
{
    public override Element Render()
    {
        var (reader, resource) = UseSettings(() => ReadAsync(null));
        var (device, setDevice) = UseState("");
        var (rate, setRate) = UseState<uint?>(null);
        var (draftBrightness, setDraftBrightness) = UseState<double?>(null);
        var (busy, setBusy) = UseState(false);
        var (failure, setFailure) = UseState<string?>(null);
        var (applied, setApplied) = UseState<string?>(null);
        UseEffect(() => EventBus.Subscribe(raised =>
        {
            if (!MachineStore.IsActive || !BackgroundHost.IsVisible) return;
            if (raised.Name == "osd://system" && raised.Payload?["kind"]?.GetValue<string>() == "brightness")
                _ = reader.RefreshAsync(value => ReadAsync(value, displays: false, brightness: true));
            else if (raised.Name == "command://applied")
            {
                var command = raised.Payload?["command"]?.GetValue<string>();
                if (command == "set_display_brightness")
                    _ = reader.RefreshAsync(value => ReadAsync(value, displays: false, brightness: true));
                else if (command is "set_display_monitor_refresh_rate" or "switch_refresh_rate")
                    _ = reader.RefreshAsync(value => ReadAsync(value, displays: true, brightness: false));
            }
        }), []);
        var bundle = resource.Value;
        var monitors = bundle?.Displays ?? [];
        var chosenDevice = device.Length == 0 ? monitors.FirstOrDefault()?.DeviceName ?? "" : device;
        var chosen = DisplayPolicy.Find(monitors, chosenDevice);
        var rates = chosen is null ? [] : DisplayPolicy.Rates(chosen);
        var currentChoice = rate ?? chosen?.CurrentHz;
        var brightness = draftBrightness ?? bundle?.Brightness;
        var working = busy || resource.Refreshing;
        UseEffect(() => { if (device.Length == 0 && chosenDevice.Length > 0) setDevice(chosenDevice); }, chosenDevice);
        UseEffect(() => { setRate(null); }, chosenDevice, chosen?.CurrentHz);
        UseEffect(() => { setDraftBrightness(bundle?.Brightness); }, bundle?.Brightness);

        async Task Reload()
        {
            setFailure(null); setApplied(null);
            await reader.RefreshAsync(value => ReadAsync(value));
        }
        void Settled(string? error, string? status) { setFailure(error); setApplied(status); }
        void ApplyRate()
        {
            if (working || bundle?.DisplayError is not null || currentChoice is not { } hz || !DisplayPolicy.CanApply(monitors, chosenDevice, hz)) return;
            Act.Fire("set_display_monitor_refresh_rate", async () =>
            {
                // Re-enumerate before writing, so an unplugged selection cannot target another display.
                var fresh = await Backend.CallAsync<DisplayInfo[]>("get_displays");
                if (!DisplayPolicy.CanApply(fresh, chosenDevice, hz))
                {
                    await reader.RefreshAsync(value => Task.FromResult(value with { Displays = fresh, DisplayError = null }));
                    throw new InvalidOperationException("所选显示器已断开，或该刷新率已不可用。请重新选择。");
                }
                try { await Backend.CallAsync("set_display_monitor_refresh_rate", new { device_name = chosenDevice, hz }); }
                finally { await reader.RefreshAsync(value => ReadAsync(value, displays: true, brightness: false)); }
                var readback = reader.Snapshot.Value;
                if (readback?.DisplayError is { } error) throw new InvalidOperationException("刷新率写入后读回失败：" + error);
                if (DisplayPolicy.Find(readback?.Displays ?? [], chosenDevice) is not { } actual) throw new InvalidOperationException("显示器已断开，请重新检测。");
                if (actual.CurrentHz != hz) throw new InvalidOperationException($"请求 {hz} Hz，实际读回 {actual.CurrentHz} Hz。请检查显示模式。");
                setRate(null);
            }, Settled, setBusy)();
        }
        void ApplyBrightness()
        {
            if (working || bundle?.BrightnessError is not null || brightness is not { } value || DisplayPolicy.Brightness(value) is not { } level || bundle?.Brightness is null) return;
            Act.Fire("set_display_brightness", async () =>
            {
                try { await Backend.CallAsync("set_display_brightness", new { level }); }
                finally { await reader.RefreshAsync(previous => ReadAsync(previous, displays: false, brightness: true)); }
                var actual = reader.Snapshot.Value;
                if (actual?.BrightnessError is { } error) throw new InvalidOperationException("亮度写入后读回失败：" + error);
                if (actual?.Brightness is not { } readback) throw new InvalidOperationException("无法读取内置屏幕亮度。");
                setDraftBrightness(readback);
                if (readback != level) throw new InvalidOperationException($"请求亮度 {level}%，实际读回 {readback}%。");
            }, Settled, setBusy)();
        }

        var names = monitors.Select(display => string.IsNullOrWhiteSpace(display.FriendlyName) ? display.DeviceName : $"{display.FriendlyName} · {display.DeviceName}").ToList();
        var selectedIndex = Array.FindIndex(monitors.ToArray(), display => display.DeviceName == chosenDevice);
        if (chosen is null && chosenDevice.Length > 0) { selectedIndex = names.Count; names.Add($"已断开 · {chosenDevice}"); }
        var controlsEnabled = !working && chosen is not null && bundle?.DisplayError is null;
        var brightnessEnabled = !working && bundle?.Brightness is not null && bundle.BrightnessError is null;
        var displayRows = new List<Element>
        {
            Body("选择显示器后，可应用它在当前分辨率下支持的刷新率。").Foreground(Theme.SecondaryText).TextWrapping(TextWrapping.Wrap),
            ComboBox(names.ToArray(), Optional<int>.Of(selectedIndex), index =>
            {
                if (working || index < 0 || index >= monitors.Count) return;
                setDevice(monitors[index].DeviceName); setRate(null); setFailure(null); setApplied(null);
            }).AutomationName("显示器选择").HAlign(HorizontalAlignment.Stretch).IsEnabled(!working && monitors.Count > 0),
            Chrome.Field("当前刷新率", chosen is null || chosen.CurrentHz == 0 ? "未知" : $"{chosen.CurrentHz} Hz"),
            ComboBox(rates.Select(hz => $"{hz} Hz").ToArray(), Optional<int>.Of(currentChoice is { } selectedHz ? Array.IndexOf(rates, selectedHz) : -1), index =>
            { if (index >= 0 && index < rates.Length) setRate(rates[index]); })
                .AutomationName("显示刷新率").HAlign(HorizontalAlignment.Stretch).IsEnabled(controlsEnabled && rates.Length > 0),
            Button("应用刷新率", ApplyRate).AutomationName("应用显示刷新率").HAlign(HorizontalAlignment.Left)
                .IsEnabled(controlsEnabled && currentChoice is { } picked && picked != chosen!.CurrentHz && DisplayPolicy.CanApply(monitors, chosenDevice, picked)),
        };
        if (bundle?.DisplayError is { } displayError) displayRows.Add(Chrome.Notice("显示器读取失败", "保留上次读取结果；重新检测后恢复操作。" + displayError, InfoBarSeverity.Warning));
        else if (resource.Refreshing && bundle is null) displayRows.Add(Caption("正在检测显示器…").Foreground(Theme.SecondaryText));
        else if (chosen is null) displayRows.Add(Chrome.Notice("显示器不可用", "所选设备已断开，或尚未检测到活动显示器。重新检测后选择设备。", InfoBarSeverity.Warning));
        else if (rates.Length == 0) displayRows.Add(Caption("未读取到可用刷新率，请重新检测。").Foreground(Theme.SecondaryText));

        var brightnessRows = new List<Element>
        {
            Body("只控制 Windows 内置屏幕背光，与上方显示器选择无关；外接显示器亮度请使用显示器自身菜单。").Foreground(Theme.SecondaryText).TextWrapping(TextWrapping.Wrap),
            Chrome.Field("当前亮度", bundle?.Brightness is { } actual ? $"{actual}%" : "未知"),
            Slider(brightness is { } level ? Optional<double>.Of(level) : default, 0, 100, value => { if (double.IsFinite(value)) setDraftBrightness(Math.Round(value)); })
                .StepFrequency(1).AutomationName("内置屏幕亮度滑块").IsEnabled(brightnessEnabled),
            Chrome.SettingRow("亮度 (%)", "输入 0–100，应用后读取实际亮度。",
                NumberBox(brightness is { } number ? Optional<double>.Of(number) : default, value => { if (DisplayPolicy.Brightness(value) is { } valid) setDraftBrightness(valid); })
                    .Range(0, 100).SpinButtons().Width(120).AutomationName("内置屏幕亮度百分比").IsEnabled(brightnessEnabled)),
            Button("应用亮度", ApplyBrightness).AutomationName("应用内置屏幕亮度").HAlign(HorizontalAlignment.Left)
                .IsEnabled(brightnessEnabled && brightness is { } draft && DisplayPolicy.Brightness(draft) != bundle?.Brightness),
        };
        if (bundle?.BrightnessError is { } brightnessError) brightnessRows.Add(Chrome.Notice("内置屏幕亮度不可用", "请重新检测后重试。" + brightnessError, InfoBarSeverity.Warning));
        return Chrome.Page("显示设置", "刷新率、内置屏幕亮度与显卡输出模式",
            Button(resource.Refreshing ? "正在重新检测…" : "重新检测显示器与亮度", () => _ = Reload()).AutomationName("重新检测显示设置").HAlign(HorizontalAlignment.Left).IsEnabled(!working),
            Chrome.Feedback(failure ?? resource.Error, applied),
            Chrome.SectionCard("显示器与刷新率", [.. displayRows]),
            Chrome.SectionCard("内置屏幕亮度", [.. brightnessRows]),
            Component<GpuSettingsSection>().WithKey("display-gpu-settings"));
    }

    private static async Task<DisplayBundle> ReadAsync(DisplayBundle? previous, bool displays = true, bool brightness = true)
    {
        var list = previous?.Displays ?? [];
        var level = previous?.Brightness;
        var displayError = previous?.DisplayError;
        var brightnessError = previous?.BrightnessError;
        if (displays)
        {
            try { list = await Backend.CallAsync<DisplayInfo[]>("get_displays"); displayError = null; }
            catch (Exception error) { displayError = error.Message; StartupLog.Write(error); }
        }
        if (brightness)
        {
            try
            {
                var actual = await Backend.CallAsync<uint>("get_display_brightness");
                if (actual > 100) throw new InvalidOperationException("亮度读数超出 0–100%。");
                level = actual; brightnessError = null;
            }
            catch (Exception error) { brightnessError = error.Message; StartupLog.Write(error); }
        }
        return new(list, level, displayError, brightnessError);
    }
}
