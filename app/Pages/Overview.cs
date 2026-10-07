using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using JiYaoChu.Model;
using JiYaoChu.Services;
using JiYaoChu.Ui;
using JiYaoChu.Interop;
using static Microsoft.UI.Reactor.Factories;

namespace JiYaoChu.Pages;

/// <summary>状态概览 — the live dashboard.</summary>
public sealed class OverviewPage : Component
{
    public override Element Render()
    {
        var state = UseExternalStore(MachineStore.Subscribe, () => MachineStore.Snapshot).ForPage("overview");
        var wide = UseBreakpoint(1120);
        var (busy, setBusy) = UseState(false);
        var (failure, setFailure) = UseState<string?>(null);

        var sections = new List<Element>
        {
            MachineStoreHeader(state),
        };

        if (state.Status is not { } status)
        {
            sections.Add(
                VStack(12,
                    ProgressRing().IsActive(true),
                    Body(state.Loading ? "正在读取硬件状态…" : "硬件状态暂时不可用").Foreground(Theme.SecondaryText))
                .Margin(0, 24, 0, 0));
            return Chrome.Page("状态概览", null, [.. sections]);
        }

        sections.Add(Chrome.Feedback(failure, state.LastUpdated is { } updated
            ? $"更新于 {updated.ToLocalTime():HH:mm:ss}" + (state.IsStale ? " · 数据已过期" : "") : null));
        sections.Add(Readings(status, wide));
        sections.Add(Chrome.SectionCard("常用控制",
            Chrome.SettingRow("一键强冷", "临时提高风扇输出，关闭后恢复自动调节。",
                ToggleSwitch(Optional<bool>.Of(status.FanBoost == true), enabled => Act.Fire("set_fan_boost",
                    () => Backend.CallAsync("set_fan_boost", new { enabled }),
                    (error, _) => { setFailure(error); MachineStore.Refresh(); }, setBusy)())
                    .IsEnabled(status.SupportFlags.FanBoost && !busy)),
            Chrome.Rule(),
            Chrome.Field("显卡配置", GpuModes.Label(status.GpuMode)),
            Caption("显卡输出方式与充电上限可在左侧对应页面调整。").Foreground(Theme.SecondaryText)));
        sections.Add(Trends(state.Trends));

        return Chrome.Page("状态概览", status.Device.Model, [.. sections]);
    }

    /// <summary>The strip above the readings: elevation, backend and errors.</summary>
    private static Element MachineStoreHeader(MachineState state)
    {
        var notices = new List<Element>();

        if (state.ConfigError is { } configError)
            notices.Add(Chrome.Notice("启动设置需要检查", configError, InfoBarSeverity.Warning));
        if (state.Error is { } error)
        {
            notices.Add(Chrome.Notice("硬件数据已过期", "当前保留上次成功读取的数据。" + error, InfoBarSeverity.Warning));
        }

        if (state.Status is { Elevated: false })
        {
            notices.Add(Chrome.Notice(
                "未以管理员身份运行",
                "电源模式、风扇曲线和灯效设置都需要管理员权限。请以管理员身份重新启动机耀处。",
                InfoBarSeverity.Warning));
        }

        if (state.Backend == "mock")
        {
            notices.Add(Chrome.Notice(
                "模拟数据",
                "后端运行在模拟模式，显示的数字并非来自本机硬件。",
                InfoBarSeverity.Informational));
        }

        return notices.Count == 0 ? Grid([], []) : VStack(8, [.. notices]);
    }

    /// <summary>CPU, GPU, fan and battery readings, four to a row.</summary>
    private static Element Readings(HardwareStatus status, bool wide)
    {
        var cpu = status.Cpu;
        var gpu = status.Gpu;
        var battery = status.Battery;

        var cards = new Element[]
        {
            Chrome.StatCard(
                "CPU 温度（EC）",
                Chrome.Number(cpu.Temp),
                "°C",
                $"{Chrome.Number(cpu.FreqMhz, "0")} MHz · 负载 {Chrome.Number(cpu.Load, "0")} %",
                Chrome.TemperatureTone(cpu.Temp)),

            Chrome.StatCard(
                "显卡温度",
                Chrome.Number(gpu.Temp),
                "°C",
                gpu.Present ? gpu.Name : "未检测到独立显卡",
                Chrome.TemperatureTone(gpu.Temp)),

            Chrome.StatCard(
                "风扇转速",
                status.Fans.Available ? Chrome.Number(status.Fans.CpuRpm) : "—",
                "RPM",
                status.Fans.Available ? $"CPU {status.Fans.CpuRpm} · GPU {status.Fans.GpuRpm}" : "暂未取得转速"),

            Chrome.StatCard(
                "电池电量",
                Chrome.Number(battery.Percent, "0"),
                "%",
                BatteryNote(battery),
                battery.Percent <= 15 && !battery.Charging ? Theme.SystemCritical : null),
        };

        if (!wide) return VStack(12, Chrome.ChoiceRow(cards[0], cards[1]), Chrome.ChoiceRow(cards[2], cards[3]));
        return Grid(
            columns: [GridSize.Star(), GridSize.Star(), GridSize.Star(), GridSize.Star()],
            rows: [GridSize.Auto],
            cards[0].Grid(row: 0, column: 0).Margin(0, 0, 6, 0),
            cards[1].Grid(row: 0, column: 1).Margin(6, 0, 6, 0),
            cards[2].Grid(row: 0, column: 2).Margin(6, 0, 6, 0),
            cards[3].Grid(row: 0, column: 3).Margin(6, 0, 0, 0));
    }

