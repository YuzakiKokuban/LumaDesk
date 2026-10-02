//! UniWill EC transport recovered from the installed OEM driver and OpenRevo 0.8.5.
//! Device: \\.\ACPIDriver. Read: 0x9C40A488; write: 0x9C40A48C.
//! Requests contain little-endian DWORDs. Writes are read back and checked.
//! See reverse/native/REPORT.md for evidence and firmware limitations.

use crate::core::error::{HalError, HalResult};
use std::fs::OpenOptions;
use std::io::Write;
use std::path::PathBuf;
use std::sync::Mutex;

pub const IOCTL_EC_READ: u32 = 0x9C40A488;
pub const IOCTL_EC_WRITE: u32 = 0x9C40A48C;

/// Device paths the vendor ACPI bridge has been seen under, best first.
///
/// `\DosDevices\ACPIDriver` is the symbolic link created by the
/// `UWACPIDriver.sys` installed on this machine (the name is embedded in that
/// driver); `\\.\ACPIH` is retained only as a compatibility probe. Probing
/// both keeps the verdict a statement about the machine instead of about which
/// driver revision happens to be present.
pub const ACPI_DEVICE_PATHS: &[&str] = &[r"\\.\ACPIDriver", r"\\.\ACPIH"];

/// The device path named in diagnostics and error text.
pub const ACPI_DEVICE_PATH: &str = r"\\.\ACPIDriver";

/// Hardware id of the UniWill ACPI device node.
pub const ACPI_HARDWARE_ID: &str = r"ACPI\INOU0000";

/// Registry keys whose presence means the driver service is installed.
#[cfg(windows)]
const DRIVER_SERVICE_KEYS: &[&str] = &[
    r"SYSTEM\CurrentControlSet\Services\UWACPIDriver",
    r"SYSTEM\CurrentControlSet\Services\ACPIH",
];

/// Registry key whose presence means the device node was enumerated.
#[cfg(windows)]
const DEVICE_ENUM_KEY: &str = r"SYSTEM\CurrentControlSet\Enum\ACPI\INOU0000";

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum Probe {
    /// Not probed yet.
    Unknown,
    /// A candidate device path opened for read/write.
    DeviceOpen(&'static str),
    /// Driver/device node present, but none of the device paths opened.
    PresentNotOpen,
    /// Nothing found at all — a stock machine without the OEM driver.
    Absent,
}

impl Probe {
    fn is_open(self) -> bool {
        matches!(self, Probe::DeviceOpen(_))
    }

    /// The path that opened, if one did.
    fn path(self) -> Option<&'static str> {
        match self {
            Probe::DeviceOpen(path) => Some(path),
            _ => None,
        }
    }

    fn is_present(self) -> bool {
        matches!(self, Probe::DeviceOpen(_) | Probe::PresentNotOpen)
    }
}

/// Snapshot handed to the UI and the boot log.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct AcpiStatus {
    /// A candidate device path accepted a read/write open.
    pub device_reachable: bool,
    /// Driver service or device node installed, whether or not it opened.
    pub driver_installed: bool,
    /// Which path opened, when one did.
    pub device_path: Option<String>,
    /// Human-readable summary, safe to log.
    pub detail: String,
}

impl AcpiStatus {
    /// One-line description used in "not supported" error reasons.
    pub fn summary(&self) -> String {
        if let Some(path) = self.device_path.as_deref() {
            format!("{path} is reachable")
        } else if self.driver_installed {
            format!(
                "{} is installed but none of {} opened",
                ACPI_HARDWARE_ID,
                ACPI_DEVICE_PATHS.join(", ")
            )
        } else {
            format!("the UniWill driver for {ACPI_HARDWARE_ID} is not installed")
        }
    }
}

/// The ACPI bridge, probed once and then cached.
///
/// The probe runs on first use (or explicitly through [`AcpiDriver::probe`]) and
/// its verdict is cached for the lifetime of the process. The device is *not*
/// held open: nothing here can issue IOCTLs yet, so keeping a handle would only
/// block other tools. The handle is opened lazily by future protocol work.
#[derive(Debug)]
pub struct AcpiDriver {
    io: Mutex<()>,
    probe: Mutex<Probe>,
    /// Extra diagnostics gathered during probing, for the boot log.
    notes: Mutex<Vec<String>>,
    log_path: Option<PathBuf>,
}

