"""Exercise preference persistence and safe restore through the production C ABI."""
import argparse
import ctypes
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile


def exercise(library: Path, stage: str) -> None:
    core = ctypes.CDLL(str(library))
    core.lumadesk_call.argtypes = [ctypes.c_char_p, ctypes.c_char_p]
    core.lumadesk_call.restype = ctypes.c_void_p
    core.lumadesk_free.argtypes = [ctypes.c_void_p]

    def call(command, arguments=None):
        ptr = core.lumadesk_call(command.encode(), None if arguments is None else json.dumps(arguments).encode())
        try:
            response = json.loads(ctypes.string_at(ptr))
        finally:
            core.lumadesk_free(ptr)
        assert response["ok"], response
        return response["data"]

    assert call("system.ping")["pong"]
    if stage == "write_off":
        assert not call("get_autostart"), "Mock queried a real login task"
        call("set_autostart", {"enabled": True})
        assert call("get_autostart")
        call("set_autostart_enabled", {"enabled": False})
        assert not call("get_autostart"), "Mock autostart aliases do not share simulated state"
        call("toggle_oem_service", {"enable": True})
        assert call("get_oem_status")["taken_over"]
        call("restore_official_control_center")
        assert not call("get_oem_status")["taken_over"], "Mock OEM restore did not update simulated state"
        call("set_log_level", {"level": "off"})
        assert call("get_app_config")["log_level"] == "off", "Log level was not saved"
        assert not call("get_log_status")["enabled"]
    elif stage == "restart_off":
        status = call("get_log_status")
        assert status["level"] == "off" and not status["enabled"], "Saved logging opt-out was ignored at startup"
        call("set_log_level", {"level": "debug"})
        status = call("get_log_status")
        assert status["level"] == "debug" and status["enabled"], "Changing level did not reenable logging"
        assert call("get_app_config")["log_level"] == "debug"
    else:
        status = call("get_log_status")
        assert status["level"] == "debug" and status["enabled"], "Saved log verbosity was ignored at startup"
        before = call("get_app_config")
        changed = dict(before)
        changed.update(gpu_mode="dgpu", takeover_oem=not before["takeover_oem"], autostart=not before["autostart"], battery_limit=60,
                       power_mode=2, fan_boost=not before["fan_boost"], win_key_locked=not before["win_key_locked"],
                       log_level="trace", log_enabled=True, auto_power_mode=True, power_mode_ac=2, power_mode_battery=0)
        changed["osd"] = dict(before["osd"], opacity=37, watch_physical_profile=True)
        effective = call("restore_app_settings", {"cfg": changed})
        for field in ("gpu_mode", "takeover_oem", "autostart", "battery_limit", "power_mode", "fan_boost", "win_key_locked"):
            assert effective[field] == before[field], f"Restore altered protected setting: {field}"
        assert effective["osd"]["opacity"] == 37 and effective["osd"]["watch_physical_profile"]
        assert effective["log_level"] == "trace" and call("get_log_status")["level"] == "trace"
        assert call("get_hardware_status")["power_mode"] == 1, "Preference restore applied hardware mode"
        persisted = json.loads((Path(os.environ["JIYAOCHU_DATA_DIR"]) / "config.json").read_text())
        assert persisted == effective, "Restore memory and disk differ"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--library", type=Path, required=True)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--stage", choices=("write_off", "restart_off", "restart_debug"), help=argparse.SUPPRESS)
    args = parser.parse_args()
    library = args.library.resolve(strict=True)
    if args.stage:
        assert os.environ.get("JIYAOCHU_FORCE_MOCK") == "1" and os.environ.get("JIYAOCHU_DATA_DIR"), "Require isolated mock"
        exercise(library, args.stage)
        return
    with tempfile.TemporaryDirectory(prefix="LumaDesk-preferences-") as directory:
        environment = dict(os.environ, JIYAOCHU_FORCE_MOCK="1", JIYAOCHU_DATA_DIR=directory)
        for stage in ("write_off", "restart_off", "restart_debug"):
            subprocess.run([sys.executable, __file__, "--library", str(library), "--stage", stage], env=environment, check=True, timeout=30)
    report = {"passed": True, "mock_system_controls_isolated": True, "log_preferences_survive_restart": True, "logging_reenable": True,
              "restore_preserves_device_and_system_settings": True}
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report))


if __name__ == "__main__":
    main()