    private static string BatteryNote(BatteryStatus battery)
    {
        var parts = new List<string>(3);
        parts.Add(battery.Charging ? "正在充电" : battery.OnAc ? "已连接电源" : "使用电池");
        parts.Add(battery.Limit is { } limit ? $"充电上限 {limit} %" : "充电上限未知");
        parts.Add(battery.HealthPercent is { } health
            ? $"电池健康 {Chrome.Number(health)} %"
            : "电池健康未知");
        return string.Join(" · ", parts);
    }

    /// <summary>Which cooling profile, graphics mode and Windows plan are active.</summary>
    private static Element Modes(HardwareStatus status)
        => Chrome.SectionCard(
            "当前模式",
            Chrome.Field("电源模式", PowerModes.Label(status.PowerMode)),
            Chrome.Field("已保存显卡配置", GpuModes.Label(status.GpuMode)),
            Chrome.Field(
                "Windows 电源计划",
                string.IsNullOrWhiteSpace(status.WindowsPowerScheme) ? "未知" : status.WindowsPowerScheme));

    /// <summary>Who this laptop is.</summary>
    private static Element Device(HardwareStatus status)
    {
        var device = status.Device;
        return Chrome.SectionCard(
            "设备信息",
            Chrome.Field("型号", device.Model),
            Chrome.Field("项目代号", Or(device.Project)),
            Chrome.Field("BIOS 版本", Or(device.Bios)),
            Chrome.Field("EC 版本", Or(device.Ec)),
            Chrome.Field("序列号", Or(device.Serial)));
    }

    /// <summary>
    /// Which optional hardware this machine has.
    /// </summary>
    /// <remarks>
    /// Shown as a list rather than a grid of equal tiles: the names differ in
    /// length and there is no reason to pretend otherwise.
    /// </remarks>
    private static Element Features(SupportFlags flags)
    {
        (string Name, bool Present)[] entries =
        [
            ("一键强冷", flags.FanBoost),
            ("充电上限", flags.BatteryLimit),
            ("水冷魔盒", flags.WaterCooler),
            ("四分区背光", flags.FourZone),
            ("Logo 灯效", flags.Logo),
            ("转轴灯效", flags.Hinge),
            ("灯带", flags.Lightbar),
            ("BIOS 高级菜单", flags.BiosAdvanced),
            ("显卡切换", flags.GpuSwitching),
            ("显示调校", flags.DisplayTuning),
            ("风扇曲线", flags.FanCurve),
        ];

        var rows = new List<Element>(entries.Length);
        foreach (var (name, present) in entries)
        {
            rows.Add(Grid(
                columns: [GridSize.Star(), GridSize.Auto],
                rows: [GridSize.Auto],
                Body(name).Grid(row: 0, column: 0),
                Chrome.Pill(
                    present ? "已接入" : "暂未接入",
                    present ? InfoBarSeverity.Success : InfoBarSeverity.Informational)
                    .Grid(row: 0, column: 1)));
        }

        return Chrome.SectionCard("功能接入状态", VStack(8, [.. rows]));
    }

    private static string Or(string value, string fallback = "—")
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static Element Trends(IReadOnlyList<TrendSample> samples)
    {
        Element Plot(string title, TrendMetric metric)
            => VStack(6,
                Body(title).SemiBold(),
                new TrendGraphElement(samples, metric).WithKey("trend:" + metric),
                Caption(TrendGraph.Summary(samples, metric)).Foreground(Theme.SecondaryText).TextWrapping(TextWrapping.Wrap));
        return Chrome.SectionCard("最近五分钟趋势",
            Caption("仅在状态概览可见时记录；切页、收起、读取失败与缺失传感器显示为空隙。CPU 温度来自 EC；显卡优先使用 NVIDIA 读数，无效时尝试 EC。")
                .Foreground(Theme.SecondaryText).TextWrapping(TextWrapping.Wrap),
            Plot("温度 (°C)", TrendMetric.Temperature),
            Plot("负载 (%)", TrendMetric.Load),
            Plot("风扇转速 (RPM)", TrendMetric.Fan));
    }
}
