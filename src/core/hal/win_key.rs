//! Event-driven Windows-key suppression. No EC status bit is treated as proof
//! that the keyboard firmware suppresses a key. Windows releases the hook when
//! this process exits; the saved preference is reapplied by the desktop shell.
use crate::core::error::{HalError, HalResult};
use std::sync::{
    atomic::{AtomicBool, Ordering},
    OnceLock,
};
use windows::Win32::Foundation::{HINSTANCE, LPARAM, LRESULT, WPARAM};
use windows::Win32::System::LibraryLoader::GetModuleHandleW;
use windows::Win32::UI::WindowsAndMessaging::{
    CallNextHookEx, DispatchMessageW, GetMessageW, SetWindowsHookExW, TranslateMessage,
    UnhookWindowsHookEx, KBDLLHOOKSTRUCT, MSG, WH_KEYBOARD_LL,
};

static LOCKED: AtomicBool = AtomicBool::new(false);
static HOOK: OnceLock<Result<(), String>> = OnceLock::new();

unsafe extern "system" fn on_key(code: i32, w: WPARAM, l: LPARAM) -> LRESULT {
    if code >= 0 && LOCKED.load(Ordering::Relaxed) {
        // SAFETY: WH_KEYBOARD_LL supplies a KBDLLHOOKSTRUCT for nonnegative codes.
        let key = unsafe { &*(l.0 as *const KBDLLHOOKSTRUCT) };
        if matches!(key.vkCode, 0x5b | 0x5c) {
            return LRESULT(1);
        }
    }
    unsafe { CallNextHookEx(None, code, w, l) }
}

pub fn locked() -> bool {
    LOCKED.load(Ordering::Relaxed)
}

pub fn set_locked(value: bool) -> HalResult<()> {
    if value {
        let installed = HOOK.get_or_init(|| {
            let (sender, receiver) = std::sync::mpsc::sync_channel(1);
            std::thread::Builder::new()
                .name("windows-key-hook".into())
                .spawn(move || {
                    // The installing thread owns the hook and pumps its messages.
                    let hook = unsafe {
                        GetModuleHandleW(None).and_then(|module| {
                            SetWindowsHookExW(
                                WH_KEYBOARD_LL,
                                Some(on_key),
                                Some(HINSTANCE(module.0)),
                                0,
                            )
                        })
                    };
                    match hook {
                        Ok(hook) => {
                            let _ = sender.send(Ok(()));
                            let mut message = MSG::default();
                            while unsafe { GetMessageW(&mut message, None, 0, 0) }.0 > 0 {
                                unsafe {
                                    let _ = TranslateMessage(&message);
                                    DispatchMessageW(&message);
                                }
                            }
                            LOCKED.store(false, Ordering::Relaxed);
                            unsafe {
                                let _ = UnhookWindowsHookEx(hook);
                            }
                        }
                        Err(error) => {
                            let _ = sender.send(Err(error.to_string()));
                        }
                    }
                })
                .map_err(|error| error.to_string())?;
            receiver.recv().map_err(|error| error.to_string())?
        });
        installed
            .as_ref()
            .map_err(|error| HalError::io(format!("Win 键锁启动失败：{error}")))?;
    }
    LOCKED.store(value, Ordering::Relaxed);
    Ok(())
}
