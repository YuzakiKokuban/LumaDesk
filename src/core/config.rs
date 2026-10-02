//! Application settings stored in `%APPDATA%\JiYaoChu`.
//! Missing keys use defaults; invalid JSON is preserved as config.json.bad.

use serde::{Deserialize, Serialize};
use std::path::{Path, PathBuf};

// ---------------------------------------------------------------- primitives

/// 0 = 办公 Office, 1 = 均衡 Balance, 2 = 狂暴 Beast, 3 = 自定义 Custom.
pub type PowerModeId = u8;

/// Serialises to `"igpu" | "hybrid" | "dgpu"`.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, Default)]
#[serde(rename_all = "snake_case")]
pub enum GpuMode {
    Igpu,
    #[default]
    Hybrid,
    Dgpu,
}

impl GpuMode {
    pub fn as_str(self) -> &'static str {
        match self {
            GpuMode::Igpu => "igpu",
            GpuMode::Hybrid => "hybrid",
            GpuMode::Dgpu => "dgpu",
        }
    }

    /// Parses the wire form; unknown values degrade to `hybrid`.
    pub fn from_wire(value: &str) -> Self {
        match value {
            "igpu" => GpuMode::Igpu,
            "dgpu" => GpuMode::Dgpu,
            _ => GpuMode::Hybrid,
        }
    }

    /// Parses the wire form, rejecting anything the UI could not have sent.
    pub fn from_wire_opt(value: &str) -> Option<Self> {
        match value.trim().to_ascii_lowercase().as_str() {
            "igpu" => Some(GpuMode::Igpu),
            "hybrid" => Some(GpuMode::Hybrid),
            "dgpu" => Some(GpuMode::Dgpu),
            _ => None,
        }
    }
}

/// Serialises to `"long_life" | "balanced" | "workstation"`.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, Default)]
#[serde(rename_all = "snake_case")]
pub enum BatteryMode {
    LongLife,
    #[default]
    Balanced,
    Workstation,
}

impl BatteryMode {
    pub fn as_str(self) -> &'static str {
        match self {
            BatteryMode::LongLife => "long_life",
            BatteryMode::Balanced => "balanced",
            BatteryMode::Workstation => "workstation",
        }
    }
}

/// Serialises to `"hardware" | "betterrgb"`.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, Default)]
#[serde(rename_all = "snake_case")]
pub enum LightingEngine {
    #[default]
    Hardware,
    BetterRgb,
}

impl LightingEngine {
    pub fn as_str(self) -> &'static str {
        match self {
            LightingEngine::Hardware => "hardware",
            LightingEngine::BetterRgb => "betterrgb",
        }
    }

    /// Parses the wire form, rejecting anything the UI could not have sent.
    pub fn from_wire_opt(value: &str) -> Option<Self> {
        match value.trim().to_ascii_lowercase().as_str() {
            "hardware" | "hw" => Some(LightingEngine::Hardware),
            "betterrgb" | "better_rgb" | "software" => Some(LightingEngine::BetterRgb),
            _ => None,
        }
    }
}

/// Clamps a lighting state into the documented ranges.
///
/// Split out of [`AppConfig::sanitise`] so the lighting commands can clean a
/// payload that arrives on its own, without the rest of the configuration.
pub fn sanitise_lighting(lighting: &mut LightingState) {
    lighting.kb_brightness = lighting.kb_brightness.min(4);
    lighting.kb_fps = match lighting.kb_fps {
        15 | 30 | 45 | 60 => lighting.kb_fps,
        _ => 30,
    };
    lighting.sleep_minutes = lighting.sleep_minutes.min(24 * 60);
    lighting.kb_color = normalise_hex(&lighting.kb_color);
    lighting.logo_color = normalise_hex(&lighting.logo_color);
    lighting.hinge_color = normalise_hex(&lighting.hinge_color);
    lighting.lightbar_color = normalise_hex(&lighting.lightbar_color);
    for colour in lighting.four_zone_colors.iter_mut() {
        *colour = normalise_hex(colour);
    }
    if let Some(script) = lighting.custom_script_id.as_ref() {
        if script.trim().is_empty() {
            lighting.custom_script_id = None;
        }
    }
}

