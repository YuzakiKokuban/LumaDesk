using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using JiYaoChu.Interop;
using JiYaoChu.Model;
using JiYaoChu.Services;
using JiYaoChu.Ui;
using static Microsoft.UI.Reactor.Factories;

namespace JiYaoChu.Pages;

/// <summary>
/// 系统 — the machine's identity, the firmware switches, the OEM takeover, the
/// log file and the on-screen display.
/// </summary>
/// <remarks>
/// The page reads a bundle of six independent commands in one resource rather
/// than six resources: they are all cheap, they are all refetched together
/// after any write, and one <see cref="AsyncValue{T}"/> keeps the page's
/// loading and error handling in a single place.
/// </remarks>
public sealed class SystemPage : Component
{
    public override Element Render()
    {
        var (busy, setBusy) = UseState(false);
        var (failure, setFailure) = UseState<string?>(null);
        var (applied, setApplied) = UseState<string?>(null);
        var (revision, setRevision) = UseState(0);
        UseEffect(() => EventBus.Subscribe(raised =>
        {
            if (raised.Name == "shell://win-key-changed") setRevision(revision + 1);
        }), revision);
        var machine = UseExternalStore(MachineStore.Subscribe, () => MachineStore.Snapshot);

        var bundle = UseResource(_ => FetchAsync(), deps: [revision]);

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

        sections.Add(bundle.Match<Element>(
            () => Chrome.SectionCard("系统信息", HStack(12, ProgressRing(), Body("正在读取系统设置…"))),
            value => VStack(
                20,
                IdentitySection(machine.Status),
                SwitchesSection(value.Switches, value.SwitchesError, busy, setBusy, Settled),
                FirmwareSection(value, busy, setBusy, Settled),
                LogSection(value, busy, setBusy, Settled),
                OsdSection(value.Osd, value.OsdError, busy, setBusy, Settled)),
            error => Chrome.Notice("读取系统设置失败", error.Message, InfoBarSeverity.Error)));

        return Chrome.Page("系统", machine.Status?.Device.Model, [.. sections]);
    }

    /// <summary>The six reads this page needs, fetched together.</summary>
    /// <remarks>
    /// Each read stands alone. On real hardware several of these are refused
    /// outright — <c>get_bios_advanced_menu_status</c> needs the vendor IOCTL
    /// protocol of <c>\\.\ACPIH</c>, which this build does not have — and one
    /// refusal must not blank the whole page.
    /// </remarks>
    private sealed record Bundle(
        IReadOnlyList<DeviceSwitch>? Switches,
        string? SwitchesError,
        OemStatus? Oem,
        string? OemError,
        bool? BiosAdvanced,
        string? BiosError,
        bool? Autostart,
        string? AutostartError,
        LogStatus? Log,
        string? LogError,
        string? LogPath,
        OsdConfig? Osd,
        string? OsdError);

    private static async Task<(T? Value, string? Error)> Soft<T>(Func<Task<T>> read)
        where T : class
    {
        try
        {
            return (await read().ConfigureAwait(false), null);
        }
        catch (Exception error)
        {
            return (null, error.Message);
        }
    }

    private static async Task<(T? Value, string? Error)> SoftValue<T>(Func<Task<T>> read)
        where T : struct
    {
        try
        {
            return (await read().ConfigureAwait(false), null);
        }
        catch (Exception error)
        {
            return (null, error.Message);
        }
    }

    private static async Task<Bundle> FetchAsync()
    {
        var switches = await Soft(() => Backend.CallAsync<List<DeviceSwitch>>("get_device_switches")).ConfigureAwait(false);
        var oem = await Soft(() => Backend.CallAsync<OemStatus>("get_oem_status")).ConfigureAwait(false);
        var bios = await SoftValue(() => Backend.CallAsync<bool>("get_bios_advanced_menu_status")).ConfigureAwait(false);
        var autostart = await SoftValue(() => Backend.CallAsync<bool>("get_autostart")).ConfigureAwait(false);
        var log = await Soft(() => Backend.CallAsync<LogStatus>("get_log_status")).ConfigureAwait(false);
        var path = await Soft(() => Backend.CallAsync<string>("get_log_path")).ConfigureAwait(false);
        var osd = await Soft(() => Backend.CallAsync<OsdConfig>("get_osd_config")).ConfigureAwait(false);

        return new Bundle(
            switches.Value, switches.Error,
            oem.Value, oem.Error,
            bios.Value, bios.Error,
            autostart.Value, autostart.Error,
            log.Value, log.Error,
            path.Value,
            osd.Value, osd.Error);
    }

