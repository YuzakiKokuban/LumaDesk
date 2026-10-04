//! Windows backend for 机耀处.
//! Windows APIs provide system controls, NVML provides NVIDIA telemetry, and
//! recovered EC/UEFI protocols provide the implemented chassis controls.
//! Feature-specific protocols without sufficient evidence return Unsupported.

#![allow(clippy::too_many_arguments)]

use super::cache::ReadCache;
use crate::core::config::{
    AppConfig, BatteryMode, BatteryStatus, CoolerStrategy, CpuStatus, CurvePoint, DeviceStatus,
    FanStatus, GpuMode, GpuStatus, HardwareStatus, LightingState, PowerModeId, SupportFlags,
};
use crate::core::driver::AcpiDriver;
use crate::core::error::{HalError, HalResult};
use crate::core::hal::{
    DisplayInfo, HardwareHal, LightingRuntimeStatus, PowerScheme, WaterCoolerStatus,
};
use std::sync::atomic::{AtomicBool, Ordering};
use std::time::Duration;

#[cfg(windows)]
use crate::core::hal::winapi;
/// The Windows backend.
pub struct WindowsHal {
    /// The vendor driver probe. Every unrecoverable feature asks it first.
    acpi: AcpiDriver,
    display_tuning: AtomicBool,
    device_cache: ReadCache<DeviceStatus>,
    battery_health_cache: ReadCache<Option<f64>>,
    #[cfg(windows)]
    clock_cache: ReadCache<f64>,
}

impl Default for WindowsHal {
    fn default() -> Self {
        Self::new()
    }
}

impl WindowsHal {
    /// Creates the backend with an unprobed ACPI driver.
    pub fn new() -> Self {
        WindowsHal {
            acpi: AcpiDriver::new(),
            display_tuning: AtomicBool::new(false),
            device_cache: ReadCache::default(),
            battery_health_cache: ReadCache::default(),
            #[cfg(windows)]
            clock_cache: ReadCache::default(),
        }
    }

    /// Creates the backend with an ACPI driver that mirrors findings to a log.
    pub fn with_acpi(acpi: AcpiDriver) -> Self {
        WindowsHal {
            acpi,
            display_tuning: AtomicBool::new(false),
            device_cache: ReadCache::default(),
            battery_health_cache: ReadCache::default(),
            #[cfg(windows)]
            clock_cache: ReadCache::default(),
        }
    }

    /// The vendor driver probe, for callers that need to explain themselves.
    pub fn acpi(&self) -> &AcpiDriver {
        &self.acpi
    }

    /// Refuse feature-specific protocols that are not implemented yet.
    fn vendor_feature(&self, capability: &str) -> HalResult<()> {
        Err(self.acpi.protocol_unsupported(capability))
    }

    /// Telemetry helper: CPU load, from `GetSystemTimes` deltas.
    #[cfg(windows)]
    fn cpu_load_percent(&self) -> f64 {
        cpu_load::sample()
    }

    #[cfg(not(windows))]
    fn cpu_load_percent(&self) -> f64 {
        0.0
    }
}

// ---------------------------------------------------------------------------
// Elevation helper
// ---------------------------------------------------------------------------

/// True when the current process token is elevated.
///
/// This is the one function in this module that is compiled on every platform,
/// because the simulator and the UI's elevation banner both call it.
pub fn is_process_elevated_if_windows() -> bool {
    #[cfg(windows)]
    {
        winapi::is_process_elevated()
    }
    #[cfg(not(windows))]
    {
        // Elevation is a Windows concept; other targets have no equivalent and
        // claiming otherwise would be a lie.
        false
    }
}

// ---------------------------------------------------------------------------
// Trait implementation
// ---------------------------------------------------------------------------

