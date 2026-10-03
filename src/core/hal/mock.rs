//! A fully functional hardware simulator.
//!
//! `MockHal` exists so the entire UI is usable — and demoable — on a machine
//! that has none of the UniWill hardware. Nothing here fabricates *success* for
//! a destructive operation: the mutating calls behave as if the feature were
//! present, which is the whole point of a mock backend, and they are clearly
//! labelled as simulated in the log. The real backend is the one that must be
//! honest about what it cannot do.
//!
//! The simulation is deliberately *plausible*: the CPU temperature follows the
//! load with a thermal time constant, the fans ramp up when the temperature
//! crosses the configured curve, and the battery drains (or charges) at a rate
//! that depends on the current power mode and the AC state.

use super::{
    ColorPreset, DeviceSwitch, DisplayInfo, HardwareHal, LightingRuntimeStatus, LiveTweak,
    PowerScheme, WaterCoolerStatus,
};
use crate::core::config::{
    normalise_hex, AppConfig, BatteryMode, BatteryStatus, CoolerStrategy, CpuStatus, CurvePoint,
    DeviceStatus, FanStatus, GpuMode, GpuStatus, HardwareStatus, LightingState, PowerModeId,
    SupportFlags,
};
use crate::core::error::{HalError, HalResult};
use std::sync::atomic::{AtomicBool, AtomicU32, AtomicU64, Ordering};
use std::sync::Mutex;
use std::time::Instant;

/// Small deterministic PRNG so the simulator produces a repeatable-looking but
/// non-repetitive signal without pulling in `rand`.
struct Lcg(u64);

impl Lcg {
    fn new(seed: u64) -> Self {
        Lcg(seed | 1)
    }

    fn next_u32(&mut self) -> u32 {
        // Numerical Recipes LCG constants.
        self.0 = self
            .0
            .wrapping_mul(6_364_136_223_846_793_005)
            .wrapping_add(1_442_695_040_888_963_407);
        (self.0 >> 33) as u32
    }

    fn next_f64(&mut self) -> f64 {
        f64::from(self.next_u32()) / f64::from(u32::MAX)
    }

    /// Uniform in `[lo, hi)`.
    fn range(&mut self, lo: f64, hi: f64) -> f64 {
        lo + self.next_f64() * (hi - lo)
    }
}

#[derive(Debug, Clone)]
struct SimState {
    cpu_temp: f64,
    gpu_temp: f64,
    cpu_load: f64,
    gpu_load: f64,
    fan_rpm: f64,
    gpu_fan_rpm: f64,
    battery_percent: f64,
    water_temp: f64,
    pump_volt: f64,
    burst: f64,
    on_ac: bool,
}

/// The simulator.
pub struct MockHal {
    start: Instant,
    rng: Mutex<Lcg>,
    state: Mutex<SimState>,
    power_mode: AtomicU32,
    fan_boost: AtomicBool,
    battery_limit: AtomicU32,
    lighting_enabled: AtomicBool,
    kb_brightness: AtomicU32,
    kb_effect: AtomicU32,
    lighting: Mutex<LightingState>,
    cfg: Mutex<AppConfig>,
    brightness: AtomicU32,
    display_tuning: AtomicBool,
    color_calibration: AtomicBool,
    last_tick_ms: AtomicU64,
}

impl Default for MockHal {
    fn default() -> Self {
        Self::new()
    }
}

impl MockHal {
    pub fn new() -> Self {
        Self {
            start: Instant::now(),
            // Fixed seed: the simulator looks alive but never surprises a test.
            rng: Mutex::new(Lcg::new(0x0FEE_D00D_1234_5678)),
            state: Mutex::new(SimState {
                cpu_temp: 46.0,
                gpu_temp: 44.0,
                cpu_load: 8.0,
                gpu_load: 3.0,
                fan_rpm: 0.0,
                gpu_fan_rpm: 0.0,
                battery_percent: 78.0,
                water_temp: 33.0,
                pump_volt: 0.0,
                burst: 0.0,
                on_ac: true,
            }),
            power_mode: AtomicU32::new(1),
            fan_boost: AtomicBool::new(false),
            battery_limit: AtomicU32::new(100),
            lighting_enabled: AtomicBool::new(true),
            kb_brightness: AtomicU32::new(3),
            kb_effect: AtomicU32::new(0),
            lighting: Mutex::new(LightingState::default()),
            cfg: Mutex::new(AppConfig::default()),
            brightness: AtomicU32::new(70),
            display_tuning: AtomicBool::new(false),
            color_calibration: AtomicBool::new(false),
            last_tick_ms: AtomicU64::new(0),
        }
    }

