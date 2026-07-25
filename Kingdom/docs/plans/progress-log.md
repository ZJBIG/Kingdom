# Refactor progress log

## 2026-07-24 lifecycle tests and component binding audit

- Added PlayMode lifecycle tests for disabled ResourceViewer simulation continuity and refresh-scheduler unregister behavior.
- Audited all local C# script GUIDs against Scene/Prefab `m_Script` references: every runtime MonoBehaviour has an attachment; definition/State/math/validation/editor/test scripts correctly remain non-component code.
- Verified `GameUIRefreshManager` is attached to the Manager object and `ResearchLineView` is attached to `ResearchTransitionLine.prefab`.
- The repository YAML verifier still reports 178 unresolved GUIDs because it treats Unity UI/TMP/package script GUIDs as local; targeted local-script audit found no missing local runtime component GUID.

## 2026-07-24 Displayer simplification

- ResourceDisplayer and BuildingDisplayer cache their card/layout Images instead of resolving components during interaction/layout refresh.
- ResearchDisplayer no longer repeats refresh work in both `Awake` and `Start`; line color constants now live with ResearchLineView.
- MusicDisplayer retains only the bound AudioClip and display references needed for playback.

## 2026-07-24 ResearchLineView robustness

- Added `ResearchLineView` to the research transition-line prefab with cached `Image` and `RectTransform` references.
- ResearchViewer now stores relationship-keyed `ResearchLineView` instances and delegates geometry/color state to each line.
- Selection colors are derived from the explicit prerequisite/target relationship, eliminating direct Image mutation in ResearchViewer.
- Added EditMode coverage for unselected, prerequisite, target and unrelated selection states.

## 2026-07-24 K3-05 music binding boundary

- MusicViewer now loads each AudioClip once and binds the actual clip to MusicDisplayer.
- MusicDisplayer no longer reconstructs a Resources path from display strings; playback sends the bound AudioClip to MusicManager.
- Missing clips are logged and skipped safely; empty clip/source cases remain guarded.
- SettingViewer's MusicViewer reference is now private serialized data.

## 2026-07-24 K3-03 refresh scheduler and K3-05 cleanup

- Added `GameUIRefreshManager` to the Manager object as the single bounded UI refresh scheduler.
- ResourceViewer, BuildingViewer and ResearchViewer now register/unregister refresh interfaces instead of owning `Update` timers.
- GameHudViewer and MusicViewer no longer own UI refresh coroutines; hidden viewers perform no scheduled refresh work.
- Replaced frame-rate-dependent background rotation with `Time.deltaTime` and preserved the serialized `RotateSpeed` field through `FormerlySerializedAs`.
- Deleted the empty `Dimension` script after confirming no Scene, Prefab or code references.

## 2026-07-24 K3-02 save candidate transactions

- Save loading now treats JSON parsing plus full State application as one candidate transaction.
- A semantically invalid main save resets runtime State and attempts the backup; both candidate failures start a clean new game without retaining partial data.
- Repeated loads reset Resource, Building and Research State in place, clear research queue/selection and preserve existing StateAdded subscriptions, preventing stale state and duplicate Viewer cards.
- Error logs include the candidate path and the application exception message, including the affected field or stable ID from existing parse/lookup exceptions.
- Added runtime reset hooks for all mutable State collections; derived rates are rebuilt after each candidate reset.
- Unity batch reimport was attempted with `D:\Unity\Hub\Editor\2022.3.62f2c1\Editor\Unity.exe`, but an older Unity batch instance held the project lock; only the newly started waiting process was stopped. Existing editor instances were left untouched.

## 2026-07-24 TMP dynamic Chinese glyph coverage

- Confirmed `SIMSUN SDF` contained only 126 pre-generated characters while project UI/data uses over 600 Chinese characters.
- Enabled TMP multi-atlas dynamic population on the existing `SIMSUN SDF` asset so missing characters can be generated from the bundled `SIMSUN.TTC` source without changing the asset GUID.
- Static scan confirmed all serialized UI font assets and shared materials use SIMSUN; runtime glyph generation still requires Unity import/render validation.

