//! Thin, safe-ish wrappers over the documented Windows APIs the real
//! [`super::windows::WindowsHal`] is built from.
//!
//! Everything in this module is *documented* behaviour: power schemes,
//! display modes, WMI backlight control, battery status, WMI queries and
//! the ACPI firmware table. Nothing here needs the UniWill kernel driver, which
//! is why this is the part of the backend that genuinely works on a stock
//! Windows install.
//!
//! House rules for this file:
//!   * every `unsafe` block is small, and carries a `// SAFETY:` comment that
//!     names the invariant being relied on,
//!   * nothing panics — fallible calls come back as `Result` or `Option`,
//!   * ASCII control characters never reach the log (paths can be poisoned).

use crate::core::error::{HalError, HalResult};
use std::collections::HashMap;
use std::path::Path;
use windows::core::{BSTR, GUID, PCWSTR, PWSTR};
use windows::Win32::Devices::Display::*;
use windows::Win32::Foundation::{CloseHandle, HANDLE, HWND, MAX_PATH};
use windows::Win32::Graphics::Gdi::{
    ChangeDisplaySettingsExW, EnumDisplayDevicesW, EnumDisplaySettingsW, CDS_TEST,
    CDS_UPDATEREGISTRY, DEVMODEW, DISPLAY_DEVICEW, DISPLAY_DEVICE_ACTIVE, DISP_CHANGE_SUCCESSFUL,
    DM_DISPLAYFREQUENCY, ENUM_CURRENT_SETTINGS, ENUM_DISPLAY_SETTINGS_MODE,
};
use windows::Win32::Security::{GetTokenInformation, TokenElevation, TOKEN_ELEVATION, TOKEN_QUERY};
use windows::Win32::System::Com::{
    CoCreateInstance, CoInitializeEx, CoInitializeSecurity, CoSetProxyBlanket, CoUninitialize,
    CLSCTX_INPROC_SERVER, COINIT_MULTITHREADED, EOLE_AUTHENTICATION_CAPABILITIES,
    RPC_C_AUTHN_LEVEL_DEFAULT, RPC_C_IMP_LEVEL_IMPERSONATE, SAFEARRAY,
};
use windows::Win32::System::Ole::{SafeArrayGetElement, SafeArrayGetLBound, SafeArrayGetUBound};
use windows::Win32::System::Power::{
    CallNtPowerInformation, GetSystemPowerStatus, PowerEnumerate, PowerGetActiveScheme,
    PowerReadFriendlyName, PowerSetActiveScheme, POWER_DATA_ACCESSOR, POWER_INFORMATION_LEVEL,
    SYSTEM_BATTERY_STATE, SYSTEM_POWER_STATUS,
};

// Windows 11 exposes separate user power-mode votes for AC and battery.
// Resolve dynamically so older systems report an unsupported feature cleanly.
fn user_power_mode_function(
    name: &'static [u8],
) -> HalResult<unsafe extern "system" fn(*mut GUID) -> u32> {
    use std::sync::OnceLock;
    use windows::Win32::System::LibraryLoader::{GetProcAddress, LoadLibraryW};
    static MODULE: OnceLock<usize> = OnceLock::new();
    let module = *MODULE.get_or_init(|| {
        let path = Path::new(&std::env::var("SystemRoot").unwrap_or_else(|_| "C:\\Windows".into()))
            .join("System32\\powrprof.dll");
        let wide: Vec<u16> = path
            .to_string_lossy()
            .encode_utf16()
            .chain(Some(0))
            .collect();
        // SAFETY: the absolute DLL path is terminated and remains alive during the call.
        unsafe {
            LoadLibraryW(PCWSTR(wide.as_ptr()))
                .map(|h| h.0 as usize)
                .unwrap_or(0)
        }
    });
    if module == 0 {
        return Err(HalError::unavailable("无法加载 Windows 电源管理 API"));
    }
    // SAFETY: cached module remains loaded; each name has a terminating NUL.
    let address = unsafe {
        GetProcAddress(
            windows::Win32::Foundation::HMODULE(module as *mut _),
            windows::core::PCSTR(name.as_ptr()),
        )
    }
    .ok_or_else(|| HalError::unsupported("此 Windows 版本不提供电源模式 API"))?;
    // SAFETY: all four documented Power{Get,Set}UserConfigured{AC,DC}PowerMode
    // exports return DWORD and accept one GUID pointer (const for setters).
    Ok(unsafe {
        std::mem::transmute::<
            unsafe extern "system" fn() -> isize,
            unsafe extern "system" fn(*mut GUID) -> u32,
        >(address)
    })
}

fn on_ac_power() -> HalResult<bool> {
    let mut status = SYSTEM_POWER_STATUS::default();
    // SAFETY: valid output buffer.
    unsafe { GetSystemPowerStatus(&mut status) }
        .map_err(|e| HalError::unavailable(e.to_string()))?;
    match status.ACLineStatus {
        1 => Ok(true),
        0 => Ok(false),
        _ => Err(HalError::unavailable("无法确定当前供电方式")),
    }
}

pub fn user_power_mode() -> HalResult<u8> {
    let get = user_power_mode_function(if on_ac_power()? {
        b"PowerGetUserConfiguredACPowerMode\0"
    } else {
        b"PowerGetUserConfiguredDCPowerMode\0"
    })?;
    let mut guid = GUID::zeroed();
    // SAFETY: valid writable GUID buffer.
    let code = unsafe { get(&mut guid) };
    if code != 0 {
        return Err(HalError::unavailable(format!(
            "读取 Windows 电源模式失败：{code}"
        )));
    }
    if guid == GUID::from_u128(0x961cc777_2547_4f9d_8174_7d86181b8a7a) {
        Ok(0)
    } else if guid == GUID::from_u128(0xded574b5_45a0_4f42_8737_46345c09c238) {
        Ok(2)
    } else if guid == GUID::zeroed() {
        Ok(1)
    } else {
        Err(HalError::unsupported("Windows 返回了未知电源模式"))
    }
}

pub fn set_user_power_mode(mode: u8) -> HalResult<()> {
    let mut guid = match mode {
        0 => GUID::from_u128(0x961cc777_2547_4f9d_8174_7d86181b8a7a),
        1 => GUID::zeroed(),
        2 => GUID::from_u128(0xded574b5_45a0_4f42_8737_46345c09c238),
        _ => return Err(HalError::unsupported("尚未实现 OEM 自定义性能档位")),
    };
    let set = user_power_mode_function(if on_ac_power()? {
        b"PowerSetUserConfiguredACPowerMode\0"
    } else {
        b"PowerSetUserConfiguredDCPowerMode\0"
    })?;
    // SAFETY: valid GUID input buffer, matching documented signature.
    let code = unsafe { set(&mut guid) };
    if code != 0 {
        return Err(HalError::unavailable(format!(
            "切换 Windows 电源模式失败：{code}"
        )));
    }
    if user_power_mode()? != mode {
        return Err(HalError::unavailable("Windows 电源模式回读不一致"));
    }
    Ok(())
}
use windows::Win32::System::SystemInformation::{GetSystemFirmwareTable, FIRMWARE_TABLE_PROVIDER};
use windows::Win32::System::Threading::{
    GetCurrentProcess, OpenProcessToken, QueryFullProcessImageNameW, PROCESS_NAME_WIN32,
};
use windows::Win32::System::Variant::{
    VariantClear, VariantToDoubleWithDefault, VariantToInt32WithDefault,
    VariantToUInt32WithDefault, VariantToUInt64WithDefault, VARIANT,
};
use windows::Win32::System::Wmi::{
    IEnumWbemClassObject, IWbemClassObject, IWbemContext, IWbemLocator, IWbemServices, WbemLocator,
    WBEM_FLAG_FORWARD_ONLY, WBEM_FLAG_RETURN_IMMEDIATELY, WBEM_GENERIC_FLAG_TYPE,
};
use windows::Win32::UI::ColorSystem::SetDeviceGammaRamp;

