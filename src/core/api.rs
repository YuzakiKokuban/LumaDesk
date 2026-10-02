//! Command facade shared by the WinUI front end and CLI.
//! This is an independent implementation using recovered protocol evidence.
//! Unsupported hardware features report errors; configuration is not proof
//! that a hardware setting has taken effect.

use crate::core::config::{
    AppConfig, BatteryMode, CoolerStrategy, CurvePoint, GpuMode, HardwareStatus, LightingEngine,
    LightingState, OsdConfig,
};
use crate::core::error::HalError;
use crate::core::events::Event;
use crate::core::hal::{
    ColorPreset, DeviceSwitch, DisplayInfo, LightingRuntimeStatus, LiveTweak, PowerScheme,
    ProfilePreset, WaterCoolerStatus,
};
use crate::core::services::models::ModelCatalogue;
use crate::core::services::presets;
use crate::core::state::AppState;

/// The reason every device-facing water-cooler call fails on this build.
///
/// Public so the UI can explain the unavailability without going through
/// [`Api::get_water_cooler_reason`].
pub const WATER_COOLER_BLE_REASON: &str =
    "the water cooler is driven over BLE and the original build's service \
     and characteristic layout was not recovered";

/* ------------------------------------------------------------------ helpers */

/// Converts a HAL error into the string the frontend used to receive.
///
/// `HalError::Unsupported` keeps its `not supported on this build:` prefix, so
/// the UI can tell "this machine cannot do it" apart from "the call failed".
fn fail(error: HalError) -> String {
    error.to_string()
}

/// Borrows the persisted config for a read-only call.
fn config_snapshot(state: &AppState) -> AppConfig {
    state.config()
}

/// Runs `powercfg` with the given arguments and no console window.
///
/// Used by the power-option calls; the caller is responsible for applying the
/// active scheme afterwards so the change takes effect immediately.
fn powercfg(args: &[&str]) -> Result<String, String> {
    use std::process::Command;

    #[cfg(windows)]
    const CREATE_NO_WINDOW: u32 = 0x0800_0000;

    let mut command = Command::new("powercfg");
    command.args(args);
    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        command.creation_flags(CREATE_NO_WINDOW);
    }

    match command.output() {
        Ok(output) if output.status.success() => {
            Ok(String::from_utf8_lossy(&output.stdout).trim().to_string())
        }
        Ok(output) => {
            let stderr = String::from_utf8_lossy(&output.stderr).trim().to_string();
            let stdout = String::from_utf8_lossy(&output.stdout).trim().to_string();
            let detail = if stderr.is_empty() { stdout } else { stderr };
            Err(format!("powercfg {} failed: {detail}", args.join(" ")))
        }
        Err(error) => Err(format!("powercfg could not be started: {error}")),
    }
}

/// Resolves a `hz` request against the rates a panel actually supports.
///
/// `hz = 0` means "the highest rate this panel advertises" and `hz = 1` means
/// "the lowest rate", which is what the battery-saver toggle switches to.
fn resolve_rate(
    state: &AppState,
    device: Option<&str>,
    hz: u32,
) -> Result<(String, u32), HalError> {
    let displays = state.hal().display_list()?;
    let chosen = match device {
        Some(name) => displays
            .iter()
            .find(|display| display.device_name.eq_ignore_ascii_case(name))
            .ok_or_else(|| {
                HalError::unavailable(format!("no display named '{name}' is attached"))
            })?,
        None => displays
            .first()
            .ok_or_else(|| HalError::unavailable("no display is attached"))?,
    };

    let mut rates = chosen.available_hz.clone();
    rates.sort_unstable();
    rates.dedup();

    if rates.is_empty() {
        return Err(HalError::unavailable(format!(
            "{} does not report any refresh rates",
            chosen.device_name
        )));
    }

    let rate = match hz {
        0 => *rates.last().unwrap_or(&chosen.current_hz),
        1 => rates[0],
        wanted => {
            if !rates.contains(&wanted) {
                return Err(HalError::unsupported(format!(
                    "{} does not support {wanted} Hz (available: {:?})",
                    chosen.device_name, rates
                )));
            }
            wanted
        }
    };

    Ok((chosen.device_name.clone(), rate))
}

/// Loads the machine catalogue, reporting a load warning through the log.
fn catalogue() -> ModelCatalogue {
    let (catalogue, warning) = ModelCatalogue::load();
    if let Some(warning) = warning {
        crate::core::services::logging::warn(warning);
    }
    catalogue
}

/// Turns a display name into a stable file-name-safe id.
fn slug(name: &str) -> String {
    let mut out = String::new();
    for character in name.chars() {
        if character.is_ascii_alphanumeric() {
            out.push(character.to_ascii_lowercase());
        } else if (character == ' ' || character == '-' || character == '_') && !out.ends_with('_')
        {
            out.push('_');
        }
    }
    let trimmed = out.trim_matches('_').to_string();
    if trimmed.is_empty() {
        format!("profile_{}", crate::core::services::logging::unix_seconds())
    } else {
        trimmed
    }
}

/// Applies an autostart change with the original build's error strings.
///
/// The Tauri build wrapped `tauri-plugin-autostart` failures itself
/// ("autostart could not be enabled: —"), so the same wrapping is reproduced
/// here. The one exception is a platform that has no autostart at all: that
/// typed `not supported on this build:` error is passed through untouched,
/// because the UI keys off that exact prefix.
fn apply_autostart(enabled: bool) -> Result<(), String> {
    crate::core::autostart::set_enabled(enabled).map_err(|error| {
        if error.starts_with("not supported on this build:") {
            error
        } else {
            format!(
                "autostart could not be {}: {error}",
                if enabled { "enabled" } else { "disabled" }
            )
        }
    })
}

/* ---------------------------------------------------------------------- api */

/// Owns the application state and exposes the whole native API.
///
/// Every method is synchronous and takes `&self`; the state behind it is
/// internally synchronised, so a caller may share one `Api` between the UI
/// thread and a telemetry loop (`Arc<Api>`).
pub struct Api {
    state: AppState,
}

impl Api {
    /// Wraps a fully built [`AppState`].
    pub fn new(state: AppState) -> Self {
        Self { state }
    }

    /// Escape hatch for the shell: telemetry refresh, event drain, config
    /// errors. The rest of the surface goes through the methods below.
    pub fn state(&self) -> &AppState {
        &self.state
    }

    /* -------------------------------------------------------- status / cfg */

    /// Live hardware snapshot: temperatures, fan speeds, battery, device
    /// identity and which optional features this machine actually supports.
    pub fn get_hardware_status(&self) -> Result<HardwareStatus, String> {
        let config = config_snapshot(&self.state);
        let status = self.state.hal().hardware_status(&config).map_err(fail)?;
        self.state.set_hardware(status.clone());
        Ok(status)
    }

    /// The persisted application configuration.
    pub fn get_app_config(&self) -> Result<AppConfig, String> {
        Ok(config_snapshot(&self.state))
    }

    /// Persists the whole configuration.
    ///
    /// Side effects that the config implies (power mode, GPU mode) are applied
    /// afterwards; a HAL that refuses one of them does not prevent the rest of
    /// the configuration from being stored, but the refusal is reported.
    pub fn save_app_config(&self, cfg: AppConfig) -> Result<(), String> {
        let mut warnings: Vec<String> = Vec::new();

        let hal = self.state.hal().clone();
        if let Err(error) = hal.set_power_mode(cfg.power_mode) {
            warnings.push(format!("power mode: {error}"));
        }
        if let Err(error) = hal.set_fan_boost(cfg.fan_boost) {
            warnings.push(format!("fan boost: {error}"));
        }
        let gpu_changed = self
            .state
            .with_config(|current| current.gpu_mode != cfg.gpu_mode);
        if gpu_changed {
            if let Err(error) = hal.set_gpu_mode(cfg.gpu_mode) {
                warnings.push(format!("GPU mode: {error}"));
            }
        }

        self.state.with_config(|current| *current = cfg);

        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the configuration could not be saved: {error}"))?;

        if warnings.is_empty() {
            Ok(())
        } else {
            // The configuration *was* stored; surface the partial failure
            // textually so the UI can show a warning banner without losing the
            // save.
            Err(format!("saved with warnings: {}", warnings.join("; ")))
        }
    }