## 2026-07-24 TMP material and compact number display

- Fixed the remaining four UI TMP components whose `m_fontAsset` was SIMSUN but whose `m_sharedMaterial` still referenced LiberationSans; both asset and material now use SIMSUN SDF.
- Increased `ExpantaNum.ToGameString()` default precision from 3 to 4 significant digits so `1220` displays as `1.22K`.
- Added a regression test for the compact thousands display.
- Static serialized scan found no non-SIMSUN UI font or shared-material references. Unity rendering was not run.

## 2026-07-24 K3-01 simulation correctness

- Implemented per-Tick resource satisfaction using current inventory plus potential production over the exact Tick duration; positive inventory no longer implies full efficiency.
- Added stable resource ordering and transient Tick potential/satisfaction state without LINQ, closures or per-Tick dictionaries.
- Building efficiency now consumes the minimum satisfaction of its input resources before actual rates are committed.
- Food now integrates every simulation Tick; calendar days use an independent elapsed-seconds accumulator.
- Added backlog clamping to `tickInterval * maximumTicksPerFrame`; `ManualTick` remains unrestricted.
- Added EditMode regression coverage for partial resource satisfaction and separate food/calendar progression.
- `dotnet build Kingdom.sln --no-restore --verbosity minimal` passed with 45 existing Inspector-field warnings and 0 errors. 未执行真实 Unity 编译。

## 2026-07-24 Repository pack and TMP binding

- Replaced repository guidance, scoped instructions, runtime/UI skills, architecture docs, TODO, audit scripts and acceptance documents from `Kingdom_Codex_Repository_Pack_2026-07-24`.
- Removed superseded handoff files and the historical `Assets/Resources/Script.zip`; preserved this progress log and the definition-database note because the pack does not replace them.
- Rebound every serialized UI TMP `m_fontAsset` reference in the Scene and Prefabs to `SIMSUN SDF` (GUID `08ad5319768e92d4a9d133b324661387`); static scan found 0 non-SIMSUN UI font references.
- Guidance validation passed and `dotnet build Kingdom.sln --no-restore --verbosity minimal` passed with 0 warnings and 0 errors.
- Real Unity compilation, Inspector serialization validation and PlayMode/visual validation remain pending.

## 2026-07-21 baseline and first logic batch

- User constraint: do not modify UI assets or UI component structure.
- Preserved pre-existing edits in `BuildingDisplayer.cs`, `ResearchManager.cs`, `BigNumber.cs`, and `Tool.cs`.
- Installed repository guidance and the supplied `ExpantaNum` runtime sources.
- Baseline Unity 2022.3.62f2c1 compilation passed after importing ExpantaNum.
- Current batch scope: Pair compatibility, calendar correctness, Singleton lifecycle, research graph validation, and research queue completion state.
- Added temporary pre-State fixes for atomic construction checks, deconstruction clamping/returns, resource non-negativity, efficiency direction, and food net-rate progression without changing serialized UI references.
- Unity EditMode result XML initially recorded 16/16 passing tests; the China Editor did not self-exit after writing results. The launcher now removes stale XML, waits for a complete new result, and stops only the non-exiting batch PID after a grace period.
- Research progress now multiplies its per-second speed by the 0.02-second tick and clamps to BaseCost.
- Fresh EditMode result generated at 2026-07-21 22:41 local time: 17 passed, 0 failed.

## 2026-07-22 IDE synchronization

