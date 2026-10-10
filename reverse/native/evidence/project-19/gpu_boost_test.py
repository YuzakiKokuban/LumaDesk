"""Reversible Dynamic Boost write check for project 0x19 (Yilong 15 Pro / GM5HG0A).

Run with the OEM control center on the balanced profile:

    python reverse/native/evidence/project-19/gpu_boost_test.py

The OEM turbo profile differs from balanced in 0x751 plus three bytes the OEM
metadata names ConfigurableTGP_DynamicBoost_CTRL_BYTE (0x743),
DynamicBoost_TotalProcessingPowerTarget_VALUE (0x745) and
DynamicBoost_MaxinumTGP_VALUE (0x746). This script writes exactly the values
observed under the OEM turbo profile, records whether 0x7C4/0x7D4/0x7D5 follow
as they do under the OEM software, and restores every starting value.
"""
import json
import subprocess
import time

from write_test import HERE, SETTLE_SECONDS, Ec

TURBO = [(0x746, 0x19), (0x745, 0xFF), (0x743, 0x07)]  # values first, control byte last
RESTORE_ORDER = [0x743, 0x745, 0x746]
OEM_TURBO = {"0x743": 0x07, "0x745": 0xFF, "0x746": 0x19, "0x7c4": 0x38, "0x7d4": 0xFF, "0x7d5": 0x19}


def nvidia():
    try:
        return subprocess.run(
            ["nvidia-smi", "--query-gpu=power.draw,power.limit,power.default_limit,power.max_limit",
             "--format=csv,noheader"], capture_output=True, text=True, timeout=15).stdout.strip()
    except (OSError, subprocess.SubprocessError) as error:
        return f"unavailable: {error}"


def sample(ec):
    row = ec.watch()
    row["nvidia_smi"] = nvidia()
    return row


def show(label, row):
    print(f"  {label:<9} 751={row['0x751']:#04x} 743={row['0x743']:#04x} 745={row['0x745']:#04x}"
          f" 746={row['0x746']:#04x} 7c4={row['0x7c4']:#04x} 7d4={row['0x7d4']:#04x}"
          f" 7d5={row['0x7d5']:#04x} nvidia={row['nvidia_smi']}")


def main():
    report = {"captured_at": time.strftime("%Y-%m-%dT%H:%M:%S%z")}
    ec = Ec()
    try:
        project = ec.read(0x740)
        if project != 0x19:
            raise SystemExit(f"EC project is {project:#04x}, not 0x19; nothing was written")
        if ec.read(0x726) & 0x80:
            raise SystemExit("The OEM custom profile is active; pick the balanced profile first")
        start = {address: ec.read(address) for address in (0x751, *RESTORE_ORDER)}
        report["start"] = sample(ec)
        show("start", report["start"])
        try:
            ec.write_field(0x751, 0xB0, 0x10)
            for address, value in TURBO:
                ec.write_field(address, 0xFF, value)
            time.sleep(SETTLE_SECONDS)
            report["turbo"] = sample(ec)
            show("turbo", report["turbo"])
            report["matches_oem_turbo"] = {
                key: report["turbo"][key] == value for key, value in OEM_TURBO.items()}
            print("  matches OEM turbo:", report["matches_oem_turbo"])
        finally:
            for address in RESTORE_ORDER:
                ec.write_field(address, 0xFF, start[address])
            ec.write_field(0x751, 0xB0, start[0x751] & 0xB0)
        time.sleep(SETTLE_SECONDS)
        report["restored"] = sample(ec)
        show("restored", report["restored"])
    finally:
        ec.close()
        output = HERE / "gpu-boost-test.json"
        output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"saved {output}")


if __name__ == "__main__":
    main()
