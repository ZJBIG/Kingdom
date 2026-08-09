# Kingdom3 repository map

## Baseline

- Unity: `2022.3.62f2c1`
- Enabled build scene: `Assets/Scenes/SampleScene.unity`
- Game scripts: `42` files / about `7006` lines
- Definition assets: `50` (`6` Building, `14` Research, `30` Resource)
- Prefabs: `9`
- Scenes under Assets: `2`
- asmdefs: `0`
- Existing tests: one EditMode file, `35` Test/TestCase markers
- PlayMode tests: none

## Current runtime code

```text
Assets/Resources/Script/
├─ Building/
├─ Data/
├─ Manager/
├─ Math/
├─ Research/
├─ Resource/
├─ Runtime/
├─ Setting/
├─ Special/
├─ UI/
└─ Validation/
```

Important current files:

- `Manager/SimulationManager.cs`: core fixed-step loop and ManualTick
- `Manager/ResourceManager.cs`: ResourceState ownership and inventory update
- `Manager/BuildingManager.cs`: build/deconstruct, rates, efficiency and auto-build
- `Manager/ResearchManager.cs`: research State, queue, payment and progression
- `Manager/GameManager.cs`: GameState, calendar and food
- `Manager/SaveManager.cs`: versioned save, temp and backup
- `Manager/GameBootstrap.cs`: definition validation, load and simulation start
- `Runtime/*.cs`: authoritative mutable State
- `Math/ExpantaNum*.cs`: numeric core and stateless formulas
- `UI/GameHudViewer.cs`: HUD rendering
- `UI/MainNavigationViewer.cs`: current tab switching and temporary off-screen hiding
- `Resource/ResourceViewer.cs`: resource UI creation/binding
- `Building/BuildingViewer.cs`: building UI creation/binding
- `Research/ResearchViewer.cs`: graph/node/line/detail UI
- `Setting/Music/MusicViewer.cs`: music panel rendering
- `Tests/Editor/KingdomLogicTests.cs`: current EditMode regressions

## Completed architecture

- BigNumber removed.
- Pair resource values migrated to ExpantaNum.
- State classes established.
- Managers own State and do not reference UI.
- Viewers own Displayers.
- stable IDs and DataBase established.
- unified SaveManager established.
- no `Transform.GetChild(index)` in game scripts.
- enum description cache established.
- music auto-play core resides in MusicManager.

## Remaining architecture debt

- partial per-tick resource satisfaction is incorrect;
- food integration is coupled to the calendar step;
- Viewer/HUD/Music UI refresh is distributed;
- manual UI height calculations remain;
- pages are hidden by transform position;
- no PlayMode UI lifecycle tests;
- ResearchLineView is absent;
- background rotation is frame-rate dependent;
- empty/placeholder scripts remain.

## Definition assets

- `Assets/Resources/Datas/Building/`
- `Assets/Resources/Datas/Research/`
- `Assets/Resources/Datas/Resource/`

All current definition assets have a non-empty stable ID. IDs are unique within each definition type.

## UI assets

- `Assets/Resources/UI/`
- `Assets/Scenes/SampleScene.unity`

Scene/Prefab edits require preserving `.meta` GUIDs and actual PlayMode inspection.
