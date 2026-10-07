"""Read-only temperature comparison on project 0x1A; no hardware writes."""
import argparse
import ctypes
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "reverse/native"))
from probe_ec import read_addresses


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--library", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--samples", type=int, default=10)
    args = parser.parse_args()
    if args.samples < 2:
        parser.error("--samples must be at least 2")
    os.environ.pop("JIYAOCHU_FORCE_MOCK", None)
    report = {"captured_utc": datetime.now(timezone.utc).isoformat(),
              "hardware_writes": 0, "samples": [], "errors": []}
    with tempfile.TemporaryDirectory(prefix="lumadesk-temperatures-") as directory:
        os.environ["JIYAOCHU_DATA_DIR"] = directory
        core = ctypes.CDLL(str(args.library.resolve()))
        core.lumadesk_call.argtypes = [ctypes.c_char_p, ctypes.c_char_p]
        core.lumadesk_call.restype = ctypes.c_void_p
        core.lumadesk_free.argtypes = [ctypes.c_void_p]
        for index in range(args.samples):
            ptr = core.lumadesk_call(b"get_page_status", b'{"page":"overview"}')
            try:
                envelope = json.loads(ctypes.string_at(ptr).decode())
            finally:
                core.lumadesk_free(ptr)
            ec = read_addresses([0x740, 0x43e, 0x44f])
            values = {row["address"]: row["byte"] for row in ec["reads"]
                      if row["transport_ok"] and row["returned_bytes"] == 4}
            sample = {"index": index, "ec": values, "api_ok": envelope.get("ok")}
            if values.get("0x740") != 0x1a:
                report["errors"].append(f"sample {index}: EC project not verified")
            data = envelope.get("data", {})
            for sensor, address in (("cpu", "0x43e"), ("gpu", "0x44f")):
                value = data.get(sensor, {}).get("temp")
                sample[sensor] = value
                if value is None or not 1 <= value <= 125:
                    report["errors"].append(f"sample {index}: {sensor} missing/invalid")
                reference = values.get(address)
                if value is not None and reference is not None and 1 <= reference <= 125 and abs(value - reference) > 5:
                    report["errors"].append(f"sample {index}: {sensor} differs from EC by >5 C")
            try:
                result = subprocess.run(["nvidia-smi", "--query-gpu=temperature.gpu", "--format=csv,noheader,nounits"],
                                        check=True, capture_output=True, text=True, timeout=10)
                sample["nvidia_smi"] = float(result.stdout.strip().splitlines()[0])
                if sample["gpu"] is not None and abs(sample["gpu"] - sample["nvidia_smi"]) > 5:
                    report["errors"].append(f"sample {index}: GPU differs from nvidia-smi by >5 C")
            except (OSError, ValueError, subprocess.SubprocessError) as error:
                sample["nvidia_smi_error"] = str(error)
                report["errors"].append(f"sample {index}: independent GPU reference unavailable")
            report["samples"].append(sample)
            if index + 1 < args.samples:
                time.sleep(1)
    report["passed"] = not report["errors"]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"passed": report["passed"], "errors": report["errors"],
                      "cpu_values": [row["cpu"] for row in report["samples"]],
                      "gpu_values": [row["gpu"] for row in report["samples"]]}))
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
