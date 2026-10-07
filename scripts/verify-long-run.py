"""Real Windows FFI telemetry, with optional bounded brightness restoration.

Default operations only read. No mock, MUX/OEM/task changes, forced sleep or reboot.
Physical sleep/wake evidence comes from Windows power notifications, not time gaps.
Reports contain allowlisted numeric/capability fields, never raw backend payloads.
"""
import argparse
from collections import Counter, deque
import ctypes
from ctypes import wintypes
import json
import math
import os
from pathlib import Path
import tempfile
import threading
import time


PAGES = ("overview", "tuning", "display", "system", "lighting")
FIELDS = {
    "cpu": ("temp", "load", "freq_mhz", "power_w"),
    "gpu": ("present", "temp", "load", "freq_mhz", "vram_used_mb", "vram_total_mb"),
    "fans": ("available", "cpu_rpm", "gpu_rpm"),
    "battery": ("percent", "charging", "on_ac", "limit", "health_percent"),
}


def error_code(error):
    """Classify without persisting driver messages, paths, serials or credentials."""
    text = str(error).lower()
    if any(word in text for word in ("unknown command", "unrecognised command", "unrecognized command")):
        return "command_missing"
    if any(word in text for word in ("unsupported", "not supported", "not implemented", "access denied", "access is denied", "requires administrator", "requires elevation", "不支持", "未实现", "权限", "拒绝访问")):
        return "capability_unknown"
    return "backend_error"


def capabilities(value):
    output = {}
    if type(value) in (int, float) and math.isfinite(value):
        return {"value": {"state": "known", "value": value}}
    if not isinstance(value, dict):
        return output
    for group, fields in FIELDS.items():
        data = value.get(group)
        if not isinstance(data, dict):
            continue
        unavailable = data.get("available") is False or data.get("present") is False
        for field in fields:
            if field not in data:
                continue
            number = data[field]
            known = isinstance(number, (int, float)) and math.isfinite(number)
            output[group + "." + field] = {"state": "known", "value": number} if known and not unavailable else {"state": "unknown"}
    # These readback fields have no device identity or arbitrary text.
    for field in ("power_mode", "fan_boost", "elevated"):
        if field in value:
            number = value[field]
            output[field] = {"state": "known", "value": number} if isinstance(number, (int, float)) and math.isfinite(number) else {"state": "unknown"}
    for field in ("power_mode_error", "runtime_error", "error"):
        if value.get(field):
            output[field.removesuffix("_error")] = {"state": "unknown"}
    if isinstance(value.get("battery"), dict) and value["battery"].get("limit_error"):
        output["battery.limit"] = {"state": "unknown"}
    return output


class CommandMetrics:
    def __init__(self, name):
        self.name = name
        self.attempts = self.successes = self.failures = 0
        self.total = self.maximum = 0.0
        self.minimum = None
        self.durations = deque(maxlen=2048)
        self.errors = Counter()
        self.last_success = None
        self.last_attempt_succeeded = False
        self.fields = {}

    def record(self, now, duration, value=None, error=None):
        self.attempts += 1
        self.total += duration
        self.maximum = max(self.maximum, duration)
        self.minimum = duration if self.minimum is None else min(self.minimum, duration)
        self.durations.append(duration)
        self.last_attempt_succeeded = error is None
        if error is not None:
            self.failures += 1
            self.errors[error_code(error)] += 1
        else:
            self.successes += 1
            self.last_success = now
            self.fields = capabilities(value)

    def summary(self, now, stale_after):
        age = None if self.last_success is None else max(0, now - self.last_success)
        stale = not self.last_attempt_succeeded or age is None or age > stale_after
        fields = {key: dict(item) for key, item in self.fields.items()}
        if stale:
            for field in fields.values():
                if field["state"] == "known":
                    field["state"] = "stale"
        sorted_times = sorted(self.durations)
        def percentile(fraction):
            return None if not sorted_times else round(sorted_times[max(0, math.ceil(len(sorted_times) * fraction) - 1)] * 1000, 3)
        return {
            "attempts": self.attempts, "successes": self.successes, "failures": self.failures,
            "mean_ms": self.total * 1000 / self.attempts if self.attempts else None,
            "minimum_ms": None if self.minimum is None else self.minimum * 1000,
            "maximum_ms": self.maximum * 1000, "p50_ms": percentile(0.5), "p95_ms": percentile(0.95),
            "percentile_sample_count": len(sorted_times), "error_categories": dict(self.errors),
            "last_success_age_seconds": age, "stale": stale, "capabilities": fields,
        }