- The generated solution and project files were stale from 2026-03-22 and did not list newly added source files.
- Added a Unity Editor solution-sync entry point and reusable `tools/codex/sync-solution.ps1` wrapper.
- Added a read-only legacy resource-pair migration audit for all Building and Research definition assets.
- Dry-run passed for 6 Building assets, 14 Research assets, and 17 entries with zero failures.
- Applied temporary typed `Pair<Resource, ExpantaNum>` migration fields to 20 definition assets; save/reload verification passed with zero mismatches.
- Pre-migration asset backup: `MigrationBackups/20260722-103411`.
- Post-migration Unity compilation passed; fresh EditMode result: 18 passed, 0 failed.
- Regenerated `Kingdom.sln`, `Assembly-CSharp.csproj`, and `Assembly-CSharp-Editor.csproj`; all new runtime, Editor, and test sources are listed.
- Deferred: Scene/Prefab/UI layout changes, runtime State migration, simulation ownership migration, and serialized Pair value-type migration.

## 2026-07-22 ExpantaNum runtime cutover

- Replaced runtime `BigNumber` state and arithmetic across game, resource, building, and research systems with `ExpantaNum`.
- Building construction, deconstruction, generation, and consumption now read typed `Pair<Resource, ExpantaNum>` definition fields.
- Updated runtime number display calls to `ToGameString()` without changing serialized UI references, scene objects, prefab layouts, or UI assets.
- Updated transaction, food progression, and research progression tests to assert `ExpantaNum` behavior.
- Confirmed the legacy `BigNumber.cs` GUID was not referenced by Unity assets, then removed the obsolete source and meta files.
- Regenerated solution/project files; no `BigNumber` references remain in source or generated IDE project files.
- Final Unity compilation passed; fresh EditMode result: 18 passed, 0 failed.
- Deferred: unified/versioned string save format and simulation ownership migration.

## 2026-07-22 stable definition database

- Added `GameDefinition` as the immutable-definition base with a serialized stable `Id`.
- Added lazy, bounded `DataBase<T>` indexes for `Resource`, `Building`, and `Research` definitions under `Resources/Datas`.
- Public lookup API supports `Find`, `TryFind`, `Contains`, `Count`, and deterministic `All`; IDs are case-insensitive for convenient code lookup.
- Added an Editor migration and reusable command-line wrapper for assigning and auditing definition IDs.
- Unity migration assigned IDs to 50 assets from their existing asset names; zero empty or duplicate IDs were found.
- Managers now resolve definition IDs through `DataBase<T>` and save stable IDs instead of Finder field names or ScriptableObject references.
- Per-manager save filenames remain canonical `.json` names; legacy save compatibility is intentionally unsupported per user direction.
- Finder fields are no longer used by runtime code.
- No Prefab, UI asset, UI hierarchy, serialized UI reference, or ProjectSettings content was changed.
- Unity compilation and solution synchronization passed; fresh EditMode result: 20 passed, 0 failed.

## 2026-07-22 canonical definitions and ResourceState

- Finalized Building and Research resource lists as the only canonical `Pair<Resource, ExpantaNum>` fields and removed all temporary/legacy string fields and migration scripts.
- Forced Unity to reserialize all 50 definition assets; obsolete serialized fields were removed while all 17 typed resource entries were preserved.
- Added an Editor PropertyDrawer that renders each resource/value pair as a draggable Resource object field plus a parseable ExpantaNum text field.
- Made Pair serialized fields private while preserving their `first` and `second` YAML names and read-only public properties.
- Removed the three Finder assets, reflection-based Finder source, and only their three serialized Manager references from `SampleScene`; no UI component data changed.
- Added `ResourceState` and made ResourceManager the authority for resource amount/rates/efficiency.
- ResourceDisplayer now binds to ResourceState and no longer advances or stores gameplay resource values.
- Resource progression moved from the UI card coroutine to ResourceManager and uses explicit elapsed seconds.
- Resource persistence now writes parseable ExpantaNum strings and stable Resource IDs.
- Canonical save filenames use an internal schema number; mismatched legacy files trigger a clean new-game initialization instead of compatibility migration.
- Unity compilation and solution synchronization passed; fresh EditMode result: 22 passed, 0 failed.
- PlayMode test command completed with zero failures, but the project currently contains zero PlayMode test cases, so scene behavior remains manually unverified.

