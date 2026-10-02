//! JiYaoChu 控制中心 — 后端核心（cdylib）。
//!
//! 这一层是原来 Tauri 后端的纯 Rust 移植：去掉了 webview、IPC 层、事件总线和
//! 插件依赖，逻辑本身没有改动。前端现在是一个 WinUI 3 应用（C# + Reactor），
//! 通过 [`ffi`] 里的 C ABI 调用这里。
//!
//! 调用约定见 [`ffi`] 的模块文档。

pub mod core;
pub mod ffi;

pub use core::{Api, AppState, HardwareStatus};
pub use core::{AcpiDriver, HalError, HalResult, HardwareHal};
