[CmdletBinding()]
param(
    [string]$ProjectRoot = '',
    [string]$ReportPath = '',
    [switch]$IndustrialBaseline
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Join-Path (Split-Path -Parent $PSScriptRoot) '..'
}
$project = (Resolve-Path $ProjectRoot).Path
$assetsRoot = Join-Path $project 'Assets'
if (-not (Test-Path (Join-Path $assetsRoot 'Resources\Datas')) -and
    (Test-Path (Join-Path $project 'Kingdom\Assets\Resources\Datas'))) {
    $project = (Resolve-Path (Join-Path $project 'Kingdom')).Path
    $assetsRoot = Join-Path $project 'Assets'
}
if (-not (Test-Path (Join-Path $assetsRoot 'Resources\Datas'))) {
    throw "Unity Assets/Resources/Datas was not found under $project"
}

$guidToDefinition = @{}
$definitions = New-Object System.Collections.Generic.List[object]

function Get-Section([string]$text, [string]$name) {
    $pattern = '(?ms)^  ' + [regex]::Escape($name) + ':\s*\r?\n(.*?)(?=^  [A-Za-z][A-Za-z0-9_]*:\s*|\z)'
    $match = [regex]::Match($text, $pattern)
    if ($match.Success) { return $match.Groups[1].Value }
    return ''
}

