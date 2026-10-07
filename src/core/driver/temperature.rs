//! Read-only chassis temperatures on the device-verified project 0x1A.
//! Uniwill addresses: https://github.com/tuxedocomputers/tuxedo-drivers/blob/main/src/tuxedo_io/tuxedo_io.c
use super::acpi::AcpiDriver;

pub(crate) const CPU: u16 = 0x043e;
pub(crate) const GPU: u16 = 0x044f;

pub(crate) fn valid_celsius(value: Option<f64>) -> Option<f64> {
    // Zero/0xff denote unavailable EC sensors. NVML can also return corrupt
    // success values (e.g. the reported 2560 C); never display or rescale them.
    value.filter(|value| (1.0..=125.0).contains(value))
}

pub(crate) fn read_ec(driver: &AcpiDriver, address: u16) -> Option<f64> {
    driver
        .transaction(|ec| {
            super::performance::require_project(ec.read(0x740)?)?;
            ec.read(address)
                .map(|raw| valid_celsius(Some(f64::from(raw))))
        })
        .ok()
        .flatten()
}

pub(crate) fn gpu_with_fallback(
    primary: Option<f64>,
    fallback: impl FnOnce() -> Option<f64>,
) -> Option<f64> {
    valid_celsius(primary).or_else(|| valid_celsius(fallback()))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn rejects_unavailable_and_corrupt_temperature_samples() {
        for invalid in [0.0, -1.0, 126.0, 255.0, 2560.0, f64::NAN, f64::INFINITY] {
            assert_eq!(valid_celsius(Some(invalid)), None, "raw {invalid}");
        }
        assert_eq!(valid_celsius(None), None);
        for valid in [1.0, 40.0, 54.0, 100.0, 125.0] {
            assert_eq!(valid_celsius(Some(valid)), Some(valid));
        }
    }

    #[test]
    fn gpu_uses_fresh_fallback_only_when_primary_is_missing_or_corrupt() {
        assert_eq!(
            gpu_with_fallback(Some(41.0), || panic!("unneeded EC read")),
            Some(41.0)
        );
        for primary in [None, Some(0.0), Some(2560.0)] {
            assert_eq!(gpu_with_fallback(primary, || Some(40.0)), Some(40.0));
            assert_eq!(gpu_with_fallback(primary, || Some(255.0)), None);
            assert_eq!(gpu_with_fallback(primary, || None), None);
        }
        assert_eq!(gpu_with_fallback(Some(2560.0), || Some(43.0)), Some(43.0));
    }
}
