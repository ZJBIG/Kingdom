# 13 · Runtime / Math / Misc 层 C# 源码审计报告

审计时间：2026-10-10　审计员：general-purpose-13　阶段：只读盘点（未修改任何文件）
基准：以当前磁盘源码为事实基准（工作树含并行任务未提交改动）。

## 1. 范围

本次实际完整读取的文件（共 19 个 .cs）：

- `Assets/Resources/Script/Runtime/`（14）：`BuildingState.cs`、`CampaignState.cs`、`GameState.cs`、`HappinessFormula.cs`、`MilitaryState.cs`、`OfflineProgressSummary.cs`、`PopulationState.cs`、`RelicState.cs`、`ResearchState.cs`、`ResourceState.cs`、`StoryProgressState.cs`、`TerritoryState.cs`、`UltraProjectState.cs`、`WorkshopUpgradeState.cs`
- `Assets/Resources/Script/Math/`（2）：`ExpantaNum.cs`、`ExpantaNumExtensions.cs`
- `Assets/Resources/Script/Misc/`（2）：`Singleton.cs`、`Tool.cs`
- `Assets/KingdomAssemblyMarker.cs`（1）
- `Assets/Resources/Script/` 根目录下无独立 .cs（已确认）。

交叉引用（为判定"在用/失效"而查阅，不属本区块结论）：`Data/SaveFormat.cs`、`Manager/SaveManager.cs`、`Manager/GameManager.cs`、`Manager/BuildingManager.cs`（成本公式）、`UI/KingdomUIRoot.OfflineSummary.cs`、`Editor/Migration/DefinitionIdMigration.cs`、`Resources/Script/Kingdom.Runtime.asmdef`、全部 `Assets/Tests/**`。

## 2. 结论清单

### 2.1 建议删除（死代码 / 无调用点）

- [High] `Assets/Resources/Script/Runtime/UltraProjectState.cs:276` — D9；证据：`internal void ReadyToCommit() => MarkReadyToCommit();` 与 `MarkReadyToCommit()`(248) 完全等价，全仓搜索 `ReadyToCommit()` 仅命中自身定义与其 wrapper `ReadyToCommitForEditor`(604)，二者均无外部调用（`MarkReadyToCommit` 本体在 361 行被内部调用，属在用）；建议：删除该重载；理由：纯冗余转发。
- [High] `Assets/Resources/Script/Runtime/UltraProjectState.cs:603` `MarkReadyToCommitForEditor` — D9；证据：全仓搜索式 `MarkReadyToCommitForEditor` 仅命中定义，0 调用（`UltraProjectStateTests.cs` 用 `AdvanceStageProgressForEditor`/`CommitForEditor` 组合推进，未用此钩子）；建议：删除；理由：未使用的编辑器测试钩子。
- [High] `Assets/Resources/Script/Runtime/UltraProjectState.cs:604` `ReadyToCommitForEditor` — D9；证据：全仓搜索式 `ReadyToCommitForEditor` 仅命中定义，0 调用；建议：删除（连带 276 行 `ReadyToCommit()`）；理由：未使用的编辑器测试钩子。
- [Med] `Assets/Resources/Script/Runtime/UltraProjectState.cs:599-600` `SetStageProgressForEditor` — D9；证据：全仓搜索式 `SetStageProgressForEditor` 仅命中定义，0 调用（同类 `AdvanceStageProgressForEditor`、`CommitForEditor`、`AbandonForEditor` 在 `UltraProjectStateTests.cs` 均有调用，唯独此钩子无）；建议：删除；理由：未使用的编辑器测试钩子。
- [Med] `Assets/Resources/Script/Runtime/CampaignState.cs:200` `SetDoctrineForEditor` — D9；证据：全仓搜索式 `SetDoctrineForEditor` 仅命中定义，0 调用（`SetDoctrine` 本体经 `GameState.SetCampaignDoctrine`→`UltraProjectManager` 在用）；建议：删除；理由：未使用的编辑器测试钩子。
- [Med] `Assets/Resources/Script/Runtime/GameState.cs:34-35` `HappinessScore` — D9；证据：全仓搜索式 `HappinessScore` 仅命中定义，0 引用（同族的 `HappinessConstraintMultiplier` 在 `BuildingManager.cs:1517`、`HappinessRewardMultiplier` 在 Resource/UI/Manager 多处被用）；建议：删除或确认是否供 UI 展示；理由：对外暴露但无人消费的派生属性。
- [Med] `Assets/Resources/Script/Misc/Singleton.cs:51-52` `public virtual void Save()/Load()` — D9；证据：全仓搜索 `\.Save\(\)` 仅命中 `PlayerPrefs.Save()`、`\.Load\(\)` 0 命中；4 处 override（`GameManager.cs:450-452`、`ResourceManager.cs:575-577`、`ResearchManager.cs:1073-1075`、`BuildingManager.cs:1954-1956`）仅定义未调用；3 个 `.unity` 场景中无 `m_MethodName: Save/Load` 持久化绑定；UI 存档按钮直接调 `SaveManager.SaveNow`（`UI/KingdomUIRoot.ReviewActions.cs:65-67`）；建议：确认无外部 UnityEvent 绑定后，删除基类钩子与 4 处 override（override 属 Manager 区块，需与 11 号协作）；理由：无调用点的空钩子。
- [Low] `Assets/Resources/Script/Math/ExpantaNum.cs:1713` `public string ToDebugString()` — D9；证据：全仓搜索式 `ToDebugString` 仅命中定义，0 调用；其专用依赖 `GetRepresentationName()`(2560) 也只被它调用；建议：保留观察（数学库调试入口，删除收益低，且受"不重做 ExpantaNum"约束）；理由：未使用但属库调试 API。

