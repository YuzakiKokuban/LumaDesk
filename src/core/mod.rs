//! OpenRevo's native core.
//!
//! This tree is the Tauri backend ported out of `src-tauri/src` and onto plain
//! Rust. The logic is unchanged; what was removed is the runtime that used to
//! drive it:
//!
//! * no webview, no IPC layer — the shell calls [`Api`] directly;
//! * no event bus — [`AppState::push_event`] queues [`Event`]s and the shell
//!   drains them with [`AppState::drain_events`];
//! * no plugin crates — autostart is a registry write in [`autostart`], and
//!   "open this file" already shelled out to `cmd /C start` in the original;
//! * no Tauri window management — the OSD and the mini drawer are shell
//!   concerns, so their calls queue the same events the overlay used to receive
//!   (see `osd://*` and `shell://mini-drawer`).
//!
//! # Why `dead_code` is allowed here
//!
//! This crate has no `lib` target: `src/main.rs` is the crate root and declares
//! `mod core;`. Rust therefore analyses reachability from `main`, and a
//! library-style surface that is only reached through the shell looks like
//! three hundred dead items — which is exactly what it looked like before the
//! shell existed. The allow is structural, and it is scoped to this module tree
//! only; it does not silence anything in the UI, the platform layer or the
//! binary root.
//!
//! The re-exports below carry `unused_imports` for the same reason: a `pub use`
//! in a binary crate has no external reader, so the lint fires on every one of
//! them. It is allowed per re-export rather than module-wide so that a genuinely
//! unused import inside a child module is still reported.
//!
//! Everything the machine can really do lives behind the [`HardwareHal`] trait:
//! power schemes, refresh rate, backlight, fan and battery control, RGB, the
//! water cooler and the vendor driver. A feature whose protocol was never
//! recovered reports the typed [`HalError::Unsupported`] error instead of
//! pretending to work — the string still starts with
//! `not supported on this build:`, which is what the UI keys off.

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
