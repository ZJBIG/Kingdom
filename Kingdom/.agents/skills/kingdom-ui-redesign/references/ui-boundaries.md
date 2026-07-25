# UI boundaries and lifecycle

## Current progress

Kingdom3 already moved UI ownership out of Managers. Viewers create cards/nodes and bind State. This must be preserved.

## Viewer responsibilities

- subscribe/unsubscribe to Manager StateAdded events;
- create and own Displayer instances;
- manage selection, grouping and page-level presentation;
- refresh only while visible;
- force a full bind/refresh on enable.

## Displayer/View responsibilities

- cache explicit serialized component references;
- bind a State or immutable definition;
- format and render values;
- expose user input events;
- call Manager command APIs;
- never store authoritative gameplay values.

## Refresh ownership

Target one `GameUIRefreshManager`:

- refresh HUD at a bounded rate;
- refresh only the active main Viewer;
- refresh MusicViewer only while settings is open;
- use State.Version to skip formatting;
- never advance simulation.

No per-card Update/coroutine. Viewer-specific Update methods and HUD/Music UI coroutines must be removed.

## Layout ownership

Use Unity layout components instead of manual `sizeDelta` formulas:

- VerticalLayoutGroup
- HorizontalLayoutGroup
- GridLayoutGroup
- ContentSizeFitter
- LayoutElement

Expanding a card toggles its Details object. Resource categories do not reparent cards into a hidden Transform.

## Navigation gate

Off-screen hiding remains a temporary compatibility mechanism. It can be removed only after PlayMode tests prove that disabling each Viewer does not stop simulation, music or state refresh.

After the gate:

- `SetMainTab(MainTab)` controls GameObject activation;
- `CurrentTab` and `MainTabChanged` are explicit;
- Viewer `OnEnable` binds and refreshes;
- Viewer `OnDisable` only unsubscribes;
- SettingViewer can be disabled while MusicManager continues.

## Visual redesign boundary

Theme, animation and Prefab redesign may not reintroduce gameplay state into UI. UITheme is definition data only. Visual feedback reads explicit result/status values rather than parsing text.