### 2.2 建议合并 / 更新（重复定义）

- [Med] `Assets/Resources/Script/Runtime/UltraProjectState.cs:20-22` `UltraProjectDoctrine.PhaseStability / DeepSpaceIndustry / GatewayHub` — D8；证据：三者与 `Stable=1 / Surge=2 / Expedition=3` 数值完全相同（同枚举内别名），全仓仅 `IsValidDoctrine`(558-559) 用 `>= PhaseStability && <= GatewayHub` 引用它们，其余所有代码（`UltraProjectManager`、`KingdomUIRoot.UltraProject`、全部测试）一律使用 `Stable/Surge/Expedition`；`UltraProjectManager.cs:489` 的 `"PhaseStabilityCertification"` 是定义 ID 字符串，与本别名无关；建议：删除三个别名，`IsValidDoctrine` 改写为 `doctrine >= UltraProjectDoctrine.Stable && doctrine <= UltraProjectDoctrine.Expedition`；理由：重命名后的历史别名，造成同一概念双名。

### 2.3 保留观察

- [Low] `Assets/KingdomAssemblyMarker.cs:1-5` `KingdomAssemblyMarker` — D9（疑似）；证据：全仓唯一处于 `Assembly-CSharp`（默认程序集）的 .cs，其余运行时代码全部在 `Kingdom.Runtime.asmdef` 下；文件自注释"Keeps the legacy Assembly-CSharp project non-empty"；`Assets/Editor/**` 位于 `Assembly-CSharp-Editor` 并自动引用 `Assembly-CSharp`；建议：保留（有意垫片，删除可能导致 Editor 程序集引用悬空）；理由：属必要构建垫片，非死代码。
- [Low] Runtime 各 State 类的 `[Serializable]` 属性 — D9/D10（疑似遗留）；证据：12 个 State 类（BuildingState/CampaignState/GameState/MilitaryState/PopulationState/RelicState/ResearchState/ResourceState/StoryProgressState/TerritoryState/UltraProjectState/WorkshopUpgradeState）内均无 `[SerializeField]`、无 public 字段（`Runtime/` 目录 `[SerializeField]` 0 命中），存档经 `SaveManager` 的 DTO 序列化，故该属性对 `JsonUtility` 无实际作用（对比 `Misc/Tool.cs` 的 `Pair`/`ResourceAmountDefinition` 确有 `[SerializeField]`，属在用）；建议：保留观察（无功能影响，删除需评估 Inspector/工具链）；理由：疑似历史直序列化遗留。
- [Low] `Assets/Resources/Script/Math/ExpantaNum.cs:10-15` `ExpantaNumFormat` + `ToGameString`（K/M/B/T 后缀、Suffix/Scientific/HyperOperation） — D7（轻度）；证据：`ToGameString` 的默认分支产出游戏化后缀文本，属"展示关切"却位于 Math 层；`ExpantaNumFormat.HyperOperation` 仅被 `Tests/Editor/KingdomLogicTests.cs` 引用，生产路径（UI 调 `ToGameString()` 不传 format）恒为 Suffix；建议：保留观察（受"不重做 BigNumber/ExpantaNum"硬约束，不主张迁移）；理由：库设计如此，无实际危害。
- [Low] `Assets/Resources/Script/Math/ExpantaNum.cs` 的超运算机制（HyperRepresentation / `Tetrate` 1165 / `IteratedLog`） — D10（轻度）；证据：玩法代码（`Assets/Resources/Script`）无 `.Tetrate(`/`.Slog(`/`.Pentate(` 直接调用（搜索 0 命中），仅库内部 `CompressHyperOperation`/`IteratedLog`/`Operator` 自用；文档注释（55 行）称"仅在玩法确实需要时使用"；建议：保留观察（属 BigNumber 既有能力，受"不重做"约束）；理由：潜在超前记数能力，但无玩法依赖。

