# PlayMode UI lifecycle test plan

## Test assembly

Create a PlayMode test assembly only if the project structure requires it. Do not move runtime scripts merely to satisfy tests.

## Required automated cases

1. `ResourceViewerDisabled_SimulationContinues`
2. `BuildingViewerDisabled_SimulationContinues`
3. `ResearchViewerDisabled_ResearchContinues`
4. `SettingViewerDisabled_MusicManagerContinues`
5. `ViewerReenabled_ImmediatelyShowsLatestState`
6. `RepeatedEnableDisable_DoesNotDuplicateCards`
7. `RepeatedEnableDisable_DoesNotDuplicateSubscriptions`
8. `MainTabSwitch_DoesNotMutateGameplayState`
9. `DisabledViewer_DoesNotRefresh`

Use `SimulationManager.ManualTick` for long simulated intervals. Add one short real-frame smoke test for OnEnable/OnDisable behavior.

## Manual checks

- disable each page for 60 real seconds;
- check music auto-advance with settings closed;
- reopen every page and compare rendered values with State;
- inspect Console;
- inspect Profiler for TMP, Layout.Rebuild, Canvas.BuildBatch and GC Alloc.

A PlayMode runner result with zero cases is a failure, not acceptance.
