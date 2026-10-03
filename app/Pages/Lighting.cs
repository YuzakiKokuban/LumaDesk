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
        var (reader, resource) = UseSettings(() => Backend.CallAsync<LightingState>("get_lighting_state"));
        void Apply(LightingState state)
        {
            if (busy) return;
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
                var colors = Palettes.Keyboard.Select(palette => Chrome.Swatch(palette.Hex, palette.Label,
                    Hex.Normalise(state.KbColor) == palette.Hex, !busy,
                    () => Apply(state with { KbColor = palette.Hex, Enabled = true, KbBrightness = Math.Max(1u, state.KbBrightness) }))).ToArray();
                return Chrome.Page("键盘灯效", "耀世 15 Air · EC RGB",
                    Chrome.Feedback(failure ?? resource.Error, null),
                    state.FirmwareManaged ? Chrome.Notice("固件默认背光", "选择亮度或颜色即可接管。", InfoBarSeverity.Informational) : null!,
                    Chrome.SectionCard("背光",
                        Chrome.SettingRow("键盘背光", state.FirmwareManaged ? "固件默认" : "单色常亮",
                            state.FirmwareManaged
                                ? Button("启用控制", () => Apply(state with { Enabled = true, KbBrightness = Math.Max(1u, state.KbBrightness) })).IsEnabled(!busy)
                                : ToggleSwitch(Optional<bool>.Of(state.Enabled), value => { if (value != state.Enabled) Apply(state with { Enabled = value, KbBrightness = Math.Max(1u, state.KbBrightness), FirmwareManaged = false }); }).IsEnabled(!busy)),
                        Chrome.Rule(),
                        Body("亮度").SemiBold(),
                        FlexRow([.. new[] { "关闭", "低", "中", "高", "最高" }.Select((label, index) =>
                            Chrome.CompactChoice(label, !state.FirmwareManaged && index == (state.Enabled ? (int)state.KbBrightness : 0), !busy,
                                () => Apply(state with { Enabled = index > 0, KbBrightness = (uint)index, FirmwareManaged = false })))]) with
                            { Wrap = FlexWrap.Wrap, ColumnGap = 8, RowGap = 8 },
                        Chrome.Rule(), Body("颜色").SemiBold(),
                        FlexRow(colors) with { Wrap = FlexWrap.Wrap, ColumnGap = 8, RowGap = 8 },
                        Caption("当前颜色：" + state.KbColor.ToUpperInvariant()).Foreground(Theme.SecondaryText)),
                    Caption("支持单色常亮。颜色与亮度立即生效。").Foreground(Theme.SecondaryText));
            },
            error => Chrome.Page("键盘灯效", null, Chrome.Notice("无法读取键盘", error.Message, InfoBarSeverity.Warning)));
    }
}
