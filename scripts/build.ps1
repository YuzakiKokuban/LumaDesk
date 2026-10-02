param([string]$Version = '0.1.0')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $projectRoot "artifacts\LumaDesk-$Version-win-x64"
Push-Location $projectRoot
try {
    cargo build --release
    if ($LASTEXITCODE -ne 0) { throw 'Rust build failed' }
    dotnet publish app/JiYaoChu.csproj -c Release -r win-x64 -o $publishDir -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'WinUI publish failed' }
    Copy-Item -LiteralPath 'target\release\jiyaochu-ctl.exe' -Destination $publishDir
    Copy-Item -LiteralPath 'README.md','README_en.md','LICENSE' -Destination $publishDir
    Copy-Item -LiteralPath 'docs' -Destination $publishDir -Recurse -Force
    $protocolDir = Join-Path $publishDir 'reverse/native'
    New-Item -ItemType Directory -Path $protocolDir -Force | Out-Null
    Copy-Item -LiteralPath 'reverse/native/REPORT.md','reverse/native/MUX.md','reverse/native/RGB.md','reverse/native/PERFORMANCE.md' -Destination $protocolDir
    Copy-Item -LiteralPath 'reverse/native/evidence' -Destination $protocolDir -Recurse -Force
    Copy-Item -LiteralPath 'THIRD_PARTY_NOTICES.md' -Destination $publishDir
    $archivePath = Join-Path $projectRoot "artifacts\LumaDesk-$Version-win-x64.zip"
    Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $archivePath -Force
    Get-FileHash -LiteralPath $archivePath -Algorithm SHA256
} finally {
    Pop-Location
}
