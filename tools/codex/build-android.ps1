param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path,
    [string]$UnityPath,
    [int]$TimeoutSeconds = 1800,
    [string]$LogPath = (Join-Path $ProjectPath "Logs/codex-android-build.log")
)

$ErrorActionPreference = "Stop"
if (-not $UnityPath) {
    $UnityPath = & (Join-Path $PSScriptRoot "find-unity.ps1") -ProjectPath $ProjectPath
}
$editorInstancePath = Join-Path $ProjectPath "Library/EditorInstance.json"
if (Test-Path -LiteralPath $editorInstancePath) {
    $lockConflict = $null
    try {
        $editorInstance = Get-Content -LiteralPath $editorInstancePath -Raw | ConvertFrom-Json
        $existingUnity = Get-Process -Id ([int]$editorInstance.process_id) -ErrorAction SilentlyContinue
        if ($existingUnity -and $existingUnity.ProcessName -eq "Unity") {
            $lockConflict = "Unity project is already open by PID $($editorInstance.process_id). Continue other work instead of waiting for the project lock."
        }
    }
    catch {
        Write-Warning "Could not inspect Unity project lock metadata: $($_.Exception.Message)"
    }
    if ($lockConflict) {
        throw $lockConflict
    }
}

& (Join-Path $PSScriptRoot "validate-android-settings.ps1") -ProjectPath $ProjectPath
& (Join-Path $PSScriptRoot "validate-ui-contract.ps1") -ProjectPath $ProjectPath
& (Join-Path $PSScriptRoot "verify-yaml-references.ps1") -ProjectPath $ProjectPath

$outputPath = Join-Path $ProjectPath "Builds/Android/Kingdom.apk"
New-Item -ItemType Directory -Force (Split-Path $LogPath) | Out-Null
Remove-Item -LiteralPath $LogPath -Force -ErrorAction SilentlyContinue

$arguments = @(
    "-batchmode",
    "-nographics",
    "-quit",
    "-projectPath", $ProjectPath,
    "-executeMethod", "KingdomBuild.BuildAndroid",
    "-buildTarget", "Android",
    "-logFile", $LogPath
)
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments `
    -WorkingDirectory $ProjectPath -PassThru -WindowStyle Hidden
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    throw "Unity Android build timed out after $TimeoutSeconds seconds. Log=$LogPath"
}

$log = if (Test-Path -LiteralPath $LogPath) { Get-Content -LiteralPath $LogPath -Raw } else { "" }
$hasBuildErrors = $log -match "error CS\d+|Build failed|Scripts have compiler errors|Aborting batchmode|The referenced script on this Behaviour is missing|Missing (font|resource)"
if ($process.ExitCode -ne 0 -or $hasBuildErrors) {
    throw "Unity Android build failed. ExitCode=$($process.ExitCode). Log=$LogPath"
}
if (-not (Test-Path -LiteralPath $outputPath -PathType Leaf) -or
    (Get-Item -LiteralPath $outputPath).Length -le 0) {
    throw "Unity reported success but APK was not created: $outputPath"
}

Write-Host "Android build passed: $outputPath ($((Get-Item -LiteralPath $outputPath).Length) bytes)"