/// `RPC_C_AUTHN_WINNT` — authenticate with NTLM.
///
/// Defined here rather than imported because the constant lives in
/// `Win32::System::Rpc`, and pulling that whole module in only for two integers
/// is not worth the extra feature. Values are from `rpcdce.h`.
const RPC_C_AUTHN_WINNT: u32 = 10;
/// `RPC_C_AUTHZ_NONE` — no authorisation service (`rpcdce.h`).
const RPC_C_AUTHZ_NONE: u32 = 0;

/// Values we know how to pull out of a WMI row.
#[derive(Debug, Clone, PartialEq)]
pub enum WmiValue {
    Str(String),
    Num(f64),
    Bool(bool),
    Array(Vec<WmiValue>),
    Null,
}

impl WmiValue {
    pub fn as_str(&self) -> Option<&str> {
        match self {
            WmiValue::Str(value) => Some(value.as_str()),
            _ => None,
        }
    }

    pub fn to_string_lossy(&self) -> String {
        match self {
            WmiValue::Str(value) => value.clone(),
            WmiValue::Num(value) => {
                if value.fract() == 0.0 && value.abs() < 1e15 {
                    format!("{}", *value as i64)
                } else {
                    format!("{value}")
                }
            }
            WmiValue::Bool(value) => value.to_string(),
            WmiValue::Array(values) => values
                .iter()
                .map(WmiValue::to_string_lossy)
                .collect::<Vec<_>>()
                .join(","),
            WmiValue::Null => String::new(),
        }
    }

    pub fn to_f64(&self) -> Option<f64> {
        match self {
            WmiValue::Num(value) => Some(*value),
            WmiValue::Bool(value) => Some(if *value { 1.0 } else { 0.0 }),
            WmiValue::Str(value) => value.trim().parse::<f64>().ok(),
            _ => None,
        }
    }

    pub fn to_bool(&self) -> Option<bool> {
        match self {
            WmiValue::Bool(value) => Some(*value),
            WmiValue::Num(value) => Some(*value != 0.0),
            WmiValue::Str(value) => match value.to_ascii_lowercase().as_str() {
                "true" | "yes" | "1" => Some(true),
                "false" | "no" | "0" => Some(false),
                _ => None,
            },
            _ => None,
        }
    }
}

/// One WMI instance.
#[derive(Debug, Clone, Default)]
pub struct WmiRow(pub HashMap<String, WmiValue>);

impl WmiRow {
    pub fn get(&self, name: &str) -> Option<&WmiValue> {
        // WMI property names are case-insensitive in practice.
        if let Some(value) = self.0.get(name) {
            return Some(value);
        }
        self.0
            .iter()
            .find(|(key, _)| key.eq_ignore_ascii_case(name))
            .map(|(_, value)| value)
    }

    pub fn str_of(&self, name: &str) -> Option<String> {
        self.get(name)
            .map(WmiValue::to_string_lossy)
            .filter(|value| !value.is_empty())
    }

    pub fn f64_of(&self, name: &str) -> Option<f64> {
        self.get(name).and_then(WmiValue::to_f64)
    }

    pub fn u64_of(&self, name: &str) -> Option<u64> {
        self.f64_of(name).map(|value| value.max(0.0) as u64)
    }

    pub fn u32_of(&self, name: &str) -> Option<u32> {
        self.f64_of(name)
            .map(|value| value.max(0.0).min(f64::from(u32::MAX)) as u32)
    }

    pub fn bool_of(&self, name: &str) -> Option<bool> {
        self.get(name).and_then(WmiValue::to_bool)
    }
}

/// A short-lived WMI connection.
///
/// WMI objects are apartment-bound, so this type deliberately lives and dies
/// inside one call on one thread: [`Wmi::connect`] initialises COM for the
/// current thread and the handle is dropped before the caller returns. It is
/// therefore neither `Send` nor `Sync`, which is exactly what we want.
pub struct Wmi {
    /// `None` once the value is being dropped; see [`Drop for Wmi`].
    services: Option<IWbemServices>,
    /// True when we own the COM initialisation and must pair it with
    /// `CoUninitialize`.
    own_com: bool,
}

impl Wmi {
    /// Subscribe to firmware notifications, blocking until an event arrives.
    /// This extrinsic event query has no WITHIN interval and never scans EC/WMI.
    pub fn watch_oem_hotkeys(
        &self,
        ready: impl Fn(Result<(), String>),
        interrupted: impl Fn() -> bool,
        notify: impl Fn(u32),
    ) -> HalResult<()> {
        self.watch_numeric_event(
            "SELECT * FROM AcpiTest_EventULong",
            "ULong",
            ready,
            interrupted,
            notify,
        )
    }

    /// Windows pushes the resulting display brightness; no periodic query.
    pub fn watch_display_brightness(
        &self,
        ready: impl Fn(Result<(), String>),
        interrupted: impl Fn() -> bool,
        notify: impl Fn(u32),
    ) -> HalResult<()> {
        self.watch_numeric_event(
            "SELECT * FROM WmiMonitorBrightnessEvent WHERE Active = TRUE",
            "Brightness",
            ready,
            interrupted,
            notify,
        )
    }

    fn watch_numeric_event(
        &self,
        query: &str,
        property: &str,
        ready: impl Fn(Result<(), String>),
        interrupted: impl Fn() -> bool,
        notify: impl Fn(u32),
    ) -> HalResult<()> {
        let services = self
            .services
            .as_ref()
            .ok_or_else(|| HalError::unavailable("WMI closed"))?;
        let subscription = unsafe {
            services.ExecNotificationQuery(
                &BSTR::from("WQL"),
                &BSTR::from(query),
                WBEM_FLAG_FORWARD_ONLY | WBEM_FLAG_RETURN_IMMEDIATELY,
                None::<&IWbemContext>,
            )
        };
        let enumerator = match subscription {
            Ok(value) => {
                ready(Ok(()));
                value
            }
            Err(error) => {
                ready(Err(error.to_string()));
                return Err(HalError::unavailable(error.to_string()));
            }
        };
        loop {
            if interrupted() {
                return Ok(());
            }
            let mut batch: [Option<IWbemClassObject>; 1] = [None];
            let mut returned = 0;
            // Bounded event wait allows reconnect after resume. No hardware query.
            let status = unsafe { enumerator.Next(5_000, &mut batch, &mut returned) };
            status
                .ok()
                .map_err(|error| HalError::unavailable(error.to_string()))?;
            if returned == 0 {
                if status.0 == 0x00040004 {
                    continue;
                } // WBEM_S_TIMEDOUT
                return Err(HalError::unavailable("WMI event subscription ended"));
            }
            if let Some(object) = &batch[0] {
                if interrupted() {
                    return Ok(());
                }
                let row = unsafe { read_object(object) };
                if let Some(code) = row.f64_of(property) {
                    notify(code as u32);
                }
            }
        }
    }

