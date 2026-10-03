param([Parameter(Mandatory)][string]$Tag)
$ErrorActionPreference = 'Stop'

$versionJson = python (Join-Path $PSScriptRoot 'version.py') --tag $Tag
if ($LASTEXITCODE -ne 0) { throw 'Unsupported release tag' }
$versionInfo = $versionJson | ConvertFrom-Json
$version = $versionInfo.version
if (!(Test-Path -LiteralPath "docs/releases/$version.md") -and !(Test-Path -LiteralPath 'docs/releases/NOTES.md')) { throw 'Release notes are missing' }

$branch = $versionInfo.branch
git fetch origin "+refs/heads/${branch}:refs/remotes/origin/${branch}"
if ($LASTEXITCODE -ne 0) { throw 'Could not fetch release branch' }
$tagCommit = git rev-parse --verify "refs/tags/$Tag^{commit}"
if ($LASTEXITCODE -ne 0) { throw 'Release tag is missing' }
$headCommit = git rev-parse --verify HEAD
if ($LASTEXITCODE -ne 0 -or $tagCommit -ne $headCommit) { throw 'Checkout does not match release tag' }
git merge-base --is-ancestor $tagCommit "refs/remotes/origin/$branch"
if ($LASTEXITCODE -ne 0) { throw "Release tag must belong to $branch" }
Write-Output "Release source verified: $Tag on $branch"
