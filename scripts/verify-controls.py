"""Administrator checks for local controls; every tested setting is restored."""
import argparse
import ctypes
from ctypes import wintypes
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
REGISTERS = [0x751, 0x7ab, 0x783, 0x784, 0x785, 0x45b, 0x726, 0x727,
             0x767, 0x769, 0x76a, 0x76b, 0x78c, 0x7c5,
             0x770, 0x7b9, 0x7d0, 0x7a6, 0x768]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--library", default=str(ROOT / "target/debug/jiyaochu_core.dll"))
    parser.add_argument("--output", default=str(ROOT / "reverse/native/evidence/control-validation.json"))
    parser.add_argument("--hold", type=float, default=0.3, help="Seconds to observe each keyboard intensity")
    args = parser.parse_args()
    output = Path(args.output).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    report = {"captured_utc": datetime.now(timezone.utc).isoformat(), "passed": False}
    before, handle, windows_mode, call, kernel = {}, None, None, None, None
    try:
        if not ctypes.windll.shell32.IsUserAnAdmin():
            raise RuntimeError("Run this verification as administrator")
        library = Path(args.library).resolve()
        # Takeover uses the real application backup, before isolating test config.
        cli = library.with_name("jiyaochu-ctl.exe")
        process = subprocess.run([str(cli), "toggle_oem_service", '{"enable":true}'],
                                 capture_output=True, encoding="utf-8", timeout=60)
        if process.returncode:
            raise RuntimeError(process.stdout + process.stderr)
        report["oem_takeover"] = json.loads(process.stdout)
        os.environ["JIYAOCHU_DATA_DIR"] = str(output.parent / "control-test-config")
        core = ctypes.CDLL(str(library))
        core.lumadesk_call.argtypes = [ctypes.c_char_p, ctypes.c_char_p]
        core.lumadesk_call.restype = ctypes.c_void_p
        core.lumadesk_free.argtypes = [ctypes.c_void_p]
        def call(command, arguments=None):
            pointer = core.lumadesk_call(command.encode(), None if arguments is None else json.dumps(arguments).encode())
            try:
                reply = json.loads(ctypes.string_at(pointer).decode())
                if not reply.get("ok"):
                    raise RuntimeError(f"{command}: {reply}")
                return reply["data"]
            finally:
                core.lumadesk_free(pointer)
        kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel.CreateFileW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD, ctypes.c_void_p,
                                       wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE]
        kernel.CreateFileW.restype = wintypes.HANDLE
        kernel.DeviceIoControl.argtypes = [wintypes.HANDLE, wintypes.DWORD, ctypes.c_void_p, wintypes.DWORD,
                                           ctypes.c_void_p, wintypes.DWORD, ctypes.POINTER(wintypes.DWORD), ctypes.c_void_p]
        kernel.DeviceIoControl.restype = wintypes.BOOL
        kernel.CloseHandle.argtypes = [wintypes.HANDLE]
        handle = kernel.CreateFileW(r"\\.\ACPIDriver", 0xc0000000, 3, None, 3, 0, None)
        if handle == ctypes.c_void_p(-1).value:
            handle = None
            raise ctypes.WinError(ctypes.get_last_error())
        def ioctl(code, values):
            data = (wintypes.DWORD * len(values))(*values)
            value, returned = wintypes.DWORD(), wintypes.DWORD()
            if not kernel.DeviceIoControl(handle, code, data, ctypes.sizeof(data), ctypes.byref(value), 4,
                                           ctypes.byref(returned), None):
                raise ctypes.WinError(ctypes.get_last_error())
            if returned.value != 4:
                raise RuntimeError("Invalid EC reply length")
            return value.value & 255
        def read(address): return ioctl(0x9c40a488, [address])
        def write(address, value): return ioctl(0x9c40a48c, [address, value])
        before = {address: read(address) for address in REGISTERS}
        report["before"] = {hex(address): value for address, value in before.items()}
        status = call("get_hardware_status")
        status.get("device", {}).pop("serial", None)
        report["telemetry"] = status
        report["mux_read"] = call("get_gpu_mode_info")
        settings = call("get_power_settings")
        windows_mode = settings["windows_power_mode"]
        assert settings["schemes"], "No Windows power schemes detected"
        report["oem_modes"] = []
        for mode in [0, 1, 2]:
            call("set_power_mode", {"mode": mode})
            actual = call("get_power_settings")["power_mode"]
            report["oem_modes"].append({"requested": mode, "actual": actual})
            assert actual == mode
        report["windows_modes"] = []
        for mode in [0, 1, 2]:
            call("set_windows_power_mode", {"mode": mode})
            actual = call("get_power_settings")["windows_power_mode"]
            report["windows_modes"].append({"requested": mode, "actual": actual})
            assert actual == mode
        state = call("get_lighting_state")
        report["rgb"] = []
        # Hold a constant color while testing intensity, then test every palette color.
        palette = ["#00ffff", "#6366f1", "#10b981", "#ef4444", "#f59e0b", "#ec4899", "#3b82f6", "#ffffff"]
        changes = [("#ffffff", level) for level in [0, 1, 2, 3, 4]] + [(color, 3) for color in palette]
        for color, level in changes:
            state.update(enabled=level > 0, kb_engine="hardware", kb_effect=0, kb_color=color, kb_brightness=level)
            call("apply_keyboard_lighting", {"lighting": state})
            time.sleep(args.hold)
            control, effect = read(0x78c), read(0x7c5)
            expected_control = (before[0x78c] & 0x0d) | [0x12, 0x30, 0x50, 0x70, 0x90][level]
            actual = call("get_lighting_state")
            entry = {"color": color, "requested_brightness": level, "control_78c": control,
                     "effect_7c5": effect, "rgb_raw": [read(a) for a in [0x769, 0x76a, 0x76b]], "state": actual}
            report["rgb"].append(entry)
            assert control & ~0x10 == expected_control & ~0x10, entry
            assert not effect & 7, entry
            assert actual["enabled"] == (level > 0), entry
            if level:
                assert actual["kb_brightness"] == level, entry
                expected = [max(1, int(color[1+i*2:3+i*2], 16) * 50 // 255) if int(color[1+i*2:3+i*2], 16) else 0 for i in range(3)]
                assert entry["rgb_raw"] == expected, entry
            # OEM profile changes must leave keyboard intensity and color intact.
            for mode in [0, 1, 2]:
                call("set_power_mode", {"mode": mode})
                assert read(0x78c) & ~0x10 == control & ~0x10
                assert not read(0x7c5) & 7
        report["battery_limits"] = []
        for mode, limit in [("long_life", 60), ("balanced", 80), ("workstation", 100)]:
            call("set_battery_mode", {"mode": mode})
            entry = {"limit": limit, "registers": {hex(a): read(a) for a in [0x770, 0x7b9, 0x7d0, 0x7a6]}}
            report["battery_limits"].append(entry)
            assert read(0x7b9) == limit and read(0x7d0) == limit - 5
            assert call("get_hardware_status")["battery"]["limit"] == limit
        report["win_key_lock"] = []
        for enabled in [True, False]:
            call("set_device_switch", {"id": "win_key_lock", "enabled": enabled})
            actual = next(item for item in call("get_device_switches") if item["id"] == "win_key_lock")["enabled"]
            report["win_key_lock"].append({"requested": enabled, "actual": actual})
            assert actual == enabled
        report["fan_boost"] = []
        for enabled in [True, False]:
            call("set_fan_boost", {"enabled": enabled})
            time.sleep(3)
            status = call("get_hardware_status")
            report["fan_boost"].append({"requested": enabled, "actual": status["fan_boost"], "fans": status["fans"]})
            assert status["fan_boost"] == enabled and status["fans"]["available"]
        report["passed"] = True
    except Exception as error:
        report["error"] = str(error)
    finally:
        errors = report["restore_errors"] = []
        if windows_mode is not None and call:
            try: call("set_windows_power_mode", {"mode": windows_mode})
            except Exception as error: errors.append(str(error))
        if before and handle:
            for address, value in before.items():
                if address == 0x767: continue
                try: write(address, value)
                except Exception as error: errors.append(str(error))
            try:
                write(0x767, before[0x767] | 0x20)
                time.sleep(0.3)
                report["restored"] = {hex(address): read(address) for address in REGISTERS}
                report["ec_restored"] = all(read(address) == value for address, value in before.items() if address != 0x767)
                if not report["ec_restored"]: errors.append("EC restore mismatch")
            except Exception as error: errors.append(str(error))
        if kernel and handle: kernel.CloseHandle(handle)
        report["passed"] = report["passed"] and not errors
        output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