    /// Connects to the given WMI namespace (usually `ROOT\WMI` or `ROOT\CIMV2`).
    pub fn connect(namespace: &str) -> HalResult<Self> {
        // SAFETY: `CoInitializeEx` with a null reserved pointer is the
        // documented way to enter an apartment; the returned HRESULT is
        // checked below and `S_FALSE` ("already initialised") is accepted.
        let hr = unsafe { CoInitializeEx(None, COINIT_MULTITHREADED) };
        let already_initialised = hr == windows::Win32::Foundation::S_FALSE;
        if hr.is_err() && !already_initialised {
            return Err(HalError::unavailable(format!(
                "COM could not be initialised for the WMI query (0x{:08X})",
                hr.0 as u32
            )));
        }
        // On `S_FALSE` the thread was already in an apartment, so the matching
        // `CoUninitialize` is not ours to call.
        let own_com = !already_initialised;

        // Process-wide security defaults. This may only be called once; a
        // second call fails with RPC_E_TOO_LATE, which we ignore on purpose.
        // SAFETY: all arguments are either null (documented as "use the
        // default") or the standard default constants.
        unsafe {
            let _ = CoInitializeSecurity(
                None,
                -1,
                None,
                None,
                RPC_C_AUTHN_LEVEL_DEFAULT,
                RPC_C_IMP_LEVEL_IMPERSONATE,
                None,
                EOLE_AUTHENTICATION_CAPABILITIES(0),
                None,
            );
        }

        // SAFETY: `WbemLocator` is a documented in-proc class and the returned
        // interface is only used through this module's wrappers.
        let locator: IWbemLocator =
            match unsafe { CoCreateInstance(&WbemLocator, None, CLSCTX_INPROC_SERVER) } {
                Ok(locator) => locator,
                Err(err) => {
                    if own_com {
                        // SAFETY: balances the successful `CoInitializeEx` above.
                        unsafe { CoUninitialize() };
                    }
                    return Err(HalError::unavailable(format!(
                        "the WMI service is not available ({err})"
                    )));
                }
            };

        let namespace_bstr = BSTR::from(namespace);
        // SAFETY: every `BSTR` lives until the end of the statement, the extra
        // arguments are the documented "default" values and the context
        // out-parameter is type-annotated to the expected interface.
        let services = match unsafe {
            locator.ConnectServer(
                &namespace_bstr,
                &BSTR::new(),
                &BSTR::new(),
                &BSTR::new(),
                0,
                &BSTR::new(),
                None::<&IWbemContext>,
            )
        } {
            Ok(services) => services,
            Err(err) => {
                if own_com {
                    // SAFETY: balances the successful `CoInitializeEx` above.
                    unsafe { CoUninitialize() };
                }
                return Err(HalError::unavailable(format!(
                    "the WMI namespace {namespace} could not be opened ({err})"
                )));
            }
        };

        // Without this blanket the proxy marshals as an unidentified caller and
        // every query comes back as E_ACCESSDENIED.
        // SAFETY: the interface pointer is alive for the whole call and the
        // authorisation service / levels are the documented defaults.
        unsafe {
            let _ = CoSetProxyBlanket(
                &services,
                RPC_C_AUTHN_WINNT,
                RPC_C_AUTHZ_NONE,
                None,
                RPC_C_AUTHN_LEVEL_DEFAULT,
                RPC_C_IMP_LEVEL_IMPERSONATE,
                None,
                EOLE_AUTHENTICATION_CAPABILITIES(0),
            );
        }

        Ok(Wmi {
            services: Some(services),
            own_com,
        })
    }

    /// Runs a WQL query and materialises every returned instance.
    pub fn query(&self, wql: &str) -> HalResult<Vec<WmiRow>> {
        let Some(services) = self.services.as_ref() else {
            return Err(HalError::unavailable(
                "the WMI connection has already been closed",
            ));
        };
        // SAFETY: the services pointer stays alive for the whole call, the
        // `BSTR`s live until the statement ends and the out-parameter is
        // type-annotated to the expected interface.
        let enumerator: IEnumWbemClassObject = unsafe {
            services
                .ExecQuery(
                    &BSTR::from("WQL"),
                    &BSTR::from(wql),
                    WBEM_FLAG_FORWARD_ONLY | WBEM_FLAG_RETURN_IMMEDIATELY,
                    None::<&IWbemContext>,
                )
                .map_err(|err| HalError::unavailable(format!("WMI query failed ({err})")))?
        };

        let mut rows = Vec::new();
        loop {
            let mut batch: [Option<IWbemClassObject>; 16] = Default::default();
            let mut returned: u32 = 0;
            // SAFETY: `batch` is a valid slice of 16 slots, `returned` is a
            // valid out-pointer and the two-second timeout keeps a wedged WMI
            // provider from blocking a UI command forever.
            let hr = unsafe { enumerator.Next(2_000, &mut batch, &mut returned) };
            if hr.is_err() || returned == 0 {
                break;
            }
            for object in batch.iter().take(returned as usize).flatten() {
                // SAFETY: `object` came from the enumerator above, so it is a
                // live `IWbemClassObject` on this thread.
                rows.push(unsafe { read_object(object) });
            }
            if rows.len() > 4_096 {
                break;
            }
        }
        Ok(rows)
    }

    /// Connects, queries and releases in one call. This is the entry point the
    /// HAL uses.
    pub fn query_rows(namespace: &str, wql: &str) -> HalResult<Vec<WmiRow>> {
        let wmi = Wmi::connect(namespace)?;
        // `rows` is produced before `wmi` is dropped at the end of the block.
        wmi.query(wql)
    }

    /// Runs a query and keeps only the first instance.
    pub fn first_row(namespace: &str, wql: &str) -> HalResult<Option<WmiRow>> {
        let wmi = Wmi::connect(namespace)?;
        Ok(wmi.query(wql)?.into_iter().next())
    }

    fn set_brightness(&self, path: &str, percent: u8) -> HalResult<()> {
        let services = self
            .services
            .as_ref()
            .ok_or_else(|| HalError::unavailable("WMI connection closed"))?;
        let mut class = None;
        let mut signature = None;
        let mut output = None;
        let method = wide_z("WmiSetBrightness");
        // SAFETY: all interfaces and BSTR/VARIANT buffers live on this COM
        // apartment for the synchronous call; each output pointer is valid.
        let row = unsafe {
            services
                .GetObject(
                    &BSTR::from("WmiMonitorBrightnessMethods"),
                    WBEM_GENERIC_FLAG_TYPE(0),
                    None::<&IWbemContext>,
                    Some(&mut class),
                    None,
                )
                .map_err(|e| HalError::unavailable(format!("读取亮度接口失败：{e}")))?;
            let class = class.ok_or_else(|| HalError::unavailable("缺少 WMI 亮度控制类"))?;
            class
                .GetMethod(
                    PCWSTR(method.as_ptr()),
                    0,
                    &mut signature,
                    std::ptr::null_mut(),
                )
                .map_err(|e| HalError::unavailable(format!("读取亮度方法失败：{e}")))?;
            let input = signature
                .ok_or_else(|| HalError::unavailable("缺少亮度方法参数"))?
                .SpawnInstance(0)
                .map_err(|e| HalError::unavailable(e.to_string()))?;
            input
                .Put(
                    PCWSTR(wide_z("Timeout").as_ptr()),
                    0,
                    &VARIANT::from(0i32),
                    0,
                )
                .map_err(|e| HalError::io(format!("设置亮度 Timeout 参数失败：{e}")))?;
            input
                .Put(
                    PCWSTR(wide_z("Brightness").as_ptr()),
                    0,
                    &VARIANT::from(percent),
                    0,
                )
                .map_err(|e| HalError::io(format!("设置 Brightness 参数失败：{e}")))?;
            services
                .ExecMethod(
                    &BSTR::from(path),
                    &BSTR::from("WmiSetBrightness"),
                    WBEM_GENERIC_FLAG_TYPE(0),
                    None::<&IWbemContext>,
                    &input,
                    Some(&mut output),
                    None,
                )
                .map_err(|e| HalError::io(format!("设置内置屏幕亮度失败：{e}")))?;
            output.as_ref().map(|value| read_object(value))
        };
        match row.as_ref().and_then(|value| value.u32_of("ReturnValue")) {
            Some(0) => Ok(()),
            Some(code) => Err(HalError::io(format!("内置屏幕亮度设置被驱动拒绝：{code}"))),
            // Some panel providers omit out-parameters despite successful
            // HRESULT. Require actual readback rather than inventing a result.
            None if brightness_panel(self)?.u32_of("CurrentBrightness")
                == Some(u32::from(percent)) =>
            {
                Ok(())
            }
            None => Err(HalError::io("亮度方法未返回成功状态，且实际读回不匹配")),
        }
    }
}

