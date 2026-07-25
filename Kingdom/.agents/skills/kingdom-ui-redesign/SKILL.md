---
name: kingdom-ui-redesign
description: Use for Kingdom UI work involving Viewer/Displayer lifecycle, refresh scheduling, layout groups, navigation, SetActive migration, HUD, Resource/Building/Research/Settings screens, Prefabs, UITheme, interaction feedback, responsive layout, accessibility, PlayMode UI tests, or visual redesign.
---

# Kingdom UI redesign

## Required reading

- `/AGENTS.md`
- `/ToDoList_New.txt`
- `/docs/repository-map.md`
- `/docs/architecture/ui-boundaries.md`
- `/docs/plans/ui-ready-and-redesign.md`
- `/docs/ui/visual-direction.md`
- `/docs/ui/component-library.md`
- `/docs/testing/acceptance-checklist.md`
- nearest scoped `AGENTS.md`

## Gate before visual redesign

Do not begin a broad visual rewrite until:

- exact simulation defects are fixed;
- UI refresh loops are centralized;
- manual dynamic heights are removed;
- PlayMode lifecycle tests exist;
- disabling each Viewer is proven safe;
- off-screen hiding is replaced by SetActive.

Small preparatory Prefab changes needed for the gate are allowed.

## Current UI facts

- Managers no longer own UI.
- Viewers own Displayers and listen for StateAdded.
- three main Viewers still have Update refresh loops.
- HUD and MusicViewer still have UI refresh coroutines.
- resource/building cards still use manual height calculations.
- main/settings pages are moved off screen.
- ResearchLineView is absent.
- no PlayMode tests exist.

## Architecture rules

- UI renders State and invokes Manager commands only.
- no gameplay in animation, Update, coroutine or text.
- no per-card Update/coroutine.
- one bounded UI refresh scheduler.
- use State.Version to avoid repeat formatting.
- explicit serialized references only.
- layout components own dynamic sizing.
- disabled pages perform no ordinary refresh work.
- MusicManager continues independently of settings UI.

## Scene and Prefab safety

- preserve `.meta` and GUIDs;
- inspect serialized references before deletion;
- use `git mv`;
- do not claim visual completion without opening Unity;
- record any Inspector wiring and screenshots;
- do not install UI packages without permission.

## Redesign process

1. inventory Canvas, Viewers, Prefabs and screenshots;
2. pass the UI-Ready gate;
3. agree visual references and resolutions;
4. create UITheme/tokens;
5. create common components;
6. redesign HUD/navigation;
7. redesign Resource, Building, Research and Settings;
8. add tooltip/toast/confirm feedback;
9. run PlayMode, resolution and Profiler acceptance.

## Output quality

BuildFailure and status values drive feedback. Do not parse TMP text. Show explicit insufficient resource/space/productivity/input reasons. Large ExpantaNum values must remain readable.

## Completion report

Include changed Scene/Prefab/scripts, Inspector changes, screenshots, resolutions tested, PlayMode XML/logs, Console and Profiler evidence. If Unity was not run, write `未执行真实 Unity 编译。`
