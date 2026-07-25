# Kingdom repository instructions

## Repository identity

- Repository: `Kingdom`
- Audited baseline: `Kingdom5.7z`
- Baseline date: `2026-07-25`
- Unity Editor: `2022.3.62f2c1`
- Primary scene: `Assets/Scenes/SampleScene.unity`
- Target device: Huawei P40 Pro, landscape mobile build
- Source of truth: current repository, then the current task-specific ToDoList, then `docs/`.

Historical Kingdom3/Kingdom4 audits are context only and must not override current code.

## Current milestone

Core numeric/runtime/save/UI foundations largely exist. Do not redo BigNumber, Pair, Runtime State, Manager/UI split or stable-ID migrations.

The current milestone is gameplay content and balance:

1. validate progression reachability;
2. fix the blocked Animal/Neolithic production chains;
3. introduce scalable building costs and research effects;
4. complete a playable Animal -> Neolithic -> Medieval vertical slice;
5. add population and territory;
6. then expand Medieval, Industrial, Space and alien war.

## Required reading for content work

1. `ToDoList_Content_Next_2026-07-25.txt`
2. `.agents/skills/kingdom-content-expansion/SKILL.md`
3. `docs/audits/kingdom5-content-static-audit.md`
4. `docs/balance/no-resource-caps.md`
5. `docs/balance/balance-model.md`
6. `docs/content/progression-roadmap.md`
7. `docs/testing/content-balance-tests.md`
8. the nearest scoped `AGENTS.md`

Use:

- `kingdom-content-expansion` for gameplay, balance and progression;
- `kingdom-runtime-refactor` for State/Manager/save/simulation correctness;
- `kingdom-ui-redesign` for visual/UI work.

## Non-negotiable economy rule

Food is the only capped stockpile.

Do not add capacity, MaxAmount or storage buildings for ordinary resources. Use geometric costs, production chains, research, workforce, territory, power, logistics and combat as progression gates.

## Content quality gates

A new Resource requires:

- stable ID;
- source;
- at least two sinks or one strategic sink;
- reachable unlock path;
- UI category/description;
- validation test.

A new Research requires at least one real effect.

A new Building requires:

- reachable construction inputs;
- defined role;
- first-copy payback target;
- cost growth;
- source/sink effect;
- mobile-safe UI representation.

## Current known content blockers

- Clay has no producer.
- PlantFiber has no producer.
- CopperOre/TinOre/IronOre have no mines.
- PotteryKiln and WeavingWorkshop do not produce processed goods.
- StoneTool has no production loop.
- Smithing_Bronze does not use Tin.
- SmithingRevolution does not advance the tech level.
- GameBootstrap adds Gold as debug content.
- 22 of 35 resources are currently outside the active economy.
- current static progression cannot leave the Animal era.

## Number rules

- Building material costs should use geometric growth.
- Bulk purchases must use closed-form ExpantaNum extension functions.
- Research costs are derived from target duration and expected ResearchPower.
- Do not introduce extreme notation before content progression justifies it.
- Keep old resources useful in later eras.
- Record before/after values and simulated pacing.

## Unity and Git safety

- Preserve `.meta` and GUIDs.
- Prefer Editor migrations for ScriptableObject changes.
- Do not install packages or upgrade Unity without permission.
- Inspect `git status`; do not discard user work.
- Do not push, rebase, amend or force operations.
- Do not enter a later content milestone with compile errors or unreachable main progression.

## Validation

Each batch must include:

- definition validation;
- resource source/sink audit;
- progression reachability;
- Unity compilation;
- EditMode tests;
- relevant PlayMode tests;
- Console inspection;
- numerical pacing report.

The existing PlayMode report contains zero test cases and is not evidence of PlayMode acceptance.

If Unity is unavailable, state: `未执行真实 Unity 编译。`
If no P40 Pro was used, state: `未执行 Huawei P40 Pro 真机验收。`
