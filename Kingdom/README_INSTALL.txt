Kingdom Codex Repository Pack (2026-07-24)
=====================================

Baseline: Kingdom3.7z
Unity: 2022.3.62f2c1

Installation
------------

1. Back up the current Kingdom repository or create a Git branch.
2. Copy this pack into the Kingdom repository root and preserve directory structure.
3. Allow replacement of stale guidance files:
   - AGENTS.md
   - .agents/skills/kingdom-runtime-refactor/
4. Add the new files:
   - ToDoList_New.txt
   - .agents/skills/kingdom-ui-redesign/
   - docs/
   - tools/codex/
   - CODEX_START_PROMPT.txt
   - CODEX_UI_REDESIGN_PROMPT.txt
5. Do not copy the ExpantaNum package into the project blindly: Kingdom3 already contains the exact same stable source. The standalone ExpantaNum pack is for archive/reuse.
6. Run:
   powershell -ExecutionPolicy Bypass -File tools/codex/validate-guidance.ps1
   powershell -ExecutionPolicy Bypass -File tools/codex/inspect-kingdom.ps1
   powershell -ExecutionPolicy Bypass -File tools/codex/audit-ui.ps1
7. Start Codex with CODEX_START_PROMPT.txt.

This pack does not automatically modify Scene, Prefab, ScriptableObject or ProjectSettings.
