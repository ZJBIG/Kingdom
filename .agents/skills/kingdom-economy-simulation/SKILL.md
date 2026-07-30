---
name: kingdom-economy-simulation
description: Mandatory Kingdom analysis workflow for technology trees, research reachability, resource production, building costs, era transitions, economy balance, population economy, and simulation reports. Use before changing any Kingdom Research, Resource, Building, TechLevel, Workshop, production, consumption, or balance data.
---

# Kingdom economy simulation

Use this skill for any Kingdom request involving technology trees, research reachability, resource loops, building production or costs, era progression, population economy, balance, pacing, bottlenecks, simulators, or balance reports.

## Mandatory order

1. Inspect `git status` and preserve all existing user changes.
2. Read the current runtime rules in `Kingdom/Assets/Resources/Script`, the relevant `AGENTS.md`, and the actual `Assets/Resources/Datas` definitions.
3. Run the static closure check before editing data:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\tools\codex\content-closure-check.ps1
   ```

4. Run or update the standalone simulator under `tools/EconomySimulator/`. Do not launch Unity for the analysis pass. The simulator must model current `GameBootstrap` and runtime rules, including starting resources, starting research and TechLevel, prerequisite research, research resource costs, resource production/consumption, clamped inventory, construction costs, geometric cost growth, multiple buildings, and player strategy.
5. Emit evidence before proposing or applying balance changes. At minimum produce `EconomySimulationReport.md`, `SimulationTimeline.csv`, and `BalanceWarnings.csv`; for staged balance work preserve Before/After snapshots.
6. Classify each blocker as an unreachable definition graph, resource producer deadlock, resource shortage, construction wait, research wait, player strategy/expansion choice, or simulator mismatch.
7. Only after analysis, modify the minimum allowed fields. Never change IDs, GUIDs, coordinates, descriptions, unrelated effects, or `.meta` files without explicit authorization.
8. Re-run the same closure and simulation checks after edits. Do not claim reachability or pacing from static inspection alone.

## Graphs that must be checked together

- Research prerequisite graph: detect cycles and unreachable research.
- Research resource graph: trace each cost to reachable producers.
- Building required-research graph: trace every required building unlock.
- Resource producer graph: include producer research and construction costs.

For every failure, print the complete trace:

```text
Research -> required resource -> producer building -> required research -> Research
```

Do not fix a single reported research by deleting a prerequisite before checking all four graphs.

## Simulation requirements

- Use the real runtime tick and formulas when known; use seconds internally when runtime uses seconds and aggregate reports in minutes.
- Apply production satisfaction and resource clamping exactly as runtime does.
- Pay research costs progressively if runtime does so; do not treat them as a free start-time check.
- Treat one-minute reports as output aggregation, not permission to omit sub-tick behavior.
- Compare Normal, Fast, and Conservative routes when pacing is under investigation.
- Report era-entry and completion time, research completion, first/second core-building time, minimum stockpiles, zero-resource duration, research/building wait time, and bottlenecks.
- Detect negative resource flow, unique producer bottlenecks, multiplier combinations, research pacing anomalies, and buildings whose costs exceed current production capacity.

## Required closeout

Report changed files and field-level before/after values, dependency changes, simulation results, unresolved blockers, and validation limits. If Unity was not run, state exactly:

```text
未执行真实 Unity 编译。
```

If the device was not tested, state exactly:

```text
未执行 Huawei P40 Pro 真机验收。
```