/// Serialises to `"smart" | "silent" | "balanced" | "extreme"`.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, Default)]
#[serde(rename_all = "snake_case")]
pub enum CoolerStrategy {
    #[default]
    Smart,
    Silent,
    Balanced,
    Extreme,
}

impl CoolerStrategy {
    pub fn as_str(self) -> &'static str {
        match self {
            CoolerStrategy::Smart => "smart",
            CoolerStrategy::Silent => "silent",
            CoolerStrategy::Balanced => "balanced",
            CoolerStrategy::Extreme => "extreme",
        }
    }
}

// -------------------------------------------------------------- status types

/// Which optional chassis features this machine actually exposes.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct SupportFlags {
    #[serde(default)]
    pub fan_boost: bool,
    #[serde(default)]
    pub battery_limit: bool,
    pub water_cooler: bool,
    pub four_zone: bool,
    pub logo: bool,
    pub hinge: bool,
    pub lightbar: bool,
    pub bios_advanced: bool,
    pub gpu_switching: bool,
    pub display_tuning: bool,
    pub fan_curve: bool,
}

impl Default for SupportFlags {
    fn default() -> Self {
        Self {
            fan_boost: false,
            battery_limit: false,
            water_cooler: false,
            four_zone: false,
            logo: false,
            hinge: false,
            lightbar: false,
            bios_advanced: false,
            gpu_switching: false,
            display_tuning: true,
            fan_curve: false,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct CpuStatus {
    pub temp: Option<f64>,
    pub freq_mhz: f64,
    pub load: f64,
    pub power_w: Option<f64>,
}

impl Default for CpuStatus {
    fn default() -> Self {
        Self {
            temp: None,
            freq_mhz: 0.0,
            load: 0.0,
            power_w: None,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
pub struct GpuStatus {
    pub present: bool,
    pub name: String,
    pub temp: Option<f64>,
    pub freq_mhz: Option<f64>,
    pub load: Option<f64>,
    pub vram_used_mb: Option<u64>,
    pub vram_total_mb: Option<u64>,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
pub struct FanStatus {
    #[serde(default)]
    pub available: bool,
    pub cpu_rpm: u32,
    pub gpu_rpm: u32,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct BatteryStatus {
    pub percent: f64,
    pub charging: bool,
    #[serde(default)]
    pub on_ac: bool,
    pub limit: u32,
    pub health_percent: Option<f64>,
}

impl Default for BatteryStatus {
    fn default() -> Self {
        Self {
            percent: 0.0,
            charging: false,
            on_ac: false,
            limit: 100,
            health_percent: None,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct DeviceStatus {
    pub model: String,
    pub project: String,
    pub bios: String,
    pub ec: String,
    pub serial: String,
}

impl Default for DeviceStatus {
    fn default() -> Self {
        Self {
            model: "Unknown".into(),
            project: String::new(),
            bios: String::new(),
            ec: String::new(),
            serial: String::new(),
        }
    }
}

/// Whole-machine snapshot returned by `get_hardware_status`.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
pub struct HardwareStatus {
    pub cpu: CpuStatus,
    pub gpu: GpuStatus,
    pub fans: FanStatus,
    pub battery: BatteryStatus,
    pub device: DeviceStatus,
    pub support_flags: SupportFlags,
    pub power_mode: PowerModeId,
    pub gpu_mode: Option<GpuMode>,
    #[serde(default)]
    pub fan_boost: Option<bool>,
    pub windows_power_scheme: String,
    pub elevated: bool,
}

// ------------------------------------------------------------- curve / light

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct CurvePoint {
    pub temp: f64,
    /// 0..100
    pub duty: f64,
    /// Only meaningful for the water-cooler pump (7 / 8 / 11).
    #[serde(default)]
    pub volt: u32,
}

impl Default for CurvePoint {
    fn default() -> Self {
        Self {
            temp: 0.0,
            duty: 0.0,
            volt: 0,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct LightingState {
    #[serde(default)]
    pub firmware_managed: bool,
    pub enabled: bool,
    pub kb_engine: LightingEngine,
    pub kb_effect: u32,
    /// 0..4
    pub kb_brightness: u32,
    /// `#rrggbb`
    pub kb_color: String,
    /// 15 | 30 | 45 | 60
    pub kb_fps: u32,
    pub streamer_effect: u32,
    pub custom_script_id: Option<String>,
    pub four_zone_colors: [String; 4],
    pub logo_color: String,
    pub hinge_color: String,
    pub lightbar_color: String,
    /// Idle minutes before the lighting sleeps; 0 = never.
    pub sleep_minutes: u32,
}

impl Default for LightingState {
    fn default() -> Self {
        Self {
            firmware_managed: false,
            enabled: true,
            kb_engine: LightingEngine::Hardware,
            kb_effect: 0,
            kb_brightness: 3,
            kb_color: "#ff00ff".into(),
            kb_fps: 30,
            streamer_effect: 0,
            custom_script_id: None,
            four_zone_colors: [
                "#ff0000".into(),
                "#00ff00".into(),
                "#0000ff".into(),
                "#ffffff".into(),
            ],
            logo_color: "#ff00ff".into(),
            hinge_color: "#00ffff".into(),
            lightbar_color: "#ff0000".into(),
            sleep_minutes: 0,
        }
    }
}

// ------------------------------------------------------------------- osd

/// On-screen-display overlay configuration (`OsdConfig` in `bridge.ts`).
///
/// The original kept these in a separate `osd.json`; we fold them into the
/// config so there is exactly one place to look for user settings, while still
/// answering `get_osd_config` / `set_osd_config` with the original shape.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(default, rename_all = "snake_case")]
pub struct OsdConfig {
    pub enabled: bool,
    pub theme: String,
    pub position: String,
    /// 0..100
    pub opacity: u32,
    pub duration_ms: u32,
    pub show_on_power_change: bool,
    pub show_on_refresh_change: bool,
}

impl Default for OsdConfig {
    fn default() -> Self {
        Self {
            enabled: true,
            theme: "dark".into(),
            position: "bottom_right".into(),
            opacity: 85,
            duration_ms: 2_200,
            show_on_power_change: true,
            show_on_refresh_change: true,
        }
    }
}

impl OsdConfig {
    pub fn sanitise(&mut self) {
        self.opacity = self.opacity.min(100);
        // Keep the popup visible long enough to read but never permanent.
        self.duration_ms = self.duration_ms.clamp(600, 15_000);
        if self.theme.trim().is_empty() {
            self.theme = "dark".into();
        }
        if self.position.trim().is_empty() {
            self.position = "bottom_right".into();
        }
    }
}

// ----------------------------------------------------------------- app config

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(default, rename_all = "snake_case")]
pub struct AppConfig {
    pub power_mode: PowerModeId,
    pub power_mode_ac: PowerModeId,
    pub power_mode_battery: PowerModeId,
    pub fan_boost: bool,
    pub battery_limit: u32,
    pub battery_mode: BatteryMode,
    pub autostart: bool,
    pub log_enabled: bool,
    /// Persisted verbosity for `boot.log` (`error`/`warn`/`info`/`debug`/`trace`).
    ///
    /// Kept as a string rather than a typed enum so that a value written by a
    /// newer build (or hand-edited) still loads; `LogLevel::parse` falls back to
    /// `info` for anything it does not recognise.
    #[serde(default = "default_log_level")]
    pub log_level: String,
    pub gpu_mode: GpuMode,
    pub refresh_rate: u32,
    pub auto_min_refresh_on_battery: bool,
    pub high_perf_scheme: bool,
    pub cpu_safety_guard: bool,
    pub cpu_temp_target_min: f64,
    pub cpu_temp_target_max: f64,
    pub cpu_pl4_margin: f64,
    pub gpu_offset_step: f64,
    pub usb_charge_enabled: bool,
    pub ac_recovery_enabled: bool,
    pub fn_lock_enabled: bool,
    pub win_key_locked: bool,
    pub display_tuning_enabled: bool,
    pub water_cooler_enabled: bool,
    pub cooler_strategy: CoolerStrategy,
    pub cooler_curve_points: Vec<CurvePoint>,
    pub osd_enabled: bool,
    pub osd_theme: String,
    pub osd_position: String,
    pub osd: OsdConfig,
    pub takeover_oem: bool,
    pub active_profile_id: String,
    pub lighting: LightingState,
}

impl Default for AppConfig {
    fn default() -> Self {
        Self {
            power_mode: 1,
            power_mode_ac: 2,
            power_mode_battery: 0,
            fan_boost: false,
            battery_limit: 100,
            battery_mode: BatteryMode::Balanced,
            autostart: false,
            log_enabled: true,
            log_level: default_log_level(),
            gpu_mode: GpuMode::Hybrid,
            refresh_rate: 0,
            auto_min_refresh_on_battery: true,
            high_perf_scheme: false,
            cpu_safety_guard: true,
            cpu_temp_target_min: 80.0,
            cpu_temp_target_max: 95.0,
            cpu_pl4_margin: 0.0,
            gpu_offset_step: 0.0,
            usb_charge_enabled: true,
            ac_recovery_enabled: true,
            fn_lock_enabled: false,
            win_key_locked: false,
            display_tuning_enabled: false,
            water_cooler_enabled: false,
            cooler_strategy: CoolerStrategy::Smart,
            // A conservative default curve, mirroring the shape the original
            // wrote into models.json: 40 °C → 20 %, 95 °C → 100 %.
            cooler_curve_points: vec![
                CurvePoint {
                    temp: 40.0,
                    duty: 20.0,
                    volt: 0,
                },
                CurvePoint {
                    temp: 55.0,
                    duty: 35.0,
                    volt: 0,
                },
                CurvePoint {
                    temp: 70.0,
                    duty: 60.0,
                    volt: 0,
                },
                CurvePoint {
                    temp: 85.0,
                    duty: 85.0,
                    volt: 0,
                },
                CurvePoint {
                    temp: 95.0,
                    duty: 100.0,
                    volt: 0,
                },
            ],
            osd_enabled: true,
            osd_theme: "dark".into(),
            osd_position: "bottom_right".into(),
            osd: OsdConfig::default(),
            takeover_oem: false,
            active_profile_id: "builtin_balanced".into(),
            lighting: LightingState::default(),
        }
    }
}

impl AppConfig {
    /// Clamps the numeric fields that the UI is allowed to scrub, so a corrupt
    /// or hand-edited file can never drive the hardware outside sane bounds.
    pub fn sanitise(&mut self) {
        self.power_mode = self.power_mode.min(3);
        self.power_mode_ac = self.power_mode_ac.min(3);
        self.power_mode_battery = self.power_mode_battery.min(3);
        self.battery_limit = self.battery_limit.clamp(50, 100);
        self.refresh_rate = self.refresh_rate.min(1000);
        self.cpu_temp_target_min = self.cpu_temp_target_min.clamp(40.0, 100.0);
        self.cpu_temp_target_max = self.cpu_temp_target_max.clamp(40.0, 100.0);
        if self.cpu_temp_target_max < self.cpu_temp_target_min {
            std::mem::swap(&mut self.cpu_temp_target_min, &mut self.cpu_temp_target_max);
        }
        self.cpu_pl4_margin = self.cpu_pl4_margin.clamp(-50.0, 100.0);
        self.gpu_offset_step = self.gpu_offset_step.clamp(-500.0, 500.0);

        self.lighting.kb_brightness = self.lighting.kb_brightness.min(4);
        // The controller only accepts 15 / 30 / 45 / 60 Hz.
        self.lighting.kb_fps = match self.lighting.kb_fps {
            60 => 60,
            45 => 45,
            15 => 15,
            _ => 30,
        };
        for color in self.lighting.four_zone_colors.iter_mut() {
            *color = normalise_hex(color);
        }
        self.lighting.kb_color = normalise_hex(&self.lighting.kb_color);
        self.lighting.logo_color = normalise_hex(&self.lighting.logo_color);
        self.lighting.hinge_color = normalise_hex(&self.lighting.hinge_color);
        self.lighting.lightbar_color = normalise_hex(&self.lighting.lightbar_color);

        for point in self.cooler_curve_points.iter_mut() {
            point.temp = point.temp.clamp(0.0, 120.0);
            point.duty = point.duty.clamp(0.0, 100.0);
            point.volt = match point.volt {
                7 | 8 | 11 => point.volt,
                _ => 0,
            };
        }
        self.cooler_curve_points.sort_by(|a, b| {
            a.temp
                .partial_cmp(&b.temp)
                .unwrap_or(std::cmp::Ordering::Equal)
        });

        self.osd.sanitise();
        // The two representations of the OSD switch must never disagree.
        self.osd.enabled = self.osd_enabled;

        // An unrecognised or empty verbosity silently becomes `info`.
        if crate::core::services::logging::LogLevel::parse_strict(&self.log_level).is_none() {
            self.log_level = default_log_level();
        }
    }
}

/// Default `boot.log` verbosity, matching the original's shipped default.
fn default_log_level() -> String {
    "info".to_string()
}

/// Coerces any user-supplied colour into `#rrggbb`; invalid input becomes black.
pub fn normalise_hex(value: &str) -> String {
    let trimmed = value.trim();
    let body = trimmed.strip_prefix('#').unwrap_or(trimmed);
    let digits: String = body.chars().filter(|c| c.is_ascii_hexdigit()).collect();
    match digits.len() {
        6 => format!("#{}", digits.to_ascii_lowercase()),
        // Accept `#abc` shorthand by doubling each nibble.
        3 => {
            let mut out = String::from("#");
            for ch in digits.chars() {
                out.push(ch.to_ascii_lowercase());
                out.push(ch.to_ascii_lowercase());
            }
            out
        }
        8 => format!("#{}", digits[2..].to_ascii_lowercase()),
        _ => "#000000".into(),
    }
}

// ------------------------------------------------------------------ file IO

/// User data. The override is intended for isolated development/verification.
pub fn data_dir() -> PathBuf {
    if let Some(path) = std::env::var_os("JIYAOCHU_DATA_DIR") {
        return PathBuf::from(path);
    }
    dirs::config_dir()
        .unwrap_or_else(|| PathBuf::from("."))
        .join("JiYaoChu")
}

/// `%APPDATA%\JiYaoChu\config.json`
pub fn config_path() -> PathBuf {
    data_dir().join("config.json")
}

/// `%APPDATA%\JiYaoChu\models.json` — the per-machine power presets file that
/// `docs/models_customization_guide.md` documents.
pub fn models_path() -> PathBuf {
    data_dir().join("models.json")
}

/// `%APPDATA%\JiYaoChu\boot.log`
pub fn log_path() -> PathBuf {
    data_dir().join("boot.log")
}

/// `%APPDATA%\JiYaoChu\profiles` — user profile presets.
pub fn profiles_dir() -> PathBuf {
    data_dir().join("profiles")
}

/// `%APPDATA%\JiYaoChu\brfx` — user-authored BRFX lighting scripts.
pub fn brfx_dir() -> PathBuf {
    data_dir().join("brfx")
}

/// Creates the data directory (and the given sub-directory) if needed.
pub fn ensure_dir(path: &Path) -> std::io::Result<()> {
    std::fs::create_dir_all(path)
}

/// Loads the config, degrading to [`AppConfig::default`] on *any* problem.
///
/// A file that exists but does not parse is preserved as `config.json.bad` so
/// the user can recover hand-edited values.
pub fn load_config() -> (AppConfig, Option<String>) {
    let path = config_path();
    match std::fs::read_to_string(&path) {
        Ok(text) => match serde_json::from_str::<AppConfig>(&text) {
            Ok(mut cfg) => {
                cfg.sanitise();
                (cfg, None)
            }
            Err(err) => {
                let backup = path.with_extension("json.bad");
                let _ = std::fs::copy(&path, &backup);
                let mut cfg = AppConfig::default();
                cfg.sanitise();
                (
                    cfg,
                    Some(format!(
                        "config.json could not be parsed ({err}); defaults restored, \
                         the previous file was kept at {}",
                        backup.display()
                    )),
                )
            }
        },
        Err(err) if err.kind() == std::io::ErrorKind::NotFound => {
            let mut cfg = AppConfig::default();
            cfg.sanitise();
            (cfg, None)
        }
        Err(err) => {
            let mut cfg = AppConfig::default();
            cfg.sanitise();
            (cfg, Some(format!("config.json could not be read: {err}")))
        }
    }
}

/// Writes the config atomically (temp file + rename) so a crash mid-write can
/// never leave a half-written file behind.
pub fn save_config(cfg: &AppConfig) -> std::io::Result<()> {
    let dir = data_dir();
    ensure_dir(&dir)?;
    let path = config_path();
    let tmp = path.with_extension("json.tmp");
    let text = serde_json::to_string_pretty(cfg)
        .map_err(|e| std::io::Error::new(std::io::ErrorKind::InvalidData, e))?;
    std::fs::write(&tmp, text)?;
    std::fs::rename(&tmp, &path)?;
    Ok(())
}
