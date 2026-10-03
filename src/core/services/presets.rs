//! Profile presets, display colour presets and BRFX custom lighting scripts.
//!
//! All three are files under `%APPDATA%\JiYaoChu`:
//!
//!   * `profiles/*.json` — user profile presets (one [`ProfilePreset`] each),
//!   * colour presets live in the code (they are gamma-ramp gain triples; see
//!     [`crate::core::hal::HardwareHal::apply_display_color_preset`]),
//!   * `brfx/*.brfx` — the "BetterRGB" custom effect scripts, which the user
//!     edits in Notepad.
//!
//! The built-in profile presets are generated from the machine's factory specs
//! in `models.json` rather than hard-coded, so a machine the catalogue knows
//! about automatically gets sensible Office / Balanced / Beast presets.

use crate::core::config::{AppConfig, CurvePoint, PowerModeId};
use crate::core::error::{HalError, HalResult};
use crate::core::hal::{ColorPreset, ProfilePreset};
use crate::core::services::models::ModelCatalogue;
use std::collections::BTreeMap;
use std::path::{Path, PathBuf};

// ---------------------------------------------------------------------------
// Built-in profile presets
// ---------------------------------------------------------------------------

/// The four built-in profile presets every machine gets.
///
/// The numbers follow the documented factory example (office 45 W, balanced
/// 75 W, turbo 205 W with the 200 W PL4 ceiling). `custom` starts from the
/// balanced values and is meant to be edited.
pub fn builtin_presets(catalogue: &ModelCatalogue) -> Vec<ProfilePreset> {
    let local = catalogue.local();
    let project = local.model_id.clone();
    let specs = &local.factory_specs;

    let curve = default_fan_curve();

    vec![
        ProfilePreset {
            id: "builtin_office".into(),
            name: "Office".into(),
            builtin: true,
            base_project: project.clone(),
            power_mode: 0,
            cpu_pl1: specs.office.pl1,
            cpu_pl2: specs.office.pl2,
            gpu_tgp: (local.base_tgp * 0.35).round(),
            gpu_db: 0.0,
            fan_curve: quiet_curve(),
        },
        ProfilePreset {
            id: "builtin_balanced".into(),
            name: "Balanced".into(),
            builtin: true,
            base_project: project.clone(),
            power_mode: 1,
            cpu_pl1: specs.balanced.pl1,
            cpu_pl2: specs.balanced.pl2,
            gpu_tgp: local.base_tgp,
            gpu_db: local.max_db * 0.5,
            fan_curve: curve.clone(),
        },
        ProfilePreset {
            id: "builtin_beast".into(),
            name: "Beast".into(),
            builtin: true,
            base_project: project,
            power_mode: 2,
            cpu_pl1: specs.turbo.pl1,
            cpu_pl2: specs.turbo.pl2,
            gpu_tgp: (local.base_tgp * 1.2).min(250.0),
            gpu_db: local.max_db,
            fan_curve: aggressive_curve(),
        },
        ProfilePreset {
            id: "builtin_custom".into(),
            name: "Custom".into(),
            builtin: true,
            base_project: local.model_id.clone(),
            power_mode: 3,
            cpu_pl1: specs.balanced.pl1,
            cpu_pl2: specs.balanced.pl2,
            gpu_tgp: local.base_tgp,
            gpu_db: local.max_db * 0.5,
            fan_curve: curve,
        },
    ]
}

/// The same conservative curve `AppConfig::default()` uses.
pub fn default_fan_curve() -> Vec<CurvePoint> {
    vec![
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
    ]
}

fn quiet_curve() -> Vec<CurvePoint> {
    vec![
        CurvePoint {
            temp: 45.0,
            duty: 0.0,
            volt: 0,
        },
        CurvePoint {
            temp: 60.0,
            duty: 20.0,
            volt: 0,
        },
        CurvePoint {
            temp: 75.0,
            duty: 40.0,
            volt: 0,
        },
        CurvePoint {
            temp: 88.0,
            duty: 70.0,
            volt: 0,
        },
        CurvePoint {
            temp: 95.0,
            duty: 100.0,
            volt: 0,
        },
    ]
}

