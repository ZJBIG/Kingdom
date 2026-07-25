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
$hasSink = @($sourceSink | Where-Object { $_.HasAnySink -eq 'True' }).Count
$unused = @($sourceSink | Where-Object { $_.CurrentlyUnused -eq 'True' }).Count
Add-SummaryRow $rows 'ResourcesWithProducer' $hasProducer 'count' 'current_resource_source_sink_audit.csv' 'Measured' 'Producer field is present in the supplied static audit.'
Add-SummaryRow $rows 'ResourcesWithAnySink' $hasSink 'count' 'current_resource_source_sink_audit.csv' 'Measured' 'Any sink includes building or research sinks.'
Add-SummaryRow $rows 'CurrentlyUnusedResources' $unused 'count' 'current_resource_source_sink_audit.csv' 'Measured' 'Static audit classification; not a release decision.'

$reachable = @($researches | Where-Object { $_.ReachableFromNewGameStaticAudit -eq 'True' })
Add-SummaryRow $rows 'ResearchReachableFromNewGameStaticAudit' $reachable.Count 'count' 'current_researches.csv' 'Measured' 'Copied from the supplied static audit column.'

$expectedResearchPower = 1.0
$animalReachableCost = 0.0
$animalUnparsed = New-Object System.Collections.Generic.List[string]
foreach ($research in ($reachable | Where-Object { $_.TechLevel -like 'Animal*' })) {
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
Add-SummaryRow $rows 'AnimalToNeolithicEstimatedTime' 'NotDerivable' 'seconds' 'current_researches.csv' 'Blocked' 'No statically reachable Neolithic transition is present; requires C1 fixes and simulation.'
Add-SummaryRow $rows 'NeolithicToMedievalEstimatedTime' 'NotDerivable' 'seconds' 'current_researches.csv' 'Blocked' 'No statically reachable Medieval transition is present; requires C1 fixes and simulation.'

Add-SummaryRow $rows 'BuildingFirstCopyPayback' 'NotDerivable' 'seconds' 'current_buildings.csv' 'MissingModel' 'The snapshot has material requirements and outputs but no resource valuation, workforce schedule, or construction-time model.'
Add-SummaryRow $rows 'ResearchPowerAssumption' $expectedResearchPower.ToString('0.###', $culture) 'research/s' 'balance-model.md' 'Assumption' 'Temporary baseline only; replace with measured ResearchPower after C2.'

$parent = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Force $parent | Out-Null
$rows | Export-Csv -LiteralPath $OutputPath -NoTypeInformation -Encoding UTF8
Write-Output "Wrote $($rows.Count) snapshot rows to $OutputPath"
