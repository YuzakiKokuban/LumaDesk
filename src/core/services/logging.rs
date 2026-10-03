//! Append-only boot log.
//!
//! The original JiYaoChu wrote a very chatty, step-by-step boot log to
//! `%APPDATA%\JiYaoChu\boot.log` and that file is the only reason the OEM
//! takeover sequence could be reconstructed at all. We keep the same shape:
//! every mutation of the system is written down *before* it is attempted and
//! the outcome is appended afterwards, so the log is a usable audit trail and
//! doubles as the recipe for the restore path.

use std::fs::{self, OpenOptions};
use std::io::Write;
use std::path::Path;
use std::sync::OnceLock;
use std::time::{SystemTime, UNIX_EPOCH};

/// Runtime filter for the log, mirroring the recovered `set_log_filter` /
/// `set_log_level` commands.
#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord)]
pub enum LogLevel {
    Off = 0,
    Error = 1,
    Warn = 2,
    Info = 3,
    Debug = 4,
    Trace = 5,
}

impl LogLevel {
    pub fn parse(value: &str) -> Self {
        match value.trim().to_ascii_lowercase().as_str() {
            "off" | "none" | "0" => LogLevel::Off,
            "error" | "1" => LogLevel::Error,
            "warn" | "warning" | "2" => LogLevel::Warn,
            "debug" | "4" => LogLevel::Debug,
            "trace" | "5" => LogLevel::Trace,
            _ => LogLevel::Info,
        }
    }

    /// Like [`LogLevel::parse`] but reports whether the text was recognised.
    ///
    /// `AppConfig::sanitise` uses this to rewrite a hand-edited verbosity back
    /// to the default instead of silently keeping nonsense in `config.json`.
    pub fn parse_strict(value: &str) -> Option<Self> {
        let lowered = value.trim().to_ascii_lowercase();
        match lowered.as_str() {
            "off" | "none" | "0" => Some(LogLevel::Off),
            "error" | "1" => Some(LogLevel::Error),
            "warn" | "warning" | "2" => Some(LogLevel::Warn),
            "info" | "3" | "" => Some(LogLevel::Info),
            "debug" | "4" => Some(LogLevel::Debug),
            "trace" | "5" => Some(LogLevel::Trace),
            _ => None,
        }
    }

    pub fn as_str(self) -> &'static str {
        match self {
            LogLevel::Off => "off",
            LogLevel::Error => "error",
            LogLevel::Warn => "warn",
            LogLevel::Info => "info",
            LogLevel::Debug => "debug",
            LogLevel::Trace => "trace",
        }
    }

    fn tag(self) -> &'static str {
        match self {
            LogLevel::Off => "OFF  ",
            LogLevel::Error => "ERROR",
            LogLevel::Warn => "WARN ",
            LogLevel::Info => "INFO ",
            LogLevel::Debug => "DEBUG",
            LogLevel::Trace => "TRACE",
        }
    }
}

/// Process-wide logging configuration. `enabled` is driven by
/// `AppConfig::log_enabled`; `level` by `set_log_level`.
#[derive(Debug, Clone, Copy)]
pub struct LogSettings {
    pub enabled: bool,
    pub level: LogLevel,
}

static SETTINGS: OnceLock<std::sync::Mutex<LogSettings>> = OnceLock::new();

/// Optional free-text filter applied to every message.
///
/// `set_log_filter` in the original surface let the user narrow a very chatty
/// log down to one subsystem; an empty filter keeps everything. Matching is a
/// case-insensitive substring test, which is what the in-app viewer's search
/// box does too.
static FILTER: OnceLock<std::sync::Mutex<String>> = OnceLock::new();

fn settings() -> &'static std::sync::Mutex<LogSettings> {
    SETTINGS.get_or_init(|| {
        std::sync::Mutex::new(LogSettings {
            enabled: true,
            level: LogLevel::Info,
        })
    })
}

fn filter_slot() -> &'static std::sync::Mutex<String> {
    FILTER.get_or_init(|| std::sync::Mutex::new(String::new()))
}

/// Replaces the free-text filter.
pub fn set_filter(filter: &str) {
    if let Ok(mut guard) = filter_slot().lock() {
        *guard = filter.trim().to_string();
    }
}

/// The current free-text filter.
pub fn filter() -> String {
    match filter_slot().lock() {
        Ok(guard) => guard.clone(),
        Err(_) => String::new(),
    }
}

/// True when `message` passes the filter.
fn passes_filter(message: &str) -> bool {
    match filter_slot().lock() {
        Ok(guard) if !guard.is_empty() => message
            .to_ascii_lowercase()
            .contains(&guard.to_ascii_lowercase()),
        _ => true,
    }
}

/// Applies the persisted log settings.
pub fn configure(enabled: bool, level: LogLevel) {
    if let Ok(mut guard) = settings().lock() {
        guard.enabled = enabled;
        guard.level = level;
    }
}

/// Current settings, as `(enabled, level)`.
pub fn current() -> (bool, LogLevel) {
    match settings().lock() {
        Ok(guard) => (guard.enabled, guard.level),
        Err(_) => (true, LogLevel::Info),
    }
}

/// Path of the active log file.
pub fn path() -> std::path::PathBuf {
    crate::core::config::log_path()
}

fn timestamp() -> String {
    let now = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default();
    let secs = now.as_secs();
    let millis = now.subsec_millis();
    // Format as a local-ish ISO-8601 stamp. We deliberately avoid pulling in a
    // full timezone database: the boot log is read by humans, not parsed.
    let (y, mo, d, h, mi, s) = civil_from_unix(secs as i64);
    format!("{y:04}-{mo:02}-{d:02}T{h:02}:{mi:02}:{s:02}.{millis:03}Z")
}