fn aggressive_curve() -> Vec<CurvePoint> {
    vec![
        CurvePoint {
            temp: 40.0,
            duty: 40.0,
            volt: 0,
        },
        CurvePoint {
            temp: 55.0,
            duty: 60.0,
            volt: 0,
        },
        CurvePoint {
            temp: 70.0,
            duty: 80.0,
            volt: 0,
        },
        CurvePoint {
            temp: 80.0,
            duty: 95.0,
            volt: 0,
        },
        CurvePoint {
            temp: 90.0,
            duty: 100.0,
            volt: 0,
        },
    ]
}

// ---------------------------------------------------------------------------
// Profile file store
// ---------------------------------------------------------------------------

/// Directory holding the user's exported profile files.
pub fn profiles_dir() -> PathBuf {
    crate::core::config::profiles_dir()
}

/// A profile preset plus the file it came from (for user presets).
#[derive(Debug, Clone, PartialEq)]
pub struct StoredProfile {
    pub preset: ProfilePreset,
    /// `None` for built-ins, which are regenerated rather than stored.
    pub path: Option<PathBuf>,
}

impl StoredProfile {
    fn builtin(preset: ProfilePreset) -> Self {
        StoredProfile { preset, path: None }
    }

    fn user(preset: ProfilePreset, path: PathBuf) -> Self {
        StoredProfile {
            preset,
            path: Some(path),
        }
    }
}

/// Loads the built-ins plus every user profile file, in a stable order.
pub fn load_profiles(catalogue: &ModelCatalogue) -> (Vec<StoredProfile>, Vec<String>) {
    let mut warnings = Vec::new();
    let mut out: Vec<StoredProfile> = builtin_presets(catalogue)
        .into_iter()
        .map(StoredProfile::builtin)
        .collect();

    let dir = profiles_dir();
    if !dir.exists() {
        return (out, warnings);
    }

    let mut entries: Vec<PathBuf> = match std::fs::read_dir(&dir) {
        Ok(reader) => reader
            .flatten()
            .map(|entry| entry.path())
            .filter(|path| {
                path.extension()
                    .map(|extension| extension.eq_ignore_ascii_case("json"))
                    .unwrap_or(false)
            })
            .collect(),
        Err(error) => {
            warnings.push(format!("{} could not be listed: {error}", dir.display()));
            return (out, warnings);
        }
    };
    entries.sort();

    for path in entries {
        match read_profile(&path) {
            Ok(mut preset) => {
                // A file can never masquerade as one of the built-ins.
                if preset.id.trim().is_empty() {
                    preset.id = path
                        .file_stem()
                        .map(|stem| stem.to_string_lossy().to_string())
                        .unwrap_or_else(|| "profile".into());
                }
                preset.builtin = false;
                if out.iter().any(|stored| stored.preset.id == preset.id) {
                    warnings.push(format!(
                        "{} has the id '{}' which is already in use; the file was loaded as '{}-file'",
                        path.display(),
                        preset.id,
                        preset.id
                    ));
                    preset.id = format!("{}-file", preset.id);
                }
                out.push(StoredProfile::user(preset, path));
            }
            Err(error) => warnings.push(format!("{} could not be read: {error}", path.display())),
        }
    }

    (out, warnings)
}

fn read_profile(path: &Path) -> HalResult<ProfilePreset> {
    let text = std::fs::read_to_string(path)?;
    let mut preset: ProfilePreset = serde_json::from_str(&text)
        .map_err(|error| HalError::io(format!("profile JSON is not valid: {error}")))?;
    sanitise_profile(&mut preset);
    Ok(preset)
}

