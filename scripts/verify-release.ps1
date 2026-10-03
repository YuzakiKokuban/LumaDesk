param([Parameter(Mandatory)][string]$Tag)
$ErrorActionPreference = 'Stop'

$manifest = Get-Content -LiteralPath 'Cargo.toml' -Raw
$version = [regex]::Match($manifest, '(?m)^version\s*=\s*"([^"]+)"').Groups[1].Value
if (!$version -or $Tag -cne "v$version") { throw 'Release tag does not match package version' }
if ($version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw 'Unsupported release version format' }
if (!(Test-Path -LiteralPath "docs/releases/$version.md")) { throw 'Release notes are missing' }

$branch = if ($version.Contains('-')) { 'dev' } else { 'main' }
git fetch origin "+refs/heads/${branch}:refs/remotes/origin/${branch}"
if ($LASTEXITCODE -ne 0) { throw 'Could not fetch release branch' }
$tagCommit = git rev-parse --verify "refs/tags/$Tag^{commit}"
if ($LASTEXITCODE -ne 0) { throw 'Release tag is missing' }
$headCommit = git rev-parse --verify HEAD
if ($LASTEXITCODE -ne 0 -or $tagCommit -ne $headCommit) { throw 'Checkout does not match release tag' }
git merge-base --is-ancestor $tagCommit "refs/remotes/origin/$branch"
if ($LASTEXITCODE -ne 0) { throw "Release tag must belong to $branch" }
Write-Output "Release source verified: $Tag on $branch"
