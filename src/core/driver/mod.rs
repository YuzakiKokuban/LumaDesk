//! Hardware access: recovered UniWill EC transport, UEFI MUX variables, and NVML telemetry.

pub mod acpi;
pub mod nvml;
pub(crate) mod temperature;
pub mod uefi;

pub use acpi::{AcpiDriver, AcpiStatus};
pub(crate) mod battery;
pub mod keyboard;
pub mod performance;
pub(crate) mod rollback;
