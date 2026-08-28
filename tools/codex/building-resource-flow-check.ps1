param(
    [string]$ProjectPath = (Get-Location).Path
)

$buildingPath = Join-Path $ProjectPath 'Assets/Resources/Datas/Building'
if (-not (Test-Path -LiteralPath $buildingPath)) {
    throw "Building data directory not found: $buildingPath"
}

function Get-ResourceGuids([string]$yaml, [string]$sectionName) {
    $pattern = '(?ms)^  {0}:\s*(.*?)(?=^  [A-Za-z][A-Za-z0-9]*:|\z)' -f [regex]::Escape($sectionName)
    $section = [regex]::Match($yaml, $pattern)
    if (-not $section.Success) {
        return @{}
    }

    $result = @{}
    foreach ($match in [regex]::Matches($section.Groups[1].Value, 'resource:.*?guid: ([0-9a-fA-F]+)')) {
        $result[$match.Groups[1].Value.ToLowerInvariant()] = $true
    }
    return $result
}

$files = @(Get-ChildItem -LiteralPath $buildingPath -Recurse -Filter '*.asset')
$overlaps = [System.Collections.Generic.List[string]]::new()

foreach ($file in $files) {
    $yaml = Get-Content -LiteralPath $file.FullName -Raw -Encoding utf8
    $generated = Get-ResourceGuids $yaml 'resourceGenerationRates'
    $consumed = Get-ResourceGuids $yaml 'resourceConsumptionRates'
    foreach ($guid in $generated.Keys) {
        if ($consumed.ContainsKey($guid)) {
            $overlaps.Add($file.FullName)
            break
        }
    }
}

Write-Output "BuildingAssets=$($files.Count)"
if ($overlaps.Count -gt 0) {
    $overlaps | Sort-Object -Unique | ForEach-Object {
        Write-Error "Opposing raw resource flow: $_"
    }
    Write-Output "OpposingRawResourceFlows=$($overlaps.Count)"
    exit 1
}

Write-Output 'OpposingRawResourceFlows=0'
