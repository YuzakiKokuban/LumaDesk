param(
    [Parameter(Mandatory=$true)][string]$ApplicationDirectory,
    [string]$Output
)
$ErrorActionPreference = 'Stop'
if (!$Output) { $Output = Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/startup-validation.json' }
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if (!([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run as administrator' }
$directory = (Resolve-Path -LiteralPath $ApplicationDirectory).Path
$cli = Join-Path $directory 'jiyaochu-ctl.exe'
$application = Join-Path $directory '机耀处.exe'
if (!(Test-Path -LiteralPath $application -PathType Leaf)) { throw 'Application is missing' }
$original = Get-ScheduledTask -TaskPath '\' -TaskName LumaDesk -ErrorAction SilentlyContinue
$originalXml = if ($original) { Export-ScheduledTask -TaskPath '\' -TaskName LumaDesk } else { $null }
$previousProcesses = @(Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -eq $application } | Select-Object -ExpandProperty ProcessId)
$report = [ordered]@{ captured_utc=[DateTime]::UtcNow.ToString('o'); passed=$false; original_task_present=($null -ne $original) }
function Invoke-Core([string]$command, [string]$payload = '{}') {
    $replyText = $payload | & $cli $command -
    if ($LASTEXITCODE) { throw "$command failed: $replyText" }
    $reply = $replyText | ConvertFrom-Json
    if (!$reply.ok) { throw "$command failed: $($reply.error)" }
    return $reply.data
}
try {
    Invoke-Core set_autostart_enabled '{"enabled":false}' | Out-Null
    if (Invoke-Core get_autostart) { throw 'Disable did not take effect' }
    Invoke-Core set_autostart_enabled '{"enabled":true}' | Out-Null
    if (!(Invoke-Core get_autostart)) { throw 'Enable did not take effect' }
    $task = Get-ScheduledTask -TaskPath '\' -TaskName LumaDesk
    $report.task = @{ run_level=[string]$task.Principal.RunLevel; logon_type=[string]$task.Principal.LogonType; execute=$task.Actions[0].Execute; trigger=$task.Triggers[0].CimClass.CimClassName }
    if ($task.Principal.RunLevel -ne 'Highest' -or $task.Actions[0].Execute -ne $application -or $report.task.trigger -ne 'MSFT_TaskLogonTrigger') { throw 'Invalid logon task' }
    Start-ScheduledTask -TaskPath '\' -TaskName LumaDesk
    $deadline = [DateTime]::UtcNow.AddSeconds(25)
    do {
        Start-Sleep -Milliseconds 500
        $launched = @(Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -eq $application -and $_.ProcessId -notin $previousProcesses })
    } while (!$launched.Count -and [DateTime]::UtcNow -lt $deadline)
    if (!$launched.Count) { throw 'Scheduled launch did not start the application' }
    Start-Sleep -Seconds 4
    $report.launched = @($launched | ForEach-Object { @{ pid=$_.ProcessId; running=($null -ne (Get-Process -Id $_.ProcessId -ErrorAction SilentlyContinue)) } })
    if (@($report.launched | Where-Object { !$_.running }).Count) { throw 'Application exited after scheduled launch' }
    $report.passed = $true
} catch { $report.error = $_.Exception.Message } finally {
    try {
        # Only close processes created by this test, using the verified path and PID.
        Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -eq $application -and $_.ProcessId -notin $previousProcesses } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
        if ($originalXml) { Register-ScheduledTask -TaskPath '\' -TaskName LumaDesk -Xml $originalXml -Force | Out-Null }
        else { Unregister-ScheduledTask -TaskPath '\' -TaskName LumaDesk -Confirm:$false -ErrorAction SilentlyContinue }
        $restored = Get-ScheduledTask -TaskPath '\' -TaskName LumaDesk -ErrorAction SilentlyContinue
        $report.restored = if ($originalXml) { (Export-ScheduledTask -TaskPath '\' -TaskName LumaDesk) -eq $originalXml } else { $null -eq $restored }
        if (!$report.restored) { throw 'Original task was not restored' }
    } catch { $report.restore_error = $_.Exception.Message; $report.passed = $false }
    $outputDirectory = Split-Path $Output -Parent
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Output -Encoding UTF8
}
if (!$report.passed) { exit 1 }