/// Days-to-civil conversion (Howard Hinnant's algorithm), UTC.
fn civil_from_unix(unix: i64) -> (i64, u32, u32, u32, u32, u32) {
    let days = unix.div_euclid(86_400);
    let secs_of_day = unix.rem_euclid(86_400);
    let z = days + 719_468;
    let era = if z >= 0 { z } else { z - 146_096 } / 146_097;
    let doe = (z - era * 146_097) as u64;
    let yoe = (doe - doe / 1460 + doe / 36_524 - doe / 146_096) / 365;
    let y = yoe as i64 + era * 400;
    let doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
    let mp = (5 * doy + 2) / 153;
    let d = (doy - (153 * mp + 2) / 5 + 1) as u32;
    let m = if mp < 10 { mp + 3 } else { mp - 9 } as u32;
    let y = if m <= 2 { y + 1 } else { y };
    (
        y,
        m,
        d,
        (secs_of_day / 3600) as u32,
        ((secs_of_day % 3600) / 60) as u32,
        (secs_of_day % 60) as u32,
    )
}

/// Writes one line to `boot.log` (best effort — logging must never fail a
/// command).
pub fn write(level: LogLevel, message: &str) {
    let (enabled, threshold) = current();
    if !enabled || level > threshold {
        return;
    }
    if !passes_filter(message) {
        return;
    }
    let path = path();
    if let Some(parent) = path.parent() {
        let _ = fs::create_dir_all(parent);
    }
    let Ok(lock) = OpenOptions::new()
        .create(true)
        .truncate(false)
        .read(true)
        .write(true)
        .open(path.with_extension("log.lock"))
    else {
        return;
    };
    if lock.lock().is_err() {
        return;
    }
    rotate();
    let line = format!("[{}] [{}] {}\n", timestamp(), level.tag(), message);
    let _ = OpenOptions::new()
        .create(true)
        .append(true)
        .open(&path)
        .and_then(|mut f| f.write_all(line.as_bytes()));
}

/// Retains five bounded log segments, including previous application runs.
pub fn rotate() {
    let path = path();
    if let Some(parent) = path.parent() {
        let _ = fs::create_dir_all(parent);
    }
    if fs::metadata(&path).is_ok_and(|meta| meta.len() > 2 * 1024 * 1024) {
        for index in (1..=4).rev() {
            let source = if index == 1 {
                path.clone()
            } else {
                path.with_extension(format!("log.{}", index - 1))
            };
            let destination = path.with_extension(format!("log.{index}"));
            if source.exists() {
                let _ = fs::rename(source, destination);
            }
        }
    }
}

/// Reads the tail of the log for the in-app log viewer.
pub fn tail(max_bytes: usize) -> String {
    let path = path();
    let Ok(text) = fs::read_to_string(&path) else {
        return String::new();
    };
    if text.len() <= max_bytes {
        return text;
    }
    // Cut on a char boundary so we never produce invalid UTF-8.
    let mut start = text.len() - max_bytes;
    while start < text.len() && !text.is_char_boundary(start) {
        start += 1;
    }
    format!("… (truncated)\n{}", &text[start..])
}

pub fn info(message: impl AsRef<str>) {
    write(LogLevel::Info, message.as_ref());
}

pub fn warn(message: impl AsRef<str>) {
    write(LogLevel::Warn, message.as_ref());
}

pub fn error(message: impl AsRef<str>) {
    write(LogLevel::Error, message.as_ref());
}

pub fn debug(message: impl AsRef<str>) {
    write(LogLevel::Debug, message.as_ref());
}

/// Ensures the data directory exists and starts a fresh log file.
pub fn init() {
    let _ = crate::core::config::ensure_dir(&crate::core::config::data_dir());
    info("=== JiYaoChu control center starting ===");
}

/// Seconds since the Unix epoch, saturating at 0 on a nonsensical clock.
///
/// Used to build default file names (`custom_1727…`) without pulling a date
/// library into every call site.
pub fn unix_seconds() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|duration| duration.as_secs())
        .unwrap_or(0)
}

/// Opens the log file with the OS default handler.
pub fn open_in_shell() -> Result<(), crate::core::error::HalError> {
    let path = path();
    if !path.exists() {
        return Err(crate::core::error::HalError::unavailable(format!(
            "log file {} does not exist yet",
            path.display()
        )));
    }
    open_path(&path)
}

/// Opens an arbitrary path with the OS default handler (notepad for `.json`,
/// the default text editor for `.log`, ...).
pub fn open_path(path: &Path) -> Result<(), crate::core::error::HalError> {
    // Only the Windows arm below has a shell to hand the path to.
    #[cfg(not(windows))]
    let _ = path;
    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        // `cmd /c start "" <path>` uses the shell association table. We pass the
        // path as a separate argv entry so no quoting/injection is possible.
        const CREATE_NO_WINDOW: u32 = 0x0800_0000;
        std::process::Command::new("cmd")
            .args(["/C", "start", ""])
            .arg(path)
            .creation_flags(CREATE_NO_WINDOW)
            .spawn()
            .map_err(|e| {
                crate::core::error::HalError::io(format!("could not open {}: {e}", path.display()))
            })?;
        Ok(())
    }
    #[cfg(not(windows))]
    {
        Err(crate::core::error::HalError::unsupported(
            "opening files with the shell is only implemented on Windows",
        ))
    }
}
