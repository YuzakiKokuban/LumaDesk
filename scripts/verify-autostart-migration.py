"""Exercise the actual migration script without changing Task Scheduler."""
import copy
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile

root = Path(__file__).resolve().parents[1]
source = (root / "src/core/autostart.rs").read_text(encoding="utf-8")
script = re.search(r'let script = r#"(.*?)"#;', source, re.S).group(1)
shell = shutil.which("powershell") or shutil.which("pwsh")
assert shell, "PowerShell is required"
sid = subprocess.check_output([
    shell, "-NoProfile", "-NonInteractive", "-Command",
    "[Security.Principal.WindowsIdentity]::GetCurrent().User.Value",
], text=True).strip()
prelude = r"""
$fixture=Get-Content -LiteralPath $env:LUMADESK_MIGRATION_FIXTURE -Raw | ConvertFrom-Json
$script:MockTask=$fixture.task
function Get-ScheduledTask { $script:MockTask }
function Set-ScheduledTask {
 param($InputObject)
 $InputObject | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $env:LUMADESK_MIGRATION_OUTPUT -Encoding UTF8
 $InputObject
}
"""
with tempfile.TemporaryDirectory(prefix="lumadesk-task-migration-") as temp:
    folder = Path(temp)
    executable = folder / "LumaDesk.exe"
    executable.touch()
    task = {
        "TaskName": "LumaDesk", "TaskPath": "\\",
        "Actions": [{"Execute": str(executable), "Arguments": "", "WorkingDirectory": str(folder),
                     "Id": "original-action", "CimClass": {"CimClassName": "MSFT_TaskExecAction"}}],
        "Principal": {"UserId": sid, "RunLevel": "Highest", "LogonType": "Interactive"},
        "Settings": {"Enabled": True, "ExecutionTimeLimit": "PT0S", "StartWhenAvailable": False},
        "Triggers": [{"Enabled": True, "UserId": sid, "Delay": "PT5S"}],
    }
    cases = [("old-owned", task, True), ("missing", None, False)]
    for name, change, migrate in [
        ("renamed-owned", lambda t: t["Actions"][0].update(Execute=str(folder / "机耀处.exe")), True),
        ("renamed-background", lambda t: t["Actions"][0].update(Execute=str(folder / "机耀处.exe"), Arguments="--background"), True),
        ("renamed-disabled", lambda t: (t["Actions"][0].update(Execute=str(folder / "机耀处.exe")), t["Settings"].update(Enabled=False)), True),
        ("renamed-custom", lambda t: t["Actions"][0].update(Execute=str(folder / "机耀处.exe"), Arguments="--custom"), False),
        ("renamed-foreign-directory", lambda t: t["Actions"][0].update(Execute=str(folder / "other" / "机耀处.exe")), False),
        ("disabled-owned", lambda t: t["Settings"].update(Enabled=False), True),
        ("case-normalized", lambda t: t["Actions"][0].update(Execute=str(executable).upper()), True),
        ("already-background", lambda t: t["Actions"][0].update(Arguments="--background"), False),
        ("custom-arguments", lambda t: t["Actions"][0].update(Arguments="--layout-check"), False),
        ("foreign-path", lambda t: t["Actions"][0].update(Execute=str(folder / "other.exe")), False),
        ("relative-path", lambda t: t["Actions"][0].update(Execute="LumaDesk.exe"), False),
        ("drive-relative-path", lambda t: t["Actions"][0].update(Execute=executable.drive + "LumaDesk.exe"), False),
        ("foreign-principal", lambda t: t["Principal"].update(UserId="S-1-5-18"), False),
        ("multiple-actions", lambda t: t["Actions"].append(copy.deepcopy(t["Actions"][0])), False),
        ("different-action", lambda t: t["Actions"][0]["CimClass"].update(CimClassName="MSFT_TaskComHandlerAction"), False),
        ("wrong-runlevel", lambda t: t["Principal"].update(RunLevel="Limited"), False),
        ("wrong-logon", lambda t: t["Principal"].update(LogonType="Password"), False),
    ]:
        candidate = copy.deepcopy(task)
        change(candidate)
        cases.append((name, candidate, migrate))
    for name, candidate, migrate in cases:
        fixture = folder / f"{name}.json"
        output = folder / f"{name}-result.json"
        fixture.write_text(json.dumps({"task": candidate}, ensure_ascii=False), encoding="utf-8-sig")
        environment = os.environ.copy()
        environment.update(LUMADESK_STARTUP_ACTION="migrate", LUMADESK_STARTUP_EXE=str(executable),
                           LUMADESK_MIGRATION_FIXTURE=str(fixture), LUMADESK_MIGRATION_OUTPUT=str(output))
        result = subprocess.run([shell, "-NoProfile", "-NonInteractive", "-Command", prelude + script],
                                env=environment, capture_output=True, text=True)
        assert result.returncode == 0, (name, result.stderr)
        assert output.exists() == migrate, (name, "unexpected modification")
        if migrate:
            expected = copy.deepcopy(candidate)
            expected["Actions"][0]["Execute"] = str(executable)
            expected["Actions"][0]["Arguments"] = "--background"
            actual = json.loads(output.read_text(encoding="utf-8-sig"))
            assert actual == expected, (name, "migration changed other task metadata")
        print(json.dumps({"case": name, "migrated": migrate, "passed": True}))
print("Owned task migration and metadata preservation verified; no registered task was changed")
