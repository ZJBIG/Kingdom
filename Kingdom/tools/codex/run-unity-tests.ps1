param(
    [ValidateSet("EditMode", "PlayMode")]
    [string]$Platform = "EditMode",
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path,
    [string]$UnityPath,
    [string]$ResultsPath,
    [string]$LogPath
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

$arguments = @(
    "-batchmode",
    "-nographics",
    "-projectPath", $ProjectPath,
    "-runTests",
    "-testPlatform", $Platform,
    "-testResults", $ResultsPath,
    "-logFile", $LogPath
)
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -Wait -PassThru -NoNewWindow
if ($process.ExitCode -ne 0 -or -not (Test-Path $ResultsPath)) {
    Write-Error "Unity $Platform tests failed or produced no XML. ExitCode=$($process.ExitCode). Log=$LogPath"
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
