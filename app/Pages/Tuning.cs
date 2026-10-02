using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml.Controls;
using JiYaoChu.Interop;
using JiYaoChu.Model;
using JiYaoChu.Services;
using JiYaoChu.Ui;
using static Microsoft.UI.Reactor.Factories;

namespace JiYaoChu.Pages;

/// <summary>
/// 电源管理 — cooling profile, battery care, and the Windows power scheme.
/// </summary>
/// <remarks>
/// The stored configuration arrives through <c>get_power_settings</c> rather
/// than from the polled status: it carries the scheme list and the CPU power
/// limits, which the one-second snapshot deliberately does not. Every write
/// bumps a revision so that resource refetches, because these values are not
/// part of the polled snapshot.
/// </remarks>
public sealed class TuningPage : Component
{
    public override Element Render()
    {
        var (busy, setBusy) = UseState(false);
        var (failure, setFailure) = UseState<string?>(null);
        var (applied, setApplied) = UseState<string?>(null);
        var (revision, setRevision) = UseState(0);
        var machine = UseExternalStore(MachineStore.Subscribe, () => MachineStore.Snapshot);

        var settings = UseResource(
            _ => Backend.CallAsync<PowerSettings>("get_power_settings"),
            deps: [revision]);

        // One callback for "a write landed": refresh the live snapshot and force
        // the resource above to re-read the stored configuration.
        void Settled(string? error, string? ok)
        {
            setFailure(error);
            setApplied(ok);
            MachineStore.Refresh();
            setRevision(revision + 1);
        }

        var sections = new List<Element>();

        if (failure is not null)
        {
            sections.Add(Chrome.Notice("操作失败", failure, InfoBarSeverity.Error));
        }
        else if (applied is not null)
        {
            sections.Add(Chrome.Notice("已应用", applied, InfoBarSeverity.Success));
        }

        if (machine.Error is not null)
        {
            sections.Add(Chrome.Notice("读取硬件状态失败", machine.Error, InfoBarSeverity.Error));
        }

        sections.Add(settings.Match<Element>(
            () => Chrome.SectionCard("机械革命性能档位", HStack(12, ProgressRing(), Body("正在读取电源设置…"))),
            value => VStack(
                20,
                PowerSection(value, busy, setBusy, Settled),
                BatterySection(machine.Status, busy, setBusy, Settled),
                WindowsModeSection(value, busy, setBusy, Settled),
                SchemeSection(value, busy, setBusy, Settled)),
            error => Chrome.Notice("读取电源设置失败", error.Message, InfoBarSeverity.Error)));

        sections.Add(LiveSection(machine.Status));

        return Chrome.Page(
            "电源管理",
            "能效模式、充电上限与实时读数",
            [.. sections]);
    }

    private static Element PowerSection(
        PowerSettings settings,
        bool busy,
        Action<bool> setBusy,
        Act.Report settled)
    {
        var cards = PowerModes.All
            .Select(mode => Chrome.ChoiceCard(
                PowerModes.Label((byte)mode),
                PowerModes.Tag((byte)mode),
                PowerModes.Description((byte)mode),
                selected: (byte)mode == settings.PowerMode,
                enabled: !busy,
                onClick: Act.Fire(
                    "set_power_mode",
                    () => Backend.CallAsync("set_power_mode", new { mode = (byte)mode }),
                    settled,
                    setBusy)))
            .ToArray();

        return Chrome.SectionCard(
            "机械革命性能档位",
            Body("办公适合日常轻负载，均衡兼顾性能与噪声，狂暴用于高负载。")
                .Foreground(Theme.SecondaryText),
            Chrome.ChoiceRow(cards));
    }

    private static Element BatterySection(
        HardwareStatus? status,
        bool busy,
        Action<bool> setBusy,
        Act.Report settled)
    {
        var battery = status?.Battery;

        var cards = BatteryModes.All
            .Select(mode => Chrome.ChoiceCard(
                BatteryModes.Name(mode),
                $"上限 {BatteryModes.Limit(mode)} %",
                BatteryModes.Detail(mode),
                selected: battery is not null && battery.Limit == BatteryModes.Limit(mode),
                enabled: !busy && status?.SupportFlags.BatteryLimit == true,
                onClick: Act.Fire(
                    "set_battery_mode",
                    () => Backend.CallAsync("set_battery_mode", new { mode = BatteryModes.Wire(mode) }),
                    settled,
                    setBusy)))
            .ToArray();

        return Chrome.SectionCard(
            "电池养护",
            Chrome.Field("当前电量", $"{Chrome.Number(battery?.Percent, "0.#")} %"
                + (battery is null ? "" : battery.Charging ? " · 正在充电" : battery.OnAc ? " · 已连接电源" : " · 使用电池")),
            Chrome.Field("充电上限", battery is null ? "—" : $"{battery.Limit} %"),
            Chrome.Field("电池健康", battery?.HealthPercent is { } health
                ? $"{Chrome.Number(health, "0.#")} %"
                : "未知（固件未提供设计容量）"),
            Chrome.ChoiceRow(cards));
    }

