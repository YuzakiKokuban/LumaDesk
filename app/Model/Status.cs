namespace JiYaoChu.Model;

/// <summary>Which graphics adapters are enabled.</summary>
public enum GpuMode
{
    Igpu,
    Hybrid,
    Dgpu,
}

/// <summary>How far the battery charges when plugged in.</summary>
public enum BatteryMode
{
    LongLife,
    Balanced,
    Workstation,
}

/// <summary>Which lighting engine drives the keyboard.</summary>
public enum LightingEngine
{
    Hardware,
    BetterRgb,
}

/// <summary>
/// The four cooling profiles, as stored in the configuration.
/// </summary>
/// <remarks>
/// The wire type is a bare <c>u8</c>. The ids are zero-based on this build —
/// <c>Api::set_power_mode</c> clamps with <c>min(3)</c> and the built-in presets
/// store <c>power_mode: 0..=3</c> — so this mirrors that numbering exactly.
/// </remarks>
public enum PowerMode : byte
{
    Office = 0,
    Balance = 1,
    Beast = 2,
    Custom = 3,
}

/// <summary>Temperature, clock, load and package power for the CPU.</summary>
public sealed record CpuStatus
{
    public double? Temp { get; init; }
    public double FreqMhz { get; init; }
    public double Load { get; init; }
    public double? PowerW { get; init; }
}

/// <summary>
/// The discrete (or only) graphics adapter.
/// </summary>
/// <remarks>
/// Every measurement is nullable: a laptop in hybrid mode powers the dGPU down
/// and the backend reports that honestly rather than inventing a zero.
/// </remarks>
public sealed record GpuStatus
{
    public bool Present { get; init; }
    public string Name { get; init; } = "";
    public double? Temp { get; init; }
    public double? FreqMhz { get; init; }
    public double? Load { get; init; }
    public ulong? VramUsedMb { get; init; }
    public ulong? VramTotalMb { get; init; }
}

/// <summary>Fan tachometers. Zero means the fan is stopped, not missing.</summary>
public sealed record FanStatus
{
    public bool Available { get; init; }
    public uint CpuRpm { get; init; }
    public uint GpuRpm { get; init; }
}

/// <summary>Battery charge, charge limit and wear.</summary>
public sealed record BatteryStatus
{
    public double Percent { get; init; }
    public bool Charging { get; init; }
    public uint Limit { get; init; } = 100;

    /// <summary>Full capacity as a share of design capacity, when both are known.</summary>
    public double? HealthPercent { get; init; }
}

/// <summary>Identity of the chassis this copy is running on.</summary>
public sealed record DeviceStatus
{
    public string Model { get; init; } = "Unknown";
    public string Project { get; init; } = "";
    public string Bios { get; init; } = "";
    public string Ec { get; init; } = "";
    public string Serial { get; init; } = "";
}

/// <summary>Which optional chassis features this machine actually exposes.</summary>
public sealed record SupportFlags
{
    public bool FanBoost { get; init; }
    public bool BatteryLimit { get; init; }
    public bool WaterCooler { get; init; }
    public bool FourZone { get; init; }
    public bool Logo { get; init; }
    public bool Hinge { get; init; }
    public bool Lightbar { get; init; }
    public bool BiosAdvanced { get; init; }
    public bool GpuSwitching { get; init; }
    public bool DisplayTuning { get; init; }
    public bool FanCurve { get; init; }
}

/// <summary>Whole-machine snapshot returned by <c>get_hardware_status</c>.</summary>
public sealed record HardwareStatus
{
    public CpuStatus Cpu { get; init; } = new();
    public GpuStatus Gpu { get; init; } = new();
    public FanStatus Fans { get; init; } = new();
    public BatteryStatus Battery { get; init; } = new();
    public DeviceStatus Device { get; init; } = new();
    public SupportFlags SupportFlags { get; init; } = new();

    /// <summary>The stored cooling profile; zero when the configuration is unreadable.</summary>
    public byte PowerMode { get; init; }

    public GpuMode? GpuMode { get; init; }
    public bool? FanBoost { get; init; }
    public string WindowsPowerScheme { get; init; } = "";
    public bool Elevated { get; init; }
}

/// <summary>Chinese names for the four cooling profiles.</summary>
public static class PowerModes
{
    /// <summary>The number of profiles the wire understands.</summary>
    public const byte Count = 4;

    /// <summary>Clamps any stored byte onto a real profile id.</summary>
    public static byte Clamp(byte mode) => mode > (byte)PowerMode.Custom ? (byte)PowerMode.Custom : mode;