    fn elapsed_ms(&self) -> u64 {
        self.start.elapsed().as_millis() as u64
    }

    /// Advances the simulation to "now". Safe to call from any thread; the
    /// lock is only held for the duration of the integration step.
    fn tick(&self) {
        let now = self.elapsed_ms();
        let last = self.last_tick_ms.swap(now, Ordering::Relaxed);
        let dt_ms = now.saturating_sub(last).min(2_000);
        if dt_ms == 0 {
            return;
        }
        let dt = dt_ms as f64 / 1000.0;

        let mode = self.power_mode.load(Ordering::Relaxed);
        let boost = self.fan_boost.load(Ordering::Relaxed);
        let limit = self.battery_limit.load(Ordering::Relaxed);

        let Ok(mut rng) = self.rng.lock() else { return };
        let Ok(mut sim) = self.state.lock() else {
            return;
        };
        let cfg = match self.cfg.lock() {
            Ok(c) => c.clone(),
            Err(p) => p.into_inner().clone(),
        };

        // ---- load model -----------------------------------------------------
        // Each mode has a different sustained-load band. A slow "burst" term
        // makes the trace look like real work rather than white noise.
        let (load_lo, load_hi) = match mode {
            0 => (3.0, 22.0),
            1 => (10.0, 45.0),
            2 => (35.0, 92.0),
            _ => (15.0, 60.0),
        };
        sim.burst = (sim.burst * 0.92 + rng.range(-1.0, 1.0) * 0.08).clamp(-1.0, 1.0);
        let target_load =
            (load_lo + (load_hi - load_lo) * (0.5 + 0.5 * sim.burst)).clamp(0.0, 100.0);
        sim.cpu_load += (target_load - sim.cpu_load) * (dt / 1.5).min(1.0);
        let gpu_target = if mode == 2 {
            rng.range(40.0, 88.0)
        } else {
            rng.range(0.0, 18.0)
        };
        sim.gpu_load += (gpu_target - sim.gpu_load) * (dt / 2.0).min(1.0);

        // ---- thermal model --------------------------------------------------
        // Ambient 26 °C, load heats the die, fans cool it.
        let air_flow = sim.fan_rpm / 4_500.0;
        let cpu_dissipation = 1.9 * (1.0 + air_flow * 3.2);
        sim.cpu_temp +=
            (26.0 + sim.cpu_load * 0.72 - sim.cpu_temp) * (cpu_dissipation * dt / 8.0).min(1.0);
        let gpu_dissipation = 1.4 * (1.0 + air_flow * 3.0);
        sim.gpu_temp +=
            (26.0 + sim.gpu_load * 0.70 - sim.gpu_temp) * (gpu_dissipation * dt / 9.0).min(1.0);
        sim.cpu_temp = sim.cpu_temp.clamp(28.0, 101.0);
        sim.gpu_temp = sim.gpu_temp.clamp(28.0, 101.0);

        // ---- fan curve ------------------------------------------------------
        let hottest = sim.cpu_temp.max(sim.gpu_temp);
        let mut duty = curve_duty(&cfg.cooler_curve_points, hottest);
        if boost {
            duty = 100.0;
        }
        if sim.cpu_temp >= 97.0 {
            // Hardware protection override, exactly like a real EC.
            duty = 100.0;
        }
        let target_rpm = duty / 100.0 * 5_200.0;
        let ramp = if target_rpm > sim.fan_rpm {
            1_400.0
        } else {
            700.0
        };
        let delta = (target_rpm - sim.fan_rpm).clamp(-ramp * dt, ramp * dt);
        sim.fan_rpm = (sim.fan_rpm + delta).clamp(0.0, 5_400.0);
        // The GPU fan lags the CPU fan slightly.
        sim.gpu_fan_rpm += (sim.fan_rpm * 0.96 - sim.gpu_fan_rpm) * (dt / 1.2).min(1.0);

        // ---- battery --------------------------------------------------------
        let (drain_per_hour, charging) = if sim.on_ac {
            (0.0, sim.battery_percent < limit as f64 - 0.5)
        } else {
            let base = match mode {
                0 => 7.0,
                1 => 14.0,
                2 => 34.0,
                _ => 18.0,
            };
            (base, false)
        };
        if charging {
            sim.battery_percent = (sim.battery_percent + 30.0 * dt / 3600.0).min(limit as f64);
        } else {
            sim.battery_percent = (sim.battery_percent - drain_per_hour * dt / 3600.0).max(3.0);
        }

        // ---- water cooler ---------------------------------------------------
        if cfg.water_cooler_enabled {
            sim.water_temp += (24.0 + duty * 0.16 - sim.water_temp) * (dt / 25.0).min(1.0);
            sim.pump_volt += (pump_volt_for(duty) - sim.pump_volt) * (dt / 3.0).min(1.0);
        } else {
            sim.water_temp += (27.0 - sim.water_temp) * (dt / 60.0).min(1.0);
            sim.pump_volt += (0.0 - sim.pump_volt) * (dt / 3.0).min(1.0);
        }
    }

