//! Resident event subscriptions with bounded backoff and explicit reconnect.
use serde::Serialize;
use std::sync::{
    atomic::{AtomicBool, AtomicU64, Ordering},
    Mutex,
};
use std::time::Duration;

#[derive(Clone, Default, Serialize)]
pub struct SubscriptionStatus {
    pub state: &'static str,
    pub failures: u32,
    pub last_error: Option<String>,
    pub connections: u64,
}

pub struct Subscription {
    name: &'static str,
    started: AtomicBool,
    generation: AtomicU64,
    status: Mutex<SubscriptionStatus>,
    worker: Mutex<Option<std::thread::Thread>>,
}

impl Subscription {
    pub const fn new(name: &'static str) -> Self {
        Self {
            name,
            started: AtomicBool::new(false),
            generation: AtomicU64::new(0),
            status: Mutex::new(SubscriptionStatus {
                state: "inactive",
                failures: 0,
                last_error: None,
                connections: 0,
            }),
            worker: Mutex::new(None),
        }
    }
    pub fn status(&self) -> SubscriptionStatus {
        self.status
            .lock()
            .unwrap_or_else(|e| e.into_inner())
            .clone()
    }
    pub fn interrupted(&self, generation: u64) -> bool {
        self.generation.load(Ordering::Acquire) != generation
    }
    pub fn ready(&self, result: Result<(), String>) {
        if result.is_ok() {
            let mut state = self.status.lock().unwrap_or_else(|e| e.into_inner());
            state.state = "connected";
            state.failures = 0;
            state.last_error = None;
            state.connections = state.connections.saturating_add(1);
        }
    }
    pub fn reconnect(&self) {
        self.generation.fetch_add(1, Ordering::AcqRel);
        if self.started.load(Ordering::Acquire) {
            self.status.lock().unwrap_or_else(|e| e.into_inner()).state = "reconnecting";
        }
        if let Some(worker) = &*self.worker.lock().unwrap_or_else(|e| e.into_inner()) {
            worker.unpark();
        }
    }
    pub fn start(
        &'static self,
        watch: impl Fn(u64) -> crate::core::error::HalResult<()> + Send + 'static,
    ) -> Result<(), String> {
        if self.started.swap(true, Ordering::AcqRel) {
            return Ok(());
        }
        let result = std::thread::Builder::new()
            .name(self.name.into())
            .spawn(move || {
                *self.worker.lock().unwrap_or_else(|e| e.into_inner()) =
                    Some(std::thread::current());
                loop {
                    let generation = self.generation.load(Ordering::Acquire);
                    self.status.lock().unwrap_or_else(|e| e.into_inner()).state = "connecting";
                    let result = watch(generation);
                    if self.interrupted(generation) {
                        continue;
                    }
                    let failure = result
                        .err()
                        .map(|e| e.to_string())
                        .unwrap_or_else(|| "event subscription ended".into());
                    let attempts = {
                        let mut state = self.status.lock().unwrap_or_else(|e| e.into_inner());
                        state.state = "retrying";
                        state.failures = state.failures.saturating_add(1);
                        state.last_error = Some(failure.clone());
                        state.failures
                    };
                    super::logging::warn(format!(
                        "{}: {failure}; reconnect attempt {attempts}",
                        self.name
                    ));
                    std::thread::park_timeout(retry_delay(attempts));
                }
            });
        if let Err(error) = result {
            self.started.store(false, Ordering::Release);
            return Err(error.to_string());
        }
        Ok(())
    }
}

fn retry_delay(failures: u32) -> Duration {
    Duration::from_secs((1u64 << failures.saturating_sub(1).min(5)).min(30))
}
pub static BRIGHTNESS: Subscription = Subscription::new("display-brightness-events");
pub static HOTKEYS: Subscription = Subscription::new("oem-key-events");

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn initial_failure_is_retried_without_starting_another_worker() {
        static TEST: Subscription = Subscription::new("retry-test");
        let attempts = std::sync::Arc::new(std::sync::atomic::AtomicU32::new(0));
        let seen = attempts.clone();
        let (sender, receiver) = std::sync::mpsc::channel();
        TEST.start(move |_| {
            if seen.fetch_add(1, Ordering::AcqRel) == 0 {
                return Err(crate::core::error::HalError::unavailable(
                    "temporary provider failure",
                ));
            }
            TEST.ready(Ok(()));
            let _ = sender.send(());
            loop {
                std::thread::park();
            }
        })
        .unwrap();
        receiver.recv_timeout(Duration::from_secs(4)).unwrap();
        assert_eq!(attempts.load(Ordering::Acquire), 2);
        assert_eq!(TEST.status().state, "connected");
        assert!(TEST.status().last_error.is_none());
    }
    #[test]
    fn reconnect_invalidates_old_watcher_and_backoff_is_bounded() {
        let subscription = Subscription::new("test");
        assert!(!subscription.interrupted(0));
        subscription.reconnect();
        assert!(subscription.interrupted(0));
        subscription.ready(Ok(()));
        assert_eq!(subscription.status().state, "connected");
        assert_eq!(retry_delay(1), Duration::from_secs(1));
        assert_eq!(retry_delay(3), Duration::from_secs(4));
        assert_eq!(retry_delay(u32::MAX), Duration::from_secs(30));
    }
}