    /// <summary>Label for a stored profile id, with a fallback for the unknown case.</summary>
    public static string Label(byte mode) => Clamp(mode) switch
    {
        (byte)PowerMode.Office => "办公",
        (byte)PowerMode.Balance => "均衡",
        (byte)PowerMode.Beast => "狂暴",
        _ => "自定义",
    };

    /// <summary>The one-word subtitle printed under the name.</summary>
    public static string Subtitle(byte mode) => Clamp(mode) switch
    {
        (byte)PowerMode.Office => "静音",
        (byte)PowerMode.Balance => "日常",
        (byte)PowerMode.Beast => "狂暴",
        _ => "极客调校",
    };

    /// <summary>The Latin tag the original printed beside the Chinese name.</summary>
    public static string Tag(byte mode) => Clamp(mode) switch
    {
        (byte)PowerMode.Office => "OFFICE",
        (byte)PowerMode.Balance => "BALANCED",
        (byte)PowerMode.Beast => "BEAST",
        _ => "CUSTOM",
    };

    /// <summary>The paragraph on a power-mode card.</summary>
    public static string Description(byte mode) => Clamp(mode) switch
    {
        0 => "使用办公风扇表与固件办公功耗默认值。",
        1 => "使用标准风扇表与固件游戏功耗默认值。",
        2 => "启用性能增强风扇档位，由 EC 管理性能功耗。",
        _ => "OEM 自定义档位尚未接入。",
    };

    /// <summary>The Windows power-scheme flavour each mode is mapped onto.</summary>
    public static string SchemeHint(byte mode) => Clamp(mode) switch
    {
        (byte)PowerMode.Office => "办公",
        (byte)PowerMode.Beast => "狂暴",
        _ => "均衡",
    };

    /// <summary>Every profile the UI can offer, in display order.</summary>
    public static IReadOnlyList<PowerMode> All { get; } =
    [
        PowerMode.Office,
        PowerMode.Balance,
        PowerMode.Beast,
    ];
}

/// <summary>Chinese names for the graphics modes.</summary>
public static class GpuModes
{
    /// <summary>Every mode, in the order the cards are drawn.</summary>
    public static IReadOnlyList<GpuMode> All { get; } = [GpuMode.Igpu, GpuMode.Hybrid, GpuMode.Dgpu];

    /// <summary>The wire spelling <c>set_gpu_mode</c> expects.</summary>
    public static string Wire(GpuMode mode) => mode switch
    {
        GpuMode.Igpu => "igpu",
        GpuMode.Dgpu => "dgpu",
        _ => "hybrid",
    };

    /// <summary>The short name printed on the card.</summary>
    public static string Name(GpuMode mode) => mode switch
    {
        GpuMode.Igpu => "纯集显",
        GpuMode.Hybrid => "标准混合",
        _ => "独显直连",
    };

    /// <summary>The English tag printed beside the name.</summary>
    public static string Tag(GpuMode mode) => mode switch
    {
        GpuMode.Igpu => "iGPU",
        GpuMode.Hybrid => "Dynamic",
        _ => "dGPU",
    };

    /// <summary>The descriptive copy for each mode, recovered verbatim.</summary>
    public static string Description(GpuMode mode) => mode switch
    {
        GpuMode.Igpu =>
            "彻底切断独立显卡 PCIe 供电与待机功耗，极致降低整机能耗与发热，极大提升续航。",
        GpuMode.Hybrid =>
            "轻载时核显输出以省电静音，高负载 3D 图形与游戏时自动介入独显加速。",
        _ =>
            "独立显卡物理直连内置屏幕。避开核显中转，发挥极致电竞帧率与超低延迟。",
    };

    /// <summary>The description used when the machine has no discrete card at all.</summary>
    public const string IgpuOnlyDescription =
        "原生高能效核显架构。彻底无独显与 MUX 功耗开销，实现极致低发热与超长电池续航。";

    /// <summary>How a mode is named in a status line.</summary>
    public static string Label(GpuMode? mode) => mode switch
    {
        GpuMode.Igpu => "核显模式",
        GpuMode.Hybrid => "混合输出",
        GpuMode.Dgpu => "独显直连",
        _ => "未读取到",
    };
}

public sealed record GpuModeInfo
{
    public GpuMode? ConfiguredMode { get; init; }
    public string Platform { get; init; } = "";
    public string Variable { get; init; } = "";
    public byte ApVersion { get; init; }
    public byte RawMode { get; init; }
    public bool Supported { get; init; }
    public bool SupportsIgpu { get; init; }
    public bool PendingReboot { get; init; }
    public string Reason { get; init; } = "";
}
