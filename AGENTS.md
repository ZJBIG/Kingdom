# Kingdom repository instructions

## Repository identity

- Repository: `Kingdom`
- Audited baseline: `Kingdom5.7z`
- Baseline date: `2026-07-25`
- Unity Editor: `2022.3.62f3c1`
- Primary scene: `Assets/Scenes/SampleScene.unity`
- Target device: Huawei P40 Pro, landscape mobile build
- Source of truth: current repository, then `.codex/prompts/CODEX_ECONOMY_PROMPT.md`, then current generated reports and `docs/`.
- Codex layout: keep the discovery entry `AGENTS.md`, project skills under
  `.agents/skills/`, and executable project helpers under `tools/codex/`.
  Store Codex-only prompts, archive metadata, and recoverable historical
  outputs under `.codex/`.

Historical Kingdom3/Kingdom4 audits are context only and must not override current code.

## Current milestone

Core numeric/runtime/save/UI foundations largely exist. Do not redo BigNumber, Pair, Runtime State, Manager/UI split or stable-ID migrations.

The current milestone is playable content and gameplay correctness:

1. keep static progression closure passing;
2. investigate reproducible gameplay bugs in runtime code and current assets;
3. complete and validate the active era's production, research, Workshop and progression loops;
4. keep the existing offline simulator as a regression tool, without further strategy development;
5. fix pacing only after the corresponding gameplay behavior is correct in Unity.

## Required reading for content work

1. `.codex/prompts/CODEX_ECONOMY_PROMPT.md`
2. `.agents/skills/kingdom-content-expansion/SKILL.md`
3. `.agents/skills/kingdom-economy-simulation/SKILL.md`
4. `docs/balance/no-resource-caps.md`
5. `docs/balance/balance-model.md`
6. `docs/content/progression-roadmap.md`
7. `docs/testing/content-balance-tests.md`
8. current closure and simulation reports

Current evidence is limited to `data/content-closure-static.md`, the files
directly under `data/economy-simulation`, its `Fast`, `Normal`, and
`Conservative` subdirectories, and `TestResults/Latest-Test-Errors.txt`.
Anything under `.codex/archive/` is recoverable historical material and must
not be cited as current evidence. Dated iteration, round-audit, baseline,
before/after snapshot directories, old exports, logs, and build intermediates
are historical artifacts.
9. the nearest scoped `AGENTS.md`

Use:

- `kingdom-content-expansion` for gameplay, balance and progression;
- `kingdom-economy-simulation` is mandatory for any Research, Resource, Building,
  TechLevel, Workshop, production, consumption, economy, reachability, pacing or
  balance task. Run the static closure check and offline simulator before changing
  definitions.
- the nearest scoped `AGENTS.md` plus `docs/architecture/runtime-state.md`,
  `docs/architecture/serialized-pairs.md`, and
  `docs/architecture/ui-boundaries.md` for State/Manager/save/simulation
  correctness;
- `kingdom-ui-redesign` for visual/UI work.

For Research UI work, `kingdom-ui-redesign` is the source of truth for the
ResearchTreeSK reference path, integer-grid layout, shared bus connectors,
independent touch dragging (including a raycastable background drag surface),
measured scroll bounds, and legacy UI isolation.
The graph's integer rows use ResearchTreeSK's top-left coordinate convention;
nodes and connector parts must share one explicit conversion to Unity's
bottom-left RectTransform space.
Research nodes must forward drag lifecycle events to the graph gesture owner
so a Button cannot swallow a drag that starts on a node; short release remains
a click. Log measured overflow per axis before claiming vertical scrolling.
Also log the topology decision and duplicate/backward-edge/inversion counts;
an asset-grid fallback must be explainable from those counts.
Do not claim those behaviors from static intent alone; require runtime logs.
The dedicated PlayMode audit is
`ResearchTree_RuntimeLayoutAndOverflow_AreLoggedAndNonOverlapping`; it must
report 79 unique node cells, positive viewport/content bounds, and actual
content movement before graph interaction is considered verified.
The active CanvasScaler is part of that contract: ScaleWithScreenSize,
2640x1200 reference resolution, Match Width; ConstantPixelSize is forbidden.

Non-essential UI components must be authored in the relevant Scene or Prefab.
Runtime code may find existing components and bind data, state, and events, but
must not hard-code their visual hierarchy, dimensions, labels, or create them at
runtime. Runtime generation is reserved for genuinely data-driven repeated
content (such as research nodes and connector segments) or an explicitly
documented fallback that is required for compatibility.

The canonical economy skill is `.agents/skills/kingdom-economy-simulation/SKILL.md`.
Do not use a duplicate economy skill under `.agents/skills`.

## Non-negotiable economy rule

Food is the only capped stockpile.

Do not add capacity, MaxAmount or storage buildings for ordinary resources. Use geometric costs, production chains, research, productivity, territory, power, logistics and combat as progression gates. Do not reintroduce workforce.

## Long-term content decisions

- Before adding content, check whether an existing Research, Workshop, Building
  or Resource can take the role. Prefer completing missing connections between
  existing definitions over adding parallel content.
- After entering Spacer, do not add an orbital replacement factory for each
  industrial resource. Keep lower-era industry valuable through building
  upgrades, Workshops, Research and higher efficiency.
- Sector long-term output must not replace player-built advanced production.
  Sectors primarily provide territory, raw materials, one-time loot and limited
  strategic resource flows rather than becoming the unlimited main source of
  advanced processed materials.
- Whenever population capacity increases, estimate
  `PopulationCapacity * FoodConsumptionPerPerson` at full load and verify that
  a reasonable number of same-era Food producers can support it.

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

## Current known blockers

- Static closure currently passes through Industrial; preserve it.
- Offline pacing acceptance currently fails and remains diagnostic evidence, not the development focus.
- Full gameplay behavior still requires Unity compilation, tests and direct runtime evidence.
- Do not extend simulator strategies, route scoring or decision-trace features unless explicitly requested.
- Do not use dated Kingdom5 audit blocker lists as current facts.

## Number rules

- Building material costs should use geometric growth.
- Bulk purchases must use closed-form ExpantaNum extension functions.
- Research costs are derived from target duration and expected ResearchPower.
- Do not introduce extreme notation before content progression justifies it.
- Keep old resources useful in later eras.
- Record before/after values and simulated pacing.

## Numeric assertion rules

- Numeric assertions must not use exact equality by default (`Is.EqualTo`, `Assert.AreEqual`, or equivalent). Prefer relationship assertions, ranges, or tolerance-based approximate assertions.
- Exact numeric equality is allowed only when exactness is semantically required, such as a discrete count, protocol constant, stable ID, enum/version value, or save-compatibility field. The test or an adjacent comment must make that reason clear.
- Do not hard-code exact equality for floating-point, `ExpantaNum`, production, consumption, cost, pacing, or other calculated values merely for convenience.

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

Keep generated evidence bounded: retain only the current summary outputs and
the latest `TestResults/Latest-Test-Errors.txt`. Move older test exports,
dated simulation snapshots, logs, and build intermediates to
`.codex/archive/`; do not permanently delete them during routine cleanup.

The existing PlayMode report contains zero test cases and is not evidence of PlayMode acceptance.

If Unity is unavailable, state: `未执行真实 Unity 编译。`