/// Forces a profile into the documented safe ranges.
pub fn sanitise_profile(preset: &mut ProfilePreset) {
    preset.power_mode = preset.power_mode.min(3);
    // The same PL1/PL2 envelope the model catalogue uses.
    preset.cpu_pl1 = preset.cpu_pl1.clamp(15.0, 220.0);
    preset.cpu_pl2 = preset.cpu_pl2.clamp(15.0, 250.0);
    preset.gpu_tgp = preset.gpu_tgp.clamp(15.0, 250.0);
    preset.gpu_db = preset.gpu_db.clamp(0.0, 80.0);
    if preset.id.trim().is_empty() {
        preset.id = format!("user_{}", crate::core::services::logging::unix_seconds());
    }
    if preset.name.trim().is_empty() {
        preset.name = preset.id.clone();
    }
    for point in preset.fan_curve.iter_mut() {
        point.temp = point.temp.clamp(0.0, 120.0);
        point.duty = point.duty.clamp(0.0, 100.0);
        point.volt = match point.volt {
            7 | 8 | 11 => point.volt,
            _ => 0,
        };
    }
    preset.fan_curve.sort_by(|a, b| {
        a.temp
            .partial_cmp(&b.temp)
            .unwrap_or(std::cmp::Ordering::Equal)
    });
}

/// Writes a profile file, returning the path it went to.
pub fn save_profile(preset: &ProfilePreset) -> HalResult<PathBuf> {
    let mut preset = preset.clone();
    preset.builtin = false;
    if preset.id.trim().is_empty() {
        return Err(HalError::io(
            "a profile preset needs an id before it can be saved",
        ));
    }
    sanitise_profile(&mut preset);

    let dir = profiles_dir();
    crate::core::config::ensure_dir(&dir)?;
    let path = dir.join(format!("{}.json", safe_file_stem(&preset.id)));
    let text = serde_json::to_string_pretty(&preset)
        .map_err(|error| HalError::io(format!("the profile could not be encoded: {error}")))?;
    std::fs::write(&path, text)?;
    Ok(path)
}

/// Removes a user profile file. Built-ins cannot be deleted.
pub fn delete_profile(catalogue: &ModelCatalogue, id: &str) -> HalResult<()> {
    if builtin_presets(catalogue)
        .iter()
        .any(|preset| preset.id == id)
    {
        return Err(HalError::unsupported(format!(
            "'{id}' is a built-in preset; it can be reset but not deleted"
        )));
    }
    let path = profiles_dir().join(format!("{}.json", safe_file_stem(id)));
    if !path.exists() {
        return Err(HalError::io(format!(
            "no profile preset named '{id}' exists"
        )));
    }
    std::fs::remove_file(&path)?;
    Ok(())
}

/// Resets a profile: built-ins are returned regenerated, user profiles are
/// rewritten from the matching built-in when one exists.
pub fn reset_profile(catalogue: &ModelCatalogue, id: &str) -> HalResult<ProfilePreset> {
    let builtins = builtin_presets(catalogue);
    if let Some(preset) = builtins.iter().find(|preset| preset.id == id) {
        return Ok(preset.clone());
    }

    // A user profile derived from a built-in (`base_project` match) resets to
    // that built-in's tuning but keeps its own identity.
    let stored = load_profiles(catalogue).0;
    let Some(existing) = stored
        .into_iter()
        .find(|profile| profile.preset.id == id)
        .map(|profile| profile.preset)
    else {
        return Err(HalError::io(format!(
            "no profile preset named '{id}' exists"
        )));
    };

    let base = builtins
        .iter()
        .find(|preset| preset.base_project == existing.base_project)
        .cloned()
        .unwrap_or_else(|| builtins[1].clone());

    let reset = ProfilePreset {
        id: existing.id,
        name: existing.name,
        builtin: false,
        base_project: existing.base_project,
        ..base
    };
    save_profile(&reset)?;
    Ok(reset)
}

