using Windows.UI;

namespace JiYaoChu.Model;

/// <summary>One entry in an effect grid: the wire id, the label, the icon name.</summary>
/// <param name="Id">The value the backend expects for this effect.</param>
/// <param name="Label">The Chinese label, exactly as recovered.</param>
/// <param name="Icon">The Segoe Fluent icon name shown above the label.</param>
public sealed record EffectOption(uint Id, string Label, string Icon);

/// <summary>Chinese names for how far the battery charges.</summary>
public static class BatteryModes
{
    /// <summary>The three modes, in the order the row draws them.</summary>
    public static IReadOnlyList<BatteryMode> All { get; } =
    [
        BatteryMode.Workstation,
        BatteryMode.Balanced,
        BatteryMode.LongLife,
    ];

    /// <summary>The wire spelling <c>set_battery_mode</c> expects.</summary>
    public static string Wire(BatteryMode mode) => mode switch
    {
        BatteryMode.Workstation => "workstation",
        BatteryMode.LongLife => "long_life",
        _ => "balanced",
    };

    /// <summary>The short name.</summary>
    public static string Name(BatteryMode mode) => mode switch
    {
        BatteryMode.Workstation => "工作站",
        BatteryMode.LongLife => "长效",
        _ => "平衡",
    };

    /// <summary>The full label, including the charge limit it implies.</summary>
    public static string Detail(BatteryMode mode)
        => mode == BatteryMode.Workstation ? "充满电池，适合外出前准备。" : $"充电至 {Limit(mode)}%，减少长期插电时的电池损耗。";

    /// <summary>The charge limit a mode writes.</summary>
    public static uint Limit(BatteryMode mode) => mode switch
    {
        BatteryMode.Workstation => 100,
        BatteryMode.Balanced => 80,
        _ => 60,
    };
}

/// <summary>Chinese names for the water-cooling strategies.</summary>
public static class CoolerStrategies
{
    /// <summary>The four strategies, in display order.</summary>
    public static IReadOnlyList<string> All { get; } = ["smart", "silent", "balanced", "extreme"];

    /// <summary>The name of a strategy.</summary>
    public static string Name(string wire) => wire switch
    {
        "silent" => "静音策略",
        "balanced" => "均衡策略",
        "extreme" => "极速狂暴",
        _ => "智慧自适应",
    };

    /// <summary>The badge the original put in the strategy card's corner.</summary>
    public static string Badge(string wire) => wire switch
    {
        "silent" or "balanced" => "固定",
        "extreme" => "满血",
        _ => "温控随动",
    };

    /// <summary>The one-line explanation of a strategy.</summary>
    public static string Detail(string wire) => wire switch
    {
        "silent" => "固定 7V 水泵 + 40% 风扇 · 适合安静夜间使用",
        "balanced" => "固定 8V 水泵 + 75% 风扇 · 兼顾散热与风噪",
        "extreme" => "固定 11V 水泵 + 100% 风扇",
        _ => "依据核心温度动态调控",
    };
}

/// <summary>Chinese names for the two lighting engines.</summary>
public static class LightingEngines
{
    /// <summary>Every engine the picker offers, in display order.</summary>
    public static IReadOnlyList<LightingEngine> All { get; } =
    [
        LightingEngine.Hardware,
        LightingEngine.BetterRgb,
    ];

    /// <summary>The engine name as the picker shows it.</summary>
    public static string Name(LightingEngine engine) => engine switch
    {
        LightingEngine.BetterRgb => "极客高刷 (BetterRGB 算法)",
        _ => "原厂固件 (0% CPU)",
    };

    /// <summary>The one-line explanation of an engine.</summary>
    public static string Detail(LightingEngine engine) => engine switch
    {
        LightingEngine.BetterRgb => "由 BetterRGB 算法计算并由驱动逐帧推流到键盘",
        _ => "由 ITE 单片机固件内部硬件定时器渲染，系统 0% CPU 占用",
    };

    /// <summary>The wire spelling <c>set_keyboard_engine</c> expects.</summary>
    public static string Wire(LightingEngine engine)
        => engine == LightingEngine.BetterRgb ? "better_rgb" : "hardware";
}

