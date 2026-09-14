# UI boundaries and lifecycle

This document owns presentation/State boundaries and lifecycle facts. Detailed
layout, authored component exceptions and interaction gates live in
`.agents/skills/kingdom-ui-redesign/SKILL.md`; page actions and selection semantics
live only in `docs/ui/page-responsibilities.md`. Observable regression scenarios
live in `docs/testing/playmode-test-plan.md`.

## State and command ownership

- Managers validate commands and mutate runtime State; UI binds definitions and
  State, formats values, manages selection and issues Manager commands.
- Views cache explicit component references and own their subscriptions and
  presentation objects, not authoritative gameplay values.
- Theme data and visual feedback remain presentation-only. Render explicit
  result/status values rather than parsing labels to infer gameplay state.
- Closing or switching a page must not stop simulation, research or music.
  Returning to a page must display current State; subscription cleanup must not
  disable Manager behavior.

## Current refresh and navigation

- `KingdomUIRoot.LiveRefresh.cs` owns the current bounded live-refresh loop and
  page/branch refresh decisions. Preserve cached references, change detection and
  bounded formatting; do not add permanent per-card Update methods or coroutines.
- `KingdomUIRoot.SetPage` owns page switching and position restoration. Ordinary
  pages toggle activation; the cached Research hierarchy stays active and
  `SetResearchPageVisible` gates CanvasGroup rendering, raycasts, interaction and
  the graph gesture component. Hidden does not mean interactive.
- Do not replace that Research cache with blanket hierarchy activation merely
  because an old guide required Viewer OnEnable/OnDisable on every switch.
- `GameUIRefreshManager`, `SetMainTab`, `CurrentTab` and `MainTabChanged` were old
  design targets, not required current APIs. Do not rebuild them from this guide.
- Legacy Viewer/Displayer isolation follows the UI skill. A migration or refresh
  refactor requires its own scope and behavior/performance evidence, not just a
  documentation edit.
