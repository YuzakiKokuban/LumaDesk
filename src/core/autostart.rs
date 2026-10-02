//! Per-user Task Scheduler logon entry with Highest privileges.
//! The UI requires elevation; an ordinary HKCU Run entry cannot provide it.
#[cfg(windows)]
fn task_command(action: &str) -> Result<String, String> {
    use std::os::windows::process::CommandExt;
    let current = std::env::current_exe().map_err(|e| e.to_string())?;
    let exe = if current.file_name().and_then(|p| p.to_str()) == Some("机耀处.exe") { current }
        else { current.with_file_name("机耀处.exe") };
    if action == "enable" && !exe.exists() { return Err("找不到机耀处.exe，请在完整发布目录启用自启动".into()); }
    let script = r#"$ErrorActionPreference='Stop'; [Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$name='LumaDesk';$task=Get-ScheduledTask -TaskName $name -TaskPath '\' -ErrorAction SilentlyContinue
switch($env:LUMADESK_STARTUP_ACTION) {
 'query' { if ($task -and $task.Settings.Enabled -and $task.Principal.RunLevel -eq 'Highest') { Write-Output 'enabled' } else { Write-Output 'disabled' } }
 'disable' { if ($task) { Unregister-ScheduledTask -TaskName $name -TaskPath '\' -Confirm:$false } }
 'enable' {
  $user=[Security.Principal.WindowsIdentity]::GetCurrent().Name
  $action=New-ScheduledTaskAction -Execute $env:LUMADESK_STARTUP_EXE
  $trigger=New-ScheduledTaskTrigger -AtLogOn -User $user
  $principal=New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Highest
  $settings=New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero)
  Register-ScheduledTask -TaskName $name -TaskPath '\' -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
 }
}"#;
    let shell = std::path::PathBuf::from(std::env::var("SystemRoot").unwrap_or_else(|_| "C:\\Windows".into()))
        .join("System32\\WindowsPowerShell\\v1.0\\powershell.exe");
    let output = std::process::Command::new(shell).args(["-NoProfile","-NonInteractive","-Command",script])
        .env("LUMADESK_STARTUP_ACTION",action).env("LUMADESK_STARTUP_EXE",exe)
        .creation_flags(0x08000000).output().map_err(|e| e.to_string())?;
    if !output.status.success() { return Err(String::from_utf8_lossy(&output.stderr).trim().into()); }
    Ok(String::from_utf8_lossy(&output.stdout).trim().into())
}
#[cfg(windows)]
pub fn is_enabled() -> bool { task_command("query").map(|v| v == "enabled").unwrap_or(false) }
#[cfg(windows)]
pub fn set_enabled(on: bool) -> Result<(), String> { task_command(if on {"enable"} else {"disable"}).map(|_| ()) }
#[cfg(not(windows))]
pub fn is_enabled() -> bool { false }
#[cfg(not(windows))]
pub fn set_enabled(_on: bool) -> Result<(), String> { Err("Windows required".into()) }
