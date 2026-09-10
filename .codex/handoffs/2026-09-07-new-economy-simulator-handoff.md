# New economy simulator handoff

## Status

The legacy standalone simulator and its historical report directory were retired. The replacement is `tools/NewEconomySimulator`, a deterministic parity harness driven by versioned typed snapshots. Unity runtime managers remain the economy rule source.

## Implemented

- Added a versioned snapshot exporter and validator for Resource, Building, Research, Workshop, TechLevel, Sector, initial state, save state, and runtime state.
- Added a deterministic simulation core with fixed realtime/offline step semantics, ordered Unity-compatible system phases, atomic research payment, Food-only capacity, uncapped ordinary resources, building/workshop/sector/campaign progression, and stable event ordering.
- Added 13 self-validation checks covering determinism, Food capacity, ordinary resource capacity, research payment, save resume, geometric bulk costs, workshop/production chains, realtime/offline parity, large ExpantaNum values, era-step contracts, trace comparison, and report contracts.
- Added JSON/CSV/Markdown fact reports that expose snapshots, ordered events, first difference, and Unity/simulator comparisons without pacing or balance conclusions.
- Updated guidance, repository map, runtime/balance/testing docs, tools README, and validation script to reference the new simulator.
- Removed `tools/EconomySimulator` and `data/economy-simulation` historical outputs.
- Updated three legacy tests to call the now-public `CaptureSaveData()` APIs directly instead of non-public reflection lookup.

## Validation

- Static content closure: passed through Industrial, Spacer, and current Ultra reachability.
- `dotnet build tools/NewEconomySimulator/NewEconomySimulator.csproj --no-restore`: 0 warnings, 0 errors.
- New simulator self-test: 13/13 passed.
- Unity 2022.3.62f3c1 EditMode: 660/660 passed.
- Unity PlayMode: 33 passed, 0 failed, 1 skipped out of 34. The skipped
  `OuterPageScroll_DirectDragReportsMeasuredBounds` case reported no measured
  Overview overflow, so its background drag branch was not exercised.
- `tools/codex/validate-guidance.ps1`: passed.

## 2026-09-08 era-step refinement

- Added `EraStepSchedule` with deterministic realtime/offline steps for all
  seven eras.
- `SimulationCore.FromSnapshot` retains Unity-parity fixed steps (`0.1 s`
  realtime and at most `60 s` offline). Era steps are an explicit opt-in via
  `FromSnapshotWithEraSteps`; adaptive runs select the step from the era at
  tick start, and a changed era affects the following tick.
- Tick boundary events now record era, requested seconds and effective seconds.
- Each independent offline scenario now starts a fresh Unity-style offline
  settlement window, while the 2-hour/8-hour reduction bands remain continuous
  inside that scenario.
- Invalid simulation modes and invalid/incomplete era schedules are rejected.
  Snapshot TechLevel validation and the schedule share one canonical era list.
- Acceptance comparison mismatches now fail the aggregate report and CLI;
  controlled first-difference fixtures are explicitly diagnostic.
- Added the `era step schedule` self-validation check; build and all 13
  self-validation checks pass after the change.
- Independent subagent reviews found the parity-default, offline-session and
  comparison-aggregation defects. Multiple later correction agents were
  interrupted by local service/network failures after partial patches; the main
  agent reviewed, completed and verified those patches.

## 2026-09-09 validation refresh

- `dotnet build`: passed with 0 errors. A full rebuild emitted 18 existing
  nullable warnings, mostly from linked runtime `ExpantaNum.cs`; no runtime
  numeric code was changed to suppress them. The final incremental build
  reported 0 warnings and 0 errors.
- Simulator self-validation: 13/13 passed; 0 acceptance mismatches and 1
  intentional diagnostic mismatch.
- Static closure: Industrial 81/81 research, 37/37 Workshop, 50/50 buildings;
  Spacer 47/47, 46/46, 16/16; Ultra 1/1; 40 resources.
- Guidance validation passed; no active references to `tools/EconomySimulator`
  or `data/economy-simulation` remain.
- Unity compilation was retried twice (180 s and 300 s) but Unity created no log
  and the helper timed out. It only reported stale 2026-08-30 Library lock
  timestamps. Therefore no new Unity compile/test acceptance is claimed for
  this refresh. The latest existing reports remain EditMode 660/660 and
  PlayMode 33 passed, 0 failed, 1 skipped.

## Remaining risks and next action

- The simulator is a deterministic parity tool, not a pacing or balance acceptance tool.
- Era-adaptive steps are deterministic long-horizon diagnostic optimization,
  not parity evidence. Any first difference against Unity must be reported and
  investigated before using an adaptive result operationally.
- The next concrete action is to restore a functioning Unity batch session and
  rerun compilation, EditMode and PlayMode. Then add an end-to-end test where a
  real research completion advances the era and verify the following tick uses
  the new adaptive step.
- A future improvement is to add an end-to-end legacy save migration test that loads old `StoneChunk_Marble` / `StoneBrick_Marble` IDs through `SaveManager`, plus a negative test for mixed old/new duplicate IDs.

## 2026-09-09 EditMode expected-exception cleanup

- Updated `Assets/Tests/Editor/KingdomLogicTests.cs` so invalid-save rejection
  cases use the public `LoadOrCreateGame` path. Each case now writes an invalid
  primary plus a valid backup, expects the diagnostic Error, and verifies the
  backup marker is restored without asserting reflection-only
  `TargetInvocationException` wrappers.
- Replaced the non-finite runtime-state reflection assertions with public
  `GameManager`, `ResearchManager`, `ResourceManager`, and `BuildingManager`
  boundary assertions. The existing atomic commit/rollback/observer failure
  injection tests and their expected exception logs remain intact.
- Updated `Assets/Tests/Editor/SectorBuildingTests.cs` to validate an
  inconsistent sector-building save through real primary/backup loading, and
  moved the unknown campaign target case from the reflective
  `SectorManagerTests` path into the same public save-load coverage.
- Updated `Assets/Tests/Editor/TutorialManagerTests.cs` so
  `SaveSessionVersionChangesOnlyWhenRuntimeProgressIsRestored` creates its real
  runtime dependencies and no longer manufactures an unrelated missing-manager
  Error during `Evaluate()`.
- Static validation: `git diff --check` passed for the four edited test files;
  no `Assert.Throws<TargetInvocationException>` remains under
  `Assets/Tests/Editor`.
- Unity EditMode was started, then stopped at the user's request because the
  user will rerun the assertion tests. No result is claimed. 未执行真实 Unity 编译。
- Next concrete action: run the full EditMode suite and confirm zero failures
  and zero unexpected Error/Exception logs, paying special attention to the
  invalid-primary/valid-backup helper and sector-building fallback case.
