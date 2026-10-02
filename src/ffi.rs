use std::ffi::{c_char, CStr, CString};
use std::sync::OnceLock;

use serde::de::DeserializeOwned;
use serde_json::Value;

use crate::core::config::{AppConfig, CoolerStrategy, CurvePoint, LightingState, OsdConfig};
use crate::core::hal::ProfilePreset;
use crate::core::{create_hal, AcpiDriver, Api, AppState};

pub const ABI_VERSION: u32 = 1;

/// Every command [`lumadesk_call`] accepts, as `(name, argument names)`.
///
/// The front end generates its wrapper from this, and the test below proves the
/// list and [`dispatch`] agree, so the two cannot drift apart silently.
pub const COMMANDS: &[&str] = &[
    "get_hardware_status",
    "get_app_config",
    "save_app_config",
    "import_models_json",
    "export_models_json",
    "open_models_in_notepad",
    "set_power_mode",
    "set_windows_power_mode",
    "set_fan_boost",
    "set_battery_limit",
    "set_battery_hardware_limit",
    "set_battery_mode",
    "set_active_windows_power_scheme",
    "set_power_option",
    "apply_live_custom_tweak",
    "super_perf_enable",
    "heal_gpu_power_contract",
    "apply_fan_curve_live",
    "toggle_fan_curve_control",
    "toggle_fan_ramp_rate",
    "toggle_fan_isolated_output",
    "set_gpu_mode",
    "get_gpu_mode_info",
    "restart_system",
    "get_power_settings",
    "get_power_settings_detail",
    "get_windows_power_schemes",
    "get_device_switches",
    "set_device_switch",
    "set_sleep_auto_off",
    "set_master_sleep_guard",
    "get_sleep_guard_option",
    "switch_refresh_rate",
    "set_display_monitor_refresh_rate",
    "set_auto_min_refresh_on_battery",
    "set_display_tuning_enabled",
    "apply_display_color_preset",
    "list_color_presets",
    "import_color_preset_content",
    "get_display_brightness",
    "set_display_brightness",
    "get_display_tuning_state",
    "get_color_calibration_state",
    "set_color_calibration_enabled",
    "get_displays",
    "get_display_settings",
    "get_lighting_state",
    "get_lighting_status",
    "get_lighting_runtime_status",
    "get_lighting_runtime",
    "get_keyboard_hardware_info",
    "apply_keyboard_lighting",
    "apply_logo_lighting",
    "apply_hinge_lighting",
    "apply_lightbar_lighting",
    "apply_four_zone_colors",
    "set_lighting_sleep_timer",
    "trigger_lighting_welcome",
    "set_keyboard_engine",
    "set_streamer_fps",
    "set_custom_fx_text",
    "set_auto_fallback_kb_on_battery",
    "list_custom_brfx_scripts",
    "get_custom_brfx_list",
    "apply_custom_brfx_script",
    "save_custom_brfx",
    "open_brfx_in_notepad",
    "get_water_cooler_status",
    "get_water_cooler_reason",
    "set_water_cooler_enabled",
    "apply_cooler_strategy",
    "set_water_cooler_speed",
    "set_water_cooler_led",
    "set_water_cooler_strategy",
    "reset_water_cooler",
    "list_profile_presets",
    "get_profiles_list",
    "get_default_builtin_presets",
    "create_profile_preset",
    "save_profile_preset",
    "delete_profile_preset",
    "rename_profile_preset",
    "set_active_profile_id",
    "get_active_profile_id",
    "reset_profile_preset",
    "export_profile_preset",
    "import_profile_preset",
    "apply_custom_profile",
    "open_profiles_folder",
    "set_win_key_locked",
    "set_fn_lock",
    "set_usb_charge",
    "set_ac_recovery",
    "toggle_bios_advanced_menu",
    "get_bios_advanced_menu_status",
    "get_autostart",
    "set_autostart",
    "set_autostart_enabled",
    "set_log_level",
    "set_log_filter",
    "get_log_path",
    "get_log_status",
    "open_log_file",
    "show_osd_window",
    "hide_osd_window",
    "trigger_osd_preview",
    "get_osd_config",
    "set_osd_config",
    "save_osd_config",
    "get_oem_status",
    "toggle_oem_service",
    "restore_official_control_center",
    "open_mini_drawer",
    "system.ping",
    "system.commands",
];

