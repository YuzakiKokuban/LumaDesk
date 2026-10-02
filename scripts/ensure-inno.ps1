$ErrorActionPreference = 'Stop'
$tools = Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/tools'
$compiler = Join-Path $tools 'InnoSetup/ISCC.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    New-Item -ItemType Directory -Path $tools -Force | Out-Null
    $download = Join-Path $tools 'innosetup-7.1.0-x64.exe'
    Invoke-WebRequest 'https://github.com/jrsoftware/issrc/releases/download/is-7_1_0/innosetup-7.1.0-x64.exe' -OutFile $download
    if ((Get-FileHash $download).Hash -ne '0362A383ED217D4C4239B5933866DD96D3EB2102737DA92F80F6057A4B40DF2F') { throw 'Inno Setup checksum mismatch' }
    $signature = Get-AuthenticodeSignature $download
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Pyrsys B.V.') { throw 'Inno Setup publisher verification failed' }
    $process = Start-Process $download -ArgumentList ('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /CURRENTUSER /DIR="' + (Join-Path $tools 'InnoSetup') + '"') -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Inno Setup installation failed: $($process.ExitCode)" }
}
if (!(Test-Path -LiteralPath $compiler)) { throw 'Inno Setup compiler is missing' }
$compiler
