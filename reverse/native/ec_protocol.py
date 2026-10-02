"""Pure encoders recovered from OpenRevo 0.8.5 and the sampled UniWill driver.

These helpers construct bytes only. Firmware writes are not performed here.
"""
import struct

IOCTL_EC_READ = 0x9C40A488
IOCTL_EC_WRITE = 0x9C40A48C


def encode_read(address):
    if not 0 <= address <= 0xFFFF:
        raise ValueError("address must fit the original u16 signature")
    return struct.pack("<I", address)


def encode_write(address, value):
    if not 0 <= address <= 0xFFFF or not 0 <= value <= 0xFF:
        raise ValueError("address/value must fit the original u16/u8 signature")
    return struct.pack("<II", address, value)


def decode_read(payload):
    if len(payload) != 4:
        raise ValueError("the sampled read returns a four-byte integer")
    return struct.unpack("<I", payload)[0] & 0xFF


def fan_rpm(high, low):
    """OpenRevo polling function 0x1400DE180 combines MSB then LSB."""
    if not 0 <= high <= 255 or not 0 <= low <= 255:
        raise ValueError("fan bytes must be u8")
    return (high << 8) | low


def set_fan_boost_bit(current, enabled):
    """Bit-preserving branch of set_fan_boost_hardware at 0x14030D538."""
    if not 0 <= current <= 255:
        raise ValueError("current must be u8")
    return (current & 0xBF) | (0x40 if enabled else 0)


def set_fan_isolated_bit(current, enabled):
    """set_fan_isolated_output 0x14030E330 updates bit 7 of EC 0x7C5."""
    if not 0 <= current <= 255:
        raise ValueError("current must be u8")
    return (current & 0x7F) | (0x80 if enabled else 0)


def encode_fan_ramp(enabled, speed_ms):
    """set_fan_ramp_rate 0x14030BAF0: disabled=0x81, otherwise 100-ms units."""
    if not 0 <= speed_ms <= 0xFFFF:
        raise ValueError("speed_ms must be u16")
    return 0x81 if not enabled else 0x80 | (min(max(speed_ms, 100), 12700) // 100)
