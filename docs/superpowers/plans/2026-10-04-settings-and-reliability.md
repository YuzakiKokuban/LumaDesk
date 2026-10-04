# Settings and reliability implementation

The user authorized implementation of the audit fixes, settings restoration and migration, a display settings page, visible-window trends, and custom keyboard hex colors. Work directly on dev; validate, commit and push without creating a release.

## Behavior

- Restrict recovered EC writes to validated machine capabilities. Unknown battery limits stay unknown. Preserve and report rollback failures. Persist and restore logging preferences.
- Update mounted settings after physical buttons, tray commands, automation and restore. Background profile polling becomes an independent opt-in preference, default off.
- Export all settings/custom files including colors. Restore a selected ZIP after validation and preview, preserve a pre-restore backup, and report partial failures. Application preference restoration does not alter MUX, OEM takeover or login tasks; device settings require an explicit restore choice.
- Display page lists actual displays and supported refresh rates and exposes built-in screen brightness with apply/readback and retry.
- Keep at most five minutes of successful visible-window telemetry. Hidden windows collect no samples; missing readings and inactive periods appear as gaps. Provide clear and accessible summaries.
- Accept six-digit keyboard colors with optional #, show a preview, reject malformed values, and apply using the existing verified lighting path.

## Tasks and interfaces

1. Rust reliability: src/core/config.rs, hal/windows.rs, driver/*, api.rs, ffi.rs. Add behavior regressions before fixes; expose `restore_app_settings(cfg) -> AppConfig`, nullable battery limit and error, and `OsdConfig.watch_physical_profile`.
2. Restore: SupportExport and new restore service plus SystemPage. Test legacy/current archives, traversal, duplicate names, unsupported version/model, digest changes, limits and rollback. Verify restored preferences preserve protected system state.
3. Display/trends: new DisplayPage, bounded TrendHistory/UI, Overview and MachineStore. Test device selection/readback, missing device rejection, sampling gaps, bounds and hidden pause.
4. Integration/custom colors: C# models, Backend events, PowerPolicy, OSD, App routes, Tuning and Lighting. Verify external writes preserve controls and correct selection, invalid colors cannot write, and restored rules do not perform an implicit hardware change.
5. Delivery: run locked Rust format/Clippy/tests and Python checks; build Debug/Release; test isolated Release mock shell including five pages, restore, custom colors and trends; build ZIP/installer and verify both. Update FEATURES/VALIDATION/BUILD/ROADMAP, review final diff and push dev.

The user subsequently requested one combined display/GPU page named 显示设置 and a larger power/battery navigation icon. Keep GPU writes explicit and retain a single scrolling page.

## Review focus

- Changed ZIP between preview and confirmation must not be imported.
- Unknown/mismatched model cannot apply recovered hardware settings.
- Failed readback cannot display a saved battery preference as current hardware state.
- Hidden time and unavailable sensor values cannot become fabricated graph data.
- Restore must retain OEM, MUX and login-task state and avoid starting automation implicitly.

Real screen refresh changes, panel brightness, colors observed on the keyboard, charging cutoff, physical profile buttons and sleep/resume remain separate device checks.