impl Drop for Wmi {
    fn drop(&mut self) {
        // Release the interface *before* leaving the apartment. A struct's
        // fields are dropped after its `Drop::drop` body, so calling
        // `CoUninitialize` here directly would tear down COM while
        // `self.services` is still a live proxy — the final `Release` then runs
        // against a dead apartment and access-violates inside `combased.dll`.
        drop(self.services.take());
        if self.own_com {
            // SAFETY: balances the successful `CoInitializeEx` in `connect`, and
            // no COM interface this call created is still referenced.
            unsafe { CoUninitialize() };
        }
    }
}

/// Reads every property of one WMI object into a [`WmiRow`].
///
/// # Safety
/// `object` must be a live `IWbemClassObject` obtained on this thread.
unsafe fn read_object(object: &IWbemClassObject) -> WmiRow {
    let mut row = WmiRow::default();
    // Include system properties such as __PATH, needed to target this instance
    // when calling a method. 0x10 is NONSYSTEM_ONLY, not a materialisation flag.
    // SAFETY: `object` is a live interface. `BeginEnumeration` only rewinds the
    // object's property cursor and returns an HRESULT, not an enumerator.
    if object.BeginEnumeration(0).is_err() {
        return row;
    }

    loop {
        let mut name = BSTR::new();
        let mut value = VARIANT::default();
        let mut cim_type: i32 = 0;
        let mut flavour: i32 = 0;
        // SAFETY: `name`, `value`, `cim_type` and `flavour` are valid
        // out-pointers owned by this scope; the loop ends at
        // WBEM_S_NO_MORE_DATA, which the wrapper reports as an error.
        if unsafe {
            object.Next(
                0,
                &mut name as *mut BSTR,
                &mut value as *mut VARIANT,
                &mut cim_type as *mut i32,
                &mut flavour as *mut i32,
            )
        }
        .is_err()
        {
            break;
        }
        if name.is_empty() {
            // SAFETY: `value` was filled in by the call above.
            unsafe {
                let _ = VariantClear(&mut value);
            }
            break;
        }
        let key = name.to_string();
        // SAFETY: `value` is an initialised VARIANT freshly written by WMI.
        row.0.insert(key, unsafe { read_variant(&value) });
        // SAFETY: releases any BSTR/SAFEARRAY the variant owns.
        unsafe {
            let _ = VariantClear(&mut value);
        }
    }
    row
}

/// Converts a `VARIANT` into a [`WmiValue`].
///
/// # Safety
/// `value` must be an initialised `VARIANT`; the `VariantTo*` helpers read the
/// tag and payload, and the union fields are only touched for the matching tag.
unsafe fn read_variant(value: &VARIANT) -> WmiValue {
    let tag = value.Anonymous.Anonymous.vt.0;
    if tag & windows::Win32::System::Variant::VT_ARRAY.0 != 0 {
        return unsafe { read_array(value) };
    }
    let payload = &value.Anonymous.Anonymous.Anonymous;
    match tag & 0x0FFF {
        // VT_BSTR
        8 => {
            if payload.bstrVal.is_empty() {
                WmiValue::Null
            } else {
                WmiValue::Str(payload.bstrVal.to_string())
            }
        }
        // VT_BOOL
        11 => WmiValue::Bool(payload.boolVal.as_bool()),
        // VT_I2 / VT_I4
        2 | 3 => WmiValue::Num(f64::from(VariantToInt32WithDefault(value, 0))),
        // VT_UI1 / VT_UI2 / VT_UI4 (brightness events are uint8).
        17..=19 => WmiValue::Num(f64::from(VariantToUInt32WithDefault(value, 0))),
        // VT_I8
        20 => WmiValue::Num(VariantToInt32WithDefault(value, 0) as f64),
        // VT_UI8
        21 => WmiValue::Num(VariantToUInt64WithDefault(value, 0) as f64),
        // VT_R4
        4 => WmiValue::Num(f64::from(payload.fltVal)),
        // VT_R8
        5 => WmiValue::Num(VariantToDoubleWithDefault(value, 0.0)),
        // VT_EMPTY / VT_NULL
        0 | 1 => WmiValue::Null,
        _ => WmiValue::Null,
    }
}

#[cfg(test)]
mod brightness_event_tests {
    use super::*;

    #[test]
    fn byte_brightness_variants_preserve_percentage() {
        for percent in [0u8, 65, 100] {
            let mut value = VARIANT::default();
            // SAFETY: initialise the uint8 tag and its matching primitive payload.
            unsafe {
                let payload = &mut *value.Anonymous.Anonymous;
                payload.vt = windows::Win32::System::Variant::VT_UI1;
                payload.Anonymous.bVal = percent;
                assert_eq!(read_variant(&value).to_f64(), Some(f64::from(percent)));
            }
        }
    }
}

/// # Safety
/// `value` must be an initialised `VARIANT` holding a one-dimensional
/// `SAFEARRAY`.
unsafe fn read_array(value: &VARIANT) -> WmiValue {
    let array: *mut SAFEARRAY = value.Anonymous.Anonymous.Anonymous.parray;
    if array.is_null() {
        return WmiValue::Array(Vec::new());
    }
    // SAFETY: `array` is non-null and points at a valid descriptor. Dimensions
    // are 1-based and element access is bounds-checked by the API itself.
    let Ok(lower) = (unsafe { SafeArrayGetLBound(array, 1) }) else {
        return WmiValue::Array(Vec::new());
    };
    let Ok(upper) = (unsafe { SafeArrayGetUBound(array, 1) }) else {
        return WmiValue::Array(Vec::new());
    };
    let mut out = Vec::new();
    let mut index = lower;
    while index <= upper && out.len() < 64 {
        let mut element = VARIANT::default();
        // SAFETY: `index` is inside the bounds reported by the API and
        // `element` is a valid out-pointer for the element copy.
        let copied = unsafe {
            SafeArrayGetElement(
                array,
                &index as *const i32,
                &mut element as *mut VARIANT as *mut _,
            )
        };
        if copied.is_ok() {
            // SAFETY: `element` is now an initialised VARIANT.
            out.push(unsafe { read_variant(&element) });
            // SAFETY: releases the element copy's payload.
            unsafe {
                let _ = VariantClear(&mut element);
            }
        }
        index += 1;
    }
    WmiValue::Array(out)
}

/// Formats a GUID the way `powercfg` and the rest of Windows do.
pub fn guid_string(guid: &GUID) -> String {
    format!(
        "{{{:08x}-{:04x}-{:04x}-{:02x}{:02x}-{:02x}{:02x}{:02x}{:02x}{:02x}{:02x}}}",
        guid.data1,
        guid.data2,
        guid.data3,
        guid.data4[0],
        guid.data4[1],
        guid.data4[2],
        guid.data4[3],
        guid.data4[4],
        guid.data4[5],
        guid.data4[6],
        guid.data4[7]
    )
}

