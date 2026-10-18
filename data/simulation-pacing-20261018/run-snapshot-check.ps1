param([string]$EvidenceName, [string]$TestFilter)
$ErrorActionPreference = "Stop"
if (Get-Process Unity -ErrorAction SilentlyContinue) { throw "Unity Editor is already running." }
$taskEvidence = $PSScriptRoot
$taskXml = Join-Path $taskEvidence ($EvidenceName + '.xml')
$taskLog = Join-Path $taskEvidence ($EvidenceName + '.log')
if (Test-Path -LiteralPath $taskXml) { throw "Evidence path already exists; preserve it." }
$taskOldEvidence = $env:KINGDOM_PARITY_SNAPSHOT_PATH
$env:KINGDOM_PARITY_SNAPSHOT_PATH = Join-Path $taskEvidence ($EvidenceName + '-input.json')
try {
    $taskProcess = Start-Process -FilePath 'D:\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe' -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-nographics','-projectPath','D:\GitHub\Kingdom','-runTests','-testPlatform','EditMode','-testFilter',$TestFilter,'-testResults',$taskXml,'-logFile',$taskLog)
} finally { $env:KINGDOM_PARITY_SNAPSHOT_PATH = $taskOldEvidence }
$taskProcess.Id | Set-Content (Join-Path $taskEvidence ($EvidenceName + '-pid.txt'))
$taskWatch = [Diagnostics.Stopwatch]::StartNew()
$taskXmlAt = $null
while (-not $taskProcess.WaitForExit(1000)) {
    if ((Test-Path -LiteralPath $taskXml) -and $null -eq $taskXmlAt) { $taskXmlAt = $taskWatch.Elapsed.TotalSeconds }
    if ($taskWatch.Elapsed.TotalSeconds -gt 300 -or ($null -ne $taskXmlAt -and $taskWatch.Elapsed.TotalSeconds - $taskXmlAt -gt 60)) {
        Stop-Process -Id $taskProcess.Id
        'timeout; stopped task PID; runner exit1' | Set-Content (Join-Path $taskEvidence ($EvidenceName + '-status.txt'))
        exit 1
    }
}
$taskProcess.Refresh()
"runner exit$($taskProcess.ExitCode)" | Set-Content (Join-Path $taskEvidence ($EvidenceName + '-status.txt'))
exit $taskProcess.ExitCode
