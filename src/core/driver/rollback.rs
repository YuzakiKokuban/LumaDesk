//! Best-effort recovery that attempts every register and reports incomplete restoration.

use crate::core::error::{HalError, HalResult};

pub(crate) fn recover<T>(
    result: HalResult<T>,
    snapshot: &[(u16, u8)],
    mut restore: impl FnMut(u16, u8) -> HalResult<()>,
) -> HalResult<T> {
    let error = match result {
        Ok(value) => return Ok(value),
        Err(error) => error,
    };
    let mut failures = Vec::new();
    for &(address, value) in snapshot {
        if let Err(failure) = restore(address, value) {
            failures.push(format!("EC {address:#x}: {failure}"));
        }
    }
    if failures.is_empty() {
        Err(error)
    } else {
        Err(HalError::io(format!(
            "{error}; rollback incomplete: {}",
            failures.join("; ")
        )))
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::core::error::{HalError, HalResult};
    use std::cell::RefCell;

    #[test]
    fn rollback_attempts_all_registers_and_reports_original_and_recovery_errors() {
        let registers = RefCell::new(vec![9, 9, 9]);
        let attempts = RefCell::new(Vec::new());
        let snapshot = [(0, 10), (1, 20), (2, 30)];
        let result: HalResult<()> = Err(HalError::io("injected apply failure"));
        let error = recover(result, &snapshot, |address, value| {
            attempts.borrow_mut().push(address);
            if address == 1 {
                return Err(HalError::io("injected rollback failure"));
            }
            registers.borrow_mut()[address as usize] = value;
            Ok(())
        })
        .unwrap_err()
        .to_string();
        assert_eq!(*attempts.borrow(), vec![0, 1, 2]);
        assert_eq!(*registers.borrow(), vec![10, 9, 30]);
        assert!(error.contains("injected apply failure"));
        assert!(error.contains("rollback incomplete"));
        assert!(error.contains("0x1"));
        assert!(error.contains("injected rollback failure"));
    }

    #[test]
    fn successful_rollback_restores_all_values_and_keeps_original_error() {
        let mut registers = [9, 9, 9];
        let snapshot = [(0, 10), (1, 20), (2, 30)];
        let result: HalResult<()> = Err(HalError::unsupported("injected failure"));
        let error = recover(result, &snapshot, |address, value| {
            registers[address as usize] = value;
            Ok(())
        })
        .unwrap_err();
        assert_eq!(registers, [10, 20, 30]);
        assert!(matches!(error, HalError::Unsupported(_)));
    }

    #[test]
    fn successful_apply_never_runs_rollback() {
        assert_eq!(
            recover(Ok(42), &[(1, 2)], |_, _| panic!("unexpected rollback")).unwrap(),
            42
        );
    }
}