## 2026-07-22 BuildingState authority

- Completed ToDoList P2-02 by adding `BuildingState` for amount, efficiency, auto-build toggle, auto-build progress, and cached parsed definition costs.
- BuildingDisplayer now binds to BuildingState, renders it, and sends commands only; it no longer owns or mutates gameplay building values.
- Manual build and deconstruct transactions moved into BuildingManager with validate-then-commit behavior and shared resource/space/productivity checks.
- Auto-build now accumulates elapsed-time progress, considers affordability, and calls the same `TryBuild` transaction used by manual construction.
- Only auto-build-enabled states receive progress. Execution order is stable by Building Id and does not depend on Dictionary enumeration.
- Building efficiency and resource-rate changes moved out of the UI card coroutine and into BuildingManager.
- Building saves now contain stable IDs plus parseable amount/progress strings; efficiency and resource rates are recalculated rather than saved.
- Resource saves now persist only non-derived amounts. Base wood generation is restored before loaded buildings reapply their rates.
- Save schema mismatch intentionally starts a clean game; no legacy-save migration is provided.
- Unity compilation and solution synchronization passed; fresh EditMode result: 24 passed, 0 failed.
- No Prefab, UI asset, UI hierarchy, serialized UI reference, or ProjectSettings content was changed.
- Deferred: P2-03 ResearchState, P2-04 GameState, and replacement of temporary Manager coroutines with the single SimulationManager.

## 2026-07-22 ResearchState authority

- Completed ToDoList P2-03 by adding `ResearchState` and the UI-independent `ResearchStatus` lifecycle.
- Research progress, status, one-time cost-paid flag, and parsed BaseCost now live outside ResearchDisplayer.
- ResearchManager queue now stores ResearchState and remains active independently of the research viewer.
- Research resource requirements are paid atomically when a state reaches the queue head; insufficient resources produce `WaitingResources` and zero progress.
- Paid research costs are not refunded when another queue is selected, and the same research never pays twice.
- Research completion clamps progress, marks the state Completed, unlocks buildings, recalculates availability, and advances the queue.
- Research saves now use stable IDs plus parseable progress strings and save only non-derivable completion/cost/queue state.
- ResourceState and BuildingState creation can now succeed without UI prefabs, enabling headless gameplay-rule tests.
- Added a no-UI EditMode integration test proving insufficient research resources do not partially deduct and successful cost is paid exactly once.
- Unity compilation and solution synchronization passed; fresh EditMode result: 29 passed, 0 failed.
- No Prefab, UI asset, UI hierarchy, serialized UI reference, or ProjectSettings content was changed.
- Deferred: P2-04 GameState and replacement of temporary Manager coroutines with the single SimulationManager.

## 2026-07-22 GameState and semantic naming

- Completed ToDoList P2-04 by adding GameState for calendar, kingdom identity, tech level, food, available territory, available productivity, and last-save time.
- GameManager is now the only normal writer of GameState; BuildingManager and ResearchManager use GameManager commands/read-only State values.
- Replaced duplicated dated save-schema constants with one `SaveFormat.CurrentVersion`; mismatched files start a clean game and are not migrated.
- Replaced ambiguous Building definition fields with typed ExpantaNum fields: AutoBuildWorkRequired, SpaceCost, BuildEffort, ProductivityGranted, FoodProductionRate, and FoodConsumptionRate.
- Migrated signed legacy productivity values into separate non-negative BuildEffort and ProductivityGranted values.
- Migrated signed food values into separate non-negative production and consumption rates.
- Corrected the `Prequisites` typo to `Prerequisites` across definitions, runtime, validation, UI code, tests, and serialized assets.
- Renamed resource runtime `GenerateRate/ConsumeRate` APIs to `ProductionRate/ConsumptionRate`.
- Added a general ExpantaNum Inspector drawer so scalar definition values are entered as parseable text instead of internal numeric fields.
- Added a construction conflict check that prevents removing productivity-granting buildings while their granted productivity is in use.
- Unity reserialized all 50 definitions with the canonical field names; obsolete names no longer remain in source or definition YAML.
- Unity compilation and solution synchronization passed; fresh EditMode result: 30 passed, 0 failed.
- No Prefab, UI asset, UI hierarchy, serialized UI reference, or ProjectSettings content was changed.
- Deferred: single SimulationManager, removal of temporary per-manager simulation coroutines, and unified one-file SaveManager.

