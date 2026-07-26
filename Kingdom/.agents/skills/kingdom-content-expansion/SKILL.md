---
name: kingdom-content-expansion
description: Use for Kingdom gameplay content, balance, progression, research, buildings, resources, population, territory, industrialization, space, alien war, and numerical economy work. Do not use for pure UI styling or ExpantaNum internals.
---

# Kingdom content expansion

## Trigger

Use this Skill when the task involves:

- adding or rebalancing resources, buildings or research;
- civilization-era progression;
- building costs, production chains or research pacing;
- population, workforce, territory, energy or logistics;
- space exploration or alien warfare;
- content reachability and source/sink validation;
- long-term incremental-game balance.

Do not use it for:

- pure visual/UI restyling;
- isolated audio changes;
- refactoring ExpantaNum internals without a demonstrated math bug;
- unrelated Unity projects.

## Required reading

1. `ToDoList_Content_Next_2026-07-25.txt`
2. `docs/audits/kingdom5-content-static-audit.md`
3. `docs/balance/no-resource-caps.md`
4. `docs/balance/balance-model.md`
5. `docs/content/progression-roadmap.md`
6. `docs/testing/content-balance-tests.md`
7. `data/current_resource_source_sink_audit.csv`
8. `data/proposed_research_rebalance.csv`
9. `data/proposed_buildings_vertical_slice.csv`
10. current repository code and assets

Current repository files always outrank this guidance when they conflict. Report the conflict instead of silently guessing.

## Non-negotiable rule

Food is the only capped stockpile.

Never add capacity to ordinary Resource definitions or ResourceState. Progression gates must come from cost growth, production chains, research, workforce, territory, power, logistics, projects or combat.

## Current milestone

The immediate milestone is not Industrial or alien warfare.

Complete a playable vertical slice:

`Animal -> Neolithic -> Medieval`

The current repository has unreachable content because Clay, PlantFiber and metal ores lack producers. Fix reachability before adding later eras.

## Execution order

1. Audit and tests only.
2. Fix unreachable definitions.
3. Add geometric building costs.
4. Add research effects and ResearchPower flow.
5. Rebalance the early vertical slice.
6. Add population and territory.
7. Add Medieval content.
8. Industrial, space and alien war only after earlier acceptance passes.

Do not generate hundreds of future assets in one patch.

## Balance rules

- Use `GeometricSeriesCost` and `MaxAffordableGeometricSeries` for scalable building costs.
- Do not loop once per purchased building for bulk operations.
- Every resource needs a source and a sink before release.
- Every research needs a gameplay effect.
- Derive research costs from target duration and expected ResearchPower.
- Avoid unexplained adjacent cost jumps above roughly one order of magnitude.
- Do not use extreme ExpantaNum notation merely because it is available.
- Preserve old resources as useful inputs in later eras.

## Asset rules

- Stable IDs and `.meta` GUIDs must be preserved.
- Create/migrate ScriptableObject assets through Unity Editor code where practical.
- Never hand-edit GUIDs.
- Do not rename serialized enum values without a tested migration.
- Fix `TinOre` display text without changing its stable ID.
- Do not retain debug-added Gold in new games.

## Validation

For each batch:

1. run the definition/source-sink audit;
2. run reachability tests;
3. compile Unity;
4. run EditMode tests;
5. run relevant PlayMode tests;
6. inspect Console;
7. report numerical before/after values;
8. stop if the main progression is unreachable.

Zero PlayMode tests is not a passing PlayMode validation.

## Reporting

Report:

- TODO IDs;
- changed scripts and assets;
- stable IDs added;
- reachability result;
- source/sink result;
- expected and simulated pacing;
- compile/tests/Console;
- P40 Pro real-device status;
- remaining blockers.

If Unity was not run, state exactly: `未执行真实 Unity 编译。`
If the device was not tested, state exactly: `未执行 Huawei P40 Pro 真机验收。`
