//! EC RGB: OEM LightingModel.EC_SPEC and native call sequence documented in RGB.md.
use super::AcpiDriver;
use crate::core::{config::{LightingState, LightingEngine}, error::{HalError, HalResult}};
const REGISTERS: [u16; 6] = [0x769, 0x76a, 0x76b, 0x7ab, 0x78c, 0x767];
const BRIGHTNESS: [u8; 5] = [0, 0x20, 0x40, 0x60, 0x80];

pub fn supported(driver: &AcpiDriver) -> bool { driver.read_ec(0x766).map(|v| v & 4 != 0).unwrap_or(false) }

pub fn read(driver: &AcpiDriver) -> HalResult<LightingState> {
    driver.transaction(|ec| {
        if ec.read(0x766)? & 4 == 0 { return Err(HalError::unsupported("此键盘没有 EC RGB 通道")); }
        let level = u32::from((ec.read(0x7ab)? >> 5).min(4));
        let mut rgb = [0u8; 3];
        for (i, value) in rgb.iter_mut().enumerate() { *value = (u32::from(ec.read(0x769 + i as u16)?).min(50) * 255 / 50) as u8; }
        Ok(LightingState { firmware_managed: ec.read(0x78c)? & 0x10 == 0, enabled: level != 0, kb_brightness: level,
            kb_color: format!("#{:02x}{:02x}{:02x}", rgb[0], rgb[1], rgb[2]), ..LightingState::default() })
    })
}

pub fn apply(driver: &AcpiDriver, state: &LightingState) -> HalResult<()> {
    if state.kb_engine != LightingEngine::Hardware || state.kb_effect != 0 {
        return Err(HalError::unsupported("当前 EC RGB 通道只实现单色常亮；其他灯效尚未接入"));
    }
    let color = crate::core::config::normalise_hex(&state.kb_color);
    let rgb: Vec<u8> = (0..3).map(|i| u8::from_str_radix(&color[1+i*2..3+i*2],16).unwrap_or(0)).collect();
    let level = if state.enabled { state.kb_brightness.min(4) as usize } else { 0 };
    driver.transaction(|ec| {
        if ec.read(0x766)? & 4 == 0 { return Err(HalError::unsupported("此键盘没有 EC RGB 通道")); }
        let mut before = [0u8; 6];
        for (i, a) in REGISTERS.iter().enumerate() { before[i] = ec.read(*a)?; }
        let result = (|| {
            // RGB values are 0..50; brightness lives separately in 7AB bits 5..7.
            for i in 0..3 { ec.write_verified(REGISTERS[i], if level == 0 { 0 } else { (u32::from(rgb[i]) * 50 / 255) as u8 })?; }
            ec.write_verified(0x7ab, (before[3] & 0x1f) | BRIGHTNESS[level])?;
            ec.write_verified(0x78c, before[4] | 0x10)?;
            // Clear welcome animation and pulse the OEM RGB update trigger.
            ec.write_command(0x767, (before[5] & !0x80) | 0x20)
        })();
        if result.is_err() {
            for i in 0..5 { let _ = ec.write_verified(REGISTERS[i], before[i]); }
            let _ = ec.write_command(0x767, before[5] | 0x20);
        }
        result
    })
}
