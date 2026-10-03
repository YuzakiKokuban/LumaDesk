//! Embeds the application icon and version resource into the executable.
//!
//! A native build has no bundler to do this for us, so the PE resources come
//! straight from the project mark in `assets/LumaDesk.ico`. The same source is
//! used by the WinUI application and installer, keeping the taskbar, Start
//! menu and installer visually consistent.

fn main() {
    println!("cargo:rerun-if-changed=assets/LumaDesk.ico");
    println!("cargo:rerun-if-changed=build.rs");
    println!("cargo:rerun-if-changed=scripts/version.py");

    #[cfg(windows)]
    {
        // Share the same resolver as the C# frontend and installer, including
        // Windows' numeric ordering of preview and finished versions.
        let version = std::process::Command::new("python")
            .args([
                "scripts/version.py",
                "--version",
                env!("CARGO_PKG_VERSION"),
                "--file-version-only",
            ])
            .output()
            .expect("Python 3.12 or newer is required to resolve the Windows version");
        assert!(
            version.status.success(),
            "Version resolution failed: {}",
            String::from_utf8_lossy(&version.stderr)
        );
        let file_version = String::from_utf8(version.stdout).expect("Version must be UTF-8");
        let numeric_version = file_version.trim().split('.').fold(0u64, |value, part| {
            (value << 16) | part.parse::<u16>().expect("Invalid Windows version") as u64
        });
        let mut res = winresource::WindowsResource::new();
        res.set_icon("assets/LumaDesk.ico");
        res.set("ProductName", "机耀处");
        res.set("FileDescription", "机耀处");
        res.set("CompanyName", "JiYaoChu");
        res.set("LegalCopyright", "Copyright (c) 2026 由崎黑板");
        res.set("OriginalFilename", "jiyaochu-ctl.exe");
        res.set("InternalName", "jiyaochu");
        res.set("ProductVersion", env!("CARGO_PKG_VERSION"));
        res.set("FileVersion", file_version.trim());
        res.set_version_info(winresource::VersionInfo::FILEVERSION, numeric_version);
        res.set_version_info(winresource::VersionInfo::PRODUCTVERSION, numeric_version);
        res.compile().expect(
            "Windows resource compilation failed; install the Windows SDK resource compiler",
        );
    }
}