impl HardwareHal for WindowsHal {
    fn backend_name(&self) -> &'static str {
        "windows"
    }

    // ------------------------------------------------------------- telemetry

    fn hardware_status(&self, cfg: &AppConfig) -> HalResult<HardwareStatus> {
        let power_mode = self.get_power_mode();
        let power_mode_error = power_mode.as_ref().err().map(ToString::to_string);
        Ok(HardwareStatus {
            cpu: self.cpu_status()?,
            gpu: self.gpu_status()?,
            fans: self.fan_status()?,
            battery: self.battery_status(cfg.battery_limit)?,
            device: self.device_status()?,
            support_flags: self.support_flags()?,
            power_mode: power_mode.ok(),
            power_mode_error,
            gpu_mode: self
                .gpu_mode_info(cfg)
                .ok()
                .and_then(|info| info.configured_mode),
            fan_boost: self.acpi.read_ec(0x751).ok().map(|v| v & 0x40 != 0),
            windows_power_scheme: self.active_windows_power_scheme().unwrap_or_default(),
            elevated: self.is_elevated(),
        })
    }

    fn cpu_status(&self) -> HalResult<CpuStatus> {
        let load = self.cpu_load_percent();

        #[cfg(windows)]
        {
            // `Win32_Processor` gives us the rated and current clocks; the
            // temperature comes from the ACPI thermal zone, which the platform
            // exposes on most laptops and on almost no desktops.
            let freq_mhz = self.clock_cache.read(Duration::from_secs(5), || {
                Ok(winapi::Wmi::first_row(
                    "ROOT\\CIMV2",
                    "SELECT CurrentClockSpeed FROM Win32_Processor",
                )
                .ok()
                .flatten()
                .and_then(|row| row.f64_of("CurrentClockSpeed"))
                .unwrap_or(0.0))
            })?;

            let temp = acpi_thermal_zone_celsius();

            Ok(CpuStatus {
                temp,
                freq_mhz,
                load,
                // Package power needs an MSR or the vendor driver; neither is
                // available, so we say so instead of estimating.
                power_w: None,
            })
        }

        #[cfg(not(windows))]
        {
            Ok(CpuStatus {
                temp: None,
                freq_mhz: 0.0,
                load,
                power_w: None,
            })
        }
    }

    fn gpu_status(&self) -> HalResult<GpuStatus> {
        if let Some(status) = crate::core::driver::nvml::snapshot() {
            return Ok(status);
        }
        #[cfg(windows)]
        {
            // A machine with remote-desktop, capture or streaming software
            // installed exposes several `Win32_VideoController` rows, and the
            // first one is frequently a *virtual* adapter — this exact laptop
            // ordered "GameViewer Virtual Display Adapter" ahead of the real
            // GPU. Ranking the rows keeps the card honest.
            let rows = winapi::Wmi::query_rows(
                "ROOT\\CIMV2",
                "SELECT Name, AdapterRAM, CurrentRefreshRate, PNPDeviceID FROM Win32_VideoController",
            )
            .unwrap_or_default();

            let Some(row) = best_video_adapter(&rows) else {
                return Ok(GpuStatus::default());
            };

            let name = row
                .str_of("Name")
                .map(|value| tidy_vendor_name(&value))
                .unwrap_or_default();
            // `AdapterRAM` is a `uint32`, so drivers clamp it at 4 GiB — but a
            // clamped truth beats an invented number, and the UI shows n/a when
            // it is absent. The display class key knows the real figure, so try
            // that first and only fall back to WMI.
            let vram_total_mb = winapi::video_memory_bytes(&name)
                .or_else(|| row.u64_of("AdapterRAM").filter(|bytes| *bytes > 0))
                .map(|bytes| bytes.div_ceil(1024 * 1024));

            Ok(GpuStatus {
                present: !name.is_empty(),
                name,
                // GPU core temperature, clocks, utilisation and dedicated
                // VRAM-in-use are not exposed through WMI on any consumer
                // driver. Reporting a guess here would be the exact dishonesty
                // this backend is written to avoid.
                temp: None,
                freq_mhz: None,
                load: None,
                vram_used_mb: None,
                vram_total_mb,
            })
        }

        #[cfg(not(windows))]
        {
            Ok(GpuStatus::default())
        }
    }

    fn fan_status(&self) -> HalResult<FanStatus> {
        let result = self.acpi.transaction(|ec| {
            let cpu_rpm = u32::from(u16::from_be_bytes([ec.read(0x464)?, ec.read(0x465)?]));
            let gpu_rpm = u32::from(u16::from_be_bytes([ec.read(0x46C)?, ec.read(0x46B)?]));
            if cpu_rpm > 8000 || gpu_rpm > 8000 {
                return Err(HalError::io("风扇转速读数超出有效范围"));
            }
            Ok(FanStatus {
                available: true,
                cpu_rpm,
                gpu_rpm,
            })
        });
        Ok(result.unwrap_or_default())
    }

    fn battery_status(&self, limit: u32) -> HalResult<BatteryStatus> {
        let _ = limit;
        #[cfg(windows)]
        {
            let snapshot = winapi::power_snapshot()?;
            let health = self.battery_health_percent()?;
            let applied_limit = self.acpi.transaction(|ec| {
                crate::core::driver::performance::require_project(ec.read(0x740)?)?;
                let value = ec.read(0x7b9)?;
                if !(50..=100).contains(&value) {
                    return Err(HalError::io(format!(
                        "EC battery limit {value}% is outside 50-100%"
                    )));
                }
                Ok(u32::from(value))
            });
            let limit_error = applied_limit.as_ref().err().map(ToString::to_string);
            Ok(BatteryStatus {
                percent: snapshot.percent,
                charging: snapshot.charging,
                on_ac: snapshot.on_ac,
                limit: applied_limit.ok(),
                limit_error,
                health_percent: health,
            })
        }

        #[cfg(not(windows))]
        {
            Err(HalError::unavailable("battery state requires Windows"))
        }
    }

    fn device_status(&self) -> HalResult<DeviceStatus> {
        self.device_cache.read(Duration::from_secs(300), || {
            #[cfg(windows)]
            {
                let product = winapi::Wmi::first_row(
                    "ROOT\\CIMV2",
                    "SELECT Vendor, Name, IdentifyingNumber FROM Win32_ComputerSystemProduct",
                )
                .ok()
                .flatten();
                let bios = winapi::Wmi::first_row(
                    "ROOT\\CIMV2",
                    "SELECT SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS",
                )
                .ok()
                .flatten();

                let vendor = product
                    .as_ref()
                    .and_then(|row| row.str_of("Vendor"))
                    .unwrap_or_default();
                let product_name = product
                    .as_ref()
                    .and_then(|row| row.str_of("Name"))
                    .unwrap_or_default();
                let serial = product
                    .as_ref()
                    .and_then(|row| row.str_of("IdentifyingNumber"))
                    .unwrap_or_default();
                let bios_version = bios
                    .as_ref()
                    .and_then(|row| row.str_of("SMBIOSBIOSVersion"))
                    .unwrap_or_default();

                // The "model" line in the UI is the physical machine name that the
                // model catalogue keys off, so a missing vendor would be worse than
                // a plain fallback.
                let model = match (vendor.is_empty(), product_name.is_empty()) {
                    (false, false) => format!("{vendor} {product_name}"),
                    (true, false) => product_name.clone(),
                    (false, true) => vendor.clone(),
                    (true, true) => "Unknown".to_string(),
                };

                Ok(DeviceStatus {
                    model,
                    // "Project" and "EC version" are UniWill-specific fields that
                    // only the driver knows. Left empty on purpose.
                    project: self
                        .acpi
                        .read_ec(0x740)
                        .map(|v| format!("0x{v:02X}"))
                        .unwrap_or_default(),
                    bios: bios_version,
                    ec: String::new(),
                    serial,
                })
            }

            #[cfg(not(windows))]
            {
                Ok(DeviceStatus::default())
            }
        })
    }

    fn support_flags(&self) -> HalResult<SupportFlags> {
        let project = self.acpi.read_ec(0x740).ok();
        let validated =
            project.is_some_and(|p| crate::core::driver::performance::require_project(p).is_ok());

        #[cfg(windows)]
        let has_displays = winapi::displays()
            .map(|list| !list.is_empty())
            .unwrap_or(false);
        #[cfg(not(windows))]
        let has_displays = false;

        // Advertise only implemented paths; a reachable driver alone is insufficient.
        Ok(SupportFlags {
            fan_boost: validated,
            battery_limit: validated,
            water_cooler: false,
            four_zone: false,
            logo: false,
            hinge: false,
            lightbar: false,
            bios_advanced: false,
            gpu_switching: crate::core::driver::uefi::info(project)
                .map(|v| v.supported)
                .unwrap_or(false),
            // Display tuning is the gamma ramp: real, no driver needed.
            display_tuning: has_displays,
            // Custom fan curves are EC tables.
            fan_curve: false,
        })
    }

    fn is_elevated(&self) -> bool {
        is_process_elevated_if_windows()
    }

    // ----------------------------------------------------------------- power

    fn set_power_mode(&self, mode: PowerModeId) -> HalResult<()> {
        crate::core::driver::performance::apply(&self.acpi, mode)
    }

    fn get_power_mode(&self) -> HalResult<PowerModeId> {
        crate::core::driver::performance::read(&self.acpi)
    }

    fn set_fan_boost(&self, enabled: bool) -> HalResult<()> {
        self.acpi.transaction(|ec| {
            crate::core::driver::performance::require_project(ec.read(0x740)?)?;
            ec.set_bit(0x751, 0x40, enabled)
        })
    }

    fn toggle_fan_curve_control(
        &self,
        cpu_curve: &[CurvePoint],
        gpu_curve: &[CurvePoint],
    ) -> HalResult<()> {
        let _ = (cpu_curve, gpu_curve);
        self.vendor_feature("custom fan curve control")
    }

    fn apply_fan_curve_live(
        &self,
        cpu_curve: &[CurvePoint],
        gpu_curve: &[CurvePoint],
    ) -> HalResult<()> {
        let _ = (cpu_curve, gpu_curve);
        self.vendor_feature("live fan curve updates")
    }

    fn set_fan_ramp_rate(&self, speed_ms: u32) -> HalResult<()> {
        let _ = speed_ms;
        self.vendor_feature("fan ramp rate (protocol has not been validated)")
    }

    fn set_fan_isolated_output(&self, enabled: bool) -> HalResult<()> {
        let _ = enabled;
        self.vendor_feature("isolated fan output (protocol has not been validated)")
    }

    fn set_sleep_auto_off(&self, enabled: bool) -> HalResult<()> {
        let _ = enabled;
        self.vendor_feature("sleep-time device auto-off")
    }

    fn set_master_sleep_guard(&self, enabled: bool) -> HalResult<()> {
        let _ = enabled;
        self.vendor_feature("the sleep guard")
    }

    // --------------------------------------------------------------- battery

    fn set_battery_limit(&self, limit: u32) -> HalResult<()> {
        if !(50..=100).contains(&limit) {
            return Err(HalError::unsupported(format!(
                "a battery charge limit of {limit}% is out of the supported 50-100% range"
            )));
        }
        // Charge thresholds are stored in the recovered UniWill EC registers.
        self.acpi.transaction(|ec| {
            crate::core::driver::performance::require_project(ec.read(0x740)?)?;
            let limit = limit as u8;
            let mode = if limit >= 95 {
                1
            } else if limit >= 66 {
                0x11
            } else {
                0x21
            };
            let addresses = [0x770, 0x7B9, 0x7D0, 0x7A6];
            let mut before = [0; 4];
            for (i, address) in addresses.iter().enumerate() {
                before[i] = ec.read(*address)?;
            }
            let values = [
                if limit < 95 { 4 } else { 0xFF },
                limit,
                limit.saturating_sub(5),
                (before[3] & 8) | mode,
            ];
            let result = (|| {
                for (i, address) in addresses.iter().enumerate() {
                    ec.write_verified(*address, values[i])?;
                }
                Ok(())
            })();
            let snapshot: Vec<_> = addresses.into_iter().zip(before).collect();
            crate::core::driver::rollback::recover(result, &snapshot, |address, value| {
                ec.write_verified(address, value)
            })
        })
    }

    fn set_battery_hardware_limit(&self, limit: u32) -> HalResult<()> {
        self.set_battery_limit(limit)
    }

    fn set_battery_mode(&self, mode: BatteryMode) -> HalResult<()> {
        self.set_battery_limit(match mode {
            BatteryMode::LongLife => 60,
            BatteryMode::Balanced => 80,
            BatteryMode::Workstation => 100,
        })
    }

    fn battery_health_percent(&self) -> HalResult<Option<f64>> {
        self.battery_health_cache.read(Duration::from_secs(60), || {
            #[cfg(windows)]
            {
                let Some(full) = winapi::battery_full_capacity_mwh() else {
                    return Ok(None);
                };
                // Prefer the firmware design capacity; fall back to the WMI value
                // only when the FADT field is not implemented.
                let design = winapi::battery_design_capacity_mwh().or_else(|| {
                    winapi::Wmi::first_row(
                        "ROOT\\CIMV2",
                        "SELECT DesignedCapacity FROM Win32_Battery",
                    )
                    .ok()
                    .flatten()
                    .and_then(|row| row.u64_of("DesignedCapacity"))
                    .filter(|capacity| *capacity > 0)
                });

                let Some(design) = design else {
                    return Ok(None);
                };
                if design == 0 {
                    return Ok(None);
                }
                let health = (full as f64 / design as f64) * 100.0;
                // A wildly out-of-range ratio means we read the wrong field; report
                // "unknown" rather than a nonsense percentage.
                if !(1.0..=200.0).contains(&health) {
                    return Ok(None);
                }
                Ok(Some((health * 10.0).round() / 10.0))
            }

            #[cfg(not(windows))]
            {
                Ok(None)
            }
        })
    }

    // ------------------------------------------------------------------- gpu

    fn set_gpu_mode(&self, mode: GpuMode) -> HalResult<()> {
        // Hybrid/ignoring the MUX is what Windows already does. The two
        // *switching* modes need the ACPI method the driver exposes.
        crate::core::driver::uefi::set_mode(self.acpi.read_ec(0x740).ok(), mode)
    }

    fn gpu_mode_info(&self, _cfg: &AppConfig) -> HalResult<crate::core::driver::uefi::GpuModeInfo> {
        crate::core::driver::uefi::info(self.acpi.read_ec(0x740).ok())
    }

    // --------------------------------------------------------------- display

    fn switch_refresh_rate(&self, hz: u32) -> HalResult<()> {
        #[cfg(windows)]
        {
            let displays = winapi::displays()?;
            let mut changed = Vec::new();
            let mut refusals = Vec::new();

            for display in &displays {
                if display.available_hz.contains(&hz) {
                    match winapi::set_refresh_rate(&display.device_name, hz) {
                        Ok(()) => changed.push(display.device_name.clone()),
                        Err(error) => refusals.push(format!("{}: {error}", display.device_name)),
                    }
                }
            }

            if changed.is_empty() {
                let offered = displays
                    .iter()
                    .flat_map(|display| display.available_hz.iter().copied())
                    .collect::<std::collections::BTreeSet<_>>();
                let offered = offered
                    .iter()
                    .map(|value| value.to_string())
                    .collect::<Vec<_>>()
                    .join(", ");
                return Err(HalError::unsupported(format!(
                    "no connected display offers {hz} Hz (available: {offered}){}",
                    if refusals.is_empty() {
                        String::new()
                    } else {
                        format!("; refusals: {}", refusals.join("; "))
                    }
                )));
            }

            crate::core::services::logging::info(format!(
                "refresh rate set to {hz} Hz on {}",
                changed.join(", ")
            ));
            Ok(())
        }

        #[cfg(not(windows))]
        {
            let _ = hz;
            Err(HalError::unavailable(
                "refresh-rate switching requires Windows",
            ))
        }
    }

    fn set_display_monitor_refresh_rate(&self, device_name: &str, hz: u32) -> HalResult<()> {
        #[cfg(windows)]
        {
            winapi::set_refresh_rate(device_name, hz)
        }
        #[cfg(not(windows))]
        {
            let _ = (device_name, hz);
            Err(HalError::unavailable(
                "refresh-rate switching requires Windows",
            ))
        }
    }

    fn display_brightness(&self) -> HalResult<u32> {
        #[cfg(windows)]
        {
            winapi::lcd_brightness()
        }
        #[cfg(not(windows))]
        {
            Err(HalError::unavailable("backlight control requires Windows"))
        }
    }

    fn set_display_brightness(&self, percent: u32) -> HalResult<()> {
        #[cfg(windows)]
        {
            winapi::set_lcd_brightness(percent)
        }
        #[cfg(not(windows))]
        {
            let _ = percent;
            Err(HalError::unavailable("backlight control requires Windows"))
        }
    }

    fn display_tuning_state(&self) -> HalResult<bool> {
        Ok(self.display_tuning.load(Ordering::Relaxed))
    }

    fn set_display_tuning_enabled(&self, enabled: bool) -> HalResult<()> {
        self.display_tuning.store(enabled, Ordering::Relaxed);
        if !enabled {
            // Returning to the neutral ramp is the only safe "off".
            #[cfg(windows)]
            {
                winapi::set_gamma_ramp(1.0, 1.0, 1.0)?;
            }
        }
        crate::core::services::logging::info(format!(
            "display colour tuning {}",
            if enabled { "enabled" } else { "disabled" }
        ));
        Ok(())
    }

    fn apply_display_color_preset(&self, preset: &str) -> HalResult<()> {
        // The presets are per-channel gain triples; anything else would need a
        // real ICC profile loader, which this build does not have.
        let (red, green, blue) = match preset.to_ascii_lowercase().as_str() {
            "neutral" | "default" | "off" => (1.0, 1.0, 1.0),
            "warm" | "night" => (1.0, 0.92, 0.82),
            "cool" => (0.92, 0.96, 1.0),
            "vivid" | "vibrant" => (1.08, 1.0, 1.04),
            "srgb" => (0.98, 1.0, 0.98),
            "reading" => (1.0, 0.95, 0.88),
            other => {
                return Err(HalError::unsupported(format!(
                    "'{other}' is not a colour preset this build knows; available presets are \
                     neutral, warm, cool, vivid, srgb and reading"
                )));
            }
        };

        #[cfg(windows)]
        {
            winapi::set_gamma_ramp(red, green, blue)?;
            self.display_tuning.store(true, Ordering::Relaxed);
            crate::core::services::logging::info(format!(
                "display colour preset '{preset}' applied"
            ));
            Ok(())
        }

        #[cfg(not(windows))]
        {
            let _ = (red, green, blue);
            Err(HalError::unavailable(
                "display colour tuning requires Windows",
            ))
        }
    }

    fn color_calibration_state(&self) -> HalResult<bool> {
        Ok(self.display_tuning.load(Ordering::Relaxed))
    }

    fn set_color_calibration_enabled(&self, enabled: bool) -> HalResult<()> {
        self.set_display_tuning_enabled(enabled)
    }

    // -------------------------------------------------------------- lighting

    fn lighting_state(&self) -> HalResult<LightingState> {
        crate::core::driver::keyboard::read(&self.acpi)
    }

    fn lighting_runtime_status(&self) -> HalResult<LightingRuntimeStatus> {
        let state = self.lighting_state()?;
        Ok(LightingRuntimeStatus {
            engine: "EC RGB".into(),
            effect: 0,
            brightness: state.kb_brightness,
            color: state.kb_color,
            fps: 0,
            sleep_minutes: 0,
            welcome_active: false,
        })
    }

    fn apply_keyboard_lighting(&self, state: &LightingState) -> HalResult<()> {
        crate::core::driver::keyboard::apply(&self.acpi, state)
    }

    fn apply_logo_lighting(&self, effect: u32, color: &str) -> HalResult<()> {
        let _ = (effect, color);
        self.vendor_feature("logo lighting")
    }

    fn apply_hinge_lighting(&self, speed: u32, color: &str) -> HalResult<()> {
        let _ = (speed, color);
        self.vendor_feature("hinge lighting")
    }

    fn apply_lightbar_lighting(&self, effect: u32, color: &str) -> HalResult<()> {
        let _ = (effect, color);
        self.vendor_feature("lightbar lighting")
    }

    fn apply_four_zone_colors(&self, zones: [&str; 4]) -> HalResult<()> {
        let _ = zones;
        self.vendor_feature("four-zone keyboard lighting")
    }

    fn set_lighting_sleep_timer(&self, minutes: u32) -> HalResult<()> {
        let _ = minutes;
        self.vendor_feature("the lighting sleep timer")
    }

    fn trigger_lighting_welcome(&self) -> HalResult<()> {
        self.vendor_feature("the lighting welcome animation")
    }

    fn keyboard_hardware_info(&self) -> HalResult<serde_json::Value> {
        let supported = crate::core::driver::keyboard::supported(&self.acpi);
        Ok(
            serde_json::json!({ "controller": "UniWill EC RGB", "backend": "ACPIDriver",
            "protocol": "EC RGB / 0x766 bit 2", "effects": if supported { 1 } else { 0 },
            "supported": supported, "per_key": false, "four_zone": false,
            "hardware_engine": supported, "reason": if supported { "" } else { "未检测到 EC RGB 通道" } }),
        )
    }

    fn detect_lighting_support(&self) -> HalResult<SupportFlags> {
        // Nothing to detect without the driver: report the same conservative
        // flags as `support_flags` rather than pretending a probe succeeded.
        self.support_flags()
    }

    // ----------------------------------------------------------- water cooler

    fn water_cooler_status(&self) -> HalResult<WaterCoolerStatus> {
        // The cooler is a BLE accessory that the original drove through the
        // driver's bridge. Report "not connected" rather than a fake reading.
        Ok(WaterCoolerStatus::default())
    }

    fn set_water_cooler_enabled(&self, enabled: bool) -> HalResult<()> {
        let _ = enabled;
        self.vendor_feature("the water cooler")
    }

    fn apply_cooler_strategy(
        &self,
        strategy: CoolerStrategy,
        points: &[CurvePoint],
    ) -> HalResult<()> {
        let _ = (strategy, points);
        self.vendor_feature("water cooler strategies")
    }

    fn set_water_cooler_speed(&self, duty: f64) -> HalResult<()> {
        let _ = duty;
        self.vendor_feature("water cooler pump speed")
    }

    fn set_water_cooler_led(&self, color: &str, mode: u32) -> HalResult<()> {
        let _ = (color, mode);
        self.vendor_feature("water cooler lighting")
    }

    fn reset_water_cooler(&self) -> HalResult<()> {
        self.vendor_feature("the water cooler reset")
    }

    // ----------------------------------------------------------- misc system

    fn set_win_key_locked(&self, locked: bool) -> HalResult<()> {
        #[cfg(windows)]
        {
            super::win_key::set_locked(locked)
        }
        #[cfg(not(windows))]
        {
            let _ = locked;
            self.vendor_feature("Windows key lock")
        }
    }

    fn set_fn_lock(&self, enabled: bool) -> HalResult<()> {
        let _ = enabled;
        self.vendor_feature("the Fn lock")
    }

    fn get_fn_lock(&self) -> HalResult<bool> {
        self.acpi.transaction(|ec| {
            if ec.read(0x740)? != 0x1a {
                return Err(HalError::unsupported(
                    "Fn 锁读回当前仅适配耀世 15 Air / 0x1A",
                ));
            }
            // Uniwill BIOS OEM byte, bit 4. Physical Fn+Esc on this project
            // produced 0x00 -> 0x10 -> 0x00; this command never writes EC.
            // Source and device evidence: reverse/native/FN_LOCK.md.
            Ok(ec.read(0x74e)? & 0x10 != 0)
        })
    }

    fn set_usb_charge(&self, enabled: bool) -> HalResult<()> {
        let _ = enabled;
        self.vendor_feature("USB charging while powered off")
    }

    fn set_ac_recovery(&self, enabled: bool) -> HalResult<()> {
        let _ = enabled;
        self.vendor_feature("AC power-loss recovery")
    }

    fn toggle_bios_advanced_menu(&self, enable: bool) -> HalResult<()> {
        let _ = enable;
        self.vendor_feature("the BIOS advanced menu")
    }

    fn bios_advanced_menu_status(&self) -> HalResult<bool> {
        Err(self
            .acpi
            .protocol_unsupported("the BIOS advanced menu status"))
    }

    // ----------------------------------------------------------- system info

    fn windows_power_schemes(&self) -> HalResult<Vec<PowerScheme>> {
        #[cfg(windows)]
        {
            let (active_guid, _) = winapi::active_power_scheme()?;
            let mut out = Vec::new();
            for guid in winapi::power_schemes()? {
                let name =
                    winapi::power_scheme_name(&guid).unwrap_or_else(|| winapi::guid_string(&guid));
                out.push(PowerScheme {
                    guid: winapi::guid_string(&guid),
                    name,
                    active: guid == active_guid,
                });
            }
            Ok(out)
        }

        #[cfg(not(windows))]
        {
            Err(HalError::unavailable("power schemes require Windows"))
        }
    }

    fn set_active_windows_power_scheme(&self, guid: &str) -> HalResult<()> {
        #[cfg(windows)]
        {
            let parsed = winapi::parse_guid(guid)?;
            winapi::set_active_power_scheme(&parsed)?;
            crate::core::services::logging::info(format!("active power scheme set to {guid}"));
            Ok(())
        }
        #[cfg(not(windows))]
        {
            let _ = guid;
            Err(HalError::unavailable("power schemes require Windows"))
        }
    }

    fn active_windows_power_scheme(&self) -> HalResult<String> {
        #[cfg(windows)]
        {
            let (_, name) = winapi::active_power_scheme()?;
            Ok(name)
        }
        #[cfg(not(windows))]
        {
            Err(HalError::unavailable("power schemes require Windows"))
        }
    }

    fn display_list(&self) -> HalResult<Vec<DisplayInfo>> {
        #[cfg(windows)]
        {
            let displays = winapi::displays()?;
            Ok(displays
                .into_iter()
                .map(|display| DisplayInfo {
                    device_name: display.device_name,
                    friendly_name: display.friendly_name,
                    current_hz: display.current_hz,
                    available_hz: display.available_hz,
                })
                .collect())
        }

        #[cfg(not(windows))]
        {
            Err(HalError::unavailable(
                "display enumeration requires Windows",
            ))
        }
    }

    // --------------------------------------------------------- custom tweaks

    fn apply_live_custom_tweak(&self, tweak: &crate::core::hal::LiveTweak) -> HalResult<()> {
        let _ = tweak;
        self.vendor_feature("live CPU/GPU power tweaks")
    }

    fn heal_gpu_power_contract(&self) -> HalResult<()> {
        self.vendor_feature("the GPU power contract repair")
    }

    fn set_cpu_freq_limit(&self, mhz: u32) -> HalResult<()> {
        let _ = mhz;
        self.vendor_feature("the CPU frequency limit")
    }

    fn reset_cpu_freq_limit(&self) -> HalResult<()> {
        self.vendor_feature("the CPU frequency limit")
    }
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/// Turns `NVIDIA GeForce RTX 4060 Laptop GPU` style names into something a
/// little tidier by dropping the legal suffixes drivers love.
#[cfg(windows)]
fn tidy_vendor_name(name: &str) -> String {
    let mut out = name
        .replace("(R)", "")
        .replace("(TM)", "")
        .replace("(tm)", "");
    while out.contains("  ") {
        out = out.replace("  ", " ");
    }
    out.trim().to_string()
}