## 3. 序列化字段专项（"不可直接删除"分组）

**结论：Runtime / Math / Misc 范围内未发现"已废弃但仍序列化"的字段。**

- Runtime 各 State 类**不直接参与序列化**：存档由 `SaveManager.CaptureSaveData()` 组装 DTO（`KingdomSaveData` 及其子类）经 `JsonUtility` 写出（`Manager/SaveManager.cs:176/564`），State 类仅作运行时权威数据侧。
- 本区块内的两个 DTO 字段**全部在用**：
  - `RelicStateSaveData`（`Runtime/RelicState.cs:17-31`）12 字段均在 `CaptureSaveData`(163)/`Restore`(179)/`Validate`(203) 中读写。
  - `UltraProjectStateSaveData`（`Runtime/UltraProjectState.cs:44-56`）10 字段均在 `CaptureSaveData`(312)/`Restore`(331)/`ValidateSaveData`(373) 中读写。
- `OfflineProgressSummary`（`Runtime/OfflineProgressSummary.cs:14`）显式注释为 "Session evidence only"，不序列化。

**给后续清理的提醒**：真正的存档字段（如 `SaveManager.cs:910/924/938` 的 `GlobalEfficiencyFactor`、`GameSaveData.PowerSatisfaction`/`LogisticsSatisfaction`）位于 Manager 区块，本次经交叉核对均为在用（有读有写）。若将来判定某存档字段陈旧，**必须先走 Editor 迁移**（参考 `Editor/Migration/` 现有工具形态）再移除，不可直接删字段。本次审计未发现此类陈旧证据。

## 4. 未发现（负向结论）

- **旧版本存档迁移代码：未发现**。`Manager/SaveManager.cs:974-977` `IsSupportedVersion` 严格 `version == SaveFormat.CurrentVersion`，`SaveFormat.CurrentVersion = 9`（`Data/SaveFormat.cs:3`），无 v1..v8 迁移分支；`RelicState`/`UltraProjectState` 的 `CurrentSaveVersion=1` 与 DTO `StateVersion` 均严格等值校验，无迁移逻辑。
- **反射：未发现**。Runtime/Math/Misc 中 `System.Reflection` / `BindingFlags` / `Activator.` / `GetMethod(` 0 命中（符合硬约束）。
- **TODO/FIXME/HACK/XXX/OBSOLETE：未发现**（Runtime/Math/Misc 全部 0 命中，故无"引入时间"可查）。
- **注释掉的大段代码 / `#if` 永久关闭分支：未发现**。`#if UNITY_EDITOR` 均为在用测试钩子（除 2.1 列出的 4 个未用钩子）。
- **几何成本"双实现"：未发现**。Math 层仅 `ExpantaNumExtensions.GeometricSeriesCost`（闭式，`Math/ExpantaNumExtensions.cs:19`）一套；`BuildingManager.cs` 全部成本/退款/可负担量路径（79、702、787、974、1026）均调用该闭式公式，未发现"精确逐个累加"的旧实现。
- **Math 层混入玩法/UI 类型：未发现**。Math 目录不引用 `Resource/Building/Sector/Research/GameState/Manager` 等游戏类型；`ExpantaNum.cs` 的 `using UnityEngine`(8) 仅服务于其自身的 `[SerializeField] private ExpantaNumOperator[] operators`(113)。
- **Math 层重复辅助函数：未发现**。`ExpantaNumExtensions` 的 `LogOnePlus(ExpantaNum)`/`LogOnePlus(double)`/`ExpMinusOne(double)`（126-180）为针对小量/近 1 的数值稳定重载，非功能重复。

## 5. 不确定项（需用户决策）

- `Singleton.Save()/Load()`（及 4 处 Manager override）是否为 Inspector/UnityEvent 或外部脚本预留的公共 API？代码与 3 个场景内均无绑定，但存在"被外部/未来调用"的可能，需确认后方可删除。
- `UltraProjectDoctrine` 三个别名是否出于对外语义兼容而刻意保留？若仅为内部使用，可安全合并。
- `GameState.HappinessScore` 是否计划供 UI 显示（当前无消费方）？

## 6. 未覆盖项

- `Assets/Resources/Script/Math/ExpantaNum.cs`（约 92 KB）按"不重做 BigNumber / ExpantaNum"硬约束，仅审计其**调用面/死 API/游戏耦合**，未逐行复核其数值算法与内部表示。
- `Manager/SaveManager.cs`、`Manager/GameManager.cs`、`Manager/BuildingManager.cs` 属 Manager 区块（11 号），本报告仅作交叉引用，不深入。
- `Assets/Editor/Migration/DefinitionIdMigration.cs` 为**定义稳定 ID 迁移**工具（非存档迁移），属 Editor 区块，未纳入结论。
