param(
    [string]$Path = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path "Temp/KingdomPerf.log"),
    [double]$MinimumMinutes = 10,
    [double]$MaximumIdleRatio = 2.0,
    [double]$MaximumMemoryGrowthMB = 50
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "FAIL: performance log not found: $Path"
}

$resolvedPath = (Resolve-Path -LiteralPath $Path).Path
$lines = @(Get-Content -LiteralPath $resolvedPath)
if ($lines.Count -eq 0) {
    throw "FAIL: performance log is empty: $resolvedPath"
}

$sessionStartIndex = -1
for ($i = $lines.Count - 1; $i -ge 0; $i--) {
    if ($lines[$i] -match '\[KingdomPerf\] SessionStart ') {
        $sessionStartIndex = $i
        break
    }
}
if ($sessionStartIndex -lt 0) {
    throw "FAIL: performance log has no [KingdomPerf] SessionStart marker"
}
$sessionLines = @($lines[$sessionStartIndex..($lines.Count - 1)])
Write-Host "Using latest session starting at log line $($sessionStartIndex + 1)"

$timestamps = @(
    $sessionLines | ForEach-Object {
        if ($_ -match '^(?<timestamp>\d{4}-\d{2}-\d{2}T[^ ]+) ') {
            try { [DateTime]::Parse($Matches.timestamp) } catch { }
        }
    }
)
if ($timestamps.Count -lt 2) {
    throw "FAIL: performance log has fewer than two timestamped samples"
}

$durationMinutes = ($timestamps[-1] - $timestamps[0]).TotalMinutes
if ($durationMinutes -lt $MinimumMinutes) {
    throw "FAIL: log duration is $([Math]::Round($durationMinutes, 2)) minutes; required at least $MinimumMinutes minutes"
}
Write-Host "PASS: log duration=$([Math]::Round($durationMinutes, 2)) minutes"

$tickLines = @($sessionLines | Where-Object { $_ -match '\[KingdomPerf\] ManualTickStats ' })
$frameLines = @($sessionLines | Where-Object { $_ -match '\[KingdomPerf\] FrameStats ' })
$memoryLines = @($sessionLines | Where-Object { $_ -match '\[KingdomPerf\] Memory ' })
if ($tickLines.Count -lt 3) { throw "FAIL: only $($tickLines.Count) ManualTickStats samples" }
if ($frameLines.Count -lt 3) { throw "FAIL: only $($frameLines.Count) FrameStats samples" }
if ($memoryLines.Count -lt 2) { throw "FAIL: only $($memoryLines.Count) Memory samples" }
Write-Host "PASS: samples tick=$($tickLines.Count) frame=$($frameLines.Count) memory=$($memoryLines.Count)"

$frameStats = @($frameLines | ForEach-Object {
    if ($_ -match 'FrameStats\s+samples=(?<samples>\d+)\s+avg=(?<avg>[\d.]+)ms\s+max=(?<max>[\d.]+)ms\s+slow20=(?<slow20>\d+).*?page=(?<page>\S+)') {
        [pscustomobject]@{
            Samples = [int]$Matches.samples
            Average = [double]$Matches.avg
            Maximum = [double]$Matches.max
            Slow20 = [int]$Matches.slow20
            Page = $Matches.page
        }
    }
})
$steadyFrames = @($frameStats | Where-Object { $_.Samples -ge 100 })
if ($steadyFrames.Count -lt 2) {
    throw "FAIL: fewer than two steady-state FrameStats windows with at least 100 frames"
}
$page = $steadyFrames[-1].Page
$samePage = @($steadyFrames | Where-Object { $_.Page -eq $page })
if ($samePage.Count -lt 2) {
    throw "FAIL: fewer than two steady-state windows on the final page '$page'"
}
$early = @($samePage | Select-Object -First ([Math]::Min(3, $samePage.Count)))
$late = @($samePage | Select-Object -Last ([Math]::Min(3, $samePage.Count)))
$earlyAverage = [Math]::Round((($early | Measure-Object -Property Average -Average).Average), 2)
$lateAverage = [Math]::Round((($late | Measure-Object -Property Average -Average).Average), 2)
$idleRatio = if ($earlyAverage -gt 0) { [Math]::Round($lateAverage / $earlyAverage, 2) } else { 0 }
if ($idleRatio -ge $MaximumIdleRatio) {
    throw "FAIL: late idle frame average ratio=$idleRatio (early=$earlyAverage ms late=$lateAverage ms)"
}
Write-Host "PASS: idle trend page=$page earlyAvg=${earlyAverage}ms lateAvg=${lateAverage}ms ratio=$idleRatio"

$memoryMB = @($memoryLines | ForEach-Object {
    if ($_ -match '\[KingdomPerf\] Memory\s+(?<mb>[\d.]+)MB') { [double]$Matches.mb }
})
if ($memoryMB.Count -lt 2) { throw "FAIL: memory samples could not be parsed" }
$memoryGrowth = [Math]::Round($memoryMB[-1] - $memoryMB[0], 1)
if ($memoryGrowth -ge $MaximumMemoryGrowthMB) {
    throw "FAIL: memory growth=${memoryGrowth}MB (first=$($memoryMB[0])MB last=$($memoryMB[-1])MB)"
}
Write-Host "PASS: memory growth=${memoryGrowth}MB"

$rebuildLines = @($sessionLines | Where-Object { $_ -match 'BuildMusicPage|musicPageBuilt\s*=\s*false' })
if ($rebuildLines.Count -gt 0) {
    throw "FAIL: music page rebuild evidence found ($($rebuildLines.Count) lines)"
}
Write-Host "PASS: no music-page rebuild evidence"

Write-Host "Unity performance log validation passed."
