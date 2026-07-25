param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path,
    [string]$UnityPath,
    [string]$LogPath = (Join-Path $ProjectPath "Logs/codex-compile.log")
)
$ErrorActionPreference = "Stop"
if (-not $UnityPath) {
    $UnityPath = & (Join-Path $PSScriptRoot "find-unity.ps1") -ProjectPath $ProjectPath
}
New-Item -ItemType Directory -Force (Split-Path $LogPath) | Out-Null

$arguments = @(
    "-batchmode",
    "-nographics",
    "-quit",
    "-projectPath", $ProjectPath,
    "-logFile", $LogPath
)
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -Wait -PassThru -NoNewWindow
$log = if (Test-Path $LogPath) { Get-Content $LogPath -Raw } else { "" }
$compileErrors = $log -match "error CS\d+|Compilation failed|Scripts have compiler errors|Aborting batchmode due to failure"
if ($process.ExitCode -ne 0 -or $compileErrors) {
    Write-Error "Unity compile failed. ExitCode=$($process.ExitCode). Log=$LogPath"
    exit 1
}
Write-Host "Unity compile passed. ExitCode=$($process.ExitCode). Log=$LogPath"
