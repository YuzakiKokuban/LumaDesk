//! Per-user Task Scheduler logon entry with Highest privileges.
//! The UI requires elevation; an ordinary HKCU Run entry cannot provide it.
#[cfg(windows)]
fn task_command(action: &str) -> Result<String, String> {
    use std::os::windows::process::CommandExt;
    let current = std::env::current_exe().map_err(|e| e.to_string())?;
    let exe = if current.file_name().and_then(|p| p.to_str()) == Some("机耀处.exe") {
        current
    } else {
        current.with_file_name("机耀处.exe")
    };
    if action == "enable" && !exe.exists() {
        return Err("找不到机耀处.exe，请在完整发布目录启用自启动".into());
    }
    let script = r#"$ErrorActionPreference='Stop'; [Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$name='LumaDesk';$task=Get-ScheduledTask -TaskName $name -TaskPath '\' -ErrorAction SilentlyContinue
switch($env:LUMADESK_STARTUP_ACTION) {
 'query' { if ($task -and $task.Settings.Enabled -and $task.Principal.RunLevel -eq 'Highest') { Write-Output 'enabled' } else { Write-Output 'disabled' } }
 'disable' { if ($task) { Unregister-ScheduledTask -TaskName $name -TaskPath '\' -Confirm:$false } }
 'migrate' {
  # Upgrade only the task created by this application for this executable and
  # user. Do not create or enable a task, or replace unknown custom arguments.
  if ($task -and @($task.Actions).Count -eq 1 -and
      $task.Principal.RunLevel -eq 'Highest' -and $task.Principal.LogonType -eq 'Interactive') {
   $existing=$task.Actions[0]
   $user=[Security.Principal.WindowsIdentity]::GetCurrent()
   $owned=$false
   try {
    $owner=if ($task.Principal.UserId -eq $user.User.Value) { $user.User } else {
     ([Security.Principal.NTAccount]::new($task.Principal.UserId)).Translate([Security.Principal.SecurityIdentifier])
    }
    $path=$existing.Execute.Trim('"')
    $root=[IO.Path]::GetPathRoot($path)
    $owned=$owner.Value -eq $user.User.Value -and
     $existing.CimClass.CimClassName -eq 'MSFT_TaskExecAction' -and
     [IO.Path]::IsPathRooted($path) -and $root -ne '\' -and $root -notmatch '^[A-Za-z]:$' -and
     [string]::Equals([IO.Path]::GetFullPath($path), [IO.Path]::GetFullPath($env:LUMADESK_STARTUP_EXE), [StringComparison]::OrdinalIgnoreCase)
   } catch { $owned=$false }
   if ($owned -and [string]::IsNullOrWhiteSpace($existing.Arguments)) {
    $existing.Arguments='--background'
    $task.Actions=@($existing)
    Set-ScheduledTask -InputObject $task | Out-Null
   }
  }
 }
 'enable' {
  $user=[Security.Principal.WindowsIdentity]::GetCurrent().Name
  $action=New-ScheduledTaskAction -Execute $env:LUMADESK_STARTUP_EXE -Argument '--background'
  $trigger=New-ScheduledTaskTrigger -AtLogOn -User $user
  $principal=New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Highest
  $settings=New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero)
  Register-ScheduledTask -TaskName $name -TaskPath '\' -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
 }
}
exit 0"#;
    let shell = std::path::PathBuf::from(
        std::env::var("SystemRoot").unwrap_or_else(|_| "C:\\Windows".into()),
    )
    .join("System32\\WindowsPowerShell\\v1.0\\powershell.exe");
    let output = std::process::Command::new(shell)
        .args(["-NoProfile", "-NonInteractive", "-Command", script])
        .env("LUMADESK_STARTUP_ACTION", action)
        .env("LUMADESK_STARTUP_EXE", exe)
        .creation_flags(0x08000000)
        .output()
        .map_err(|e| e.to_string())?;
    if !output.status.success() {
        return Err(String::from_utf8_lossy(&output.stderr).trim().into());
    }
    Ok(String::from_utf8_lossy(&output.stdout).trim().into())
}
#[cfg(windows)]
pub fn is_enabled() -> bool {
    task_command("query")
        .map(|v| v == "enabled")
        .unwrap_or(false)
}
#[cfg(windows)]
pub fn set_enabled(on: bool) -> Result<(), String> {
    task_command(if on { "enable" } else { "disable" }).map(|_| ())
}
/// Migrates an existing owned login task without changing whether it is enabled.
#[cfg(windows)]
pub fn ensure_background_launch() -> Result<(), String> {
    task_command("migrate").map(|_| ())
}
#[cfg(not(windows))]
pub fn is_enabled() -> bool {
    false
}
#[cfg(not(windows))]
pub fn set_enabled(_on: bool) -> Result<(), String> {
    Err("Windows required".into())
}
#[cfg(not(windows))]
pub fn ensure_background_launch() -> Result<(), String> {
    Ok(())
}
