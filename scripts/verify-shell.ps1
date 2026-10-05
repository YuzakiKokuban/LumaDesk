param([Parameter(Mandatory)][string]$ApplicationDirectory, [string]$Report = (Join-Path $PSScriptRoot '../artifacts/shell-validation.json'))
$ErrorActionPreference = 'Stop'
$directory = (Resolve-Path -LiteralPath $ApplicationDirectory).Path
$reportPath = [IO.Path]::GetFullPath($Report)
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
$env:JIYAOCHU_FORCE_MOCK = '1'
$env:JIYAOCHU_DATA_DIR = Join-Path (Split-Path $reportPath -Parent) ('shell-test-config-' + [guid]::NewGuid().ToString('N'))
if ($env:JIYAOCHU_VERIFY_SETTINGS -eq '1') {
    New-Item -ItemType Directory -Path $env:JIYAOCHU_DATA_DIR -Force | Out-Null
    # Older preferences must be claimed at startup without turning a saved-off keyboard on.
    [IO.File]::WriteAllText((Join-Path $env:JIYAOCHU_DATA_DIR 'config.json'), '{"lighting":{"firmware_managed":true,"enabled":false,"kb_engine":"better_rgb","kb_effect":100,"kb_brightness":2,"kb_color":"#123456","kb_fps":30,"streamer_effect":0,"custom_script_id":null,"four_zone_colors":["#ff0000","#00ff00","#0000ff","#ffffff"],"logo_color":"#ff00ff","hinge_color":"#00ffff","lightbar_color":"#ff0000","sleep_minutes":0}}')
}
$previewPath = [IO.Path]::ChangeExtension($reportPath, '.preview.json')
foreach ($file in @($reportPath, $previewPath, ($reportPath + '.lighting.png'), ($reportPath + '.colors.png'), ($reportPath + '.system.png'), ($reportPath + '.restore.png'), ($reportPath + '.tuning.png'), ($reportPath + '.gpu.png'), ($reportPath + '.display.png'), ($reportPath + '.overview.png'), ($reportPath + '.trends.png'))) { if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file } }
$arguments = '--background "--verify-shell=' + $reportPath + '"'
$executable = Join-Path $directory 'LumaDesk.exe'
if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) -and
    [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($executable)).Contains('requireAdministrator')) {
    throw 'Run from an elevated shell, or build an isolated asInvoker mock test host as documented in BUILD.md.'
}
$app = Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory $directory -WindowStyle Hidden -PassThru
if (-not ('LumaDeskCapture' -as [type])) {
    Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public static class LumaDeskCapture { [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags); }'
}
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(180)
    while ([DateTime]::UtcNow -lt $deadline -and !(Test-Path -LiteralPath $reportPath)) {
        Start-Sleep -Milliseconds 100
        $app.Refresh()
        if ($app.HasExited -and !(Test-Path -LiteralPath $reportPath)) { throw 'Application exited before completing shell verification; see ui-errors.log' }
        if (Test-Path -LiteralPath $previewPath) {
            $previewStream = [IO.File]::Open($previewPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
            $previewReader = [IO.StreamReader]::new($previewStream)
            try { $preview = $previewReader.ReadToEnd() | ConvertFrom-Json }
            finally { $previewReader.Dispose() }
            if ($preview.page_bounds -and $preview.page_capture_path -and !(Test-Path -LiteralPath $preview.page_capture_path)) {
                $capturePath = [IO.Path]::GetFullPath($preview.page_capture_path)
                if ((Split-Path $capturePath -Parent) -ne (Split-Path $reportPath -Parent)) { throw 'Screenshot path is outside the report directory' }
                Add-Type -AssemblyName System.Drawing
                $r = $preview.page_bounds
                $bitmap = [Drawing.Bitmap]::new([int]($r.Right - $r.Left), [int]($r.Bottom - $r.Top))
                $graphics = [Drawing.Graphics]::FromImage($bitmap)
                try {
                    $dc = $graphics.GetHdc()
                    try { if (![LumaDeskCapture]::PrintWindow([IntPtr]([long]$preview.page_handle), $dc, 2)) { throw 'Application window capture failed' } }
                    finally { $graphics.ReleaseHdc($dc) }
                    $bitmap.Save($capturePath, [Drawing.Imaging.ImageFormat]::Png)
                } finally { $graphics.Dispose(); $bitmap.Dispose() }
            }
            if ($env:JIYAOCHU_VERIFY_SCREENSHOTS -ne '0' -and $preview.osd_bounds -and !(Test-Path -LiteralPath ($reportPath + '.png'))) {
                Add-Type -AssemblyName System.Drawing
                $r = $preview.osd_bounds
                $bitmap = [Drawing.Bitmap]::new([int]($r.Right - $r.Left), [int]($r.Bottom - $r.Top))
                $graphics = [Drawing.Graphics]::FromImage($bitmap)
                try {
                    $graphics.CopyFromScreen($r.Left, $r.Top, 0, 0, $bitmap.Size)
                    $bitmap.Save($reportPath + '.png', [Drawing.Imaging.ImageFormat]::Png)
                } finally { $graphics.Dispose(); $bitmap.Dispose() }
            }
        }
    }
    if (!(Test-Path -LiteralPath $reportPath)) { throw 'Shell verification timed out' }
    $result = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if (!$result.passed) { throw $result.error }
    if (!$app.WaitForExit(5000)) { throw 'Tray exit left the application running' }
    if ($app.ExitCode -ne 0) { throw "Application exit failed: $($app.ExitCode)" }
} finally { if (!$app.HasExited) { $app.Kill() } }
Get-Content -LiteralPath $reportPath
