param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path,
    [string]$OutputPath = (Join-Path $ProjectRoot 'data/content-baseline-summary.csv')
)

$ErrorActionPreference = 'Stop'
$culture = [Globalization.CultureInfo]::InvariantCulture
$numberStyles = [Globalization.NumberStyles]::Float

function Convert-ToSnapshotNumber([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return $null }
    $text = $value.Trim()
    if ($text.StartsWith('e', [StringComparison]::OrdinalIgnoreCase)) {
        $text = '1' + $text
    }
    [double]$number = 0
    if ([double]::TryParse($text, $numberStyles, $culture, [ref]$number)) {
        return $number
    }
    return $null
}

function Add-SummaryRow(
    [System.Collections.Generic.List[object]]$rows,
    [string]$metric,
    [string]$value,
    [string]$unit,
    [string]$source,
    [string]$status,
    [string]$notes) {
    $rows.Add([pscustomobject]@{
        Metric = $metric
        Value = $value
        Unit = $unit
        Source = $source
        Status = $status
        Notes = $notes
    })
}

$dataRoot = Join-Path $ProjectRoot 'data'
$resources = @(Import-Csv (Join-Path $dataRoot 'current_resources.csv'))
$buildings = @(Import-Csv (Join-Path $dataRoot 'current_buildings.csv'))
$researches = @(Import-Csv (Join-Path $dataRoot 'current_researches.csv'))
$sourceSink = @(Import-Csv (Join-Path $dataRoot 'current_resource_source_sink_audit.csv'))
$rows = [System.Collections.Generic.List[object]]::new()

Add-SummaryRow $rows 'ResourceCount' $resources.Count 'count' 'current_resources.csv' 'Measured' 'Static definition snapshot.'
Add-SummaryRow $rows 'BuildingCount' $buildings.Count 'count' 'current_buildings.csv' 'Measured' 'Static definition snapshot.'
Add-SummaryRow $rows 'ResearchCount' $researches.Count 'count' 'current_researches.csv' 'Measured' 'Static definition snapshot.'

foreach ($group in ($buildings | Group-Object TechLevel)) {
    Add-SummaryRow $rows ("BuildingCount:{0}" -f $group.Name) $group.Count 'count' 'current_buildings.csv' 'Measured' 'Grouped by serialized TechLevel.'
}
foreach ($group in ($researches | Group-Object TechLevel)) {
    Add-SummaryRow $rows ("ResearchCount:{0}" -f $group.Name) $group.Count 'count' 'current_researches.csv' 'Measured' 'Grouped by serialized TechLevel.'
}
foreach ($group in ($resources | Group-Object DisplaySet)) {
    Add-SummaryRow $rows ("ResourceCount:DisplaySet:{0}" -f $group.Name) $group.Count 'count' 'current_resources.csv' 'Measured' 'Grouped by serialized display set.'
}

$hasProducer = @($sourceSink | Where-Object { $_.HasProducer -eq 'True' }).Count
$hasSink = @($sourceSink | Where-Object {
    ([int]$_.BuildingSinks + [int]$_.ResearchSinks) -gt 0
}).Count
$unused = @($sourceSink | Where-Object {
    $_.HasProducer -ne 'True' -and
    ([int]$_.BuildingSinks + [int]$_.ResearchSinks) -eq 0
}).Count
Add-SummaryRow $rows 'ResourcesWithProducer' $hasProducer 'count' 'current_resource_source_sink_audit.csv' 'Measured' 'Producer field is present in the supplied static audit.'
Add-SummaryRow $rows 'ResourcesWithAnySink' $hasSink 'count' 'current_resource_source_sink_audit.csv' 'Measured' 'Any sink includes building or research sinks.'
Add-SummaryRow $rows 'CurrentlyUnusedResources' $unused 'count' 'current_resource_source_sink_audit.csv' 'Measured' 'Static audit classification; not a release decision.'

$reachable = @($researches)
Add-SummaryRow $rows 'ResearchDefinitionsInProgressionAudit' $reachable.Count 'count' 'current_researches.csv' 'Measured' 'All current research definitions are included in the Unity progression audit.'

$expectedResearchPower = 1.0
$animalReachableCost = 0.0
$animalUnparsed = New-Object System.Collections.Generic.List[string]
foreach ($research in ($reachable | Where-Object { $_.TechLevel -eq 'Animal' })) {
    $cost = Convert-ToSnapshotNumber $research.BaseCost
    if ($null -eq $cost) {
        $animalUnparsed.Add($research.Id)
    } else {
        $animalReachableCost += $cost
    }
}
$animalNotes = 'Sum of reachable Animal research BaseCost divided by expected ResearchPower=1/s.'
if ($animalUnparsed.Count -gt 0) {
    $animalNotes += ' Unparsed BaseCost IDs: ' + ($animalUnparsed -join ', ') + '.'
}
Add-SummaryRow $rows 'AnimalReachableNominalResearchTime' ($animalReachableCost / $expectedResearchPower).ToString('0.###', $culture) 'seconds' 'current_researches.csv' 'Derived' $animalNotes
$pacingPath = Join-Path $ProjectRoot 'TestResults/C10-28/balance/vertical-slice-pacing-after.csv'
if (Test-Path $pacingPath) {
    $balanced = Import-Csv $pacingPath | Where-Object {
        $_.Scenario -eq 'Balanced' -and $_.HorizonSeconds -eq '86400'
    } | Select-Object -First 1
    Add-SummaryRow $rows 'AnimalToNeolithicEstimatedTime' $balanced.NeolithicAtSeconds 'seconds' 'vertical-slice-pacing-after.csv' 'Estimated' 'Deterministic balanced-strategy research model; resource reachability is validated separately.'
    Add-SummaryRow $rows 'AnimalToMedievalEstimatedTime' $balanced.MedievalAtSeconds 'seconds' 'vertical-slice-pacing-after.csv' 'Estimated' 'Deterministic balanced-strategy research model; not a recorded human playthrough.'
}

Add-SummaryRow $rows 'BuildingFirstCopyPayback' 'ReportedSeparately' 'seconds' 'building-first-copy-payback-after.csv' 'Estimated' 'Nominal material-unit heuristic; strategic and infrastructure buildings require playtest valuation.'
Add-SummaryRow $rows 'ResearchPowerAssumption' $expectedResearchPower.ToString('0.###', $culture) 'research/s' 'balance-model.md' 'Assumption' 'Temporary baseline only; replace with measured ResearchPower after C2.'

$parent = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Force $parent | Out-Null
$rows | Export-Csv -LiteralPath $OutputPath -NoTypeInformation -Encoding UTF8
Write-Output "Wrote $($rows.Count) snapshot rows to $OutputPath"
