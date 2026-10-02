import json
import unittest
from pathlib import Path
from mux_protocol import decode_mode, encode_mode, needs_door, patch_buffer


class MuxProtocolTests(unittest.TestCase):
    def test_known_encodings_match_official_metadata(self):
        fields = json.loads((Path(__file__).parent/"evidence"/"oem-metadata-constants.json").read_text(encoding="utf-8"))
        fields = [f for f in fields if "DGgpuDirectConnectionMode" in (f["type"] or "")]
        values = {f["name"]: f["value"] for f in fields}
        for platform, mode, field in [
            ("intel", "igpu", "Intel_iGPU_only"), ("intel", "dgpu", "Intel_dGPU_only"),
            ("intel", "hybrid", "Intel_Dynamic"), ("amd", "igpu", "AMD_iGPU_only"),
            ("amd", "dgpu", "AMD_dGPU_only"), ("amd", "hybrid", "AMD_MSHybrid")]:
            self.assertEqual(encode_mode(platform, mode), values[field])
            self.assertEqual(decode_mode(platform, values[field]), mode)

    def test_buffer_patch_preserves_other_bytes(self):
        original = bytes(range(256))*2
        for platform in ("intel", "amd"):
            for mode in ("igpu", "dgpu", "hybrid"):
                result = patch_buffer(original, platform, mode)
                self.assertEqual(result[:0x62], original[:0x62])
                self.assertEqual(result[0x63:], original[0x63:])
                self.assertEqual(result[0x62], encode_mode(platform, mode))
                self.assertEqual(len(result), len(original))
        self.assertEqual(original, bytes(range(256))*2)

    def test_short_buffer_and_unknown_values(self):
        with self.assertRaises(ValueError):
            patch_buffer(bytes(0x62), "intel", "dgpu")
        self.assertEqual(len(patch_buffer(bytes(0x63), "intel", "dgpu")), 0x63)
        for platform in ("intel", "amd"):
            self.assertEqual(decode_mode(platform, 255), "unknown")
        with self.assertRaises(KeyError):
            encode_mode("intel", "invalid")

    def test_door_selection_boundaries(self):
        self.assertTrue(needs_door("OemMagicVariable", 0))
        self.assertFalse(needs_door("UniWillVariable", 24))
        self.assertTrue(needs_door("UniWillVariable", 25))


if __name__ == "__main__":
    unittest.main()