impl Default for AcpiDriver {
    fn default() -> Self {
        Self::new()
    }
}

impl AcpiDriver {
    /// Serialize complete read/modify/write sequences within this process.
    pub fn transaction<T>(&self, work: impl FnOnce(&mut EcSession) -> HalResult<T>) -> HalResult<T> {
        let _guard = self.io.lock().unwrap_or_else(|e| e.into_inner());
        #[cfg(windows)]
        {
            use std::os::windows::fs::OpenOptionsExt;
            let status = self.status();
            let path = status.device_path.as_deref().ok_or_else(|| HalError::unavailable(status.summary()))?;
            let file = OpenOptions::new().read(true).write(true).share_mode(3).open(path)?;
            work(&mut EcSession { file })
        }
        #[cfg(not(windows))]
        {
            let _ = work;
            Err(HalError::unavailable("EC access requires Windows"))
        }
    }

    pub fn read_ec(&self, address: u16) -> HalResult<u8> {
        self.transaction(|ec| ec.read(address))
    }

    /// Creates an unprobed driver handle.
    pub fn new() -> Self {
        AcpiDriver {
            io: Mutex::new(()),
            probe: Mutex::new(Probe::Unknown),
            notes: Mutex::new(Vec::new()),
            log_path: None,
        }
    }

    /// Creates a handle that mirrors its findings into the boot log.
    pub fn with_log(log_path: PathBuf) -> Self {
        AcpiDriver {
            io: Mutex::new(()),
            probe: Mutex::new(Probe::Unknown),
            notes: Mutex::new(Vec::new()),
            log_path: Some(log_path),
        }
    }

    /// Runs the probe if it has not run yet and returns the cached status.
    pub fn status(&self) -> AcpiStatus {
        {
            let mut probe = self.lock_probe();
            if *probe == Probe::Unknown {
                let (result, mut notes) = probe_device();
                *probe = result;
                self.lock_notes().append(&mut notes);
                let verdict = result;
                drop(probe);
                self.log_probe(verdict);
            }
        }
        self.status_cached()
    }

    /// Forces a fresh probe, discarding the cached verdict.
    pub fn probe(&self) -> AcpiStatus {
        let (result, mut notes) = probe_device();
        *self.lock_probe() = result;
        let mut stored = self.lock_notes();
        stored.clear();
        stored.append(&mut notes);
        drop(stored);
        self.log_probe(result);
        self.status_cached()
    }

    /// True when `\\.\ACPIH` opened for read/write.
    pub fn available(&self) -> bool {
        self.status().device_reachable
    }

    /// True when the driver or its device node is installed, even if we could
    /// not open it.
    pub fn installed(&self) -> bool {
        self.status().driver_installed
    }

    /// Diagnostics collected while probing (registry hits, last error).
    pub fn notes(&self) -> Vec<String> {
        // Make sure a probe has happened before reporting notes.
        let _ = self.status();
        self.lock_notes().clone()
    }

    /// An error explaining that `capability` needs the vendor IOCTL protocol.
    ///
    /// Every feature that would have to speak to the EC uses this, so the user
    /// always gets the same shape of answer: what is missing, and why.
    pub fn protocol_unsupported(&self, capability: &str) -> HalError {
        let status = self.status();
        HalError::unsupported(format!(
            "{capability} needs the vendor IOCTL protocol of the UniWill ACPI device, \
             whose feature-specific command is not implemented in this build; {}",
            status.summary()
        ))
    }

    /// Convenience: `Err(protocol_unsupported(capability))`.
    pub fn unsupported<T>(&self, capability: &str) -> HalResult<T> {
        Err(self.protocol_unsupported(capability))
    }

