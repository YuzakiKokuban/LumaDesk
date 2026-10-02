# LumaDesk · 机耀处

A Windows control center for Mechrevo laptops, focused on the Yaoshi 15 Air (EC project 0x1A). Version 0.1.0 uses C# WinUI 3 and a Rust hardware backend.

Repository: [YuzakiKokuban/LumaDesk](https://github.com/YuzakiKokuban/LumaDesk). This repository has independent history and new branding.

Extract the complete portable ZIP and run `机耀处.exe` with administrator privileges. It uses the installed UniWill driver; no vendor binaries or kernel driver are distributed. Startup automatically takes over the OEM control center. The System page restores backed-up service/task state and opts out of automatic takeover.

OEM chassis profiles and Windows power modes are separate. Three chassis profiles, three Windows modes, and EC RGB colors/brightness passed register/API readback tests. Fan boost and OEM takeover/restore passed reversible tests. NVIDIA telemetry uses NVML. MUX reading works; rebooted display routing, charging cutoff behavior, and physical key-lock behavior remain unverified.

Dynamic RGB, per-key lighting, custom fan curves, GPU power tuning, several firmware switches and standalone OSD remain unfinished. The UI offers implemented controls and identifies missing capabilities. Recent responsive layout changes compile successfully but need live visual verification.

Build: `cargo build`, `cargo test`, `dotnet build app/JiYaoChu.csproj`, or `./scripts/build.ps1` for the complete portable package. See [Build](docs/BUILD.md), [Features](docs/FEATURES.md), [Validation](docs/VALIDATION.md), and [Protocol provenance](docs/PROVENANCE.md).

MIT licensed. Preserve [third-party notices](THIRD_PARTY_NOTICES.md). This is an independent implementation based on recovered protocol evidence, not the vendor's complete source code.
