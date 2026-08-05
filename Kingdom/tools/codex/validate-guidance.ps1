param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path,
    [string]$RepositoryPath = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
)
$ErrorActionPreference = "Stop"
$repositoryRequired = @(
    "AGENTS.md",
    "CODEX_ECONOMY_PROMPT.md",
    ".agents/skills/kingdom-content-expansion/SKILL.md",
    ".agents/skills/kingdom-economy-simulation/SKILL.md",
    ".agents/skills/kingdom-economy-simulation/agents/openai.yaml",
    "docs/balance/no-resource-caps.md",
    "docs/balance/balance-model.md",
    "docs/content/progression-roadmap.md",
    "docs/testing/content-balance-tests.md",
    "tools/EconomySimulator/EconomySimulator.csproj",
    "tools/EconomySimulator/UnityAssetSnapshotReader.cs",
    "tools/EconomySimulator/SimulationStrategy.cs",
    "tools/EconomySimulator/WorkshopSimulator.cs"
)
$projectRequired = @(
    "AGENTS.md",
    "docs/repository-map.md",
    "docs/architecture/runtime-state.md",
    "docs/architecture/ui-boundaries.md",
    "docs/architecture/serialized-pairs.md",
    "docs/testing/acceptance-checklist.md",
    "tools/codex/find-unity.ps1",
    "tools/codex/compile-unity.ps1",
    "tools/codex/run-unity-tests.ps1",
    "tools/codex/inspect-kingdom.ps1",
    "tools/codex/audit-ui.ps1"
)
$missing = @()
foreach ($relative in $repositoryRequired) {
    if (-not (Test-Path (Join-Path $RepositoryPath $relative))) {
        $missing += $relative
    }
}
foreach ($relative in $projectRequired) {
    if (-not (Test-Path (Join-Path $ProjectPath $relative))) {
        $missing += "Kingdom/$relative"
    }
}
if ($missing.Count -gt 0) {
    Write-Error ("Missing Kingdom guidance files:`n" + ($missing -join "`n"))
    exit 1
}
Write-Host "Current Kingdom guidance, simulator, and validation files are present."
