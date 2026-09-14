param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path,
    [string]$RepositoryPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
)
$ErrorActionPreference = "Stop"
$repositoryRequired = @(
    "AGENTS.md",
    ".codex/prompts/CODEX_ECONOMY_PROMPT.md",
    ".agents/skills/kingdom-project-dev/SKILL.md",
    ".agents/skills/kingdom-project-dev/references/validation.md",
    ".agents/skills/kingdom-project-dev/references/guidance-maintenance.md",
    ".agents/skills/kingdom-project-dev/references/subagents.md",
    ".agents/skills/kingdom-project-dev/references/evidence.md",
    ".agents/skills/kingdom-project-dev/references/acceptance-cases.md",
    ".agents/skills/kingdom-project-dev/assets/handoff-template.md",
    ".agents/skills/kingdom-ui-redesign/SKILL.md",
    ".agents/skills/kingdom-ui-redesign/agents/openai.yaml",
    ".workbuddy-ai/skills/kingdom-project-dev/SKILL.md",
    ".agents/skills/kingdom-economy-simulation/references/content-design.md",
    ".agents/skills/kingdom-economy-simulation/SKILL.md",
    ".agents/skills/kingdom-economy-simulation/agents/openai.yaml",
    "docs/balance/no-resource-caps.md",
    "docs/balance/balance-model.md",
    "docs/content/progression-roadmap.md",
    "docs/testing/content-balance-tests.md",
    "tools/NewEconomySimulator/NewEconomySimulator.csproj",
    "tools/NewEconomySimulator/SnapshotExporter.cs",
    "tools/NewEconomySimulator/SimulationCore.cs",
    "tools/NewEconomySimulator/ReportWriter.cs"
)
$projectRequired = @(
    "AGENTS.md",
    "docs/repository-map.md",
    "docs/architecture/runtime-state.md",
    "docs/architecture/ui-boundaries.md",
    "docs/architecture/serialized-pairs.md",
    "docs/testing/acceptance-checklist.md",
    "docs/testing/playmode-test-plan.md",
    "docs/architecture/research-queue-payment.md",
    "docs/ui/page-responsibilities.md",
    "docs/decisions/conservative-defaults.md",
    "Assets/Resources/Script/AGENTS.md",
    "Assets/Tests/AGENTS.md",
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
        $missing += $relative
    }
}
if ($missing.Count -gt 0) {
    Write-Error ("Missing Kingdom guidance files:`n" + ($missing -join "`n"))
    exit 1
}
Write-Host "Current Kingdom guidance, simulator, and validation files are present."