/// <summary>
/// The effect tables, recovered from the original front end.
/// </summary>
/// <remarks>
/// The ids are the values the backend takes on the wire; they are not contiguous,
/// so an effect is always referenced by id rather than by index.
/// </remarks>
public static class LightingEffects
{
    /// <summary>The fourteen BetterRGB streamer effects (<c>kb_effect</c> 100..=113).</summary>
    public static IReadOnlyList<EffectOption> Streamer { get; } =
    [
        new(100, "呼吸", "Brightness"),
        new(101, "波浪", "Wave"),
        new(102, "涟漪", "Ripple"),
        new(103, "流沙", "Sparkles"),
        new(104, "电流", "Flash"),
        new(105, "色轮", "Color"),
        new(106, "彩虹", "Rainbow"),
        new(107, "矩阵", "Matrix"),
        new(108, "闪电", "Lightning"),
        new(109, "火焰", "Fire"),
        new(110, "雨滴", "Water"),
        new(111, "星光", "Star"),
        new(112, "双向电流", "TwoBars"),
        new(113, "单色常亮", "Light"),
    ];

    /// <summary>The four-zone (ITE 8291 r2) hardware effects.</summary>
    public static IReadOnlyList<EffectOption> FourZone { get; } =
    [
        new(1, "单色/四区常亮", "Light"),
        new(2, "单色呼吸", "Brightness"),
        new(3, "全彩波浪", "Wave"),
        new(5, "彩虹循环", "Rainbow"),
        new(12, "闪烁跳变", "Sparkles"),
        new(13, "混合流光", "Flash"),
    ];

    /// <summary>The single-zone (per-key) hardware effects.</summary>
    public static IReadOnlyList<EffectOption> PerKey { get; } =
    [
        new(1, "单色常亮", "Light"),
        new(2, "单色呼吸", "Brightness"),
        new(3, "波浪流光", "Wave"),
        new(4, "按键微光", "Sparkles"),
        new(5, "彩虹相环", "Rainbow"),
        new(6, "涟漪扩散", "Ripple"),
        new(9, "全彩跑马", "Flash"),
        new(10, "随机雨滴", "Water"),
        new(14, "幽静极光", "Star"),
        new(17, "璀璨烟花", "Lightning"),
        new(21, "电竞高亮", "TwoBars"),
    ];

    /// <summary>The front coastline lightbar effects (048D:7001).</summary>
    public static IReadOnlyList<EffectOption> Lightbar { get; } =
    [
        new(1, "单色", "Light"),
        new(2, "呼吸", "Brightness"),
        new(3, "波浪", "Wave"),
        new(4, "冲突", "Sparkles"),
        new(6, "流星", "Star"),
    ];

    /// <summary>The air-vent / hinge effects; the wire carries a speed here.</summary>
    public static IReadOnlyList<EffectOption> Hinge { get; } =
    [
        new(1, "单色常亮", "Light"),
        new(2, "能量呼吸", "Brightness"),
    ];

    /// <summary>The A-cover logo effects.</summary>
    public static IReadOnlyList<EffectOption> Logo { get; } =
    [
        new(1, "单色", "Light"),
        new(2, "呼吸", "Brightness"),
        new(3, "波浪", "Wave"),
        new(4, "冲突", "Sparkles"),
        new(6, "流星", "Star"),
        new(9, "跑马", "Flash"),
    ];

    /// <summary>The water-cooler LED effects, as <c>(label, effect id)</c>.</summary>
    public static IReadOnlyList<EffectOption> WaterLed { get; } =
    [
        new(0, "炫彩流动", "Rainbow"),
        new(1, "七彩呼吸", "Brightness"),
        new(2, "单色呼吸", "Light"),
    ];

    /// <summary>The label for an effect id in <paramref name="list"/>.</summary>
    public static string LabelOf(IReadOnlyList<EffectOption> list, uint id)
        => list.FirstOrDefault(e => e.Id == id)?.Label ?? "未检测到硬件";

    /// <summary>The index of an effect id in <paramref name="list"/>, or 0.</summary>
    public static int IndexOf(IReadOnlyList<EffectOption> list, uint id)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Id == id)
            {
                return i;
            }
        }

        return 0;
    }
}

/// <summary>The colour palettes recovered from the original pickers.</summary>
public static class Palettes
{
    /// <summary>The eight keyboard swatches.</summary>
    public static IReadOnlyList<(string Label, string Hex)> Keyboard { get; } =
    [
        ("青色", "#00ffff"),
        ("紫色", "#6366f1"),
        ("绿色", "#10b981"),
        ("红色", "#ef4444"),
        ("橙色", "#f59e0b"),
        ("粉色", "#ec4899"),
        ("蓝色", "#3b82f6"),
        ("白色", "#ffffff"),
    ];

