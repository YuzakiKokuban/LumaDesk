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
if ($NoRestore) {
    $assets = Get-Content -LiteralPath (Join-Path $projectRoot 'app/obj/project.assets.json') -Raw | ConvertFrom-Json
    if ($assets.libraries.PSObject.Properties.Name -match '^Microsoft.UI.Reactor.Devtools/') {
        throw 'Debug dependencies are active. Restore with -p:Configuration=Release before publishing with -NoRestore.'
    }
}
$publishDir = Join-Path $projectRoot ("artifacts\publish-" + [guid]::NewGuid().ToString('N'))
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
    Copy-Item -LiteralPath 'THIRD_PARTY_NOTICES.md' -Destination $publishDir
    Copy-Item -LiteralPath 'docs/RUNTIMES.md' -Destination $publishDir
    $archivePath = Join-Path $projectRoot "artifacts\LumaDesk-$Version-win-x64.zip"
    Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $archivePath -Force
    $hash = Get-FileHash -LiteralPath $archivePath -Algorithm SHA256
    [IO.File]::WriteAllText(($archivePath + '.sha256'), ($hash.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($archivePath) + "`n"), [Text.UTF8Encoding]::new($false))
    $hash
    $compiler = & (Join-Path $PSScriptRoot 'ensure-inno.ps1')
    & $compiler '/Q' "/DAppVersion=$Version" "/DPublishDir=$publishDir" (Join-Path $projectRoot 'installer/LumaDesk.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
    $installer = Join-Path $projectRoot "artifacts\LumaDesk-$Version-win-x64-Setup.exe"
    $installerHash = Get-FileHash -LiteralPath $installer -Algorithm SHA256
    [IO.File]::WriteAllText(($installer + '.sha256'), ($installerHash.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($installer) + "`n"), [Text.UTF8Encoding]::new($false))
    $installerHash
} finally {
    Pop-Location
}
