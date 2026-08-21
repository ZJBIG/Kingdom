---
name: kingdom-economy-simulation
description: Mandatory Kingdom analysis workflow for technology trees, research reachability, resource production, building costs, era transitions, economy balance, population economy, and simulation reports. Use before changing any Kingdom Research, Resource, Building, TechLevel, Workshop, production, consumption, or balance data.
---

# Kingdom economy simulation

## Mandatory order

1. Inspect `git status` and preserve existing work.
2. Read `.codex/prompts/CODEX_ECONOMY_PROMPT.md`, the nearest `AGENTS.md`, current runtime
   rules, and actual definition assets.
3. Run the static closure check before editing economy data:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\tools\codex\content-closure-check.ps1
   ```

4. Build and self-test the standalone simulator:

   ```powershell
   dotnet build .\tools\EconomySimulator\EconomySimulator.csproj --no-restore
   dotnet run --project .\tools\EconomySimulator\EconomySimulator.csproj --no-build -- --self-test
   ```

5. Run the existing simulator regression and inspect `PacingAcceptance.txt`,
   Workshop purchases, warnings, and milestone summaries when pacing is affected.
   Retain only `data/content-closure-static.md`, the current root report and
   the Fast/Normal/Conservative outputs. Dated iteration, round-audit,
   baseline, and before/after snapshot directories are historical artifacts
   and must not be treated as current evidence; they belong under
   `.codex/archive/`.
6. Classify failures as definition graph, producer deadlock, resource shortage,
   construction wait, research wait, Workshop wait, input mismatch, gameplay bug,
   or runtime parity mismatch.
7. Change only the minimum authorized source fields.
8. Re-run the same checks, Unity compilation, EditMode, relevant PlayMode, and Console.

## Simulator contract

- Input is a strict typed snapshot of Resource, Building, Research, and Workshop
  assets resolved through `.meta` GUIDs.
- Missing IDs, `.meta` files, unresolved GUIDs, duplicate per-kind IDs, and
  duplicate resource pairs are hard failures; never silently omit definitions.
- Workshop unlocks, research/workshop prerequisites, resource costs, purchases,
  and effects must be modeled.
- Research costs are atomic: progress starts only after the complete remaining
  cost can be paid, matching current `ResearchManager`.
- Use current runtime constants and parity formulas. Do not copy values from dated audits.
- Treat the current route strategies and decision traces as frozen diagnostics.
  Do not extend scoring, route AI, or trace features unless explicitly requested.
- Simulation output is balance evidence, not Unity runtime acceptance.

## Required outputs

Each route must emit:

- `EconomySimulationReport.md`
- `SimulationTimeline.csv`
- `BalanceWarnings.csv`
- `MilestoneSummary.csv`
- `WorkshopPurchaseTimeline.csv`
- research/building/upgrade timelines

The root output must include `PacingAcceptance.txt`. Never rewrite FAIL as PASS
without a fresh run satisfying every gate.

## Validation boundaries

- Static closure and dynamic pacing are separate gates.
- Self-tests and parity tests must pass before balance tuning.
- Zero PlayMode tests is not acceptance.
- Never change IDs, GUIDs, coordinates, descriptions, unrelated effects, or
  `.meta` files without explicit authorization.
- Food remains the only capped stockpile.

Report changed files, field-level before/after values, dependencies, closure,
simulation results, tests, unresolved blockers, and validation limits.

If Unity was not run, state exactly:

```text
未执行真实 Unity 编译。
```

If the device was not tested, state exactly:

```text
未执行 Huawei P40 Pro 真机验收。
```
