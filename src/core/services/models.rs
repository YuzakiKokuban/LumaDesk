//! The machine model catalogue: `%APPDATA%\JiYaoChu\models.json`.
//!
//! Recovered layout (see `docs/models_customization_guide.md`): the file is an
//! **array** of machines, and the entry for the local machine must be at index
//! 0. Each entry carries two layers:
//!
//!   * `factory_specs` — the read-only truth for that hardware revision,
//!   * `presets` — the user-editable copy that the UI writes to.
//!
//! Unknown keys are preserved verbatim (`serde_json::Value` passthrough fields),
//! so a catalogue written by a newer build survives a round trip through this
//! one. That matters because the UI has explicit 编辑 / 导出 / 导入 buttons and
//! users are expected to hand-edit the file.

use crate::core::error::{HalError, HalResult};
use serde::{Deserialize, Serialize};
use serde_json::Value;
use std::path::Path;

/// The three thermal presets every model carries.
#[derive(Debug, Clone, Default, PartialEq, Serialize, Deserialize)]
#[serde(default)]
pub struct PresetTuning {
    pub pl1: f64,
    pub pl2: f64,
    pub pl4: f64,
    pub temp_offset: f64,
}

impl PresetTuning {
    /// `office` for the factory example in the guide.
    pub fn office() -> Self {
        PresetTuning { pl1: 45.0, pl2: 45.0, pl4: 125.0, temp_offset: 15.0 }
    }

    /// `balanced` for the factory example in the guide.
    pub fn balanced() -> Self {
        PresetTuning { pl1: 75.0, pl2: 75.0, pl4: 125.0, temp_offset: 5.0 }
    }

    /// `turbo` for the factory example in the guide.
    pub fn turbo() -> Self {
        PresetTuning { pl1: 205.0, pl2: 205.0, pl4: 200.0, temp_offset: 5.0 }
    }

    /// Applies the documented safe ranges: PL1 15..220 W, PL2 15..250 W,
    /// PL4 60..250 W, temperature offset 0..30 (target = 100 − offset).
    pub fn sanitise(&mut self) {
        self.pl1 = self.pl1.clamp(15.0, 220.0);
        self.pl2 = self.pl2.clamp(15.0, 250.0);
        self.pl4 = self.pl4.clamp(60.0, 250.0);
        self.temp_offset = self.temp_offset.clamp(0.0, 30.0);
    }

    /// The temperature target these values imply (`100 − offset`).
    pub fn temp_target(&self) -> f64 {
        100.0 - self.temp_offset
    }
}

/// The three named presets of one model.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(default)]
pub struct ModelPresets {
    pub office: PresetTuning,
    pub balanced: PresetTuning,
    pub turbo: PresetTuning,
}

impl Default for ModelPresets {
    fn default() -> Self {
        ModelPresets {
            office: PresetTuning::office(),
            balanced: PresetTuning::balanced(),
            turbo: PresetTuning::turbo(),
        }
    }
}

impl ModelPresets {
    fn sanitise(&mut self) {
        self.office.sanitise();
        self.balanced.sanitise();
        self.turbo.sanitise();
    }
}

/// One machine in the catalogue.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(default)]
pub struct ModelEntry {
    /// True for the machine the app is running on. Must be index 0.
    pub mine: bool,
    pub model_id: String,
    /// The physical hardware name reported by the firmware. The guide is
    /// explicit that this is not user-editable.
    pub display_name: String,
    /// The user's nickname for this machine.
    pub alias: String,
    /// Read-only truth layer.
    pub factory_specs: ModelPresets,
    /// User-editable layer.
    pub presets: ModelPresets,
    pub base_tgp: f64,
    pub max_db: f64,
    /// Anything else the file carries, kept so a newer schema survives.
    #[serde(flatten)]
    pub extra: serde_json::Map<String, Value>,
}

impl Default for ModelEntry {
    fn default() -> Self {
        ModelEntry {
            mine: false,
            model_id: String::new(),
            display_name: String::new(),
            alias: String::new(),
            factory_specs: ModelPresets::default(),
            presets: ModelPresets::default(),
            base_tgp: 150.0,
            max_db: 25.0,
            extra: serde_json::Map::new(),
        }
    }
}

impl ModelEntry {
    /// A fresh entry for the machine we are running on.
    pub fn local(display_name: impl Into<String>) -> Self {
        let display_name = display_name.into();
        ModelEntry {
            mine: true,
            model_id: slugify(&display_name),
            alias: display_name.clone(),
            display_name,
            ..ModelEntry::default()
        }
    }

    fn sanitise(&mut self) {
        if self.display_name.trim().is_empty() {
            self.display_name = "Unknown".into();
        }
        if self.alias.trim().is_empty() {
            self.alias = self.display_name.clone();
        }
        if self.model_id.trim().is_empty() {
            self.model_id = slugify(&self.display_name);
        }
        self.base_tgp = self.base_tgp.clamp(15.0, 250.0);
        self.max_db = self.max_db.clamp(0.0, 80.0);
        self.factory_specs.sanitise();
        self.presets.sanitise();
    }
}

/// The whole catalogue.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(transparent)]
pub struct ModelCatalogue {
    pub models: Vec<ModelEntry>,
}

impl Default for ModelCatalogue {
    fn default() -> Self {
        ModelCatalogue { models: vec![ModelEntry::local("Unknown")] }
    }
}

