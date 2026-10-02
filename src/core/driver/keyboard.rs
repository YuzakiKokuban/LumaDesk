//! EC RGB: OEM LightingModel.EC_SPEC and native call sequence documented in RGB.md.
use super::AcpiDriver;
use crate::core::{
    config::{LightingEngine, LightingState},
    error::{HalError, HalResult},
};
const REGISTERS: [u16; 6] = [0x769, 0x76a, 0x76b, 0x78c, 0x7c5, 0x767];
// EC RGB uses AP_OEM_BYTE2, not AP_EC_LOGO (0x7AB).
const BRIGHTNESS: [u8; 5] = [0x12, 0x30, 0x50, 0x70, 0x90];

fn control_byte(previous: u8, level: usize) -> u8 {
    (previous & 0x0d) | BRIGHTNESS[level]
}

fn brightness(control: u8) -> u32 {
    if control & 0x02 != 0 {
        0
    } else {
        u32::from((control >> 5).min(4))
    }
}

fn channel(value: u8) -> u8 {
    if value == 0 {
        0
    } else {
        ((u32::from(value) * 50 / 255) as u8).max(1)
    }
}

pub fn readback_color(color: &str) -> String {
    let color = crate::core::config::normalise_hex(color);
    let values: Vec<u8> = (0..3)
        .map(|i| {
            let value = u8::from_str_radix(&color[1 + i * 2..3 + i * 2], 16).unwrap_or(0);
            (u32::from(channel(value)) * 255 / 50) as u8
        })
        .collect();
    format!("#{:02x}{:02x}{:02x}", values[0], values[1], values[2])
}

pub fn supported(driver: &AcpiDriver) -> bool {
    driver.read_ec(0x766).map(|v| v & 4 != 0).unwrap_or(false)
}

pub fn read(driver: &AcpiDriver) -> HalResult<LightingState> {
    driver.transaction(|ec| {
        if ec.read(0x766)? & 4 == 0 {
            return Err(HalError::unsupported("此键盘没有 EC RGB 通道"));
        }
        let control = ec.read(0x78c)?;
        let level = brightness(control);
        let mut rgb = [0u8; 3];
        for (i, value) in rgb.iter_mut().enumerate() {
            *value = (u32::from(ec.read(0x769 + i as u16)?).min(50) * 255 / 50) as u8;
        }
        Ok(LightingState {
            // Bit 4 is a self-clearing submission bit. Nonzero intensity or
            // the explicit off bit remain readable after firmware consumes it.
            firmware_managed: control & 0xe2 == 0,
            enabled: level != 0,
            kb_brightness: level,
            kb_color: format!("#{:02x}{:02x}{:02x}", rgb[0], rgb[1], rgb[2]),
            ..LightingState::default()
        })
    })
}

pub fn apply(driver: &AcpiDriver, state: &LightingState) -> HalResult<()> {
    if state.kb_engine != LightingEngine::Hardware || state.kb_effect != 0 {
        return Err(HalError::unsupported(
            "当前 EC RGB 通道只实现单色常亮；其他灯效尚未接入",
        ));
    }
    let color = crate::core::config::normalise_hex(&state.kb_color);
    let rgb: Vec<u8> = (0..3)
        .map(|i| u8::from_str_radix(&color[1 + i * 2..3 + i * 2], 16).unwrap_or(0))
        .collect();
    let level = if state.enabled {
        state.kb_brightness.min(4) as usize
    } else {
        0
    };
    driver.transaction(|ec| {
        if ec.read(0x766)? & 4 == 0 {
            return Err(HalError::unsupported("此键盘没有 EC RGB 通道"));
        }
        let mut before = [0u8; 6];
        for (i, a) in REGISTERS.iter().enumerate() {
            before[i] = ec.read(*a)?;
        }
        let result = (|| {
            // Bit 4 is self-clearing: only persistent control bits are checked.
            // Clear the firmware effect selector, preserving fan-ramp bit 7.
            let control = control_byte(before[3], level);
            ec.write_command(0x78c, control)?;
            if ec.read(0x78c)? & !0x10 != control & !0x10 {
                return Err(HalError::io("键盘亮度读回不一致"));
            }
            ec.write_verified(0x7c5, before[4] & !7)?;
            for i in 0..3 {
                ec.write_verified(REGISTERS[i], if level == 0 { 0 } else { channel(rgb[i]) })?;
            }
            // Clear welcome animation and pulse the OEM RGB update trigger.
            ec.write_command(0x767, (before[5] & !0x80) | 0x20)
        })();
        if result.is_err() {
            for i in 0..5 {
                let _ = ec.write_verified(REGISTERS[i], before[i]);
            }
            let _ = ec.write_command(0x767, before[5] | 0x20);
        }
        result
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn ec_rgb_intensity_roundtrips_and_preserves_other_controls() {
        for previous in 0..=255 {
            for level in 0..5 {
                let control = control_byte(previous, level);
                assert_eq!(brightness(control), level as u32);
                assert_eq!(control & 0x0d, previous & 0x0d);
                assert_ne!(control & 0x10, 0);
                assert_eq!(control & 2 != 0, level == 0);
            }
        }
        assert!(
            !REGISTERS.contains(&0x7ab),
            "logo/performance register must not set keyboard brightness"
        );
    }

    #[test]
    fn low_nonzero_channels_remain_visible() {
        assert_eq!(channel(0), 0);
        assert_eq!(channel(1), 1);
        assert_eq!(channel(255), 50);
        assert_eq!(readback_color("#ffffff"), "#ffffff");
        assert_eq!(readback_color("#010000"), "#050000");
    }
}
