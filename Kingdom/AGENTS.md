# Kingdom repository instructions

## Repository identity

- Repository: `Kingdom`
- Audited baseline: `Kingdom3.7z`
- Baseline date: `2026-07-24`
- Unity Editor: `2022.3.62f2c1`
- Primary build scene: `Assets/Scenes/SampleScene.unity`
- Source of truth: current repository files/assets, then `ToDoList_New.txt`, then `docs/`.
- Historical `Script.zip`, BigNumber plans and pre-Kingdom3 repository maps are not authoritative.

## Current milestone

Kingdom3 has already completed the BigNumber-to-ExpantaNum migration, serialized Pair migration, Runtime State ownership, Manager/UI ownership split, stable IDs, DataBase, Bootstrap and unified save foundation.

Do not redo completed migrations.

The current sequence is:

1. fix per-tick resource satisfaction and food integration;
2. harden save fallback/reload;
3. centralize UI refresh;
4. migrate manual layouts;
5. add real PlayMode UI lifecycle tests;
6. pass the UI-Ready gate and replace off-screen hiding with `SetActive`;
7. build the UI design system and redesign screens.

## Read before modifying

1. `ToDoList_New.txt`
2. `docs/repository-map.md`
3. `docs/audits/kingdom3-static-audit.md`
4. `docs/architecture/runtime-state.md`
5. `docs/architecture/ui-boundaries.md`
6. `docs/architecture/serialized-pairs.md`
7. `docs/plans/ui-ready-and-redesign.md`
8. `docs/testing/acceptance-checklist.md`
9. the nearest scoped `AGENTS.md` for every touched file

Use `kingdom-runtime-refactor` for simulation, State, Manager, save and correctness work.
Use `kingdom-ui-redesign` for Viewer/Displayer, Canvas, Prefab, layout, navigation, theme and visual redesign work.

## Non-negotiable architecture

- ScriptableObject = immutable definition data.
- State class = mutable authoritative runtime data.
- Manager = validation, transaction and State mutation.
- `SimulationManager` = the only core gameplay clock.
- Viewer/Displayer = bind, render, input and Manager commands only.
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

- resource satisfaction is not exact for partial per-tick inventory;
- food is integrated inside the calendar step;
- three main viewers own Update refresh loops;
- HUD and MusicViewer own UI refresh coroutines;
- resource/building layout still uses manual size calculations;
- main/settings pages are hidden by moving to `(-10000,-10000)`;
- there are no real PlayMode test cases;
- `BackGround` rotation is frame-rate dependent.

Do not claim P7/UI decoupling complete while any blocker remains.

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