/// Makes a file-name-safe stem out of an arbitrary id.
fn safe_file_stem(id: &str) -> String {
    let mut out = String::new();
    for character in id.chars() {
        if character.is_ascii_alphanumeric() || character == '-' || character == '_' {
            out.push(character);
        } else {
            out.push('_');
        }
    }
    if out.is_empty() {
        "profile".into()
    } else {
        out
    }
}

// ---------------------------------------------------------------------------
// Colour presets
// ---------------------------------------------------------------------------

/// The display colour presets shipped with the app.
///
/// The values are per-channel gain percentages (`red`, `green`, `blue`) plus a
/// nominal colour temperature; the gamma ramp applies the gains and the UI shows
/// the rest as descriptive metadata.
pub fn color_presets() -> Vec<ColorPreset> {
    let preset = |id: &str, name: &str, red: &str, green: &str, blue: &str, kelvin: &str| {
        let mut values = BTreeMap::new();
        values.insert("red".to_string(), red.to_string());
        values.insert("green".to_string(), green.to_string());
        values.insert("blue".to_string(), blue.to_string());
        values.insert("kelvin".to_string(), kelvin.to_string());
        ColorPreset {
            id: id.to_string(),
            name: name.to_string(),
            builtin: true,
            values,
        }
    };

    vec![
        preset("neutral", "Neutral", "100", "100", "100", "6500"),
        preset("srgb", "sRGB", "98", "100", "98", "6500"),
        preset("warm", "Warm", "100", "92", "82", "5000"),
        preset("cool", "Cool", "92", "96", "100", "7500"),
        preset("vivid", "Vivid", "108", "100", "104", "6800"),
        preset("reading", "Reading", "100", "95", "88", "5500"),
    ]
}

/// Directory holding user-imported colour presets.
pub fn colors_dir() -> PathBuf {
    crate::core::config::data_dir().join("colors")
}

/// Built-ins plus any colour presets the user imported.
pub fn all_color_presets() -> (Vec<ColorPreset>, Vec<String>) {
    let mut warnings = Vec::new();
    let mut out = color_presets();

    let dir = colors_dir();
    if !dir.exists() {
        return (out, warnings);
    }

    let mut entries: Vec<PathBuf> = match std::fs::read_dir(&dir) {
        Ok(reader) => reader
            .flatten()
            .map(|entry| entry.path())
            .filter(|path| {
                path.extension()
                    .is_some_and(|ext| ext.eq_ignore_ascii_case("json"))
            })
            .collect(),
        Err(error) => {
            warnings.push(format!("{} could not be listed: {error}", dir.display()));
            return (out, warnings);
        }
    };
    entries.sort();

    for path in entries {
        match std::fs::read_to_string(&path)
            .map_err(HalError::from)
            .and_then(|text| {
                serde_json::from_str::<ColorPreset>(&text).map_err(|error| {
                    HalError::io(format!("colour preset JSON is not valid: {error}"))
                })
            }) {
            Ok(mut preset) => {
                if preset.id.trim().is_empty() {
                    preset.id = path
                        .file_stem()
                        .map(|stem| stem.to_string_lossy().to_string())
                        .unwrap_or_else(|| "custom".into());
                }
                if out.iter().any(|existing| existing.id == preset.id) {
                    preset.id = format!("{}-custom", preset.id);
                }
                preset.builtin = false;
                out.push(preset);
            }
            Err(error) => warnings.push(format!("{} could not be read: {error}", path.display())),
        }
    }

    (out, warnings)
}

/// Imports a colour preset from JSON, returning the stored preset.
pub fn import_color_preset(json_content: &str) -> HalResult<ColorPreset> {
    let mut preset: ColorPreset = serde_json::from_str(json_content)
        .map_err(|error| HalError::io(format!("the colour preset is not valid JSON: {error}")))?;

    if preset.values.is_empty() {
        return Err(HalError::io("the colour preset has no values"));
    }
    if preset.id.trim().is_empty() {
        preset.id = format!("custom_{}", crate::core::services::logging::unix_seconds());
    }
    if preset.name.trim().is_empty() {
        preset.name = preset.id.clone();
    }
    preset.builtin = false;

    let dir = colors_dir();
    crate::core::config::ensure_dir(&dir)?;
    let path = dir.join(format!("{}.json", safe_file_stem(&preset.id)));
    let text = serde_json::to_string_pretty(&preset).map_err(|error| {
        HalError::io(format!("the colour preset could not be encoded: {error}"))
    })?;
    std::fs::write(path, text)?;
    Ok(preset)
}

