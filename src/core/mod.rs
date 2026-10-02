//! LumaDesk hardware, configuration and application services.
//! The WinUI shell and CLI share the same command dispatcher and event queue.

#![allow(dead_code)]

pub mod api;
pub mod autostart;
pub mod config;
pub mod driver;
pub mod error;
pub mod events;
pub mod hal;
pub mod services;
pub mod state;

#[allow(unused_imports)]
pub use api::{Api, WATER_COOLER_BLE_REASON};
#[allow(unused_imports)]
pub use config::{AppConfig, HardwareStatus};
#[allow(unused_imports)]
pub use driver::{AcpiDriver, AcpiStatus};
#[allow(unused_imports)]
pub use error::{HalError, HalResult};
#[allow(unused_imports)]
pub use events::Event;
#[allow(unused_imports)]
pub use hal::{create_hal, HardwareHal};
#[allow(unused_imports)]
pub use state::AppState;
