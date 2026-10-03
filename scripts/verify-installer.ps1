param([Parameter(Mandatory)][string]$Installer, [Parameter(Mandatory)][string]$Directory)
$ErrorActionPreference = 'Stop'
$destination = [IO.Path]::GetFullPath($Directory)
if (Test-Path -LiteralPath $destination) { throw 'Installer test requires a new, empty destination' }
$setup = [IO.Path]::GetFullPath($Installer)
$expected = (Get-Content -LiteralPath ($setup + '.sha256')).Split(' ')[0]
if ((Get-FileHash $setup).Hash.ToLowerInvariant() -ne $expected) { throw 'Installer checksum mismatch' }
$installed = $false
try {
    $process = Start-Process $setup -ArgumentList ('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /DIR="' + $destination + '" /LOG="' + $destination + '-setup.log"') -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Installation failed: $($process.ExitCode)" }
    $installed = $true
    # Upgrade an existing installation containing the previous assembly name.
    $obsolete = @('机耀处.exe','机耀处.dll','机耀处.pri','机耀处.deps.json','机耀处.runtimeconfig.json')
    foreach ($file in $obsolete) { Set-Content -LiteralPath (Join-Path $destination $file) -Value 'old application output' }
    $keep = Join-Path $destination 'upgrade-preserve.txt'
    Set-Content -LiteralPath $keep -Value 'preserve unrelated files'
    $upgrade = Start-Process $setup -ArgumentList ('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /DIR="' + $destination + '"') -WindowStyle Hidden -Wait -PassThru
    if ($upgrade.ExitCode -ne 0) { throw "Upgrade failed: $($upgrade.ExitCode)" }
    foreach ($file in $obsolete) {
        if (Test-Path -LiteralPath (Join-Path $destination $file)) { throw "Upgrade left obsolete output: $file" }
    }
    if ((Get-Content -LiteralPath $keep) -ne 'preserve unrelated files') { throw 'Upgrade changed unrelated files' }
    foreach ($file in @('LumaDesk.exe','LumaDesk.pri','jiyaochu_core.dll','jiyaochu-ctl.exe','unins000.exe')) {
        if (!(Test-Path -LiteralPath (Join-Path $destination $file))) { throw "Missing installed file: $file" }
    }
    $cli = Join-Path $destination 'jiyaochu-ctl.exe'
    $reply = & $cli system.ping | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or !$reply.ok) { throw 'Installed CLI failed' }
    $installedApp = Start-Process -FilePath (Join-Path $destination 'LumaDesk.exe') -WorkingDirectory $destination -PassThru
    try {
        Start-Sleep -Seconds 8
        $installedApp.Refresh()
        if ($installedApp.HasExited -or $installedApp.MainWindowHandle -eq 0) { throw 'Installed application did not open a window' }
    } finally {
        if (!$installedApp.HasExited) { $installedApp.CloseMainWindow() | Out-Null; if (!$installedApp.WaitForExit(5000)) { $installedApp.Kill() } }
    }
} finally {
    if ($installed) {
        $uninstall = Start-Process (Join-Path $destination 'unins000.exe') -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -WindowStyle Hidden -Wait -PassThru
        if ($uninstall.ExitCode -ne 0) { throw "Uninstallation failed: $($uninstall.ExitCode)" }
        if (Test-Path -LiteralPath (Join-Path $destination 'LumaDesk.exe')) { throw 'Uninstallation left the application executable' }
    }
}
'Verified installer: checksum, installation, renamed-output upgrade, installed CLI, application startup, uninstallation'
