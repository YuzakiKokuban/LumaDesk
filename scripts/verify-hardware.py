"""Local administrator smoke test. Only the fan boost bit is changed and restored.

Usage: python scripts/verify-hardware.py --library target/debug/jiyaochu_core.dll
       --output artifacts/hardware-validation.json
The MUX is read only. No reboot, charge threshold, power plan or OEM service changes.
"""
import argparse
import ctypes
from ctypes import wintypes
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "reverse/native"))
from probe_ec import read_addresses


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--library", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    output = Path(args.output).resolve()
    os.environ["JIYAOCHU_DATA_DIR"] = str(output.parent / "verification-config")
    core = ctypes.CDLL(str(Path(args.library).resolve()))
    core.lumadesk_call.argtypes = [ctypes.c_char_p, ctypes.c_char_p]
    core.lumadesk_call.restype = ctypes.c_void_p
    core.lumadesk_free.argtypes = [ctypes.c_void_p]
    def call(name):
        ptr = core.lumadesk_call(name.encode(), None)
        try:
            return json.loads(ctypes.string_at(ptr).decode())
        finally:
            core.lumadesk_free(ptr)
    result = {"captured_utc": datetime.now(timezone.utc).isoformat(),
              "gpu_mode": call("get_gpu_mode_info"), "hardware": call("get_hardware_status")}
    # Machine identifiers are unnecessary in the reviewable artifact.
    result.get("hardware", {}).get("data", {}).get("device", {}).pop("serial", None)
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.CreateFileW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD,
                                   ctypes.c_void_p, wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE]
    kernel.CreateFileW.restype = wintypes.HANDLE
    kernel.DeviceIoControl.argtypes = [wintypes.HANDLE, wintypes.DWORD, ctypes.c_void_p,
                                       wintypes.DWORD, ctypes.c_void_p, wintypes.DWORD,
                                       ctypes.POINTER(wintypes.DWORD), ctypes.c_void_p]
    kernel.DeviceIoControl.restype = wintypes.BOOL
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    handle = kernel.CreateFileW(r"\\.\ACPIDriver", 0xC0000000, 3, None, 3, 0, None)
    test = result["fan_boost_roundtrip"] = {}
    def ioctl(code, values):
        values = (wintypes.DWORD * len(values))(*values)
        value, returned = wintypes.DWORD(), wintypes.DWORD()
        if not kernel.DeviceIoControl(handle, code, values, ctypes.sizeof(values),
                                      ctypes.byref(value), 4, ctypes.byref(returned), None):
            raise ctypes.WinError(ctypes.get_last_error())
        if returned.value != 4:
            raise RuntimeError(f"Unexpected returned length {returned.value}")
        return value.value & 255
    if handle == ctypes.c_void_p(-1).value:
        test["error"] = str(ctypes.WinError(ctypes.get_last_error()))
    else:
        original = None
        try:
            original = ioctl(0x9C40A488, [0x751])
            test["original"] = original
            wanted = original ^ 0x40
            ioctl(0x9C40A48C, [0x751, wanted])
            test["changed"] = ioctl(0x9C40A488, [0x751])
            test["write_verified"] = test["changed"] == wanted
        except Exception as error:
            test["error"] = str(error)
        finally:
            try:
                if original is not None:
                    current = ioctl(0x9C40A488, [0x751])
                    # Restore only the owned bit; preserve any concurrent firmware updates.
                    ioctl(0x9C40A48C, [0x751, (current & ~0x40) | (original & 0x40)])
                    test["restored"] = ioctl(0x9C40A488, [0x751])
                    test["restore_verified"] = test["restored"] & 0x40 == original & 0x40
            except Exception as error:
                test["restore_error"] = str(error)
            finally:
                kernel.CloseHandle(handle)
    result["ec_snapshot"] = read_addresses([0x740, 0x751, 0x768, 0x770, 0x7A6, 0x7B9, 0x7D0])
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(output)


if __name__ == "__main__":
    main()
