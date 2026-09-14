# Kingdom3 UI-Ready and redesign execution plan — historical

> 历史计划：以下内容保留原始设计顺序，不是当前待实施清单。`ToDoList_New.txt`、新增独立刷新Manager、统一SetActive迁移等属于旧上下文，不能由本文推断仍缺实现。当前工作从根 `AGENTS.md` 经项目开发主入口进入UI分支；生命周期事实见 `docs/architecture/ui-boundaries.md`。

Original context: this plan was subordinate to repository code and `ToDoList_New.txt` at the time.

## Phase 1 — simulation correctness

- implement exact per-tick resource satisfaction;
- separate food integration from calendar day advance;
- bound severe catch-up backlog;
- add deterministic EditMode tests.

Do not modify UI appearance in this phase.

## Phase 2 — save resilience

- make candidate load application transactional;
- fall back to backup on semantic/apply failure;
- make repeated Load idempotent;
- add save regression tests.

## Phase 3 — UI refresh centralization

- add one UI refresh manager;
- remove Update from ResourceViewer, BuildingViewer and ResearchViewer;
- remove HUD and MusicViewer UI refresh coroutines;
- retain State.Version skipping;
- remove OnEnable scene searches where practical.

## Phase 4 — layout migration

- migrate resource rows/categories;
- migrate building card/requirements;
- introduce ResearchLineView and structured research detail fields;
- remove manual sizeDelta formulas and hidden card parents.

## Phase 5 — UI-Ready gate

- clean background/empty scripts/naming;
- add PlayMode UI lifecycle tests;
- manually disable viewers and inspect Console/Profiler;
- only after all pass, switch navigation to SetActive and delete off-screen constants.

## Phase 6 — UI design system

- agree visual direction and target resolutions;
- create UITheme and common components;
- redesign HUD/navigation first;
- redesign Resource, Building, Research and Settings pages;
- add tooltip/toast/confirm dialog.

## Phase 7 — acceptance

- responsive layouts;
- accessibility states;
- PlayMode interaction tests;
- Profiler and one-hour memory run;
- final screenshots and manual checklist.

Each phase must compile and pass relevant tests before the next phase.
