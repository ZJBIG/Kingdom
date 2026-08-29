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

## Authored UI component rule

非必要的 UI 组件必须直接放在对应的 Scene 或 Prefab 中。运行时代码只能查找
已有组件并绑定数据、状态和事件，不得硬编码其视觉层级、尺寸、文案或通过
`new GameObject` / `AddComponent` 生成它们。只有研究节点、连接线等真正由数据
驱动的重复内容，或有明确兼容理由的降级路径，才允许运行时生成。

`SafeAreaRoot/Content/PageTool` 是页面级固定工具的统一外层；Overview 导航工具
与研究队列在其中保持同级。运行时只绑定已有控件，并按当前导航页控制其可见性。

### Compatibility fallback

The shared detail surface is a documented compatibility fallback: it is one
essential shell used by every detail page, and may be rebuilt when an older
root prefab has no compatible detail hierarchy. This exception does not cover
page toolbars, buttons, labels, fixed dimensions, or repeated visual rows;
those remain authored in the relevant Scene/Prefab or generated only as
data-driven repeated content.

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

## Current detail and sector boundary

- Research detail queues or removes research. Queue-head payment is automatic;
  there is no player-facing payment button.
- Workshop detail's main button purchases the selected workshop upgrade and
  does not reuse research detail content.
- Building detail has no build or upgrade button. Buildings are built from the
  building list; `SectorBuilding` is built only from the occupied sector menu.
- Buildings page filters every `SectorBuilding`. An unoccupied sector must not
  show the expand button or instantiate its menu content.
- The active research target alone uses the gold Outline in both the research
  tree and queue graphic.