    /// <summary>The six water-cooler LED swatches.</summary>
    public static IReadOnlyList<(string Label, string Hex)> Water { get; } =
    [
        ("经典翡翠绿", "#008000"),
        ("冰河赛博青", "#00e5ff"),
        ("极速烈焰红", "#ff1744"),
        ("幻夜魅影紫", "#d500f9"),
        ("耀金暖阳橙", "#ff9100"),
        ("纯白天际线", "#ffffff"),
    ];
}

/// <summary>The 闲置熄灯 options.</summary>
public static class SleepMinutes
{
    /// <summary>The choices, in minutes; zero means never switch the lights off.</summary>
    public static IReadOnlyList<uint> All { get; } = [0, 1, 5, 10, 15, 30];

    /// <summary>The label for a choice.</summary>
    public static string Label(uint minutes) => minutes switch
    {
        0 => "从不关闭",
        1 => "1 分钟",
        _ => $"{minutes} 分钟",
    };
}

/// <summary>The Chinese labels for the device switches.</summary>
/// <remarks>
/// The backend names the switches in English and in a fixed order; the id is what
/// the label is keyed off, so an unknown id still renders honestly.
/// </remarks>
public static class DeviceSwitches
{
    /// <summary>The label for a switch id.</summary>
    public static string Label(string id) => id switch
    {
        "usb_charge" => "关机下使用USB给电",
        "ac_recovery" => "来电自动开机 (AC Recovery)",
        "fn_lock" => "Fn锁定·F1-F12",
        "win_key_lock" => "Win键锁",
        "water_cooler" => "水冷魔盒 (BLE)",
        "bios_advanced" => "高级 BIOS 超频菜单",
        _ => "未知设备开关",
    };

    /// <summary>The one-line description of a switch.</summary>
    public static string Description(string id) => id switch
    {
        "usb_charge" => "可在关机或休眠状态下支持USB端口持续供电，方便为手机或外设应急充电。",
        "ac_recovery" => "开启后在关机状态下，接通外部电源适配器瞬间主板自动开机启动系统。",
        "fn_lock" => "交换 F1-F12 的媒体键与功能键行为，游戏与外设直控常用。",
        "win_key_lock" => "游戏模式：锁定 Windows 徽标键，防止全屏游戏中误触返回桌面。",
        "water_cooler" => "与蓝牙水冷魔盒联动；自改水冷或未原生支持的机型可在此强制开启。",
        "bios_advanced" => "向主板 UEFI NVRAM 同步写入高级菜单解锁标志位，重启后生效。",
        _ => "此开关未在本构建的设备开关表中声明。",
    };
}

/// <summary>The system page's log, OSD and power-scheme option tables.</summary>
public static class SystemOptions
{
    /// <summary>The log verbosity options, as <c>(label, wire value)</c>.</summary>
    public static IReadOnlyList<(string Label, string Wire)> LogLevels { get; } =
    [
        ("所有日志 (详细追踪)", "trace"),
        ("常规日志", "info"),
        ("错误日志 (仅故障及异常崩溃)", "error"),
    ];

    /// <summary>The 排障模块精准过滤 chips, as <c>(label, substring)</c>.</summary>
    public static IReadOnlyList<(string Label, string Wire)> LogFilters { get; } =
    [
        ("全部", ""),
        ("RGB与推流", "light"),
        ("电源与风扇", "power"),
        ("底层EC", "ec"),
        ("设备外设", "device"),
        ("窗口与系统", "ui"),
    ];

    /// <summary>The OSD theme options, as <c>(label, wire value)</c>.</summary>
    public static IReadOnlyList<(string Label, string Wire)> OsdThemes { get; } =
    [
        ("电影字幕", "cinema"),
        ("向右羽化", "fade"),
        ("磨砂胶囊", "pill"),
    ];

    /// <summary>The OSD position options, as <c>(label, wire value)</c>.</summary>
    public static IReadOnlyList<(string Label, string Wire)> OsdPositions { get; } =
    [
        ("左上角", "top-left"),
        ("下方中央", "bottom-center"),
        ("屏幕正中", "center"),
    ];

    /// <summary>The index of a stored log level, falling back to 常规日志.</summary>
    public static int LogLevelIndex(string? wire) => wire?.Trim().ToLowerInvariant() switch
    {
        "trace" or "debug" => 0,
        "error" or "off" or "warn" => 2,
        _ => 1,
    };

