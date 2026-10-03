"""Record only special-key/raw consumer notifications; never ordinary typed text."""
import argparse
import ctypes
from ctypes import wintypes as w
from datetime import datetime, timezone
import json
from pathlib import Path
import time


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--seconds', type=int, default=90)
    parser.add_argument('--output', type=Path, default=Path('artifacts/special-input.jsonl'))
    args = parser.parse_args()
    if not 10 <= args.seconds <= 300:
        parser.error('seconds must be between 10 and 300')
    user = ctypes.WinDLL('user32', use_last_error=True)
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    proc_type = ctypes.WINFUNCTYPE(ctypes.c_ssize_t, w.HWND, w.UINT, w.WPARAM, w.LPARAM)
    hook_type = ctypes.WINFUNCTYPE(ctypes.c_ssize_t, ctypes.c_int, w.WPARAM, w.LPARAM)

    class WindowClass(ctypes.Structure):
        _fields_ = [('style', w.UINT), ('proc', proc_type), ('class_extra', ctypes.c_int),
                    ('window_extra', ctypes.c_int), ('instance', w.HINSTANCE), ('icon', w.HICON),
                    ('cursor', w.HANDLE), ('brush', w.HBRUSH), ('menu', w.LPCWSTR), ('name', w.LPCWSTR)]

    class RawDevice(ctypes.Structure):
        _fields_ = [('page', w.USHORT), ('usage', w.USHORT), ('flags', w.DWORD), ('target', w.HWND)]

    class RawHeader(ctypes.Structure):
        _fields_ = [('kind', w.DWORD), ('size', w.DWORD), ('device', w.HANDLE), ('param', w.WPARAM)]

    user.DefWindowProcW.argtypes = [w.HWND, w.UINT, w.WPARAM, w.LPARAM]
    user.DefWindowProcW.restype = ctypes.c_ssize_t
    user.CreateWindowExW.argtypes = [w.DWORD, w.LPCWSTR, w.LPCWSTR, w.DWORD, ctypes.c_int,
                                    ctypes.c_int, ctypes.c_int, ctypes.c_int, w.HWND, w.HMENU, w.HINSTANCE, w.LPVOID]
    user.CreateWindowExW.restype = w.HWND
    user.GetRawInputData.argtypes = [w.HANDLE, w.UINT, w.LPVOID, ctypes.POINTER(w.UINT), w.UINT]
    user.GetRawInputData.restype = w.UINT
    user.RegisterRawInputDevices.argtypes = [ctypes.POINTER(RawDevice), w.UINT, w.UINT]
    user.RegisterRawInputDevices.restype = w.BOOL
    user.PeekMessageW.argtypes = [ctypes.POINTER(w.MSG), w.HWND, w.UINT, w.UINT, w.UINT]
    user.DestroyWindow.argtypes = [w.HWND]
    kernel.GetModuleHandleW.argtypes = [w.LPCWSTR]
    kernel.GetModuleHandleW.restype = w.HMODULE
    user.SetWindowsHookExW.argtypes = [ctypes.c_int, hook_type, w.HINSTANCE, w.DWORD]
    user.SetWindowsHookExW.restype = w.HANDLE
    user.CallNextHookEx.argtypes = [w.HANDLE, ctypes.c_int, w.WPARAM, w.LPARAM]
    user.CallNextHookEx.restype = ctypes.c_ssize_t
    user.UnhookWindowsHookEx.argtypes = [w.HANDLE]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open('w', encoding='utf-8') as output:
        def keyboard_hook(code, wp, lp):
            if code >= 0:
                values = ctypes.cast(lp, ctypes.POINTER(w.DWORD))
                key, scan, flags = values[0], values[1], values[2]
                if key == 0xff or 0x70 <= key <= 0x87 or scan in (0x76, 0x78):
                    line = json.dumps({'utc': datetime.now(timezone.utc).isoformat(), 'kind': 'low_level',
                                       'key': hex(key), 'scan': hex(scan), 'flags': flags, 'message': int(wp)})
                    output.write(line + '\n')
                    output.flush()
                    print(line, flush=True)
            return user.CallNextHookEx(None, code, wp, lp)

        hook_ref = hook_type(keyboard_hook)
        hook = user.SetWindowsHookExW(13, hook_ref, kernel.GetModuleHandleW(None), 0)
        if not hook:
            raise ctypes.WinError(ctypes.get_last_error())
        def callback(hwnd, message, wp, lp):
            if message == 0xff:
                size = w.UINT()
                user.GetRawInputData(lp, 0x10000003, None, ctypes.byref(size), ctypes.sizeof(RawHeader))
                if size.value <= 4096:
                    data = ctypes.create_string_buffer(size.value)
                    if user.GetRawInputData(lp, 0x10000003, data, ctypes.byref(size), ctypes.sizeof(RawHeader)) == size.value:
                        header = RawHeader.from_buffer_copy(data)
                        payload = data.raw[ctypes.sizeof(RawHeader):]
                        record = {'utc': datetime.now(timezone.utc).isoformat(), 'kind': header.kind}
                        if header.kind == 1 and len(payload) >= 16:
                            key = int.from_bytes(payload[6:8], 'little')
                            # Exclude letters, digits, whitespace, punctuation
                            # and Unicode packet input; retain unknown system keys.
                            text_keys = set(range(0x30, 0x5b)) | set(range(0x60, 0x6a)) | set(range(0xba, 0xe3)) | {0x20, 0xe7}
                            if key not in text_keys:
                                record.update(key=hex(key), scan=hex(int.from_bytes(payload[:2], 'little')),
                                              flags=int.from_bytes(payload[2:4], 'little'))
                            else:
                                record = None
                        elif header.kind == 2:
                            record['consumer_report'] = payload.hex()
                        else:
                            record = None
                        if record:
                            line = json.dumps(record)
                            output.write(line + '\n')
                            output.flush()
                            print(line, flush=True)
            return user.DefWindowProcW(hwnd, message, wp, lp)

        callback_ref = proc_type(callback)
        instance = kernel.GetModuleHandleW(None)
        name = f'LumaDeskSpecialInput{time.time_ns()}'
        cls = WindowClass(0, callback_ref, 0, 0, instance, None, None, None, None, name)
        if not user.RegisterClassW(ctypes.byref(cls)):
            raise ctypes.WinError(ctypes.get_last_error())
        hwnd = user.CreateWindowExW(0, name, '', 0, 0, 0, 0, 0, None, None, instance, None)
        if not hwnd:
            raise ctypes.WinError(ctypes.get_last_error())
        devices = (RawDevice * 4)(RawDevice(1, 6, 0x100, hwnd), RawDevice(0xc, 1, 0x100, hwnd), RawDevice(1, 0x80, 0x100, hwnd), RawDevice(1, 0x0c, 0x100, hwnd))
        try:
            if not user.RegisterRawInputDevices(devices, 4, ctypes.sizeof(RawDevice)):
                raise ctypes.WinError(ctypes.get_last_error())
            print(f'READY: special input capture for {args.seconds}s; ordinary text keys excluded', flush=True)
            deadline = time.monotonic() + args.seconds
            message = w.MSG()
            while time.monotonic() < deadline:
                while user.PeekMessageW(ctypes.byref(message), None, 0, 0, 1):
                    user.TranslateMessage(ctypes.byref(message))
                    user.DispatchMessageW(ctypes.byref(message))
                time.sleep(0.01)
        finally:
            user.UnhookWindowsHookEx(hook)
            user.DestroyWindow(hwnd)
    print(f'Saved {args.output}', flush=True)


if __name__ == '__main__':
    main()