class ResumeEvidence:
    def __init__(self):
        self._gate = threading.Lock()
        self._events = deque(maxlen=256)
        self._suspended = False
        self._cycles = 0

    def record(self, kind, at=None):
        if kind not in ("suspend", "resume_suspend", "resume_automatic"):
            return
        with self._gate:
            self._events.append({"kind": kind, "utc_unix_seconds": time.time() if at is None else at})
            if kind == "suspend":
                self._suspended = True
            elif self._suspended:
                self._suspended = False
                self._cycles += 1

    def summary(self):
        with self._gate:
            return {"source": "windows_power_notifications", "status": "observed" if self._cycles else "pending",
                    "completed_cycles": self._cycles, "suspend_without_resume": self._suspended, "events": list(self._events)}


class WindowsPowerObserver:
    """PowerRegisterSuspendResumeNotification's callback needs no message loop."""
    def __init__(self, evidence):
        self.evidence = evidence
        self.handle = wintypes.HANDLE()
        self.library = None
        self.callback = self.parameters = None

    def start(self):
        callback_type = ctypes.WINFUNCTYPE(wintypes.DWORD, ctypes.c_void_p, wintypes.DWORD, ctypes.c_void_p)
        class Parameters(ctypes.Structure):
            _fields_ = [("callback", callback_type), ("context", ctypes.c_void_p)]
        def notify(context, event_type, setting):
            kind = {4: "suspend", 7: "resume_suspend", 18: "resume_automatic"}.get(event_type)
            if kind:
                self.evidence.record(kind)
            return 0
        self.callback = callback_type(notify)
        self.parameters = Parameters(self.callback, None)
        self.library = ctypes.WinDLL("powrprof", use_last_error=True)
        register = self.library.PowerRegisterSuspendResumeNotification
        register.argtypes = [wintypes.DWORD, ctypes.c_void_p, ctypes.POINTER(wintypes.HANDLE)]
        register.restype = wintypes.DWORD
        unregister = self.library.PowerUnregisterSuspendResumeNotification
        unregister.argtypes = [wintypes.HANDLE]
        unregister.restype = wintypes.DWORD
        result = register(2, ctypes.byref(self.parameters), ctypes.byref(self.handle))
        if result:
            raise OSError(result, "power_notification_registration_failed")

    def close(self):
        if self.library is not None and self.handle.value:
            result = self.library.PowerUnregisterSuspendResumeNotification(self.handle)
            self.handle = wintypes.HANDLE()
            if result:
                raise OSError(result, "power_notification_unregistration_failed")


def brightness_sequence(original):
    if type(original) is not int or not 1 <= original <= 100:
        return []
    return [level for offset in (1, 2, 1, -1, -2, -1)
            if (level := max(1, min(100, original + offset))) != original]


def poll_brightness(call, expected, *, timeout=1.0, poll_interval=0.05,
                    clock=time.monotonic, sleep=time.sleep):
    """Wait for authoritative WMI readback; retain only bounded numeric evidence."""
    started = clock()
    result = {"expected": expected, "matched": False, "attempts": 0, "observed": [], "errors": []}
    while True:
        result["attempts"] += 1
        try:
            actual = call("get_display_brightness")
            if type(actual) in (int, float) and math.isfinite(actual):
                result["observed"].append(actual)
                result["observed"] = result["observed"][-32:]
            if type(actual) is int and actual == expected:
                result["matched"] = True
        except Exception as error:
            result["errors"].append(error_code(error))
            result["errors"] = result["errors"][-32:]
        elapsed = clock() - started
        result["elapsed_seconds"] = elapsed
        if result["matched"] or elapsed >= timeout:
            return result
        sleep(min(poll_interval, timeout - elapsed))


