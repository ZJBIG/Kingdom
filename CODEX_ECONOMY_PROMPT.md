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