/// Parses `{xxxxxxxx-xxxx-...}` or `xxxxxxxx-xxxx-...`.
pub fn parse_guid(text: &str) -> HalResult<GUID> {
    let trimmed = text.trim().trim_start_matches('{').trim_end_matches('}');
    let parts: Vec<&str> = trimmed.split('-').collect();
    if parts.len() != 5 {
        return Err(HalError::io(format!("'{text}' is not a GUID")));
    }
    let data1 = u32::from_str_radix(parts[0], 16)
        .map_err(|_| HalError::io(format!("'{text}' is not a GUID")))?;
    let data2 = u16::from_str_radix(parts[1], 16)
        .map_err(|_| HalError::io(format!("'{text}' is not a GUID")))?;
    let data3 = u16::from_str_radix(parts[2], 16)
        .map_err(|_| HalError::io(format!("'{text}' is not a GUID")))?;
    if parts[3].len() != 4 || parts[4].len() != 12 {
        return Err(HalError::io(format!("'{text}' is not a GUID")));
    }
    let mut data4 = [0u8; 8];
    for (index, slot) in data4.iter_mut().take(2).enumerate() {
        *slot = u8::from_str_radix(&parts[3][index * 2..index * 2 + 2], 16)
            .map_err(|_| HalError::io(format!("'{text}' is not a GUID")))?;
    }
    for (index, slot) in data4[2..].iter_mut().enumerate() {
        *slot = u8::from_str_radix(&parts[4][index * 2..index * 2 + 2], 16)
            .map_err(|_| HalError::io(format!("'{text}' is not a GUID")))?;
    }
    Ok(GUID {
        data1,
        data2,
        data3,
        data4,
    })
}

// -------------------------------------------------------------- power schemes

/// `POWER_DATA_ACCESSOR_ACCESS_SCHEME` is not re-exported by the `windows`
/// crate for this version, so we spell out its documented value.
const ACCESS_SCHEME: POWER_DATA_ACCESSOR = POWER_DATA_ACCESSOR(16);

/// The active power scheme and its friendly name.
pub fn active_power_scheme() -> HalResult<(GUID, String)> {
    let mut raw: *mut GUID = std::ptr::null_mut();
    // SAFETY: null root key selects the current user's power store; `raw` is a
    // valid out-pointer and the GUID it points at is copied before the block is
    // released below.
    let status = unsafe { PowerGetActiveScheme(None, &mut raw) };
    if status.0 != 0 || raw.is_null() {
        return Err(HalError::unavailable(format!(
            "the active power scheme could not be read (Win32 error {})",
            status.0
        )));
    }
    // SAFETY: the pointer came from `PowerGetActiveScheme`, which allocates the
    // GUID with `LocalAlloc`; it is freed after the copy.
    let guid = unsafe {
        let copy = *raw;
        let _ = windows::Win32::Foundation::LocalFree(Some(windows::Win32::Foundation::HLOCAL(
            raw as *mut _,
        )));
        copy
    };

    let name = power_scheme_name(&guid).unwrap_or_else(|| guid_string(&guid));
    Ok((guid, name))
}

/// Friendly name of a power scheme (localised, as Windows shows it).
pub fn power_scheme_name(guid: &GUID) -> Option<String> {
    let mut size: u32 = 0;
    // SAFETY: `guid` points at a live GUID and `size` is a valid in/out
    // pointer; the null buffer is the documented sizing call.
    let _ = unsafe { PowerReadFriendlyName(None, Some(guid), None, None, None, &mut size) };
    if size == 0 || size > 4_096 {
        return None;
    }
    let mut buffer = vec![0u8; size as usize];
    // SAFETY: the buffer is exactly the size the API just asked for.
    let status = unsafe {
        PowerReadFriendlyName(
            None,
            Some(guid),
            None,
            None,
            Some(buffer.as_mut_ptr()),
            &mut size,
        )
    };
    if status.0 != 0 {
        return None;
    }
    Some(wide_bytes_to_string(&buffer))
}

/// Every power scheme registered for the current user.
pub fn power_schemes() -> HalResult<Vec<GUID>> {
    let mut out = Vec::new();
    let mut index = 0u32;
    loop {
        let mut size: u32 = 0;
        // SAFETY: a null buffer is the documented sizing call and
        // `ACCESS_SCHEME` restricts the enumeration to top-level schemes.
        let status =
            unsafe { PowerEnumerate(None, None, None, ACCESS_SCHEME, index, None, &mut size) };
        // A null buffer is a sizing call: ERROR_MORE_DATA is expected.
        if (status.0 != 0 && status.0 != 234) || size < std::mem::size_of::<GUID>() as u32 {
            break;
        }
        let mut buffer = vec![0u8; size as usize];
        // SAFETY: the buffer is exactly `size` bytes; a GUID is read out of it
        // with `read_unaligned` below, so alignment is not assumed.
        let status = unsafe {
            PowerEnumerate(
                None,
                None,
                None,
                ACCESS_SCHEME,
                index,
                Some(buffer.as_mut_ptr()),
                &mut size,
            )
        };
        if status.0 != 0 {
            break;
        }
        // SAFETY: the API wrote a GUID into a buffer of at least GUID size.
        out.push(unsafe { std::ptr::read_unaligned(buffer.as_ptr() as *const GUID) });
        index += 1;
        if out.len() > 64 {
            break;
        }
    }
    if out.is_empty() {
        return Err(HalError::unavailable("no power schemes are registered"));
    }
    Ok(out)
}

/// Switches the active power scheme.
pub fn set_active_power_scheme(guid: &GUID) -> HalResult<()> {
    // SAFETY: `guid` points at a live GUID for the duration of the call and the
    // root key is intentionally null (current user).
    let status = unsafe { PowerSetActiveScheme(None, Some(guid)) };
    if status.0 != 0 {
        return Err(HalError::io(format!(
            "the power scheme could not be activated (Win32 error {})",
            status.0
        )));
    }
    Ok(())
}

// ------------------------------------------------------------------- display

/// Encodes a `PCWSTR` argument from a Rust string.
fn wide_z(text: &str) -> Vec<u16> {
    text.encode_utf16().chain(std::iter::once(0)).collect()
}

/// Turns a fixed-size UTF-16 field into a `String`.
fn wide_field_to_string(field: &[u16]) -> String {
    let end = field.iter().position(|c| *c == 0).unwrap_or(field.len());
    String::from_utf16_lossy(&field[..end])
}

/// Interprets a byte buffer that the power APIs filled with UTF-16 text.
fn wide_bytes_to_string(bytes: &[u8]) -> String {
    let units: Vec<u16> = bytes
        .chunks_exact(2)
        .map(|pair| u16::from_ne_bytes([pair[0], pair[1]]))
        .collect();
    wide_field_to_string(&units)
}

/// One connected display with the refresh rates it supports.
#[derive(Debug, Clone, PartialEq)]
pub struct DisplayMode {
    pub device_name: String,
    pub friendly_name: String,
    pub is_internal: bool,
    pub current_hz: u32,
    pub available_hz: Vec<u32>,
}

