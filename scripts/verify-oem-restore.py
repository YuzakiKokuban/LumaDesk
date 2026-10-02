"""Verify application OEM restore and retakeover against the saved service state."""
import argparse
import ctypes
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--application-directory', default=str(ROOT / 'artifacts/LumaDesk-0.1.2-win-x64'))
    parser.add_argument('--output', default=str(ROOT / 'reverse/native/evidence/oem-roundtrip.json'))
    args = parser.parse_args()
    report = {'captured_utc': datetime.now(timezone.utc).isoformat(), 'passed': False}
    directory = Path(args.application_directory).resolve()
    cli = directory / 'jiyaochu-ctl.exe'
    output = Path(args.output).resolve()

    def call(enable):
        result = subprocess.run([str(cli), 'toggle_oem_service', json.dumps({'enable': enable})],
                                capture_output=True, encoding='utf-8', timeout=60)
        reply = json.loads(result.stdout)
        if result.returncode or not reply.get('ok'):
            raise RuntimeError(f'OEM operation failed: {reply} {result.stderr}')
        return reply

    def state():
        script = 'Get-CimInstance Win32_Service -Filter "Name=\'GCUBridge\'" | Select-Object State,StartMode | ConvertTo-Json -Compress'
        result = subprocess.run(['powershell.exe', '-NoProfile', '-NonInteractive', '-Command', script],
                                capture_output=True, encoding='utf-8', check=True, timeout=20)
        return json.loads(result.stdout)

    if not ctypes.windll.shell32.IsUserAnAdmin():
        raise RuntimeError('Run as administrator')
    try:
        backup_path = Path(os.environ['APPDATA']) / 'JiYaoChu/oem_takeover.json'
        backup = json.loads(backup_path.read_text(encoding='utf-8-sig'))
        report['expected'] = {'StartMode': {2: 'Auto', 3: 'Manual', 4: 'Disabled'}[backup['start']],
                              'State': 'Running' if backup['running'] else 'Stopped'}
        report['official_task_count'] = len(backup.get('tasks', []))
        restored = subprocess.run([str(directory / '机耀处.exe'), '--restore-oem'],
                                  capture_output=True, timeout=60)
        report['restore_exit_code'] = restored.returncode
        if restored.returncode:
            raise RuntimeError(f'Application restore exited {restored.returncode}')
        report['restored_service'] = state()
        assert report['restored_service'] == report['expected'], report
        report['passed'] = True
    except Exception as error:
        report['error'] = str(error)
    finally:
        try:
            report['takeover'] = call(True)
            report['taken_over_service'] = state()
            assert report['taken_over_service'] == {'State': 'Stopped', 'StartMode': 'Disabled'}, report
        except Exception as error:
            report['retakeover_error'] = str(error)
            report['passed'] = False
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    return 0 if report['passed'] else 1

if __name__ == '__main__':
    sys.exit(main())
