---
name: kingdom-economy-simulation
description: Mandatory Kingdom analysis workflow for technology trees, research reachability, resource production, building costs, era transitions, economy balance, population economy, and simulation reports. Use before changing any Kingdom Research, Resource, Building, TechLevel, Workshop, production, consumption, or balance data.
---

# Kingdom economy simulation

## Current evidence boundary

`tools/NewEconomySimulator` is a deterministic parity harness. Its output under
`data/economy-parity` is limited to snapshots, events, and first differences;
it is not pacing, balance, progression, or Unity acceptance evidence. Do not
tune content from it. Use real Unity runtime/PlayMode evidence and player
playtests as the authority.

## Mandatory order

1. Inspect `git status` and preserve existing work.
2. Read `.codex/prompts/CODEX_ECONOMY_PROMPT.md`, the nearest `AGENTS.md`, current runtime
   rules, and actual definition assets.
3. Run the static closure check before editing economy data:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\tools\codex\content-closure-check.ps1
   ```

4. Run the new simulator only for deterministic parity checks, then verify
   affected behavior with Unity runtime/PlayMode tests.

5. Treat existing `PacingAcceptance.txt`, Workshop purchases, warnings, and
   milestone summaries as frozen diagnostics only; never use them to tune
   current pacing or claim runtime acceptance.
6. Classify runtime failures as definition graph, producer deadlock, resource shortage,
   construction wait, research wait, Workshop wait, input mismatch, gameplay bug,
   or runtime parity mismatch.
7. Change only the minimum authorized source fields.
8. Re-run the same checks, Unity compilation, EditMode, relevant PlayMode, and Console.

## Simulator contract

- Input is a strict typed snapshot of Resource, Building, Research, Workshop,
  TechLevel, Sector, and runtime/save state
  assets resolved through `.meta` GUIDs.
- Missing IDs, `.meta` files, unresolved GUIDs, duplicate per-kind IDs, and
  duplicate resource pairs are hard failures; never silently omit definitions.
- Workshop unlocks, research/workshop prerequisites, resource costs, purchases,
  and effects must be modeled.
- Research costs are atomic: progress starts only after the complete remaining
  cost can be paid, matching current `ResearchManager`.
- Use current runtime constants and parity formulas. Do not copy values from dated audits.
- Simulation output is parity evidence only, not current balance evidence or
  Unity runtime acceptance.

## Required outputs

Persist only snapshots, ordered events, and first-difference facts under
`data/economy-parity/`. Do not generate route, pacing, or balance acceptance
reports.

## Validation boundaries

- Static closure and Unity runtime behavior are the active gates.
- Self-tests and parity tests are diagnostic only and do not authorize balance
  tuning.
- Zero PlayMode tests is not acceptance.
- Never change IDs, GUIDs, coordinates, descriptions, unrelated effects, or
  `.meta` files without explicit authorization.
- Food remains the only capped stockpile.

Report changed files, field-level before/after values, dependencies, closure,
Unity/runtime results, tests, unresolved blockers, and validation limits.

If Unity was not run, state exactly:

```text
未执行真实 Unity 编译。
```
