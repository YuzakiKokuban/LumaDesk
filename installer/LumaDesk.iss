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
PrivilegesRequiredOverridesAllowed=
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
VersionInfoVersion=0.2.0.2

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
english.MissingNet=Missing x64 .NET 10 Runtime.
english.MissingWinUI=Missing x64 Windows App SDK Runtime 2.5.1 or newer 2.x stable version.
english.DownloadOpened=The official download page for each missing component has been opened. Install the components, then click Install again.
english.RuntimeCheckFailed=Unable to check runtime dependencies. Install the required components and retry.
english.RuntimeRequired=Install the x64 .NET 10 Desktop Runtime and Windows App SDK Runtime 2.5.1 (or a newer 2.x stable version), then run this installer again.%n%n.NET: https://dotnet.microsoft.com/en-us/download/dotnet/10.0%nWinUI: https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads
chinesesimplified.DesktopShortcut=创建桌面快捷方式
chinesesimplified.Shortcuts=快捷方式：
chinesesimplified.LaunchApp=启动机耀处
chinesesimplified.MissingNet=缺少 x64 .NET 10 Runtime。
chinesesimplified.MissingWinUI=缺少 x64 Windows App SDK Runtime 2.5.1 或更新的 2.x 稳定版。
chinesesimplified.DownloadOpened=已打开缺失组件的官方下载页。安装组件后，点击“安装”重新检测。
chinesesimplified.RuntimeCheckFailed=无法检测运行依赖，请安装所需组件后重试。
chinesesimplified.RuntimeRequired=请先安装 x64 的 .NET 10 Desktop Runtime 和 Windows App SDK Runtime 2.5.1（或更新的 2.x 稳定版），然后重新运行安装程序。%n%n.NET：https://dotnet.microsoft.com/en-us/download/dotnet/10.0%nWinUI：https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads

[Code]
var
  NetDownloadOpened, WinUIDownloadOpened: Boolean;

function OpenDownload(URL: String): Boolean;
var
  Code: Integer;
begin
  Result := False;
  if WizardSilent then Exit;
  Result := ShellExecAsOriginalUser('open', URL, '', '', SW_SHOWNORMAL, ewNoWait, Code);
  if not Result then
    Result := ShellExec('open', URL, '', '', SW_SHOWNORMAL, ewNoWait, Code);
end;

function PSQuote(Value: String): String;
begin
  StringChangeEx(Value, '''', '''''', True);
  Result := Value;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Code: Integer;
  Script, Marker, NetMarker, WinUIMarker: String;
  AllDownloadsOpened: Boolean;
begin
  Marker := ExpandConstant('{tmp}\LumaDesk-runtime-ready');
  NetMarker := Marker + '-net';
  WinUIMarker := Marker + '-winui';
  DeleteFile(Marker);
  DeleteFile(NetMarker);
  DeleteFile(WinUIMarker);
  Script := '$ErrorActionPreference=''Stop''; try { ' +
    '$exe=Join-Path $env:ProgramW6432 ''dotnet\dotnet.exe''; ' +
    'if ((Test-Path -LiteralPath $exe) -and @(& $exe --list-runtimes | Select-String ''^Microsoft.NETCore.App 10\.[0-9]+\.[0-9]+ \['').Count -gt 0) { Set-Content -LiteralPath ''' + PSQuote(NetMarker) + ''' -Value ready }; ' +
    '$ui=@(Get-AppxPackage Microsoft.WindowsAppRuntime.2 -ErrorAction Stop | Where-Object { $_.Architecture -eq ''X64'' -and [version]$_.Version -ge [version]''2.5.1.0'' }); ' +
    'if ($ui.Count -gt 0) { Set-Content -LiteralPath ''' + PSQuote(WinUIMarker) + ''' -Value ready }; ' +
    'Set-Content -LiteralPath ''' + PSQuote(Marker) + ''' -Value checked } catch { exit 1 }';
  if not Exec(ExpandConstant('{sysnative}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -NonInteractive -Command "' + Script + '"', '', SW_HIDE, ewWaitUntilTerminated, Code) then begin
    Result := CustomMessage('RuntimeCheckFailed') + #13#10 + CustomMessage('RuntimeRequired');
    Exit;
  end;
  if (Code <> 0) or not FileExists(Marker) then begin
    Result := CustomMessage('RuntimeCheckFailed') + #13#10 + CustomMessage('RuntimeRequired');
    Exit;
  end;
  Result := '';
  AllDownloadsOpened := True;
  if not FileExists(NetMarker) then begin
    Result := CustomMessage('MissingNet') + #13#10;
    if not NetDownloadOpened then begin
      NetDownloadOpened := OpenDownload('https://dotnet.microsoft.com/en-us/download/dotnet/10.0');
    end;
    AllDownloadsOpened := AllDownloadsOpened and NetDownloadOpened;
  end;
  if not FileExists(WinUIMarker) then begin
    Result := Result + CustomMessage('MissingWinUI') + #13#10;
    if not WinUIDownloadOpened then begin
      WinUIDownloadOpened := OpenDownload('https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads');
    end;
    AllDownloadsOpened := AllDownloadsOpened and WinUIDownloadOpened;
  end;
  if Result <> '' then begin
    if WizardSilent then Result := Result + CustomMessage('RuntimeRequired')
    else if AllDownloadsOpened then Result := Result + CustomMessage('DownloadOpened')
    else Result := Result + CustomMessage('RuntimeRequired');
  end;
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
    Exec(ExpandConstant('{sysnative}\WindowsPowerShell\v1.0\powershell.exe'),
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