    fn snapshot(&self) -> SimState {
        self.tick();
        match self.state.lock() {
            Ok(s) => s.clone(),
            Err(p) => p.into_inner().clone(),
        }
    }

    fn set_mode(&self, mode: PowerModeId) {
        self.power_mode.store(mode.min(3) as u32, Ordering::Relaxed);
    }
}

/// Linear interpolation over the user fan curve.
fn curve_duty(points: &[CurvePoint], temp: f64) -> f64 {
    if points.is_empty() {
        return 45.0;
    }
    let mut sorted: Vec<&CurvePoint> = points.iter().collect();
    sorted.sort_by(|a, b| {
        a.temp
            .partial_cmp(&b.temp)
            .unwrap_or(std::cmp::Ordering::Equal)
    });
    if temp <= sorted[0].temp {
        return sorted[0].duty;
    }
    for pair in sorted.windows(2) {
        let (a, b) = (pair[0], pair[1]);
        if temp <= b.temp {
            let span = (b.temp - a.temp).max(0.001);
            let t = (temp - a.temp) / span;
            return a.duty + (b.duty - a.duty) * t;
        }
    }
    sorted[sorted.len() - 1].duty
}

/// The pump accepts 7 V / 8 V / 11 V steps.
fn pump_volt_for(duty: f64) -> f64 {
    if duty >= 80.0 {
        11.0
    } else if duty >= 45.0 {
        8.0
    } else {
        7.0
    }
}

fn sim_log(message: &str) {
    crate::core::services::logging::debug(format!("[sim] {message}"));
}

