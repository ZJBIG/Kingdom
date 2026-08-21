# Kingdom economy continuation prompt

Continue from the current `Kingdom` worktree. Do not rely on dated handoff
numbers or restore historical Kingdom3/Kingdom4 assumptions.

Before changing Research, Resource, Building, TechLevel, Workshop, production,
consumption, pacing, or balance:

1. read `.agents/skills/kingdom-content-expansion/SKILL.md` and
   `.agents/skills/kingdom-economy-simulation/SKILL.md`;
2. inspect `git status` and current runtime/assets;
3. run the static closure check;
4. reproduce the affected gameplay behavior in code or Unity tests;
5. run static closure and the existing simulator only when the change affects
   reachability, production, consumption or pacing;
6. inspect `PacingAcceptance.txt`, Workshop purchases, warnings, and milestones
   as regression evidence rather than as the primary development target.

Report retention: use `data/content-closure-static.md`, only the current
`data/economy-simulation` root outputs and its `Fast`, `Normal`, and
`Conservative` subdirectories, plus `TestResults/Latest-Test-Errors.txt` as
current evidence. Anything under `.codex/archive/` is recoverable historical
material and must not be cited as current state.

Current locked rules:

- Food is the only capped stockpile.
- Do not reintroduce workforce; population and productivity are the player-facing systems.
- Research resource costs are paid atomically before progress begins.
- The simulator must load Resource, Building, Research, and Workshop definitions
  strictly through stable IDs and `.meta` GUID resolution.
- Workshop unlocks, prerequisites, purchases, costs, and effects are part of pacing.
- Runtime constants come from current code, not this prompt.
- Static reachability does not prove pacing, and simulator output does not prove Unity acceptance.
- Keep current simulator strategies frozen. Do not add route scoring, decision AI,
  or trace features unless explicitly requested.

Resource and late-era design contract:

- A resource is allowed only when it has a reachable source, a visible unlock
  path, at least two durable sinks or one strategic sink, and a purpose that
  survives into later eras. Do not add resources merely to label another tier.
- Advanced resources must be used by advanced Research, Workshops, Buildings,
  and late campaigns. Their production should consume both advanced inputs and
  selected earlier-era inputs, so older resources remain valuable rather than
  becoming obsolete immediately after an era transition.
- TitaniumAlloy is a high-strength structural resource and should be used
  throughout Space construction, high-tier Research, advanced Workshops, and
  interstellar logistics. Nickel and other underused industrial resources should
  feed later alloy or phase-material chains instead of remaining isolated.
- PhantomAlloy and PhantomWeave are valid examples of post-Space materials:
  they require a real production building and a research gate, consume existing
  industrial/high-tech inputs, and must have multiple Space or warfare sinks.
- Interstellar campaigns must be materially and temporally expensive. Maintain
  ongoing food, fuel, logistics, and advanced-material costs, require adequate
  military/logistics satisfaction for reliable progress, and provide territory
  and resource rewards at a scale appropriate for late-game investment rather
  than trivial hundreds or low thousands.
- Never add ordinary-resource capacity or storage caps to solve the cost of
  these late campaigns; use production chains, supply, logistics, territory,
  research, and strategic resource consumption instead.

Long-term content decisions:

- Before adding content, first check whether an existing Research, Workshop,
  Building, or Resource can take the role. Prefer completing missing links
  between existing definitions over adding parallel content.
- After entering Spacer, do not create a simple orbital replacement factory
  for every industrial resource. Lower-era industry must continue serving
  later-era demand through upgraded buildings, Workshops, Research, and higher
  efficiency.
- Sector long-term output must not visibly replace player-built advanced
  production chains. Sectors should primarily provide territory, raw materials,
  one-time loot, and limited strategic resource flows, not unlimited advanced
  processed materials.
- Any population-capacity increase must be checked with
  `PopulationCapacity * FoodConsumptionPerPerson` at full load, and the same
  era must have a reasonable number of Food producers able to support that
  demand.

UI research-tree boundary:

- Research UI may read definitions and runtime state, but must not change
  research rules, costs, IDs, save format, or progress calculations.
- Use the external ResearchTreeSK package as the visual and interaction
  reference.
- Use a deterministic integer prerequisite graph, shared bus connector
  segments, a raycastable background drag surface for direct graph dragging,
  and measured viewport/content bounds. Do not
  add blank spacer rows to manufacture scrolling.
- Keep the graph's top-left integer row coordinate consistent for nodes and
  connectors; convert to Unity's bottom-left RectTransform space exactly once.
- Research node Buttons must forward drag lifecycle events to the graph gesture
  owner; short release remains a click. Enable panning per axis only when
  measured graph content exceeds the viewport on that axis.
- Log topology acceptance and all rejection counts before using integerized
  authored coordinates as a fallback.
- When the graph must fall back to authored coordinates, round both x and y to
  integer cells and move only the minimum nodes needed to keep prerequisites
  strictly to the left; do not replace authored x with an unrelated depth
  column.
- Keep generated UI isolated from legacy Viewer/Displayer objects and verify
  this with Unity logs before claiming runtime acceptance.
- The graph PlayMode audit is
  `ResearchTree_RuntimeLayoutAndOverflow_AreLoggedAndNonOverlapping`; it must
  remain a real runtime test, not a static string check.
- The active CanvasScaler must use ScaleWithScreenSize at 2640x1200 and match
  width; ConstantPixelSize is prohibited for the P40 landscape UI because it
  can produce a negative research viewport between fixed navigation/detail
  panels.

Prioritize gameplay bugs and content loops in the Unity project. Do not tune assets
while relevant input or runtime parity tests are failing, and do not describe a
current FAIL report as passing.