## 2026-07-22 single SimulationManager

- Completed the next runtime step by adding `SimulationManager` as the single deterministic gameplay clock.
- Mounted `SimulationManager` on the existing non-UI `Manager` scene object; no UI hierarchy, UI component, UI prefab, or serialized UI reference was changed.
- Removed the temporary core gameplay coroutines from `ResourceManager`, `BuildingManager`, `ResearchManager`, and the calendar progression in `GameManager`.
- Current tick order is calendar, building efficiency, resource progression, research progression, then auto-build progression.
- `SimulationManager.ManualTick(double deltaSeconds)` supports headless deterministic tests and rejects negative elapsed time.
- Research state initialization now tolerates missing UI references so gameplay-rule tests can run without spawning research UI cards.
- Added a no-UI EditMode integration test proving a manual 5-second simulation tick advances calendar and resource production through the manager layer.
- Unity compilation, solution synchronization, and EditMode tests passed; fresh EditMode result: 31 passed, 0 failed.
- PlayMode test command passed with zero failures, but the project currently contains zero PlayMode test cases, so scene/UI behavior is still not visually verified.
- Deferred: remaining UI refresh coroutines, bootstrap/load ordering cleanup, and unified one-file SaveManager.

## 2026-07-22 GameBootstrap and unified safe save

- Completed ToDoList P8-05 by adding `GameBootstrap` and removing the old `WaitForSecondsRealtime(0.1f)` load-order guess from `GameManager`.
- `SimulationManager` now starts paused and is explicitly enabled only after `GameBootstrap` validates definitions and `SaveManager` loads or creates the game.
- Completed ToDoList P8-06 by replacing the four manager save files with one versioned root save DTO written to `KingdomSave.json`.
- Unified save data stores stable IDs and non-derivable state: game core fields, resource amounts, building amount/auto-build/progress, research progress/cost-paid/completion/queue/selection.
- Derived data such as production rates, consumption rates, food rates, space/productivity after buildings, and building efficiency are reset and recalculated after load instead of being saved.
- Completed ToDoList P8-07 core safety by adding a 30-second autosave loop, dirty/version signature checks, pause/quit saves, temp writes, backup creation, backup fallback on read failure, and exception logging instead of save-time crashes.
- Added `GameBootstrap` and `SaveManager` to the existing non-UI `Manager` scene object through a Unity Editor command; UI hierarchy, UI component structure, and serialized UI references were not changed.
- Updated `SaveFormat.CurrentVersion` to 2 because the save root format changed and legacy save compatibility is intentionally unsupported.
- Added EditMode regression coverage for the unified save DTO shape and paused simulation startup behavior.
- Unity compilation passed and solution synchronization passed. `dotnet build Kingdom.sln --no-restore` also passed with warnings only.
- EditMode tests passed after the final permission update; fresh EditMode result: 33 passed, 0 failed.
- PlayMode test command passed with zero failures, but the project currently contains zero PlayMode test cases, so scene/UI behavior is still not visually verified.
- Deferred: P6 UI refresh/layout migration, P7 UI SetActive acceptance, P9 music/GetChild/empty-script cleanup, and visual PlayMode scene validation.

## 2026-07-24 Resource and building dynamic UI ownership