// ---------------------------------------------------------------------------
// BRFX custom lighting scripts
// ---------------------------------------------------------------------------

/// The sample script written on first run so the editor is never empty.
pub const EXAMPLE_BRFX: &str = "\
# JiYaoChu BetterRGB script (brfx)
# Each line is `effect <name> <fps> <#rrggbb>` or `set <value>`.
# Lines starting with # are comments.
effect breathe 30 #ff00ff
";

/// Directory holding the BRFX scripts.
pub fn brfx_dir() -> PathBuf {
    crate::core::config::brfx_dir()
}

/// Ensures the directory exists and contains the sample script.
pub fn ensure_brfx_dir() -> HalResult<PathBuf> {
    let dir = brfx_dir();
    crate::core::config::ensure_dir(&dir)?;
    let example = dir.join("example.brfx");
    if !example.exists() {
        std::fs::write(&example, EXAMPLE_BRFX)?;
    }
    Ok(dir)
}

/// Lists the script file names (not full paths), sorted.
pub fn list_brfx() -> HalResult<Vec<String>> {
    let dir = ensure_brfx_dir()?;
    let mut out: Vec<String> = std::fs::read_dir(&dir)?
        .flatten()
        .map(|entry| entry.path())
        .filter(|path| path.is_file())
        .filter_map(|path| {
            path.file_name()
                .map(|name| name.to_string_lossy().to_string())
        })
        .collect();
    out.sort();
    Ok(out)
}

/// Reads a script by file name.
pub fn read_brfx(filename: &str) -> HalResult<String> {
    let path = script_path(filename)?;
    Ok(std::fs::read_to_string(path)?)
}

/// Writes a script, creating or replacing it.
pub fn save_brfx(name: &str, source: &str) -> HalResult<String> {
    let filename = normalise_script_name(name);
    let path = script_path(&filename)?;
    std::fs::write(path, source)?;
    Ok(filename)
}

/// Resolves a script file name inside the BRFX directory, refusing traversal.
fn script_path(filename: &str) -> HalResult<PathBuf> {
    let trimmed = filename.trim();
    if trimmed.is_empty() {
        return Err(HalError::io("a script file name is required"));
    }
    if trimmed.contains(['/', '\\']) || trimmed.contains("..") {
        return Err(HalError::unsupported(format!(
            "'{filename}' is not a plain script file name"
        )));
    }
    let dir = ensure_brfx_dir()?;
    Ok(dir.join(trimmed))
}

/// Public form of [`script_path`] for the "open in Notepad" command.
///
/// It deliberately goes through the same validation so the command cannot be
/// turned into an arbitrary-file opener.
pub fn script_file(filename: &str) -> HalResult<PathBuf> {
    let path = script_path(filename)?;
    if !path.exists() {
        return Err(HalError::unavailable(format!(
            "there is no script named '{filename}'"
        )));
    }
    Ok(path)
}

/// Guarantees a `.brfx` extension and a safe file name.
///
/// A name that already carries the extension must not acquire a second one, and
/// the extension has to be removed *before* sanitising: the sanitiser drops
/// every character outside `[A-Za-z0-9_-]`, so the dot never survives to be
/// tested for at the end.
fn normalise_script_name(name: &str) -> String {
    let trimmed = name.trim();
    let cut = trimmed.len().saturating_sub(5);
    let stem_source =
        if trimmed.is_char_boundary(cut) && trimmed[cut..].eq_ignore_ascii_case(".brfx") {
            &trimmed[..cut]
        } else {
            trimmed
        };

    let mut stem = String::new();
    for character in stem_source.chars() {
        if character.is_ascii_alphanumeric() || character == '-' || character == '_' {
            stem.push(character);
        } else if character == ' ' {
            stem.push('_');
        }
    }
    if stem.is_empty() {
        stem = format!("custom_{}", crate::core::services::logging::unix_seconds());
    }
    format!("{stem}.brfx")
}

