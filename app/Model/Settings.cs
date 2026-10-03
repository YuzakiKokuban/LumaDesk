namespace JiYaoChu.Model;

/// <summary>One point on a fan or pump curve.</summary>
public sealed record CurvePoint
{
    public double Temp { get; init; }
    public double Duty { get; init; }
    public uint Volt { get; init; }
}

/// <summary>The persisted lighting configuration.</summary>
/// <remarks>
/// Field names mirror <c>LightingState</c> in <c>src\core\config.rs:324</c>
/// exactly — the keyboard settings carry a <c>kb_</c> prefix there, and the
/// whole struct travels back to the backend on every apply.
/// </remarks>
public sealed record LightingState
{
    public bool FirmwareManaged { get; init; }
    public bool Enabled { get; init; } = true;

    public LightingEngine KbEngine { get; init; } = LightingEngine.Hardware;

    public uint KbEffect { get; init; }

    /// <summary>0..4.</summary>
    public uint KbBrightness { get; init; } = 3;

    /// <summary><c>#rrggbb</c>.</summary>
    public string KbColor { get; init; } = "#ff00ff";

    /// <summary>15 | 30 | 45 | 60.</summary>
    public uint KbFps { get; init; } = 30;

    /// <summary>Kept separate from <see cref="KbEffect"/>: the streamer ids are 100..=113.</summary>
    public uint StreamerEffect { get; init; }

    public string? CustomScriptId { get; init; }

    public IReadOnlyList<string> FourZoneColors { get; init; } = ["#ff0000", "#00ff00", "#0000ff", "#ffffff"];

    public string LogoColor { get; init; } = "#ff00ff";
    public string LightbarColor { get; init; } = "#ff0000";
    public string HingeColor { get; init; } = "#00ffff";

    /// <summary>Idle minutes before the lighting sleeps; 0 = never.</summary>
    public uint SleepMinutes { get; init; }
}

/// <summary>What the driver currently reports back for the keyboard.</summary>
public sealed record LightingRuntime
{
    public uint Brightness { get; init; }
    public string Color { get; init; } = "";
    public uint Effect { get; init; }
    public string Engine { get; init; } = "";
    public uint Fps { get; init; }
    public uint SleepMinutes { get; init; }
    public bool WelcomeActive { get; init; }
}

/// <summary>Which lighting zones this machine actually has.</summary>
public sealed record LightingSupport
{
    public bool FourZone { get; init; }
    public bool Logo { get; init; }
    public bool Hinge { get; init; }
    public bool Lightbar { get; init; }
}

/// <summary>The composite lighting answer, including why the hardware path failed.</summary>
public sealed record LightingStatus
{
    public string Acpi { get; init; } = "";
    public string Backend { get; init; } = "";
    public LightingState? Configured { get; init; }
    public LightingRuntime? Runtime { get; init; }
    public string? RuntimeError { get; init; }
    public LightingSupport Supported { get; init; } = new();
}

/// <summary>What the ITE controller reports about itself.</summary>
public sealed record KeyboardHardwareInfo
{
    public string Backend { get; init; } = "";
    public string Controller { get; init; } = "";
    public uint Effects { get; init; }
    public bool FourZone { get; init; }
    public bool PerKey { get; init; }
    public string ProductId { get; init; } = "";
    public string Protocol { get; init; } = "";
    public string VendorId { get; init; } = "";
}

/// <summary>The persisted OSD configuration.</summary>
public sealed record OsdConfig
{
    public bool Enabled { get; init; } = true;
    public uint DurationMs { get; init; } = 2200;
    public uint Opacity { get; init; } = 60;
    public string Position { get; init; } = "bottom_right";
    public string Theme { get; init; } = "dark";
    public bool ShowOnPowerChange { get; init; } = true;
    public bool ShowOnRefreshChange { get; init; } = true;
}

