"""Reversible EC write checks for project 0x19 (Yilong 15 Pro / GM5HG0A).

Run from an administrator terminal with the OEM control center set to a
built-in profile (not the custom one):

    python reverse/native/evidence/project-19/write_test.py

Writes are limited to two fields the OEM control center itself changes:
the 0xB0 profile field of EC 0x751 and the full-fan bit 2 of EC 0x768. Every
other bit is preserved, each write is read back, and the starting values are
restored even when a step fails. The firmware GPU-mode variable is only read.
"""
import ctypes
import json
import pathlib
import sys
import time
from ctypes import wintypes

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parents[1]))
import probe_mux  # noqa: E402  read-only firmware variable probe

READ, WRITE = 0x9C40A488, 0x9C40A48C
WATCH = [0x460, 0x461, 0x464, 0x465, 0x46A, 0x46B, 0x46C, 0x46D, 0x46F, 0x726,
         0x743, 0x745, 0x746, 0x751, 0x75B, 0x75C, 0x767, 0x768, 0x783, 0x784,
         0x785, 0x7AB, 0x7C4, 0x7D4, 0x7D5]
PROFILES = [("turbo", 0x10), ("office", 0xA0), ("balanced", 0x00)]
SETTLE_SECONDS = 6

kernel = ctypes.WinDLL("kernel32", use_last_error=True)
kernel.CreateFileW.restype = wintypes.HANDLE
kernel.CreateFileW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD, ctypes.c_void_p,
                               wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE]
kernel.DeviceIoControl.restype = wintypes.BOOL
kernel.DeviceIoControl.argtypes = [wintypes.HANDLE, wintypes.DWORD, ctypes.c_void_p, wintypes.DWORD,
                                   ctypes.c_void_p, wintypes.DWORD, ctypes.POINTER(wintypes.DWORD),
                                   ctypes.c_void_p]
kernel.CloseHandle.argtypes = [wintypes.HANDLE]


class Ec:
    def __init__(self):
        self.handle = kernel.CreateFileW(r"\\.\ACPIDriver", 0xC0000000, 3, None, 3, 0, None)
        if self.handle == ctypes.c_void_p(-1).value:
            raise OSError(ctypes.get_last_error(), "cannot open \\\\.\\ACPIDriver")

    def read(self, address):
        argument, output, returned = wintypes.DWORD(address), wintypes.DWORD(0), wintypes.DWORD(0)
        ok = kernel.DeviceIoControl(self.handle, READ, ctypes.byref(argument), 4,
                                    ctypes.byref(output), 4, ctypes.byref(returned), None)
        if not ok or returned.value != 4:
            raise OSError(ctypes.get_last_error(), f"EC read {address:#x} failed")
        return output.value & 0xFF

    def write_field(self, address, mask, value):
        """Replace only `mask` bits, then require the readback to match."""
        assert value & ~mask == 0
        target = (self.read(address) & ~mask & 0xFF) | value
        argument = (wintypes.DWORD * 2)(address, target)
        output, returned = wintypes.DWORD(0), wintypes.DWORD(0)
        if not kernel.DeviceIoControl(self.handle, WRITE, argument, 8,
                                      ctypes.byref(output), 4, ctypes.byref(returned), None):
            raise OSError(ctypes.get_last_error(), f"EC write {address:#x} failed")
        actual = self.read(address)
        if actual & mask != value:
            raise RuntimeError(f"{address:#x}: wrote {target:#04x}, read back {actual:#04x}")

    def watch(self):
        row = {f"{address:#05x}": self.read(address) for address in WATCH}
        row["cpu_rpm"] = (row["0x464"] << 8) | row["0x465"]
        row["gpu_rpm"] = (row["0x46c"] << 8) | row["0x46d"]
        return row

    def close(self):
        kernel.CloseHandle(self.handle)


def show(label, row):
    print(f"  {label:<10} 751={row['0x751']:#04x} PL1/PL2/PL4={row['0x46a']}/{row['0x46b']}/{row['0x46f']}"
          f" 743={row['0x743']:#04x} 746={row['0x746']} 768={row['0x768']:#04x}"
          f" fans={row['cpu_rpm']}/{row['gpu_rpm']}")


def main():
    report = {"captured_at": time.strftime("%Y-%m-%dT%H:%M:%S%z"), "profiles": {}, "fan_boost": {}}
    print("[1/3] GPU mode firmware variable (read only)")
    report["mux"] = probe_mux.read_firmware()
    for row in report["mux"]["variables"]:
        print(f"  {row['name']}: read_ok={row['read_ok']} error={row['win32_error']}"
              f" mode_byte={row.get('display_mode_byte')} decoded={row.get('configured_mode')}"
              f" ap_version={row.get('ap_version')} first_byte={row.get('first_byte')}")

    ec = Ec()
    try:
        project = ec.read(0x740)
        if project != 0x19:
            raise SystemExit(f"EC project is {project:#04x}, not 0x19; nothing was written")
        if ec.read(0x726) & 0x80:
            raise SystemExit("The OEM custom profile is active; pick a built-in profile first")
        start_profile, start_boost = ec.read(0x751) & 0xB0, ec.read(0x768) & 0x04
        report["start"] = ec.watch()
        show("start", report["start"])

        print("[2/3] Profile field of 0x751 only")
        try:
            for name, value in PROFILES:
                ec.write_field(0x751, 0xB0, value)
                time.sleep(SETTLE_SECONDS)
                report["profiles"][name] = ec.watch()
                show(name, report["profiles"][name])
        finally:
            ec.write_field(0x751, 0xB0, start_profile)

        print("[3/3] Full-fan bit 2 of 0x768")
        try:
            for name, value in (("on", 0x04), ("off", 0x00)):
                ec.write_field(0x768, 0x04, value)
                time.sleep(SETTLE_SECONDS)
                report["fan_boost"][name] = ec.watch()
                show(name, report["fan_boost"][name])
        finally:
            ec.write_field(0x768, 0x04, start_boost)

        time.sleep(SETTLE_SECONDS)
        report["restored"] = ec.watch()
        show("restored", report["restored"])
    finally:
        ec.close()
        output = HERE / "write-test.json"
        output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"saved {output}")


if __name__ == "__main__":
    main()