    private static Element WindowsModeSection(PowerSettings settings, bool busy, Action<bool> setBusy, Act.Report settle)
        => Chrome.SectionCard("Windows 电源模式",
            Caption("独立于机身档位，应用于当前供电方式。").Foreground(Theme.SecondaryText),
            ComboBox(new[] { "最佳能效", "平衡", "最佳性能" }, Optional<int>.Of(settings.WindowsPowerMode is { } mode ? mode : -1),
                index => { if (index is >= 0 and <= 2 && index != settings.WindowsPowerMode) Act.Fire("set_windows_power_mode", () => Backend.CallAsync("set_windows_power_mode", new { mode = (byte)index }), settle, setBusy)(); })
                .IsEnabled(!busy && settings.WindowsPowerMode is not null));

    private static Element SchemeSection(
        PowerSettings settings,
        bool busy,
        Action<bool> setBusy,
        Act.Report settled)
    {
        var rows = new List<Element>();

        foreach (var scheme in settings.Schemes)
        {
            var row = scheme.Active
                ? Chrome.Pill("使用中", InfoBarSeverity.Success)
                : (Element)Button("切换", Act.Fire(
                        "set_active_windows_power_scheme",
                        () => Backend.CallAsync(
                            "set_active_windows_power_scheme",
                            new { guid = scheme.Guid }),
                        settled,
                        setBusy))
                    .SubtleButton()
                    .IsEnabled(!busy);

            rows.Add(Chrome.SettingRow(scheme.Name, scheme.Guid.ToUpperInvariant(), row));
        }

        if (rows.Count == 0)
        {
            rows.Add(Body("没有读到任何电源计划。").Foreground(Theme.SecondaryText));
        }

        return Chrome.SectionCard(
            "Windows 电源计划",
            Body($"当前：{settings.ActiveScheme}").Foreground(Theme.SecondaryText),
            VStack(10, [.. rows]));
    }

    private static Element LimitsSection(PowerSettings settings)
        => Chrome.SectionCard(
            "功耗墙",
            Chrome.Field("温度目标下限", $"{Chrome.Number(settings.CpuTempTargetMin, "0.#")} °C"),
            Chrome.Field("温度目标上限", $"{Chrome.Number(settings.CpuTempTargetMax, "0.#")} °C"),
            Chrome.Field("PL4 余量", $"{Chrome.Number(settings.CpuPl4Margin, "0.#")} W"),
            Chrome.Field("显卡偏移步进", $"{Chrome.Number(settings.GpuOffsetStep, "0.#")} MHz"),
            Chrome.Field("安全看门狗", settings.CpuSafetyGuard ? "已启用" : "已关闭"),
            Chrome.Field("高性能计划", settings.HighPerfScheme ? "已启用" : "已关闭"));

    private static Element LiveSection(HardwareStatus? status)
    {
        var cpu = status?.Cpu;
        var fans = status?.Fans;

        return Chrome.SectionCard(
            "实时读数",
            Chrome.Field("ACPI 热区温度", $"{Chrome.Number(cpu?.Temp, "0.#")} °C"),
            Chrome.Field("CPU 频率", $"{Chrome.Number(cpu?.FreqMhz, "0")} MHz"),
            Chrome.Field("CPU 负载", $"{Chrome.Number(cpu?.Load, "0.#")} %"),
            Chrome.Field("CPU 功耗", cpu?.PowerW is null
                ? "—"
                : $"{Chrome.Number(cpu.PowerW, "0.#")} W"),
            Chrome.Field("风扇转速", fans is null || !fans.Available
                ? "—"
                : $"CPU {fans.CpuRpm} · GPU {fans.GpuRpm}"));
    }
}
