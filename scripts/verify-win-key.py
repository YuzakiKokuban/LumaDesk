"""Verify actual keyboard delivery, using a consuming downstream observer.

No keystroke reaches other applications. Uses an isolated config directory and
restores the unlocked state even on failure. Run with the normal Windows HAL.
"""
import argparse, ctypes as c, json, os, tempfile, threading, time
from ctypes import wintypes as w
from pathlib import Path

parser=argparse.ArgumentParser()
parser.add_argument('--library', default='target/debug/jiyaochu_core.dll')
args=parser.parse_args()
u=c.WinDLL('user32',use_last_error=True)
k=c.WinDLL('kernel32',use_last_error=True)
callback_type=c.WINFUNCTYPE(c.c_ssize_t,c.c_int,c.c_size_t,c.c_ssize_t)
class Keyboard(c.Structure):
    _fields_=[('vk',w.DWORD),('scan',w.DWORD),('flags',w.DWORD),('time',w.DWORD),('extra',c.c_size_t)]
u.SetWindowsHookExW.argtypes=[c.c_int,callback_type,c.c_void_p,w.DWORD];u.SetWindowsHookExW.restype=c.c_void_p
u.CallNextHookEx.argtypes=[c.c_void_p,c.c_int,c.c_size_t,c.c_ssize_t];u.CallNextHookEx.restype=c.c_ssize_t
u.GetMessageW.argtypes=[c.POINTER(w.MSG),c.c_void_p,w.UINT,w.UINT]
u.UnhookWindowsHookEx.argtypes=[c.c_void_p]
u.PostThreadMessageW.argtypes=[w.DWORD,w.UINT,c.c_size_t,c.c_ssize_t]
u.keybd_event.argtypes=[w.BYTE,w.BYTE,w.DWORD,c.c_size_t]
k.GetModuleHandleW.argtypes=[w.LPCWSTR];k.GetModuleHandleW.restype=c.c_void_p
seen=[];ready=threading.Event();errors=[];thread_ids=[]
@callback_type
def observe(code,wp,lp):
    if code>=0:
        key=c.cast(lp,c.POINTER(Keyboard)).contents
        if key.flags & 0x10 and key.vk in (0x5b,0x5c,0x87):
            seen.append(key.vk)
            return 1
    return u.CallNextHookEx(None,code,wp,lp)
def observer():
    thread_ids.append(k.GetCurrentThreadId())
    hook=u.SetWindowsHookExW(13,observe,k.GetModuleHandleW(None),0)
    if not hook: errors.append(c.get_last_error())
    ready.set()
    if hook:
        msg=w.MSG()
        while u.GetMessageW(c.byref(msg),None,0,0)>0: pass
        u.UnhookWindowsHookEx(hook)
thread=threading.Thread(target=observer,daemon=True);thread.start();assert ready.wait(5) and not errors,errors
with tempfile.TemporaryDirectory(prefix='lumadesk-win-key-') as directory:
    os.environ['JIYAOCHU_DATA_DIR']=directory
    os.environ.pop('JIYAOCHU_FORCE_MOCK',None)
    lib=c.CDLL(str(Path(args.library).resolve()))
    lib.lumadesk_call.argtypes=[c.c_char_p,c.c_char_p];lib.lumadesk_call.restype=c.c_void_p
    lib.lumadesk_free.argtypes=[c.c_void_p]
    def call(command,args=None):
        raw=lib.lumadesk_call(command.encode(),json.dumps(args or {}).encode())
        try: reply=json.loads(c.string_at(raw).decode())
        finally: lib.lumadesk_free(raw)
        assert reply['ok'],reply
        return reply['data']
    try:
        for locked in (False,True,False):
            call('set_win_key_locked',{'locked':locked})
            seen.clear()
            for vk in (0x5b,0x5c,0x87):
                u.keybd_event(vk,0,0,0);u.keybd_event(vk,0,2,0)
            time.sleep(.25)
            expected=[0x87,0x87] if locked else [0x5b,0x5b,0x5c,0x5c,0x87,0x87]
            assert seen==expected,{'locked':locked,'observed':seen,'expected':expected}
            print(json.dumps({'locked':locked,'observed':seen,'passed':True}))
    finally:
        call('set_win_key_locked',{'locked':False})
        u.PostThreadMessageW(thread_ids[0],0x12,0,0)
        thread.join(3)
print('Both Windows keys are blocked only while locked; unrelated keys pass through')