- Began the P3/P6 UI boundary cleanup for Resource and Building screens while preserving the intended gameplay behavior that cards are added gradually as resources/buildings are discovered.
- `ResourceManager` now owns only resource runtime state and raises `ResourceStateAdded` when a new resource state is created; it no longer stores resource UI sets, resource prefabs, or creates resource displayers.
- `BuildingManager` now owns only building runtime state and raises `BuildingStateAdded` when a new building state is created; it no longer stores building UI content/prefab references or creates building displayers.
- `ResourceViewer` now listens for `ResourceStateAdded`, binds already-existing states when opened, and creates the correct resource card under the matching `ResourceDisplayerSet`.
- `BuildingViewer` now listens for `BuildingStateAdded`, binds already-existing states when opened, and creates building cards in its content transform.
- `ResourceDisplayer` and `BuildingDisplayer` no longer run permanent UI refresh coroutines; they bind to State and refresh from Viewer polling.
- `ResourceDisplayerSet` no longer runs a permanent height coroutine; it recalculates layout when opened/closed or when ResourceViewer refreshes.
- Migrated `ResourceDisplayerPrefab` and `BuildingDisplayerPrefab` references in `SampleScene.unity` from the non-UI Manager components to their corresponding Viewer components.
- Bound the resource description TMP reference on `ResourceDisplayer.prefab` and removed obsolete serialized runtime fields from that prefab.
- Added EditMode coverage for Manager state-added events and for ResourceViewer catching up when opened after a resource has already been discovered.
- `dotnet build Kingdom.sln --no-restore --verbosity minimal` passed with 0 warnings and 0 errors.
- Real Unity batch compilation and EditMode execution were not run in this pass because the required Unity execution approval was rejected by the environment.
- Deferred: ResearchManager still creates research UI nodes/lines, GameManager still owns top-level UI refs and page switching, BuildingDisplayer still uses child indices for requirement row internals, and PlayMode visual/UI validation is still pending.

## 2026-07-24 Research dynamic UI ownership

- Continued the P3/P6 UI boundary cleanup by moving research node and transition-line ownership out of `ResearchManager`.
- `ResearchManager` now owns research runtime state, queue/status/progress/cost payment, research counts, selected research ID for save/restore, and raises `ResearchStateAdded`; it no longer stores a `ResearchViewer`, research displayer prefab, transition-line prefab, displayer dictionary, or line dictionary.
- `ResearchViewer` now listens for `ResearchStateAdded`, binds already-existing research states when opened, creates research nodes and prerequisite transition lines, owns selection highlighting, and refreshes selected research details from `ResearchState`.
- `ResearchDisplayer` now binds to `ResearchState`, renders node progress through `Refresh()`, and sends selection input to its parent `ResearchViewer`; its permanent refresh coroutine was removed.
- Research requirement rows now rebuild only when the selected research changes, and the research UI code no longer uses `Transform.GetChild(index)` for requirement row internals.
- Added `Resources.Load` fallback paths for research node, requirement-row, and transition-line prefabs so this code change does not depend on immediate scene YAML migration.
- Static search found no remaining `ResearchManager.Instance.ResearchViewer`, `ResearchManager.Instance.Displayers`, `ResearchManager.Instance.Lines`, research `IEnumerator UpdateUI`, or research `Transform.GetChild` references.
- `dotnet build Kingdom.sln --no-restore --verbosity minimal` passed with 43 CS0649 Inspector-field warnings and 0 errors.
- Real Unity batch compilation, EditMode tests, PlayMode tests, scene/prefab/Inspector validation, and visual UI validation were intentionally not run in this pass per user direction.
- Deferred: scene serialized references should still be inspected/migrated in Unity when command execution is acceptable, GameManager still owns top-level UI refs and page switching, music remains coupled to Settings UI, BuildingDisplayer still uses child indices for requirement row internals, and PlayMode visual/UI validation is still pending.

## 2026-07-24 Requirement rows, music, and tab state cleanup

