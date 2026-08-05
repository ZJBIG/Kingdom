---
name: kingdom-ui-redesign
description: Implement and verify Kingdom Unity UI and ResearchTreeSK-style research graph changes for the Huawei P40 Pro landscape target.
---

# Kingdom UI redesign

Keep gameplay calculations, Manager rules, definition IDs, save data, and
research/resource formulas unchanged.

Canvas setup is part of the graph contract: the active CanvasScaler must use
`ScaleWithScreenSize`, reference resolution `2640x1200`, and match width for
the Huawei P40 Pro landscape composition. A fixed-pixel CanvasScaler is not
acceptable because it can make the page between the navigation and detail
panels have a negative width, invalidating every graph and drag measurement.

Use `D:/Verse/RimworldMods/#HSK/ResearchTree_SK` and its `ResearchTreeSK.dll`
or checked-in `ResearchTreeSK.il` as the authoritative visual and interaction
reference. Preserve `NodeSize=(205,50)`, `NodeMargins=(50,10)`,
`NodeFullSize=(255,60)`, `CurveRadius=10`, `LineThickness=4`, and
`ArrowThickness=16`; reuse its line, curve, arrow, era, and progress textures.

Research graph rules:

- Build positions from `Prerequisites`, preferably as a deterministic
  left-to-right integer topological grid.
- If authored coordinates are required, round them to integer cells and repair
  duplicates and backward prerequisite columns deterministically; preserve
  both authored axes wherever the repair does not require moving a node.
- Keep nodes at the reference size and connectors behind nodes.
- Treat graph positions as ResearchTreeSK top-left coordinates (row 0 is the
  top lane); convert to Unity's bottom-left RectTransform origin exactly once
  for both nodes and connector parts.
- Share integer grid connector segments as bus lines and preserve prerequisite
  direction. Selecting a node highlights its complete transitive prerequisite
  closure and all connecting segments.
- Do not add blank spacer rows to manufacture scrolling. Pan only when measured
  content exceeds the measured viewport.
- Attach a drag forwarder to each research Button so node-started drags reach
  the graph gesture owner while short releases still invoke node clicks.
  Log whether topology was accepted and why an integerized asset fallback was
  selected, including duplicate, backward-edge and inversion counts.

Touch and isolation rules:

- Implement panning like `ButtonInvisibleDraggable`: dragging a node or blank
  graph area moves the whole graph while a short release still activates it.
  The graph must provide a transparent raycastable drag surface behind nodes;
  relying on an empty RectTransform or on ScrollRect alone is insufficient for
  Unity's pointer-drag initialization.
- Clamp both axes to real content bounds and keep pinch zoom independent of
  topology.
- Generate the new UI under `KingdomUIRoot/SafeAreaRoot`; disable or destroy
  legacy children before building it so old viewers cannot render or receive
  input.

Verification must include C# and Unity compilation plus logs proving node count,
unique integer cells, forward prerequisite columns, positive viewport/content
sizes, real pan ranges, active drag owner, inactive legacy viewer, and no null,
missing-reference, or prefab-import errors. Do not claim PlayMode, visual, or
Huawei P40 Pro acceptance without direct evidence.
The targeted runtime audit is
`ResearchTree_RuntimeLayoutAndOverflow_AreLoggedAndNonOverlapping`; run it
with Unity's PlayMode test runner and retain its log when graph behavior
changes.
