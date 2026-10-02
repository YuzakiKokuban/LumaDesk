param([Parameter(Mandatory)][string]$Executable, [Parameter(Mandatory)][string]$Output)
$ErrorActionPreference = 'Stop'
$started = [Diagnostics.Stopwatch]::StartNew()
$app = Start-Process -FilePath $Executable -WorkingDirectory (Split-Path $Executable) -PassThru
$samples = @()
try {
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Seconds 1
        $app.Refresh()
        if ($app.HasExited) { throw "Application exited: $($app.ExitCode)" }
        $samples += [pscustomobject]@{ second=$started.Elapsed.TotalSeconds; privateMiB=$app.PrivateMemorySize64/1MB; workingMiB=$app.WorkingSet64/1MB; cpuSeconds=$app.TotalProcessorTime.TotalSeconds; window=($app.MainWindowHandle -ne 0) }
    }
    $steady = @($samples | Select-Object -Skip 20)
    [pscustomobject]@{ executable=$Executable; firstWindowSeconds=($samples | Where-Object window | Select-Object -First 1).second; privateMiB=($steady.privateMiB | Measure-Object -Average).Average; workingMiB=($steady.workingMiB | Measure-Object -Average).Average; cpuSeconds=$samples[-1].cpuSeconds-$samples[20].cpuSeconds; samples=$samples } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $Output -Encoding utf8
} catch {
    $_ | Out-String | Set-Content -LiteralPath ($Output + '.error') -Encoding utf8
    throw
} finally {
    if (!$app.HasExited) { $app.CloseMainWindow() | Out-Null; if (!$app.WaitForExit(5000)) { $app.Kill() } }
}
