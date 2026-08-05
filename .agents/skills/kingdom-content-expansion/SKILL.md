---
name: kingdom-content-expansion
description: Use for Kingdom gameplay content, balance, progression, research, buildings, resources, population, territory, industrialization, space, alien war, and numerical economy work. Do not use for pure UI styling or ExpantaNum internals.
---

# Kingdom content expansion

## Scope

Use this skill for gameplay content and progression design. Do not use it for
pure UI styling, audio work, or ExpantaNum internals.

For any Research, Resource, Building, TechLevel, Workshop, production,
consumption, reachability, pacing, or balance work, also use the canonical
`kingdom-economy-simulation` skill before changing definitions.

## Read first

1. `CODEX_ECONOMY_PROMPT.md`
2. `docs/balance/no-resource-caps.md`
3. `docs/balance/balance-model.md`
4. `docs/content/progression-roadmap.md`
5. `docs/testing/content-balance-tests.md`
6. current code, assets, closure report, and simulation reports

Do not require missing historical TODO or proposed CSV files. Current repository
state and freshly generated evidence outrank dated audits.

## Locked design rules

- Food is the only capped stockpile.
- Ordinary resources never receive capacity fields, storage buildings, or hidden clamps.
- Population and productivity are player-facing systems; do not reintroduce workforce.
- Stable IDs and Unity GUIDs must be preserved.
- New resources need a reachable source and meaningful sinks.
- New research needs a real effect.
- New buildings need reachable inputs, geometric cost growth, a defined role,
  and a first-copy payback target.
- Old resources should remain useful in later eras.

## Delivery order

1. Reproduce and fix gameplay bugs in current runtime code and assets.
2. Prove closure and validate affected behavior in Unity.
3. Complete and validate the active era slice, using the simulator only for regression evidence.
4. Expand later eras only after earlier acceptance gates pass.

Do not bulk-generate future assets and do not tune balance from a simulator that
fails its input or relevant parity tests. Do not extend simulator strategies unless
explicitly requested.

## Validation

Follow `kingdom-economy-simulation` for closure, simulator, build, Unity tests,
Console, and report requirements. Zero PlayMode tests is not acceptance.

If Unity was not run, state exactly: `未执行真实 Unity 编译。`
If the device was not tested, state exactly: `未执行 Huawei P40 Pro 真机验收。`