static API: OnceLock<Api> = OnceLock::new();

/// Builds (once) and hands back the process-wide backend.
fn api() -> Result<&'static Api, String> {
    if let Some(existing) = API.get() {
        return Ok(existing);
    }
    let (config, config_error) = crate::core::config::load_config();
    let state = AppState::new(config, create_hal(), AcpiDriver::new(), config_error);
    let _ = API.set(Api::new(state));
    API.get()
        .ok_or_else(|| "the backend could not be created".to_string())
}

// -- serialisation helpers -------------------------------------------------

/// Wraps a command result in the envelope and hands ownership to C.
fn respond<T: serde::Serialize>(result: Result<T, String>) -> *mut c_char {
    let envelope = match result {
        Ok(value) => match serde_json::to_value(value) {
            Ok(value) => serde_json::json!({ "ok": true, "data": value }),
            Err(error) => {
                serde_json::json!({ "ok": false, "error": format!("the result could not be serialised: {error}") })
            }
        },
        Err(message) => serde_json::json!({ "ok": false, "error": message }),
    };
    leak(envelope.to_string())
}

/// Pulls one named argument out of the JSON object, or explains what is missing.
fn take<T: DeserializeOwned>(args: &Value, key: &str) -> Result<T, String> {
    let raw = args
        .get(key)
        .ok_or_else(|| format!("missing argument `{key}`"))?;
    serde_json::from_value(raw.clone()).map_err(|error| format!("argument `{key}`: {error}"))
}

/// Moves a `String` to the C heap. The caller owns it until [`lumadesk_free`].
fn leak(text: String) -> *mut c_char {
    // A NUL cannot appear in the JSON we produce (serde escapes control
    // characters), but `CString::new` would panic rather than truncate, so the
    // interior NUL is stripped defensively instead.
    let cleaned: String = text.chars().filter(|c| *c != '\0').collect();
    match CString::new(cleaned) {
        Ok(value) => value.into_raw(),
        Err(_) => CString::new("{\"ok\":false,\"error\":\"the response contained a NUL byte\"}")
            .map(CString::into_raw)
            .unwrap_or(std::ptr::null_mut()),
    }
}

/// Borrows a caller-supplied C string.
///
/// # Safety
///
/// `ptr` must be `NULL` or point to a NUL-terminated string that stays alive for
/// the duration of the call.
unsafe fn borrow(ptr: *const c_char) -> Option<String> {
    if ptr.is_null() {
        return None;
    }
    Some(
        unsafe { CStr::from_ptr(ptr) }
            .to_string_lossy()
            .into_owned(),
    )
}

// -- the command table -----------------------------------------------------

/// Runs one [`Api`] method, deserialising every parameter from `args` by name.
///
/// The parameter names in the macro invocation *are* the JSON keys, so a typo
/// here is a compile error rather than a runtime "missing argument".
macro_rules! cmd {
    ($api:ident, $args:ident, $method:ident $(, $arg:ident : $ty:ty)* $(,)?) => {{
        $(
            let $arg: $ty = match take(&$args, stringify!($arg)) {
                Ok(value) => value,
                Err(message) => return respond::<()>(Err(message)),
            };
            // Keeps the binding live on the path where the call is skipped.
            let _ = &$arg;
        )*
        if dry_run() {
            return respond::<Value>(Ok(serde_json::json!({ "dry_run": true })));
        }
        respond($api.$method($($arg),*))
    }};
}

