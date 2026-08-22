param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path,
    [switch]$Build
)

$ErrorActionPreference = "Stop"

function Assert-Contains([string]$Path, [string]$Pattern, [string]$Message) {
    $content = Get-Content -LiteralPath (Join-Path $ProjectPath $Path) -Raw
    if ($content -notmatch $Pattern) {
        throw "FAIL: $Message ($Path)"
    }
    Write-Host "PASS: $Message"
}

function Assert-NotContains([string]$Path, [string]$Pattern, [string]$Message) {
    $content = Get-Content -LiteralPath (Join-Path $ProjectPath $Path) -Raw
    if ($content -match $Pattern) {
        throw "FAIL: $Message ($Path)"
    }
    Write-Host "PASS: $Message"
}

$music = Get-Content -LiteralPath (Join-Path $ProjectPath "Assets/Resources/Script/UI/KingdomUIRoot.Music.cs") -Raw
$refresh = [regex]::Match($music, '(?s)private void RefreshMusicPage\(\).*?(?=\r?\n    private static string FormatMusicTime)')
if (-not $refresh.Success) { throw "FAIL: RefreshMusicPage was not found" }
if ($refresh.Value -match 'RemoveAllListeners|AddListener\(') {
    throw "FAIL: RefreshMusicPage still mutates slider listeners"
}
Write-Host "PASS: RefreshMusicPage does not rebind slider listeners"

Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.SceneLayout.cs" `
    'musicProgressSlider\.onValueChanged\.AddListener\(musicProgressSeekHandler\)' `
    "music progress listener is bound during page construction"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.Music.cs" `
    '!musicProgressDragging\)' `
    "music progress is not written back while dragging"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.Music.cs" `
    'private MusicManager musicManagerCache' `
    "music manager lookup is cached"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.Music.cs" `
    'if \(musicManagerCache == null\)' `
    "music page does not scan the scene every refresh"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.SceneLayout.cs" `
    'musicProgressDragging = true' `
    "music progress drag start is tracked"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.SceneLayout.cs" `
    'musicProgressDragging = false' `
    "music progress drag end is tracked"

Assert-Contains "Assets/Resources/Script/Misc/Tool.cs" `
    '(?s)ToPairs\(.*?ref List<Pair<Resource, ExpantaNum>> cache' `
    "resource pair conversion has a cache overload"
Assert-Contains "Assets/Resources/Script/Data/Building.cs" `
    '(?s)ToPairs\(.*?ref resourceRequirementsCache' `
    "building resource views use cached pairs"
Assert-Contains "Assets/Resources/Script/Data/Research.cs" `
    '(?s)ToPairs\(.*?ref resourceRequirementsCache' `
    "research resource views use cached pairs"
Assert-Contains "Assets/Resources/Script/Data/SectorDefinition.cs" `
    '(?s)ToPairs\(.*?ref occupiedResourceRatesCache' `
    "sector resource views use cached pairs"

Assert-Contains "Assets/Resources/Script/Manager/SectorManager.cs" `
    'occupiedProductionBuffer\.Clear\(\)' `
    "occupied production uses a per-tick aggregation buffer"
Assert-Contains "Assets/Resources/Script/Manager/SectorManager.cs" `
    'foreach \(KeyValuePair<Resource, ExpantaNum> entry in occupiedProductionBuffer\)' `
    "occupied production applies one resource-manager update per resource"
Assert-Contains "Assets/Resources/Script/Manager/ResourceManager.cs" `
    'private sealed class TransactionWorkspace' `
    "atomic resource transactions use a reusable workspace"
Assert-Contains "Assets/Resources/Script/Manager/ResourceManager.cs" `
    'AcquireTransactionWorkspace\(\)' `
    "atomic transaction workspace acquisition is explicit"
Assert-Contains "Assets/Resources/Script/Manager/ResourceManager.cs" `
    'ReleaseTransactionWorkspace\(workspace\)' `
    "atomic transaction workspaces are released after callbacks"

Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.ResearchTree.cs" `
    'private void AbortResearchTreeBuild\(\)' `
    "research tree build has a failure-state reset"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.ResearchTree.cs" `
    'private void ClearResearchGeneratedVisuals\(\)' `
    "research tree retries clear generated visual objects"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.ResearchTree.cs" `
    'researchTreeBuildInProgress = false' `
    "research tree build cannot remain permanently marked in progress"

Assert-Contains "Kingdom.Runtime.csproj" `
    'Assets\\Resources\\Script\\UI\\KingdomUIRoot\.ResearchQueueGraphic\.cs' `
    "research queue graphic is permanently included in the runtime project"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.ResearchQueueGraphic.cs" `
    'ResearchQueueGraphic refreshMs=.*rebuild=' `
    "research queue graphic logs rebuild timing and object counts"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.ResearchQueueGraphic.cs" `
    'contentChildren=\{researchQueueContent\.childCount\}' `
    "research queue graphic logs content child count"

Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.cs" `
    'private bool resourceRowsBuilt' `
    "authored resource rows have a persistent build state"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.cs" `
    'private bool buildingRowsBuilt' `
    "authored building rows have a persistent build state"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.cs" `
    '!AreAuthoredRowsBuilt\(name\)' `
    "page row destruction is gated by the persistent build state"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.cs" `
    'private bool AreAuthoredRowsBuilt\(string name\)' `
    "page row reuse has one explicit structural gate"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.cs" `
    'bool pageHasCachedLayout' `
    "page switching distinguishes cached layouts from first construction"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.cs" `
    'bool researchTreeWasBuilt = researchTreePageBuilt' `
    "research page refresh remembers whether graph geometry is already cached"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.cs" `
    'if \(!researchTreeWasBuilt\)\s+Canvas\.ForceUpdateCanvases\(\)' `
    "cached research page does not force a full Canvas rebuild on every switch"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.cs" `
    'if \(name != "Research" \|\| !researchTreeWasBuilt\)' `
    "outer page layout refresh skips cached research transitions"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.cs" `
    'RefreshLayoutBounds\(!researchTreeWasBuilt\)' `
    "cached research transitions preserve graph position"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.cs" `
    'if \(!pageWasBuilt\)\s*\{' `
    "cached page transitions skip redundant RectTransform writes"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.cs" `
    'if \(pair\.Value\.gameObject\.activeSelf != shouldBeActive\)' `
    "page activation only writes when visibility changes"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.cs" `
    'if \(pageScroll\.content != pages\[name\]\)' `
    "page ScrollRect content is not rebound on an unchanged page"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.cs" `
    'bool pageWasBuilt = name == "Research"' `
    "cached pages retain their existing DataRows layout"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.cs" `
    'if \(!pageWasBuilt\)\s*\{' `
    "DataRows height is recalculated only when a page is built or rebuilt"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.ResearchTree.cs" `
    'selectedResearchPrerequisiteIdBuffer' `
    "research selection closure uses a persistent set buffer"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.ResearchTree.cs" `
    'visualReferences\.StateVersion != stateVersion' `
    "research node text refresh is gated by state version"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.ResearchQueueGraphic.cs" `
    'researchQueueDefinitionBuffer' `
    "research queue definitions use a persistent list buffer"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.ResearchQueueGraphic.cs" `
    'StateVersion = state == null \? -1 : state.Version' `
    "research queue visuals retain state version metadata"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.Quantity.cs" `
    'EnsureMinimumBuildQuantity' `
    "building Max and Custom quantities have a minimum display/request value"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.Quantity.cs" `
    'amount < ExpantaNum.One.*ExpantaNum.One.*amount' `
    "insufficient build quantity displays x1 instead of x0"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.Quantity.cs" `
    'return deconstruct \? selected : EnsureMinimumBuildQuantity\(selected\)' `
    "deconstruct quantity is not forced to one by the build display fix"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.AuthoredRows.cs" `
    'SetRowText\(row, "Effect", FormatBuildingEfficiency\(state\)' `
    "building rows bind the current efficiency column"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.AuthoredRows.cs" `
    'SetRowText\(row, "TechLevel", building\.TechLevel\.GetDescription\(\)\)' `
    "building rows bind the TechLevel column"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.AuthoredRows.cs" `
    'state\.Efficiency' `
    "building Effect uses BuildingState efficiency"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.LiveRefresh.cs" `
    'buildingEffectLabels' `
    "building efficiency labels are cached for live refresh"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.DetailPanel.cs" `
    'ShowBuildingDetails\(Building building, bool preserveScrollPosition = false\)' `
    "building detail rebuilds can preserve scroll position"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.DetailPanel.cs" `
    'ShowBuildingDetails\(building, true\)' `
    "live building detail fallback preserves scroll position"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.LiveRefresh.cs" `
    'FormatBuildingEfficiency\(state\)' `
    "building efficiency is refreshed with live state"
Assert-Contains "Assets/Resources/UI/Kingdom/KingdomUIBuildingCard.prefab" `
    'm_Name: Effect' `
    "building card prefab permanently contains the Effect column"
Assert-Contains "Assets/Resources/UI/Kingdom/KingdomUIBuildingCard.prefab" `
    'm_Name: TechLevel' `
    "building card prefab permanently contains the TechLevel column"
Assert-Contains "Assets/Resources/UI/Kingdom/KingdomUIBuildingCard.prefab" `
    'm_AnchorMin: \{x: 0\.(40|43|46), y: 0\}' `
    "building card Amount column has a dedicated layout region"
Assert-Contains "Assets/Resources/UI/Kingdom/KingdomUIBuildingCard.prefab" `
    'm_AnchorMin: \{x: 0\.24, y: 0\}' `
    "building card TechLevel column starts farther left"
Assert-Contains "Assets/Resources/UI/Kingdom/KingdomUIBuildingCard.prefab" `
    'm_AnchorMax: \{x: 0\.39, y: 1\}' `
    "building card TechLevel column is widened"
Assert-Contains "Assets/Resources/UI/Kingdom/KingdomUIBuildingCard.prefab" `
    'm_AnchorMin: \{x: 0\.(5|50|56), y: 0\}' `
    "building card Effect column has a dedicated layout region"
Assert-Contains "Assets/Resources/UI/Kingdom/KingdomUIBuildingCard.prefab" `
    'm_fontSize: 34' `
    "building card information columns use the unified font size"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.SceneLayout.cs" `
    'topPlayPause\.gameObject\.SetActive\(false\)' `
    "music page removes the top standalone play button"
Assert-Contains "Assets/Resources/UI/Kingdom/KingdomUIRoot.prefab" `
    'guid: a406d1b4f7c47dbea708192a4b5c6d78' `
    "music previous icon is authored in the scene"
Assert-Contains "Assets/Resources/UI/Kingdom/KingdomUIRoot.prefab" `
    'guid: b517e2c508d58efbf8192a4b5c6d7e8f' `
    "music next icon is authored in the scene"
Assert-Contains "Assets/Resources/UI/Kingdom/KingdomUIRoot.prefab" `
    'm_Name: Pause' `
    "music pause control is authored in the scene"
Assert-Contains "Assets/Resources/UI/Kingdom/KingdomUIRoot.prefab" `
    'guid: 93f5c0a3e6b36cad9d6f708192a4b5c9' `
    "music pause control uses the permanent stop icon"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.SceneLayout.cs" `
    'row\.enabled = false' `
    "music rows no longer provide row-level button behavior"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.SceneLayout.cs" `
    'ConfigureMusicTrackIcon\(playPause, "play"\)' `
    "music rows provide a play pause icon button"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.SceneLayout.cs" `
    'musicTrackButtons\[track\.Id\] = playPause' `
    "music icon refresh tracks the row play pause button"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.SceneLayout.cs" `
    'ConfigureMusicTrackColumn\(rowObject, "Length"' `
    "music rows retain the length column"
Assert-NotContains "Assets/Resources/UI/Kingdom/KingdomUIMusicTrack.prefab" `
    'm_text: Type|&1000031' `
    "music row play pause controls have no obsolete text component"
Assert-NotContains "Assets/Resources/Script/UI/KingdomUIRoot.SceneLayout.cs" `
    'ConfigureMusicTrackColumn\(rowObject, "Type"' `
    "music rows no longer display the category column"
Assert-Contains "Assets/Resources/UI/Kingdom/KingdomUIMusicTrack.prefab" `
    'm_Name: PlayPause' `
    "music row prefab names its action column PlayPause"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.Music.cs" `
    'string iconName = selected && manager\.IsPermanentlyStopped' `
    "playing music rows switch to the pause icon"
Assert-Contains "Assets/Resources/Script/Manager/MusicManager.cs" `
    'IsPermanentlyStopped' `
    "music manager exposes the permanent stop state"
Assert-Contains "Assets/Resources/Script/UI/KingdomUIRoot.Music.cs" `
    'selected && manager\.IsPermanentlyStopped' `
    "music rows display the stop icon after permanent pause"

if ($Build) {
    dotnet build (Join-Path $ProjectPath "Kingdom.sln") --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "FAIL: dotnet build exited with code $LASTEXITCODE"
    }
}

Write-Host "Performance-fix validation passed."