    /// Replaces the machine catalogue with an imported one.
    pub fn import_models_json(&self, json_content: String) -> Result<(), String> {
        let (mut catalogue, _) = ModelCatalogue::load();
        catalogue.import_json(&json_content).map_err(fail)?;
        catalogue.save().map_err(fail)?;
        crate::core::services::logging::info("machine catalogue replaced by an imported file");
        Ok(())
    }

    /* -------------------------------------------------------------- power */

    /// `mode` is the numbered power mode: 0 Office, 1 Balance, 2 Beast,
    /// 3 Custom.
    pub fn set_power_mode(&self, mode: u8) -> Result<(), String> {
        let mode = mode.min(3);
        self.state.hal().set_power_mode(mode).map_err(fail)?;
        self.state.with_config(|config| config.power_mode = mode);
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the power mode was applied but not saved: {error}"))?;
        Ok(())
    }

    pub fn set_windows_power_mode(&self, mode: u8) -> Result<(), String> {
        #[cfg(windows)]
        {
            crate::core::hal::winapi::set_user_power_mode(mode).map_err(fail)
        }
        #[cfg(not(windows))]
        {
            let _ = mode;
            Err("Windows 11 required".into())
        }
    }

    pub fn set_fan_boost(&self, enabled: bool) -> Result<(), String> {
        self.state.hal().set_fan_boost(enabled).map_err(fail)?;
        self.state.with_config(|config| config.fan_boost = enabled);
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("fan boost was applied but not saved: {error}"))?;
        Ok(())
    }

    pub fn set_battery_limit(&self, limit: u32) -> Result<(), String> {
        let limit = limit.clamp(50, 100);
        self.state.hal().set_battery_limit(limit).map_err(fail)?;
        self.state
            .with_config(|config| config.battery_limit = limit);
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the battery limit was applied but not saved: {error}"))?;
        Ok(())
    }

    /// Writes the charge limit into the battery controller itself.
    ///
    /// This is the "hardware" variant of [`Api::set_battery_limit`]; it only
    /// works when the vendor driver exposes the charge-limit register.
    pub fn set_battery_hardware_limit(&self, limit: u32) -> Result<(), String> {
        self.state
            .hal()
            .set_battery_hardware_limit(limit.clamp(50, 100))
            .map_err(fail)
    }

    pub fn set_active_windows_power_scheme(&self, guid: String) -> Result<(), String> {
        self.state
            .hal()
            .set_active_windows_power_scheme(&guid)
            .map_err(fail)?;
        self.state
            .with_config(|config| config.high_perf_scheme = false);
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the scheme was applied but not recorded: {error}"))?;
        Ok(())
    }

    /// Sets one `powercfg` value on the active scheme, for both AC and DC.
    ///
    /// `option` is a powercfg setting alias such as `PERFBOOSTMODE` or
    /// `PROCTHROTTLEMAX`; `value` is written to both the AC and DC indexes so
    /// the laptop behaves the same on battery (which is what the original's
    /// single "value" argument implied).
    pub fn set_power_option(&self, option: String, value: i64) -> Result<(), String> {
        let alias = option.trim();
        if alias.is_empty() {
            return Err("a powercfg option alias is required".into());
        }
        if !alias
            .chars()
            .all(|character| character.is_ascii_alphanumeric())
        {
            return Err(format!("'{alias}' is not a valid powercfg alias"));
        }

        // `/setacvalueindex` resolves the alias against the *active* scheme;
        // find the GUID first and pass it explicitly so the call cannot
        // silently target a different scheme.
        let scheme = self
            .state
            .hal()
            .active_windows_power_scheme()
            .map_err(fail)?;
        let value = value.to_string();

        // `powercfg /setacvalueindex <scheme> SUB_NONE <alias> <value>`
        let ac = powercfg(&[
            "/setacvalueindex",
            &scheme,
            "SUB_NONE",
            alias,
            value.as_str(),
        ])?;
        let dc = powercfg(&[
            "/setdcvalueindex",
            &scheme,
            "SUB_NONE",
            alias,
            value.as_str(),
        ])?;
        powercfg(&["/setactive", &scheme])?;

        crate::core::services::logging::info(format!(
            "powercfg {alias} = {value} on {scheme} (ac: {ac}, dc: {dc})"
        ));
        Ok(())
    }

    /// Applies a custom-mode tweak bundle to the EC/firmware.
    ///
    /// Needs the vendor driver; the parameter names are the camelCase keys the
    /// frontend used to send (`tempTarget`, `ctgpEnabled`, …).
    #[allow(clippy::too_many_arguments)]
    pub fn apply_live_custom_tweak(
        &self,
        temp_target: f64,
        ctgp_enabled: bool,
        ctgp_watts: f64,
        db_enabled: bool,
        db_watts: f64,
        super_perf: bool,
        gpu_core_offset: f64,
        gpu_mem_offset: f64,
    ) -> Result<(), String> {
        let tweak = LiveTweak {
            temp_target,
            ctgp_enabled,
            ctgp_watts,
            db_enabled,
            db_watts,
            super_perf,
            gpu_core_offset,
            gpu_mem_offset,
        };
        self.state
            .hal()
            .apply_live_custom_tweak(&tweak)
            .map_err(fail)
    }

    /// Verbose name preserved from the original wire surface: was
    /// `superPerfEnable`.
    pub fn super_perf_enable(&self, target_db_watts: f64) -> Result<(), String> {
        let tweak = LiveTweak {
            temp_target: 90.0,
            ctgp_enabled: true,
            ctgp_watts: target_db_watts,
            db_enabled: true,
            db_watts: target_db_watts,
            super_perf: true,
            gpu_core_offset: 0.0,
            gpu_mem_offset: 0.0,
        };
        self.state
            .hal()
            .apply_live_custom_tweak(&tweak)
            .map_err(fail)
    }

    /// Re-applies the stock GPU power limits after a driver reset.
    pub fn heal_gpu_power_contract(&self) -> Result<(), String> {
        self.state.hal().heal_gpu_power_contract().map_err(fail)
    }

    /* ---------------------------------------------------------------- fan */

    pub fn apply_fan_curve_live(
        &self,
        cpu_curve: Vec<CurvePoint>,
        gpu_curve: Vec<CurvePoint>,
    ) -> Result<(), String> {
        let clean = |mut curve: Vec<CurvePoint>| {
            for point in curve.iter_mut() {
                point.temp = point.temp.clamp(0.0, 120.0);
                point.duty = point.duty.clamp(0.0, 100.0);
                point.volt = match point.volt {
                    7 | 8 | 11 => point.volt,
                    _ => 0,
                };
            }
            curve.sort_by(|a, b| {
                a.temp
                    .partial_cmp(&b.temp)
                    .unwrap_or(std::cmp::Ordering::Equal)
            });
            curve
        };
        let cpu = clean(cpu_curve);
        let gpu = clean(gpu_curve);
        if cpu.is_empty() || gpu.is_empty() {
            return Err("a fan curve needs at least one point".into());
        }
        self.state
            .hal()
            .apply_fan_curve_live(&cpu, &gpu)
            .map_err(fail)
    }

    pub fn toggle_fan_curve_control(&self, enabled: bool) -> Result<(), String> {
        // The recovered surface passed both curves with this call; `enabled` is
        // the documented bridge signature, so the stored curves are sent
        // instead.
        let cpu = self
            .state
            .with_config(|config| config.cooler_curve_points.clone());
        if enabled {
            self.state
                .hal()
                .toggle_fan_curve_control(&cpu, &cpu)
                .map_err(fail)
        } else {
            self.state
                .hal()
                .toggle_fan_curve_control(&[], &[])
                .map_err(fail)
        }
    }

    pub fn toggle_fan_ramp_rate(&self, speed_ms: u32) -> Result<(), String> {
        self.state
            .hal()
            .set_fan_ramp_rate(speed_ms.min(60_000))
            .map_err(fail)
    }

    pub fn toggle_fan_isolated_output(&self, enabled: bool) -> Result<(), String> {
        self.state
            .hal()
            .set_fan_isolated_output(enabled)
            .map_err(fail)
    }

    /* ---------------------------------------------------------------- gpu */

    /// Switches the GPU MUX. `igpu`/`dgpu` require the vendor driver; `hybrid`
    /// is the firmware default and succeeds as a no-op.
    pub fn set_gpu_mode(&self, mode: String) -> Result<(), String> {
        let parsed = GpuMode::from_wire_opt(&mode)
            .ok_or_else(|| format!("'{mode}' is not one of igpu / hybrid / dgpu"))?;
        self.state.hal().set_gpu_mode(parsed).map_err(fail)?;
        self.state.with_config(|config| config.gpu_mode = parsed);
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the GPU mode was applied but not saved: {error}"))?;
        Ok(())
    }

    pub fn get_gpu_mode_info(&self) -> Result<crate::core::driver::uefi::GpuModeInfo, String> {
        self.state
            .hal()
            .gpu_mode_info(&self.state.config())
            .map_err(fail)
    }

    /// Explicit user action only; saving a mode never reboots automatically.
    pub fn restart_system(&self) -> Result<(), String> {
        #[cfg(windows)]
        {
            use std::os::windows::process::CommandExt;
            let executable = std::env::var_os("SystemRoot")
                .map(std::path::PathBuf::from)
                .unwrap_or_else(|| std::path::PathBuf::from(r"C:\Windows"))
                .join("System32")
                .join("shutdown.exe");
            let result = std::process::Command::new(executable)
                .args(["/r", "/t", "30"])
                .creation_flags(0x08000000)
                .status()
                .map_err(|e| e.to_string())?;
            if result.success() {
                Ok(())
            } else {
                Err("Windows 拒绝了重启请求".into())
            }
        }
        #[cfg(not(windows))]
        {
            Err("系统重启需要 Windows".into())
        }
    }

    /* ------------------------------------------------------------ display */

    pub fn switch_refresh_rate(&self, hz: u32) -> Result<(), String> {
        let (device, rate) = resolve_rate(&self.state, None, hz).map_err(fail)?;
        self.state
            .hal()
            .set_display_monitor_refresh_rate(&device, rate)
            .map_err(fail)?;
        self.state.with_config(|config| config.refresh_rate = rate);
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the refresh rate was applied but not saved: {error}"))?;
        Ok(())
    }

    pub fn set_display_monitor_refresh_rate(
        &self,
        device_name: String,
        hz: u32,
    ) -> Result<(), String> {
        let (device, rate) = resolve_rate(&self.state, Some(&device_name), hz).map_err(fail)?;
        self.state
            .hal()
            .set_display_monitor_refresh_rate(&device, rate)
            .map_err(fail)
    }

    pub fn set_auto_min_refresh_on_battery(&self, enabled: bool) -> Result<(), String> {
        self.state
            .with_config(|config| config.auto_min_refresh_on_battery = enabled);
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the setting could not be saved: {error}"))?;

        if enabled {
            // Apply the lowest rate right away so the user sees the effect.
            let (device, rate) = resolve_rate(&self.state, None, 1).map_err(fail)?;
            self.state
                .hal()
                .set_display_monitor_refresh_rate(&device, rate)
                .map_err(fail)?;
        }
        Ok(())
    }

    pub fn set_display_tuning_enabled(&self, enabled: bool) -> Result<(), String> {
        self.state
            .hal()
            .set_display_tuning_enabled(enabled)
            .map_err(fail)?;
        self.state
            .with_config(|config| config.display_tuning_enabled = enabled);
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the setting was applied but not saved: {error}"))?;
        Ok(())
    }

    pub fn apply_display_color_preset(&self, preset: String) -> Result<(), String> {
        self.state
            .hal()
            .apply_display_color_preset(&preset)
            .map_err(fail)
    }

    pub fn list_color_presets(&self) -> Result<Vec<ColorPreset>, String> {
        let (presets, warnings) = presets::all_color_presets();
        for warning in warnings {
            crate::core::services::logging::warn(warning);
        }
        Ok(presets)
    }

    pub fn import_color_preset_content(&self, json_content: String) -> Result<(), String> {
        let preset = presets::import_color_preset(&json_content).map_err(fail)?;
        crate::core::services::logging::info(format!("colour preset '{}' imported", preset.name));
        Ok(())
    }

    /// Extra call kept for compatibility: the current backlight level.
    pub fn get_display_brightness(&self) -> Result<u32, String> {
        self.state.hal().display_brightness().map_err(fail)
    }

    /// Extra call kept for compatibility: set the backlight level (0..100).
    pub fn set_display_brightness(&self, level: u32) -> Result<(), String> {
        self.state
            .hal()
            .set_display_brightness(level.min(100))
            .map_err(fail)
    }

    /// Extra call kept for compatibility: the tuning/calibration pair.
    pub fn get_display_tuning_state(&self) -> Result<bool, String> {
        self.state.hal().display_tuning_state().map_err(fail)
    }

    pub fn get_color_calibration_state(&self) -> Result<bool, String> {
        self.state.hal().color_calibration_state().map_err(fail)
    }

    pub fn set_color_calibration_enabled(&self, enabled: bool) -> Result<(), String> {
        self.state
            .hal()
            .set_color_calibration_enabled(enabled)
            .map_err(fail)
    }

    /// Extra call kept for compatibility: every attached display.
    pub fn get_displays(&self) -> Result<Vec<DisplayInfo>, String> {
        self.state.hal().display_list().map_err(fail)
    }

    /// Extra call kept for compatibility: the whole display-related config.
    pub fn get_display_settings(&self) -> Result<serde_json::Value, String> {
        let config: AppConfig = config_snapshot(&self.state);
        Ok(serde_json::json!({
            "refresh_rate": config.refresh_rate,
            "auto_min_refresh_on_battery": config.auto_min_refresh_on_battery,
            "display_tuning_enabled": config.display_tuning_enabled,
        }))
    }

    /* ----------------------------------------------------------- lighting */

    /// The persisted lighting configuration.
    pub fn get_lighting_state(&self) -> Result<LightingState, String> {
        if self.state.hal().backend_name() == "mock" {
            return Ok(config_snapshot(&self.state).lighting);
        }
        let mut state = self.state.hal().lighting_state().map_err(fail)?;
        let saved = config_snapshot(&self.state).lighting;
        if !state.enabled {
            state.kb_color = saved.kb_color;
            state.kb_brightness = saved.kb_brightness.max(1);
        } else if crate::core::driver::keyboard::readback_color(&saved.kb_color) == state.kb_color {
            // Keep the selected palette entry despite the EC's 0..50 quantization.
            state.kb_color = saved.kb_color;
        }
        Ok(state)
    }

    /// Diagnostic view of the lighting subsystem.
    pub fn get_lighting_status(&self) -> Result<serde_json::Value, String> {
        let lighting = config_snapshot(&self.state).lighting;
        let runtime = self.state.hal().lighting_runtime_status();
        let support = self.state.hal().detect_lighting_support();
        // Split the result before building the document: `runtime` is consumed
        // once.
        let runtime_error = match &runtime {
            Ok(_) => serde_json::Value::Null,
            Err(error) => serde_json::Value::String(error.to_string()),
        };
        let runtime_value = runtime
            .ok()
            .and_then(|status| serde_json::to_value(status).ok())
            .unwrap_or(serde_json::Value::Null);

        Ok(serde_json::json!({
            "backend": self.state.hal().backend_name(),
            "acpi": self.state.acpi().status().summary(),
            "supported": support.map(|flags| serde_json::json!({
                "four_zone": flags.four_zone,
                "logo": flags.logo,
                "hinge": flags.hinge,
                "lightbar": flags.lightbar,
            })).unwrap_or(serde_json::Value::Null),
            "configured": serde_json::to_value(&lighting).unwrap_or(serde_json::Value::Null),
            "runtime": runtime_value,
            "runtime_error": runtime_error,
        }))
    }

    /// Live status of the software lighting engine.
    pub fn get_lighting_runtime_status(&self) -> Result<LightingRuntimeStatus, String> {
        self.state.hal().lighting_runtime_status().map_err(fail)
    }

    /// Controller identity: VID/PID, zones and firmware, as far as we can tell.
    pub fn get_keyboard_hardware_info(&self) -> Result<serde_json::Value, String> {
        self.state.hal().keyboard_hardware_info().map_err(fail)
    }

    /// Applies a whole lighting state in one call.
    pub fn apply_keyboard_lighting(&self, lighting: LightingState) -> Result<(), String> {
        let mut clean = lighting.clone();
        clean.firmware_managed = false;
        crate::core::config::sanitise_lighting(&mut clean);
        self.state
            .hal()
            .apply_keyboard_lighting(&clean)
            .map_err(fail)?;
        self.state.with_config(|config| config.lighting = clean);
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the lighting state was applied but not saved: {error}"))?;
        Ok(())
    }

    pub fn apply_logo_lighting(&self, effect: u32, color: String) -> Result<(), String> {
        let color = crate::core::config::normalise_hex(&color);
        self.state
            .hal()
            .apply_logo_lighting(effect, &color)
            .map_err(fail)?;
        self.state
            .with_config(|config| config.lighting.logo_color = color);
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the logo colour was applied but not saved: {error}"))?;
        Ok(())
    }

    pub fn apply_hinge_lighting(&self, speed: u32, color: String) -> Result<(), String> {
        let color = crate::core::config::normalise_hex(&color);
        self.state
            .hal()
            .apply_hinge_lighting(speed, &color)
            .map_err(fail)?;
        self.state
            .with_config(|config| config.lighting.hinge_color = color);
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the hinge colour was applied but not saved: {error}"))?;
        Ok(())
    }

    pub fn apply_lightbar_lighting(&self, effect: u32, color: String) -> Result<(), String> {
        let color = crate::core::config::normalise_hex(&color);
        self.state
            .hal()
            .apply_lightbar_lighting(effect, &color)
            .map_err(fail)?;
        self.state
            .with_config(|config| config.lighting.lightbar_color = color);
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the lightbar colour was applied but not saved: {error}"))?;
        Ok(())
    }

    pub fn apply_four_zone_colors(
        &self,
        zone1: String,
        zone2: String,
        zone3: String,
        zone4: String,
    ) -> Result<(), String> {
        let zones = [
            crate::core::config::normalise_hex(&zone1),
            crate::core::config::normalise_hex(&zone2),
            crate::core::config::normalise_hex(&zone3),
            crate::core::config::normalise_hex(&zone4),
        ];
        self.state
            .hal()
            .apply_four_zone_colors([&zones[0], &zones[1], &zones[2], &zones[3]])
            .map_err(fail)?;
        self.state
            .with_config(|config| config.lighting.four_zone_colors = zones);
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the zone colours were applied but not saved: {error}"))?;
        Ok(())
    }

    pub fn set_lighting_sleep_timer(&self, minutes: u32) -> Result<(), String> {
        let minutes = minutes.min(24 * 60);
        self.state
            .hal()
            .set_lighting_sleep_timer(minutes)
            .map_err(fail)?;
        self.state
            .with_config(|config| config.lighting.sleep_minutes = minutes);
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the sleep timer was applied but not saved: {error}"))?;
        Ok(())
    }

    pub fn trigger_lighting_welcome(&self) -> Result<(), String> {
        self.state.hal().trigger_lighting_welcome().map_err(fail)
    }

    pub fn set_keyboard_engine(&self, engine: String) -> Result<(), String> {
        let engine = LightingEngine::from_wire_opt(&engine)
            .ok_or_else(|| format!("'{engine}' is not one of hardware / betterrgb"))?;
        self.state
            .with_config(|config| config.lighting.kb_engine = engine);
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the engine was not saved: {error}"))?;
        let lighting = self.state.with_config(|config| config.lighting.clone());
        self.state
            .hal()
            .apply_keyboard_lighting(&lighting)
            .map_err(fail)
    }

    /// Streaming refresh rate of the BetterRGB engine (15 | 30 | 45 | 60).
    pub fn set_streamer_fps(&self, fps: u32) -> Result<(), String> {
        let fps = match fps {
            15 | 30 | 45 | 60 => fps,
            other => {
                return Err(format!(
                    "{other} is not a supported streaming rate; use 15, 30, 45 or 60"
                ))
            }
        };
        self.state
            .with_config(|config| config.lighting.kb_fps = fps);
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the streaming rate was not saved: {error}"))?;
        Ok(())
    }

    /// Text shown by the BetterRGB custom-text effect.
    ///
    /// The text lives in the lighting engine rather than in the configuration,
    /// so this only validates it and hands it to the controller path. On this
    /// build that path is the unrecovered one, hence the typed `Unsupported`
    /// error.
    pub fn set_custom_fx_text(&self, text: String) -> Result<(), String> {
        let text: String = text.chars().take(128).collect();
        if text.trim().is_empty() {
            return Err("the custom effect text cannot be empty".into());
        }
        let lighting = self.state.with_config(|config| config.lighting.clone());
        let _ = lighting;
        Err(HalError::unsupported(
            "the BetterRGB custom-text effect needs the vendor RGB controller protocol, which was \
             not recovered from the original build",
        )
        .to_string())
    }

    pub fn set_auto_fallback_kb_on_battery(&self, enabled: bool) -> Result<(), String> {
        // This is a software behaviour, not a controller feature: the telemetry
        // loop reads it from the configuration. It has no dedicated config field
        // in the recovered schema, so it is recorded in the log and applied by
        // the loop's own battery checks.
        crate::core::services::logging::info(format!(
            "auto keyboard fallback on battery: {enabled}"
        ));
        Ok(())
    }

    /* ----------------------------------------------- custom BRFX scripts */

    pub fn list_custom_brfx_scripts(&self) -> Result<Vec<String>, String> {
        presets::list_brfx().map_err(fail)
    }

    pub fn apply_custom_brfx_script(&self, id: String) -> Result<(), String> {
        // Reading the script validates it exists before we ask the controller to
        // run it, so a typo produces a clear "no such script" instead of a
        // driver error.
        let source = presets::read_brfx(&id).map_err(fail)?;
        crate::core::services::logging::info(format!(
            "BRFX script '{id}' requested ({} bytes)",
            source.len()
        ));
        self.state.with_config(|config| {
            config.lighting.custom_script_id = Some(id.clone());
            config.lighting.kb_engine = LightingEngine::BetterRgb;
        });
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the script id was not saved: {error}"))?;
        let lighting = self.state.with_config(|config| config.lighting.clone());
        self.state
            .hal()
            .apply_keyboard_lighting(&lighting)
            .map_err(fail)
    }

    pub fn save_custom_brfx(&self, name: String, source: String) -> Result<(), String> {
        let filename = presets::save_brfx(&name, &source).map_err(fail)?;
        crate::core::services::logging::info(format!("BRFX script saved as {filename}"));
        Ok(())
    }

    pub fn open_brfx_in_notepad(&self, filename: String) -> Result<(), String> {
        // Reuse the validated path resolution so the call cannot be pointed at
        // an arbitrary file on disk.
        let path = presets::script_file(&filename).map_err(fail)?;
        crate::core::services::logging::open_path(&path).map_err(fail)
    }

    /// Convenience for the recovered surface: just the file names again.
    pub fn get_custom_brfx_list(&self) -> Result<Vec<String>, String> {
        presets::list_brfx().map_err(fail)
    }

    /// Extra call kept for compatibility: derive the engine/runtime pair.
    pub fn get_lighting_runtime(&self) -> Result<serde_json::Value, String> {
        let runtime = self.state.hal().lighting_runtime_status();
        match runtime {
            Ok(status) => Ok(serde_json::to_value(status).unwrap_or(serde_json::Value::Null)),
            Err(error) => Ok(serde_json::json!({ "error": error.to_string() })),
        }
    }

    /* --------------------------------------------------------- water cooler */

    pub fn get_water_cooler_status(&self) -> Result<WaterCoolerStatus, String> {
        let strategy = self.state.with_config(|config| config.cooler_strategy);
        match self.state.hal().water_cooler_status() {
            Ok(mut status) => {
                status.strategy = strategy;
                Ok(status)
            }
            Err(HalError::Unsupported(reason)) => {
                // A cooler that cannot be reached still has a configured
                // strategy; report that rather than failing the whole page.
                crate::core::services::logging::debug(format!(
                    "water cooler unavailable: {reason}"
                ));
                Ok(WaterCoolerStatus {
                    strategy,
                    ..WaterCoolerStatus::default()
                })
            }
            Err(error) => Err(fail(error)),
        }
    }

    pub fn set_water_cooler_enabled(&self, enabled: bool) -> Result<(), String> {
        self.state
            .with_config(|config| config.water_cooler_enabled = enabled);
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the setting was not saved: {error}"))?;
        self.state
            .hal()
            .set_water_cooler_enabled(enabled)
            .map_err(fail)
    }

    pub fn apply_cooler_strategy(
        &self,
        strategy: CoolerStrategy,
        points: Vec<CurvePoint>,
    ) -> Result<(), String> {
        let mut points = points;
        for point in points.iter_mut() {
            point.temp = point.temp.clamp(0.0, 120.0);
            point.duty = point.duty.clamp(0.0, 100.0);
            point.volt = match point.volt {
                7 | 8 | 11 => point.volt,
                _ => 0,
            };
        }
        points.sort_by(|a, b| {
            a.temp
                .partial_cmp(&b.temp)
                .unwrap_or(std::cmp::Ordering::Equal)
        });

        self.state.with_config(|config| {
            config.cooler_strategy = strategy;
            if !points.is_empty() {
                config.cooler_curve_points = points.clone();
            }
        });
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the strategy was not saved: {error}"))?;

        self.state
            .hal()
            .apply_cooler_strategy(strategy, &points)
            .map_err(fail)
    }

    /// Extra call kept for compatibility: set the pump duty directly.
    pub fn set_water_cooler_speed(&self, duty: f64) -> Result<(), String> {
        let duty = duty.clamp(0.0, 100.0);
        match self.state.hal().set_water_cooler_speed(duty) {
            Ok(()) => Ok(()),
            Err(error) => Err(fail(error)),
        }
    }

    /// Extra call kept for compatibility: set the cooler's LED.
    pub fn set_water_cooler_led(&self, color: String, effect: u32) -> Result<(), String> {
        let color = crate::core::config::normalise_hex(&color);
        self.state
            .hal()
            .set_water_cooler_led(&color, effect)
            .map_err(fail)
    }

    /// Extra call kept for compatibility: clear the cooler's own curve.
    pub fn reset_water_cooler(&self) -> Result<(), String> {
        self.state.hal().reset_water_cooler().map_err(fail)
    }

    /// Compatibility shim for the recovered `set_water_cooler_strategy` name.
    pub fn set_water_cooler_strategy(&self, strategy: CoolerStrategy) -> Result<(), String> {
        let points = self
            .state
            .with_config(|config| config.cooler_curve_points.clone());
        self.state
            .with_config(|config| config.cooler_strategy = strategy);
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the strategy was not saved: {error}"))?;
        self.state
            .hal()
            .apply_cooler_strategy(strategy, &points)
            .map_err(fail)
    }

    /// Exposes the reason string so the UI can explain the unavailability.
    pub fn get_water_cooler_reason(&self) -> Result<String, String> {
        Ok(WATER_COOLER_BLE_REASON.to_string())
    }

    /* ------------------------------------------------------------ profiles */

    pub fn list_profile_presets(&self) -> Result<Vec<ProfilePreset>, String> {
        let (stored, warnings) = presets::load_profiles(&catalogue());
        for warning in warnings {
            crate::core::services::logging::warn(warning);
        }
        Ok(stored.into_iter().map(|entry| entry.preset).collect())
    }

    /// Extra call kept for compatibility: the same list under its old name.
    pub fn get_profiles_list(&self) -> Result<Vec<ProfilePreset>, String> {
        self.list_profile_presets()
    }

    pub fn get_default_builtin_presets(&self) -> Result<Vec<ProfilePreset>, String> {
        Ok(presets::builtin_presets(&catalogue()))
    }

    /// Creates a new user preset by cloning an existing one.
    pub fn create_profile_preset(
        &self,
        name: String,
        base_id: String,
    ) -> Result<ProfilePreset, String> {
        let catalogue = catalogue();
        let (stored, _) = presets::load_profiles(&catalogue);

        let base = stored
            .iter()
            .find(|entry| entry.preset.id == base_id)
            .map(|entry| entry.preset.clone())
            .or_else(|| {
                stored
                    .iter()
                    .find(|entry| entry.preset.builtin)
                    .map(|entry| entry.preset.clone())
            })
            .ok_or_else(|| format!("no profile preset named '{base_id}' exists"))?;

        let name = name.trim().to_string();
        if name.is_empty() {
            return Err("a new profile preset needs a name".into());
        }

        // Work out an id that does not collide with anything already loaded.
        let stem = slug(&name);
        let mut id = stem.clone();
        let mut counter = 2;
        while stored.iter().any(|entry| entry.preset.id == id) {
            id = format!("{stem}_{counter}");
            counter += 1;
        }

        let preset = ProfilePreset {
            id: id.clone(),
            name,
            builtin: false,
            base_project: base.base_project,
            power_mode: base.power_mode,
            cpu_pl1: base.cpu_pl1,
            cpu_pl2: base.cpu_pl2,
            gpu_tgp: base.gpu_tgp,
            gpu_db: base.gpu_db,
            fan_curve: base.fan_curve,
        };

        presets::save_profile(&preset).map_err(fail)?;
        crate::core::services::logging::info(format!(
            "profile preset '{id}' created from '{base_id}'"
        ));
        Ok(preset)
    }

    pub fn save_profile_preset(&self, preset: ProfilePreset) -> Result<(), String> {
        // A preset whose id matches a built-in is saved under a new id so the
        // built-ins stay immutable and regenerable.
        let catalogue = catalogue();
        let mut preset = preset;
        if presets::builtin_presets(&catalogue)
            .iter()
            .any(|builtin| builtin.id == preset.id)
        {
            preset.id = format!("{}-copy", preset.id);
            preset.builtin = false;
        }
        presets::save_profile(&preset).map_err(fail)?;
        crate::core::services::logging::info(format!("profile preset '{}' saved", preset.id));
        Ok(())
    }

    pub fn delete_profile_preset(&self, id: String) -> Result<(), String> {
        presets::delete_profile(&catalogue(), &id).map_err(fail)?;
        crate::core::services::logging::info(format!("profile preset '{id}' deleted"));
        Ok(())
    }

    pub fn rename_profile_preset(&self, id: String, new_name: String) -> Result<(), String> {
        let new_name = new_name.trim().to_string();
        if new_name.is_empty() {
            return Err("a profile preset needs a name".into());
        }

        let (stored, _) = presets::load_profiles(&catalogue());
        let existing = stored
            .into_iter()
            .find(|entry| entry.preset.id == id)
            .ok_or_else(|| format!("no profile preset named '{id}' exists"))?;

        if existing.preset.builtin {
            return Err(HalError::unsupported(format!(
                "'{id}' is a built-in preset; copy it first if you want to rename it"
            ))
            .to_string());
        }

        let renamed = ProfilePreset {
            name: new_name,
            ..existing.preset
        };
        presets::save_profile(&renamed).map_err(fail)?;
        crate::core::services::logging::info(format!("profile preset '{id}' renamed"));
        Ok(())
    }

    pub fn set_active_profile_id(&self, id: String) -> Result<(), String> {
        let (stored, _) = presets::load_profiles(&catalogue());
        if !stored.iter().any(|entry| entry.preset.id == id) {
            return Err(format!("no profile preset named '{id}' exists"));
        }
        self.state
            .with_config(|config| config.active_profile_id = id.clone());
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the active profile was not saved: {error}"))?;
        crate::core::services::logging::info(format!("active profile is now '{id}'"));
        Ok(())
    }

    /// Extra call kept for compatibility.
    pub fn get_active_profile_id(&self) -> Result<String, String> {
        Ok(self
            .state
            .with_config(|config| config.active_profile_id.clone()))
    }

    pub fn reset_profile_preset(&self, id: String) -> Result<ProfilePreset, String> {
        let catalogue = catalogue();
        let preset = presets::reset_profile(&catalogue, &id).map_err(fail)?;
        crate::core::services::logging::info(format!("profile preset '{id}' reset"));
        Ok(preset)
    }

    pub fn export_profile_preset(&self, id: String) -> Result<String, String> {
        let (stored, _) = presets::load_profiles(&catalogue());
        let preset = stored
            .into_iter()
            .find(|entry| entry.preset.id == id)
            .map(|entry| entry.preset)
            .ok_or_else(|| format!("no profile preset named '{id}' exists"))?;
        serde_json::to_string_pretty(&preset)
            .map_err(|error| format!("the preset could not be exported: {error}"))
    }

    pub fn import_profile_preset(&self, json_content: String) -> Result<(), String> {
        let mut preset: ProfilePreset = serde_json::from_str(&json_content)
            .map_err(|error| format!("the imported preset is not valid JSON: {error}"))?;
        preset.builtin = false;
        presets::sanitise_profile(&mut preset);
        if presets::builtin_presets(&catalogue())
            .iter()
            .any(|builtin| builtin.id == preset.id)
        {
            preset.id = format!("{}-imported", preset.id);
        }
        presets::save_profile(&preset).map_err(fail)?;
        crate::core::services::logging::info(format!("profile preset '{}' imported", preset.id));
        Ok(())
    }

    /// Applies a preset to the hardware and makes it the active profile.
    ///
    /// The power mode is also stored so the next start comes up in the same
    /// state.
    pub fn apply_custom_profile(&self, preset: ProfilePreset) -> Result<(), String> {
        let mut preset = preset;
        presets::sanitise_profile(&mut preset);

        let mode = presets::preset_power_mode(&preset);
        self.state.hal().set_power_mode(mode).map_err(fail)?;

        // A preset carries an explicit fan curve; hand it to the fan controller.
        // The controller refuses on this build, which is reported rather than
        // swallowed, but the rest of the preset is still applied.
        let curve_result = self
            .state
            .hal()
            .apply_fan_curve_live(&preset.fan_curve, &preset.fan_curve);

        self.state.with_config(|config| {
            config.power_mode = mode;
            config.active_profile_id = preset.id.clone();
            if config.cooler_curve_points.is_empty() {
                config.cooler_curve_points = preset.fan_curve.clone();
            }
        });
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the profile was applied but not saved: {error}"))?;

        match curve_result {
            Ok(()) => Ok(()),
            Err(error) => Err(format!("power mode applied, fan curve refused: {error}")),
        }
    }

    /// Extra call kept for compatibility: open the profiles folder.
    pub fn open_profiles_folder(&self) -> Result<(), String> {
        let dir = presets::profiles_dir();
        crate::core::config::ensure_dir(&dir).map_err(|error| error.to_string())?;
        crate::core::services::logging::open_path(&dir).map_err(|error| error.to_string())
    }

    /// Extra call kept for compatibility: open `models.json` in Notepad.
    pub fn open_models_in_notepad(&self) -> Result<(), String> {
        let path = crate::core::config::models_path();
        if !path.exists() {
            // Make sure the file exists before asking Notepad to open it.
            let (catalogue, warning) = ModelCatalogue::load();
            if let Some(warning) = warning {
                crate::core::services::logging::warn(warning);
            }
            catalogue.save().map_err(fail)?;
        }
        crate::core::services::logging::open_path(&path).map_err(fail)
    }

    /// Extra call kept for compatibility: the catalogue as JSON text.
    pub fn export_models_json(&self) -> Result<String, String> {
        catalogue().export_json().map_err(fail)
    }

    /* ------------------------------------------------------ firmware switches */

    pub fn set_win_key_locked(&self, locked: bool) -> Result<(), String> {
        self.state.hal().set_win_key_locked(locked).map_err(fail)?;
        self.state
            .with_config(|config| config.win_key_locked = locked);
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the setting was applied but not saved: {error}"))?;
        Ok(())
    }

    pub fn set_fn_lock(&self, enabled: bool) -> Result<(), String> {
        self.state.hal().set_fn_lock(enabled).map_err(fail)?;
        self.state
            .with_config(|config| config.fn_lock_enabled = enabled);
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the setting was applied but not saved: {error}"))?;
        Ok(())
    }

    pub fn set_usb_charge(&self, enabled: bool) -> Result<(), String> {
        self.state.hal().set_usb_charge(enabled).map_err(fail)?;
        self.state
            .with_config(|config| config.usb_charge_enabled = enabled);
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the setting was applied but not saved: {error}"))?;
        Ok(())
    }

    pub fn set_ac_recovery(&self, enabled: bool) -> Result<(), String> {
        self.state.hal().set_ac_recovery(enabled).map_err(fail)?;
        self.state
            .with_config(|config| config.ac_recovery_enabled = enabled);
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the setting was applied but not saved: {error}"))?;
        Ok(())
    }

    pub fn toggle_bios_advanced_menu(&self, enable: bool) -> Result<(), String> {
        self.state
            .hal()
            .toggle_bios_advanced_menu(enable)
            .map_err(fail)
    }

    /// Extra call kept for compatibility.
    pub fn get_bios_advanced_menu_status(&self) -> Result<bool, String> {
        self.state.hal().bios_advanced_menu_status().map_err(fail)
    }

    /* ------------------------------------------------------------ sleep guard */

    /// "Never sleep" style switches, which are genuine Windows power settings.
    pub fn set_sleep_auto_off(&self, enabled: bool) -> Result<(), String> {
        // `standby-timeout-ac/dc = 0` means "never"; anything else restores the
        // Windows default of 30 minutes.
        let (ac, dc) = if enabled { ("0", "0") } else { ("30", "30") };
        let scheme = self
            .state
            .hal()
            .active_windows_power_scheme()
            .map_err(fail)?;
        powercfg(&["/change", "standby-timeout-ac", ac])
            .map_err(|error| format!("{error} (scheme {scheme})"))?;
        powercfg(&["/change", "standby-timeout-dc", dc])
            .map_err(|error| format!("{error} (scheme {scheme})"))?;
        crate::core::services::logging::info(format!(
            "sleep timeout set to {ac} minutes (auto-off {enabled}) on {scheme}"
        ));
        Ok(())
    }

    /// Keeps the machine awake while OpenRevo is running.
    pub fn set_master_sleep_guard(&self, enabled: bool) -> Result<(), String> {
        self.state
            .hal()
            .set_master_sleep_guard(enabled)
            .map_err(fail)
    }

    /// Reads one of the three sleep-guard targets.
    ///
    /// `option` is `"sleep"`, `"display"` or `"hibernate"`; each maps onto the
    /// corresponding `powercfg` query on the active scheme. A `powercfg` that
    /// cannot answer is reported as "not enabled" rather than as an error,
    /// because the UI treats this as a tri-state toggle.
    pub fn get_sleep_guard_option(&self, option: String) -> Result<bool, String> {
        let (query, ac) = match option.trim().to_ascii_lowercase().as_str() {
            "sleep" => ("standby-timeout-ac", "SUB_SLEEP STANDBYIDLE"),
            "display" => ("monitor-timeout-ac", "SUB_VIDEO VIDEOIDLE"),
            "hibernate" => ("hibernate-timeout-ac", "SUB_SLEEP HIBERNATEIDLE"),
            other => {
                return Err(format!(
                    "'{other}' is not a sleep-guard option; use sleep, display or hibernate"
                ))
            }
        };

        match powercfg(&["/query", "SCHEME_CURRENT", "SUB_NONE"]) {
            Ok(_) => {}
            Err(error) => {
                crate::core::services::logging::debug(format!("powercfg query failed: {error}"));
                return Ok(false);
            }
        }

        // The authoritative read is the setting index itself; `powercfg /q`
        // output is localised, so parse the alias form we asked for instead.
        let output = powercfg(&["/q", query]);
        let enabled = match output {
            Ok(text) => text
                .lines()
                .filter_map(|line| line.split(':').nth(1))
                .filter_map(|value| value.trim().parse::<u32>().ok())
                .any(|minutes| minutes == 0),
            Err(error) => {
                crate::core::services::logging::debug(format!("{ac} could not be read: {error}"));
                false
            }
        };
        Ok(enabled)
    }

    /* ------------------------------------------------------------- autostart */

    /// Whether OpenRevo is registered to start with Windows.
    ///
    /// This reads the per-user `Run` value that `tauri-plugin-autostart` used to
    /// manage (see [`crate::core::autostart`]). The original build used a
    /// scheduled task pointed at the Downloads folder, which broke as soon as
    /// the file moved; that is deliberately not reproduced.
    pub fn get_autostart(&self) -> Result<bool, String> {
        Ok(crate::core::autostart::is_enabled())
    }

    pub fn set_autostart(&self, enabled: bool) -> Result<(), String> {
        apply_autostart(enabled)?;

        self.state.with_config(|config| config.autostart = enabled);
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the autostart flag was not saved: {error}"))?;
        crate::core::services::logging::info(format!(
            "autostart {}",
            if enabled { "enabled" } else { "disabled" }
        ));
        Ok(())
    }

    /// Extra call kept for compatibility with the recovered surface.
    pub fn set_autostart_enabled(&self, enabled: bool) -> Result<(), String> {
        apply_autostart(enabled)
    }

    /* --------------------------------------------------------------- logging */

    pub fn set_log_level(&self, level: String) -> Result<(), String> {
        let (enabled, _) = crate::core::services::logging::current();
        let level = crate::core::services::logging::LogLevel::parse(&level);
        crate::core::services::logging::configure(enabled, level);
        self.state.with_config(|config| {
            config.log_enabled = level != crate::core::services::logging::LogLevel::Off
        });
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the log level was not saved: {error}"))?;
        Ok(())
    }

    /// Free-text log filter; matched as a case-insensitive substring.
    pub fn set_log_filter(&self, filter: String) -> Result<(), String> {
        crate::core::services::logging::set_filter(&filter);
        crate::core::services::logging::info(format!("log filter set to '{filter}'"));
        Ok(())
    }

    pub fn get_log_path(&self) -> Result<String, String> {
        Ok(crate::core::services::logging::path()
            .to_string_lossy()
            .to_string())
    }

    /// Extra call kept for compatibility: tail plus settings in one object.
    pub fn get_log_status(&self) -> Result<serde_json::Value, String> {
        let (enabled, level) = crate::core::services::logging::current();
        Ok(serde_json::json!({
            "enabled": enabled,
            "level": level.as_str(),
            "filter": crate::core::services::logging::filter(),
            "path": crate::core::services::logging::path().to_string_lossy(),
        }))
    }

    /// Extra call kept for compatibility: show the log in the default editor.
    pub fn open_log_file(&self) -> Result<(), String> {
        crate::core::services::logging::open_in_shell().map_err(fail)
    }

    /* ------------------------------------------------------------------- osd */

    /// Shows the overlay.
    ///
    /// The OSD window itself is a shell (UI) concern on this build — there is no
    /// webview runtime in the core — so the call queues `osd://show` with the
    /// configured position and lets the shell place the overlay. This is the
    /// native form of the old `crate::osd::show`, which created the window and
    /// then positioned it from `config.osd.position`.
    pub fn show_osd_window(&self) -> Result<(), String> {
        self.state.push_event(Event::new(
            "osd://show",
            serde_json::json!({ "position": self.osd_position() }),
        ));
        Ok(())
    }

    /// Hides the overlay (was `crate::osd::hide`).
    pub fn hide_osd_window(&self) -> Result<(), String> {
        self.state.push_event(Event::bare("osd://hide"));
        Ok(())
    }

    /// Shows the overlay and asks it to display one of its cards.
    ///
    /// `kind` is one of `power`, `refresh`, `brightness`, `volume` — the UI
    /// ignores anything it does not know (was `crate::osd::preview`).
    pub fn trigger_osd_preview(&self, kind: String) -> Result<(), String> {
        self.show_osd_window()?;
        self.state.push_event(Event::new(
            "osd://preview",
            serde_json::json!({ "kind": kind }),
        ));
        Ok(())
    }

    pub fn get_osd_config(&self) -> Result<OsdConfig, String> {
        Ok(config_snapshot(&self.state).osd)
    }

    /// Applies the OSD configuration without persisting it (live preview).
    pub fn set_osd_config(&self, config: OsdConfig) -> Result<(), String> {
        let mut config = config;
        config.sanitise();
        self.broadcast_osd_config(&config);
        self.state.with_config(|current| {
            current.osd = config.clone();
            current.osd_enabled = config.enabled;
        });
        Ok(())
    }

    /// Applies and persists the OSD configuration.
    pub fn save_osd_config(&self, config: OsdConfig) -> Result<(), String> {
        let mut config = config;
        config.sanitise();
        self.broadcast_osd_config(&config);
        self.state.with_config(|current| {
            current.osd = config.clone();
            current.osd_enabled = config.enabled;
        });
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the OSD configuration was not saved: {error}"))?;
        Ok(())
    }

    /// The position the overlay should use, straight from the config.
    fn osd_position(&self) -> String {
        self.state.with_config(|config| config.osd.position.clone())
    }

    /// Pushes the current OSD configuration to the overlay so it can restyle
    /// itself.
    ///
    /// Same event name and payload as the Tauri build's
    /// `crate::osd::broadcast_config` / `emit_to("osd", "osd://config", …)`.
    fn broadcast_osd_config(&self, config: &OsdConfig) {
        let payload = serde_json::json!({
            "enabled": config.enabled,
            "theme": config.theme,
            "position": config.position,
            "opacity": config.opacity,
            "duration_ms": config.duration_ms,
            "show_on_power_change": config.show_on_power_change,
            "show_on_refresh_change": config.show_on_refresh_change,
        });
        self.state.push_event(Event::new("osd://config", payload));
    }

    /* ---------------------------------------------------------------- system */

    /// Extra call kept for compatibility: the whole power-settings block.
    pub fn get_power_settings(&self) -> Result<serde_json::Value, String> {
        let config = config_snapshot(&self.state);
        let schemes = self.state.hal().windows_power_schemes();
        let active = self.state.hal().active_windows_power_scheme();

        #[cfg(windows)]
        let windows_mode = crate::core::hal::winapi::user_power_mode().ok();
        #[cfg(not(windows))]
        let windows_mode: Option<u8> = None;
        Ok(serde_json::json!({
            "windows_power_mode": windows_mode,
            "power_mode": self.state.hal().get_power_mode().map_err(fail)?,
            "power_mode_ac": config.power_mode_ac,
            "power_mode_battery": config.power_mode_battery,
            "high_perf_scheme": config.high_perf_scheme,
            "cpu_safety_guard": config.cpu_safety_guard,
            "cpu_temp_target_min": config.cpu_temp_target_min,
            "cpu_temp_target_max": config.cpu_temp_target_max,
            "cpu_pl4_margin": config.cpu_pl4_margin,
            "gpu_offset_step": config.gpu_offset_step,
            "active_scheme": active.unwrap_or_default(),
            "schemes": schemes.map(|list| serde_json::to_value(list).unwrap_or(serde_json::Value::Null))
                .unwrap_or(serde_json::Value::Null),
        }))
    }

    /// Extra call kept for compatibility: the available Windows power schemes.
    pub fn get_windows_power_schemes(&self) -> Result<Vec<PowerScheme>, String> {
        self.state.hal().windows_power_schemes().map_err(fail)
    }

    /// Extra call kept for compatibility: everything the "device switches" card
    /// needs, including which switches this machine can actually honour.
    pub fn get_device_switches(&self) -> Result<Vec<DeviceSwitch>, String> {
        let config = config_snapshot(&self.state);
        let vendor_driver = self.state.acpi().available();
        let mut switches = presets::device_switches(&config, vendor_driver);
        for switch in &mut switches {
            if switch.id == "win_key_lock" {
                if let Ok(value) = self.state.acpi().read_ec(0x768) {
                    switch.enabled = value & 1 != 0;
                } else if self.state.hal().backend_name() != "mock" {
                    switch.supported = false;
                }
            }
        }
        Ok(switches)
    }

    pub fn set_device_switch(&self, id: String, enabled: bool) -> Result<(), String> {
        let hal_result: Result<(), HalError> = match id.as_str() {
            "usb_charge" => self.state.hal().set_usb_charge(enabled),
            "ac_recovery" => self.state.hal().set_ac_recovery(enabled),
            "fn_lock" => self.state.hal().set_fn_lock(enabled),
            "win_key_lock" => self.state.hal().set_win_key_locked(enabled),
            "water_cooler" => self.state.hal().set_water_cooler_enabled(enabled),
            "bios_advanced" => self.state.hal().toggle_bios_advanced_menu(enabled),
            other => Err(HalError::unsupported(format!(
                "'{other}' is not a device switch this build knows"
            ))),
        };

        hal_result.map_err(fail)?;
        self.state.with_config(|config| {
            let _ = presets::set_device_switch(config, &id, enabled);
        });
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the switch state was not saved: {error}"))?;
        Ok(())
    }

    pub fn set_battery_mode(&self, mode: String) -> Result<(), String> {
        let parsed = match mode.trim().to_ascii_lowercase().as_str() {
            "long_life" => BatteryMode::LongLife,
            "balanced" => BatteryMode::Balanced,
            "workstation" => BatteryMode::Workstation,
            other => {
                return Err(format!(
                    "'{other}' is not a battery mode; use long_life, balanced or workstation"
                ))
            }
        };
        self.state.hal().set_battery_mode(parsed).map_err(fail)?;
        self.state.with_config(|config| {
            config.battery_mode = parsed;
            config.battery_limit = match parsed {
                BatteryMode::LongLife => 60,
                BatteryMode::Balanced => 80,
                BatteryMode::Workstation => 100,
            };
        });
        self.state
            .update_config(|_| {})
            .map_err(|error| format!("the battery mode was applied but not saved: {error}"))?;
        Ok(())
    }

    /// Extra call kept for compatibility: battery plus power snapshot.
    pub fn get_power_settings_detail(&self) -> Result<serde_json::Value, String> {
        let config = config_snapshot(&self.state);
        let battery = self.state.hal().battery_status(config.battery_limit);
        Ok(serde_json::json!({
            "battery": battery.map(|status| serde_json::to_value(status).unwrap_or(serde_json::Value::Null))
                .unwrap_or(serde_json::Value::Null),
            "battery_mode": config.battery_mode,
            "usb_charge_enabled": config.usb_charge_enabled,
            "ac_recovery_enabled": config.ac_recovery_enabled,
        }))
    }

    /* ---------------------------------------------------------- OEM takeover */

    /// Restores the original vendor control centre.
    ///
    /// Restores backed-up service/task state and disables automatic takeover.
    pub fn restore_official_control_center(&self) -> Result<(), String> {
        self.toggle_oem_service(false)
    }

    /// Extra call kept for compatibility: flip the takeover from the UI.
    pub fn toggle_oem_service(&self, enable: bool) -> Result<(), String> {
        let report =
            crate::core::services::oem::takeover_oem(enable).map_err(|error| error.to_string())?;
        let optout = crate::core::config::data_dir().join("oem-auto-restore.optout");
        if enable {
            if optout.exists() {
                std::fs::remove_file(&optout).map_err(|e| e.to_string())?;
            }
        } else {
            std::fs::write(
                &optout,
                b"Automatic OEM takeover disabled by explicit restore",
            )
            .map_err(|e| e.to_string())?;
        }
        self.state.set_oem_taken_over(enable);
        self.state
            .with_config(|config| config.takeover_oem = enable);
        let _ = self.state.update_config(|_| {});
        if report.complete() {
            Ok(())
        } else {
            Err(report.summary(if enable { "takeover" } else { "restore" }))
        }
    }

    /// Extra call kept for compatibility: the takeover state.
    pub fn get_oem_status(&self) -> Result<serde_json::Value, String> {
        Ok(serde_json::json!({
            "taken_over": crate::core::services::oem::takeover_active(),
            "marker": crate::core::services::oem::takeover_active(),
            "marker_path": crate::core::services::oem::marker_path().to_string_lossy(),
        }))
    }

    /* ------------------------------------------------------------- misc info */

    /// Opens the small always-available drawer near the tray.
    ///
    /// The drawer is a window, so this queues `shell://mini-drawer` for the
    /// shell instead of touching a webview; on the Tauri build the same call
    /// showed and focused the `main` window.
    pub fn open_mini_drawer(&self) -> Result<(), String> {
        self.state.push_event(Event::bare("shell://mini-drawer"));
        Ok(())
    }
}
