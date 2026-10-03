"""Passively verify native WMI watcher startup, idempotency and reconnect."""
import argparse
import ctypes
import json
import os
from pathlib import Path
import tempfile
import time

parser = argparse.ArgumentParser()
parser.add_argument('--library', required=True)
parser.add_argument('--output', required=True)
args = parser.parse_args()
os.environ['JIYAOCHU_FORCE_MOCK'] = '1'
with tempfile.TemporaryDirectory(prefix='lumadesk-wmi-check-') as directory:
    os.environ['JIYAOCHU_DATA_DIR'] = directory
    dll = ctypes.CDLL(str(Path(args.library).resolve()))
    dll.lumadesk_call.argtypes = [ctypes.c_char_p, ctypes.c_char_p]
    dll.lumadesk_call.restype = ctypes.c_void_p
    dll.lumadesk_free.argtypes = [ctypes.c_void_p]
    callback_type = ctypes.CFUNCTYPE(None, ctypes.c_uint32)
    # Only receive events; never send input or change any device state.
    callback = callback_type(lambda value: None)
    def decode(pointer):
        try:
            envelope = json.loads(ctypes.string_at(pointer).decode('utf-8'))
        finally:
            dll.lumadesk_free(pointer)
        assert envelope['ok'], envelope
        return envelope.get('data')
    def call(command):
        return decode(dll.lumadesk_call(command.encode(), None))
    for name in ('lumadesk_start_display_brightness', 'lumadesk_start_oem_hotkeys'):
        method = getattr(dll, name)
        method.argtypes = [callback_type]
        method.restype = ctypes.c_void_p
        decode(method(callback))
    def connected(minimum):
        deadline = time.monotonic() + 12
        while time.monotonic() < deadline:
            status = call('system.subscriptions')
            if all(value['state'] == 'connected' and value['connections'] >= minimum for value in status.values()):
                return status
            time.sleep(0.1)
        raise AssertionError(status)
    initial = connected(1)
    decode(dll.lumadesk_start_display_brightness(callback))
    decode(dll.lumadesk_start_oem_hotkeys(callback))
    stable = call('system.subscriptions')
    assert all(stable[key]['connections'] == initial[key]['connections'] for key in initial), stable
    call('system.reconnect_events')
    restored = connected(2)
    report = {'passed': True, 'passive_native_wmi': True, 'duplicate_start_is_idempotent': True, 'initial': initial, 'after_reconnect': restored}
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(report, ensure_ascii=False))
