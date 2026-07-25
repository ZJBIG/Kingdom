param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path,
    [string]$UnityPath,
    [string]$LogPath = (Join-Path $ProjectPath "Logs/codex-reserialize-definitions.log")
)

$ErrorActionPreference = "Stop"
$finder = Join-Path $PSScriptRoot "find-unity.ps1"
if (-not $UnityPath) { $UnityPath = & $finder -ProjectPath $ProjectPath }
New-Item -ItemType Directory -Force (Split-Path $LogPath) | Out-Null

$unityArguments = @(
    "-batchmode", "-nographics", "-quit",
    "-projectPath", $ProjectPath,
    "-executeMethod", "Kingdom.EditorTools.DefinitionIdMigration.ReserializeFromCommandLine",
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
if ($process.ExitCode -ne 0 -or $log -notmatch "Definition reserialization passed") {
    Write-Error "Definition reserialization failed. ExitCode=$($process.ExitCode). Log=$LogPath"
    exit 1
}

$summary = Select-String -LiteralPath $LogPath -Pattern "Definition reserialization passed.*" |
    Select-Object -Last 1 -ExpandProperty Matches |
    ForEach-Object Value
Write-Host $summary
