//! Notifications queued by backend commands and drained by the WinUI shell.

/// One queued notification.
#[derive(Debug, Clone, serde::Serialize)]
pub struct Event {
    pub name: String,
    /// JSON payload, `Value::Null` when the event carries nothing.
    pub payload: serde_json::Value,
}

impl Event {
    /// Builds an event from a name and a payload.
    pub fn new(name: impl Into<String>, payload: serde_json::Value) -> Self {
        Self {
            name: name.into(),
            payload,
        }
    }

    /// Builds a payload-less event.
    pub fn bare(name: impl Into<String>) -> Self {
        Self {
            name: name.into(),
            payload: serde_json::Value::Null,
        }
    }
}
