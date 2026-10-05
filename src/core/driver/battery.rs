//! Project 0x1A charge threshold: bits 0..6 are percent, bit 7 is firmware status.
use super::acpi::EcSession;
use crate::core::error::{HalError, HalResult};

pub(crate) fn decode(raw: u8) -> HalResult<u32> {
    let threshold = raw & 0x7f;
    match threshold {
        // Firmware's uninitialised threshold means unrestricted charging.
        0 => Ok(100),
        1..=100 => Ok(u32::from(threshold)),
        _ => Err(HalError::io(format!(
            "EC 充电阈值 {threshold}% 无效（原始值 {raw:#04x}）"
        ))),
    }
}

fn merge(raw: u8, threshold: u8) -> u8 {
    (raw & 0x80) | (threshold & 0x7f)
}

pub(crate) fn mode(raw: u8, limit: u8) -> u8 {
    let profile = if limit >= 95 {
        1
    } else if limit >= 66 {
        0x11
    } else {
        0x21
    };
    (raw & !0x31) | profile
}

pub(crate) fn write(ec: &EcSession, threshold: u8) -> HalResult<()> {
    // The reached bit may change between the write and readback. Verify only
    // the owned threshold, preserving the latest status when writing/rolling back.
    let before = ec.read(0x7b9)?;
    ec.write_command(0x7b9, merge(before, threshold))?;
    let actual = ec.read(0x7b9)?;
    if actual & 0x7f != threshold & 0x7f {
        return Err(HalError::io(format!(
            "EC 充电阈值写入 {}%，读回 {}%",
            threshold & 0x7f,
            actual & 0x7f
        )));
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn reached_flag_does_not_change_threshold() {
        assert_eq!(decode(0xd0).unwrap(), 80);
        for threshold in 1..=100 {
            assert_eq!(decode(threshold).unwrap(), u32::from(threshold));
            assert_eq!(decode(threshold | 0x80).unwrap(), u32::from(threshold));
            assert_eq!(merge(0x80, threshold), threshold | 0x80);
            assert_eq!(merge(0, threshold), threshold);
        }
        assert_eq!(decode(0).unwrap(), 100);
        assert_eq!(decode(0x80).unwrap(), 100);
        for threshold in 101..=127 {
            assert!(decode(threshold).is_err());
            assert!(decode(threshold | 0x80).is_err());
        }
    }
    #[test]
    fn charge_profile_preserves_unrelated_firmware_flags() {
        for original in 0..=255 {
            for (limit, expected) in [(60, 0x21), (80, 0x11), (100, 1)] {
                assert_eq!(mode(original, limit) & 0x31, expected);
                assert_eq!(mode(original, limit) & !0x31, original & !0x31);
            }
        }
    }
}
