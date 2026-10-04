using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using JiYaoChu.Interop;
using JiYaoChu.Model;
using JiYaoChu.Services;
using JiYaoChu.Ui;
using static Microsoft.UI.Reactor.Factories;

namespace JiYaoChu.Pages;

public sealed class LightingPage : SettingsPage
{
    public override Element Render()
    {
        var (busy, setBusy) = UseState(false);
        var (failure, setFailure) = UseState<string?>(null);
        var (colorInput, setColorInput) = UseState("");
        var (reader, resource) = UseSettings(() => Backend.CallAsync<LightingState>("get_lighting_state"));
        UseEffect(() => EventBus.Subscribe(raised =>
        {
            if (raised.Name == "command://applied" && raised.Payload?["command"]?.GetValue<string>() == "apply_keyboard_lighting"
                || raised.Name == "osd://system" && raised.Payload?["kind"]?.GetValue<string>() == "keyboard_light") reader.Refresh();
        }), []);
        void Apply(LightingState state)
        {
            if (busy || resource.Refreshing) return;
            Act.Fire("apply_keyboard_lighting",
                async () => {
                    try { await Backend.CallAsync("apply_keyboard_lighting", new { lighting = state with { KbEngine = LightingEngine.Hardware, KbEffect = 0, FirmwareManaged = false } }); }
                    finally { await reader.RefreshAsync(); }
                },
                (error, _) => setFailure(error), setBusy)();
        }
        return resource.Match<Element>(
            () => Chrome.Page("键盘灯效", "正在读取键盘…", ProgressRing()),
            loaded => {
                var state = loaded;
                var validColor = KeyboardColor.TryParse(colorInput, out var customColor);
                var working = busy || resource.Refreshing;
                var colors = Palettes.Keyboard.Select(palette => Chrome.Swatch(palette.Hex, palette.Label,
                    Hex.Normalise(state.KbColor) == palette.Hex, !working,
                    () => Apply(state with { KbColor = palette.Hex, Enabled = true, KbBrightness = Math.Max(1u, state.KbBrightness) }))).ToArray();
                return Chrome.Page("键盘灯效", "耀世 15 Air · EC RGB",
                    Chrome.Feedback(failure ?? resource.Error, null),
                    state.FirmwareManaged ? Chrome.Notice("固件默认背光", "选择亮度或颜色即可接管。", InfoBarSeverity.Informational) : null!,
                    Chrome.SectionCard("背光",
                        Chrome.SettingRow("键盘背光", state.FirmwareManaged ? "固件默认" : "单色常亮",
                            state.FirmwareManaged
                                ? Button("启用控制", () => Apply(state with { Enabled = true, KbBrightness = Math.Max(1u, state.KbBrightness) })).IsEnabled(!working)
                                : ToggleSwitch(Optional<bool>.Of(state.Enabled), value => { if (value != state.Enabled) Apply(state with { Enabled = value, KbBrightness = Math.Max(1u, state.KbBrightness), FirmwareManaged = false }); }).IsEnabled(!working)),
                        Chrome.Rule(),
                        Body("亮度").SemiBold(),
                        FlexRow([.. new[] { "关闭", "低", "中", "高", "最高" }.Select((label, index) =>
                            Chrome.CompactChoice(label, !state.FirmwareManaged && index == (state.Enabled ? (int)state.KbBrightness : 0), !working,
                                () => Apply(state with { Enabled = index > 0, KbBrightness = (uint)index, FirmwareManaged = false })))]) with
                            { Wrap = FlexWrap.Wrap, ColumnGap = 8, RowGap = 8 },
                        Chrome.Rule(), Body("颜色").SemiBold(),
                        FlexRow(colors) with { Wrap = FlexWrap.Wrap, ColumnGap = 8, RowGap = 8 },
                        Caption("当前颜色：" + state.KbColor.ToUpperInvariant()).Foreground(Theme.SecondaryText)),
                    Chrome.SectionCard("自定义颜色",
                        TextBox(Optional<string>.Of(colorInput), setColorInput, "例如 #12ABEF").AutomationName("键盘颜色代码").IsEnabled(!working),
                        HStack(10,
                            (validColor ? Border(null).Background(customColor) : Border(null).Background(Theme.CardBackground)).Width(28).Height(28).CornerRadius(6).WithBorder(Theme.CardStroke, 1),
                            Body(validColor ? customColor.ToUpperInvariant() : "输入六位十六进制颜色，可省略 #").TextWrapping(TextWrapping.Wrap)),
                        colorInput.Length > 0 && !validColor ? Caption("颜色代码应为六位 0–9、A–F，例如 #12ABEF。").Foreground(Theme.SystemCritical).TextWrapping(TextWrapping.Wrap) : (Element)VStack(),
                        Button("应用颜色", () => { if (KeyboardColor.TryParse(colorInput, out var color)) Apply(state with { KbColor = color, Enabled = true, KbBrightness = Math.Max(1u, state.KbBrightness) }); })
                            .AccentButton().AutomationName("应用自定义键盘颜色").IsEnabled(validColor && !working),
                        Caption("显示硬件读回颜色；EC 会量化 RGB，实际颜色可能与输入略有差异。").Foreground(Theme.SecondaryText).TextWrapping(TextWrapping.Wrap)),
                    Caption("支持单色常亮。颜色与亮度立即生效。").Foreground(Theme.SecondaryText));
            },
            error => Chrome.Page("键盘灯效", null, Chrome.Notice("无法读取键盘", error.Message, InfoBarSeverity.Warning)));
    }
}