/// Picks the `Win32_VideoController` row most likely to be the machine's real
/// graphics adapter.
///
/// Virtual, remote and capture adapters (IDD/parsec/sunshine/GameViewer and
/// friends) are dropped when anything else is present, and an adapter that
/// reports a sensible amount of dedicated memory outranks one that does not.
#[cfg(windows)]
fn best_video_adapter(rows: &[winapi::WmiRow]) -> Option<&winapi::WmiRow> {
    const VIRTUAL_HINTS: &[&str] = &[
        "virtual",
        "basic display",
        "basic render",
        "remote",
        "mirror",
        "indirect display",
        "idd",
        "parsec",
        "spacedesk",
        "sunshine",
        "gameviewer",
        "meta quest",
        "usb display",
    ];

    fn is_virtual(name: &str) -> bool {
        let lower = name.to_ascii_lowercase();
        VIRTUAL_HINTS.iter().any(|hint| lower.contains(hint))
    }

    fn rank(row: &winapi::WmiRow) -> i32 {
        let name = row.str_of("Name").unwrap_or_default();
        let lower = name.to_ascii_lowercase();
        let mut score = 0;
        if lower.contains("nvidia")
            || lower.contains("geforce")
            || lower.contains("quadro")
            || lower.contains("radeon")
            || lower.contains("arc ")
            || lower.contains("iris")
            || lower.contains("uhd graphics")
        {
            score += 10;
        }
        if row.u64_of("AdapterRAM").unwrap_or(0) > 0 {
            score += 2;
        }
        if lower.contains("laptop") || lower.contains("mobile") {
            score += 1;
        }
        score
    }

    let real: Vec<&winapi::WmiRow> = rows
        .iter()
        .filter(|row| !is_virtual(&row.str_of("Name").unwrap_or_default()))
        .collect();
    // If every adapter looks virtual we still report one rather than nothing —
    // an honest "this is all we can see" beats an empty card.
    let pool = if real.is_empty() {
        rows.iter().collect()
    } else {
        real
    };
    pool.into_iter().max_by_key(|row| rank(row))
}

