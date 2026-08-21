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
if (-not (Test-Path (Join-Path $assetsRoot 'Resources\Datas'))) {
    throw "Unity Assets/Resources/Datas was not found under $project"
}

$guidToDefinition = @{}
$definitions = New-Object System.Collections.Generic.List[object]
$definitionErrors = New-Object System.Collections.Generic.List[string]
$definitionFiles = New-Object System.Collections.Generic.List[object]
$guidOwners = @{}
$idOwners = @{}
$definitionStubs = @{}

function Get-Section([string]$text, [string]$name) {
    $pattern = '(?ms)^  ' + [regex]::Escape($name) + ':\s*\r?\n(.*?)(?=^  [A-Za-z][A-Za-z0-9_]*:\s*|\z)'
    $match = [regex]::Match($text, $pattern)
    if ($match.Success) { return $match.Groups[1].Value }
    return ''
}

function Get-IdsFromSection([string]$section, [string]$kind, [hashtable]$definitions) {
    $result = New-Object System.Collections.Generic.List[string]
    foreach ($match in [regex]::Matches($section, 'guid:\s*([0-9a-f]{32})', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
        $guid = $match.Groups[1].Value
        if ($definitions.ContainsKey($guid) -and $definitions[$guid].Kind -eq $kind) {
            [void]$result.Add($definitions[$guid].Id)
        }
    }
    return @($result)
}

function Get-Number([string]$text, [string]$name, [double]$default = 0) {
    $direct = [regex]::Match($text, '(?m)^  ' + [regex]::Escape($name) + ':\s*([-+0-9.eE]+)')
    if ($direct.Success) { return [double]::Parse($direct.Groups[1].Value, [Globalization.CultureInfo]::InvariantCulture) }
    return $default
}

function Get-Definition([string]$path, [string]$kind, [hashtable]$definitions) {
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
        Prerequisites = @(Get-IdsFromSection (Get-Section $text 'prerequisites') 'Research' $definitions)
        RequiredResearch = @(Get-IdsFromSection (Get-Section $text 'requiredResearch') 'Research' $definitions)
        RequiredUpgrades = @(Get-IdsFromSection (Get-Section $text 'requiredWorkshopUpgrades') 'Workshop' $definitions)
        Costs = @(Get-IdsFromSection (Get-Section $text 'resourceRequirements') 'Resource' $definitions)
        Generates = @(Get-IdsFromSection (Get-Section $text 'resourceGenerationRates') 'Resource' $definitions)
    }
    return [pscustomobject]$record
}

foreach ($kind in @('Resource', 'Building', 'Research', 'Workshop')) {
    $root = Join-Path $assetsRoot "Resources\Datas\$kind"
    if (-not (Test-Path $root)) { continue }
    foreach ($asset in Get-ChildItem -LiteralPath $root -Recurse -Filter '*.asset') {
        $meta = $asset.FullName + '.meta'
        if (-not (Test-Path -LiteralPath $meta)) {
            [void]$definitionErrors.Add("$kind/$($asset.BaseName): missing .meta file")
            continue
        }
        $guidMatch = [regex]::Match((Get-Content -Raw -LiteralPath $meta), '(?m)^guid:\s*([0-9a-fA-F]+)\s*$')
        if (-not $guidMatch.Success -or $guidMatch.Groups[1].Value.Length -ne 32) {
            [void]$definitionErrors.Add("$kind/$($asset.BaseName): invalid .meta GUID")
            continue
        }
        $idMatch = [regex]::Match((Get-Content -Raw -LiteralPath $asset.FullName), '(?m)^  id:\s*(\S+)\s*$')
        if (-not $idMatch.Success) {
            [void]$definitionErrors.Add("$kind/$($asset.BaseName): missing definition id")
            continue
        }
        $guid = $guidMatch.Groups[1].Value.ToLowerInvariant()
        if ($guidOwners.ContainsKey($guid)) {
            [void]$definitionErrors.Add("$kind/$($asset.BaseName): duplicate GUID; already used by $($guidOwners[$guid])")
            continue
        }
        $guidOwners[$guid] = "$kind/$($asset.BaseName)"
        [void]$definitionFiles.Add([pscustomobject]@{ Path = $asset.FullName; Kind = $kind; Guid = $guid })
        $definition = Get-Definition $asset.FullName $kind $guidToDefinition
        if ($null -ne $definition) {
            $idKey = "$kind/$($definition.Id)"
            if ($idOwners.ContainsKey($idKey)) {
                [void]$definitionErrors.Add("${idKey}: duplicate definition ID; already used by $($idOwners[$idKey])")
                continue
            }
            $idOwners[$idKey] = $asset.FullName
            $definitionStubs[$definition.Guid] = [pscustomobject]@{
                Id = $definition.Id
                Kind = $definition.Kind
                Guid = $definition.Guid
            }
        }
    }
}