/// <summary>The full persisted application configuration.</summary>
public sealed record AppConfig
{
    public byte PowerMode { get; init; }
    public byte PowerModeAc { get; init; }
    public byte PowerModeBattery { get; init; }
    public bool AutoPowerMode { get; init; }
    public GpuMode GpuMode { get; init; } = GpuMode.Hybrid;
    public BatteryMode BatteryMode { get; init; } = BatteryMode.Balanced;
    public uint BatteryLimit { get; init; } = 100;
    public bool FanBoost { get; init; }
    public string CoolerStrategy { get; init; } = "smart";
    public bool WaterCoolerEnabled { get; init; }
    public double CpuTempTargetMin { get; init; } = 80;
    public double CpuTempTargetMax { get; init; } = 95;
    public double CpuPl4Margin { get; init; }
    public double GpuOffsetStep { get; init; }
    public bool CpuSafetyGuard { get; init; } = true;
    public bool HighPerfScheme { get; init; }
    public IReadOnlyList<CurvePoint> CoolerCurvePoints { get; init; } = [];
    public LightingState Lighting { get; init; } = new();
    public OsdConfig Osd { get; init; } = new();
    public bool OsdEnabled { get; init; } = true;
    public bool DisplayTuningEnabled { get; init; }
    public uint RefreshRate { get; init; }
    public bool AutoMinRefreshOnBattery { get; init; } = true;
    public bool Autostart { get; init; }
    public bool TakeoverOem { get; init; }
    public bool UsbChargeEnabled { get; init; } = true;
    public bool AcRecoveryEnabled { get; init; } = true;
    public bool FnLockEnabled { get; init; }
    public bool WinKeyLocked { get; init; }
    public bool LogEnabled { get; init; } = true;
    public string LogLevel { get; init; } = "info";
    public string ActiveProfileId { get; init; } = "";
}

/// <summary>One Windows power scheme.</summary>
public sealed record PowerScheme
{
    public string Guid { get; init; } = "";
    public string Name { get; init; } = "";
    public bool Active { get; init; }
}

/// <summary>The power page's composite read.</summary>
public sealed record PowerSettings
{
    public byte? WindowsPowerMode { get; init; }
    public string ActiveScheme { get; init; } = "";
    public byte? PowerMode { get; init; }
    public string? PowerModeError { get; init; }
    public byte PowerModeAc { get; init; }
    public byte PowerModeBattery { get; init; }
    public bool AutoPowerMode { get; init; }
    public double CpuTempTargetMin { get; init; }
    public double CpuTempTargetMax { get; init; }
    public double CpuPl4Margin { get; init; }
    public double GpuOffsetStep { get; init; }
    public bool CpuSafetyGuard { get; init; }
    public bool HighPerfScheme { get; init; }
    public IReadOnlyList<PowerScheme> Schemes { get; init; } = [];
}

/// <summary>One row of the device-switch table.</summary>
public sealed record DeviceSwitch
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public bool Enabled { get; init; }
    public bool Supported { get; init; }
}

/// <summary>A named performance preset.</summary>
public sealed record ProfilePreset
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public bool Builtin { get; init; }
    public string BaseProject { get; init; } = "";
    public double CpuPl1 { get; init; }
    public double CpuPl2 { get; init; }
    public double GpuTgp { get; init; }
    public double GpuDb { get; init; }
    public byte PowerMode { get; init; }
    public IReadOnlyList<CurvePoint> FanCurve { get; init; } = [];
}

/// <summary>One monitor and the refresh rates it offers.</summary>
public sealed record DisplayInfo
{
    public string DeviceName { get; init; } = "";
    public string FriendlyName { get; init; } = "";
    public uint CurrentHz { get; init; }
    public IReadOnlyList<uint> AvailableHz { get; init; } = [];
}

/// <summary>The display tuning switches.</summary>
public sealed record DisplaySettings
{
    public bool DisplayTuningEnabled { get; init; }
    public bool AutoMinRefreshOnBattery { get; init; } = true;
    public uint RefreshRate { get; init; }
}

/// <summary>A named colour preset the display driver accepts.</summary>
public sealed record ColorPreset
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public bool Builtin { get; init; }
    public IReadOnlyDictionary<string, string> Values { get; init; } =
        new Dictionary<string, string>();
}

/// <summary>What the water cooler reports about itself.</summary>
public sealed record WaterCoolerStatus
{
    public bool Connected { get; init; }
    public double Duty { get; init; }
    public uint? FanRpm { get; init; }
    public double? PumpVolt { get; init; }
    public double? WaterTemp { get; init; }
    public string? Mac { get; init; }
    public string Strategy { get; init; } = "smart";
}

/// <summary>Whether the OEM control centre has been taken over.</summary>
public sealed record OemStatus
{
    public bool Marker { get; init; }
    public string MarkerPath { get; init; } = "";
    public bool TakenOver { get; init; }
}

/// <summary>The log file's location, verbosity and filter.</summary>
public sealed record LogStatus
{
    public bool Enabled { get; init; }
    public string Level { get; init; } = "info";
    public string Filter { get; init; } = "";
    public string Path { get; init; } = "";
}

/// <summary>What <c>system.ping</c> answers with.</summary>
public sealed record SystemPing
{
    public bool Pong { get; init; }
    public uint AbiVersion { get; init; }
    public string Version { get; init; } = "";
}