/// Reads the first ACPI thermal zone and converts it to Celsius.
///
/// `MSAcpi_ThermalZoneTemperature.CurrentTemperature` is reported in tenths of
/// a Kelvin. The class is absent on many machines (and requires elevation on
/// some), which is why this returns `Option` rather than an error.
#[cfg(windows)]
fn acpi_thermal_zone_celsius() -> Option<f64> {
    let rows = winapi::Wmi::query_rows(
        "ROOT\\WMI",
        "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature",
    )
    .ok()?;

    let mut best: Option<f64> = None;
    for row in rows {
        let Some(raw) = row.f64_of("CurrentTemperature") else {
            continue;
        };
        // tenths of Kelvin -> Celsius
        let celsius = raw / 10.0 - 273.15;
        // Sensor-less zones report 0 K or absurd values; discard those.
        if !(-20.0..=150.0).contains(&celsius) {
            continue;
        }
        best = Some(match best {
            Some(current) => current.max(celsius),
            None => celsius,
        });
    }
    best.map(|value| (value * 10.0).round() / 10.0)
}

// ---------------------------------------------------------------------------
// CPU load sampling
// ---------------------------------------------------------------------------

#[cfg(windows)]
mod cpu_load {
    use windows::Win32::Foundation::FILETIME;
    use windows::Win32::System::Threading::GetSystemTimes;