def brightness_round_trip(call, sleep=time.sleep, result=None, *, clock=time.monotonic):
    """A failed/partially applied write still enters restoration, including Ctrl+C."""
    result = {} if result is None else result
    result.update(status="pending", restored=False, writes_attempted=0, errors=[])
    try:
        original = call("get_display_brightness")
    except Exception as error:
        result["errors"].append(error_code(error))
        return result
    sequence = brightness_sequence(original)
    if not sequence:
        result["pending_reason"] = "original_brightness_unknown_invalid_or_zero"
        return result
    result["original"] = original
    result["requested"] = sequence
    result["readbacks"] = []
    result["settling_checks"] = []
    result["restore_attempts"] = []
    try:
        for level in sequence:
            result["writes_attempted"] += 1
            call("set_display_brightness", {"level": level})
            check = poll_brightness(call, level, clock=clock, sleep=sleep)
            result["settling_checks"].append(check)
            if not check["matched"]:
                raise RuntimeError("brightness_readback_timeout")
            result["readbacks"].append(check["observed"][-1])
    except Exception as error:
        result["errors"].append("brightness_readback_timeout" if str(error) == "brightness_readback_timeout" else error_code(error))
    finally:
        # Do not allow Ctrl+C during restoration to skip the readback.
        for attempt in range(3):
            try:
                result["writes_attempted"] += 1
                call("set_display_brightness", {"level": original})
                check = poll_brightness(call, original, clock=clock, sleep=sleep)
                result["restore_attempts"].append(check)
                result["restored"] = check["matched"]
                if result["restored"]:
                    break
                if attempt == 2:
                    result["errors"].append("brightness_restore_readback_timeout")
            except KeyboardInterrupt:
                result["restore_attempts"].append({"expected": original, "matched": False, "error": "interrupted"})
                if attempt == 2:
                    result["errors"].append("brightness_restore_interrupted")
            except Exception as error:
                result["restore_attempts"].append({"expected": original, "matched": False, "error": error_code(error)})
                if attempt == 2:
                    result["errors"].append("brightness_restore_" + error_code(error))
        result["status"] = "passed" if result["restored"] and not result["errors"] else "failed"
    return result


