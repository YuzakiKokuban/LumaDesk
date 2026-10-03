<img src="assets/LumaDesk.png" width="96" alt="LumaDesk logo">

# LumaDesk · 机耀处

[![Build](https://github.com/YuzakiKokuban/LumaDesk/actions/workflows/ci.yml/badge.svg)](https://github.com/YuzakiKokuban/LumaDesk/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-MPL--2.0-blue)](LICENSE)

A Windows control center for Mechrevo laptops, built with WinUI 3 and a Rust hardware backend. Currently adapted for the **Yaoshi 15 Air (EC project 0x1A)**.

[中文](README.md)

## Features

- **Performance and cooling**: Office, Balanced and Beast profiles, fan boost, and CPU, GPU and fan monitoring.
- **Keyboard backlight**: on/off control, four brightness levels and eight solid color presets.
- **Power management**: 60%, 80% and 100% charging limits, Windows power modes and power plans; optional performance rules for AC and battery power.
- **GPU modes**: hybrid, discrete and integrated output settings, applied after a restart.
- **Desktop integration**: system tray with quick controls, elevated startup, Windows key lock, and hotkey and system status OSD.
- **Diagnostics and backups**: export version, notification health, recent logs and settings.
- **OEM software management**: control center takeover and restoration.

See [feature support](docs/FEATURES.md) and [verification records](docs/VALIDATION.md) for hardware coverage. GPU switching across a restart, visible backlight behavior and charging cutoff remain unverified.

## Getting started

Requires Windows 11 x64, .NET 10, Windows App SDK Runtime 2.5.1, and UWACPIDriver installed with the OEM control center. See [runtime dependencies](docs/RUNTIMES.md).

1. Download the installer from [Releases](https://github.com/YuzakiKokuban/LumaDesk/releases), or extract the portable ZIP to a permanent directory.
2. Run `LumaDesk.exe` and accept the administrator prompt.
3. LumaDesk takes over the OEM control center on first launch. Use the restore option in System settings to switch back.

Closing or minimizing the window keeps the application in the tray. Double-click the tray icon to reopen it, or right-click to exit. Configuration and logs are stored in `%APPDATA%\JiYaoChu`.

## Development and documentation

- [Build and release](docs/BUILD.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Hardware protocol analysis](reverse/native/REPORT.md)

## License

This project is licensed under the [Mozilla Public License 2.0](LICENSE).
