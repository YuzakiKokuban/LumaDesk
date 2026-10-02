use crate::core::error::HalResult;
use std::sync::Mutex;
use std::time::{Duration, Instant};

/// Caches successful reads, including an explicitly unavailable value.
pub(super) struct ReadCache<T>(Mutex<Option<(Instant, T)>>);

impl<T> Default for ReadCache<T> {
    fn default() -> Self {
        Self(Mutex::new(None))
    }
}

impl<T: Clone> ReadCache<T> {
    pub fn read(&self, lifetime: Duration, load: impl FnOnce() -> HalResult<T>) -> HalResult<T> {
        let mut cached = self.0.lock().unwrap_or_else(|error| error.into_inner());
        if let Some((time, value)) = cached.as_ref() {
            if time.elapsed() < lifetime {
                return Ok(value.clone());
            }
        }
        let value = load()?;
        *cached = Some((Instant::now(), value.clone()));
        Ok(value)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::core::error::HalError;

    #[test]
    fn caches_unavailable_values_and_refreshes_expired_reads() {
        let cache = ReadCache::<Option<u8>>::default();
        let lifetime = Duration::from_secs(60);
        assert_eq!(cache.read(lifetime, || Ok(None)).unwrap(), None);
        assert_eq!(
            cache
                .read(lifetime, || panic!("unexpected repeated query"))
                .unwrap(),
            None
        );
        assert_eq!(
            cache.read(Duration::ZERO, || Ok(Some(42))).unwrap(),
            Some(42)
        );
        assert_eq!(
            cache
                .read(lifetime, || panic!("cache not refreshed"))
                .unwrap(),
            Some(42)
        );
    }

    #[test]
    fn retries_failed_reads() {
        let cache = ReadCache::<u8>::default();
        assert!(cache
            .read(Duration::from_secs(60), || Err(HalError::io("transient")))
            .is_err());
        assert_eq!(cache.read(Duration::from_secs(60), || Ok(7)).unwrap(), 7);
    }
}
