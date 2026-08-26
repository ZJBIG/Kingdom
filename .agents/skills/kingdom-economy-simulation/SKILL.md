---
name: kingdom-economy-simulation
description: Mandatory Kingdom analysis workflow for technology trees, research reachability, resource production, building costs, era transitions, economy balance, population economy, and simulation reports. Use before changing any Kingdom Research, Resource, Building, TechLevel, Workshop, production, consumption, or balance data.
---

# Kingdom economy simulation

## Current evidence boundary

`tools/EconomySimulator` is currently frozen and its CLI is disabled. Its
standalone outputs under `data/economy-simulation` are historical diagnostics,
not current pacing, balance, progression, or Unity acceptance evidence. Do not
run it or tune content from its output unless the user explicitly reactivates
it after a fresh Unity-parity review. Use real Unity runtime/PlayMode evidence
and player playtests as the authority.

## Mandatory order

1. Inspect `git status` and preserve existing work.
2. Read `.codex/prompts/CODEX_ECONOMY_PROMPT.md`, the nearest `AGENTS.md`, current runtime
   rules, and actual definition assets.
3. Run the static closure check before editing economy data:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\tools\codex\content-closure-check.ps1
   ```

4. Do not build or run the standalone simulator while it is frozen. Verify
   affected behavior with Unity runtime/PlayMode tests instead.

5. Treat existing `PacingAcceptance.txt`, Workshop purchases, warnings, and
   milestone summaries as frozen diagnostics only; never use them to tune
   current pacing or claim runtime acceptance.
6. Classify runtime failures as definition graph, producer deadlock, resource shortage,
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
- Simulation output is frozen historical diagnostics, not current balance
  evidence and not Unity runtime acceptance.

## Required outputs

Historical route outputs may be retained for diagnostics, but no current route
report or `PacingAcceptance.txt` should be generated while the simulator is
frozen. Never rewrite or reinterpret the retained files as current evidence.

## Validation boundaries

- Static closure and Unity runtime behavior are the active gates.
- Self-tests and parity tests for the frozen simulator are maintenance-only and
  do not authorize balance tuning.
- Zero PlayMode tests is not acceptance.
- Never change IDs, GUIDs, coordinates, descriptions, unrelated effects, or
  `.meta` files without explicit authorization.
- Food remains the only capped stockpile.

Report changed files, field-level before/after values, dependencies, closure,
Unity/runtime results, tests, unresolved blockers, and validation limits. State
explicitly when no Unity runtime or Huawei P40 Pro device was used.

If Unity was not run, state exactly:

```text
未执行真实 Unity 编译。
```

If the device was not tested, state exactly:

```text
未执行 Huawei P40 Pro 真机验收。
```
