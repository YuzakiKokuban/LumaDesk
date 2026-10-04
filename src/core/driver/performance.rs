//! OEM profile registers for the validated Yaoshi 15 Air / project 0x1A.
//! Preserve unrelated fields when selecting profiles and default PL values.
//! Protocol evidence and implementation limits: PERFORMANCE.md.
use super::AcpiDriver;
use crate::core::error::{HalError, HalResult};
const REGISTERS: [u16; 8] = [0x751, 0x7ab, 0x783, 0x784, 0x785, 0x45b, 0x726, 0x727];

pub(crate) fn require_project(project: u8) -> HalResult<()> {
    if project != 0x1a {
        return Err(HalError::unsupported(
            "当前 EC 控制仅适配已验证的耀世 15 Air / 0x1A",
        ));
    }
    Ok(())
}
pub fn read(driver: &AcpiDriver) -> HalResult<u8> {
    driver.transaction(|ec| {
        require_project(ec.read(0x740)?)?;
        decode(ec.read(0x751)?)
    })
}

fn decode(register: u8) -> HalResult<u8> {
    // The physical mode key leaves bit 5 set in both balanced (0x20,
    // blue LED) and beast (0x30, purple LED). It is not a profile bit.
    // Ignore it and the independent full-fan flag. The existing software
    // office encoding 0xA0 still decodes through its bit 7.
    let value = register & 0x90;
    match value {
        0x80 => Ok(0),
        0 => Ok(1),
        0x10 => Ok(2),
        _ => Err(HalError::unavailable(format!(
            "当前 EC 档位 {value:#x} 尚未解析"
        ))),
    }
}

pub fn apply(driver: &AcpiDriver, mode: u8) -> HalResult<()> {
    let value = match mode {
        0 => 0xa0,
        1 => 0,
        2 => 0x10,
        _ => return Err(HalError::unsupported("OEM 自定义档位尚未实现")),
    };
    driver.transaction(|ec| {
        require_project(ec.read(0x740)?)?;
        let mut before = [0u8; 8];
        for (i, address) in REGISTERS.iter().enumerate() {
            before[i] = ec.read(*address)?;
        }
        let trigger = ec.read(0x767)?;
        let base = if mode == 0 { 0x734 } else { 0x730 };
        let limits = [ec.read(base)?, ec.read(base + 1)?, ec.read(base + 2)?];
        if limits.iter().any(|v| *v == 0 || *v > 250) {
            return Err(HalError::unavailable("固件没有提供有效的默认功耗限制"));
        }
        let result = (|| {
            // Exit auxiliary profile overrides before choosing the OEM fan table.
            ec.write_verified(0x726, before[6] & !0x80)?;
            ec.write_verified(0x727, before[7] & !0x40)?;
            for (i, value) in limits.iter().enumerate() {
                ec.write_verified(0x783 + i as u16, *value)?;
            }
            ec.write_verified(
                0x7ab,
                (before[1] & !0x1e) | if mode == 2 { 0x10 } else { 0 },
            )?;
            ec.write_verified(0x45b, u8::from(mode == 0))?;
            ec.write_verified(0x751, (before[0] & !0xb0) | value)?;
            ec.write_command(
                0x767,
                (trigger & !0x08) | 0x20 | if mode == 0 { 8 } else { 0 },
            )
        })();
        let mut snapshot: Vec<_> = REGISTERS.into_iter().zip(before).collect();
        snapshot.push((0x767, trigger | 0x20));
        super::rollback::recover(result, &snapshot, |address, value| {
            if address == 0x767 {
                ec.write_command(address, value)
            } else {
                ec.write_verified(address, value)
            }
        })
    })
}

#[cfg(test)]
mod tests {
    use super::{decode, require_project};

    #[test]
    fn ec_controls_require_the_validated_project() {
        for project in 0..=255 {
            assert_eq!(require_project(project).is_ok(), project == 0x1a);
        }
    }

    #[test]
    fn physical_key_profiles_ignore_bit_five_and_full_fan_state() {
        for (raw, expected) in [
            (0, 1),
            (0x20, 1),
            (0x10, 2),
            (0x30, 2),
            (0x80, 0),
            (0xa0, 0),
        ] {
            assert_eq!(decode(raw).unwrap(), expected);
            assert_eq!(decode(raw | 0x40).unwrap(), expected);
        }
        assert!(decode(0x90).is_err());
        assert!(decode(0xb0).is_err());
    }
}
