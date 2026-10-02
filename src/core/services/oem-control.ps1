$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$backupPath = $env:LUMADESK_OEM_BACKUP
$action = $env:LUMADESK_OEM_ACTION
$service = Get-CimInstance Win32_Service -Filter "Name='GCUBridge'"
if (!$service) { throw 'GCUBridge service was not found' }
$exe = $service.PathName.Trim('"')
$root = Split-Path (Split-Path $exe -Parent) -Parent
if ($exe -notmatch '(?i)\\OEM\\.+\\AiStoneService\\GCUBridge\.exe$') { throw 'GCUBridge path does not belong to the OEM control center' }
$registry = 'HKLM:\SYSTEM\CurrentControlSet\Services\GCUBridge'
if ($action -eq 'takeover') {
 if (!(Test-Path -LiteralPath $backupPath)) {
  $tasks = @(Get-ScheduledTask | Where-Object { @($_.Actions | Where-Object { $_.Execute -and $_.Execute.Trim('"').StartsWith($root + '\',[StringComparison]::OrdinalIgnoreCase) }).Count -gt 0 } | ForEach-Object {
   [pscustomobject]@{ name=$_.TaskName;path=$_.TaskPath;enabled=$_.Settings.Enabled }
  })
  $backup = [pscustomobject]@{ start=(Get-ItemProperty -LiteralPath $registry).Start;running=($service.State -eq 'Running');root=$root;tasks=$tasks }
  New-Item -ItemType Directory -Force -Path (Split-Path $backupPath -Parent) | Out-Null
  $backup | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath ($backupPath + '.tmp') -Encoding UTF8
  Move-Item -LiteralPath ($backupPath + '.tmp') -Destination $backupPath -Force
 }
 $backup = Get-Content -Raw -LiteralPath $backupPath | ConvertFrom-Json
 if ($backup.root -ne $root) { throw 'OEM backup belongs to a different installation' }
 foreach($task in $backup.tasks) { Disable-ScheduledTask -TaskName $task.name -TaskPath $task.path | Out-Null }
 Set-Service -Name GCUBridge -StartupType Disabled
 if ((Get-Service GCUBridge).Status -ne 'Stopped') { Stop-Service -Name GCUBridge -Force; (Get-Service GCUBridge).WaitForStatus('Stopped',[TimeSpan]::FromSeconds(15)) }
 Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($root + '\',[StringComparison]::OrdinalIgnoreCase) } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction Stop }
 if ((Get-Service GCUBridge).Status -ne 'Stopped') { throw 'GCUBridge did not stop' }
 Set-Content -LiteralPath ($backupPath + '.active') -Value 'GCUBridge stopped; owned startup tasks disabled' -Encoding UTF8
} elseif ($action -eq 'restore') {
 if (!(Test-Path -LiteralPath $backupPath)) { throw 'No OEM takeover backup exists; nothing was changed' }
 $backup = Get-Content -Raw -LiteralPath $backupPath | ConvertFrom-Json
 if ($backup.root -ne $root) { throw 'OEM backup belongs to a different installation' }
 # SCM updates both its configuration and the registry; sc start= accepts original 2/3/4.
 $startName = switch([int]$backup.start) { 2 { 'auto' } 3 { 'demand' } 4 { 'disabled' } default { throw 'Unsupported original service startup mode' } }
 & "$env:SystemRoot\System32\sc.exe" config GCUBridge start= $startName | Out-Null
 if ($LASTEXITCODE -ne 0) { throw 'Could not restore service configuration' }
 foreach($task in $backup.tasks) { if ($task.enabled) { Enable-ScheduledTask -TaskName $task.name -TaskPath $task.path | Out-Null } else { Disable-ScheduledTask -TaskName $task.name -TaskPath $task.path | Out-Null } }
 if ($backup.running) { Start-Service -Name GCUBridge }
 Remove-Item -LiteralPath $backupPath
 if (Test-Path -LiteralPath ($backupPath + '.active')) { Remove-Item -LiteralPath ($backupPath + '.active') }
} else { throw 'Unknown OEM action' }
Write-Output 'OEM operation complete'
