"""Real Windows readback; optional brightness and charge-limit round trips restore state.

Default only reads hardware and tests display modes. Does not change MUX/OEM/autostart.
"""
import argparse
import ctypes
from ctypes import wintypes
import json
import os
import struct
from pathlib import Path
import sys
import tempfile
import time

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "reverse/native"))
from probe_ec import read_addresses


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--library", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--round-trip", action="store_true")
    parser.add_argument("--battery-round-trip", action="store_true")
    parser.add_argument("--refresh-round-trip", action="store_true")
    args = parser.parse_args()
    os.environ.pop("JIYAOCHU_FORCE_MOCK", None)
    report = {"passed": False, "errors": [], "brightness_writes": 0, "battery_writes": 0, "refresh_writes": 0}
    with tempfile.TemporaryDirectory(prefix="lumadesk-display-controls-") as directory:
        os.environ["JIYAOCHU_DATA_DIR"] = directory
        core = ctypes.CDLL(str(Path(args.library).resolve()))
        core.lumadesk_call.argtypes = [ctypes.c_char_p, ctypes.c_char_p]
        core.lumadesk_call.restype = ctypes.c_void_p
        core.lumadesk_free.argtypes = [ctypes.c_void_p]

        def call(command, arguments=None):
            ptr = core.lumadesk_call(command.encode(), None if arguments is None else json.dumps(arguments).encode())
            try:
                reply = json.loads(ctypes.string_at(ptr).decode())
                if not reply.get("ok"):
                    raise RuntimeError(f"{command}: {reply.get('error')}")
                return reply["data"]
            finally:
                core.lumadesk_free(ptr)

        try:
            snapshot = read_addresses([0x740, 0x770, 0x7B9, 0x7D0, 0x7A6])
            report["ec_before"] = snapshot
            raw = {item["address"]: item["byte"] for item in snapshot["reads"] if item["transport_ok"]}
            battery = call("get_hardware_status")["battery"]
            report["battery"] = battery
            if raw.get("0x740") == 0x1A and "0x7b9" in raw:
                expected = raw["0x7b9"] & 0x7F
                expected = 100 if expected == 0 else expected
                assert battery["limit"] == expected, f"Battery threshold raw {raw['0x7b9']:#x}: expected {expected}, got {battery}"
            if args.battery_round_trip:
                original = battery["limit"]
                changed = 60 if original != 60 else 80
                try:
                    report["battery_writes"] += 1
                    call("set_battery_limit", {"limit": changed})
                    assert call("get_hardware_status")["battery"]["limit"] == changed
                    after = read_addresses([0x7A6])["reads"][0]["byte"]
                    assert after & ~0x31 == raw["0x7a6"] & ~0x31, "Charge mode write changed unrelated EC flags"
                    report["battery_changed"] = changed
                finally:
                    report["battery_writes"] += 1
                    call("set_battery_limit", {"limit": original})
                    report["battery_restored"] = call("get_hardware_status")["battery"]["limit"]
                    assert report["battery_restored"] == original
        except Exception as error:
            report["errors"].append(str(error))
        try:
            original = call("get_display_brightness")
            assert 0 <= original <= 100
            report["brightness_before"] = original
            if args.round_trip:
                changed = original + 1 if original < 100 else original - 1
                try:
                    report["brightness_writes"] += 1
                    call("set_display_brightness", {"level": changed})
                    time.sleep(0.3)
                    report["brightness_changed"] = call("get_display_brightness")
                    assert report["brightness_changed"] == changed
                finally:
                    report["brightness_writes"] += 1
                    call("set_display_brightness", {"level": original})
                    time.sleep(0.3)
                    report["brightness_restored"] = call("get_display_brightness")
                    assert report["brightness_restored"] == original
        except Exception as error:
            report["errors"].append(str(error))
        try:
            report["displays"] = call("get_displays")
            if args.refresh_round_trip:
                user = ctypes.WinDLL("user32", use_last_error=True)
                user.EnumDisplaySettingsW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, ctypes.c_void_p]
                user.EnumDisplaySettingsW.restype = wintypes.BOOL
                def dimensions(device):
                    # DEVMODEW from wingdi.h: fixed 220-byte layout, dmSize at 68,
                    # dmBitsPerPel/dmPelsWidth/dmPelsHeight at 168/172/176.
                    mode = ctypes.create_string_buffer(220)
                    struct.pack_into("<H", mode, 68, 220)
                    if not user.EnumDisplaySettingsW(device, 0xFFFFFFFF, mode):
                        raise ctypes.WinError(ctypes.get_last_error())
                    return list(struct.unpack_from("<III", mode, 168))
                monitor = report["displays"][0]
                device, original = monitor["device_name"], monitor["current_hz"]
                before = dimensions(device)
                report["refresh_resolution_before"] = before
                report["refresh_candidates"] = []
                try:
                    for hz in (90, 120):
                        if hz not in monitor["available_hz"]:
                            report["refresh_candidates"].append({"hz": hz, "supported": False})
                            continue
                        report["refresh_writes"] += 1
                        call("set_display_monitor_refresh_rate", {"device_name": device, "hz": hz})
                        actual = next(item["current_hz"] for item in call("get_displays") if item["device_name"] == device)
                        assert actual == hz, f"Requested {hz} Hz, read {actual}"
                        assert dimensions(device) == before, "Refresh rate change altered resolution/color depth"
                        report["refresh_candidates"].append({"hz": hz, "supported": True, "readback": actual})
                finally:
                    report["refresh_writes"] += 1
                    call("set_display_monitor_refresh_rate", {"device_name": device, "hz": original})
                    report["refresh_restored"] = next(item["current_hz"] for item in call("get_displays") if item["device_name"] == device)
                    assert report["refresh_restored"] == original and dimensions(device) == before
        except Exception as error:
            report["errors"].append(str(error))
    report["passed"] = not report["errors"]
    output = Path(args.output).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False))
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
