//! Embeds the application icon and version resource into the executable.
//!
//! A native build has no bundler to do this for us, so the PE resources come
//! straight from the project mark in `assets/LumaDesk.ico`. The same source is
//! used by the WinUI application and installer, keeping the taskbar, Start
//! menu and installer visually consistent.

fn main() {
    println!("cargo:rerun-if-changed=assets/LumaDesk.ico");
    println!("cargo:rerun-if-changed=build.rs");

    #[cfg(windows)]
    {
        let mut res = winresource::WindowsResource::new();
        res.set_icon("assets/LumaDesk.ico");
        res.set("ProductName", "机耀处");
        res.set("FileDescription", "机耀处");
        res.set("CompanyName", "JiYaoChu");
        res.set("LegalCopyright", "MIT License");
        res.set("OriginalFilename", "jiyaochu-ctl.exe");
        res.set("InternalName", "jiyaochu");
        res.set("ProductVersion", env!("CARGO_PKG_VERSION"));
        res.set("FileVersion", env!("CARGO_PKG_VERSION"));
        res.compile().expect(
            "Windows resource compilation failed; install the Windows SDK resource compiler",
        );
    }
}