    /// A short label for the boot log.
    pub fn label(&self) -> String {
        let status = self.status();
        if let Some(path) = status.device_path.as_deref() {
            format!("{path} reachable")
        } else if status.driver_installed {
            "ACPI driver installed, device not openable".to_string()
        } else {
            "ACPI driver absent".to_string()
        }
    }

    fn status_cached(&self) -> AcpiStatus {
        let probe = *self.lock_probe();
        let detail = match probe {
            Probe::DeviceOpen(path) => format!("{path} accepted a read/write open"),
            Probe::PresentNotOpen => format!(
                "{ACPI_HARDWARE_ID} is installed but none of {} opened",
                ACPI_DEVICE_PATHS.join(", ")
            ),
            Probe::Absent => format!("{ACPI_HARDWARE_ID} is not installed on this machine"),
            Probe::Unknown => "the ACPI device has not been probed yet".to_string(),
        };
        AcpiStatus {
            device_reachable: probe.is_open(),
            driver_installed: probe.is_present(),
            device_path: probe.path().map(str::to_string),
            detail,
        }
    }

    fn log_probe(&self, probe: Probe) {
        let Some(path) = self.log_path.as_ref() else {
            return;
        };
        let line = match probe {
            Probe::DeviceOpen(opened) => {
                format!("ACPI probe: {opened} opened for read/write")
            }
            Probe::PresentNotOpen => format!(
                "ACPI probe: {ACPI_HARDWARE_ID} present but none of {} openable",
                ACPI_DEVICE_PATHS.join(", ")
            ),
            Probe::Absent => format!("ACPI probe: {ACPI_HARDWARE_ID} not installed"),
            Probe::Unknown => "ACPI probe: skipped".to_string(),
        };
        // Best effort on purpose: failing to write a diagnostic must never take
        // the application down.
        if let Ok(mut file) = OpenOptions::new().create(true).append(true).open(path) {
            let _ = writeln!(file, "{line}");
        }
    }

    fn lock_probe(&self) -> std::sync::MutexGuard<'_, Probe> {
        // A poisoned lock only means another thread panicked while probing;
        // the cached verdict is still perfectly readable.
        self.probe.lock().unwrap_or_else(|poison| poison.into_inner())
    }

    fn lock_notes(&self) -> std::sync::MutexGuard<'_, Vec<String>> {
        self.notes.lock().unwrap_or_else(|poison| poison.into_inner())
    }
}

/// Opens `\\.\ACPIH` and immediately closes it again.
///
/// A successful open is the only reliable "the driver is alive" signal we can
/// get without the IOCTL protocol.
#[cfg(windows)]
fn probe_device() -> (Probe, Vec<String>) {
    use std::os::windows::fs::OpenOptionsExt;

    let mut notes = Vec::new();

    for &candidate in ACPI_DEVICE_PATHS {
        let mut options = OpenOptions::new();
        options.read(true).write(true);
        // `share_mode(0)` mirrors the exclusive access the vendor driver
        // expects; `FILE_FLAG_OVERLAPPED` is deliberately not set because no
        // asynchronous IOCTL is issued yet.
        options.share_mode(3);

        match options.open(candidate) {
            Ok(handle) => {
                notes.push(format!("{candidate} opened for read/write"));
                drop(handle);
                return (Probe::DeviceOpen(candidate), notes);
            }
            Err(error) => notes.push(format!("open of {candidate} failed: {error}")),
        }
    }

    let mut service = None;
    for key in DRIVER_SERVICE_KEYS {
        if registry_key_exists(key) {
            service = Some(*key);
            break;
        }
    }
    let node = registry_key_exists(DEVICE_ENUM_KEY);

    if let Some(key) = service {
        notes.push(format!(r"HKLM\{key} exists"));
    }
    if node {
        notes.push(format!(r"HKLM\{DEVICE_ENUM_KEY} exists"));
    }

    if service.is_some() || node {
        (Probe::PresentNotOpen, notes)
    } else {
        (Probe::Absent, notes)
    }
}