- Added `ResourceRequirementView` as a shared UI row component with explicit Image/TMP references and bound it on both Building and Research requirement-row prefabs.
- `BuildingDisplayer` and `ResearchViewer` now bind resource requirement rows through `ResourceRequirementView` instead of child-index or child-search assumptions.
- Moved core music playback and random autoplay into `MusicManager`; `MusicViewer` now only creates list entries and refreshes current-playing UI, and `MusicDisplayer` sends play commands to `MusicManager`.
- Added null/empty clip safety around music UI refresh and missing music clip logging in `MusicManager`.
- Replaced top-page switching logic in `GameManager` with a `MainTab` enum so viewer visibility is no longer derived from localized `Text_TopText.text`.
- Rewrote the formerly mojibake GameManager display labels as readable Chinese strings while preserving the same UI meaning.
- Removed stale serialized `MusicTypes` and `AudioSource` fields from the scene's MusicManager component, and added the new `ResourceRequirementView` source to `Assembly-CSharp.csproj` for local `dotnet build`.
- Static search found no active `Text_TopText.text ==`, `Text_TopText.text switch`, `MusicList.GetChild`, or requirement-row `Transform.GetChild` references; remaining `GetChild` hits are commented-out `FoodVarietyViewer` sample code.
- `dotnet build Kingdom.sln --no-restore --verbosity minimal` passed with 45 CS0649 Inspector-field warnings and 0 errors.
- Real Unity batch compilation, EditMode tests, PlayMode tests, scene/prefab/Inspector validation, and visual UI validation were not run in this pass per user direction.
- Deferred: GameManager still owns top-bar HUD refresh, main viewers still use outside-window hiding pending UI-off acceptance, music playback still needs runtime/visual validation, and PlayMode visual/UI validation is still pending.

## 2026-07-24 TMP, descriptions, and HUD/navigation cleanup

- Fixed the project-level TMP Chinese fallback path by making `SIMSUN SDF` the default TMP font asset and adding `SIMSUN SDF` plus `LiberationSans SDF` to the global TMP fallback list.
- Filled missing `Description` text for labeled Building, Resource, and Research definition assets only; assets without a `Label` were intentionally left untouched.
- Verified with a static asset scan that no labeled Building, Resource, or Research asset still has an empty `Description`.
- Moved top-bar HUD text refresh out of `GameManager` into `GameHudViewer`; `GameManager` no longer stores TMP references or runs a HUD refresh coroutine.
- Moved main tab navigation and viewer off-screen positioning out of `GameManager` into `MainNavigationViewer`; the existing top-tab button now targets `MainNavigationViewer.SwitchTop`.
- Moved `GameHudViewer` and `MainNavigationViewer` under `Assets/Resources/Script/UI` while preserving their `.meta` GUIDs so scene component bindings remain stable.
- Removed the unused `Top` singleton script and meta after confirming its GUID had no scene, prefab, or code references.
- Cached enum `DescriptionAttribute` lookup behind the existing `GetDescription()` extension so the top HUD no longer reflects on `TechLevel` every refresh.
- Changed `GameHudViewer` to update TMP text only when the displayed string actually changes.
- Rewrote `FoodVarietyViewer.cs` as valid UTF-8, removing a commented-out obsolete research UI implementation that contained invalid byte sequences and old `GetChild(index)` sample code while preserving the scene-bound script `.meta` GUID.
- Corrected `FoodVarietyDisplayer`'s serialized field shape to match the existing `ItemDisplayer.prefab` object-reference YAML, using `FormerlySerializedAs` while replacing public writable Inspector fields with private serialized fields.
- Static search found no TMP/UI/RectTransform/GameObject/Transform references in the core `GameManager`, `ResourceManager`, `BuildingManager`, or `ResearchManager` scripts.
- `dotnet build Kingdom.sln --no-restore --verbosity minimal` passed with 45 CS0649 Inspector-field warnings and 0 errors.
- Real Unity batch compilation, EditMode tests, PlayMode tests, scene/prefab/Inspector validation, and visual UI validation were not run in this pass per user direction.
- Deferred: main viewers still use outside-window hiding pending UI-off acceptance, music playback still needs runtime/visual validation, and PlayMode visual/UI validation is still pending.