    // ------------------------------------------------------------------ identity

    private static Element IdentitySection(HardwareStatus? status)
    {
        var device = status?.Device;

        return Chrome.SectionCard(
            "设备信息",
            Chrome.Field("型号", Blank(device?.Model)),
            Chrome.Field("项目代号", Blank(device?.Project)),
            Chrome.Field("BIOS 版本", Blank(device?.Bios)),
            Chrome.Field("EC 版本", Blank(device?.Ec)),
            Chrome.Field("序列号", Blank(device?.Serial)),
            Chrome.Field("电源模式", status is null ? "—" : PowerModes.Label(status.PowerMode)),
            Chrome.Field(
                "运行权限",
                status is null
                    ? "—"
                    : status.Elevated
                        ? "管理员 (可写入固件与 EC)"
                        : "普通用户 (只读，写入会被拒绝)"));
    }

    private static string Blank(string? value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value;

    // ------------------------------------------------------------------ switches

    private static Element SwitchesSection(
        IReadOnlyList<DeviceSwitch>? switches,
        string? error,
        bool busy,
        Action<bool> setBusy,
        Act.Report settle)
    {
        if (switches is null)
        {
            return Chrome.SectionCard(
                "系统开关",
                Chrome.Notice("读不到设备开关", error ?? "后端没有给出原因。", InfoBarSeverity.Warning));
        }

        var rows = new List<Element>();
        var unsupported = 0;

        foreach (var item in switches)
        {
            if (!item.Supported)
            {
                unsupported++;
                continue;
            }

            rows.Add(Chrome.SettingRow(
                DeviceSwitches.Label(item.Id),
                DeviceSwitches.Description(item.Id),
                ToggleSwitch(
                    Optional<bool>.Of(item.Enabled),
                    value => { if (value == item.Enabled) return; Act.Fire(
                        $"set_device_switch {item.Id}",
                        () => Backend.CallAsync("set_device_switch", new { id = item.Id, enabled = value }),
                        settle,
                        setBusy)(); },
                    offContent: "关闭",
                    onContent: "开启")
                    .IsEnabled(item.Supported && !busy)));
            rows.Add(Chrome.Rule());
        }

        if (rows.Count > 0)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var body = new List<Element>();

        if (unsupported > 0)
            body.Add(Caption("尚未接入：" + string.Join("、", switches.Where(item => !item.Supported).Select(item => DeviceSwitches.Label(item.Id))))
                .Foreground(Theme.SecondaryText).TextWrapping(TextWrapping.Wrap));

        body.AddRange(rows);

        return Chrome.SectionCard("系统开关", [.. body]);
    }

    // ------------------------------------------------------------------ firmware

    private static Element FirmwareSection(
        Bundle bundle,
        bool busy,
        Action<bool> setBusy,
        Act.Report settle)
    {
        var body = new List<Element>
        {
            Body(
                    "启动时自动接管官方控制中心，避免硬件设置被覆盖。还原会恢复服务与计划任务，并关闭自动接管。")
                .Foreground(Theme.SecondaryText)
                .TextWrapping(TextWrapping.Wrap),
        };

        if (bundle.Oem is { } oem)
        {
            body.Add(Chrome.SettingRow(
                "官方控制中心",
                oem.TakenOver ? "已接管" : "未接管",
                Button(
                        oem.TakenOver ? "还原官方控制中心" : "接管官方控制中心",
                        Act.Fire(
                            "toggle_oem_service",
                            () => Backend.CallAsync("toggle_oem_service", new { enable = !oem.TakenOver }),
                            settle,
                            setBusy))
                    .SubtleButton()
                    .AutomationName(oem.TakenOver ? "还原官方控制中心" : "接管官方控制中心")
                    .IsEnabled(!busy)));
            body.Add(Chrome.Field("标记文件", Blank(oem.MarkerPath)));
        }
        else
        {
            body.Add(Chrome.Notice("读不到接管状态", bundle.OemError ?? "后端没有给出原因。", InfoBarSeverity.Warning));
        }

        body.Add(Chrome.Rule());
        body.Add(Chrome.Field("高级 BIOS 菜单", "尚未接入"));
        body.Add(FlagRow(
            "开机自启",
            "通过计划任务在登录后以管理员权限启动到托盘，不主动打开界面。",
            bundle.Autostart,
            bundle.AutostartError,
            value => Act.Fire(
                "set_autostart",
                () => Backend.CallAsync("set_autostart", new { enabled = value }),
                settle,
                setBusy),
            busy));

        return Chrome.SectionCard("接管与固件", [.. body]);
    }

    /// <summary>
    /// A switch whose current value may be unknown, because the read behind it
    /// is allowed to be refused on hardware this build does not fully drive.
    /// </summary>
    private static Element FlagRow(
        string label,
        string description,
        bool? isOn,
        string? error,
        Func<bool, Action> fire,
        bool busy)
    {
        if (isOn is not { } value)
        {
            return Chrome.Notice(
                $"{label} 不可用",
                string.IsNullOrWhiteSpace(error) ? "后端没有给出原因。" : error,
                InfoBarSeverity.Warning);
        }

        return Chrome.SettingRow(
            label,
            description,
            ToggleSwitch(Optional<bool>.Of(value), next => { if (next != value) fire(next)(); })
                .IsEnabled(!busy));
    }

    // ------------------------------------------------------------------ log

    private static Element LogSection(
        Bundle bundle,
        bool busy,
        Action<bool> setBusy,
        Act.Report settle)
    {
        if (bundle.Log is not { } log)
        {
            return Chrome.SectionCard(
                "日志",
                Chrome.Notice("读不到日志设置", bundle.LogError ?? "后端没有给出原因。", InfoBarSeverity.Warning));
        }

        var levels = SystemOptions.LogLevels.Select(option => option.Label).ToArray();
        var filters = SystemOptions.LogFilters.Select(option => option.Label).ToArray();
        var path = string.IsNullOrWhiteSpace(bundle.LogPath) ? log.Path : bundle.LogPath;

        return Chrome.SectionCard(
            "日志",
            Chrome.SettingRow(
                "详细程度",
                "调试期间用「所有日志」，日常用「常规日志」。",
                ComboBox(
                    levels,
                    Optional<int>.Of(SystemOptions.LogLevelIndex(log.Level)),
                    index => { if (index < 0 || index >= SystemOptions.LogLevels.Count || index == SystemOptions.LogLevelIndex(log.Level)) return; Act.Fire(
                        "set_log_level",
                        () => Backend.CallAsync("set_log_level", new { level = SystemOptions.LogLevels[index].Wire }),
                        settle,
                        setBusy)(); })
                    .IsEnabled(!busy)),
            Chrome.Rule(),
            Chrome.SettingRow(
                "分类过滤",
                "只看某一类子系统写下的记录。",
                ComboBox(
                    filters,
                    Optional<int>.Of(SystemOptions.LogFilterIndex(log.Filter)),
                    index => { if (index < 0 || index >= SystemOptions.LogFilters.Count || index == SystemOptions.LogFilterIndex(log.Filter)) return; Act.Fire(
                        "set_log_filter",
                        () => Backend.CallAsync("set_log_filter", new { filter = SystemOptions.LogFilters[index].Wire }),
                        settle,
                        setBusy)(); })
                    .IsEnabled(!busy)),
            Chrome.Rule(),
            Chrome.Field("日志文件", Blank(path)),
            HStack(
                8,
                Button("打开日志文件", Act.Fire("open_log_file", () => Backend.CallAsync("open_log_file"), settle))
                    .SubtleButton(),
                Button("打开配置目录", Act.Fire("open_profiles_folder", () => Backend.CallAsync("open_profiles_folder"), settle))
                    .SubtleButton()));
    }

    // ------------------------------------------------------------------ OSD

    private static Element OsdSection(OsdConfig? osd, string? error, bool busy, Action<bool> setBusy, Act.Report settle)
    {
        if (osd is null)
            return Chrome.SectionCard("屏幕提示", Chrome.Notice("读取失败", error ?? "无法读取 OSD 设置。", InfoBarSeverity.Warning));

        void Save(OsdConfig next)
        {
            if (next == osd || busy) return;
            Act.Fire("save_osd_config", () => Backend.CallAsync("save_osd_config", new { config = next }), settle, setBusy)();
        }
        var durations = new uint[] { 1000, 2200, 4000, 6000 };
        return Chrome.SectionCard("屏幕提示",
            Caption("提示窗不抢焦点，显示后自动消失。收起主窗口后暂停硬件监控，重新打开时恢复。")
                .Foreground(Theme.SecondaryText).TextWrapping(TextWrapping.Wrap),
            Chrome.SettingRow("启用 OSD", "显示 Fn 功能、锁定键、音量、亮度与供电变化提示。",
                ToggleSwitch(Optional<bool>.Of(osd.Enabled), enabled => Save(osd with { Enabled = enabled })).IsEnabled(!busy)),
            Chrome.Rule(),
            Chrome.SettingRow("位置", "在鼠标所在屏幕的工作区域内显示。",
                ComboBox(SystemOptions.OsdPositions.Select(item => item.Label).ToArray(), Optional<int>.Of(SystemOptions.OsdPositionIndex(osd.Position)),
                    index => { if (index >= 0 && index < SystemOptions.OsdPositions.Count) Save(osd with { Position = SystemOptions.OsdPositions[index].Wire }); }).IsEnabled(!busy)),
            Chrome.SettingRow("外观", "选择提示窗配色。",
                ComboBox(SystemOptions.OsdThemes.Select(item => item.Label).ToArray(), Optional<int>.Of(SystemOptions.OsdThemeIndex(osd.Theme)),
                    index => { if (index >= 0 && index < SystemOptions.OsdThemes.Count) Save(osd with { Theme = SystemOptions.OsdThemes[index].Wire }); }).IsEnabled(!busy)),
            Chrome.SettingRow("显示时间", "连续操作时重新开始计时。",
                ComboBox(new[] { "1 秒", "2.2 秒", "4 秒", "6 秒" }, Optional<int>.Of(Math.Max(0, Array.IndexOf(durations, osd.DurationMs))),
                    index => { if (index >= 0 && index < durations.Length) Save(osd with { DurationMs = durations[index] }); }).IsEnabled(!busy)),
            Chrome.SettingRow("不透明度 (%)", "自定义 20–100%，默认 60%。数值越小，提示窗越透明。",
                NumberBox(Optional<double>.Of(osd.Opacity), value =>
                {
                    if (double.IsFinite(value)) Save(osd with { Opacity = (uint)Math.Clamp(Math.Round(value), 20, 100) });
                }).Range(20, 100).SpinButtons().AutomationName("OSD 不透明度百分比").Width(120).IsEnabled(!busy)),
            Chrome.SettingRow("性能与供电变化", "提示电源插拔；主窗口打开时也提示档位与强冷变化。",
                ToggleSwitch(Optional<bool>.Of(osd.ShowOnPowerChange), enabled => Save(osd with { ShowOnPowerChange = enabled })).IsEnabled(!busy)),
            HStack(8,
                Button("预览性能提示", Act.Fire("trigger_osd_preview", () => Backend.CallAsync("trigger_osd_preview", new { kind = "power" }), settle)).IsEnabled(osd.Enabled && !busy),
                Button("预览背光提示", Act.Fire("trigger_osd_preview", () => Backend.CallAsync("trigger_osd_preview", new { kind = "brightness" }), settle)).IsEnabled(osd.Enabled && !busy)));
    }
}
