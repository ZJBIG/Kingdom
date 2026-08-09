param(
    [ValidateSet("EditMode", "PlayMode")]
    [string]$Platform = "EditMode",
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path,
    [string]$UnityPath,
    [string]$ResultsPath,
    [string]$LogPath,
    [int]$TimeoutSeconds = 600
)
$ErrorActionPreference = "Stop"
if (-not $UnityPath) {
    $UnityPath = & (Join-Path $PSScriptRoot "find-unity.ps1") -ProjectPath $ProjectPath
}
if (-not $ResultsPath) {
    $ResultsPath = Join-Path $ProjectPath "TestResults/$Platform-results.xml"
}
if (-not $LogPath) {
    $LogPath = Join-Path $ProjectPath "Logs/codex-$($Platform.ToLower())-tests.log"
}
New-Item -ItemType Directory -Force (Split-Path $ResultsPath) | Out-Null
New-Item -ItemType Directory -Force (Split-Path $LogPath) | Out-Null
Remove-Item -LiteralPath $ResultsPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $LogPath -Force -ErrorAction SilentlyContinue

$arguments = @(
    "-batchmode",
    "-nographics",
    "-projectPath", $ProjectPath,
    "-runTests",
    "-testPlatform", $Platform,
    "-testResults", $ResultsPath,
    "-logFile", $LogPath
)
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments `
    -PassThru -WindowStyle Hidden
$completed = $process.WaitForExit($TimeoutSeconds * 1000)
if (-not $completed) {
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    Write-Error "Unity $Platform tests timed out after $TimeoutSeconds seconds. Log=$LogPath"
    exit 1
}
$unityExitCode = $process.ExitCode
if ($unityExitCode -ne 0 -or -not (Test-Path $ResultsPath)) {
    Write-Error "Unity $Platform tests failed or produced no XML. ExitCode=$unityExitCode. Log=$LogPath"
    exit 1
}

[xml]$xml = Get-Content $ResultsPath
$run = $xml.'test-run'
$failed = [int]$run.failed
$total = [int]$run.total
if ($total -eq 0) {
    Write-Error "Unity $Platform runner completed with zero test cases. This is not acceptance evidence. Results=$ResultsPath"
    exit 1
}
if ($failed -gt 0) {
    Write-Error "$failed of $total $Platform tests failed. Results=$ResultsPath"
    exit 1
}
Write-Host "$Platform tests passed. Total=$total Results=$ResultsPath Log=$LogPath"
