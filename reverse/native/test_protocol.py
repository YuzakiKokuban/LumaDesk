"""Offline protocol checks: no device handles or target processes are opened."""
import json
from pathlib import Path
import unittest

from ec_protocol import (encode_read, encode_write, decode_read, fan_rpm,
                         set_fan_boost_bit, set_fan_isolated_bit, encode_fan_ramp)


class RecoveredProtocolTests(unittest.TestCase):
    def test_wire_examples_match_native_argument_widths(self):
        self.assertEqual(encode_read(0x740), bytes.fromhex("40070000"))
        self.assertEqual(encode_write(0x751, 0x50), bytes.fromhex("5107000050000000"))
        self.assertEqual(decode_read(bytes.fromhex("1a000000")), 0x1A)
        with self.assertRaises(ValueError):
            decode_read(b"\x1a")
        with self.assertRaises(ValueError):
            encode_write(0x10000, 0)
        with self.assertRaises(ValueError):
            encode_write(0x751, 256)

    def test_bit_updates_preserve_all_other_bits(self):
        for current in range(256):
            for enabled in (False, True):
                boosted = set_fan_boost_bit(current, enabled)
                isolated = set_fan_isolated_bit(current, enabled)
                self.assertEqual(boosted & 0xBF, current & 0xBF)
                self.assertEqual(bool(boosted & 0x40), enabled)
                self.assertEqual(isolated & 0x7F, current & 0x7F)
                self.assertEqual(bool(isolated & 0x80), enabled)

    def test_fan_ramp_boundary_examples(self):
        self.assertEqual(encode_fan_ramp(False, 500), 0x81)
        for ms, byte in [(0, 0x81), (99, 0x81), (100, 0x81), (299, 0x82),
                         (300, 0x83), (500, 0x85), (1000, 0x8A), (65535, 0xFF)]:
            self.assertEqual(encode_fan_ramp(True, ms), byte)

    def test_live_capture_decodes_nonzero_rpm(self):
        root = Path(__file__).resolve().parent
        capture = json.loads((root / "evidence" / "ec-read-probe.json").read_text(encoding="utf-8"))
        values = {int(r["address"], 0): r["byte"] for r in capture["reads"]}
        self.assertEqual(fan_rpm(values[0x464], values[0x465]), 1947)
        self.assertEqual(fan_rpm(values[0x46C], values[0x46B]), 1882)

    def test_ioctls_agree_with_managed_constants(self):
        root = Path(__file__).resolve().parent
        constants = json.loads((root / "evidence" / "oem-metadata-constants.json").read_text(encoding="utf-8"))
        expected = {c["name"]: c["value"] for c in constants if c["type"] == "MyECIO.AcpiCtrl"}
        table = json.loads((root / "evidence" / "oem-ioctl-table.json").read_text(encoding="utf-8"))
        ioctls = {r["export"]: int(r["ioctl"], 0) for r in table}
        self.assertEqual(ioctls["ReadEC"], expected["IOCTL_GPD_ACPI_ECREAD"])
        self.assertEqual(ioctls["WriteEC"], expected["IOCTL_GPD_ACPI_ECWRITE"])


if __name__ == "__main__":
    unittest.main()
