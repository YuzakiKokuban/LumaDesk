//! Scoped OEM takeover. Only the installed GCUBridge control-center root and
//! scheduled tasks executing files under that root are touched. Original state
//! is backed up before any mutation, and restored exactly.
use crate::core::error::{HalError, HalResult};
use std::path::PathBuf;
#[derive(Debug, Clone, Default)]
pub struct OemReport {
    pub warnings: Vec<String>,
}
impl OemReport {
    pub fn complete(&self) -> bool {
        self.warnings.is_empty()
    }
    pub fn failures(&self) -> usize {
        self.warnings.len()
    }
    pub fn summary(&self, action: &str) -> String {
        format!(
            "OEM {action}: {}",
            if self.complete() { "完成" } else { "失败" }
        )
    }
}
pub fn marker_path() -> PathBuf {
    crate::core::config::data_dir().join("oem_takeover.json")
}
pub fn takeover_active() -> bool {
    #[cfg(windows)]
    {
        use winreg::{enums::HKEY_LOCAL_MACHINE, RegKey};
        let start: Option<u32> = RegKey::predef(HKEY_LOCAL_MACHINE)
            .open_subkey(r"SYSTEM\CurrentControlSet\Services\GCUBridge")
            .ok()
            .and_then(|key| key.get_value("Start").ok());
        marker_path().with_extension("json.active").exists() && start == Some(4)
    }
    #[cfg(not(windows))]
    {
        false
    }
}
pub fn takeover_oem(enable: bool) -> HalResult<OemReport> {
    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        let shell =
            PathBuf::from(std::env::var("SystemRoot").unwrap_or_else(|_| "C:\\Windows".into()))
                .join("System32\\WindowsPowerShell\\v1.0\\powershell.exe");
        let output = std::process::Command::new(shell)
            .args([
                "-NoProfile",
                "-NonInteractive",
                "-Command",
                include_str!("oem-control.ps1"),
            ])
            .env("LUMADESK_OEM_BACKUP", marker_path())
            .env(
                "LUMADESK_OEM_ACTION",
                if enable { "takeover" } else { "restore" },
            )
            .creation_flags(0x08000000)
            .output()
            .map_err(|e| HalError::io(e.to_string()))?;
        if !output.status.success() {
            return Err(HalError::io(format!(
                "官方控制中心操作失败：{}",
                String::from_utf8_lossy(&output.stderr).trim()
            )));
        }
        crate::core::services::logging::info(if enable {
            "OEM takeover complete"
        } else {
            "OEM original state restored"
        });
        Ok(OemReport::default())
    }
    #[cfg(not(windows))]
    {
        let _ = enable;
        Err(HalError::unsupported("OEM takeover requires Windows"))
    }
}
pub fn takeover(enable: bool) -> HalResult<OemReport> {
    takeover_oem(enable)
}
pub fn restore() -> HalResult<OemReport> {
    takeover_oem(false)
}
