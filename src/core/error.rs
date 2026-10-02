//! Typed hardware errors converted into messages at the command boundary.

use std::fmt;

/// Errors produced by the hardware abstraction layer and the services built on
/// top of it.
#[derive(Debug)]
pub enum HalError {
    /// The operation is not implemented on this build because the required
    /// hardware interface was never recovered (ACPI driver IOCTLs, EC access,
    /// RGB controller protocol, BLE water cooler, ...).
    ///
    /// This variant is *never* used as a silent no-op: the UI is expected to
    /// surface it so the user can see the feature is unavailable.
    Unsupported(String),

    /// The hardware is present and the protocol is known, but the device did not
    /// answer (e.g. the ACPI driver device object does not exist).
    Unavailable(String),

    /// A genuine, reportable failure.
    Io(String),
}

impl HalError {
    /// Convenience constructor for [`HalError::Unsupported`].
    pub fn unsupported(reason: impl fmt::Display) -> Self {
        HalError::Unsupported(reason.to_string())
    }

    /// Convenience constructor for [`HalError::Unavailable`].
    pub fn unavailable(reason: impl fmt::Display) -> Self {
        HalError::Unavailable(reason.to_string())
    }

    /// Convenience constructor for [`HalError::Io`].
    pub fn io(reason: impl fmt::Display) -> Self {
        HalError::Io(reason.to_string())
    }
}

impl fmt::Display for HalError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            HalError::Unsupported(reason) => write!(f, "not supported on this build: {reason}"),
            HalError::Unavailable(reason) => write!(f, "hardware unavailable: {reason}"),
            HalError::Io(reason) => write!(f, "{reason}"),
        }
    }
}

impl std::error::Error for HalError {}

impl From<std::io::Error> for HalError {
    fn from(value: std::io::Error) -> Self {
        HalError::Io(value.to_string())
    }
}

impl From<serde_json::Error> for HalError {
    fn from(value: serde_json::Error) -> Self {
        HalError::Io(format!("json error: {value}"))
    }
}

impl From<HalError> for String {
    fn from(value: HalError) -> Self {
        value.to_string()
    }
}

/// `Result` alias used throughout the crate's non-command layers.
pub type HalResult<T> = Result<T, HalError>;
