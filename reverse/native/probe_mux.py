"""Read-only firmware probe. No SetFirmwareEnvironmentVariable binding exists."""
import argparse
import ctypes
import datetime
import hashlib
import json
import platform
from ctypes import wintypes as w
from pathlib import Path
from mux_protocol import GUID, decode_mode


class LUID(ctypes.Structure):
    _fields_ = [("LowPart", w.DWORD), ("HighPart", w.LONG)]


class LUID_AND_ATTRIBUTES(ctypes.Structure):
    _fields_ = [("Luid", LUID), ("Attributes", w.DWORD)]


class TOKEN_PRIVILEGES(ctypes.Structure):
    _fields_ = [("PrivilegeCount", w.DWORD), ("Privileges", LUID_AND_ATTRIBUTES * 1)]


def read_firmware():
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    advapi = ctypes.WinDLL("advapi32", use_last_error=True)
    kernel.GetCurrentProcess.restype = w.HANDLE
    kernel.CloseHandle.argtypes = [w.HANDLE]
    kernel.GetFirmwareEnvironmentVariableW.argtypes = [w.LPCWSTR, w.LPCWSTR, w.LPVOID, w.DWORD]
    kernel.GetFirmwareEnvironmentVariableW.restype = w.DWORD
    advapi.OpenProcessToken.argtypes = [w.HANDLE, w.DWORD, ctypes.POINTER(w.HANDLE)]
    advapi.LookupPrivilegeValueW.argtypes = [w.LPCWSTR, w.LPCWSTR, ctypes.POINTER(LUID)]
    advapi.AdjustTokenPrivileges.argtypes = [w.HANDLE, w.BOOL, ctypes.POINTER(TOKEN_PRIVILEGES), w.DWORD, w.LPVOID, w.LPVOID]
    token = w.HANDLE()
    privileges = TOKEN_PRIVILEGES()
    privileges.PrivilegeCount = 1
    privileges.Privileges[0].Attributes = 2
    stages = []
    if advapi.OpenProcessToken(kernel.GetCurrentProcess(), 0x28, ctypes.byref(token)):
        try:
            if advapi.LookupPrivilegeValueW(None, "SeSystemEnvironmentPrivilege", ctypes.byref(privileges.Privileges[0].Luid)):
                ctypes.set_last_error(0)
                ok = advapi.AdjustTokenPrivileges(token, False, ctypes.byref(privileges), 0, None, None)
                stages.append({"stage": "AdjustTokenPrivileges", "api_ok": bool(ok), "win32_error": ctypes.get_last_error()})
            else:
                stages.append({"stage": "LookupPrivilegeValueW", "win32_error": ctypes.get_last_error()})
        finally:
            kernel.CloseHandle(token)
    else:
        stages.append({"stage": "OpenProcessToken", "win32_error": ctypes.get_last_error()})
    cpu = platform.processor()
    cpu_platform = "amd" if any(s in cpu.upper() for s in ("AMD", "RYZEN", "RADEON")) else "intel"
    rows = []
    for name in ("OemMagicVariable", "UniWillVariable", "OemDgpuPresent"):
        buffer = ctypes.create_string_buffer(512)
        ctypes.set_last_error(0)
        length = kernel.GetFirmwareEnvironmentVariableW(name, GUID, buffer, len(buffer))
        row = {"name": name, "read_ok": bool(length), "returned_bytes": length,
               "win32_error": ctypes.get_last_error() if not length else 0}
        if length:
            raw = buffer.raw[:length]
            row["sha256"] = hashlib.sha256(raw).hexdigest()
            if name == "OemDgpuPresent":
                row["first_byte"] = raw[0]
            else:
                row["ap_version"] = raw[0x43] if length > 0x43 else None
                row["display_mode_byte"] = raw[0x62] if length > 0x62 else None
                row["configured_mode"] = decode_mode(cpu_platform, raw[0x62]) if length > 0x62 else None
        rows.append(row)
    return {"captured_at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
            "guid": GUID, "cpu_platform": cpu_platform, "cpu_identifier": cpu,
            "token_privilege_stages": stages, "variables": rows,
            "interpretation": "Configured NVRAM mode; this read alone does not prove active physical display routing."}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = read_firmware()
    serialized = json.dumps(result, ensure_ascii=False, indent=2)
    if args.output:
        args.output.write_text(serialized+"\n", encoding="utf-8")
    print(serialized)