impl HardwareHal for MockHal {
    fn backend_name(&self) -> &'static str {
        "mock"
    }

    // -------------------------------------------------------------- telemetry

    fn hardware_status(&self, cfg: &AppConfig) -> HalResult<HardwareStatus> {
        if let Ok(mut guard) = self.cfg.lock() {
            *guard = cfg.clone();
        }
        self.tick();
        let sim = self.snapshot();
        let mode = self.power_mode.load(Ordering::Relaxed) as PowerModeId;
        Ok(HardwareStatus {
            cpu: CpuStatus {
                temp: Some(round1(sim.cpu_temp)),
                freq_mhz: (800.0 + sim.cpu_load / 100.0 * 3_900.0).round(),
                load: round1(sim.cpu_load),
                power_w: Some(round1(sim.cpu_load / 100.0 * 65.0)),
            },
            gpu: GpuStatus {
                present: true,
                name: "NVIDIA GeForce RTX 4060 Laptop GPU (simulated)".into(),
                temp: Some(round1(sim.gpu_temp)),
                freq_mhz: Some((300.0 + sim.gpu_load / 100.0 * 2_100.0).round()),
                load: Some(round1(sim.gpu_load)),
                vram_used_mb: Some((sim.gpu_load / 100.0 * 6_000.0).round() as u64),
                vram_total_mb: Some(8_192),
            },
            fans: FanStatus {
                available: true,
                cpu_rpm: sim.fan_rpm.round() as u32,
                gpu_rpm: sim.gpu_fan_rpm.round() as u32,
            },
            battery: BatteryStatus {
                on_ac: sim.on_ac,
                percent: round1(sim.battery_percent),
                charging: sim.on_ac
                    && sim.battery_percent < self.battery_limit.load(Ordering::Relaxed) as f64,
                limit: self.battery_limit.load(Ordering::Relaxed),
                health_percent: Some(96.4),
            },
            device: DeviceStatus {
                model: "Mechrevo / UniWill (simulated)".into(),
                project: "GM6IX9B".into(),
                bios: "N.1.20SIM (2026-01-01)".into(),
                ec: "1.07.05SIM".into(),
                serial: "SIMULATED-0000-0000".into(),
            },
            support_flags: self.support_flags()?,
            power_mode: Some(mode),
            power_mode_error: None,
            gpu_mode: Some(cfg.gpu_mode),
            fan_boost: Some(cfg.fan_boost),
            windows_power_scheme: self.active_windows_power_scheme()?,
            elevated: self.is_elevated(),
        })
    }

    fn cpu_status(&self) -> HalResult<CpuStatus> {
        let sim = self.snapshot();
        Ok(CpuStatus {
            temp: Some(round1(sim.cpu_temp)),
            freq_mhz: (800.0 + sim.cpu_load / 100.0 * 3_900.0).round(),
            load: round1(sim.cpu_load),
            power_w: Some(round1(sim.cpu_load / 100.0 * 65.0)),
        })
    }

    fn gpu_status(&self) -> HalResult<GpuStatus> {
        let sim = self.snapshot();
        Ok(GpuStatus {
            present: true,
            name: "NVIDIA GeForce RTX 4060 Laptop GPU (simulated)".into(),
            temp: Some(round1(sim.gpu_temp)),
            freq_mhz: Some((300.0 + sim.gpu_load / 100.0 * 2_100.0).round()),
            load: Some(round1(sim.gpu_load)),
            vram_used_mb: Some((sim.gpu_load / 100.0 * 6_000.0).round() as u64),
            vram_total_mb: Some(8_192),
        })
    }

    fn fan_status(&self) -> HalResult<FanStatus> {
        let sim = self.snapshot();
        Ok(FanStatus {
            available: true,
            cpu_rpm: sim.fan_rpm.round() as u32,
            gpu_rpm: sim.gpu_fan_rpm.round() as u32,
        })
    }

    fn battery_status(&self, limit: u32) -> HalResult<BatteryStatus> {
        let sim = self.snapshot();
        Ok(BatteryStatus {
            on_ac: sim.on_ac,
            percent: round1(sim.battery_percent),
            charging: sim.on_ac && sim.battery_percent < limit as f64,
            limit,
            health_percent: Some(96.4),
        })
    }

    fn device_status(&self) -> HalResult<DeviceStatus> {
        Ok(DeviceStatus {
            model: "Mechrevo / UniWill (simulated)".into(),
            project: "GM6IX9B".into(),
            bios: "N.1.20SIM (2026-01-01)".into(),
            ec: "1.07.05SIM".into(),
            serial: "SIMULATED-0000-0000".into(),
        })
    }

    fn support_flags(&self) -> HalResult<SupportFlags> {
        Ok(SupportFlags {
            fan_boost: true,
            battery_limit: true,
            water_cooler: true,
            four_zone: true,
            logo: true,
            hinge: true,
            lightbar: true,
            bios_advanced: true,
            gpu_switching: true,
            display_tuning: true,
            fan_curve: true,
        })
    }

    fn is_elevated(&self) -> bool {
        // The simulator never claims the process is elevated for real; report
        // the actual token state so the UI's elevation banner is meaningful.
        crate::core::hal::windows::is_process_elevated_if_windows()
    }

    // ------------------------------------------------------------------ power

    fn set_power_mode(&self, mode: PowerModeId) -> HalResult<()> {
        sim_log(&format!("set_power_mode({mode})"));
        self.set_mode(mode);
        Ok(())
    }

    fn get_power_mode(&self) -> HalResult<PowerModeId> {
        Ok(self.power_mode.load(Ordering::Relaxed) as PowerModeId)
    }

    fn set_fan_boost(&self, enabled: bool) -> HalResult<()> {
        sim_log(&format!("set_fan_boost({enabled})"));
        self.fan_boost.store(enabled, Ordering::Relaxed);
        Ok(())
    }

    fn toggle_fan_curve_control(
        &self,
        cpu_curve: &[CurvePoint],
        gpu_curve: &[CurvePoint],
    ) -> HalResult<()> {
        sim_log(&format!(
            "toggle_fan_curve_control(cpu={} pts, gpu={} pts)",
            cpu_curve.len(),
            gpu_curve.len()
        ));
        if let Ok(mut cfg) = self.cfg.lock() {
            if !cpu_curve.is_empty() {
                cfg.cooler_curve_points = cpu_curve.to_vec();
            }
        }
        Ok(())
    }

    fn apply_fan_curve_live(
        &self,
        cpu_curve: &[CurvePoint],
        gpu_curve: &[CurvePoint],
    ) -> HalResult<()> {
        sim_log(&format!(
            "apply_fan_curve_live(cpu={} pts, gpu={} pts)",
            cpu_curve.len(),
            gpu_curve.len()
        ));
        if let Ok(mut cfg) = self.cfg.lock() {
            if !cpu_curve.is_empty() {
                cfg.cooler_curve_points = cpu_curve.to_vec();
            }
        }
        Ok(())
    }

    fn set_fan_ramp_rate(&self, speed_ms: u32) -> HalResult<()> {
        sim_log(&format!("set_fan_ramp_rate({speed_ms} ms)"));
        Ok(())
    }

    fn set_fan_isolated_output(&self, enabled: bool) -> HalResult<()> {
        sim_log(&format!("set_fan_isolated_output({enabled})"));
        Ok(())
    }

    fn set_sleep_auto_off(&self, enabled: bool) -> HalResult<()> {
        sim_log(&format!("set_sleep_auto_off({enabled})"));
        Ok(())
    }

    fn set_master_sleep_guard(&self, enabled: bool) -> HalResult<()> {
        sim_log(&format!("set_master_sleep_guard({enabled})"));
        Ok(())
    }

    // ---------------------------------------------------------------- battery

    fn set_battery_limit(&self, limit: u32) -> HalResult<()> {
        sim_log(&format!("set_battery_limit({limit})"));
        self.battery_limit
            .store(limit.clamp(50, 100), Ordering::Relaxed);
        Ok(())
    }

    fn set_battery_hardware_limit(&self, limit: u32) -> HalResult<()> {
        sim_log(&format!("set_battery_hardware_limit({limit})"));
        self.battery_limit
            .store(limit.clamp(50, 100), Ordering::Relaxed);
        Ok(())
    }

    fn set_battery_mode(&self, mode: BatteryMode) -> HalResult<()> {
        sim_log(&format!("set_battery_mode({})", mode.as_str()));
        Ok(())
    }

    fn battery_health_percent(&self) -> HalResult<Option<f64>> {
        Ok(Some(96.4))
    }

    // -------------------------------------------------------------------- gpu

    fn set_gpu_mode(&self, mode: GpuMode) -> HalResult<()> {
        sim_log(&format!("set_gpu_mode({})", mode.as_str()));
        Ok(())
    }

    // ---------------------------------------------------------------- display

    fn switch_refresh_rate(&self, hz: u32) -> HalResult<()> {
        sim_log(&format!("switch_refresh_rate({hz})"));
        Ok(())
    }

    fn set_display_monitor_refresh_rate(&self, device_name: &str, hz: u32) -> HalResult<()> {
        sim_log(&format!(
            "set_display_monitor_refresh_rate({device_name}, {hz})"
        ));
        Ok(())
    }

    fn display_brightness(&self) -> HalResult<u32> {
        Ok(self.brightness.load(Ordering::Relaxed))
    }

    fn set_display_brightness(&self, percent: u32) -> HalResult<()> {
        self.brightness.store(percent.min(100), Ordering::Relaxed);
        Ok(())
    }

    fn display_tuning_state(&self) -> HalResult<bool> {
        Ok(self.display_tuning.load(Ordering::Relaxed))
    }

    fn set_display_tuning_enabled(&self, enabled: bool) -> HalResult<()> {
        self.display_tuning.store(enabled, Ordering::Relaxed);
        Ok(())
    }

    fn apply_display_color_preset(&self, preset: &str) -> HalResult<()> {
        sim_log(&format!("apply_display_color_preset({preset})"));
        Ok(())
    }

    fn color_calibration_state(&self) -> HalResult<bool> {
        Ok(self.color_calibration.load(Ordering::Relaxed))
    }

    fn set_color_calibration_enabled(&self, enabled: bool) -> HalResult<()> {
        self.color_calibration.store(enabled, Ordering::Relaxed);
        Ok(())
    }

    // --------------------------------------------------------------- lighting

    fn lighting_state(&self) -> HalResult<LightingState> {
        match self.lighting.lock() {
            Ok(guard) => Ok(guard.clone()),
            Err(poisoned) => Ok(poisoned.into_inner().clone()),
        }
    }

    fn lighting_runtime_status(&self) -> HalResult<LightingRuntimeStatus> {
        Ok(LightingRuntimeStatus {
            engine: if self.lighting_enabled.load(Ordering::Relaxed) {
                "simulated".into()
            } else {
                "disabled".into()
            },
            effect: self.kb_effect.load(Ordering::Relaxed),
            brightness: self.kb_brightness.load(Ordering::Relaxed),
            fps: 30,
            color: "#ff00ff".into(),
            sleep_minutes: 0,
            welcome_active: false,
        })
    }

    fn apply_keyboard_lighting(&self, state: &LightingState) -> HalResult<()> {
        sim_log(&format!(
            "apply_keyboard_lighting(effect={}, color={})",
            state.kb_effect, state.kb_color
        ));
        self.kb_effect.store(state.kb_effect, Ordering::Relaxed);
        self.kb_brightness
            .store(state.kb_brightness, Ordering::Relaxed);
        self.lighting_enabled
            .store(state.enabled, Ordering::Relaxed);
        if let Ok(mut guard) = self.lighting.lock() {
            *guard = state.clone();
        }
        Ok(())
    }

    fn apply_logo_lighting(&self, effect: u32, color: &str) -> HalResult<()> {
        sim_log(&format!(
            "apply_logo_lighting({effect}, {})",
            normalise_hex(color)
        ));
        if let Ok(mut guard) = self.lighting.lock() {
            guard.logo_color = normalise_hex(color);
        }
        Ok(())
    }

    fn apply_hinge_lighting(&self, speed: u32, color: &str) -> HalResult<()> {
        sim_log(&format!(
            "apply_hinge_lighting({speed}, {})",
            normalise_hex(color)
        ));
        if let Ok(mut guard) = self.lighting.lock() {
            guard.hinge_color = normalise_hex(color);
        }
        Ok(())
    }

    fn apply_lightbar_lighting(&self, effect: u32, color: &str) -> HalResult<()> {
        sim_log(&format!(
            "apply_lightbar_lighting({effect}, {})",
            normalise_hex(color)
        ));
        if let Ok(mut guard) = self.lighting.lock() {
            guard.lightbar_color = normalise_hex(color);
        }
        Ok(())
    }

    fn apply_four_zone_colors(&self, zones: [&str; 4]) -> HalResult<()> {
        sim_log("apply_four_zone_colors");
        if let Ok(mut guard) = self.lighting.lock() {
            for (slot, value) in guard.four_zone_colors.iter_mut().zip(zones.iter()) {
                *slot = normalise_hex(value);
            }
        }
        Ok(())
    }

    fn set_lighting_sleep_timer(&self, minutes: u32) -> HalResult<()> {
        if let Ok(mut guard) = self.lighting.lock() {
            guard.sleep_minutes = minutes;
        }
        Ok(())
    }

    fn trigger_lighting_welcome(&self) -> HalResult<()> {
        sim_log("trigger_lighting_welcome");
        Ok(())
    }

    fn keyboard_hardware_info(&self) -> HalResult<serde_json::Value> {
        Ok(serde_json::json!({
            "backend": "simulator",
            "controller": "SIMULATED ITE 8295",
            "vendor_id": "0x048d",
            "product_id": "0x600b",
            "four_zone": true,
            "per_key": false,
            "effects": 24,
            "protocol": "simulated",
        }))
    }

    fn detect_lighting_support(&self) -> HalResult<SupportFlags> {
        self.support_flags()
    }

    // ----------------------------------------------------------- water cooler

    fn water_cooler_status(&self) -> HalResult<WaterCoolerStatus> {
        let sim = self.snapshot();
        let enabled = self
            .cfg
            .lock()
            .map(|c| c.water_cooler_enabled)
            .unwrap_or(false);
        Ok(WaterCoolerStatus {
            connected: enabled,
            mac: if enabled {
                Some("AA:BB:CC:DD:EE:FF".into())
            } else {
                None
            },
            water_temp: if enabled {
                Some(round1(sim.water_temp))
            } else {
                None
            },
            pump_volt: if enabled { Some(sim.pump_volt) } else { None },
            fan_rpm: if enabled {
                Some(sim.fan_rpm.round() as u32)
            } else {
                None
            },
            duty: round1(sim.fan_rpm / 52.0),
            strategy: self
                .cfg
                .lock()
                .map(|c| c.cooler_strategy)
                .unwrap_or_default(),
        })
    }

    fn set_water_cooler_enabled(&self, enabled: bool) -> HalResult<()> {
        sim_log(&format!("set_water_cooler_enabled({enabled})"));
        if let Ok(mut cfg) = self.cfg.lock() {
            cfg.water_cooler_enabled = enabled;
        }
        Ok(())
    }

    fn apply_cooler_strategy(
        &self,
        strategy: CoolerStrategy,
        points: &[CurvePoint],
    ) -> HalResult<()> {
        sim_log(&format!(
            "apply_cooler_strategy({}, {} points)",
            strategy.as_str(),
            points.len()
        ));
        if let Ok(mut cfg) = self.cfg.lock() {
            cfg.cooler_strategy = strategy;
            if !points.is_empty() {
                cfg.cooler_curve_points = points.to_vec();
            }
        }
        Ok(())
    }

    fn set_water_cooler_speed(&self, duty: f64) -> HalResult<()> {
        sim_log(&format!("set_water_cooler_speed({duty})"));
        Ok(())
    }

    fn set_water_cooler_led(&self, color: &str, mode: u32) -> HalResult<()> {
        sim_log(&format!(
            "set_water_cooler_led({}, {mode})",
            normalise_hex(color)
        ));
        Ok(())
    }

    fn reset_water_cooler(&self) -> HalResult<()> {
        sim_log("reset_water_cooler");
        if let Ok(mut cfg) = self.cfg.lock() {
            cfg.water_cooler_enabled = false;
        }
        Ok(())
    }

    // ------------------------------------------------------------ misc system

    fn set_win_key_locked(&self, locked: bool) -> HalResult<()> {
        sim_log(&format!("set_win_key_locked({locked})"));
        Ok(())
    }

    fn set_fn_lock(&self, enabled: bool) -> HalResult<()> {
        sim_log(&format!("set_fn_lock({enabled})"));
        Ok(())
    }

    fn set_usb_charge(&self, enabled: bool) -> HalResult<()> {
        sim_log(&format!("set_usb_charge({enabled})"));
        Ok(())
    }

    fn set_ac_recovery(&self, enabled: bool) -> HalResult<()> {
        sim_log(&format!("set_ac_recovery({enabled})"));
        Ok(())
    }

    fn toggle_bios_advanced_menu(&self, enable: bool) -> HalResult<()> {
        sim_log(&format!("toggle_bios_advanced_menu({enable})"));
        Ok(())
    }

    fn bios_advanced_menu_status(&self) -> HalResult<bool> {
        Ok(false)
    }

    // ------------------------------------------------------------ system info

    fn windows_power_schemes(&self) -> HalResult<Vec<PowerScheme>> {
        Ok(vec![
            PowerScheme {
                guid: "381b4222-f694-41f0-9685-ff5bb260df2e".into(),
                name: "Balanced (simulated)".into(),
                active: true,
            },
            PowerScheme {
                guid: "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c".into(),
                name: "High performance (simulated)".into(),
                active: false,
            },
            PowerScheme {
                guid: "a1841308-3541-4fab-bc81-f71556f20b4a".into(),
                name: "Power saver (simulated)".into(),
                active: false,
            },
        ])
    }

    fn set_active_windows_power_scheme(&self, guid: &str) -> HalResult<()> {
        sim_log(&format!("set_active_windows_power_scheme({guid})"));
        Ok(())
    }

    fn active_windows_power_scheme(&self) -> HalResult<String> {
        Ok("Balanced (simulated)".into())
    }

    fn display_list(&self) -> HalResult<Vec<DisplayInfo>> {
        Ok(vec![DisplayInfo {
            device_name: r"\\.\DISPLAY1".into(),
            friendly_name: "Simulated internal panel".into(),
            current_hz: 165,
            available_hz: vec![60, 120, 144, 165],
        }])
    }

    // ---------------------------------------------------------- custom tweaks

    fn apply_live_custom_tweak(&self, tweak: &LiveTweak) -> HalResult<()> {
        sim_log(&format!(
            "apply_live_custom_tweak(temp_target={}, ctgp={}/{} W, db={}/{} W, super_perf={}, \
             gpu_core={}, gpu_mem={})",
            tweak.temp_target,
            tweak.ctgp_enabled,
            tweak.ctgp_watts,
            tweak.db_enabled,
            tweak.db_watts,
            tweak.super_perf,
            tweak.gpu_core_offset,
            tweak.gpu_mem_offset
        ));
        Ok(())
    }

    fn heal_gpu_power_contract(&self) -> HalResult<()> {
        sim_log("heal_gpu_power_contract");
        Ok(())
    }

    fn set_cpu_freq_limit(&self, mhz: u32) -> HalResult<()> {
        sim_log(&format!("set_cpu_freq_limit({mhz})"));
        Ok(())
    }

    fn reset_cpu_freq_limit(&self) -> HalResult<()> {
        sim_log("reset_cpu_freq_limit");
        Ok(())
    }

    fn open_mini_drawer(&self) -> HalResult<()> {
        Ok(())
    }
}