def atomic_write(path, report):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary = tempfile.mkstemp(prefix=path.name + ".", suffix=".tmp", dir=path.parent)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8") as stream:
            json.dump(report, stream, ensure_ascii=False, indent=2, allow_nan=False)
            stream.write("\n")
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def monitor(backend, command_names, call, *, duration=300, interval=1, resume=None,
            require_resume=False, brightness=False, progress=lambda _: None,
            clock=time.monotonic, sleep=time.sleep):
    report = {"passed": False, "validation_status": "running", "physical_backend_verified": backend == "windows",
              "backend": "windows" if backend == "windows" else "unverified", "pending": [], "failures": [],
              "duration_requested_seconds": duration, "interval_seconds": interval, "commands": {}, "read_only": not brightness,
              "scope": "Rust FFI readback; shell navigation and installed UI are not exercised"}
    if backend != "windows":
        report.update(validation_status="failed", failures=["real_windows_backend_required"])
        progress(report)
        return report
    if not math.isfinite(duration) or duration <= 0 or not math.isfinite(interval) or interval <= 0:
        raise ValueError("Duration and interval must be finite positive numbers")
    resume = resume or ResumeEvidence()
    names = set(command_names)
    page_api = "get_page_status" in names
    if not page_api:
        report["pending"].append("page_status_command_missing")
    fallback = "get_hardware_status" in names
    if not page_api and not fallback:
        report["pending"].append("hardware_status_command_missing")
    if "get_display_brightness" not in names:
        report["pending"].append("brightness_read_command_missing")
    metrics = {}
    post_resume = set()
    resume_cycle = [0]
    started = clock()
    def synchronize_resume():
        cycles = resume.summary()["completed_cycles"]
        if cycles != resume_cycle[0]:
            post_resume.clear()
            resume_cycle[0] = cycles
        return cycles
    def tracked(command, arguments=None):
        key = command + (":" + arguments["page"] if command == "get_page_status" else "")
        metric = metrics.setdefault(key, CommandMetrics(key))
        cycle_before = synchronize_resume()
        before = clock()
        try:
            result = call(command, arguments)
            if command == "get_display_brightness" and (type(result) is not int or not 0 <= result <= 100):
                raise RuntimeError("invalid_brightness_readback")
            if command in ("get_page_status", "get_hardware_status") and not isinstance(result, dict):
                raise RuntimeError("invalid_status_readback")
        except Exception as error:
            metric.record(clock() - started, clock() - before, error=error)
            raise
        metric.record(clock() - started, clock() - before, result)
        if synchronize_resume() == cycle_before and cycle_before:
            post_resume.add(key)
        return result
    required_readbacks = ({"get_page_status:" + page for page in PAGES} if page_api else {"get_hardware_status"} if fallback else set())
    if "get_display_brightness" in names:
        required_readbacks.add("get_display_brightness")
    def resume_report():
        synchronize_resume()
        evidence = resume.summary()
        recovered = bool(evidence["completed_cycles"] and required_readbacks and required_readbacks <= post_resume)
        evidence.update(readback_status="passed" if recovered else "pending", successful_readbacks=sorted(post_resume),
                        required_readbacks=sorted(required_readbacks))
        return evidence
    def snapshot():
        elapsed = clock() - started
        report["elapsed_seconds"] = elapsed
        report["commands"] = {key: value.summary(elapsed, max(5, interval * (len(PAGES) if key.startswith("get_page_status:") else 1) * 2)) for key, value in metrics.items()}
        report["physical_suspend_resume"] = resume_report()
        progress(report)
    tick = 0
    interrupted = False
    try:
        if brightness:
            if {"get_display_brightness", "set_display_brightness"} <= names:
                report["brightness_round_trip"] = {}
                brightness_round_trip(tracked, sleep, report["brightness_round_trip"], clock=clock)
            else:
                report["pending"].append("brightness_round_trip_commands_missing")
        while clock() - started < duration:
            cycle_started = clock()
            readings = []
            if page_api:
                readings.append(("get_page_status", {"page": PAGES[tick % len(PAGES)]}))
            elif fallback:
                readings.append(("get_hardware_status", None))
            if "get_display_brightness" in names:
                readings.append(("get_display_brightness", None))
            for command, arguments in readings:
                try:
                    tracked(command, arguments)
                except Exception:
                    pass  # The categorized failure and retained/stale fields are in metrics.
            tick += 1
            snapshot()
            remaining = duration - (clock() - started)
            if remaining > 0:
                sleep(min(remaining, max(0.001, interval - (clock() - cycle_started))))
    except KeyboardInterrupt:
        interrupted = True
        report["failures"].append("validation_interrupted")
    finally:
        if page_api:
            for page in PAGES:
                if not metrics.get("get_page_status:" + page, CommandMetrics("")).successes:
                    report["pending"].append("page_not_successfully_sampled:" + page)
        for key, metric in metrics.items():
            if metric.errors["capability_unknown"] or metric.errors["command_missing"]:
                report["pending"].append("capability_unknown:" + key)
            if metric.failures > metric.errors["capability_unknown"] + metric.errors["command_missing"]:
                report["failures"].append("command_failed:" + key)
            for field, value in metric.fields.items():
                if value["state"] == "unknown":
                    report["pending"].append("unknown_readback:" + key + ":" + field)
        trip = report.get("brightness_round_trip")
        if trip and trip["status"] != "passed":
            report["failures" if trip["status"] == "failed" else "pending"].append("brightness_round_trip_" + trip["status"])
        if require_resume and not resume.summary()["completed_cycles"]:
            report["pending"].append("physical_suspend_resume_not_observed")
        elif require_resume and resume_report()["readback_status"] != "passed":
            report["pending"].append("post_resume_readback_pending")
        report["validation_status"] = "failed" if interrupted or report["failures"] else "pending" if report["pending"] else "passed"
        report["telemetry_passed"] = bool(metrics) and all(metric.successes and not metric.failures for metric in metrics.values())
        report["passed"] = report["validation_status"] == "passed"
        snapshot()
    return report