    /// <summary>The index of a stored filter substring; empty or unknown is 全部.</summary>
    public static int LogFilterIndex(string? wire)
    {
        var trimmed = wire?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            return 0;
        }

        for (var i = 0; i < LogFilters.Count; i++)
        {
            if (string.Equals(LogFilters[i].Wire, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>The index of a stored OSD theme, falling back to 电影字幕.</summary>
    public static int OsdThemeIndex(string? wire) => Index(OsdThemes, wire);

    /// <summary>The index of a stored OSD position, falling back to 左上角.</summary>
    public static int OsdPositionIndex(string? wire) => Index(OsdPositions, wire);

    private static int Index(IReadOnlyList<(string Label, string Wire)> options, string? wire)
    {
        var trimmed = wire?.Trim() ?? "";
        for (var i = 0; i < options.Count; i++)
        {
            if (string.Equals(options[i].Wire, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0;
    }
}

/// <summary>The fan-curve presets the tuning page offers.</summary>
public static class CurvePresets
{
    /// <summary>The label on the preset button.</summary>
    public static string Label(string wire) => wire switch
    {
        "silent" => "极致静音",
        "aggressive" => "激进压流",
        "linear" => "线性爬升",
        _ => "出厂智能",
    };

    /// <summary>The one-line explanation under the label.</summary>
    public static string Note(string wire) => wire switch
    {
        "silent" => "45°C 停转 · 75°C 前低转速",
        "aggressive" => "40% 快速起转 · 压温优先",
        "linear" => "40°C 至 95°C 等斜率",
        _ => "40°C 起转 · 5 点保守曲线",
    };

    /// <summary>The five points of a preset, as <c>(temperature, duty)</c>.</summary>
    public static IReadOnlyList<CurvePoint> Points(string wire)
    {
        (double T, double Duty)[] raw = wire switch
        {
            "silent" => [(45, 0), (60, 20), (75, 40), (88, 70), (95, 100)],
            "aggressive" => [(40, 40), (55, 60), (70, 80), (80, 95), (90, 100)],
            "linear" => [(40, 0), (54, 25), (68, 50), (82, 75), (95, 100)],
            _ => [(40, 20), (55, 35), (70, 60), (85, 85), (95, 100)],
        };

        return [.. raw.Select(p => new CurvePoint { Temp = p.T, Duty = p.Duty })];
    }
}

/// <summary>Helpers for the <c>#rrggbb</c> strings the backend exchanges.</summary>
public static class Hex
{
    /// <summary>The brand indigo, used when a stored colour cannot be parsed.</summary>
    public const string Fallback = "#6366f1";

    /// <summary>
    /// Normalises any user- or backend-supplied hex into <c>#rrggbb</c>.
    /// </summary>
    /// <remarks>
    /// Accepts <c>#abc</c>, <c>abcdef</c>, <c>#ABCDEF</c> and values with stray
    /// whitespace; anything else falls back to <see cref="Fallback"/> rather than
    /// producing a colour the EC would reject.
    /// </remarks>
    public static string Normalise(string? input)
    {
        var digits = new string([.. (input ?? "").Where(char.IsAsciiHexDigit)]).ToLowerInvariant();

        return digits.Length switch
        {
            6 => "#" + digits,
            3 => $"#{digits[0]}{digits[0]}{digits[1]}{digits[1]}{digits[2]}{digits[2]}",
            _ => Fallback,
        };
    }

    /// <summary>True when <paramref name="input"/> is a colour this build can round-trip.</summary>
    public static bool IsHex(string? input)
        => Normalise(input) != Fallback || Normalise(Fallback) == Normalise(input);

    /// <summary>Parses a hex string into a colour, falling back to the brand indigo.</summary>
    public static Color ToColor(string? input)
    {
        var normalised = Normalise(input);
        var value = Convert.ToUInt32(normalised[1..], 16);

        return Color.FromArgb(255, (byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }

    /// <summary>
    /// The ink that stays legible on top of a swatch: near-black on a light
    /// colour, near-white on a dark one.
    /// </summary>
    public static Color InkOn(string? input)
    {
        var c = ToColor(input);
        // Rec. 601 luma, which is close enough to how the eye ranks these.
        var luma = (0.299 * c.R) + (0.587 * c.G) + (0.114 * c.B);

        return luma > 150 ? Color.FromArgb(255, 16, 16, 16) : Color.FromArgb(255, 245, 245, 245);
    }
}
