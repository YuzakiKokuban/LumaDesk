using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml.Controls;
using JiYaoChu.Interop;
using JiYaoChu.Model;
using JiYaoChu.Services;
using JiYaoChu.Ui;
using static Microsoft.UI.Reactor.Factories;

namespace JiYaoChu.Pages;

public sealed class GpuPage : SettingsPage
{
    public override Element Render()
    {
        var (busy, setBusy) = UseState(false);
        var (failure, setFailure) = UseState<string?>(null);
        var (draft, setDraft) = UseState<GpuMode?>(null);
        var (confirmRestart, setConfirmRestart) = UseState(false);
        var machine = UseExternalStore(MachineStore.Subscribe, () => MachineStore.Snapshot);
        var (reader, resource) = UseSettings(() => Backend.CallAsync<GpuModeInfo>("get_gpu_mode_info"));
        var info = resource.Match<GpuModeInfo?>(() => null, value => value, _ => null);
        var current = info?.ConfiguredMode;
        var target = draft ?? current;
        var pending = info?.PendingReboot == true;

        void Settled(string? error, string? success)
        {
            setFailure(error);
            if (success is not null) setDraft(null);
            reader.Refresh();
            MachineStore.Refresh();
        }

        var sections = new List<Element>();
        sections.Add(Chrome.Feedback(failure ?? resource.Error, null));
        if (pending)
        {
            sections.Add(Chrome.SectionCard("模式已保存，重启后生效",
                Body("请先保存正在编辑的文件。固件配置已更新，当前显示路由可能尚未改变。").TextWrapping(Microsoft.UI.Xaml.TextWrapping.Wrap),
                confirmRestart
                    ? HStack(10,
                        Button("确认重启（30 秒后）", Act.Fire("restart_system", () => Backend.CallAsync("restart_system"),
                            (error, _) => setFailure(error), setBusy)).AccentButton().IsEnabled(!busy),
                        Button("取消", () => setConfirmRestart(false)).SubtleButton())
                    : Button("准备重启", () => setConfirmRestart(true)).AccentButton().IsEnabled(!busy)));
        }

        sections.Add(resource.Match<Element>(
            () => Chrome.SectionCard("输出模式", HStack(12, ProgressRing().IsActive(true), Body("正在读取固件配置…"))),
            value => Chrome.SectionCard("输出模式",
                HStack(10, Body($"已保存：{GpuModes.Label(value.ConfiguredMode)}").SemiBold(),
                    Chrome.Pill("固件读取", InfoBarSeverity.Informational)),
                Body("选择模式后点击保存。更改将在下次重启时由固件应用。").Foreground(Theme.SecondaryText).TextWrapping(Microsoft.UI.Xaml.TextWrapping.Wrap),
                !value.RebootStatusKnown ? Caption("暂时无法确认系统启动时间，待重启状态可能需要重新检查。").Foreground(Theme.SecondaryText) : Grid([], []),
                Chrome.ChoiceRow([.. GpuModes.All.Select(mode => Chrome.ChoiceCard(
                    GpuModes.Name(mode), GpuModes.Tag(mode), GpuModes.Description(mode),
                    selected: mode == target,
                    enabled: !busy && value.Supported && (mode != GpuMode.Igpu || value.SupportsIgpu),
                    onClick: () => { setDraft(mode); setFailure(null); }, badge: "已选择"))]),
                !value.Supported || !value.SupportsIgpu
                    ? Caption(value.Reason.Length > 0 ? value.Reason : "此机型的核显模式尚未完成适配。").Foreground(Theme.SecondaryText)
                    : Grid([], []),
                HStack(12,
                    Button(busy ? "正在保存…" : "保存模式", Act.Fire("set_gpu_mode", async () =>
                        {
                            if (target is { } mode) await Backend.CallAsync("set_gpu_mode", new { mode = GpuModes.Wire(mode) });
                        }, Settled, setBusy)).AutomationName("保存显卡模式").AccentButton().IsEnabled(!busy && value.Supported && target is not null && target != current),
                    Button("重新读取", reader.Refresh).SubtleButton().IsEnabled(!busy))),
            error => Chrome.SectionCard("输出模式", Chrome.Notice("暂时无法读取", error.Message, InfoBarSeverity.Warning),
                Button("重新读取", reader.Refresh).SubtleButton())));

        var gpu = machine.Status?.Gpu;
        sections.Add(Chrome.SectionCard("图形处理器",
            Chrome.Field("型号", gpu?.Name is { Length: > 0 } name ? name : "未检测到"),
            Chrome.Field("显存容量", gpu?.VramTotalMb is { } mb ? $"{Math.Round(mb / 1024.0, 1)} GB" : "未提供"),
            Chrome.Field("温度", gpu?.Temp is { } temp ? $"{temp:0.#} °C" : "暂未取得"),
            Caption("独立显卡会在混合模式下按需休眠，部分遥测读数可能暂时不可用。").Foreground(Theme.SecondaryText)));
        return Chrome.Page("显卡模式", "按使用场景选择内屏输出方式", [.. sections]);
    }
}
