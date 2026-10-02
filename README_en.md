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

See [feature support](docs/FEATURES.md) and [local verification](docs/VALIDATION.md) for details.

## Getting started

1. Download the Windows x64 portable archive from [Releases](https://github.com/YuzakiKokuban/LumaDesk/releases) and extract it to a permanent location. Development builds are available in [Actions](https://github.com/YuzakiKokuban/LumaDesk/actions/workflows/ci.yml).
2. Open `机耀处.exe` and accept the administrator prompt.
3. Select a page in the sidebar and adjust your settings.

Requires Windows 11 x64 and the UWACPIDriver installed with the OEM control center. The archive includes the application runtimes.

LumaDesk takes over the OEM control center at first launch. To switch back, choose the restore option in System settings. GPU changes require a restart; other common settings apply immediately.

Keep the application directory in place when automatic startup is enabled. Configuration and logs are stored in `%APPDATA%\JiYaoChu`.

[Build and release](docs/BUILD.md) · [Architecture](docs/ARCHITECTURE.md) · [Protocol notes](reverse/native/REPORT.md) · [中文](README.md)

[MIT License](LICENSE) · [Third-party notices](THIRD_PARTY_NOTICES.md)
