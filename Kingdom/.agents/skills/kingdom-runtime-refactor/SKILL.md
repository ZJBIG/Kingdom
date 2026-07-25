---
name: kingdom-runtime-refactor
description: Use for Kingdom repository work involving SimulationManager, resource satisfaction, food/calendar timing, Runtime State, Manager transactions, research, save/load/bootstrap, ExpantaNum boundaries, deterministic tests, or performance. Kingdom3 already completed BigNumber and Pair migration; do not repeat them.
---

# Kingdom runtime correctness

## Required reading

- `/AGENTS.md`
- `/ToDoList_New.txt`
- `/docs/repository-map.md`
- `/docs/audits/kingdom3-static-audit.md`
- `/docs/architecture/runtime-state.md`
- `/docs/rules/kingdom-rules.md`
- `/docs/testing/acceptance-checklist.md`
- nearest scoped `AGENTS.md`

## Current baseline

Completed:

- ExpantaNum is the only numeric type;
- resource/value definitions use `Pair<Resource,ExpantaNum>`;
- four Runtime State classes exist;
- Managers own State and do not own UI;
- SimulationManager and ManualTick exist;
- stable IDs, DataBase, Bootstrap and unified SaveManager exist;
- EditMode regressions exist.

Do not add compatibility wrappers or restart completed migrations.

## Priority defects

1. Building efficiency currently treats any positive inventory as fully satisfied.
2. Food integration is coupled to the calendar step.
3. catch-up backlog policy is incomplete.
4. save application failure does not transactionally fall back to backup.
5. repeated Load needs idempotent tests.

## Workflow

1. inspect git status and current files;
2. run guidance validation and static audit;
3. compile the unmodified baseline;
4. write a failing regression;
5. make the smallest architecture-consistent change;
6. compile and run relevant EditMode tests;
7. run PlayMode tests when lifecycle/Scene is touched;
8. report evidence and unverified items.

## Simulation rules

- explicit deltaSeconds;
- deterministic stable order;
- no dictionary-order dependence;
- no LINQ/closures/repeated Parse/temp collections in Tick;
- bounded reusable buffers allowed;
- inventories never finish below zero;
- actual consumption never exceeds availability;
- ManualTick remains directly testable.

## Transaction rules

Normalize, calculate, validate, then commit. Failure causes zero partial mutation.

## Save rules

- stable IDs and non-derivable values only;
- ExpantaNum `ToString()` only;
- candidate load must not leave partial State;
- main failure may try backup;
- derived rates rebuild after load.

## Completion report

Include files, TODO IDs, compile/test evidence, log/XML paths, Console state, manual work and blockers. If Unity was not run, write `未执行真实 Unity 编译。`
