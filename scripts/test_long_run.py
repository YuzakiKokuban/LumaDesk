"""Portable checks for the real-device long-run runner; no DLL or hardware used."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


SCRIPT = Path(__file__).with_name("verify-long-run.py")


class RunnerExistsTests(unittest.TestCase):
    def test_real_device_runner_exists(self):
        self.assertTrue(SCRIPT.is_file(), "Real-device long-run validation is not implemented")


def load():
    spec = importlib.util.spec_from_file_location("long_run", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class LongRunTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.runner = load()

    def setUp(self):
        self.now = 0.0

    def sleep_time(self, seconds):
        self.now += seconds

    def clock_time(self):
        return self.now

    def test_metrics_keep_errors_and_stale_readback_separate(self):
        metrics = self.runner.CommandMetrics("get_page_status:overview")
        metrics.record(1, 0.01, {"cpu": {"temp": 48}})
        metrics.record(2, 0.03, error=RuntimeError("secret serial ABC"))
        summary = metrics.summary(10, 5)
        self.assertEqual((summary["attempts"], summary["successes"], summary["failures"]), (2, 1, 1))
        self.assertAlmostEqual(summary["mean_ms"], 20)
        self.assertAlmostEqual(summary["maximum_ms"], 30)
        self.assertTrue(summary["stale"])
        self.assertEqual(summary["capabilities"]["cpu.temp"]["state"], "stale")
        self.assertNotIn("secret", json.dumps(summary))
        metrics.record(11, 0.02, {"cpu": {"temp": 49}})
        self.assertFalse(metrics.summary(11, 5)["stale"])

    def test_failed_latest_read_is_stale_even_if_previous_read_is_recent(self):
        metrics = self.runner.CommandMetrics("status")
        metrics.record(1, 0.01, {"cpu": {"temp": 48}})
        metrics.record(2, 0.01, error=RuntimeError("read failed"))
        self.assertTrue(metrics.summary(2, 5)["stale"])

    def test_unknown_sensor_does_not_become_zero_or_supported(self):
        metrics = self.runner.CommandMetrics("status")
        metrics.record(1, 0.01, {"cpu": {"temp": None, "load": 0}, "fans": {"available": False, "cpu_rpm": 900}, "device": {"serial": "secret"}})
        fields = metrics.summary(1, 5)["capabilities"]
        self.assertEqual(fields["cpu.temp"]["state"], "unknown")
        self.assertEqual(fields["cpu.load"]["value"], 0)
        self.assertEqual(fields["fans.cpu_rpm"]["state"], "unknown")
        self.assertNotIn("secret", json.dumps(fields))

    def test_nested_payload_and_credentials_are_not_copied(self):
        metrics = self.runner.CommandMetrics("status")
        metrics.record(0, 0.1, {"cpu": {"load": float("nan"), "serial": "SECRET"}, "api_key": "SECRET", "device": {"serial": "SECRET"}})
        text = json.dumps(metrics.summary(0, 5), allow_nan=False)
        self.assertNotIn("SECRET", text)
        self.assertNotIn("api_key", text)

    def test_pause_or_resume_without_suspend_never_proves_resume(self):
        evidence = self.runner.ResumeEvidence()
        evidence.record("resume_automatic", 20)
        self.assertEqual(evidence.summary()["completed_cycles"], 0)
        self.assertEqual(evidence.summary()["status"], "pending")
        evidence.record("suspend", 21)
        self.assertEqual(evidence.summary()["status"], "pending")
        evidence.record("resume_suspend", 22)
        evidence.record("resume_automatic", 23)
        self.assertEqual(evidence.summary()["completed_cycles"], 1)
        self.assertEqual(evidence.summary()["status"], "observed")

    def test_sequence_stays_close_and_never_writes_zero(self):
        for original in (1, 2, 50, 99, 100):
            values = self.runner.brightness_sequence(original)
            self.assertTrue(values)
            self.assertTrue(all(1 <= value <= 100 and abs(value - original) <= 2 for value in values))
        for original in (-1, 0, 101, None, True):
            self.assertEqual(self.runner.brightness_sequence(original), [])

    def brightness_device(self, original=50, fail_level=None, fail_restore=False):
        state = {"level": original, "writes": []}
        def call(command, args=None):
            if command == "get_display_brightness":
                return state["level"]
            self.assertEqual(command, "set_display_brightness")
            level = args["level"]
            state["writes"].append(level)
            if level == fail_level or (fail_restore and level == original):
                raise RuntimeError("write failed; confidential")
            state["level"] = level
        return state, call

    def test_brightness_restores_after_failed_write(self):
        state, call = self.brightness_device(fail_level=51)
        result = self.runner.brightness_round_trip(call, sleep=self.sleep_time, clock=self.clock_time)
        self.assertEqual(result["status"], "failed")
        self.assertTrue(result["restored"])
        self.assertEqual(state["writes"], [51, 50])
        self.assertEqual(state["level"], 50)
        self.assertNotIn("confidential", json.dumps(result))

    def test_brightness_restore_failure_cannot_report_passed(self):
        state, call = self.brightness_device(fail_restore=True)
        result = self.runner.brightness_round_trip(call, sleep=self.sleep_time, clock=self.clock_time)
        self.assertEqual(result["status"], "failed")
        self.assertFalse(result["restored"])
        self.assertEqual(state["writes"][-1], 50)

    def test_brightness_restores_on_interruption(self):
        state, delegate = self.brightness_device()
        def call(command, args=None):
            if command == "set_display_brightness" and args["level"] == 51:
                state["level"] = 51
                raise KeyboardInterrupt()
            return delegate(command, args)
        with self.assertRaises(KeyboardInterrupt):
            self.runner.brightness_round_trip(call, sleep=self.sleep_time, clock=self.clock_time)
        self.assertEqual(state["level"], 50)

    def test_initial_zero_or_invalid_brightness_is_read_only_pending(self):
        for value in (0, -1, 101, True, None):
            state, call = self.brightness_device(original=value)
            result = self.runner.brightness_round_trip(call, sleep=self.sleep_time, clock=self.clock_time)
            self.assertEqual(result["status"], "pending")
            self.assertEqual(state["writes"], [])

    def test_actual_restore_readback_is_required(self):
        state, delegate = self.brightness_device()
        def call(command, args=None):
            if command == "set_display_brightness" and args["level"] == 50:
                state["writes"].append(50)
                return
            return delegate(command, args)
        result = self.runner.brightness_round_trip(call, sleep=self.sleep_time, clock=self.clock_time)
        self.assertFalse(result["restored"])
        self.assertEqual(result["status"], "failed")

    def test_brightness_poll_waits_for_stale_then_fresh_readback(self):
        values = iter((50, 50, 51))
        result = self.runner.poll_brightness(lambda *args: next(values), 51,
            clock=self.clock_time, sleep=self.sleep_time)
        self.assertTrue(result["matched"])
        self.assertEqual(result["observed"], [50, 50, 51])
        self.assertGreater(result["elapsed_seconds"], 0.08)
        self.assertLessEqual(result["elapsed_seconds"], 1)

    def test_brightness_poll_timeout_records_numeric_mismatch(self):
        result = self.runner.poll_brightness(lambda *args: 49, 51,
            clock=self.clock_time, sleep=self.sleep_time)
        self.assertFalse(result["matched"])
        self.assertEqual(set(result["observed"]), {49})
        self.assertEqual(result["expected"], 51)
        self.assertAlmostEqual(result["elapsed_seconds"], 1)

    def test_brightness_timeout_fails_but_still_restores_original(self):
        state, delegate = self.brightness_device()
        def call(command, args=None):
            if command == "set_display_brightness" and args["level"] == 51:
                state["writes"].append(51)
                return
            return delegate(command, args)
        result = self.runner.brightness_round_trip(call, sleep=self.sleep_time, clock=self.clock_time)
        self.assertEqual(result["status"], "failed")
        self.assertTrue(result["restored"])
        self.assertIn("brightness_readback_timeout", result["errors"])
        self.assertEqual(set(result["settling_checks"][0]["observed"]), {50})
        self.assertEqual(state["writes"], [51, 50])

    def test_brightness_round_trip_accepts_delayed_device_state_and_restore(self):
        state = {"level": 50, "target": 50, "remaining": 0}
        def call(command, args=None):
            if command == "set_display_brightness":
                state.update(target=args["level"], remaining=3)
            elif state["remaining"]:
                state["remaining"] -= 1
                return state["level"]
            else:
                state["level"] = state["target"]
                return state["level"]
        result = self.runner.brightness_round_trip(call, sleep=self.sleep_time, clock=self.clock_time)
        self.assertEqual(result["status"], "passed")
        self.assertTrue(result["restored"])
        self.assertEqual(state["level"], 50)
        self.assertTrue(all(check["attempts"] == 4 for check in result["settling_checks"]))
        self.assertEqual(result["restore_attempts"][0]["attempts"], 4)

    def test_restore_retries_after_first_restore_readback_timeout(self):
        state, delegate = self.brightness_device()
        restore_count = [0]
        def call(command, args=None):
            if command == "set_display_brightness" and args["level"] == 50:
                restore_count[0] += 1
                if restore_count[0] == 1:
                    return
            return delegate(command, args)
        result = self.runner.brightness_round_trip(call, sleep=self.sleep_time, clock=self.clock_time)
        self.assertEqual(result["status"], "passed")
        self.assertTrue(result["restored"])
        self.assertEqual(restore_count[0], 2)
        self.assertFalse(result["restore_attempts"][0]["matched"])
        self.assertTrue(result["restore_attempts"][1]["matched"])

    def monitor(self, backend="windows", names=None, require_resume=False):
        calls = []
        now = [0.0]
        def call(command, args=None):
            calls.append((command, args))
            now[0] += 0.01
            return 50 if command == "get_display_brightness" else {"cpu": {"load": 0}}
        def sleep(seconds):
            now[0] += seconds
        report = self.runner.monitor(backend, names if names is not None else ["get_page_status", "get_display_brightness"], call,
            duration=6, interval=1, resume=self.runner.ResumeEvidence(), require_resume=require_resume,
            clock=lambda: now[0], sleep=sleep, progress=lambda _: None)
        return calls, report

    def test_mock_or_unknown_backend_rejected_before_any_calls(self):
        for backend in ("mock", "unknown", "windows-mock", None):
            calls, report = self.monitor(backend=backend)
            self.assertEqual(calls, [])
            self.assertFalse(report["passed"])
            self.assertFalse(report["physical_backend_verified"])
            self.assertEqual(report["validation_status"], "failed")

    def test_default_monitor_is_read_only_and_rotates_all_pages(self):
        calls, report = self.monitor()
        self.assertTrue(report["passed"])
        self.assertEqual({args["page"] for command, args in calls if command == "get_page_status"}, set(self.runner.PAGES))
        self.assertTrue(all(command.startswith("get_") for command, _ in calls))

    def test_invalid_brightness_readback_cannot_be_a_success(self):
        now = [0.0]
        def sleep(seconds):
            now[0] += seconds
        for value in (None, True, float("nan"), -1, 101):
            now[0] = 0.0
            result = self.runner.monitor("windows", ["get_page_status", "get_display_brightness"],
                lambda command, *args: value if command == "get_display_brightness" else {},
                duration=6, interval=1, clock=lambda: now[0], sleep=sleep)
            self.assertFalse(result["passed"])
            self.assertGreater(result["commands"]["get_display_brightness"]["failures"], 0)

    def test_missing_page_api_uses_explicit_fallback_and_stays_pending(self):
        calls, report = self.monitor(names=["get_hardware_status", "get_display_brightness"])
        self.assertTrue(any(command == "get_hardware_status" for command, _ in calls))
        self.assertFalse(report["passed"])
        self.assertIn("page_status_command_missing", report["pending"])
        self.assertEqual(report["validation_status"], "pending")

    def test_required_physical_resume_cannot_be_inferred_from_elapsed_duration(self):
        calls, report = self.monitor(require_resume=True)
        self.assertTrue(calls)
        self.assertFalse(report["passed"])
        self.assertIn("physical_suspend_resume_not_observed", report["pending"])

    def test_observed_resume_without_new_readbacks_still_stays_pending(self):
        now = [0.0]
        evidence = self.runner.ResumeEvidence()
        def sleep(seconds):
            evidence.record("suspend", 10)
            evidence.record("resume_automatic", 20)
            now[0] += 50
        result = self.runner.monitor("windows", ["get_page_status"], lambda *args: {}, duration=10,
            interval=1, resume=evidence, require_resume=True, clock=lambda: now[0], sleep=sleep)
        self.assertEqual(result["physical_suspend_resume"]["completed_cycles"], 1)
        self.assertFalse(result["passed"])
        self.assertIn("post_resume_readback_pending", result["pending"])

    def test_post_resume_success_requires_all_page_readbacks(self):
        now = [0.0]
        evidence = self.runner.ResumeEvidence()
        evidence.record("suspend", 10)
        evidence.record("resume_automatic", 20)
        def sleep(seconds):
            now[0] += seconds
        result = self.runner.monitor("windows", ["get_page_status", "get_display_brightness"],
            lambda command, *args: 50 if command == "get_display_brightness" else {"cpu": {"load": 0}},
            duration=6, interval=1, resume=evidence, require_resume=True, clock=lambda: now[0], sleep=sleep)
        self.assertTrue(result["passed"])
        self.assertEqual(result["physical_suspend_resume"]["readback_status"], "passed")

    def test_unsupported_fields_are_pending_without_exposing_error_text(self):
        now = [0.0]
        def sleep(seconds):
            now[0] += seconds
        result = self.runner.monitor("windows", ["get_page_status", "get_display_brightness"],
            lambda command, *args: 50 if command == "get_display_brightness" else {"power_mode": None, "power_mode_error": "SERIAL 123 unsupported EC"},
            duration=6, interval=1, clock=lambda: now[0], sleep=sleep)
        self.assertFalse(result["passed"])
        self.assertEqual(result["validation_status"], "pending")
        self.assertNotIn("SERIAL", json.dumps(result))

    def test_atomic_output_is_valid_and_contains_no_temporary_leftover(self):
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "report.json"
            self.runner.atomic_write(target, {"passed": False})
            self.runner.atomic_write(target, {"passed": True})
            self.assertEqual(json.loads(target.read_text()), {"passed": True})
            self.assertEqual(list(Path(directory).iterdir()), [target])


if __name__ == "__main__":
    unittest.main()