fn round1(value: f64) -> f64 {
    (value * 10.0).round() / 10.0
}

/// The simulator does not model `DeviceSwitch`es beyond what the UI needs.
impl MockHal {
    /// Lists the optional chassis switches. Kept off the trait because the real
    /// backend resolves them from ACPI/registry while the simulator invents them.
    pub fn device_switches(&self) -> Vec<DeviceSwitch> {
        vec![
            DeviceSwitch {
                id: "usb_charge".into(),
                name: "USB charging in sleep".into(),
                enabled: true,
                supported: true,
            },
            DeviceSwitch {
                id: "ac_recovery".into(),
                name: "AC power recovery".into(),
                enabled: true,
                supported: true,
            },
            DeviceSwitch {
                id: "fn_lock".into(),
                name: "Fn lock".into(),
                enabled: false,
                supported: true,
            },
            DeviceSwitch {
                id: "win_key_lock".into(),
                name: "Windows key lock".into(),
                enabled: false,
                supported: true,
            },
        ]
    }

    /// Built-in display colour presets (simulator copy).
    pub fn color_presets(&self) -> Vec<ColorPreset> {
        vec![ColorPreset {
            id: "sRGB".into(),
            name: "sRGB".into(),
            builtin: true,
            values: std::collections::BTreeMap::new(),
        }]
    }

    /// Unused in the simulator but kept so `mock` and `windows` expose the same
    /// surface for the few callers that downcast.
    pub fn unsupported(reason: &str) -> HalError {
        HalError::unsupported(reason)
    }
}
