//! The event queue that replaces Tauri's `emit`.
//!
//! On the Tauri build, anything the backend wanted to tell the window went out
//! through `AppHandle::emit` / `emit_to` and was delivered straight into the
//! webview. A native build has no webview and no Tauri runtime, so the backend
//! now *queues* the same notifications and the shell drains them
//! ([`crate::core::state::AppState::drain_events`]) from whatever loop it runs.
//!
//! The names are kept identical to the Tauri event names (`osd://config`,
//! `osd://preview`, ...) so the UI can keep listening for the same strings.

/// One queued notification.
#[derive(Debug, Clone, serde::Serialize)]
pub struct Event {
    /// Event name, exactly as it was emitted on the Tauri build.
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