/// Non-Windows builds have no ACPI bridge at all; the probe is a no-op that
/// reports "absent" so the rest of the HAL degrades in the same way.
#[cfg(not(windows))]
fn probe_device() -> (Probe, Vec<String>) {
    (
        Probe::Absent,
        vec![format!(
            "the UniWill ACPI driver only exists on Windows ({ACPI_DEVICE_PATH} unavailable)"
        )],
    )
}

/// Checks for the existence of a registry key without reading its contents.
#[cfg(windows)]
fn registry_key_exists(path: &str) -> bool {
    use winreg::enums::{HKEY_LOCAL_MACHINE, KEY_READ};
    use winreg::RegKey;

    let hklm = RegKey::predef(HKEY_LOCAL_MACHINE);
    hklm.open_subkey_with_flags(path, KEY_READ).is_ok()
}

#[cfg(not(windows))]
fn registry_key_exists(_path: &str) -> bool {
    false
}

/// A synchronous vendor-driver session. All buffers use recovered DWORD layouts.
pub struct EcSession {
    #[cfg(windows)]
    file: std::fs::File,
}

impl EcSession {
    #[cfg(windows)]
    fn ioctl(&self, code: u32, input: &[u32]) -> HalResult<u32> {
        use std::os::windows::io::AsRawHandle;
        use windows::Win32::Foundation::HANDLE;
        use windows::Win32::System::IO::DeviceIoControl;
        let mut output = 0u32;
        let mut returned = 0;
        // SAFETY: the file owns the live handle; input/output outlive synchronous IO.
        unsafe {
            DeviceIoControl(HANDLE(self.file.as_raw_handle()), code,
                Some(input.as_ptr().cast()), std::mem::size_of_val(input) as u32,
                Some((&mut output as *mut u32).cast()), 4, Some(&mut returned), None)
        }.map_err(|e| HalError::io(format!("EC IOCTL {code:#x}: {e}")))?;
        if returned != 4 {
            return Err(HalError::io(format!("EC IOCTL returned {returned} bytes, expected 4")));
        }
        Ok(output)
    }

    pub fn read(&self, address: u16) -> HalResult<u8> {
        #[cfg(windows)]
        { self.ioctl(IOCTL_EC_READ, &[u32::from(address)]).map(|value| value as u8) }
        #[cfg(not(windows))]
        { let _ = address; Err(HalError::unavailable("EC access requires Windows")) }
    }

    /// Send a firmware command whose trigger bit may clear before it can be read.
    pub fn write_command(&self, address: u16, value: u8) -> HalResult<()> {
        #[cfg(windows)]
        { self.ioctl(IOCTL_EC_WRITE, &[u32::from(address), u32::from(value)]).map(|_| ()) }
        #[cfg(not(windows))]
        { let _ = (address, value); Err(HalError::unavailable("EC access requires Windows")) }
    }

    pub fn write_verified(&self, address: u16, value: u8) -> HalResult<()> {
        #[cfg(windows)]
        {
            self.ioctl(IOCTL_EC_WRITE, &[u32::from(address), u32::from(value)])?;
            let actual = self.read(address)?;
            if actual != value {
                return Err(HalError::io(format!("EC {address:#x}: wrote {value:#x}, read back {actual:#x}")));
            }
            Ok(())
        }
        #[cfg(not(windows))]
        { let _ = (address, value); Err(HalError::unavailable("EC access requires Windows")) }
    }

    pub fn set_bit(&self, address: u16, mask: u8, enabled: bool) -> HalResult<()> {
        let before = self.read(address)?;
        self.write_verified(address, (before & !mask) | if enabled { mask } else { 0 })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn probe_caches_and_reports_a_summary() {
        let driver = AcpiDriver::new();
        let first = driver.status();
        let second = driver.status();
        assert_eq!(first, second, "the probe verdict must be cached");
        assert!(!first.summary().is_empty());
    }

    #[test]
    fn unsupported_errors_name_the_capability() {
        let driver = AcpiDriver::new();
        let error = driver.protocol_unsupported("fan curve control");
        let text = error.to_string();
        assert!(text.contains("fan curve control"));
        assert!(text.contains("not supported on this build"));
    }
}