    /// Previous (idle, kernel, user) sample.
    static PREVIOUS: std::sync::Mutex<Option<(u64, u64, u64)>> = std::sync::Mutex::new(None);

    fn to_u64(time: FILETIME) -> u64 {
        (u64::from(time.dwHighDateTime) << 32) | u64::from(time.dwLowDateTime)
    }

    /// System-wide CPU load in percent, derived from two consecutive
    /// `GetSystemTimes` samples.
    ///
    /// The first call has no previous sample to compare against, so it returns
    /// `0.0`; the telemetry loop calls this every couple of seconds, so the
    /// value settles immediately afterwards.
    pub fn sample() -> f64 {
        let mut idle = FILETIME::default();
        let mut kernel = FILETIME::default();
        let mut user = FILETIME::default();
        // SAFETY: all three out-parameters are valid, initialised `FILETIME`
        // values owned by this frame.
        if unsafe { GetSystemTimes(Some(&mut idle), Some(&mut kernel), Some(&mut user)) }.is_err() {
            return 0.0;
        }

        let current = (to_u64(idle), to_u64(kernel), to_u64(user));
        let mut guard = PREVIOUS.lock().unwrap_or_else(|poison| poison.into_inner());
        let Some(previous) = *guard else {
            *guard = Some(current);
            return 0.0;
        };
        *guard = Some(current);
        drop(guard);

        let idle_delta = current.0.saturating_sub(previous.0) as f64;
        // `kernel` time already includes idle time, so total = kernel + user.
        let total_delta =
            (current.1.saturating_sub(previous.1) + current.2.saturating_sub(previous.2)) as f64;
        if total_delta <= 0.0 {
            return 0.0;
        }
        let busy = (total_delta - idle_delta).max(0.0);
        ((busy / total_delta) * 1000.0).round() / 10.0
    }
}