// Whether `cmd!` should validate its arguments but stop short of the call.
//
// Several commands open Notepad or a File Explorer window, and the exhaustive
// tests have to walk the whole table, so they turn this on first. Nothing else
// does, and outside `cfg(test)` it compiles away entirely.
#[cfg(test)]
thread_local! {
    static DRY_RUN: std::cell::Cell<bool> = const { std::cell::Cell::new(false) };
}

#[cfg(test)]
fn dry_run() -> bool {
    DRY_RUN.with(std::cell::Cell::get)
}

#[cfg(not(test))]
fn dry_run() -> bool {
    false
}

fn dispatch(api: &Api, command: &str, args: &Value) -> *mut c_char {
    match command {
        // -- status / configuration ---------------------------------------
        "get_hardware_status" => cmd!(api, args, get_hardware_status),
        "get_app_config" => cmd!(api, args, get_app_config),
        "save_app_config" => cmd!(api, args, save_app_config, cfg: AppConfig),
        "import_models_json" => cmd!(api, args, import_models_json, json_content: String),
        "export_models_json" => cmd!(api, args, export_models_json),
        "open_models_in_notepad" => cmd!(api, args, open_models_in_notepad),

        // -- power / performance ------------------------------------------
        "set_power_mode" => cmd!(api, args, set_power_mode, mode: u8),
        "set_windows_power_mode" => cmd!(api, args, set_windows_power_mode, mode: u8),
        "set_fan_boost" => cmd!(api, args, set_fan_boost, enabled: bool),
        "set_battery_limit" => cmd!(api, args, set_battery_limit, limit: u32),
        "set_battery_hardware_limit" => cmd!(api, args, set_battery_hardware_limit, limit: u32),
        "set_battery_mode" => cmd!(api, args, set_battery_mode, mode: String),
        "set_active_windows_power_scheme" => {
            cmd!(api, args, set_active_windows_power_scheme, guid: String)
        }
        "set_power_option" => cmd!(api, args, set_power_option, option: String, value: i64),
        "apply_live_custom_tweak" => cmd!(
            api, args, apply_live_custom_tweak,
            temp_target: f64,
            ctgp_enabled: bool,
            ctgp_watts: f64,
            db_enabled: bool,
            db_watts: f64,
            super_perf: bool,
            gpu_core_offset: f64,
            gpu_mem_offset: f64,
        ),
        "super_perf_enable" => cmd!(api, args, super_perf_enable, target_db_watts: f64),
        "heal_gpu_power_contract" => cmd!(api, args, heal_gpu_power_contract),
        "apply_fan_curve_live" => cmd!(
            api, args, apply_fan_curve_live,
            cpu_curve: Vec<CurvePoint>,
            gpu_curve: Vec<CurvePoint>,
        ),
        "toggle_fan_curve_control" => cmd!(api, args, toggle_fan_curve_control, enabled: bool),
        "toggle_fan_ramp_rate" => cmd!(api, args, toggle_fan_ramp_rate, speed_ms: u32),
        "toggle_fan_isolated_output" => cmd!(api, args, toggle_fan_isolated_output, enabled: bool),
        "set_gpu_mode" => cmd!(api, args, set_gpu_mode, mode: String),
        "get_gpu_mode_info" => cmd!(api, args, get_gpu_mode_info),
        "restart_system" => cmd!(api, args, restart_system),
        "get_power_settings" => cmd!(api, args, get_power_settings),
        "get_power_settings_detail" => cmd!(api, args, get_power_settings_detail),
        "get_windows_power_schemes" => cmd!(api, args, get_windows_power_schemes),
        "get_device_switches" => cmd!(api, args, get_device_switches),
        "set_device_switch" => cmd!(api, args, set_device_switch, id: String, enabled: bool),

        // -- sleep guard ---------------------------------------------------
        "set_sleep_auto_off" => cmd!(api, args, set_sleep_auto_off, enabled: bool),
        "set_master_sleep_guard" => cmd!(api, args, set_master_sleep_guard, enabled: bool),
        "get_sleep_guard_option" => cmd!(api, args, get_sleep_guard_option, option: String),

        // -- display -------------------------------------------------------
        "switch_refresh_rate" => cmd!(api, args, switch_refresh_rate, hz: u32),
        "set_display_monitor_refresh_rate" => cmd!(
            api, args, set_display_monitor_refresh_rate,
            device_name: String,
            hz: u32,
        ),
        "set_auto_min_refresh_on_battery" => {
            cmd!(api, args, set_auto_min_refresh_on_battery, enabled: bool)
        }
        "set_display_tuning_enabled" => cmd!(api, args, set_display_tuning_enabled, enabled: bool),
        "apply_display_color_preset" => cmd!(api, args, apply_display_color_preset, preset: String),
        "list_color_presets" => cmd!(api, args, list_color_presets),
        "import_color_preset_content" => {
            cmd!(api, args, import_color_preset_content, json_content: String)
        }
        "get_display_brightness" => cmd!(api, args, get_display_brightness),
        "set_display_brightness" => cmd!(api, args, set_display_brightness, level: u32),
        "get_display_tuning_state" => cmd!(api, args, get_display_tuning_state),
        "get_color_calibration_state" => cmd!(api, args, get_color_calibration_state),
        "set_color_calibration_enabled" => {
            cmd!(api, args, set_color_calibration_enabled, enabled: bool)
        }
        "get_displays" => cmd!(api, args, get_displays),
        "get_display_settings" => cmd!(api, args, get_display_settings),

        // -- lighting ------------------------------------------------------
        "get_lighting_state" => cmd!(api, args, get_lighting_state),
        "get_lighting_status" => cmd!(api, args, get_lighting_status),
        "get_lighting_runtime_status" => cmd!(api, args, get_lighting_runtime_status),
        "get_lighting_runtime" => cmd!(api, args, get_lighting_runtime),
        "get_keyboard_hardware_info" => cmd!(api, args, get_keyboard_hardware_info),
        "apply_keyboard_lighting" => {
            cmd!(api, args, apply_keyboard_lighting, lighting: LightingState)
        }
        "apply_logo_lighting" => cmd!(api, args, apply_logo_lighting, effect: u32, color: String),
        "apply_hinge_lighting" => cmd!(api, args, apply_hinge_lighting, speed: u32, color: String),
        "apply_lightbar_lighting" => {
            cmd!(api, args, apply_lightbar_lighting, effect: u32, color: String)
        }
        "apply_four_zone_colors" => cmd!(
            api, args, apply_four_zone_colors,
            zone1: String,
            zone2: String,
            zone3: String,
            zone4: String,
        ),
        "set_lighting_sleep_timer" => cmd!(api, args, set_lighting_sleep_timer, minutes: u32),
        "trigger_lighting_welcome" => cmd!(api, args, trigger_lighting_welcome),
        "set_keyboard_engine" => cmd!(api, args, set_keyboard_engine, engine: String),
        "set_streamer_fps" => cmd!(api, args, set_streamer_fps, fps: u32),
        "set_custom_fx_text" => cmd!(api, args, set_custom_fx_text, text: String),
        "set_auto_fallback_kb_on_battery" => {
            cmd!(api, args, set_auto_fallback_kb_on_battery, enabled: bool)
        }
        "list_custom_brfx_scripts" => cmd!(api, args, list_custom_brfx_scripts),
        "get_custom_brfx_list" => cmd!(api, args, get_custom_brfx_list),
        "apply_custom_brfx_script" => cmd!(api, args, apply_custom_brfx_script, id: String),
        "save_custom_brfx" => cmd!(api, args, save_custom_brfx, name: String, source: String),
        "open_brfx_in_notepad" => cmd!(api, args, open_brfx_in_notepad, filename: String),

        // -- water cooler --------------------------------------------------
        "get_water_cooler_status" => cmd!(api, args, get_water_cooler_status),
        "get_water_cooler_reason" => cmd!(api, args, get_water_cooler_reason),
        "set_water_cooler_enabled" => cmd!(api, args, set_water_cooler_enabled, enabled: bool),
        "apply_cooler_strategy" => cmd!(
            api, args, apply_cooler_strategy,
            strategy: CoolerStrategy,
            points: Vec<CurvePoint>,
        ),
        "set_water_cooler_speed" => cmd!(api, args, set_water_cooler_speed, duty: f64),
        "set_water_cooler_led" => cmd!(api, args, set_water_cooler_led, color: String, effect: u32),
        "set_water_cooler_strategy" => {
            cmd!(api, args, set_water_cooler_strategy, strategy: CoolerStrategy)
        }
        "reset_water_cooler" => cmd!(api, args, reset_water_cooler),

        // -- profiles ------------------------------------------------------
        "list_profile_presets" => cmd!(api, args, list_profile_presets),
        "get_profiles_list" => cmd!(api, args, get_profiles_list),
        "get_default_builtin_presets" => cmd!(api, args, get_default_builtin_presets),
        "create_profile_preset" => {
            cmd!(api, args, create_profile_preset, name: String, base_id: String)
        }
        "save_profile_preset" => cmd!(api, args, save_profile_preset, preset: ProfilePreset),
        "delete_profile_preset" => cmd!(api, args, delete_profile_preset, id: String),
        "rename_profile_preset" => {
            cmd!(api, args, rename_profile_preset, id: String, new_name: String)
        }
        "set_active_profile_id" => cmd!(api, args, set_active_profile_id, id: String),
        "get_active_profile_id" => cmd!(api, args, get_active_profile_id),
        "reset_profile_preset" => cmd!(api, args, reset_profile_preset, id: String),
        "export_profile_preset" => cmd!(api, args, export_profile_preset, id: String),
        "import_profile_preset" => cmd!(api, args, import_profile_preset, json_content: String),
        "apply_custom_profile" => cmd!(api, args, apply_custom_profile, preset: ProfilePreset),
        "open_profiles_folder" => cmd!(api, args, open_profiles_folder),

        // -- firmware switches ---------------------------------------------
        "set_win_key_locked" => cmd!(api, args, set_win_key_locked, locked: bool),
        "set_fn_lock" => cmd!(api, args, set_fn_lock, enabled: bool),
        "set_usb_charge" => cmd!(api, args, set_usb_charge, enabled: bool),
        "set_ac_recovery" => cmd!(api, args, set_ac_recovery, enabled: bool),
        "toggle_bios_advanced_menu" => cmd!(api, args, toggle_bios_advanced_menu, enable: bool),
        "get_bios_advanced_menu_status" => cmd!(api, args, get_bios_advanced_menu_status),

        // -- autostart -----------------------------------------------------
        "get_autostart" => cmd!(api, args, get_autostart),
        "set_autostart" => cmd!(api, args, set_autostart, enabled: bool),
        "set_autostart_enabled" => cmd!(api, args, set_autostart_enabled, enabled: bool),

        // -- logging / OSD -------------------------------------------------
        "set_log_level" => cmd!(api, args, set_log_level, level: String),
        "set_log_filter" => cmd!(api, args, set_log_filter, filter: String),
        "get_log_path" => cmd!(api, args, get_log_path),
        "get_log_status" => cmd!(api, args, get_log_status),
        "open_log_file" => cmd!(api, args, open_log_file),
        "show_osd_window" => cmd!(api, args, show_osd_window),
        "hide_osd_window" => cmd!(api, args, hide_osd_window),
        "trigger_osd_preview" => cmd!(api, args, trigger_osd_preview, kind: String),
        "get_osd_config" => cmd!(api, args, get_osd_config),
        "set_osd_config" => cmd!(api, args, set_osd_config, config: OsdConfig),
        "save_osd_config" => cmd!(api, args, save_osd_config, config: OsdConfig),

        // -- OEM -----------------------------------------------------------
        "get_oem_status" => cmd!(api, args, get_oem_status),
        "toggle_oem_service" => cmd!(api, args, toggle_oem_service, enable: bool),
        "restore_official_control_center" => cmd!(api, args, restore_official_control_center),
        "open_mini_drawer" => cmd!(api, args, open_mini_drawer),

        // -- self-description ----------------------------------------------
        // These two let a front end confirm it is talking to a compatible
        // backend before it issues anything that touches hardware.
        "system.ping" => respond::<Value>(Ok(serde_json::json!({
            "pong": true,
            "abi_version": ABI_VERSION,
            "version": env!("CARGO_PKG_VERSION"),
        }))),
        "system.commands" => respond(Ok(COMMANDS.to_vec())),

        other => respond::<()>(Err(format!("unknown command `{other}`"))),
    }
}

