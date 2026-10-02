//! Read-only NVIDIA telemetry through the installed driver's NVML library.
//! API reference: https://docs.nvidia.com/deploy/nvml-api/latest/index.html
use crate::core::config::GpuStatus;

#[cfg(windows)]
pub fn snapshot() -> Option<GpuStatus> {
    use std::{ffi::{c_char, c_void, CStr}, sync::OnceLock};
    use windows::{core::PCWSTR, Win32::System::LibraryLoader::{GetProcAddress, LoadLibraryW}};

    // The NVIDIA module stays loaded for the process lifetime. Missing APIs or
    // sleeping/unavailable GPUs fall back to Windows identity, without values.
    static MODULE: OnceLock<Option<usize>> = OnceLock::new();
    let module = *MODULE.get_or_init(|| {
        let root = std::env::var_os("SystemRoot")?;
        let path = std::path::PathBuf::from(root).join("System32/nvml.dll");
        let wide: Vec<u16> = path.as_os_str().to_string_lossy().encode_utf16().chain(Some(0)).collect();
        let module = unsafe { LoadLibraryW(PCWSTR(wide.as_ptr())).ok()? };
        let init = unsafe { GetProcAddress(module, windows::core::PCSTR(b"nvmlInit_v2\0".as_ptr()))? };
        let init: unsafe extern "C" fn() -> u32 = unsafe { std::mem::transmute(init) };
        if unsafe { init() } != 0 { return None; }
        Some(module.0 as usize)
    });
    let module = windows::Win32::Foundation::HMODULE(module? as *mut c_void);
    macro_rules! api {
        ($name:literal, $ty:ty) => {{
            let address = unsafe { GetProcAddress(module, windows::core::PCSTR(concat!($name, "\0").as_ptr()))? };
            unsafe { std::mem::transmute::<_, $ty>(address) }
        }};
    }
    type Device = *mut c_void;
    let get_device = api!("nvmlDeviceGetHandleByIndex_v2", unsafe extern "C" fn(u32, *mut Device) -> u32);
    let mut device: Device = std::ptr::null_mut();
    if unsafe { get_device(0, &mut device) } != 0 || device.is_null() { return None; }
    let name_fn = api!("nvmlDeviceGetName", unsafe extern "C" fn(Device, *mut c_char, u32) -> u32);
    let mut name = [0i8; 128];
    if unsafe { name_fn(device, name.as_mut_ptr(), name.len() as u32) } != 0 { return None; }
    name[127] = 0;
    let name = unsafe { CStr::from_ptr(name.as_ptr()) }.to_string_lossy().into_owned();
    let temp_fn = api!("nvmlDeviceGetTemperature", unsafe extern "C" fn(Device, u32, *mut u32) -> u32);
    let clock_fn = api!("nvmlDeviceGetClockInfo", unsafe extern "C" fn(Device, u32, *mut u32) -> u32);
    let mut temp = 0;
    let mut clock = 0;
    let temp = (unsafe { temp_fn(device, 0, &mut temp) } == 0).then_some(f64::from(temp));
    let freq_mhz = (unsafe { clock_fn(device, 0, &mut clock) } == 0)
        .then_some(f64::from(clock)).filter(|v| (0.0..=6000.0).contains(v));
    #[repr(C)]
    #[derive(Default)]
    struct Util { gpu: u32, memory: u32 }
    #[repr(C)]
    #[derive(Default)]
    struct Memory { total: u64, free: u64, used: u64 }
    let util_fn = api!("nvmlDeviceGetUtilizationRates", unsafe extern "C" fn(Device, *mut Util) -> u32);
    let mem_fn = api!("nvmlDeviceGetMemoryInfo", unsafe extern "C" fn(Device, *mut Memory) -> u32);
    let mut util = Util::default();
    let mut mem = Memory::default();
    let load = (unsafe { util_fn(device, &mut util) } == 0).then_some(f64::from(util.gpu));
    let memory_ok = unsafe { mem_fn(device, &mut mem) } == 0;
    Some(GpuStatus {
        present: true, name, temp, freq_mhz, load,
        vram_used_mb: memory_ok.then_some(mem.used / (1024 * 1024)),
        vram_total_mb: memory_ok.then_some(mem.total / (1024 * 1024)),
    })
}

#[cfg(not(windows))]
pub fn snapshot() -> Option<GpuStatus> { None }
