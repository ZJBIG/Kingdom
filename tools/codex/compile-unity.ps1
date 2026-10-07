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
    -WorkingDirectory $ProjectPath -PassThru -WindowStyle Hidden
$completed = $process.WaitForExit($TimeoutSeconds * 1000)
if (-not $completed) {
    $lockDiagnostics = @(
        (Join-Path $ProjectPath "Library/ArtifactDB-lock"),
        (Join-Path $ProjectPath "Library/SourceAssetDB-lock")
    ) | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object {
        $lock = Get-Item -LiteralPath $_
        "$($lock.Name)=$($lock.LastWriteTime.ToString('o'))"
    }
    $logState = if (Test-Path -LiteralPath $LogPath) {
        "exists bytes=$((Get-Item -LiteralPath $LogPath).Length)"
    } else {
        "missing"
    }
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    $locks = if ($lockDiagnostics.Count) { $lockDiagnostics -join "; " } else { "none" }
    Write-Error "Unity compile timed out after $TimeoutSeconds seconds. Log=$LogPath ($logState). Library locks: $locks"
    exit 1
}
$unityExitCode = $process.ExitCode
$logExists = Test-Path -LiteralPath $LogPath -PathType Leaf
$log = if ($logExists) { Get-Content $LogPath -Raw } else { "" }
$logHasContent = $logExists -and -not [string]::IsNullOrWhiteSpace($log)
$compileErrors = $log -match "error CS\d+|Compilation failed|Scripts have compiler errors|Aborting batchmode due to failure"
if ($unityExitCode -ne 0 -or -not $logHasContent -or $compileErrors) {
    $logState = if ($logExists) { "present bytes=$((Get-Item -LiteralPath $LogPath).Length)" } else { "missing" }
    if (-not $logHasContent) {
        Write-Error "Unity compile failed because the Unity log is missing or empty. ExitCode=$unityExitCode. Log=$LogPath ($logState)"
        exit 1
    }
    Write-Error "Unity compile failed. ExitCode=$unityExitCode. Log=$LogPath"
    exit 1
}
Write-Host "Unity compile passed. ExitCode=$unityExitCode. Log=$LogPath"