# 第一遍只建立完整的 GUID -> 类型/ID 映射；第二遍再解析前置关系，避免
# 由于资产枚举顺序导致后出现的定义被静默忽略。
$guidToDefinition = $definitionStubs
$guidToDefinition = $definitionStubs
$completeDefinitions = @{}
$definitions = New-Object System.Collections.Generic.List[object]
foreach ($file in $definitionFiles) {
    $definition = Get-Definition $file.Path $file.Kind $definitionStubs
    if ($null -ne $definition) {
        $completeDefinitions[$definition.Guid] = $definition
        [void]$definitions.Add($definition)
    }
}
$guidToDefinition = $completeDefinitions

function Validate-ReferenceSection([string]$path, [string]$owner, [string]$field, [string]$expectedKind) {
    $text = Get-Content -Raw -LiteralPath $path
    $section = Get-Section $text $field
    foreach ($match in [regex]::Matches($section, 'guid:\s*([0-9a-fA-F]+)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
        $guid = $match.Groups[1].Value.ToLowerInvariant()
        if ($guid.Length -ne 32) {
            [void]$definitionErrors.Add("${owner}.${field}: invalid GUID $guid")
            continue
        }
        if (-not $guidToDefinition.ContainsKey($guid)) {
            [void]$definitionErrors.Add("${owner}.${field}: unresolved GUID $guid")
            continue
        }
        if ($guidToDefinition[$guid].Kind -ne $expectedKind) {
            [void]$definitionErrors.Add("${owner}.${field}: GUID $guid resolves to $($guidToDefinition[$guid].Kind), expected $expectedKind")
        }
    }
}

foreach ($file in $definitionFiles) {
    $owner = "$($file.Kind)/$([System.IO.Path]::GetFileNameWithoutExtension($file.Path))"
    switch ($file.Kind) {
        'Research' {
            Validate-ReferenceSection $file.Path $owner 'prerequisites' 'Research'
            Validate-ReferenceSection $file.Path $owner 'resourceRequirements' 'Resource'
        }
        'Workshop' {
            Validate-ReferenceSection $file.Path $owner 'requiredResearch' 'Research'
            Validate-ReferenceSection $file.Path $owner 'requiredUpgrades' 'Workshop'
            Validate-ReferenceSection $file.Path $owner 'resourceRequirements' 'Resource'
        }
        'Building' {
            Validate-ReferenceSection $file.Path $owner 'requiredResearch' 'Research'
            Validate-ReferenceSection $file.Path $owner 'requiredWorkshopUpgrades' 'Workshop'
            Validate-ReferenceSection $file.Path $owner 'resourceRequirements' 'Resource'
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
[void]$lines.Add("Industrial baseline mode: $IndustrialBaseline")
$industrialResearch = @($definitions | Where-Object { $_.Kind -eq 'Research' -and $_.Tech -le 3 })
$industrialWorkshop = @($definitions | Where-Object { $_.Kind -eq 'Workshop' -and $_.Tech -le 3 })
$industrialBuilding = @($definitions | Where-Object { $_.Kind -eq 'Building' -and $_.Tech -le 3 })
$spacerResearch = @($definitions | Where-Object { $_.Kind -eq 'Research' -and $_.Tech -eq 4 })
$spacerWorkshop = @($definitions | Where-Object { $_.Kind -eq 'Workshop' -and $_.Tech -eq 4 })
$spacerBuilding = @($definitions | Where-Object { $_.Kind -eq 'Building' -and $_.Tech -eq 4 })
$ultraResearch = @($definitions | Where-Object { $_.Kind -eq 'Research' -and $_.Tech -eq 5 })
$ultraWorkshop = @($definitions | Where-Object { $_.Kind -eq 'Workshop' -and $_.Tech -eq 5 })
$ultraBuilding = @($definitions | Where-Object { $_.Kind -eq 'Building' -and $_.Tech -eq 5 })
$completedIndustrialResearch = @($industrialResearch | Where-Object { $completed.Contains($_.Id) })
$purchasedIndustrialWorkshop = @($industrialWorkshop | Where-Object { $purchased.Contains($_.Id) })
$availableIndustrialBuilding = @($industrialBuilding | Where-Object { $availableBuildings.Contains($_.Id) })
$completedSpacerResearch = @($spacerResearch | Where-Object { $completed.Contains($_.Id) })
$purchasedSpacerWorkshop = @($spacerWorkshop | Where-Object { $purchased.Contains($_.Id) })
$availableSpacerBuilding = @($spacerBuilding | Where-Object { $availableBuildings.Contains($_.Id) })
$completedUltraResearch = @($ultraResearch | Where-Object { $completed.Contains($_.Id) })
$purchasedUltraWorkshop = @($ultraWorkshop | Where-Object { $purchased.Contains($_.Id) })
$availableUltraBuilding = @($ultraBuilding | Where-Object { $availableBuildings.Contains($_.Id) })
$industrialClosureComplete =
    ($completedIndustrialResearch.Count -eq $industrialResearch.Count) -and
    ($purchasedIndustrialWorkshop.Count -eq $industrialWorkshop.Count) -and
    ($availableIndustrialBuilding.Count -eq $industrialBuilding.Count)
[void]$lines.Add("Industrial closure complete: $industrialClosureComplete")
[void]$lines.Add("Research reachable (up to Industrial): $($completedIndustrialResearch.Count)/$($industrialResearch.Count)")
[void]$lines.Add("Workshop reachable (up to Industrial): $($purchasedIndustrialWorkshop.Count)/$($industrialWorkshop.Count)")
[void]$lines.Add("Building reachable (up to Industrial): $($availableIndustrialBuilding.Count)/$($industrialBuilding.Count)")
[void]$lines.Add("Research reachable (Spacer): $($completedSpacerResearch.Count)/$($spacerResearch.Count)")
[void]$lines.Add("Workshop reachable (Spacer): $($purchasedSpacerWorkshop.Count)/$($spacerWorkshop.Count)")
[void]$lines.Add("Building reachable (Spacer): $($availableSpacerBuilding.Count)/$($spacerBuilding.Count)")
[void]$lines.Add("Research reachable (Ultra): $($completedUltraResearch.Count)/$($ultraResearch.Count)")
[void]$lines.Add("Workshop reachable (Ultra): $($purchasedUltraWorkshop.Count)/$($ultraWorkshop.Count)")
[void]$lines.Add("Building reachable (Ultra): $($availableUltraBuilding.Count)/$($ultraBuilding.Count)")
[void]$lines.Add("Resources available: $($resources.Count)")
[void]$lines.Add("")
if ($definitionErrors.Count -gt 0) {
    [void]$lines.Add('## Definition integrity errors')
    foreach ($error in $definitionErrors) { [void]$lines.Add("- $error") }
    [void]$lines.Add('')
}
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
[void]$lines.Add('')
[void]$lines.Add('## Unreachable Ultra research')
foreach ($item in $ultraResearch | Where-Object { -not $completed.Contains($_.Id) } | Sort-Object Id) {
    [void]$lines.Add("- $($item.Id): $(First-Block $item 'Research')")
}
[void]$lines.Add('')
[void]$lines.Add('## Unreachable Ultra workshop upgrades')
foreach ($item in $ultraWorkshop | Where-Object { -not $purchased.Contains($_.Id) } | Sort-Object Id) {
    [void]$lines.Add("- $($item.Id): $(First-Block $item 'Workshop')")
}
[void]$lines.Add('')
[void]$lines.Add('## Unreachable Ultra buildings')
foreach ($item in $ultraBuilding | Where-Object { -not $availableBuildings.Contains($_.Id) } | Sort-Object Id) {
    [void]$lines.Add("- $($item.Id): $(First-Block $item 'Building')")
}

if ([string]::IsNullOrWhiteSpace($ReportPath)) { $ReportPath = Join-Path $project 'data\content-closure-static.md' }
$reportFull = [System.IO.Path]::GetFullPath($ReportPath)
New-Item -ItemType Directory -Force (Split-Path $reportFull) | Out-Null
[System.IO.File]::WriteAllLines($reportFull, $lines, [Text.UTF8Encoding]::new($false))
$lines | ForEach-Object { Write-Output $_ }

$unreachable = @($definitions | Where-Object {
    ($_.Kind -eq 'Research' -and $_.Tech -le 5 -and -not $completed.Contains($_.Id)) -or
    ($_.Kind -eq 'Workshop' -and $_.Tech -le 5 -and -not $purchased.Contains($_.Id)) -or
    ($_.Kind -eq 'Building' -and $_.Tech -le 5 -and -not $availableBuildings.Contains($_.Id))
})
if ($definitionErrors.Count -gt 0) { exit 3 }
if ($unreachable.Count -gt 0) { exit 2 }
exit 0
