<img src="assets/LumaDesk.png" width="96" alt="LumaDesk logo">

# LumaDesk · 机耀处

[![Build](https://github.com/YuzakiKokuban/LumaDesk/actions/workflows/ci.yml/badge.svg)](https://github.com/YuzakiKokuban/LumaDesk/actions/workflows/ci.yml)

A control center for Mechrevo laptops, currently adapted for the **Yaoshi 15 Air**. Manage performance, keyboard backlighting, charging limits and GPU output, with live CPU, GPU and fan readings.

## Features

- Office, Balanced and Beast profiles; fan boost and fan speeds.
- Keyboard backlight toggle, four intensity levels and eight solid colors.
- 60%, 80% and 100% charging limits; battery and AC power status.
- Hybrid, discrete and integrated GPU output, applied after a restart.
- Windows power modes and plans, Windows key lock, elevated startup, and OEM control center takeover and restoration.
- Tray background mode and configurable native OSD. Closing or minimizing hides the window; hardware monitoring pauses until the window is reopened. Keyboard lock, audio, microphone and power changes arrive through Windows events.

See [feature support](docs/FEATURES.md) and [local verification](docs/VALIDATION.md) for details.

## Getting started

1. Download the Windows x64 installer (`Setup.exe`) or portable ZIP from [Releases](https://github.com/YuzakiKokuban/LumaDesk/releases). Extract the portable edition to a permanent location.
2. Open `机耀处.exe` and accept the administrator prompt.
3. Select a page in the sidebar and adjust your settings.

Requires Windows 11 x64, .NET 10, Windows App SDK Runtime 2.5.1, and the UWACPIDriver installed with the OEM control center. See [runtime downloads](https://github.com/YuzakiKokuban/LumaDesk/blob/main/docs/RUNTIMES.md).

LumaDesk takes over the OEM control center at first launch. To switch back, choose the restore option in System settings. GPU changes require a restart; other common settings apply immediately.

After takeover, Fn+F1 opens LumaDesk and Fn+F3 switches the Windows key lock. Both hotkeys are released when the OEM control center is restored. The lock suppresses both Windows keys while LumaDesk runs and is released on exit. Double-click the tray icon to open the window or right-click to exit. Login startup opens directly in the tray. Upgrading migrates an existing task owned by this application while preserving its enabled state.

OSD covers Fn notifications, Caps Lock / Num Lock / Scroll Lock, and power source changes. New configurations default to 60% opacity; System settings accepts any value from 20% to 100%, along with position, theme and duration. Existing settings are preserved. See [feature support](docs/FEATURES.md) for firmware event coverage.

Keep the application directory in place when automatic startup is enabled. Configuration and logs are stored in `%APPDATA%\JiYaoChu`. The installer adds a Start menu shortcut. Uninstallation restores the OEM control center and preserves personal settings.

[Build and release](docs/BUILD.md) · [Architecture](docs/ARCHITECTURE.md) · [Protocol notes](reverse/native/REPORT.md) · [中文](README.md)

[MIT License](LICENSE) · [Third-party notices](THIRD_PARTY_NOTICES.md)
