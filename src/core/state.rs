//! Process-wide application state.

use crate::core::config::{AppConfig, HardwareStatus};
use crate::core::driver::acpi::AcpiDriver;
use crate::core::events::Event;
use crate::core::hal::HardwareHal;
use std::sync::{Arc, Mutex, RwLock};

pub struct AppState {
    /// Persisted configuration.
    config: Mutex<AppConfig>,
    /// Last telemetry snapshot; refreshed by the background poller.
    hardware: RwLock<HardwareStatus>,
    /// The active hardware backend (real on Windows, mock elsewhere).
    hal: Arc<dyn HardwareHal>,
    /// Cache of the ACPI driver probe result.
    acpi: AcpiDriver,
    /// Whether the OEM takeover is currently applied (mirrors
    /// `AppConfig::takeover_oem` but is authoritative at runtime).
    oem_taken_over: Mutex<bool>,
    /// The last configuration load error, surfaced to the UI if present.
    config_error: Option<String>,
    /// Notifications waiting for the shell, in the order they were raised.
    ///
    /// This is the native replacement for `AppHandle::emit`; see
    /// [`crate::core::events`].
    events: Mutex<Vec<Event>>,
}

impl AppState {
    pub fn new(
        config: AppConfig,
        hal: Arc<dyn HardwareHal>,
        acpi: AcpiDriver,
        config_error: Option<String>,
    ) -> Self {
        let takeover = config.takeover_oem;
        Self {
            config: Mutex::new(config),
            hardware: RwLock::new(HardwareStatus::default()),
            hal,
            acpi,
            oem_taken_over: Mutex::new(takeover),
            config_error,
            events: Mutex::new(Vec::new()),
        }
    }

    /// Clones the current configuration out of the lock.
    pub fn config(&self) -> AppConfig {
        match self.config.lock() {
            Ok(guard) => guard.clone(),
            Err(poisoned) => poisoned.into_inner().clone(),
        }
    }

    /// Mutates the configuration in place.
    ///
    /// The closure must not block: it runs while the config lock is held.
    pub fn with_config<R>(&self, f: impl FnOnce(&mut AppConfig) -> R) -> R {
        let mut guard = match self.config.lock() {
            Ok(guard) => guard,
            Err(poisoned) => poisoned.into_inner(),
        };
        f(&mut guard)
    }

    /// Mutates the config and immediately persists it.
    pub fn update_config(&self, f: impl FnOnce(&mut AppConfig)) -> Result<(), String> {
        self.update_config_at(&crate::core::config::config_path(), f)
            .map_err(|e| format!("could not save config: {e}"))
    }

    fn update_config_at(
        &self,
        path: &std::path::Path,
        f: impl FnOnce(&mut AppConfig),
    ) -> std::io::Result<()> {
        let mut guard = self.config.lock().unwrap_or_else(|e| e.into_inner());
        if let Some(parent) = path.parent() {
            std::fs::create_dir_all(parent)?;
        }
        // Desktop and CLI may update different settings concurrently. Merge
        // the requested change into the last committed file under an OS lock.
        let lock = std::fs::OpenOptions::new()
            .create(true)
            .truncate(false)
            .read(true)
            .write(true)
            .open(path.with_extension("lock"))?;
        lock.lock()?;
        let mut candidate = std::fs::read(path)
            .ok()
            .and_then(|bytes| serde_json::from_slice::<AppConfig>(&bytes).ok())
            .unwrap_or_else(|| guard.clone());
        f(&mut candidate);
        candidate.sanitise();
        let bytes = serde_json::to_vec_pretty(&candidate).map_err(std::io::Error::other)?;
        crate::core::config::write_atomic(path, &bytes)?;
        *guard = candidate;
        Ok(())
    }

    pub fn config_error(&self) -> Option<String> {
        self.config_error.clone()
    }

    /// Latest cached telemetry snapshot.
    pub fn hardware(&self) -> HardwareStatus {
        match self.hardware.read() {
            Ok(guard) => guard.clone(),
            Err(poisoned) => poisoned.into_inner().clone(),
        }
    }

    pub fn set_hardware(&self, status: HardwareStatus) {
        match self.hardware.write() {
            Ok(mut guard) => *guard = status,
            Err(poisoned) => *poisoned.into_inner() = status,
        }
    }

    pub fn hal(&self) -> &Arc<dyn HardwareHal> {
        &self.hal
    }

    pub fn acpi(&self) -> &AcpiDriver {
        &self.acpi
    }

    pub fn oem_taken_over(&self) -> bool {
        match self.oem_taken_over.lock() {
            Ok(guard) => *guard,
            Err(poisoned) => *poisoned.into_inner(),
        }
    }

    pub fn set_oem_taken_over(&self, value: bool) {
        match self.oem_taken_over.lock() {
            Ok(mut guard) => *guard = value,
            Err(poisoned) => *poisoned.into_inner() = value,
        }
    }

    pub fn push_event(&self, ev: Event) {
        match self.events.lock() {
            Ok(mut queue) => queue.push(ev),
            Err(poisoned) => poisoned.into_inner().push(ev),
        }
    }

    /// Takes everything queued since the last drain, oldest first.
    ///
    /// The queue is left empty; a shell that cannot process an event is
    /// responsible for keeping its own copy.
    pub fn drain_events(&self) -> Vec<Event> {
        match self.events.lock() {
            Ok(mut queue) => std::mem::take(&mut *queue),
            Err(poisoned) => std::mem::take(&mut *poisoned.into_inner()),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::path::PathBuf;
    fn test_path() -> PathBuf {
        std::env::temp_dir()
            .join(format!(
                "lumadesk-config-test-{}-{}",
                std::process::id(),
                std::time::SystemTime::now()
                    .duration_since(std::time::UNIX_EPOCH)
                    .unwrap()
                    .as_nanos()
            ))
            .join("config.json")
    }
    fn state() -> AppState {
        AppState::new(
            AppConfig::default(),
            Arc::new(crate::core::hal::mock::MockHal::new()),
            AcpiDriver::new(),
            None,
        )
    }
    #[test]
    fn failed_commit_preserves_memory_configuration() {
        let path = test_path();
        std::fs::create_dir_all(&path).unwrap(); // A directory cannot be replaced by the config file.
        let state = state();
        let original = state.config();
        assert!(state
            .update_config_at(&path, |cfg| cfg.autostart = true)
            .is_err());
        assert_eq!(state.config(), original);
        std::fs::remove_dir_all(path.parent().unwrap()).unwrap();
    }
    #[test]
    fn independent_states_merge_concurrent_settings_without_lost_updates() {
        let path = test_path();
        std::thread::scope(|scope| {
            let a = &path;
            scope.spawn(move || {
                let state = state();
                for _ in 0..30 {
                    state
                        .update_config_at(a, |cfg| cfg.autostart = true)
                        .unwrap();
                }
            });
            let b = &path;
            scope.spawn(move || {
                let state = state();
                for _ in 0..30 {
                    state
                        .update_config_at(b, |cfg| cfg.win_key_locked = true)
                        .unwrap();
                }
            });
        });
        let config: AppConfig = serde_json::from_slice(&std::fs::read(&path).unwrap()).unwrap();
        assert!(config.autostart && config.win_key_locked);
        std::fs::remove_dir_all(path.parent().unwrap()).unwrap();
    }
}
