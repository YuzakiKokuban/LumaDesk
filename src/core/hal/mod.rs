//! Synchronous hardware interface shared by Windows and mock backends.
//! The WinUI shell executes blocking calls on its backend worker.

mod cache;
pub mod mock;
// The real backend: the safe wrappers are Windows-only, while `WindowsHal` and
// the elevation helper compile everywhere so that the mock and the UI can call
// them on any platform.
#[cfg(windows)]
pub mod winapi;
pub mod windows;

use crate::core::config::AppConfig;
use crate::core::config::{
    BatteryMode, BatteryStatus, CoolerStrategy, CurvePoint, DeviceStatus, FanStatus, GpuMode,
    GpuStatus, HardwareStatus, LightingState, PowerModeId, SupportFlags,
};
use crate::core::error::{HalError, HalResult};
use serde::{Deserialize, Serialize};

/// Live water-cooler state (the pump is an out-of-band BLE accessory).
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct WaterCoolerStatus {
    pub connected: bool,
    pub mac: Option<String>,
    pub water_temp: Option<f64>,
    pub pump_volt: Option<f64>,
    pub fan_rpm: Option<u32>,
    pub duty: f64,
    pub strategy: CoolerStrategy,
}

impl Default for WaterCoolerStatus {
    fn default() -> Self {
        Self {
            connected: false,
            mac: None,
            water_temp: None,
            pump_volt: None,
            fan_rpm: None,
            duty: 0.0,
            strategy: CoolerStrategy::Smart,
        }
    }
}

/// A named colour preset for the display tuning page.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct ColorPreset {
    pub id: String,
    pub name: String,
    pub builtin: bool,
    pub values: std::collections::BTreeMap<String, String>,
}

/// A power/thermal profile preset.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct ProfilePreset {
    pub id: String,
    pub name: String,
    pub builtin: bool,
    pub base_project: String,
    pub power_mode: PowerModeId,
    pub cpu_pl1: f64,
    pub cpu_pl2: f64,
    pub gpu_tgp: f64,
    pub gpu_db: f64,
    pub fan_curve: Vec<CurvePoint>,
}

/// One optional chassis device that can be switched on or off.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct DeviceSwitch {
    pub id: String,
    pub name: String,
    pub enabled: bool,
    pub supported: bool,
}

/// A Windows power scheme as reported by `PowerEnumerate`.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct PowerScheme {
    pub guid: String,
    pub name: String,
    pub active: bool,
}

/// A connected display and its currently supported refresh rates.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct DisplayInfo {
    pub device_name: String,
    pub friendly_name: String,
    pub current_hz: u32,
    pub available_hz: Vec<u32>,
}

/// Live state of one RGB channel.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct LightingRuntimeStatus {
    pub engine: String,
    pub effect: u32,
    pub brightness: u32,
    pub fps: u32,
    pub color: String,
    pub sleep_minutes: u32,
    pub welcome_active: bool,
}

/// Everything the UI can ask the hardware to do.
pub trait HardwareHal: Send + Sync {
    /// Human-readable name of this backend, for the log and the UI.
    fn backend_name(&self) -> &'static str;

    // -------------------------------------------------------------- telemetry
    fn hardware_status(&self, cfg: &crate::core::config::AppConfig) -> HalResult<HardwareStatus>;
    fn cpu_status(&self) -> HalResult<crate::core::config::CpuStatus>;
    fn gpu_status(&self) -> HalResult<GpuStatus>;
    fn fan_status(&self) -> HalResult<FanStatus>;
    fn battery_status(&self, limit: u32) -> HalResult<BatteryStatus>;
    fn device_status(&self) -> HalResult<DeviceStatus>;
    fn support_flags(&self) -> HalResult<SupportFlags>;
    fn is_elevated(&self) -> bool;

