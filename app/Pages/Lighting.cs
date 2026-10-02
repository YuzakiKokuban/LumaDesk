using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml.Controls;
using JiYaoChu.Interop;
using JiYaoChu.Model;
using JiYaoChu.Services;
using JiYaoChu.Ui;
using static Microsoft.UI.Reactor.Factories;

namespace JiYaoChu.Pages;

public sealed class LightingPage : Component
{
    public override Element Render()
    {
        var (revision, setRevision) = UseState(0);
        var (busy, setBusy) = UseState(false);
        var (failure, setFailure) = UseState<string?>(null);
        var (draft, setDraft) = UseState<LightingState?>(null);
        var resource = UseResource(_ => Backend.CallAsync<LightingState>("get_lighting_state"), deps: [revision]);
        void Apply(LightingState state)
        {
            Act.Fire("apply_keyboard_lighting",
                () => Backend.CallAsync("apply_keyboard_lighting", new { lighting = state with { KbEngine = LightingEngine.Hardware, KbEffect = 0, FirmwareManaged = false } }),
                (error, _) => { setFailure(error); if (error is null) { setDraft(state with { FirmwareManaged = false }); setRevision(revision + 1); MachineStore.Refresh(); } }, setBusy)();
        }
        return resource.Match<Element>(
            () => Chrome.Page("键盘灯效", "正在读取键盘…", ProgressRing()),
            loaded => {
                var state = draft ?? loaded;
                var colors = Palettes.Keyboard.Select(palette => Chrome.Swatch(palette.Hex, palette.Label,
                    Hex.Normalise(state.KbColor) == palette.Hex, !busy,
                    () => Apply(state with { KbColor = palette.Hex, Enabled = true, KbBrightness = Math.Max(1u, state.KbBrightness) }))).ToArray();
                return Chrome.Page("键盘灯效", "耀世 15 Air · EC RGB",
                    failure is null ? null! : Chrome.Notice("操作失败", failure, InfoBarSeverity.Error),
                    state.FirmwareManaged ? Chrome.Notice("当前由固件控制", "亮度开关尚不能直接回读。选择亮度或颜色后由机耀处接管背光。", InfoBarSeverity.Informational) : null!,
                    Chrome.SectionCard("背光",
                        Chrome.SettingRow("键盘背光", "单色常亮",
                            ToggleSwitch(Optional<bool>.Of(state.Enabled), value => { if (value != state.Enabled) Apply(state with { Enabled = value, KbBrightness = Math.Max(1u, state.KbBrightness), FirmwareManaged = false }); }).IsEnabled(!busy)),
                        Chrome.Rule(),
                        Chrome.SettingRow("亮度", "关闭、低、中、高、最高",
                            ComboBox(new[] { "关闭", "低", "中", "高", "最高" }, Optional<int>.Of(state.Enabled ? (int)state.KbBrightness : 0),
                                index => { if (index is >= 0 and <= 4 && index != (state.Enabled ? (int)state.KbBrightness : 0)) Apply(state with { Enabled = index > 0, KbBrightness = (uint)index, FirmwareManaged = false }); }).MinWidth(120).IsEnabled(!busy)),
                        Chrome.Rule(), Body("颜色").SemiBold(), Chrome.ChoiceGrid(4, colors),
                        Caption("当前颜色：" + state.KbColor.ToUpperInvariant()).Foreground(Theme.SecondaryText)),
                    Chrome.SectionCard("硬件能力",
                        Body("已接入背光开关、五档亮度和单色颜色。动态灯效、单键和分区控制仍在适配。")
                            .TextWrapping(Microsoft.UI.Xaml.TextWrapping.Wrap).Foreground(Theme.SecondaryText)));
            },
            error => Chrome.Page("键盘灯效", null, Chrome.Notice("无法读取键盘", error.Message, InfoBarSeverity.Warning)));
    }
}