impl ModelCatalogue {
    /// Loads the catalogue, creating a local-machine-only one on first run.
    ///
    /// A corrupt file is never fatal: the bad file is kept as `.bad` and the
    /// caller gets a default catalogue plus a warning.
    pub fn load() -> (Self, Option<String>) {
        let path = crate::core::config::models_path();
        let text = match std::fs::read_to_string(&path) {
            Ok(text) => text,
            Err(error) if error.kind() == std::io::ErrorKind::NotFound => {
                let catalogue = Self::default();
                let warning = match catalogue.save() {
                    Ok(()) => Some(format!(
                        "no machine catalogue existed; a new one was written to {}",
                        path.display()
                    )),
                    Err(save_error) => Some(format!(
                        "no machine catalogue existed and one could not be written to {}: \
                         {save_error}",
                        path.display()
                    )),
                };
                return (catalogue, warning);
            }
            Err(error) => {
                return (
                    Self::default(),
                    Some(format!("{} could not be read: {error}", path.display())),
                )
            }
        };

        match Self::parse(&text) {
            Ok(catalogue) => (catalogue, None),
            Err(error) => {
                let bad = path.with_extension("json.bad");
                let _ = std::fs::write(&bad, &text);
                (
                    Self::default(),
                    Some(format!(
                        "{} was not valid JSON ({error}); it was kept as {} and replaced with a \
                         fresh catalogue",
                        path.display(),
                        bad.display()
                    )),
                )
            }
        }
    }

    /// Parses a catalogue, enforcing the "local machine is index 0" rule.
    pub fn parse(text: &str) -> HalResult<Self> {
        let mut models: Vec<ModelEntry> = serde_json::from_str(text)
            .map_err(|error| HalError::io(format!("the machine catalogue is not valid: {error}")))?;

        if models.is_empty() {
            return Err(HalError::io(
                "the machine catalogue is empty; it must contain at least the local machine",
            ));
        }

        // Exactly one entry may be `mine`, and it must be first.
        let local_index = models.iter().position(|model| model.mine).unwrap_or(0);
        if local_index != 0 {
            let local = models.remove(local_index);
            models.insert(0, local);
        }
        models[0].mine = true;
        for model in models.iter_mut().skip(1) {
            model.mine = false;
            model.sanitise();
        }
        models[0].sanitise();

        Ok(ModelCatalogue { models })
    }

    /// Writes the catalogue back to `models.json`.
    pub fn save(&self) -> HalResult<()> {
        let path = crate::core::config::models_path();
        if let Some(parent) = path.parent() {
            crate::core::config::ensure_dir(parent)?;
        }
        let text = serde_json::to_string_pretty(self)
            .map_err(|error| HalError::io(format!("the machine catalogue could not be encoded: {error}")))?;
        let temporary = path.with_extension("json.tmp");
        std::fs::write(&temporary, text)?;
        std::fs::rename(&temporary, &path)?;
        Ok(())
    }

    /// The entry for the local machine.
    pub fn local(&self) -> &ModelEntry {
        self.models.first().unwrap_or_else(|| unreachable!("a catalogue always has one entry"))
    }

    /// Replaces the catalogue with the contents of `jsonContent`.
    pub fn import_json(&mut self, json_content: &str) -> HalResult<()> {
        let imported = Self::parse(json_content)?;
        *self = imported;
        Ok(())
    }

    /// Serialises the catalogue for the export button.
    pub fn export_json(&self) -> HalResult<String> {
        serde_json::to_string_pretty(self)
            .map_err(|error| HalError::io(format!("the machine catalogue could not be exported: {error}")))
    }
}

/// Builds a stable id out of a display name.
fn slugify(name: &str) -> String {
    let mut out = String::new();
    for character in name.chars() {
        if character.is_ascii_alphanumeric() {
            out.push(character.to_ascii_lowercase());
        } else if !out.ends_with('_') && !out.is_empty() {
            out.push('_');
        }
    }
    let trimmed = out.trim_matches('_').to_string();
    if trimmed.is_empty() {
        "unknown".to_string()
    } else {
        trimmed
    }
}

/// Reads a catalogue from an arbitrary path (used by the tests and the import
/// dialog's preview).
pub fn read_from(path: &Path) -> HalResult<ModelCatalogue> {
    let text = std::fs::read_to_string(path)?;
    ModelCatalogue::parse(&text)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn local_machine_is_moved_to_index_zero() {
        let text = r#"[
            {"mine": false, "display_name": "Other", "model_id": "o"},
            {"mine": true, "display_name": "Mine", "model_id": "m"}
        ]"#;
        let catalogue = ModelCatalogue::parse(text).expect("valid catalogue");
        assert_eq!(catalogue.models[0].display_name, "Mine");
        assert!(catalogue.models[0].mine);
        assert!(!catalogue.models[1].mine);
    }

    #[test]
    fn documented_ranges_are_enforced() {
        let mut tuning = PresetTuning { pl1: 999.0, pl2: 1.0, pl4: 10.0, temp_offset: 99.0 };
        tuning.sanitise();
        assert_eq!(tuning.pl1, 220.0);
        assert_eq!(tuning.pl2, 15.0);
        assert_eq!(tuning.pl4, 60.0);
        assert_eq!(tuning.temp_offset, 30.0);
        assert_eq!(tuning.temp_target(), 70.0);
    }

    #[test]
    fn unknown_keys_survive_a_round_trip() {
        let text = r#"[{"mine": true, "display_name": "Mine", "future_field": 7}]"#;
        let catalogue = ModelCatalogue::parse(text).expect("valid catalogue");
        let encoded = serde_json::to_string(&catalogue).expect("encodes");
        assert!(encoded.contains("future_field"));
    }
}
