param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path,
    [string]$UnityPath,
    [string]$LogPath = (Join-Path $ProjectPath "Logs/codex-sync-solution.log")
)

$ErrorActionPreference = "Stop"
$finder = Join-Path $PSScriptRoot "find-unity.ps1"
if (-not $UnityPath) { $UnityPath = & $finder -ProjectPath $ProjectPath }
New-Item -ItemType Directory -Force (Split-Path $LogPath) | Out-Null

$unityArguments = @(
    "-batchmode", "-nographics", "-quit",
    "-projectPath", $ProjectPath,
    "-executeMethod", "Kingdom.EditorTools.SolutionSync.Run",
    "-logFile", $LogPath
)
$argumentText = ($unityArguments | ForEach-Object {
    if ($_ -match '[\s"]') { '"' + $_.Replace('"', '\"') + '"' } else { $_ }
}) -join ' '
$startInfo = New-Object System.Diagnostics.ProcessStartInfo
$startInfo.FileName = $UnityPath
$startInfo.Arguments = $argumentText
$startInfo.UseShellExecute = $false
$process = [System.Diagnostics.Process]::Start($startInfo)
$process.WaitForExit()

$log = if (Test-Path $LogPath) { Get-Content -LiteralPath $LogPath -Raw } else { "" }
$syncFailed = $log -match "executeMethod class.*could not be found|executeMethod method.*could not be found|error CS\d+|Compilation failed|Scripts have compiler errors"
if ($process.ExitCode -ne 0 -or $syncFailed) {
    Write-Error "Unity solution sync failed. ExitCode=$($process.ExitCode). Log=$LogPath"
    exit 1
}

Write-Host "Unity solution sync passed. Log=$LogPath"