/// Group active targets by GDI source. A cloned external target makes the
/// whole source ineligible for automatic changes, since GDI changes both.
fn internal_display_sources() -> HalResult<HashMap<String, bool>> {
    for _ in 0..3 {
        let (mut path_count, mut mode_count) = (0, 0);
        // SAFETY: valid count outputs; only active paths are requested.
        unsafe {
            GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, &mut path_count, &mut mode_count)
        }
        .ok()
        .map_err(|e| HalError::unavailable(e.to_string()))?;
        let mut paths = vec![DISPLAYCONFIG_PATH_INFO::default(); path_count as usize];
        let mut modes = vec![DISPLAYCONFIG_MODE_INFO::default(); mode_count as usize];
        // SAFETY: arrays have exactly the sizes returned by Windows. Counts
        // remain in/out parameters; topology changes trigger a bounded retry.
        let result = unsafe {
            QueryDisplayConfig(
                QDC_ONLY_ACTIVE_PATHS,
                &mut path_count,
                paths.as_mut_ptr(),
                &mut mode_count,
                modes.as_mut_ptr(),
                None,
            )
        };
        if result == windows::Win32::Foundation::ERROR_INSUFFICIENT_BUFFER {
            continue;
        }
        result
            .ok()
            .map_err(|e| HalError::unavailable(e.to_string()))?;
        let mut sources = HashMap::new();
        for path in paths.iter().take(path_count as usize) {
            let mut name = DISPLAYCONFIG_SOURCE_DEVICE_NAME::default();
            name.header.r#type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
            name.header.size = std::mem::size_of_val(&name) as u32;
            name.header.adapterId = path.sourceInfo.adapterId;
            name.header.id = path.sourceInfo.id;
            // SAFETY: the header describes the full initialized source-name
            // buffer and the adapter/id originate from an active path.
            let result = unsafe { DisplayConfigGetDeviceInfo(&mut name.header) };
            if result != 0 {
                return Err(HalError::unavailable(format!(
                    "display source lookup failed: {result}"
                )));
            }
            let technology = path.targetInfo.outputTechnology;
            let internal = matches!(
                technology,
                DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL
                    | DISPLAYCONFIG_OUTPUT_TECHNOLOGY_LVDS
                    | DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EMBEDDED
                    | DISPLAYCONFIG_OUTPUT_TECHNOLOGY_UDI_EMBEDDED
            );
            let source = wide_field_to_string(&name.viewGdiDeviceName).to_ascii_lowercase();
            sources
                .entry(source)
                .and_modify(|value| *value &= internal)
                .or_insert(internal);
        }
        return Ok(sources);
    }
    Err(HalError::unavailable("display topology kept changing"))
}

/// Enumerates GDI display devices and the modes each one reports.
pub fn displays() -> HalResult<Vec<DisplayMode>> {
    // An unavailable topology must never cause an external monitor to be
    // guessed as internal. Manual display controls remain usable.
    let internal_sources = internal_display_sources().unwrap_or_default();
    let mut out = Vec::new();
    let mut index = 0u32;
    loop {
        let mut device = DISPLAY_DEVICEW {
            cb: std::mem::size_of::<DISPLAY_DEVICEW>() as u32,
            ..Default::default()
        };
        // SAFETY: `device.cb` was set to the struct size, which is the
        // documented contract, and `index` walks the adapter list.
        let adapter = unsafe { EnumDisplayDevicesW(PCWSTR::null(), index, &mut device, 0) };
        if !adapter.as_bool() {
            break;
        }
        index += 1;
        let device_name = wide_field_to_string(&device.DeviceName);
        let friendly_name = wide_field_to_string(&device.DeviceString);
        // Only active adapters expose usable modes.
        if !device.StateFlags.contains(DISPLAY_DEVICE_ACTIVE) {
            continue;
        }

        let rates = display_rates(&device_name);
        let mode = current_mode(&device_name);
        out.push(DisplayMode {
            is_internal: internal_sources
                .get(&device_name.to_ascii_lowercase())
                .copied()
                .unwrap_or(false),
            device_name,
            friendly_name,
            current_hz: mode,
            available_hz: rates,
        });
        if out.len() >= 8 {
            break;
        }
    }
    if out.is_empty() {
        return Err(HalError::unavailable("no active display adapter was found"));
    }
    Ok(out)
}

/// The refresh rate a display is currently running at.
pub fn current_mode(device_name: &str) -> u32 {
    let name_z = wide_z(device_name);
    let mut mode = DEVMODEW {
        dmSize: std::mem::size_of::<DEVMODEW>() as u16,
        ..Default::default()
    };
    // SAFETY: `dmSize` is set and `name_z` is NUL-terminated.
    if unsafe { EnumDisplaySettingsW(PCWSTR(name_z.as_ptr()), ENUM_CURRENT_SETTINGS, &mut mode) }
        .as_bool()
    {
        mode.dmDisplayFrequency
    } else {
        0
    }
}

/// The refresh rates a single display reports, sorted ascending.
pub fn display_rates(device_name: &str) -> Vec<u32> {
    let name_z = wide_z(device_name);
    let mut current = DEVMODEW {
        dmSize: std::mem::size_of::<DEVMODEW>() as u16,
        ..Default::default()
    };
    // SAFETY: initialized output struct and terminated device name.
    if !unsafe {
        EnumDisplaySettingsW(PCWSTR(name_z.as_ptr()), ENUM_CURRENT_SETTINGS, &mut current)
    }
    .as_bool()
    {
        return Vec::new();
    }
    let mut rates = Vec::new();
    let mut index = 0u32;
    loop {
        let mut mode = DEVMODEW {
            dmSize: std::mem::size_of::<DEVMODEW>() as u16,
            ..Default::default()
        };
        // SAFETY: `dmSize` is set and `name_z` is NUL-terminated.
        let ok = unsafe {
            EnumDisplaySettingsW(
                PCWSTR(name_z.as_ptr()),
                ENUM_DISPLAY_SETTINGS_MODE(index),
                &mut mode,
            )
        };
        if !ok.as_bool() || index > 512 {
            break;
        }
        index += 1;
        let hz = mode.dmDisplayFrequency;
        if hz > 1 && same_display_dimensions(&mode, &current) && !rates.contains(&hz) {
            rates.push(hz);
        }
    }
    // Some drivers accept intermediate rates without enumerating them. Test
    // only the requested 90/120 Hz candidates, at the unchanged resolution.
    for hz in [90, 120] {
        if !rates.contains(&hz) && test_refresh_rate(&name_z, &current, hz) {
            rates.push(hz);
        }
    }
    rates.sort_unstable();
    rates
}

fn same_display_dimensions(mode: &DEVMODEW, current: &DEVMODEW) -> bool {
    mode.dmPelsWidth == current.dmPelsWidth
        && mode.dmPelsHeight == current.dmPelsHeight
        && mode.dmBitsPerPel == current.dmBitsPerPel
}

fn test_refresh_rate(name_z: &[u16], current: &DEVMODEW, hz: u32) -> bool {
    let mut proposed = *current;
    proposed.dmDisplayFrequency = hz;
    proposed.dmFields |= DM_DISPLAYFREQUENCY;
    // SAFETY: valid mode copied from EnumDisplaySettings, retained dimensions.
    // CDS_TEST only validates; it neither applies a mode nor writes the registry.
    unsafe {
        ChangeDisplaySettingsExW(
            PCWSTR(name_z.as_ptr()),
            Some(&proposed),
            None,
            CDS_TEST,
            None,
        ) == DISP_CHANGE_SUCCESSFUL
    }
}

#[cfg(test)]
mod display_mode_tests {
    use super::*;
    #[test]
    fn other_resolution_or_color_depth_is_not_a_refresh_choice() {
        let current = DEVMODEW {
            dmPelsWidth: 2560,
            dmPelsHeight: 1600,
            dmBitsPerPel: 32,
            dmDisplayFrequency: 240,
            ..Default::default()
        };
        let mut proposed = current;
        proposed.dmDisplayFrequency = 120;
        assert!(same_display_dimensions(&proposed, &current));
        proposed.dmPelsWidth = 1920;
        assert!(!same_display_dimensions(&proposed, &current));
        proposed = current;
        proposed.dmPelsHeight = 1080;
        assert!(!same_display_dimensions(&proposed, &current));
        proposed = current;
        proposed.dmBitsPerPel = 16;
        assert!(!same_display_dimensions(&proposed, &current));
    }
}

