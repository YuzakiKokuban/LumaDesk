using JiYaoChu.Interop;
using JiYaoChu.Services;

namespace JiYaoChu.Ui;

/// <summary>
/// Click-handler plumbing: every page needs to fire an async backend call and
/// surface whatever went wrong, without blocking the render pass.
/// </summary>
public static class Act
{
    /// <summary>A callback that reports to the page's error and status slots.</summary>
    public delegate void Report(string? error, string? status);

    /// <summary>
    /// Wraps a backend write into a click handler.
    /// </summary>
    /// <param name="command">The wire command, used in the status line.</param>
    /// <param name="work">The call to make.</param>
    /// <param name="report">Where the outcome is published.</param>
    /// <param name="busy">Called with true before the call and false after.</param>
    public static Action Fire(
        string command,
        Func<Task> work,
        Report report,
        Action<bool>? busy = null)
        => () => _ = RunAsync(command, work, report, busy);

    /// <summary>
    /// Awaits a write, translating both backend refusals and transport faults
    /// into the same one-line message.
    /// </summary>
    public static async Task RunAsync(
        string command,
        Func<Task> work,
        Report report,
        Action<bool>? busy = null)
    {
        busy?.Invoke(true);
        report(null, null);

        try
        {
            await work();
            report(null, $"{Label(command)}已完成");
        }
        catch (CoreException ex)
        {
            StartupLog.Write(ex);
            report(Failure(command, ex.Message), null);
        }
        catch (Exception ex)
        {
            StartupLog.Write(ex);
            report($"{Label(command)}失败：{ex.Message}", null);
        }
        finally
        {
            busy?.Invoke(false);
        }
    }

    /// <summary>Asks the store for a fresh reading after a write lands.</summary>
    public static void Refresh() => MachineStore.Refresh();

    private static string Failure(string command, string message)
    {
        if (message.Contains("applied but not saved", StringComparison.OrdinalIgnoreCase)
            || message.Contains("state was not saved", StringComparison.OrdinalIgnoreCase))
            return "设置已生效，但未能保存。请检查配置目录是否可写，然后重试。";
        if (message.Contains("saved with warnings", StringComparison.OrdinalIgnoreCase))
            return "配置已保存，但部分硬件设置未生效。详细原因已记录在日志中。";
        return $"{Label(command)}失败：{message}";
    }

    private static string Label(string command) => command.Split(' ')[0] switch
    {
        "set_power_mode" => "性能档位调整",
        "set_power_automation" => "自动性能档位设置",
        "set_windows_power_mode" => "Windows 电源模式调整",
        "set_fan_boost" => "强冷设置",
        "set_battery_mode" or "set_battery_limit" => "充电上限调整",
        "set_active_windows_power_scheme" => "电源计划切换",
        "set_display_monitor_refresh_rate" or "switch_refresh_rate" => "刷新率调整",
        "set_display_brightness" => "屏幕亮度调整",
        "restore_app_settings" => "应用设置恢复",
        "apply_keyboard_lighting" => "键盘背光调整",
        "set_gpu_mode" => "显卡模式保存",
        "restart_system" => "系统重启准备",
        "set_device_switch" or "set_win_key_locked" => "设备开关调整",
        "toggle_oem_service" => "官方控制中心状态调整",
        "set_autostart" => "开机自启设置",
        "set_log_level" or "set_log_filter" => "日志设置",
        "save_osd_config" or "set_osd_config" => "屏幕提示设置",
        "open_log_file" => "打开日志",
        "open_profiles_folder" => "打开配置目录",
        "export_diagnostics" => "诊断包导出",
        "backup_settings" => "设置备份",
        "reconnect_notifications" => "通知重连请求",
        _ => "设置",
    };
}
