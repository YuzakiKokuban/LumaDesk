param([ValidateRange(10,300)][int]$Seconds = 150, [string]$Output = (Join-Path $PSScriptRoot '../artifacts/hotkeys-capture.jsonl'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Management
$outputPath = [IO.Path]::GetFullPath($Output)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($outputPath)) | Out-Null
$watcher = New-Object System.Management.ManagementEventWatcher
$watcher.Scope = New-Object System.Management.ManagementScope('\\.\ROOT\WMI')
$watcher.Query = New-Object System.Management.WqlEventQuery('SELECT * FROM AcpiTest_EventULong')
$watcher.Options.Timeout = [TimeSpan]::FromSeconds(1)
$writer = [IO.StreamWriter]::new($outputPath, $false, [Text.UTF8Encoding]::new($false))
$writer.AutoFlush = $true
try {
    $watcher.Start()
    Write-Output "READY: passive OEM event capture for $Seconds seconds; no hardware writes"
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        try { $event = $watcher.WaitForNextEvent() }
        catch [System.Management.ManagementException] {
            if ($_.Exception.ErrorCode -eq [System.Management.ManagementStatus]::Timedout) { continue }
            throw
        }
        $code = [uint32]$event.Properties['ULong'].Value
        $record = [ordered]@{ utc = [DateTime]::UtcNow.ToString('o'); code = $code; hex = ('0x{0:X8}' -f $code) }
        # Read only these known addresses once per notification. No scanning,
        # mode selection, radio changes or firmware writes occur in this tool.
        $probe = & python (Join-Path $PSScriptRoot '../reverse/native/probe_ec.py') 0x740 0x751 0x7ab 0x768
        if ($LASTEXITCODE -eq 0) { $record.ec = ($probe -join "`n" | ConvertFrom-Json) }
        $line = $record | ConvertTo-Json -Compress -Depth 8
        $writer.WriteLine($line)
        Write-Output $line
        $event.Dispose()
    }
} finally {
    $watcher.Stop()
    $watcher.Dispose()
    $writer.Dispose()
}
Write-Output "Capture saved: $outputPath"
