param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
)

$ErrorActionPreference = 'Stop'
$required = @(
    'AGENTS.md',
    'ToDoList_Content_Next_2026-07-25.txt',
    '.agents/skills/kingdom-content-expansion/SKILL.md',
    'docs/audits/kingdom5-content-static-audit.md',
    'docs/balance/no-resource-caps.md',
    'docs/balance/balance-model.md',
    'docs/content/progression-roadmap.md',
    'docs/testing/content-balance-tests.md',
    'data/current_resource_source_sink_audit.csv',
    'data/proposed_research_rebalance.csv',
    'data/proposed_buildings_vertical_slice.csv'
)

$missing = @()
foreach ($relative in $required) {
    $path = Join-Path $ProjectRoot $relative
    if (-not (Test-Path -LiteralPath $path)) {
        $missing += $relative
    }
}

if ($missing.Count -gt 0) {
    throw "Missing guidance files:`n$($missing -join "`n")"
}

$noCaps = Get-Content (Join-Path $ProjectRoot 'docs/balance/no-resource-caps.md') -Raw
if ($noCaps -notmatch 'FoodAmount' -or $noCaps -notmatch 'Capacity') {
    throw 'No-resource-cap rule is missing.'
}

Write-Output 'Kingdom content guidance validation passed.'
