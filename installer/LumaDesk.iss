#ifndef AppVersion
  #error AppVersion is required
#endif
#ifndef PublishDir
  #error PublishDir is required
#endif
#define AppName "机耀处 · LumaDesk"
#define AppExe "机耀处.exe"

[Setup]
AppId={{45F4C1DE-8087-475F-9972-8D8571CC6772}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=YuzakiKokuban
AppPublisherURL=https://github.com/YuzakiKokuban/LumaDesk
AppSupportURL=https://github.com/YuzakiKokuban/LumaDesk/issues
DefaultDirName={autopf}\LumaDesk
DefaultGroupName=LumaDesk
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
OutputDir=..\artifacts
OutputBaseFilename=LumaDesk-{#AppVersion}-win-x64-Setup
SetupIconFile=..\assets\LumaDesk.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
LicenseFile=..\LICENSE
VersionInfoVersion=0.2.0.0

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopShortcut}"; GroupDescription: "{cm:Shortcuts}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent unchecked

[CustomMessages]
english.DesktopShortcut=Create a desktop shortcut
english.Shortcuts=Shortcuts:
english.LaunchApp=Launch LumaDesk
english.RuntimeRequired=Install the x64 .NET 10 Desktop Runtime and Windows App SDK Runtime 2.5.1 (or a newer 2.x stable version), then run this installer again.%n%n.NET: https://dotnet.microsoft.com/en-us/download/dotnet/10.0%nWinUI: https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads
chinesesimplified.DesktopShortcut=创建桌面快捷方式
chinesesimplified.Shortcuts=快捷方式：
chinesesimplified.LaunchApp=启动机耀处
chinesesimplified.RuntimeRequired=请先安装 x64 的 .NET 10 Desktop Runtime 和 Windows App SDK Runtime 2.5.1（或更新的 2.x 稳定版），然后重新运行安装程序。%n%n.NET：https://dotnet.microsoft.com/en-us/download/dotnet/10.0%nWinUI：https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads

[Code]
function PSQuote(Value: String): String;
begin
  StringChangeEx(Value, '''', '''''', True);
  Result := Value;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Code: Integer;
  Script, Marker: String;
begin
  Marker := ExpandConstant('{tmp}\LumaDesk-runtime-ready');
  Script := '$ErrorActionPreference=''Stop''; try { ' +
    '$net=@(& (Join-Path $env:ProgramFiles ''dotnet\dotnet.exe'') --list-runtimes | Select-String ''^Microsoft.NETCore.App 10\.''); ' +
    '$ui=@(Get-AppxPackage Microsoft.WindowsAppRuntime.2 | Where-Object { $_.Architecture -eq ''X64'' -and [version]$_.Version -ge [version]''2.5.1.0'' }); ' +
    'if ($net.Count -gt 0 -and $ui.Count -gt 0) { Set-Content -LiteralPath ''' + PSQuote(Marker) + ''' -Value ready } } catch { exit 1 }';
  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -NonInteractive -Command "' + Script + '"', '', SW_HIDE, ewWaitUntilTerminated, Code) then
    Result := CustomMessage('RuntimeRequired')
  else if (Code <> 0) or not FileExists(Marker) then
    Result := CustomMessage('RuntimeRequired')
  else
    Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Code: Integer;
  Script: String;
begin
  if CurUninstallStep = usUninstall then begin
    // Remove only a startup task that points into this installation.
    Script := '$t=Get-ScheduledTask -TaskName LumaDesk -TaskPath ''\'' -ErrorAction SilentlyContinue; ' +
      'if ($t -and ($t.Actions.Execute -contains ''' + PSQuote(ExpandConstant('{app}\{#AppExe}')) + ''')) { ' +
      'Unregister-ScheduledTask -TaskName LumaDesk -TaskPath ''\'' -Confirm:$false }';
    Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
      '-NoProfile -NonInteractive -Command "' + Script + '"', '', SW_HIDE, ewWaitUntilTerminated, Code);
    // The application owns the OEM backup and performs the restoration.
    if FileExists(ExpandConstant('{userappdata}\JiYaoChu\oem_takeover.json')) then begin
      if not Exec(ExpandConstant('{app}\{#AppExe}'), '--restore-oem', ExpandConstant('{app}'),
        SW_HIDE, ewWaitUntilTerminated, Code) then
        RaiseException('Unable to restore the OEM control center. Restore it in LumaDesk before uninstalling.');
      if Code <> 0 then
        RaiseException('OEM restoration failed. Restore it in LumaDesk before uninstalling.');
    end;
  end;
end;
