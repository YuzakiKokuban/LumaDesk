"""Recovered OpenRevo MUX encodings and offline buffer transformation.

This module has no operating system or hardware access.
"""
GUID = "{9F33F85C-13CA-4FD1-9C4A-96217722C593}"
DISPLAY_MODE_OFFSET = 0x62
AP_VERSION_OFFSET = 0x43
ENCODINGS = {
    "intel": {"igpu": 1, "dgpu": 2, "hybrid": 4},
    "amd": {"hybrid": 0, "dgpu": 1, "igpu": 2},
}


def decode_mode(platform, value):
    return next((name for name, code in ENCODINGS[platform.lower()].items() if code == value), "unknown")


def encode_mode(platform, mode):
    return ENCODINGS[platform.lower()][mode.lower()]


def patch_buffer(buffer, platform, mode):
    """Return a changed copy for examination; never commit it to firmware."""
    if len(buffer) <= DISPLAY_MODE_OFFSET:
        raise ValueError("NVRAM buffer is too short for display mode")
    result = bytearray(buffer)
    result[DISPLAY_MODE_OFFSET] = encode_mode(platform, mode)
    return bytes(result)


def needs_door(variable_name, ap_version):
    return variable_name == "OemMagicVariable" or ap_version >= 25
