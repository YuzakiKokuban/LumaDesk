# Page Resources Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans or focused parallel workers, with parent integration and review.

**Goal:** Reduce unneeded reads, merge duplicate refreshes, retain tab context and deliver honest physical regression evidence.

**Architecture:** Page-scoped partial status replies are merged by a route-aware MachineStore. SettingsResource batches pending reads while PageContext stores only session values and offsets. An isolated Windows FFI harness records actual hardware and sleep/resume evidence.

**Tech Stack:** Locked Rust 1.92 / .NET 10 WinUI Reactor, Python 3.12+, Windows APIs.

**Spec:** ../specs/2026-10-06-page-resources.md

## Global Constraints

- Direct dev, preserve existing changes, no release or force push.
- No hidden polling or hidden page caching; uncertain fields remain unknown.
- Formal ZIP/installer retain administrator manifest; UI checks use isolated mock host.

## Review Focus

- In-flight older page result cannot overwrite the selected page.
- Refreshes during active reads cannot consume stale results; distinct partial sections survive.
- Native page scroll restore runs once, after usable layout; drafts must reconcile with real capabilities.
- Brightness writes restore their original level on every test failure.
- Sleep/resume requires actual Windows events and fresh readbacks, never elapsed gaps.

## Tasks

1. Page-scoped telemetry (parent): src/core/api.rs, src/ffi.rs, HAL getters; app/Services/MachineStore.cs and new MachineTelemetry.cs; App.cs. Add query-boundary and stale-route tests, run Rust/FFI and UI checks.
2. Readback batching (worker): SettingsResource.cs plus SettingsResourceVerification.cs. Add delayed freshness/failure/disposal tests. Parent keys partial reads and eliminates double writer refreshes.
3. Session page context (worker): PageContext.cs, Chrome/NativeLayout, Display/Lighting/Gpu pages, PageContextVerification.cs. Parent adds native tab-switch/draft/scroll tests.
4. Physical regression (worker + parent): verify-long-run.py and tests. Run portable tests, real five-minute readback/brightness test with restored state; leave unobserved physical sleep pending.
5. Integration: build native core and WinUI, run mock startup/settings/shell, inspect screenshots, build ZIP and installer and verify both. Review, update validation evidence, commit and push origin/dev.
