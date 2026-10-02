//! LumaDesk backend library and C ABI for the WinUI application.

pub mod core;
pub mod ffi;

pub use core::{AcpiDriver, HalError, HalResult, HardwareHal};
pub use core::{Api, AppState, HardwareStatus};
