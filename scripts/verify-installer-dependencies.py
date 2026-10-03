"""Execute the installer's actual runtime detection script against isolated cases.

Only the Appx lookup is substituted; no installed dependency is removed.
"""
import json, os, re, shutil, subprocess, tempfile
from pathlib import Path
root=Path(__file__).resolve().parents[1]
source=(root/'installer/LumaDesk.iss').read_text(encoding='utf-8-sig')
assert re.search(r'(?m)^PrivilegesRequired=admin$',source)
assert re.search(r'(?m)^PrivilegesRequiredOverridesAllowed=$',source)
expression=source.split("Script := '$ErrorActionPreference",1)[1].split('if not Exec',1)[0]
expression="'$ErrorActionPreference"+expression
parts=re.findall(r"'(?:[^']|'')*'|PSQuote\((\w+)\)",expression)
# finditer preserves literal text as well as the marker identifier.
tokens=list(re.finditer(r"'(?:[^']|'')*'|PSQuote\((\w+)\)",expression))
shell=shutil.which('pwsh') or shutil.which('powershell')
assert shell,'PowerShell is required'
with tempfile.TemporaryDirectory(prefix='lumadesk-runtime-check-') as temp:
    folder=Path(temp)
    for name,net,ui,expected in [
        ('ready',True,{'Architecture':'X64','Version':'2.5.1.0'},(True,True)),
        ('missing-net',False,{'Architecture':'X64','Version':'2.5.1.0'},(False,True)),
        ('missing-winui',True,None,(True,False)),
        ('old-winui',True,{'Architecture':'X64','Version':'2.4.9.0'},(True,False)),
        ('wrong-architecture',True,{'Architecture':'X86','Version':'2.5.1.0'},(True,False)),
        ('both-missing',False,None,(False,False)),
    ]:
        marker=folder/name
        markers={'Marker':str(marker),'NetMarker':str(marker)+'-net','WinUIMarker':str(marker)+'-winui'}
        script=''.join(markers[t.group(1)].replace("'","''") if t.group(1) else t.group()[1:-1].replace("''","'") for t in tokens)
        mock='function Get-AppxPackage { '+("[pscustomobject]@{ Architecture='"+ui['Architecture']+"'; Version='"+ui['Version']+"' }" if ui else '')+' }\n'
        environment=os.environ.copy()
        if not net: environment['ProgramW6432']=str(folder/'missing-dotnet')
        process=subprocess.run([shell,'-NoProfile','-NonInteractive','-Command',mock+script],env=environment,capture_output=True,text=True)
        assert process.returncode==0,(name,process.stderr)
        actual=tuple(Path(markers[m]).exists() for m in ('NetMarker','WinUIMarker'))
        assert marker.exists() and actual==expected,(name,actual,expected)
        print(json.dumps({'case':name,'net':actual[0],'winui':actual[1],'passed':True}))
print('Administrator installation and runtime detection cases verified')