    // ------------------------------------------------------------------ power
    fn set_power_mode(&self, mode: PowerModeId) -> HalResult<()>;
    fn get_power_mode(&self) -> HalResult<PowerModeId>;
    fn set_fan_boost(&self, enabled: bool) -> HalResult<()>;
    fn get_fan_boost(&self) -> HalResult<bool>;
    /// Toggles the custom fan-curve control loop.
    fn toggle_fan_curve_control(
        &self,
        cpu_curve: &[CurvePoint],
        gpu_curve: &[CurvePoint],
    ) -> HalResult<()>;
    /// Pushes a live fan curve to the EC.
    fn apply_fan_curve_live(
        &self,
        cpu_curve: &[CurvePoint],
        gpu_curve: &[CurvePoint],
    ) -> HalResult<()>;
    /// Fan ramp-rate limiting, in milliseconds between steps.
    fn set_fan_ramp_rate(&self, speed_ms: u32) -> HalResult<()>;
    /// Keeps the two fan channels independent instead of mirroring them.
    fn set_fan_isolated_output(&self, enabled: bool) -> HalResult<()>;
    fn set_sleep_auto_off(&self, enabled: bool) -> HalResult<()>;
    fn set_master_sleep_guard(&self, enabled: bool) -> HalResult<()>;

    // ---------------------------------------------------------------- battery
    fn set_battery_limit(&self, limit: u32) -> HalResult<()>;
    fn set_battery_hardware_limit(&self, limit: u32) -> HalResult<()>;
    fn set_battery_mode(&self, mode: BatteryMode) -> HalResult<()>;
    fn battery_health_percent(&self) -> HalResult<Option<f64>>;

    // -------------------------------------------------------------------- gpu
    fn set_gpu_mode(&self, mode: GpuMode) -> HalResult<()>;
    fn gpu_mode_info(&self, cfg: &AppConfig) -> HalResult<crate::core::driver::uefi::GpuModeInfo> {
        if self.backend_name() == "mock" {
            return Ok(crate::core::driver::uefi::GpuModeInfo {
                configured_mode: Some(cfg.gpu_mode),
                platform: "simulated".into(),
                variable: "simulated".into(),
                ap_version: 0,
                raw_mode: 0,
                supported: true,
                supports_igpu: true,
                pending_reboot: false,
                reboot_status_known: true,
                reason: String::new(),
            });
        }
        Err(HalError::unsupported("GPU firmware status"))
    }

    // ---------------------------------------------------------------- display
    fn switch_refresh_rate(&self, hz: u32) -> HalResult<()>;
    fn set_display_monitor_refresh_rate(&self, device_name: &str, hz: u32) -> HalResult<()>;
    fn display_brightness(&self) -> HalResult<u32>;
    fn set_display_brightness(&self, percent: u32) -> HalResult<()>;
    fn display_tuning_state(&self) -> HalResult<bool>;
    fn set_display_tuning_enabled(&self, enabled: bool) -> HalResult<()>;
    fn apply_display_color_preset(&self, preset: &str) -> HalResult<()>;
    fn color_calibration_state(&self) -> HalResult<bool>;
    fn set_color_calibration_enabled(&self, enabled: bool) -> HalResult<()>;

    // --------------------------------------------------------------- lighting
    fn lighting_state(&self) -> HalResult<LightingState>;
    fn lighting_runtime_status(&self) -> HalResult<LightingRuntimeStatus>;
    fn apply_keyboard_lighting(&self, state: &LightingState) -> HalResult<()>;
    fn apply_logo_lighting(&self, effect: u32, color: &str) -> HalResult<()>;
    fn apply_hinge_lighting(&self, speed: u32, color: &str) -> HalResult<()>;
    fn apply_lightbar_lighting(&self, effect: u32, color: &str) -> HalResult<()>;
    fn apply_four_zone_colors(&self, zones: [&str; 4]) -> HalResult<()>;
    fn set_lighting_sleep_timer(&self, minutes: u32) -> HalResult<()>;
    fn trigger_lighting_welcome(&self) -> HalResult<()>;
    fn keyboard_hardware_info(&self) -> HalResult<serde_json::Value>;
    /// Detects the RGB controller the same way the original did.
    fn detect_lighting_support(&self) -> HalResult<SupportFlags>;