// -- the exported surface --------------------------------------------------

/// Version of the envelope/argument convention. See [`ABI_VERSION`].
#[no_mangle]
pub extern "C" fn lumadesk_abi_version() -> u32 {
    ABI_VERSION
}

/// Builds the backend and returns the bootstrap payload.
///
/// `{"ok":true,"data":{"config":<AppConfig>,"config_error":<string|null>,
/// "backend":"<windows|mock>","elevated":<bool>}}`
///
/// Safe to call more than once; later calls just re-report the same state.
#[no_mangle]
pub extern "C" fn lumadesk_init() -> *mut c_char {
    let api = match api() {
        Ok(api) => api,
        Err(message) => return respond::<()>(Err(message)),
    };
    let state = api.state();
    let config = state.config();
    let config_error = state.config_error();
    let backend = api
        .get_hardware_status()
        .map(|status| {
            if status.elevated {
                "windows (elevated)"
            } else {
                "windows"
            }
        })
        .unwrap_or("unavailable");
    respond(Ok(serde_json::json!({
        "config": config,
        "config_error": config_error,
        "backend": backend,
        "abi_version": ABI_VERSION,
    })))
}

/// Runs one command.
///
/// * `command` —NUL-terminated UTF-8 command name, never `NULL`.
/// * `payload` —NUL-terminated UTF-8 JSON object of arguments, or `NULL` when
///   the command takes none.
///
/// # Safety
///
/// Both pointers must be `NULL` or valid NUL-terminated C strings.
#[no_mangle]
pub unsafe extern "C" fn lumadesk_call(
    command: *const c_char,
    payload: *const c_char,
) -> *mut c_char {
    let Some(command) = (unsafe { borrow(command) }) else {
        return respond::<()>(Err("no command was given".to_string()));
    };
    let api = match api() {
        Ok(api) => api,
        Err(message) => return respond::<()>(Err(message)),
    };
    let args = match unsafe { borrow(payload) } {
        Some(text) if !text.trim().is_empty() => match serde_json::from_str::<Value>(&text) {
            Ok(value) => value,
            Err(error) => {
                return respond::<()>(Err(format!("the arguments were not valid JSON: {error}")))
            }
        },
        _ => Value::Null,
    };
    dispatch(api, &command, &args)
}

