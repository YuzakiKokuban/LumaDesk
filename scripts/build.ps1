param([string]$Version, [switch]$NoRestore)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content -LiteralPath (Join-Path $projectRoot 'Cargo.toml') -Raw
$sourceVersion = [regex]::Match($manifest, '(?m)^version\s*=\s*"([^"]+)"').Groups[1].Value
if (!$sourceVersion) { throw 'Cargo package version is missing' }
if (!$Version) { $Version = $sourceVersion }
if ($Version -ne $sourceVersion) { throw "Requested version $Version differs from source $sourceVersion" }
[xml]$appProject = Get-Content -LiteralPath (Join-Path $projectRoot 'app/JiYaoChu.csproj') -Raw
if ($appProject.SelectSingleNode('/Project/PropertyGroup/Version').InnerText -ne $sourceVersion) { throw 'Rust and application versions differ' }
$publishDir = Join-Path $projectRoot "artifacts\LumaDesk-$Version-win-x64"
Push-Location $projectRoot
try {
    cargo build --locked --release
    if ($LASTEXITCODE -ne 0) { throw 'Rust build failed' }
    $publishArgs = @('publish', 'app/JiYaoChu.csproj', '-c', 'Release', '-r', 'win-x64', '-o', $publishDir, '-v', 'minimal', '-p:RestoreLockedMode=true')
    if ($NoRestore) { $publishArgs += '--no-restore' }
    dotnet @publishArgs
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
    $hash = Get-FileHash -LiteralPath $archivePath -Algorithm SHA256
    [IO.File]::WriteAllText(($archivePath + '.sha256'), ($hash.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($archivePath) + "`n"), [Text.UTF8Encoding]::new($false))
    $hash
} finally {
    Pop-Location
}
