use crate::core::config::GpuMode;
use crate::core::error::{HalError, HalResult};
use serde::{Deserialize, Serialize};
use std::sync::{Mutex, OnceLock};

const GUID: &str = "{9F33F85C-13CA-4FD1-9C4A-96217722C593}";
const MODE_OFFSET: usize = 0x62;
static LOCK: Mutex<()> = Mutex::new(());
static INITIAL_MODE: OnceLock<u8> = OnceLock::new();

#[derive(Debug, Serialize, Deserialize)]
struct ModeChange {
    boot_id: String,
    initial_mode: u8,
    target_mode: u8,
    variable: String,
}

impl ModeChange {
    fn pending(&self, boot_id: Option<&str>, variable: &str, raw: u8) -> bool {
        // An unavailable boot stamp must not erase a pending reboot warning.
        self.variable == variable
            && boot_id.is_none_or(|boot| boot == self.boot_id)
            && raw != self.initial_mode
    }
}

fn change_path() -> std::path::PathBuf {
    crate::core::config::data_dir().join("mux-change.json")
}
fn saved_change() -> Option<ModeChange> {
    std::fs::read(change_path())
        .ok()
        .and_then(|bytes| serde_json::from_slice(&bytes).ok())
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct GpuModeInfo {
    pub configured_mode: Option<GpuMode>,
    pub platform: String,
    pub variable: String,
    pub ap_version: u8,
    pub raw_mode: u8,
    pub supported: bool,
    pub supports_igpu: bool,
    pub pending_reboot: bool,
    /// Configured mode is not a measurement of the current display route.
    #[serde(default)]
    pub reboot_status_known: bool,
    pub reason: String,
}

pub fn encode(amd: bool, mode: GpuMode) -> u8 {
    match (amd, mode) {
        (true, GpuMode::Hybrid) => 0,
        (true, GpuMode::Dgpu) | (false, GpuMode::Igpu) => 1,
        (true, GpuMode::Igpu) | (false, GpuMode::Dgpu) => 2,
        (false, GpuMode::Hybrid) => 4,
    }
}

pub fn decode(amd: bool, raw: u8) -> Option<GpuMode> {
    [GpuMode::Igpu, GpuMode::Hybrid, GpuMode::Dgpu]
        .into_iter()
        .find(|m| encode(amd, *m) == raw)
}

fn patch(buffer: &[u8], amd: bool, mode: GpuMode) -> HalResult<Vec<u8>> {
    if buffer.len() <= MODE_OFFSET {
        return Err(HalError::unavailable("固件变量未提供显卡模式字段"));
    }
    let mut result = buffer.to_vec();
    result[MODE_OFFSET] = encode(amd, mode);
    Ok(result)
}

#[cfg(windows)]
mod native {
    use super::*;
    use windows::core::{w, PCWSTR};
    use windows::Win32::Foundation::{
        CloseHandle, GetLastError, SetLastError, HANDLE, LUID, WIN32_ERROR,
    };
    use windows::Win32::Security::{
        AdjustTokenPrivileges, LookupPrivilegeValueW, LUID_AND_ATTRIBUTES, SE_PRIVILEGE_ENABLED,
        TOKEN_ADJUST_PRIVILEGES, TOKEN_PRIVILEGES, TOKEN_QUERY,
    };
    use windows::Win32::System::Threading::{GetCurrentProcess, OpenProcessToken};
    use windows::Win32::System::WindowsProgramming::{
        GetFirmwareEnvironmentVariableW, SetFirmwareEnvironmentVariableW,
    };

    pub fn boot_id() -> Option<String> {
        static BOOT: OnceLock<String> = OnceLock::new();
        if let Some(value) = BOOT.get() {
            return Some(value.clone());
        }
        let value = crate::core::hal::winapi::Wmi::first_row(
            "ROOT\\CIMV2",
            "SELECT LastBootUpTime FROM Win32_OperatingSystem",
        )
        .ok()
        .flatten()
        .and_then(|row| row.str_of("LastBootUpTime"))
        .filter(|value| !value.is_empty())?;
        let _ = BOOT.set(value.clone());
        Some(value)
    }

    fn wide(value: &str) -> Vec<u16> {
        value.encode_utf16().chain(Some(0)).collect()
    }

    pub fn privilege() -> HalResult<()> {
        let mut token = HANDLE::default();
        // SAFETY: valid process pseudo-handle, writable handle storage.
        unsafe {
            OpenProcessToken(
                GetCurrentProcess(),
                TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY,
                &mut token,
            )
        }
        .map_err(|e| HalError::io(format!("无法打开进程令牌：{e}")))?;
        let result = (|| {
            let mut luid = LUID::default();
            // SAFETY: stable terminated string and writable LUID.
            unsafe {
                LookupPrivilegeValueW(
                    PCWSTR::null(),
                    w!("SeSystemEnvironmentPrivilege"),
                    &mut luid,
                )
            }
            .map_err(|e| HalError::io(format!("固件访问特权查询失败：{e}")))?;
            let privileges = TOKEN_PRIVILEGES {
                PrivilegeCount: 1,
                Privileges: [LUID_AND_ATTRIBUTES {
                    Luid: luid,
                    Attributes: SE_PRIVILEGE_ENABLED,
                }],
            };
            // SAFETY: token and privileges remain valid throughout synchronous call.
            unsafe {
                SetLastError(WIN32_ERROR(0));
                AdjustTokenPrivileges(token, false, Some(&privileges), 0, None, None)
                    .map_err(|e| HalError::io(format!("固件访问特权启用失败：{e}")))?;
                if GetLastError().0 != 0 {
                    return Err(HalError::unavailable(
                        "请以管理员身份运行，以读取和保存显卡模式",
                    ));
                }
            }
            Ok(())
        })();
        // SAFETY: token was successfully opened above and is closed exactly once.
        let _ = unsafe { CloseHandle(token) };
        result
    }

    pub fn read(name: &str) -> HalResult<Vec<u8>> {
        let name_w = wide(name);
        let guid_w = wide(GUID);
        let mut buffer = vec![0u8; 512];
        // SAFETY: terminated strings and writable buffer of advertised capacity.
        let length = unsafe {
            GetFirmwareEnvironmentVariableW(
                PCWSTR(name_w.as_ptr()),
                PCWSTR(guid_w.as_ptr()),
                Some(buffer.as_mut_ptr().cast()),
                buffer.len() as u32,
            )
        };
        if length == 0 {
            let code = unsafe { GetLastError() }.0;
            return Err(HalError::unavailable(format!(
                "读取 {name} 失败（Windows {code}）"
            )));
        }
        buffer.truncate(length as usize);
        Ok(buffer)
    }

    pub fn write(name: &str, buffer: &[u8]) -> HalResult<()> {
        let name_w = wide(name);
        let guid_w = wide(GUID);
        // SAFETY: strings and the full existing variable buffer outlive the synchronous call.
        unsafe {
            SetFirmwareEnvironmentVariableW(
                PCWSTR(name_w.as_ptr()),
                PCWSTR(guid_w.as_ptr()),
                Some(buffer.as_ptr().cast()),
                buffer.len() as u32,
            )
        }
        .map_err(|e| HalError::io(format!("保存 {name} 失败：{e}")))
    }

    pub fn amd() -> bool {
        use winreg::{enums::HKEY_LOCAL_MACHINE, RegKey};
        static AMD: OnceLock<bool> = OnceLock::new();
        *AMD.get_or_init(|| {
            let cpu: String = RegKey::predef(HKEY_LOCAL_MACHINE)
                .open_subkey(r"HARDWARE\DESCRIPTION\System\CentralProcessor\0")
                .and_then(|key| key.get_value("ProcessorNameString"))
                .unwrap_or_default();
            let cpu = cpu.to_uppercase();
            ["AMD", "RYZEN", "RADEON"]
                .iter()
                .any(|word| cpu.contains(word))
        })
    }

    pub fn snapshot(project: Option<u8>) -> HalResult<(GpuModeInfo, Vec<u8>)> {
        privilege()?;
        let (variable, buffer) = match read("OemMagicVariable") {
            Ok(buffer) => ("OemMagicVariable", buffer),
            Err(first) => match read("UniWillVariable") {
                Ok(buffer) => ("UniWillVariable", buffer),
                Err(second) => return Err(HalError::unavailable(format!("{first}；{second}"))),
            },
        };
        if buffer.len() <= MODE_OFFSET {
            return Err(HalError::unavailable("固件变量未提供显卡模式字段"));
        }
        let raw_mode = buffer[MODE_OFFSET];
        let configured_mode = decode(amd(), raw_mode);
        let initial = *INITIAL_MODE.get_or_init(|| raw_mode);
        let boot = boot_id();
        let change = saved_change();
        let marker_unreadable = change.is_none() && change_path().exists();
        let pending = change
            .map(|change| change.pending(boot.as_deref(), variable, raw_mode))
            .unwrap_or(raw_mode != initial || marker_unreadable);
        let dgpu = read("OemDgpuPresent").ok().and_then(|b| b.first().copied()) == Some(1);
        let supported = configured_mode.is_some() && dgpu;
        // The recovered iGPU board guard has not yet been mapped for every chassis.
        // Advertise iGPU on the current verified GM6IX9B project only.
        let supports_igpu = supported && project == Some(0x1A) && buffer[0x43] >= 25;
        let info = GpuModeInfo {
            configured_mode,
            platform: if amd() { "amd" } else { "intel" }.into(),
            variable: variable.into(),
            ap_version: buffer[0x43],
            raw_mode,
            supported,
            supports_igpu,
            pending_reboot: pending,
            reboot_status_known: boot.is_some() && !marker_unreadable,
            reason: if !dgpu {
                "固件未确认独立显卡，暂不提供 MUX 切换"
            } else if configured_mode.is_none() {
                "固件模式编码尚未识别"
            } else {
                ""
            }
            .into(),
        };
        Ok((info, buffer))
    }
}

pub fn info(project: Option<u8>) -> HalResult<GpuModeInfo> {
    let _guard = LOCK.lock().unwrap_or_else(|e| e.into_inner());
    #[cfg(windows)]
    {
        native::snapshot(project).map(|(info, _)| info)
    }
    #[cfg(not(windows))]
    {
        let _ = project;
        Err(HalError::unavailable("MUX 固件访问需要 Windows"))
    }
}

pub fn set_mode(project: Option<u8>, mode: GpuMode) -> HalResult<()> {
    let _guard = LOCK.lock().unwrap_or_else(|e| e.into_inner());
    #[cfg(windows)]
    {
        let (info, buffer) = native::snapshot(project)?;
        if !info.supported || (mode == GpuMode::Igpu && !info.supports_igpu) {
            return Err(HalError::unsupported("此机型尚未验证所选显卡模式"));
        }
        if info.configured_mode == Some(mode) {
            return Ok(());
        }
        let updated = patch(&buffer, native::amd(), mode)?;
        let boot = native::boot_id()
            .ok_or_else(|| HalError::unavailable("无法取得系统启动标识，请稍后重试显卡模式保存"))?;
        let initial_mode = saved_change()
            .filter(|change| change.boot_id == boot && change.variable == info.variable)
            .map(|change| change.initial_mode)
            .unwrap_or(info.raw_mode);
        let change = ModeChange {
            boot_id: boot,
            initial_mode,
            target_mode: updated[MODE_OFFSET],
            variable: info.variable.clone(),
        };
        // Persist intent before touching firmware so a crash cannot lose it.
        crate::core::config::ensure_dir(&crate::core::config::data_dir())
            .map_err(|e| HalError::io(e.to_string()))?;
        let bytes = serde_json::to_vec(&change).map_err(|e| HalError::io(e.to_string()))?;
        crate::core::config::write_atomic(&change_path(), &bytes)
            .map_err(|e| HalError::io(e.to_string()))?;
        if info.variable == "OemMagicVariable" || info.ap_version >= 25 {
            native::write("OemMagicDoor", &[1])?;
        }
        native::write(&info.variable, &updated)?;
        let actual = native::read(&info.variable)?;
        if actual.get(MODE_OFFSET) != updated.get(MODE_OFFSET) {
            return Err(HalError::io("显卡模式保存后读回不一致"));
        }
        Ok(())
    }
    #[cfg(not(windows))]
    {
        let _ = (project, mode);
        Err(HalError::unavailable("MUX 固件访问需要 Windows"))
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn pending_reboot_survives_process_restart_and_clears_only_on_new_boot_or_revert() {
        let change = ModeChange {
            boot_id: "boot-a".into(),
            initial_mode: 4,
            target_mode: 2,
            variable: "OemMagicVariable".into(),
        };
        let bytes = serde_json::to_vec(&change).unwrap();
        let restored: ModeChange = serde_json::from_slice(&bytes).unwrap();
        assert!(restored.pending(Some("boot-a"), "OemMagicVariable", 2));
        assert!(restored.pending(None, "OemMagicVariable", 2));
        assert!(!restored.pending(Some("boot-b"), "OemMagicVariable", 2));
        assert!(!restored.pending(Some("boot-a"), "OemMagicVariable", 4));
        assert!(!restored.pending(Some("boot-a"), "UniWillVariable", 2));
    }
    #[test]
    fn encodings_match_oem_constants() {
        assert_eq!(
            [
                encode(false, GpuMode::Igpu),
                encode(false, GpuMode::Dgpu),
                encode(false, GpuMode::Hybrid)
            ],
            [1, 2, 4]
        );
        assert_eq!(
            [
                encode(true, GpuMode::Igpu),
                encode(true, GpuMode::Dgpu),
                encode(true, GpuMode::Hybrid)
            ],
            [2, 1, 0]
        );
        assert_eq!(decode(false, 255), None);
    }
    #[test]
    fn patch_preserves_every_other_byte_and_length() {
        let original: Vec<u8> = (0..180).collect();
        for amd in [false, true] {
            for mode in [GpuMode::Igpu, GpuMode::Dgpu, GpuMode::Hybrid] {
                let result = patch(&original, amd, mode).unwrap();
                assert_eq!(result.len(), original.len());
                for i in 0..result.len() {
                    assert_eq!(
                        result[i],
                        if i == MODE_OFFSET {
                            encode(amd, mode)
                        } else {
                            original[i]
                        }
                    );
                }
            }
        }
        assert!(patch(&[0; 0x62], false, GpuMode::Dgpu).is_err());
    }
}
