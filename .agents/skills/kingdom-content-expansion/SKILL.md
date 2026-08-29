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

1. `.codex/prompts/CODEX_ECONOMY_PROMPT.md`
2. `docs/balance/no-resource-caps.md`
3. `docs/balance/balance-model.md`
4. `docs/content/progression-roadmap.md`
5. `docs/testing/content-balance-tests.md`
6. current code, assets, `data/content-closure-static.md`, and only the current
   root/Fast/Normal/Conservative simulation outputs; ignore dated historical
   iteration, audit, baseline, and before/after snapshot reports

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
- Every new resource must follow the long-term resource loop: one reachable
  source, at least two meaningful sinks or one strategic sink, a visible unlock
  path, and a role that remains useful after its first era.
- High-tech resources must be consumed by high-tech research, Workshops,
  buildings, and late campaigns. Do not create a resource that is only a
  construction fee or a one-time research requirement.
- Late production should deliberately reuse earlier resources. A valid
  advanced chain may combine an advanced structural resource with nickel,
  titanium alloy, composite, chemical materials, electronics, glass, rubber,
  cloth, or other earlier inputs so that old industries retain strategic value.
- Space and later-era buildings should use advanced structural materials in
  their construction and maintenance. Higher-tier Workshops should also pay
  those materials before granting multipliers.
- Interstellar warfare and territory expansion are long, supply-heavy
  operations. Campaigns should consume food, logistics-linked resources, fuel,
  and appropriate advanced materials over time, while successful late campaigns
  must provide rewards large enough to justify the sustained investment; do not
  leave late rewards at trivial hundreds or low thousands.
- PhantomAlloy and PhantomWeave are examples of acceptable late resources:
  they must be produced from existing advanced and earlier materials and then
  consumed by deep-space structures, Workshops, research, or campaigns.

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