/// Changes the refresh rate of one display, keeping the current resolution.
///
/// The requested rate is validated against what the panel reports *before* the
/// mode change, so an unsupported value produces a typed "not supported" error
/// instead of a black screen.
pub fn set_refresh_rate(device_name: &str, hz: u32) -> HalResult<()> {
    apply_refresh_rate(device_name, hz, false)
}

pub fn set_internal_refresh_rate(device_name: &str, hz: u32) -> HalResult<()> {
    apply_refresh_rate(device_name, hz, true)
}

fn apply_refresh_rate(device_name: &str, hz: u32, internal_only: bool) -> HalResult<()> {
    let name_z = wide_z(device_name);
    let mut current = DEVMODEW {
        dmSize: std::mem::size_of::<DEVMODEW>() as u16,
        ..Default::default()
    };
    // SAFETY: `dmSize` is set and `name_z` is NUL-terminated.
    let readable = unsafe {
        EnumDisplaySettingsW(PCWSTR(name_z.as_ptr()), ENUM_CURRENT_SETTINGS, &mut current)
    };
    if !readable.as_bool() {
        return Err(HalError::unavailable(format!(
            "the current mode of {device_name} could not be read"
        )));
    }

    let available = display_rates(device_name);
    if !available.contains(&hz) {
        return Err(HalError::unsupported(format!(
            "{device_name} does not offer {hz} Hz (available: {})",
            available
                .iter()
                .map(|value| value.to_string())
                .collect::<Vec<_>>()
                .join(", ")
        )));
    }

    if !test_refresh_rate(&name_z, &current, hz) {
        return Err(HalError::unsupported(format!(
            "当前分辨率下驱动不接受 {hz} Hz"
        )));
    }
    if internal_only
        && !internal_display_sources()?
            .get(&device_name.to_ascii_lowercase())
            .copied()
            .unwrap_or(false)
    {
        return Err(HalError::unsupported(
            "自动刷新率仅适用于独立内屏；显示拓扑已变化",
        ));
    }
    current.dmDisplayFrequency = hz;
    current.dmFields |= DM_DISPLAYFREQUENCY;
    // SAFETY: `current` is a fully populated DEVMODEW, the device name is
    // NUL-terminated and `CDS_UPDATEREGISTRY` is the documented flag for a
    // persistent, non-interactive change.
    let result = unsafe {
        ChangeDisplaySettingsExW(
            PCWSTR(name_z.as_ptr()),
            Some(&current),
            None,
            CDS_UPDATEREGISTRY,
            None,
        )
    };
    if result != DISP_CHANGE_SUCCESSFUL {
        return Err(HalError::io(format!(
            "Windows refused the mode change to {hz} Hz (code {})",
            result.0
        )));
    }
    Ok(())
}

// ----------------------------------------------------------------- backlight

/// Active WMI panel instance, never the external display selected in the UI.
fn brightness_panel(wmi: &Wmi) -> HalResult<WmiRow> {
    let panels = wmi.query("SELECT * FROM WmiMonitorBrightness WHERE Active = TRUE")?;
    match panels.len() {
        1 => Ok(panels.into_iter().next().unwrap()),
        0 => Err(HalError::unavailable("Windows 未提供活动内置屏幕亮度接口")),
        _ => Err(HalError::unsupported(
            "检测到多个背光设备，无法确定内置屏幕",
        )),
    }
}

pub fn display_brightness() -> HalResult<u32> {
    let wmi = Wmi::connect(r"ROOT\WMI")?;
    let panel = brightness_panel(&wmi)?;
    let level = panel
        .u32_of("CurrentBrightness")
        .filter(|value| *value <= 100)
        .ok_or_else(|| HalError::unavailable("内置屏幕返回了无效亮度"))?;
    Ok(level)
}

pub fn set_display_brightness(percent: u32) -> HalResult<()> {
    if percent > 100 {
        return Err(HalError::unsupported("亮度必须在 0–100% 之间"));
    }
    let wmi = Wmi::connect(r"ROOT\WMI")?;
    let panel = brightness_panel(&wmi)?;
    let name = panel
        .str_of("InstanceName")
        .ok_or_else(|| HalError::unavailable("亮度设备缺少实例标识"))?;
    let mut matches = wmi
        .query("SELECT * FROM WmiMonitorBrightnessMethods WHERE Active = TRUE")?
        .into_iter()
        .filter(|row| {
            row.str_of("InstanceName")
                .is_some_and(|candidate| candidate.eq_ignore_ascii_case(&name))
        });
    let method = matches
        .next()
        .ok_or_else(|| HalError::unavailable("当前背光设备未提供 WmiSetBrightness"))?;
    if matches.next().is_some() {
        return Err(HalError::unavailable("亮度控制实例重复"));
    }
    let path = method
        .str_of("__PATH")
        .ok_or_else(|| HalError::unavailable("亮度设备缺少 WMI 对象路径"))?;
    wmi.set_brightness(&path, percent as u8)
}

// ------------------------------------------------------------------- battery

/// What `GetSystemPowerStatus` reports.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct PowerSnapshot {
    pub on_ac: bool,
    pub percent: f64,
    pub charging: bool,
    /// `BatteryFlag == 255` means "no system battery".
    pub present: bool,
}

pub fn power_snapshot() -> HalResult<PowerSnapshot> {
    let mut status = SYSTEM_POWER_STATUS::default();
    // SAFETY: `status` is a valid, initialised out-parameter.
    unsafe { GetSystemPowerStatus(&mut status) }
        .map_err(|err| HalError::io(format!("battery state unavailable ({err})")))?;

    let present = status.BatteryFlag != 255;
    let percent = if status.BatteryLifePercent == 255 {
        0.0
    } else {
        f64::from(status.BatteryLifePercent)
    };
    Ok(PowerSnapshot {
        on_ac: status.ACLineStatus == 1,
        percent,
        charging: status.BatteryFlag & 8 != 0,
        present,
    })
}

/// Full-charge capacity of the battery, from `SystemBatteryState`.
pub fn battery_full_capacity_mwh() -> Option<u64> {
    const SYSTEM_BATTERY_STATE_LEVEL: POWER_INFORMATION_LEVEL = POWER_INFORMATION_LEVEL(5);
    let mut state = SYSTEM_BATTERY_STATE::default();
    // SAFETY: `state` is a valid out-parameter and level 5 is the documented
    // query that fills exactly this struct.
    let status = unsafe {
        CallNtPowerInformation(
            SYSTEM_BATTERY_STATE_LEVEL,
            None,
            0,
            Some(&mut state as *mut _ as *mut _),
            std::mem::size_of::<SYSTEM_BATTERY_STATE>() as u32,
        )
    };
    if status.0 < 0 || state.MaxCapacity == 0 {
        return None;
    }
    Some(u64::from(state.MaxCapacity))
}