function Get-IdsFromSection([string]$section, [string]$kind) {
    $result = New-Object System.Collections.Generic.List[string]
    foreach ($match in [regex]::Matches($section, 'guid:\s*([0-9a-f]{32})', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
        $guid = $match.Groups[1].Value
        if ($guidToDefinition.ContainsKey($guid) -and $guidToDefinition[$guid].Kind -eq $kind) {
            [void]$result.Add($guidToDefinition[$guid].Id)
        }
    }
    return @($result)
}

function Get-Number([string]$text, [string]$name, [double]$default = 0) {
    $direct = [regex]::Match($text, '(?m)^  ' + [regex]::Escape($name) + ':\s*([-+0-9.eE]+)')
    if ($direct.Success) { return [double]::Parse($direct.Groups[1].Value, [Globalization.CultureInfo]::InvariantCulture) }
    return $default
}

function Get-Definition([string]$path, [string]$kind) {
    $text = Get-Content -Raw -LiteralPath $path
    $idMatch = [regex]::Match($text, '(?m)^  id:\s*(\S+)\s*$')
    if (-not $idMatch.Success) { return $null }
    $meta = $path + '.meta'
    if (-not (Test-Path -LiteralPath $meta)) { return $null }
    $guidMatch = [regex]::Match((Get-Content -Raw -LiteralPath $meta), '(?m)^guid:\s*([0-9a-f]{32})')
    if (-not $guidMatch.Success) { return $null }
    $record = [ordered]@{
        Id = $idMatch.Groups[1].Value
        Kind = $kind
        Guid = $guidMatch.Groups[1].Value
        Tech = [int](Get-Number $text 'TechLevel' 0)
        Advances = ((Get-Number $text 'AdvancesTechLevel' 0) -gt 0.5)
        Prerequisites = @(Get-IdsFromSection (Get-Section $text 'prerequisites') 'Research')
        RequiredResearch = @(Get-IdsFromSection (Get-Section $text 'requiredResearch') 'Research')
        RequiredUpgrades = @(Get-IdsFromSection (Get-Section $text 'requiredWorkshopUpgrades') 'Workshop')
        Costs = @(Get-IdsFromSection (Get-Section $text 'resourceRequirements') 'Resource')
        Generates = @(Get-IdsFromSection (Get-Section $text 'resourceGenerationRates') 'Resource')
    }
    return [pscustomobject]$record
}

foreach ($kind in @('Resource', 'Building', 'Research', 'Workshop')) {
    $root = Join-Path $assetsRoot "Resources\Datas\$kind"
    if (-not (Test-Path $root)) { continue }
    foreach ($asset in Get-ChildItem -LiteralPath $root -Recurse -Filter '*.asset') {
        $definition = Get-Definition $asset.FullName $kind
        if ($null -ne $definition) {
            $guidToDefinition[$definition.Guid] = $definition
            [void]$definitions.Add($definition)
        }
    }
}

function Contains-All([Collections.Generic.HashSet[string]]$set, [object[]]$values) {
    foreach ($value in $values) { if (-not $set.Contains([string]$value)) { return $false } }
    return $true
}

$resources = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$completed = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$availableBuildings = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$purchased = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
[void]$resources.Add('WoodLog')
$tech = 0

if ($IndustrialBaseline) {
    $tech = 2
    foreach ($definition in $definitions) {
        if ($definition.Kind -eq 'Research' -and $definition.Tech -le 2) {
            [void]$completed.Add($definition.Id)
        }
        if ($definition.Kind -eq 'Building' -and $definition.Tech -le 2) {
            [void]$availableBuildings.Add($definition.Id)
            foreach ($output in $definition.Generates) { [void]$resources.Add($output) }
        }
    }
}

do {
    $changed = $false
    foreach ($research in $definitions | Where-Object Kind -eq 'Research') {
        if ($completed.Contains($research.Id)) { continue }
        $eraReachable = ($research.Tech -le $tech) -or ($research.Advances -and $research.Tech -eq ($tech + 1))
        if ($eraReachable -and (Contains-All $completed $research.Prerequisites) -and (Contains-All $resources $research.Costs)) {
            [void]$completed.Add($research.Id)
            if ($research.Advances -and $research.Tech -gt $tech) { $tech = $research.Tech }
            $changed = $true
        }
    }
    $workshopUnlocked = $completed.Contains('IndustrialWorkshop')
    foreach ($upgrade in $definitions | Where-Object Kind -eq 'Workshop') {
        if (-not $workshopUnlocked -or $purchased.Contains($upgrade.Id) -or $upgrade.Tech -gt $tech) { continue }
        if ((Contains-All $completed $upgrade.RequiredResearch) -and
            (Contains-All $purchased $upgrade.RequiredUpgrades) -and
            (Contains-All $resources $upgrade.Costs)) {
            [void]$purchased.Add($upgrade.Id)
            $changed = $true
        }
    }
    foreach ($building in $definitions | Where-Object Kind -eq 'Building') {
        if ($availableBuildings.Contains($building.Id) -or $building.Tech -gt $tech) { continue }
        if ((Contains-All $completed $building.RequiredResearch) -and
            (Contains-All $purchased $building.RequiredUpgrades) -and
            (Contains-All $resources $building.Costs)) {
            [void]$availableBuildings.Add($building.Id)
            foreach ($output in $building.Generates) { [void]$resources.Add($output) }
            $changed = $true
        }
    }
} while ($changed)

function First-Block($definition, [string]$kind) {
    $set = if ($kind -eq 'Research') { $completed } elseif ($kind -eq 'Workshop') { $purchased } else { $availableBuildings }
    foreach ($id in $definition.Prerequisites) { if (-not $completed.Contains($id)) { return "requires research $id" } }
    foreach ($id in $definition.RequiredResearch) { if (-not $completed.Contains($id)) { return "requires research $id" } }
    foreach ($id in $definition.RequiredUpgrades) { if (-not $purchased.Contains($id)) { return "requires workshop $id" } }
    foreach ($id in $definition.Costs) { if (-not $resources.Contains($id)) { return "costs unavailable resource $id" } }
    if ($definition.Tech -gt $tech) { return "requires tech level $($definition.Tech), reached $tech" }
    return 'unresolved dependency'
}

$lines = New-Object System.Collections.Generic.List[string]
[void]$lines.Add('# Static content closure report')
[void]$lines.Add("")
[void]$lines.Add("TechLevel reached: $tech")
[void]$lines.Add("Industrial baseline: $IndustrialBaseline")
$industrialResearch = @($definitions | Where-Object { $_.Kind -eq 'Research' -and $_.Tech -le 3 })
$industrialWorkshop = @($definitions | Where-Object { $_.Kind -eq 'Workshop' -and $_.Tech -le 3 })
$industrialBuilding = @($definitions | Where-Object { $_.Kind -eq 'Building' -and $_.Tech -le 3 })
$spacerResearch = @($definitions | Where-Object { $_.Kind -eq 'Research' -and $_.Tech -eq 4 })
$spacerWorkshop = @($definitions | Where-Object { $_.Kind -eq 'Workshop' -and $_.Tech -eq 4 })
$spacerBuilding = @($definitions | Where-Object { $_.Kind -eq 'Building' -and $_.Tech -eq 4 })
$completedIndustrialResearch = @($industrialResearch | Where-Object { $completed.Contains($_.Id) })
$purchasedIndustrialWorkshop = @($industrialWorkshop | Where-Object { $purchased.Contains($_.Id) })
$availableIndustrialBuilding = @($industrialBuilding | Where-Object { $availableBuildings.Contains($_.Id) })
$completedSpacerResearch = @($spacerResearch | Where-Object { $completed.Contains($_.Id) })
$purchasedSpacerWorkshop = @($spacerWorkshop | Where-Object { $purchased.Contains($_.Id) })
$availableSpacerBuilding = @($spacerBuilding | Where-Object { $availableBuildings.Contains($_.Id) })
[void]$lines.Add("Research reachable (up to Industrial): $($completedIndustrialResearch.Count)/$($industrialResearch.Count)")
[void]$lines.Add("Workshop reachable (up to Industrial): $($purchasedIndustrialWorkshop.Count)/$($industrialWorkshop.Count)")
[void]$lines.Add("Building reachable (up to Industrial): $($availableIndustrialBuilding.Count)/$($industrialBuilding.Count)")
[void]$lines.Add("Research reachable (Spacer): $($completedSpacerResearch.Count)/$($spacerResearch.Count)")
[void]$lines.Add("Workshop reachable (Spacer): $($purchasedSpacerWorkshop.Count)/$($spacerWorkshop.Count)")
[void]$lines.Add("Building reachable (Spacer): $($availableSpacerBuilding.Count)/$($spacerBuilding.Count)")
[void]$lines.Add("Resources available: $($resources.Count)")
[void]$lines.Add("")
[void]$lines.Add('## Unreachable research')
foreach ($item in $definitions | Where-Object { $_.Kind -eq 'Research' -and $_.Tech -le 3 -and -not $completed.Contains($_.Id) } | Sort-Object Id) {
    [void]$lines.Add("- $($item.Id): $(First-Block $item 'Research')")
}
[void]$lines.Add('')
[void]$lines.Add('## Unreachable workshop upgrades')
foreach ($item in $definitions | Where-Object { $_.Kind -eq 'Workshop' -and $_.Tech -le 3 -and -not $purchased.Contains($_.Id) } | Sort-Object Id) {
    [void]$lines.Add("- $($item.Id): $(First-Block $item 'Workshop')")
}
[void]$lines.Add('')
[void]$lines.Add('## Unreachable buildings')
foreach ($item in $definitions | Where-Object { $_.Kind -eq 'Building' -and $_.Tech -le 3 -and -not $availableBuildings.Contains($_.Id) } | Sort-Object Id) {
    [void]$lines.Add("- $($item.Id): $(First-Block $item 'Building')")
}
[void]$lines.Add('')
[void]$lines.Add('## Unreachable Spacer research')
foreach ($item in $spacerResearch | Where-Object { -not $completed.Contains($_.Id) } | Sort-Object Id) {
    [void]$lines.Add("- $($item.Id): $(First-Block $item 'Research')")
}
[void]$lines.Add('')
[void]$lines.Add('## Unreachable Spacer workshop upgrades')
foreach ($item in $spacerWorkshop | Where-Object { -not $purchased.Contains($_.Id) } | Sort-Object Id) {
    [void]$lines.Add("- $($item.Id): $(First-Block $item 'Workshop')")
}
[void]$lines.Add('')
[void]$lines.Add('## Unreachable Spacer buildings')
foreach ($item in $spacerBuilding | Where-Object { -not $availableBuildings.Contains($_.Id) } | Sort-Object Id) {
    [void]$lines.Add("- $($item.Id): $(First-Block $item 'Building')")
}

if ([string]::IsNullOrWhiteSpace($ReportPath)) { $ReportPath = Join-Path $project 'data\content-closure-static.md' }
$reportFull = [System.IO.Path]::GetFullPath($ReportPath)
New-Item -ItemType Directory -Force (Split-Path $reportFull) | Out-Null
[System.IO.File]::WriteAllLines($reportFull, $lines, [Text.UTF8Encoding]::new($false))
$lines | ForEach-Object { Write-Output $_ }

$unreachable = @($definitions | Where-Object {
    ($_.Kind -eq 'Research' -and $_.Tech -le 4 -and -not $completed.Contains($_.Id)) -or
    ($_.Kind -eq 'Workshop' -and $_.Tech -le 4 -and -not $purchased.Contains($_.Id)) -or
    ($_.Kind -eq 'Building' -and $_.Tech -le 4 -and -not $availableBuildings.Contains($_.Id))
})
if ($unreachable.Count -gt 0) { exit 2 }
exit 0
