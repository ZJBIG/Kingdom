param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path,
    [string]$UnityPath,
    [string]$LogPath = (Join-Path $ProjectPath "Logs/codex-compile.log"),
    [int]$TimeoutSeconds = 180
)
$ErrorActionPreference = "Stop"
if (-not $UnityPath) {
    $UnityPath = & (Join-Path $PSScriptRoot "find-unity.ps1") -ProjectPath $ProjectPath
}
New-Item -ItemType Directory -Force (Split-Path $LogPath) | Out-Null
Remove-Item -LiteralPath $LogPath -Force -ErrorAction SilentlyContinue

$arguments = @(
    "-batchmode",
    "-nographics",
    "-quit",
    "-projectPath", $ProjectPath,
    "-logFile", $LogPath
)
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments `
    -PassThru -WindowStyle Hidden
$completed = $process.WaitForExit($TimeoutSeconds * 1000)
if (-not $completed) {
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    Write-Error "Unity compile timed out after $TimeoutSeconds seconds. Log=$LogPath"
    exit 1
}
$unityExitCode = $process.ExitCode
$log = if (Test-Path $LogPath) { Get-Content $LogPath -Raw } else { "" }
$compileErrors = $log -match "error CS\d+|Compilation failed|Scripts have compiler errors|Aborting batchmode due to failure"
if ($unityExitCode -ne 0 -or $compileErrors) {
    Write-Error "Unity compile failed. ExitCode=$unityExitCode. Log=$LogPath"
    exit 1
}
Write-Host "Unity compile passed. ExitCode=$unityExitCode. Log=$LogPath"
