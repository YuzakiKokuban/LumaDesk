import json,subprocess
from pathlib import Path
from datetime import datetime,timezone
root=Path(__file__).resolve().parents[1];exe=str(root/'target/debug/jiyaochu-ctl.exe')
def call(enable):
 p=subprocess.run([exe,'toggle_oem_service',json.dumps({'enable':enable})],capture_output=True,text=True,encoding='utf-8');return json.loads(p.stdout)
def state():
 p=subprocess.run(['powershell.exe','-NoProfile','-NonInteractive','-Command',"Get-CimInstance Win32_Service -Filter \"Name='GCUBridge'\" | Select-Object State,StartMode | ConvertTo-Json -Compress"],capture_output=True,text=True);return json.loads(p.stdout)
r={'captured_utc':datetime.now(timezone.utc).isoformat()}
try:
 r['restore']=call(False);r['restored_service']=state()
finally:
 r['takeover']=call(True);r['taken_over_service']=state()
 (root/'reverse/native/evidence/oem-roundtrip.json').write_text(json.dumps(r,ensure_ascii=False,indent=2),encoding='utf-8')
