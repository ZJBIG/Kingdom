# Kingdom repository instructions

## Repository identity

- Repository: `Kingdom`
- Current guidance: repository-root `AGENTS.md` and `CODEX_ECONOMY_PROMPT.md`
- Unity Editor: `2022.3.62f2c1`
- Primary build scene: `Assets/Scenes/SampleScene.unity`
- Source of truth: current repository files/assets, then root guidance and current generated reports.
- Historical Kingdom3/Kingdom4 plans and dated handoffs are not authoritative.

## Current milestone

Do not redo completed numeric, state, save, or UI ownership migrations. Current
economy work prioritizes gameplay bugs, content loops, Unity evidence, static
closure, and practical pacing. The standalone simulator is a regression tool;
do not extend its strategies or decision scoring unless explicitly requested.

## Read before modifying

1. root `AGENTS.md`
2. `CODEX_ECONOMY_PROMPT.md`
3. the applicable repository skill
4. current code/assets and generated evidence
5. the nearest scoped `AGENTS.md` for every touched file

Use `kingdom-runtime-refactor` for simulation, State, Manager, save and correctness work.
Use `kingdom-ui-redesign` for Viewer/Displayer, Canvas, Prefab, layout, navigation, theme and visual redesign work.
For Research UI, also follow `D:/GitHub/Kingdom/.agents/skills/kingdom-ui-redesign/SKILL.md`
and compare graph geometry and touch behavior against
`D:/Verse/RimworldMods/#HSK/ResearchTree_SK`.
Use one shared top-left integer-grid coordinate convention for nodes and lines;
convert it once when placing Unity RectTransforms.
The active CanvasScaler must be ScaleWithScreenSize at 2640x1200 and match
width so the research viewport never becomes negative on the target layout.
Use the canonical repository skill `D:/GitHub/Kingdom/.agents/skills/kingdom-economy-simulation/SKILL.md`
for every Research, Resource, Building, TechLevel, Workshop, production, consumption,
reachability, pacing or balance task. Do not use this older scoped content skill as a
replacement for the economy simulation workflow.

## Non-negotiable architecture

- ScriptableObject = immutable definition data.
- State class = mutable authoritative runtime data.
- Manager = validation, transaction and State mutation.
- `SimulationManager` = the only core gameplay clock.
- Viewer/Displayer = bind, render, input and Manager commands only.
- ResearchTreeSK graph nodes forward drag lifecycle events to the graph
  gesture owner; do not rely on a parent ScrollRect to receive a drag that
  starts on a Button. Enable each axis only from measured content overflow.
- Any asset-coordinate fallback must log its topology rejection counts;
  silent fallback is not acceptable for research-tree parity audits.
- Run the targeted PlayMode audit
  `ResearchTree_RuntimeLayoutAndOverflow_AreLoggedAndNonOverlapping` when
  changing graph coordinates or pointer routing; its log is the runtime
  evidence for unique cells and measured overflow.
- UI activation, transform position and localized text never determine gameplay.
- Disabled UI must not stop resources, buildings, research, calendar, autosave or music.
- Save DTOs contain stable IDs and non-derivable values only.
- `ExpantaNum` remains a numeric core; gameplay/UI APIs do not belong in it.

## Completed migrations that must not be repeated

- `BigNumber` was removed.
- `Pair<Resource,string>` was migrated to `Pair<Resource,ExpantaNum>`.
- four Runtime State classes exist.
- Managers no longer hold Viewer/Displayer references.
- stable definition IDs and `DataBase<T>` exist.
- unified `SaveManager` exists.
- `Transform.GetChild(index)` was removed.
- enum description caching exists.

Compatibility wrappers or duplicate authority are prohibited.

## Current known blockers

- Static closure passes, but dynamic pacing acceptance currently fails.
- Full runtime/simulator multi-building tick parity still needs Unity evidence.
- Do not treat dated blocker lists as current facts; rerun the relevant checks.

## Pair policy

Reuse `Pair<TFirst,TSecond>` only for true two-value records.

- Resource/value definition lists use `Pair<Resource,ExpantaNum>`.
- Pair serialized field names `first` and `second` must remain stable unless a tested Editor migration changes them.
- Save DTOs use explicit named fields.
- Create a dedicated type when more fields, units, validation, behavior or versioning are required.

## Unity asset safety

- Preserve `.meta` files and GUIDs.
- Use `git mv` for Unity assets.
- Search `.unity`, `.prefab`, `.asset` and `.meta` references before deletion.
- Do not hand-edit GUIDs.
- Do not install packages, upgrade Unity or change unrelated ProjectSettings without explicit permission.
- Scene/Prefab claims require actual Unity validation.

## Git safety

- Inspect `git status` before work.
- Do not discard user changes.
- Do not reset, rebase, amend, force-push or push unless explicitly requested.
- Do not create commits unless the task authorizes them.
- Never enter the next phase with known compile errors.

## Validation

Use:

```powershell
powershell -ExecutionPolicy Bypass -File tools/codex/validate-guidance.ps1
powershell -ExecutionPolicy Bypass -File tools/codex/inspect-kingdom.ps1
powershell -ExecutionPolicy Bypass -File tools/codex/audit-ui.ps1
powershell -ExecutionPolicy Bypass -File tools/codex/compile-unity.ps1
powershell -ExecutionPolicy Bypass -File tools/codex/run-unity-tests.ps1 -Platform EditMode
powershell -ExecutionPolicy Bypass -File tools/codex/run-unity-tests.ps1 -Platform PlayMode
```

If Unity is unavailable, state exactly: `未执行真实 Unity 编译。`
Do not claim PlayMode, Inspector, visual or performance validation without evidence.

## Reporting

Report changed files, TODO IDs, design decisions, compile result, tests/result paths, Console status, manual Scene/Prefab work, blockers and next scope. Avoid “optimized”, “fixed” or “should work” without evidence.
