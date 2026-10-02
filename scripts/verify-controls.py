"""Elevated reversible EC profile/RGB and Windows power-mode validation.
Preserves all tested EC bytes and the Windows mode in finally; MUX is read-only.
OEM takeover is intentionally retained in the normal app data directory.
"""
import ctypes,json,os,subprocess,time
from ctypes import wintypes
from pathlib import Path
from datetime import datetime,timezone
ROOT=Path(__file__).resolve().parents[1]
output=ROOT/'reverse/native/evidence/control-validation.json'
r={'captured_utc':datetime.now(timezone.utc).isoformat()}
try:
 p=subprocess.run([str(ROOT/'target/debug/jiyaochu-ctl.exe'),'toggle_oem_service','{"enable":true}'],capture_output=True,text=True,encoding='utf-8')
 r['oem_takeover']={'exit_code':p.returncode,'reply':p.stdout.strip(),'error':p.stderr.strip()}
 if p.returncode: raise RuntimeError('OEM takeover failed')
 os.environ['JIYAOCHU_DATA_DIR']=str(ROOT/'artifacts/control-test-config')
 core=ctypes.CDLL(str(ROOT/'target/debug/jiyaochu_core.dll'))
 core.lumadesk_call.argtypes=[ctypes.c_char_p,ctypes.c_char_p];core.lumadesk_call.restype=ctypes.c_void_p;core.lumadesk_free.argtypes=[ctypes.c_void_p]
 def call(name,args=None):
  ptr=core.lumadesk_call(name.encode(),None if args is None else json.dumps(args).encode())
  try:
   reply=json.loads(ctypes.string_at(ptr).decode())
   if not reply.get('ok'): raise RuntimeError(str(reply))
   return reply['data']
  finally: core.lumadesk_free(ptr)
 k=ctypes.WinDLL('kernel32',use_last_error=True)
 k.CreateFileW.argtypes=[wintypes.LPCWSTR,wintypes.DWORD,wintypes.DWORD,ctypes.c_void_p,wintypes.DWORD,wintypes.DWORD,wintypes.HANDLE];k.CreateFileW.restype=wintypes.HANDLE
 k.DeviceIoControl.argtypes=[wintypes.HANDLE,wintypes.DWORD,ctypes.c_void_p,wintypes.DWORD,ctypes.c_void_p,wintypes.DWORD,ctypes.POINTER(wintypes.DWORD),ctypes.c_void_p]
 k.CloseHandle.argtypes=[wintypes.HANDLE]
 handle=k.CreateFileW(r'\\.\ACPIDriver',0xc0000000,3,None,3,0,None)
 if handle==ctypes.c_void_p(-1).value: raise ctypes.WinError(ctypes.get_last_error())
 def io(code,values):
  data=(wintypes.DWORD*len(values))(*values);out=wintypes.DWORD();returned=wintypes.DWORD()
  if not k.DeviceIoControl(handle,code,data,ctypes.sizeof(data),ctypes.byref(out),4,ctypes.byref(returned),None): raise ctypes.WinError(ctypes.get_last_error())
  return out.value & 255
 def read(a): return io(0x9c40a488,[a])
 def write(a,v): return io(0x9c40a48c,[a,v])
 registers=[0x751,0x7ab,0x783,0x784,0x785,0x45b,0x726,0x727,0x767,0x769,0x76a,0x76b,0x78c]
 before={a:read(a) for a in registers};windows_mode=None
 r['before']={hex(a):v for a,v in before.items()}
 try:
  settings=call('get_power_settings');windows_mode=settings['windows_power_mode'];r['power_schemes']=settings['schemes'];r['oem_modes']=[]
  for mode in [0,1,2]:
   call('set_power_mode',{'mode':mode});time.sleep(.15)
   actual=call('get_power_settings')['power_mode'];entry={'requested':mode,'actual':actual,'registers':{hex(a):read(a) for a in registers[:6]}}
   r['oem_modes'].append(entry)
   if actual!=mode: raise RuntimeError('OEM mode readback mismatch')
  r['windows_modes']=[]
  for mode in [0,1,2]:
   call('set_windows_power_mode',{'mode':mode});actual=call('get_power_settings')['windows_power_mode'];r['windows_modes'].append({'requested':mode,'actual':actual})
   if actual!=mode: raise RuntimeError('Windows mode readback mismatch')
  state=call('get_lighting_state');r['rgb']=[]
  for color,level in [('#ff0000',1),('#00ff00',2),('#0000ff',3),('#ffffff',4),('#ffffff',0)]:
   state.update(enabled=level>0,kb_engine='hardware',kb_effect=0,kb_color=color,kb_brightness=level)
   call('apply_keyboard_lighting',{'lighting':state});time.sleep(.15)
   rgb=[read(a) for a in [0x769,0x76a,0x76b]];brightness=read(0x7ab)>>5
   r['rgb'].append({'color':color,'requested_brightness':level,'actual_brightness':brightness,'rgb_raw':rgb})
   if brightness!=level: raise RuntimeError('RGB brightness readback mismatch')
   expected=[int(color[1+i*2:3+i*2],16)*50//255 if level else 0 for i in range(3)]
   if level and rgb!=expected: raise RuntimeError('RGB color readback mismatch')
  r['passed']=True
 finally:
  r['restore_errors']=[]
  if windows_mode is not None:
   try: call('set_windows_power_mode',{'mode':windows_mode})
   except Exception as e:r['restore_errors'].append(str(e))
  for a,v in before.items():
   if a==0x767:continue
   try:write(a,v)
   except Exception as e:r['restore_errors'].append(str(e))
  try:
   write(0x767,before[0x767]|0x20);time.sleep(.2)
   r['restored']={hex(a):read(a) for a in registers}
   r['ec_restored']=all(read(a)==v for a,v in before.items() if a!=0x767)
  finally:k.CloseHandle(handle)
except Exception as e:r['error']=str(e)
finally:output.write_text(json.dumps(r,ensure_ascii=False,indent=2),encoding='utf-8')
