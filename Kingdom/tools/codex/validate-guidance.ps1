param([string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path)
$ErrorActionPreference = "Stop"
$required = @(
    "AGENTS.md",
    "ToDoList_New.txt",
    ".agents/skills/kingdom-runtime-refactor/SKILL.md",
    ".agents/skills/kingdom-ui-redesign/SKILL.md",
    "docs/repository-map.md",
    "docs/audits/kingdom3-static-audit.md",
    "docs/architecture/runtime-state.md",
    "docs/architecture/ui-boundaries.md",
    "docs/architecture/serialized-pairs.md",
    "docs/plans/ui-ready-and-redesign.md",
    "docs/testing/acceptance-checklist.md",
    "tools/codex/find-unity.ps1",
    "tools/codex/compile-unity.ps1",
    "tools/codex/run-unity-tests.ps1",
    "tools/codex/inspect-kingdom.ps1",
    "tools/codex/audit-ui.ps1"
)
$missing = @()
foreach ($relative in $required) {
    if (-not (Test-Path (Join-Path $ProjectPath $relative))) {
        $missing += $relative
    }
}
if ($missing.Count -gt 0) {
    Write-Error ("Missing Kingdom guidance files:`n" + ($missing -join "`n"))
    exit 1
}
Write-Host "Kingdom guidance files are present."
