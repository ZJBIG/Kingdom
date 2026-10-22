[CmdletBinding(SupportsShouldProcess = $true)]
param([switch]$Apply)

$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!(Test-Path -LiteralPath (Join-Path $workspace 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'Run this script from its original Kingdom tools/codex location.'
}

# Reviewed temporary outputs, retired authoring artifacts and regenerable caches.
$relativePaths = @(
    '.codex/tmp/art-originals',
    '.codex/tmp/art-review',
    '.codex/tmp/optimize-art.py',
    '.codex/tmp/prune-finished-work.ps1',
    '.codex/tmp/art-optimization.json',
    '.codex/tmp/art-unity-pid.txt',
    '.codex/tmp/unused-art-candidates.json',
    '.codex/tmp/final-cleanup-stats.json',
    '.codex/tmp/android-size-before512.zip',
    '.codex/tmp/apk-build-inputs.zip',
    '.codex/tmp/remove-stale-git-lock.ps1',
    '.codex/tmp/sdkmanager-original.bat',
    'Assets/Editor/ArtworkOptimizationAuthor.cs',
    'Assets/Editor/ArtworkOptimizationAuthor.cs.meta',
    'Assets/Editor/KingdomOnboardingAuthoring.cs',
    'Assets/Editor/KingdomOnboardingAuthoring.cs.meta',
    'data/art-optimization-20261022',
    'data/metal-consolidation-20261014/archive-original-consolidation',
    'data/workshop-art-20261022/compile-preview.log',
    'data/workshop-art-20261022/import-check.txt',
    'data/workshop-art-20261022/unity-exit.txt',
    'data/apk-clean-build-20261022/build-audio32k.log',
    'data/apk-clean-build-20261022/build-audio32k-proxy.log',
    'data/apk-clean-build-20261022/build-audio32k-final.log',
    'data/apk-clean-build-20261022/sdk-list.txt',
    'Library/Bee',
    'Library/BurstCache',
    'Library/LastBuild.buildreport',
    'Library/BuildCache',
    'Library/BuildPlayerData',
    'Library/PlayerDataCache',
    'Library/SplashScreenCache',
    'Library/TempArtifacts',
    'Library/com.unity.addressables',
    'Temp',
    'Builds/Android/Kingdom_BurstDebugInformation_DoNotShip',
    'Assets/AddressableAssetsData/Windows',
    'Assets/AddressableAssetsData/Windows.meta',
    'Logs',
    'SafeNet Sentinel',
    'tools/NewEconomySimulator/bin',
    'tools/NewEconomySimulator/obj',
    '.codex/package-recovery',
    'TestResults',
    'Assets/TextMesh Pro/Documentation',
    'Assets/TextMesh Pro/Documentation.meta',
    'Assets/UI Toolkit',
    'Assets/UI Toolkit.meta',
    'Assets/Settings/Lit2DSceneTemplate.scenetemplate',
    'Assets/Settings/Lit2DSceneTemplate.scenetemplate.meta',
    'Assets/Settings/Scenes',
    'Assets/Settings/Scenes.meta',
    'Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Drop Shadow.mat',
    'Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Drop Shadow.mat.meta',
    'Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat',
    'Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat.meta',
    '.workbuddy-ai/memory',
    '.codex/archive/2026-10-22-document-cleanup.zip'
)

$targets = @()
$bytes = [long]0
$fileCount = 0
foreach ($relative in $relativePaths) {
    $absolute = [IO.Path]::GetFullPath((Join-Path $workspace $relative))
    if (!$absolute.StartsWith($workspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path outside workspace: $absolute"
    }
    $ancestor = $absolute
    while ($ancestor -and $ancestor.Length -ge $workspace.Length) {
        if (Test-Path -LiteralPath $ancestor) {
            $entry = Get-Item -LiteralPath $ancestor -Force
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Reparse point: $ancestor"
            }
        }
        if ($ancestor -eq $workspace) { break }
        $ancestor = Split-Path -Parent $ancestor
    }
    if (!(Test-Path -LiteralPath $absolute)) { continue }
    $entry = Get-Item -LiteralPath $absolute -Force
    $entries = @($entry)
    if ($entry.PSIsContainer) {
        $entries += @(Get-ChildItem -LiteralPath $absolute -Recurse -Force)
    }
    if ($entries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) {
        throw "Reparse point inside: $absolute"
    }
    $files = @($entries | Where-Object { !$_.PSIsContainer })
    if ($files | Where-Object { $_.Extension -eq '.patch' }) {
        throw "Unique patch inside target: $absolute"
    }
    if ($relative -eq 'SafeNet Sentinel' -and ($files | Where-Object { $_.Extension -ne '.log' })) {
        throw 'Unexpected license-provider data; only reviewed log files may be removed.'
    }
    if ($relative -eq 'Assets/AddressableAssetsData/Windows' -and
        ($files | Where-Object { $_.Name -notin @('addressables_content_state.bin', 'addressables_content_state.bin.meta') })) {
        throw 'Unexpected asset inside retired Windows Addressables build state.'
    }
    if ($relative -in @('TestResults', '.codex/package-recovery') -and $files.Count) {
        throw "Expected empty directory now contains files: $absolute"
    }
    $size = ($files | Measure-Object -Property Length -Sum).Sum
    $bytes += [long]$size
    $fileCount += $files.Count
    $targets += $absolute
    Write-Output ('{0}  ({1:N2} MiB)' -f $absolute, ($size / 1MB))
}
Write-Output ('Total: {0} files, {1:N2} MiB' -f $fileCount, ($bytes / 1MB))
if (!$Apply) {
    Write-Output 'Preview only. Add -Apply to delete these exact temporary files.'
    return
}

$activeRelativePaths = @($relativePaths | Where-Object { Test-Path -LiteralPath (Join-Path $workspace $_) })
$reviewedSources = @($activeRelativePaths | Where-Object { $_.StartsWith('Assets/') -or $_ -eq '.workbuddy-ai/memory' })
if ($reviewedSources.Count) {
    $sourceEdits = @(git -C $workspace status --porcelain -- $reviewedSources)
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect source worktree state.' }
    if ($sourceEdits | Where-Object { $_ -notmatch '^ D ' }) {
        throw 'A reviewed source target now has local edits; preserve it for review.'
    }
}
if ($activeRelativePaths.Count) {
    git -C $workspace diff --cached --quiet -- $activeRelativePaths
    if ($LASTEXITCODE -ne 0) { throw 'Staged target changes or Git inspection failure; cleanup stopped.' }
}

# Do not delete an Editor source while Unity may be compiling this project.
$editors = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'")
foreach ($editor in $editors) {
    if (!$editor.CommandLine -or $editor.CommandLine.IndexOf($workspace, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $editor.CommandLine.IndexOf($workspace.Replace('\', '/'), [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw 'Close the Kingdom Unity Editor before applying this cleanup. No process was stopped.'
    }
}
foreach ($absolute in $targets) {
    if ($PSCmdlet.ShouldProcess($absolute, 'Delete artwork optimization temporary files')) {
        Remove-Item -LiteralPath $absolute -Recurse -Force
    }
}
if ($WhatIfPreference) { return }
$remaining = @($targets | Where-Object { Test-Path -LiteralPath $_ })
if ($remaining.Count) { throw "Cleanup incomplete: $($remaining -join ', ')" }
foreach ($relative in @('.codex/tmp', '.codex/archive')) {
    $absolute = [IO.Path]::GetFullPath((Join-Path $workspace $relative))
    if (!$absolute.StartsWith($workspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Empty directory outside workspace: $absolute"
    }
    if (Test-Path -LiteralPath $absolute) {
        $entry = Get-Item -LiteralPath $absolute -Force
        $parent = Get-Item -LiteralPath (Split-Path -Parent $absolute) -Force
        if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
            ($parent.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Reparse directory: $absolute" }
        if (!(Get-ChildItem -LiteralPath $absolute -Force) -and $PSCmdlet.ShouldProcess($absolute, 'Remove empty temporary directory')) {
            Remove-Item -LiteralPath $absolute -Force
            if (Test-Path -LiteralPath $absolute) { throw "Empty directory remains: $absolute" }
        }
    }
}
Write-Output 'PhysicalPostScanRemaining=0'
