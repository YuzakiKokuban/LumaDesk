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
        if let Err(e) = res.compile() {
            // Do not fail the build on a missing resource compiler: the app is
            // still perfectly usable, it just loses its icon and version block.
            println!("cargo:warning=could not compile Windows resources: {e}");
        }
    }
}
