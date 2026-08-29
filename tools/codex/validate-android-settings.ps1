param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
)
$ErrorActionPreference = "Stop"
$settingsPath = Join-Path $ProjectPath "ProjectSettings/ProjectSettings.asset"
if (-not (Test-Path -LiteralPath $settingsPath)) {
    Write-Error "Missing Unity project settings: $settingsPath"
    exit 1
}

$settings = Get-Content -LiteralPath $settingsPath -Raw
$checks = @(
    @{ Name = "bundleVersion"; Pattern = "(?m)^  bundleVersion: 0\.5\s*$" },
    @{ Name = "AndroidMinSdkVersion"; Pattern = "(?m)^  AndroidMinSdkVersion: 22\s*$" },
    @{ Name = "AndroidTargetSdkVersion"; Pattern = "(?m)^  AndroidTargetSdkVersion: 35\s*$" },
    @{ Name = "AndroidTargetArchitectures ARM64"; Pattern = "(?m)^  AndroidTargetArchitectures: 2\s*$" },
    @{ Name = "Android scripting backend IL2CPP"; Pattern = "(?ms)^  scriptingBackend:\s*\r?\n    Android: 1\s*$" }
)
$missing = @($checks | Where-Object { $settings -notmatch $_.Pattern })
if ($missing.Count -gt 0) {
    Write-Error ("Android settings mismatch:`n" + (($missing | ForEach-Object Name) -join "`n"))
    exit 1
}
Write-Host "Android settings pass: IL2CPP, ARM64, minSdk 22, targetSdk 35, bundleVersion 0.5"
