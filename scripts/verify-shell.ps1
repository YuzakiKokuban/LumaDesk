param([Parameter(Mandatory)][string]$ApplicationDirectory, [string]$Report = (Join-Path $PSScriptRoot '../artifacts/shell-validation.json'))
$ErrorActionPreference = 'Stop'
$directory = (Resolve-Path -LiteralPath $ApplicationDirectory).Path
$reportPath = [IO.Path]::GetFullPath($Report)
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
$env:JIYAOCHU_FORCE_MOCK = '1'
$env:JIYAOCHU_DATA_DIR = Join-Path (Split-Path $reportPath -Parent) ('shell-test-config-' + [guid]::NewGuid().ToString('N'))
$previewPath = [IO.Path]::ChangeExtension($reportPath, '.preview.json')
foreach ($file in @($reportPath, $previewPath)) { if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file } }
$arguments = '--background "--verify-shell=' + $reportPath + '"'
$executable = Join-Path $directory '机耀处.exe'
if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) -and
    [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($executable)).Contains('requireAdministrator')) {
    throw 'Run from an elevated shell, or build an isolated asInvoker mock test host as documented in BUILD.md.'
}
$app = Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory $directory -WindowStyle Hidden -PassThru
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    while ([DateTime]::UtcNow -lt $deadline -and !(Test-Path -LiteralPath $reportPath)) {
        Start-Sleep -Milliseconds 100
        $app.Refresh()
        if ($app.HasExited -and !(Test-Path -LiteralPath $reportPath)) { throw 'Application exited before completing shell verification; see ui-errors.log' }
        if (Test-Path -LiteralPath $previewPath) {
            $preview = Get-Content -LiteralPath $previewPath -Raw | ConvertFrom-Json
            if ($preview.osd_bounds -and !(Test-Path -LiteralPath ($reportPath + '.png'))) {
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