/// Empties the backend's event queue and returns it as a JSON array.
///
/// `{"ok":true,"data":[{"name":"osd://show","payload":{...}}, ...]}`
#[no_mangle]
pub extern "C" fn lumadesk_drain_events() -> *mut c_char {
    match api() {
        Ok(api) => respond(Ok(api.state().drain_events())),
        Err(message) => respond::<()>(Err(message)),
    }
}

/// Releases a string this library returned.
///
/// # Safety
///
/// `ptr` must be `NULL` or a pointer previously returned by one of the
/// functions above, and must not be freed twice.
#[no_mangle]
pub unsafe extern "C" fn lumadesk_free(ptr: *mut c_char) {
    if ptr.is_null() {
        return;
    }
    drop(unsafe { CString::from_raw(ptr) });
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn take_reports_a_missing_argument_by_name() {
        let args = serde_json::json!({ "mode": 3 });
        let ok: u8 = take(&args, "mode").expect("mode is present");
        assert_eq!(ok, 3);
        let err = take::<u8>(&args, "hz").expect_err("hz is absent");
        assert!(
            err.contains("hz"),
            "the message must name the argument: {err}"
        );
    }

    #[test]
    fn take_reports_the_argument_that_failed_to_parse() {
        let args = serde_json::json!({ "mode": "turbo" });
        let err = take::<u8>(&args, "mode").expect_err("a string is not a u8");
        assert!(
            err.starts_with("argument `mode`"),
            "unexpected message: {err}"
        );
    }

    #[test]
    fn take_deserialises_a_struct_from_its_object() {
        let args = serde_json::json!({ "point": { "temp": 60.0, "duty": 40.0, "volt": 0 } });
        let point: CurvePoint = take(&args, "point").expect("an object deserialises");
        assert_eq!(point.temp, 60.0);
        assert_eq!(point.duty, 40.0);
    }

    #[test]
    fn take_deserialises_a_sequence_of_structs() {
        let args = serde_json::json!({
            "points": [
                { "temp": 40.0, "duty": 20.0, "volt": 0 },
                { "temp": 80.0, "duty": 90.0, "volt": 0 }
            ]
        });
        let points: Vec<CurvePoint> = take(&args, "points").expect("an array deserialises");
        assert_eq!(points.len(), 2);
    }

    #[test]
    fn take_reads_an_enum_from_its_wire_name() {
        let args = serde_json::json!({ "strategy": "silent" });
        let strategy: CoolerStrategy = take(&args, "strategy").expect("silent is a strategy");
        assert_eq!(strategy, CoolerStrategy::Silent);
    }

    /// Forces the hardware simulator before the lazy `Api` is built.
    ///
    /// `API` is a `OnceLock`, so whichever test wins the race fixes the backend
    /// for the whole process; the real HAL would poke at hardware that is not
    /// present on a build machine.
    fn ensure_mock() {
        static ONCE: std::sync::Once = std::sync::Once::new();
        ONCE.call_once(|| std::env::set_var("JIYAOCHU_FORCE_MOCK", "1"));
    }

    /// Calls the ABI, copies the envelope out, and releases it exactly once.
    fn envelope(command: &std::ffi::CStr, payload: Option<&std::ffi::CStr>) -> Value {
        ensure_mock();
        let payload = payload.map_or(std::ptr::null(), |value| value.as_ptr());
        let raw = unsafe { lumadesk_call(command.as_ptr(), payload) };
        let text = unsafe { borrow(raw) }.expect("every call produces a response");
        unsafe { lumadesk_free(raw) };
        serde_json::from_str(&text).unwrap_or_else(|error| panic!("not JSON ({error}): {text}"))
    }

    /// Validates arguments without letting the backend touch the outside world.
    ///
    /// Walking the whole command table reaches `open_models_in_notepad`,
    /// `open_log_file`, `open_profiles_folder` and friends; without this the
    /// suite opens Notepad windows and hangs waiting for them.
    struct DryRun;

    impl DryRun {
        fn new() -> Self {
            DRY_RUN.with(|flag| {
                assert!(!flag.get(), "a dry run is already active on this thread");
                flag.set(true);
            });
            DryRun
        }
    }

    impl Drop for DryRun {
        fn drop(&mut self) {
            DRY_RUN.with(|flag| flag.set(false));
        }
    }

    #[test]
    fn an_unknown_command_is_an_error_envelope_not_a_panic() {
        let value = envelope(c"no_such_command", None);
        assert_eq!(value["ok"], Value::Bool(false));
        let message = value["error"].as_str().unwrap_or_default();
        assert!(
            message.contains("no_such_command"),
            "the error must echo the command: {message}"
        );
    }

    #[test]
    fn a_null_command_is_rejected() {
        let raw = unsafe { lumadesk_call(std::ptr::null(), std::ptr::null()) };
        let text = unsafe { borrow(raw) }.expect("a response is always produced");
        unsafe { lumadesk_free(raw) };
        assert!(text.contains("\"ok\":false"), "unexpected response: {text}");
    }

    #[test]
    fn malformed_arguments_are_reported_as_json_errors() {
        let value = envelope(c"get_hardware_status", Some(c"{not json"));
        assert_eq!(value["ok"], Value::Bool(false));
        let message = value["error"].as_str().unwrap_or_default();
        assert!(
            message.contains("not valid JSON"),
            "unexpected response: {message}"
        );
    }

    #[test]
    fn the_envelope_always_carries_ok() {
        let value = envelope(c"get_log_path", None);
        assert!(value.get("ok").is_some(), "no `ok` in {value}");
        assert!(
            value["ok"] == Value::Bool(true) || value.get("error").is_some(),
            "a failure must carry `error`: {value}"
        );
    }

    #[test]
    fn a_command_with_missing_arguments_names_them() {
        let value = envelope(c"set_power_mode", Some(c"{}"));
        assert_eq!(value["ok"], Value::Bool(false));
        let message = value["error"].as_str().unwrap_or_default();
        assert!(
            message.contains("mode"),
            "the error must name the argument: {message}"
        );
    }

    #[test]
    fn free_accepts_null() {
        unsafe { lumadesk_free(std::ptr::null_mut()) };
    }

    #[test]
    fn the_abi_version_is_exported() {
        assert_eq!(lumadesk_abi_version(), ABI_VERSION);
        assert!(lumadesk_abi_version() >= 1);
    }

    /// The published command list and the dispatcher must not drift apart.
    ///
    /// A command that is advertised but not implemented reaches the C# caller as
    /// a confusing "unknown command"; a command that is implemented but not
    /// advertised is invisible to the generated wrapper. This walks the whole
    /// list with empty arguments and requires every one of them to get past the
    /// match —either succeeding or failing on a *missing argument*, never on
    /// the name itself.
    #[test]
    fn every_advertised_command_is_dispatched() {
        let _dry = DryRun::new();
        let mut unimplemented = Vec::new();

        for &name in COMMANDS {
            let command = std::ffi::CString::new(name).expect("no NUL in a command name");
            let value = envelope(&command, Some(c"{}"));
            let message = value["error"].as_str().unwrap_or_default();
            if message.starts_with("unknown command") {
                unimplemented.push(name.to_string());
            }
        }

        assert!(
            unimplemented.is_empty(),
            "advertised but not dispatched: {unimplemented:?}"
        );
    }

    /// Every advertised command must also survive a call with no arguments at
    /// all, which is what a careless front end sends first.
    #[test]
    fn no_command_panics_without_arguments() {
        let _dry = DryRun::new();
        for &name in COMMANDS {
            let command = std::ffi::CString::new(name).expect("no NUL in a command name");
            let value = envelope(&command, None);
            assert!(
                value.get("ok").is_some(),
                "{name} did not produce an envelope: {value}"
            );
        }
    }

    #[test]
    fn the_command_list_has_no_duplicates() {
        let mut seen = std::collections::BTreeSet::new();
        for name in COMMANDS {
            assert!(seen.insert(*name), "`{name}` is listed twice in COMMANDS");
        }
    }

    #[test]
    fn ping_reports_the_abi_version() {
        let value = envelope(c"system.ping", None);
        assert_eq!(value["ok"], Value::Bool(true), "{value}");
        assert_eq!(value["data"]["abi_version"], Value::from(ABI_VERSION));
    }
}
