"""Read explicit EC byte addresses using the protocol recovered from three binaries.

This script contains no write operation. A successful transport result does not
alone prove a successful firmware evaluation: the sampled driver completes some
failed ACPI evaluations with STATUS_SUCCESS. Compare returned data independently.
"""
import argparse
import ctypes
from ctypes import wintypes
import json
from pathlib import Path
import time

IOCTL_EC_READ = 0x9C40A488
DEVICE = r"\\.\ACPIDriver"


def read_addresses(addresses):
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.CreateFileW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD,
                                  ctypes.c_void_p, wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE]
    kernel.CreateFileW.restype = wintypes.HANDLE
    kernel.DeviceIoControl.argtypes = [wintypes.HANDLE, wintypes.DWORD, ctypes.c_void_p,
                                      wintypes.DWORD, ctypes.c_void_p, wintypes.DWORD,
                                      ctypes.POINTER(wintypes.DWORD), ctypes.c_void_p]
    kernel.DeviceIoControl.restype = wintypes.BOOL
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel.CloseHandle.restype = wintypes.BOOL
    handle = kernel.CreateFileW(DEVICE, 0xC0000000, 3, None, 3, 0, None)
    if handle == ctypes.c_void_p(-1).value:
        code = ctypes.get_last_error()
        return {"device": DEVICE, "opened": False, "win32_error": code,
                "error": ctypes.FormatError(code).strip(), "reads": []}
    result = {"device": DEVICE, "opened": True, "ioctl": hex(IOCTL_EC_READ), "reads": []}
    try:
        for address in addresses:
            if not 0 <= address <= 0xFFFF:
                raise ValueError("EC addresses must fit in u16")
            argument, output, returned = wintypes.DWORD(address), wintypes.DWORD(0), wintypes.DWORD(0)
            ctypes.set_last_error(0)
            start = time.perf_counter()
            ok = kernel.DeviceIoControl(handle, IOCTL_EC_READ, ctypes.byref(argument), 4,
                                        ctypes.byref(output), 4, ctypes.byref(returned), None)
            error = ctypes.get_last_error() if not ok else 0
            result["reads"].append({"address": hex(address), "transport_ok": bool(ok),
                                    "win32_error": error, "returned_bytes": returned.value,
                                    "output_u32": output.value, "byte": output.value & 0xFF,
                                    "duration_ms": round((time.perf_counter()-start)*1000, 3)})
    finally:
        kernel.CloseHandle(handle)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("addresses", nargs="+", type=lambda s: int(s, 0))
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = read_addresses(args.addresses)
    text = json.dumps(result, ensure_ascii=False, indent=2)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(text + "\n", encoding="utf-8")
    print(text)
