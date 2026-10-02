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
        let snapshot = {
            let mut guard = match self.config.lock() {
                Ok(guard) => guard,
                Err(poisoned) => poisoned.into_inner(),
            };
            f(&mut guard);
            guard.sanitise();
            guard.clone()
        };
        crate::core::config::save_config(&snapshot)
            .map_err(|e| format!("could not save config: {e}"))
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