class RustCore:
    def __init__(self, library):
        self.library = ctypes.CDLL(str(Path(library).resolve()))
        self.library.lumadesk_init.argtypes = []
        self.library.lumadesk_init.restype = ctypes.c_void_p
        self.library.lumadesk_call.argtypes = [ctypes.c_char_p, ctypes.c_char_p]
        self.library.lumadesk_call.restype = ctypes.c_void_p
        self.library.lumadesk_free.argtypes = [ctypes.c_void_p]
        self.library.lumadesk_free.restype = None

    def decode(self, pointer):
        if not pointer:
            raise RuntimeError("empty_ffi_reply")
        try:
            reply = json.loads(ctypes.string_at(pointer).decode("utf-8"))
            if reply.get("ok") is not True:
                raise RuntimeError(reply.get("error") or "backend_error")
            return reply.get("data")
        finally:
            self.library.lumadesk_free(pointer)

    def initialize(self):
        result = self.decode(self.library.lumadesk_init())
        if not isinstance(result, dict) or result.get("abi_version") != 1:
            raise RuntimeError("unsupported_ffi_abi")
        return result

    def call(self, command, arguments=None):
        payload = None if arguments is None else json.dumps(arguments).encode("utf-8")
        return self.decode(self.library.lumadesk_call(command.encode("utf-8"), payload))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--library", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--duration-seconds", type=float, default=300)
    parser.add_argument("--interval", type=float, default=1)
    parser.add_argument("--require-resume", action="store_true", help="Manually sleep/wake during the run; absent observed events remain pending")
    parser.add_argument("--brightness-round-trip", action="store_true", help="Explicitly perform six small brightness changes and verify original restoration")
    args = parser.parse_args()
    if not math.isfinite(args.duration_seconds) or args.duration_seconds <= 0 or not math.isfinite(args.interval) or args.interval <= 0:
        parser.error("duration-seconds and interval must be finite positive numbers")
    output = Path(args.output).resolve()
    resume = ResumeEvidence()
    report = {"passed": False, "validation_status": "failed", "physical_backend_verified": False, "failures": [], "pending": []}
    if os.name != "nt":
        report["failures"].append("windows_required")
        atomic_write(output, report)
        return 1
    old_environment = {key: os.environ.get(key) for key in ("JIYAOCHU_FORCE_MOCK", "JIYAOCHU_DATA_DIR")}
    observer = WindowsPowerObserver(resume)
    observer_error = None
    try:
        with tempfile.TemporaryDirectory(prefix="lumadesk-long-run-") as directory:
            os.environ.pop("JIYAOCHU_FORCE_MOCK", None)
            os.environ["JIYAOCHU_DATA_DIR"] = directory
            core = RustCore(args.library)
            bootstrap = core.initialize()
            if bootstrap.get("backend") != "windows":
                report = monitor(bootstrap.get("backend"), [], core.call, progress=lambda value: atomic_write(output, value))
                return 1
            command_names = core.call("system.commands")
            if not isinstance(command_names, list) or not all(isinstance(name, str) for name in command_names):
                raise RuntimeError("invalid_command_list")
            try:
                observer.start()
            except Exception:
                observer_error = "windows_power_notification_registration_failed"
            print("Reading real Windows telemetry in an isolated configuration. No automatic sleep or reboot.", flush=True)
            if args.require_resume:
                print("Manually sleep and wake Windows during this run. Missing suspend/resume events remain pending.", flush=True)
            def progress(value):
                value["power_observer_registered"] = observer_error is None
                value["inherited_mock_flag_cleared"] = old_environment["JIYAOCHU_FORCE_MOCK"] == "1"
                if observer_error:
                    value["power_observer_error"] = observer_error
                atomic_write(output, value)
                print(json.dumps({"status": value["validation_status"], "elapsed_seconds": round(value.get("elapsed_seconds", 0), 1), "command_count": len(value["commands"]), "resume_cycles": resume.summary()["completed_cycles"]}), flush=True)
            report = monitor("windows", command_names, core.call, duration=args.duration_seconds, interval=args.interval,
                             resume=resume, require_resume=args.require_resume, brightness=args.brightness_round_trip, progress=progress)
    except (Exception, KeyboardInterrupt) as error:
        report.update(passed=False, validation_status="failed")
        report["failures"].append("validation_interrupted" if isinstance(error, KeyboardInterrupt) else error_code(error))
    finally:
        try:
            observer.close()
        except Exception:
            report["pending"].append("windows_power_notification_unregistration_failed")
            if report["validation_status"] == "passed":
                report.update(passed=False, validation_status="pending")
        for key, value in old_environment.items():
            if value is None:
                os.environ.pop(key, None)
            else:
                os.environ[key] = value
        report.setdefault("physical_suspend_resume", resume.summary())
        atomic_write(output, report)
    return 0 if report["passed"] else 2 if report["validation_status"] == "pending" else 1


if __name__ == "__main__":
    raise SystemExit(main())