/// Design capacity of the battery, from the ACPI `FACP` firmware table.///
/// This is the same source Windows uses for the "design capacity" it shows in
/// the battery report. A firmware that does not implement the field yields
/// `None`, and the UI then shows "unknown" rather than a fabricated number.
pub fn battery_design_capacity_mwh() -> Option<u64> {
    // 'FACP' in little-endian ("FADT" is the same table).
    const FACP_SIGNATURE: u32 = 0x5041_4346;
    const ACPI_PROVIDER: FIRMWARE_TABLE_PROVIDER = FIRMWARE_TABLE_PROVIDER(0x4143_5049);
    // SAFETY: a null buffer is the documented sizing call.
    let size = unsafe { GetSystemFirmwareTable(ACPI_PROVIDER, FACP_SIGNATURE, None) };
    if !(64..=1024 * 1024).contains(&size) {
        return None;
    }
    let mut buffer = vec![0u8; size as usize];
    // SAFETY: the buffer is exactly the size the API just reported.
    let written =
        unsafe { GetSystemFirmwareTable(ACPI_PROVIDER, FACP_SIGNATURE, Some(&mut buffer)) };
    if written < 64 || (written as usize) > buffer.len() || buffer[0..4] != *b"FACP" {
        return None;
    }
    // FADT revision 1+ stores "Battery Design Capacity" in mWh at offset 0x6C.
    const OFFSET: usize = 0x6C;
    if buffer.len() < OFFSET + 4 {
        return None;
    }
    let capacity = u32::from_le_bytes([
        buffer[OFFSET],
        buffer[OFFSET + 1],
        buffer[OFFSET + 2],
        buffer[OFFSET + 3],
    ]);
    // 0 and 0xFFFF_FFFF are the "not implemented" sentinels.
    if capacity == 0 || capacity == u32::MAX {
        None
    } else {
        Some(u64::from(capacity))
    }
}

// ------------------------------------------------------------------- display

/// Dedicated video memory of a named adapter, in bytes, from the display class
/// key.
///
/// `Win32_VideoController.AdapterRAM` is a `uint32` and every driver clamps it
/// at 4 GiB, so a 8 GB card reports 4095 MiB — or, for the odd driver, a
/// nonsense value entirely. The class key that the display driver installs
/// itself under carries the real size, and its `DriverDesc` identifies which
/// subkey belongs to which adapter.
pub fn video_memory_bytes(adapter_name: &str) -> Option<u64> {
    use winreg::enums::{HKEY_LOCAL_MACHINE, KEY_READ};
    use winreg::RegKey;

    const DISPLAY_CLASS: &str =
        r"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
    const VALUE: &str = "HardwareInformation.qwMemorySize";

    let class = RegKey::predef(HKEY_LOCAL_MACHINE)
        .open_subkey_with_flags(DISPLAY_CLASS, KEY_READ)
        .ok()?;

    for entry in class.enum_keys().flatten() {
        let Ok(adapter) = class.open_subkey_with_flags(&entry, KEY_READ) else {
            continue;
        };
        let Ok(description) = adapter.get_value::<String, _>("DriverDesc") else {
            continue;
        };
        if !description.eq_ignore_ascii_case(adapter_name) {
            continue;
        }
        // Drivers disagree on the type: REG_QWORD on most, an 8-byte REG_BINARY
        // on others. Accept either.
        if let Ok(bytes) = adapter.get_value::<u64, _>(VALUE) {
            if bytes > 0 {
                return Some(bytes);
            }
        }
        if let Ok(raw) = adapter.get_raw_value(VALUE) {
            if raw.bytes.len() >= 8 {
                let mut buffer = [0u8; 8];
                buffer.copy_from_slice(&raw.bytes[..8]);
                let bytes = u64::from_le_bytes(buffer);
                if bytes > 0 {
                    return Some(bytes);
                }
            }
        }
    }
    None
}

// ---------------------------------------------------------------- elevation

/// True when the current process token is elevated.
pub fn is_process_elevated() -> bool {
    let mut token = HANDLE::default();
    // SAFETY: `token` is a valid out-pointer and the pseudo-handle returned by
    // `GetCurrentProcess` is always valid for `OpenProcessToken`.
    if unsafe { OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &mut token) }.is_err() {
        return false;
    }
    let mut elevated = TOKEN_ELEVATION::default();
    let mut returned = 0u32;
    // SAFETY: `token` is a live token handle and the out-parameter is the
    // correctly sized `TOKEN_ELEVATION` struct.
    let ok = unsafe {
        GetTokenInformation(
            token,
            TokenElevation,
            Some(&mut elevated as *mut _ as *mut _),
            std::mem::size_of::<TOKEN_ELEVATION>() as u32,
            &mut returned,
        )
    };
    // SAFETY: the token was opened above and is no longer needed.
    unsafe {
        let _ = CloseHandle(token);
    }
    ok.is_ok() && elevated.TokenIsElevated != 0
}

/// Full path of the running executable.
pub fn current_exe_path() -> HalResult<std::path::PathBuf> {
    let mut buffer = vec![0u16; MAX_PATH as usize * 4];
    let mut size = buffer.len() as u32;
    // SAFETY: the buffer and its length are both valid and the buffer is large
    // enough for any realistic path; the pseudo-handle is always valid.
    unsafe {
        QueryFullProcessImageNameW(
            GetCurrentProcess(),
            PROCESS_NAME_WIN32,
            PWSTR(buffer.as_mut_ptr()),
            &mut size,
        )
    }
    .map_err(|err| HalError::io(format!("the executable path could not be read ({err})")))?;
    Ok(std::path::PathBuf::from(wide_field_to_string(
        &buffer[..size as usize],
    )))
}

/// Renders a path for the log without letting control characters through.
pub fn safe_path(path: &Path) -> String {
    path.display()
        .to_string()
        .chars()
        .map(|character| {
            if character.is_control() {
                '?'
            } else {
                character
            }
        })
        .collect()
}

// ------------------------------------------------------------------- colour

/// Parses a colour hex string into `(r, g, b)` in the 0..1 range.
pub fn hex_to_rgb01(hex: &str) -> (f64, f64, f64) {
    let normalised = crate::core::config::normalise_hex(hex);
    let digits = normalised.trim_start_matches('#');
    if digits.len() != 6 {
        return (1.0, 1.0, 1.0);
    }
    let channel = |range: std::ops::Range<usize>| {
        u8::from_str_radix(&digits[range], 16).unwrap_or(0) as f64 / 255.0
    };
    (channel(0..2), channel(2..4), channel(4..6))
}

/// Applies a gamma ramp built from per-channel gains.
///
/// `SetDeviceGammaRamp` is the documented GDI entry point for display colour
/// calibration. Gains outside `0.5..=1.5` are refused rather than clamped,
/// because an extreme ramp can leave the panel unreadable with no obvious way
/// back.
pub fn set_gamma_ramp(red: f64, green: f64, blue: f64) -> HalResult<()> {
    let gains = [red, green, blue];
    if gains.iter().any(|gain| !(0.5..=1.5).contains(gain)) {
        return Err(HalError::unsupported(
            "colour gains outside 0.5..1.5 are refused to keep the panel readable",
        ));
    }

    // SAFETY: `GetDC(None)` returns the screen DC or null; the null case is
    // handled immediately below.
    let hdc = unsafe { windows::Win32::Graphics::Gdi::GetDC(None) };
    if hdc.is_invalid() {
        return Err(HalError::unavailable(
            "the screen device context is not available",
        ));
    }

    let mut ramp = [[0u16; 256]; 3];
    for (channel, gain) in gains.iter().enumerate() {
        for (index, slot) in ramp[channel].iter_mut().enumerate() {
            *slot = ((index as f64 / 255.0) * gain * 65_535.0).clamp(0.0, 65_535.0) as u16;
        }
    }

    // SAFETY: `ramp` is a stack array of exactly 3 * 256 16-bit entries, which
    // is the layout `SetDeviceGammaRamp` documents, and `hdc` is a live DC.
    let ok = unsafe {
        SetDeviceGammaRamp(
            hdc,
            &ramp as *const [[u16; 256]; 3] as *const core::ffi::c_void,
        )
    };
    // SAFETY: releases the DC acquired above.
    unsafe {
        windows::Win32::Graphics::Gdi::ReleaseDC(None, hdc);
    }
    if !ok.as_bool() {
        return Err(HalError::io("the display driver rejected the gamma ramp"));
    }
    Ok(())
}

/// Placeholder so the module always references `HWND` (used by the OSD code).
pub fn _unused_hwnd(_window: HWND) {}