// ---------------------------------------------------------------------------
// Device switches
// ---------------------------------------------------------------------------

/// The optional chassis devices the UI can list.
///
/// `supported` is false for everything that needs the vendor driver, which is
/// what makes the UI render them as unavailable instead of offering a switch
/// that does nothing.
pub fn device_switches(
    config: &AppConfig,
    win_key_supported: bool,
) -> Vec<crate::core::hal::DeviceSwitch> {
    let switch =
        |id: &str, name: &str, enabled: bool, supported: bool| crate::core::hal::DeviceSwitch {
            id: id.to_string(),
            name: name.to_string(),
            enabled,
            supported,
        };

    vec![
        switch(
            "usb_charge",
            "USB charging while off",
            config.usb_charge_enabled,
            false,
        ),
        switch(
            "ac_recovery",
            "Restore power state after AC loss",
            config.ac_recovery_enabled,
            false,
        ),
        switch("fn_lock", "Fn lock", config.fn_lock_enabled, false),
        switch(
            "win_key_lock",
            "Windows key lock",
            config.win_key_locked,
            win_key_supported,
        ),
        switch(
            "water_cooler",
            "Water cooler",
            config.water_cooler_enabled,
            false,
        ),
        switch("bios_advanced", "BIOS advanced menu", false, false),
    ]
}

/// Maps a device-switch id onto the config field it owns.
pub fn set_device_switch(config: &mut AppConfig, id: &str, enabled: bool) -> HalResult<()> {
    match id {
        "usb_charge" => config.usb_charge_enabled = enabled,
        "ac_recovery" => config.ac_recovery_enabled = enabled,
        "fn_lock" => config.fn_lock_enabled = enabled,
        "win_key_lock" => config.win_key_locked = enabled,
        "water_cooler" => config.water_cooler_enabled = enabled,
        "bios_advanced" => {
            // Nothing to persist: the BIOS menu state lives in the EC.
        }
        other => {
            return Err(HalError::unsupported(format!(
                "'{other}' is not a device switch this build knows"
            )))
        }
    }
    Ok(())
}

/// Power mode id for a preset, clamped to the documented 0..3 range.
pub fn preset_power_mode(preset: &ProfilePreset) -> PowerModeId {
    preset.power_mode.min(3)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn builtins_cover_the_documented_ids() {
        let catalogue = ModelCatalogue::default();
        let ids: Vec<String> = builtin_presets(&catalogue)
            .into_iter()
            .map(|preset| preset.id)
            .collect();
        assert!(ids.contains(&"builtin_office".to_string()));
        assert!(ids.contains(&"builtin_balanced".to_string()));
        assert!(ids.contains(&"builtin_beast".to_string()));
    }

    #[test]
    fn script_names_cannot_escape_the_directory() {
        assert!(script_path("../../etc/passwd").is_err());
        assert!(script_path("sub/dir.brfx").is_err());
    }

    #[test]
    fn script_names_get_an_extension() {
        assert_eq!(normalise_script_name("my effect"), "my_effect.brfx");
        assert_eq!(normalise_script_name("already.brfx"), "already.brfx");
        assert_eq!(normalise_script_name("Already.BRFX"), "Already.brfx");
        assert_eq!(normalise_script_name("plain"), "plain.brfx");
        // The extension check slices the string, so a multi-byte name is the
        // case that would panic on a non-boundary index.
        let multibyte = normalise_script_name("灯效");
        assert!(multibyte.ends_with(".brfx"), "{multibyte}");
    }
}