    // ----------------------------------------------------------- water cooler
    fn water_cooler_status(&self) -> HalResult<WaterCoolerStatus>;
    fn set_water_cooler_enabled(&self, enabled: bool) -> HalResult<()>;
    fn apply_cooler_strategy(
        &self,
        strategy: CoolerStrategy,
        points: &[CurvePoint],
    ) -> HalResult<()>;
    fn set_water_cooler_speed(&self, duty: f64) -> HalResult<()>;
    fn set_water_cooler_led(&self, color: &str, mode: u32) -> HalResult<()>;
    fn reset_water_cooler(&self) -> HalResult<()>;

    // ------------------------------------------------------------ misc system
    fn set_win_key_locked(&self, locked: bool) -> HalResult<()>;
    fn set_fn_lock(&self, enabled: bool) -> HalResult<()>;
    fn get_fn_lock(&self) -> HalResult<bool>;
    fn set_usb_charge(&self, enabled: bool) -> HalResult<()>;
    fn set_ac_recovery(&self, enabled: bool) -> HalResult<()>;
    fn toggle_bios_advanced_menu(&self, enable: bool) -> HalResult<()>;
    fn bios_advanced_menu_status(&self) -> HalResult<bool>;

    // ------------------------------------------------------------ system info
    fn windows_power_schemes(&self) -> HalResult<Vec<PowerScheme>>;
    fn set_active_windows_power_scheme(&self, guid: &str) -> HalResult<()>;
    fn active_windows_power_scheme(&self) -> HalResult<String>;
    fn display_list(&self) -> HalResult<Vec<DisplayInfo>>;

    // ---------------------------------------------------------- custom tweaks
    /// CPU/GPU power-target tweaks that in the original went straight to the EC.
    fn apply_live_custom_tweak(&self, tweak: &LiveTweak) -> HalResult<()>;
    /// Re-writes the GPU power contract after a driver reset.
    fn heal_gpu_power_contract(&self) -> HalResult<()>;
    fn set_cpu_freq_limit(&self, mhz: u32) -> HalResult<()>;
    fn reset_cpu_freq_limit(&self) -> HalResult<()>;

    /// The device currently registered for the optional MiniDrawer window.
    fn open_mini_drawer(&self) -> HalResult<()> {
        Err(crate::core::error::HalError::unsupported(
            "mini drawer is a UI feature and must be opened by the shell",
        ))
    }
}

/// The recovered `apply_live_custom_tweak` payload.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
#[serde(default, rename_all = "snake_case")]
pub struct LiveTweak {
    pub temp_target: f64,
    pub ctgp_enabled: bool,
    pub ctgp_watts: f64,
    pub db_enabled: bool,
    pub db_watts: f64,
    pub super_perf: bool,
    pub gpu_core_offset: f64,
    pub gpu_mem_offset: f64,
}

/// Chooses the backend for this process.
///
/// `JIYAOCHU_FORCE_MOCK=1` forces the simulator, which is what CI and the
/// frontend developers use. Otherwise Windows gets the real implementation and
/// any other OS gets the simulator.
pub fn create_hal() -> std::sync::Arc<dyn HardwareHal> {
    if std::env::var("JIYAOCHU_FORCE_MOCK")
        .map(|v| v == "1")
        .unwrap_or(false)
    {
        crate::core::services::logging::warn(
            "JIYAOCHU_FORCE_MOCK=1 — using the hardware simulator",
        );
        return std::sync::Arc::new(mock::MockHal::new());
    }

    #[cfg(windows)]
    {
        std::sync::Arc::new(windows::WindowsHal::new())
    }
    #[cfg(not(windows))]
    {
        crate::core::services::logging::warn(
            "this build is not running on Windows — falling back to the hardware simulator",
        );
        std::sync::Arc::new(mock::MockHal::new())
    }
}
#[cfg(windows)]
pub mod win_key;
