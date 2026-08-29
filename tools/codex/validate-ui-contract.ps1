param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
)

$ErrorActionPreference = "Stop"
$prefabPath = Join-Path $ProjectPath "Assets/Resources/UI/Kingdom/KingdomUIRoot.prefab"
$buildSettingsPath = Join-Path $ProjectPath "ProjectSettings/EditorBuildSettings.asset"
foreach ($path in @($prefabPath, $buildSettingsPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "UI contract input is missing: $path"
    }
}

$prefab = Get-Content -LiteralPath $prefabPath -Raw
$checks = [ordered]@{
    "ScaleWithScreenSize" = '(?m)^  m_UiScaleMode: 1\s*$'
    "ReferenceResolution" = '(?m)^  m_ReferenceResolution: \{x: 2640, y: 1200\}\s*$'
    "MatchWidth" = '(?m)^  m_ScreenMatchMode: 0\s*$'
    "MatchWidthValue" = '(?m)^  m_MatchWidthOrHeight: 0\s*$'
}
$missing = @($checks.GetEnumerator() | Where-Object { $prefab -notmatch $_.Value } | ForEach-Object Key)
if ($missing.Count -gt 0) {
    throw "UI contract mismatch: $($missing -join ', ')"
}

$buildSettings = Get-Content -LiteralPath $buildSettingsPath -Raw
if ($buildSettings -notmatch '(?ms)- enabled: 1\s*\r?\n\s*path: Assets/Scenes/SampleScene\.unity') {
    throw "Primary SampleScene is not enabled in EditorBuildSettings."
}

Write-Host "UI contract pass: ScaleWithScreenSize, 2640x1200, Match Width, SampleScene enabled"
