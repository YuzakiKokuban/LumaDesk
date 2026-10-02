//! `jiyaochu-ctl` — a headless front end over the same C ABI the WinUI app uses.
//!
//! It exists for two reasons. First, the backend can be exercised on a machine
//! with no UI and no display session, which makes it possible to tell a backend
//! bug apart from a front-end bug. Second, it is the reference for what the C#
//! side must send: every command and every argument name printed by
//! `jiyaochu-ctl --commands` comes from the same table the dispatcher uses.
//!
//! ```text
//! jiyaochu-ctl --commands
//! jiyaochu-ctl get_hardware_status
//! jiyaochu-ctl set_power_mode '{"mode":2}'
//! jiyaochu-ctl set_osd_config -            # read the arguments from stdin
//! ```
//!
//! The process exit code is `0` when the envelope says `ok:true` and `1`
//! otherwise, so it composes with a shell.

use std::ffi::{CStr, CString};
use std::io::{self, IsTerminal, Read, Write};
use std::process::ExitCode;

use jiyaochu_core::ffi;

const USAGE: &str = "\
jiyaochu-ctl — drive the 机耀处 backend without a UI

USAGE:
    jiyaochu-ctl <command> [json-arguments]
    jiyaochu-ctl --commands
    jiyaochu-ctl --init
    jiyaochu-ctl --events
    jiyaochu-ctl --version | --help

    <json-arguments> is a JSON object whose keys are the argument names shown by
    `--commands`. Pass `-` to read it from stdin instead of the command line.

EXIT CODE:
    0  the envelope reported ok:true
    1  the envelope reported ok:false, or the tool itself failed
";

fn main() -> ExitCode {
    let args: Vec<String> = std::env::args().skip(1).collect();
    let Some(first) = args.first() else {
        print!("{USAGE}");
        return ExitCode::SUCCESS;
    };

    match first.as_str() {
        "--help" | "-h" | "help" => {
            print!("{USAGE}");
            ExitCode::SUCCESS
        }
        "--version" | "-V" => {
            println!(
                "jiyaochu-ctl {} (abi {})",
                env!("CARGO_PKG_VERSION"),
                ffi::ABI_VERSION
            );
            ExitCode::SUCCESS
        }
        "--commands" => {
            for name in ffi::COMMANDS {
                println!("{name}");
            }
            ExitCode::SUCCESS
        }
        "--init" => report(unsafe { copy_and_free(ffi::lumadesk_init()) }),
        "--events" => report(unsafe { copy_and_free(ffi::lumadesk_drain_events()) }),
        command => {
            let payload = match arguments(&args[1..]) {
                Ok(payload) => payload,
                Err(message) => {
                    eprintln!("jiyaochu-ctl: {message}");
                    return ExitCode::FAILURE;
                }
            };
            let (Ok(command), payload) = (CString::new(command), payload) else {
                eprintln!("jiyaochu-ctl: the command name contains a NUL byte");
                return ExitCode::FAILURE;
            };
            let payload_pointer = payload
                .as_ref()
                .map_or(std::ptr::null(), |json| json.as_ptr());
            let raw = unsafe { ffi::lumadesk_call(command.as_ptr(), payload_pointer) };
            report(unsafe { copy_and_free(raw) })
        }
    }
}

/// Joins the remaining arguments into one JSON object, or reads stdin for `-`.
fn arguments(rest: &[String]) -> Result<Option<CString>, String> {
    let text = match rest {
        [] => return Ok(None),
        [single] if single == "-" => {
            let mut buffer = String::new();
            io::stdin()
                .read_to_string(&mut buffer)
                .map_err(|error| format!("could not read the arguments from stdin: {error}"))?;
            buffer
        }
        many => many.join(" "),
    };

    let text = text.trim();
    if text.is_empty() {
        return Ok(None);
    }
    // Reject nonsense here so the caller gets a clear message instead of the
    // backend's generic "arguments are not valid JSON".
    serde_json::from_str::<serde_json::Value>(text)
        .map_err(|error| format!("the arguments are not valid JSON: {error}"))?;

    CString::new(text)
        .map(Some)
        .map_err(|_| "the arguments contain a NUL byte".to_string())
}

/// Copies a string out of the ABI and releases the original exactly once.
///
/// # Safety
/// `raw` must be either null or a pointer returned by an `lumadesk_*` call that
/// has not been freed yet.
unsafe fn copy_and_free(raw: *mut std::os::raw::c_char) -> String {
    if raw.is_null() {
        return "{\"ok\":false,\"error\":\"the backend returned a null pointer\"}".to_string();
    }
    let text = CStr::from_ptr(raw).to_string_lossy().into_owned();
    ffi::lumadesk_free(raw);
    text
}

/// Prints the envelope, pretty when it is a pipe, and derives the exit code.
fn report(envelope: String) -> ExitCode {
    let parsed: Option<serde_json::Value> = serde_json::from_str(&envelope).ok();
    let succeeded = matches!(
        parsed
            .as_ref()
            .and_then(|value| value.get("ok"))
            .and_then(serde_json::Value::as_bool),
        Some(true)
    );

    if io::stdout().is_terminal() {
        match &parsed {
            Some(value) => {
                let rendered = if succeeded {
                    serde_json::to_string_pretty(value).unwrap_or_else(|_| envelope.clone())
                } else {
                    envelope.clone()
                };
                println!("{rendered}");
            }
            None => println!("{envelope}"),
        }
    } else {
        // Piped: one compact document, easy to feed to `jq`.
        let _ = io::stdout().write_all(envelope.as_bytes());
        let _ = io::stdout().write_all(b"\n");
    }

    if succeeded {
        ExitCode::SUCCESS
    } else {
        ExitCode::FAILURE
    }
}
