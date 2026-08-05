# Research tree UI progress

## Current change

- Research positions are generated from `Prerequisites` on an integer grid;
  authored `Research.x` and `Research.y` are not read by the UI.
- The topology pass now allocates contiguous left-to-right column blocks per
  `TechLevel`. A node is still moved to the right when any prerequisite
  requires it, so prerequisite direction remains forward.
- Selection closure is recursive, uses stable research IDs, and a connector
  is considered focused only when the target contains that exact prerequisite.
  This prevents a shared bus segment from being treated as an unrelated edge
  merely because both endpoints happen to be in the focused set.
- After integer placement, cross-column edges are checked for third-party
  nodes on the target row. Blocked targets move to the next free integer row,
  preventing a glass-bus segment from visually passing through another card.

## Evidence

- Static C# build: `dotnet build Kingdom\\Assembly-CSharp.csproj
  --no-restore --nologo -v:minimal` completed with 0 errors and 2 existing
  Unity InputSystem reference warnings.
- Latest Unity log contains 79 nodes, 157 direct links, 0 duplicate cells and
  0 backward prerequisite columns. The latest graph bounds were positive and
  content width exceeded the viewport, so horizontal panning remains real.
- After forcing an Asset Refresh and rerunning the scene in the existing Unity
  instance, the latest log reports era blocks `Animal 1-5`, `Neolithic 7-11`,
  `Medieval 13-16`, `Industrial 18-31`, `Spacer 33-35`, 79 unique nodes,
  `backwardsEdges=0`, and content `(9180,1140)` against viewport `(1489,808)`.
- The final refreshed run additionally reports
  `intermediateNodeCrossings=0`; no backward-edge diagnostic is emitted.
- Selecting `ModernUniversity` in the live scene reports
  `InterstellarNavigationIncluded=False` in the current prerequisite closure.
- No new exception appears after the latest research-page rebuild. Older
  `SaveManager` null-reference entries remain earlier in `Editor.log` and are
  not from this change.

## Next runtime check

The latest Unity run now provides the required era and closure evidence. A
Huawei P40 Pro physical-device acceptance run has not been performed.

## Latest detail-panel change

- Research graph top-left coordinates now include two complete grid rows of
  reserved space (`2 * ResearchGridY`) before row zero. The latest Unity log
  reports node top range `125` and content height `1260`, so the first row is
  draggable into view instead of being clipped against the viewport edge.
- Research details now create a `Payment` button at the fixed detail-panel
  slot above the queue action button. It calls the existing
  `ResearchManager.PayResearchCost` API and refreshes the selected details;
  buildings and generic details hide it. The queue action text resolves to
  join/delete queue based on the selected research state.
- The current Unity Device Simulation visibly shows the green `支付资源`
  button above the orange `加入研究队列` button in the right detail panel.
- The same run visibly shows the graph's first row below the reserved top
  space; runtime bounds report `range=(280,125)-(8950,1205)` and
  `content=(9180,1260)`.

## Detail panel visibility fix

- Resource rows now open a dedicated resource presenter instead of the generic
  text presenter. It shows description, current stock, production, 
  consumption and net change in the right panel.
- Building details now explicitly place the requirement viewport after the
  description and label the sections `产出 / 消耗` and `建筑建造需求`.
- Research details now label the description and the payment section as
  `研究说明` and `研究支付需求`; requirement rows remain generated from the
  research definition and show current owned amounts.
- Fixed a live-refresh null reference when an active research state has no
  definition. The current research label now remains stable while the rest of
  the UI continues refreshing.
- Verified in the existing Unity Device Simulation: resource detail shows
  stock/production/consumption, building detail shows description plus flow
  and construction rows, and research detail shows payment rows and the
  payment/queue actions. No new exception appears in the latest Editor.log
  tail.
- Building detail sections now use measured sequential slots: description,
  production/consumption flow, then construction requirements. The flow and
  requirement viewports no longer share the old fixed coordinates; each
  section height is derived from its row count and excess rows remain inside
  that section's ScrollRect. The latest Unity run visibly shows the two
  headings and rows separated without overlap.
