# Runtime/Data/Validation 只读审查（2026-09-11）

## 扫描范围与方法

- 审查基线：`3c6699f17fda9e4224c3de88ce0957afc4aa3b66`。
- 审查角色：全仓库只读扫描计划中的代理 C。
- 输出性质：只读审查建议；本次没有修改任何 `.cs`、`.asset`、`.unity`、`.meta`、`.json` 或既有文档。
- 本报告只覆盖 `Assets/Resources/Script/Runtime`、`Assets/Resources/Script/Data`、`Assets/Resources/Script/Misc`、`Assets/Resources/Script/Validation`。
- 已先阅读仓库根 `AGENTS.md`、`Assets/Resources/Script/AGENTS.md`，以及 `docs/architecture/runtime-state.md`、`docs/architecture/serialized-pairs.md`、`docs/architecture/ui-boundaries.md`、`docs/architecture/definition-database.md`。
- 已交叉阅读 `docs/audits/2026-09-10-full-readonly-refactor-scan.md`，但该文件存在乱码；本报告所有结论均来自本次独立扫描，不以旧审查作为直接证据。
- 死代码与调用点验证使用全仓库文本搜索，覆盖源码、测试、工具、场景、Prefab 和资产文本；排除 `.meta`、`Library/`、`Temp/`、`Logs/`、`obj/`、历史归档和旧审查输出，避免把历史文字误判为运行时调用。
- 未执行真实 Unity 编译、EditMode 测试或 PlayMode 测试。

### 文件覆盖

| 目录 | 文件数 | 行数 | 覆盖文件 |
|---|---:|---:|---|
| Runtime | 12 | 1,631 | `BuildingState.cs`、`CampaignState.cs`、`GameState.cs`、`HappinessFormula.cs`、`MilitaryState.cs`、`PopulationState.cs`、`ResearchState.cs`、`ResourceState.cs`、`RetiredDefinitionMigration.cs`、`StoryProgressState.cs`、`TerritoryState.cs`、`WorkshopUpgradeState.cs` |
| Data | 14 | 1,213 | `Building.cs`、`DataBase.cs`、`GameDefinition.cs`、`MusicCatalog.cs`、`Research.cs`、`ResearchEffect.cs`、`Resource.cs`、`SaveFormat.cs`、`SectorBuilding.cs`、`SectorDefinition.cs`、`SectorState.cs`、`StoryArchiveDefinition.cs`、`StoryChapterDefinition.cs`、`WorkshopUpgrade.cs` |
| Misc | 2 | 287 | `Singleton.cs`、`Tool.cs` |
| Validation | 5 | 1,571 | `BuildingTransactionRules.cs`、`EconomyDependencyValidator.cs`、`ProgressionMilestoneRecorder.cs`、`ResearchValidator.cs`、`SectorValidator.cs` |
| 合计 | 33 | 4,702 | 以上全部文件 |

### 当前证据

- `data/content-closure-static.md`：当前静态闭合报告显示 TechLevel 达到 5；Industrial 为 81/81 Research、37/37 Workshop、50/50 Building；Spacer 为 47/47 Research、46/46 Workshop、16/16 Building；Ultra 为 1/1 Research、0/0 Workshop、0/0 Building；资源 40 个。
- `TestResults/Latest-Test-Errors.txt`：总数 660，通过 658，失败 2。与本报告直接相关的是 `FlowEfficiencyTests.HappinessPenalizesNegativeFoodNetRateEvenWhenInventoryIsAvailable` 的精度断言问题；`SectorBuildingTests.SectorBuildingBuildAndSavePathIsOccupiedAndTerritoryFree` 属于 Manager/测试代理范围，本报告不代下结论。

## 发现清单

| 位置 | 问题 | 建议 | 优先级 |
|---|---|---|---|
| `Validation/EconomyDependencyValidator.cs:143-240` | Research、Workshop、Building 可达性验证在 6 处以 `TechLevel > TechLevel.Industrial` 过滤，运行时启动验证事实上止步于 Industrial；而静态闭合报告已经给出 Spacer 与 Ultra 事实。 | 将目标时代参数化，保留 Industrial 基线模式；为 Industrial、Spacer、Ultra、Archotech 分别添加验证测试。默认上限提升前先确认当前内容闭合与测试稳定。 | P1 |
| `Data/Building.cs:141-147` | `CostGrowth` 解析后会把小于 1 或 NaN 的值替换为默认 1.15，`HasValidCostGrowth` 检查的是归一化后的值，导致非法 authored value 常被报告为有效。当前资产均大于 1，因此这是验证语义缺陷而非当前内容错误。 | 验证原始解析值；明确契约是 `> 1` 还是 `>= 1`，并添加 NaN、Infinity、0、0.99、1、1.15 等边界测试。 | P1 |
| `Manager/GameBootstrap.cs:51-65`（影响本目录两个验证器） | `SectorValidator` 与 `EconomyDependencyValidator` 的失败只输出 warning，然后继续启动。启动验证到底是诊断工具还是正式质量门槛不明确。 | 明确契约：若为诊断工具，文档说明 warning 不阻塞；若为正式质量门槛，Editor/开发构建中区分阻断错误与提示，并在 CI 或测试中调用验证器。 | P2 |
| `Validation/EconomyDependencyValidator.cs:145-147` | 可达性辅助方法接收 `resources` 参数，但实际读取 `DataBase<Resource>.All` 查找 `WoodLog`，违反参数契约，并让调用方传入的列表失去意义。 | 改为使用传入的 `resources`。在当前调用图传入同一列表时可保持行为不变；随后补一个不同列表的单元测试锁定契约。 | P2 |
| `Data/Tool.cs` 的 `ResourceAmountDefinitionList.ToPairs`、相关验证器 | 资源数量定义没有统一拒绝重复资源、NaN、Infinity、负数和空引用；`ResearchState.GetRequiredResourceCost` 会把重复项相加，可能静默改变设计意图。 | 为所有资源列表定义统一验证入口：资源 ID 非空、存在于数据库、数量有限且非负、无重复。重复语义若确需叠加，必须在定义层显式声明并测试。 | P2 |
| `Validation/SectorValidator.cs` | 未验证 `OccupiedResourceRatesPerSecond`；奖励、殖民成本和战役成本大多检查 NaN/负数，但没有完整检查 Infinity、重复资源和空引用。 | 将 Sector 的所有资源列表纳入同一资源列表验证规则；对持续时间与进度倍率先验证原始值，再决定是否允许 clamp。 | P2 |
| `Data/Research.cs:9`、`Runtime/ResearchState.cs` | `BaseCost` 是 public writable 字段，解析后没有完整拒绝 Infinity；定义对象后续被改动时，已存在的 ResearchState 不会自动重新解析。 | 逐步改为 `[SerializeField] private` 加只读属性；加载或验证时检查有限性和非负性，并明确定义不可变的运行时契约。 | P2 |
| `Runtime/GameState.cs:18-42` | `GameState` 直接读取 `ProgressionModifierManager.Current` 静态状态。该字段是派生缓存，但依赖关系隐式，恢复顺序出错时容易产生难以追踪的状态漂移。 | 优先将 `ProgressionModifierState` 显式传入状态计算；短期至少在 `runtime-state.md` 写清重建顺序：先恢复/重建 modifier，再恢复派生 GameState。 | P2 |
| `Data/Building.cs`、`Data/Research.cs`、`Data/Resource.cs`、`Data/SectorDefinition.cs`、`Data/StoryArchiveDefinition.cs`、`Data/StoryChapterDefinition.cs`、`Data/WorkshopUpgrade.cs`、`Data/ResearchEffect.cs` | 多数 ScriptableObject 定义暴露 public writable 字段或属性，运行时任何代码都能改动定义，削弱“定义不可变、State 唯一可变”的边界。 | 分批迁移为 `[SerializeField] private` + 只读公开属性，Editor-only setter 保留在 `#if UNITY_EDITOR`。每次迁移必须保留 Unity 序列化字段名、`.meta` 和资产兼容性。 | P2 |
| `Data/MusicCatalog.cs` | `ReplaceEntries` 是运行时定义修改入口，当前唯一调用方是 `Assets/Editor/MusicAddressablesConfigurator.cs`。 | 包在 `#if UNITY_EDITOR` 中，或迁移到 Editor-only helper，避免玩家运行时代码看到定义修改 API。 | P2 |
| `Data/WorkshopUpgrade.cs:86-164` | Workshop 效果应用逻辑直接写在 Data 定义里，并修改 `ProgressionModifierState`，与 Data/State/Manager 边界不一致。 | 将效果应用移动到 `ProgressionModifierManager` 或专门 applier；定义只保留效果数据。迁移时保持公式、顺序和状态字段不变，并补 Workshop 全量应用/撤销回归测试。 | P2 |
| `Data/SectorState.cs` | 该类型是可变运行时状态，但放在 `Data/` 目录，容易与 ScriptableObject 定义混淆。 | 连同 `.meta` 移动到 `Runtime/`，并更新 `runtime-state.md` 的 State 所有权清单；若暂缓移动，则在文档中明确这是历史命名例外。 | P2 |
| `Data/SectorDefinition.cs` | `EnemyPower`、`TerritoryReward`、殖民/战役资源速率、持续时间、进度倍率等标量字符串在每次访问时重复解析；部分属性在验证前 clamp，掩盖 authored error。 | 增加解析缓存可保持当前返回值和性能语义；缓存稳定后再把“原始值验证”和“运行时 clamp”拆开。不要在同一次重构中改变数值行为。 | P2 |
| `docs/architecture/serialized-pairs.md` | 文档表述容易让人理解为 Building/Research 资产已经直接序列化为 `Pair`；实际资产仍使用 `ResourceAmountDefinition` 的 `resource`/`amount`，运行时 API 再转换为 `Pair`。 | 更新文档，区分“资产作者可见序列化格式”和“运行时 API/保存契约”。不要为了文档一致性去重写资产序列化格式。 | P2 |
| `Validation/EconomySimulationParity`、`tools/NewEconomySimulator` | `EconomySimulationParity` 并没有被外部模拟器引用，主要服务 EditMode 测试；`ValidateDelta` 拒绝负数但不检查 NaN/Infinity；Research 速度公式在 Manager、parity 层和模拟器之间存在重复。 | 二选一：把模拟器改为显式引用同一 parity 公式层，或将该 helper 定位为测试专用并移近测试；无论哪种，先补 NaN/Infinity 与公式等价测试，再消除重复实现。 | P2 |
| `Runtime/GameState.cs` 的 Restore 系列、`Runtime/ResearchState.cs` | 多个 `Restore*` 方法无条件递增 `Version`；`ResetPaidResourceCosts` 清空非空账本却不递增版本。保存与 UI 变更通知的版本语义不一致。 | 定义明确规则：恢复、事务回滚、快照回滚是否递增版本；为每次恢复和 reset 前后的 `Version` 写测试，避免 UI 刷新丢失。 | P2 |
| `Runtime/HappinessFormula.cs`、`Assets/Tests/.../FlowEfficiencyTests` | 当前幸福度测试失败来自测试精度：`ExpantaNum` 以 6 位小数量化，期望精确 `1/11`，实际约为 `0.090909`。 | 不要改公式。把断言改为关系断言或至少 `5e-7` 的容差，并在测试旁说明量化原因。 | P2 |

| `Runtime/ResourceState.cs` | `BeginTick` 与 tick 调整路径未统一检查有限值，NaN/Infinity 一旦进入会污染后续库存与产量。 | 在 State 入口集中检查有限性，或在生产/消耗计算源头拒绝非有限值。先补污染传播测试，再决定拦截层级。 | P3 |
| `Runtime/CampaignState.cs` | `combatRatio` 无效时错误信息使用 `nameof(casualties)`，定位信息误导。 | 改为 `nameof(combatRatio)` 或同时列出两个字段。 | P3 |
| `Validation/SectorValidator.cs:84-94`、`:278-280` | `ValidateCosts` 的具体错误随后被 generic message 覆盖；`:278` 的乱码赋值也立即被下一行覆盖。 | 删除被覆盖的无效赋值；错误聚合时保留具体子错误，避免只留下泛化信息。 | P3 |
| `Data/SectorBuilding.cs:21-30` | 使用双重转义的 `\\u201c`、`\\u201d`，运行时消息会显示字面 `\u201c` 而不是中文引号。 | 改为直接 UTF-8 中文引号或单次 `\u201c` 转义，并用测试锁定期望文本。 | P3 |
| `Misc/Singleton.cs` | 使用 legacy `FindObjectOfType`；`Save()`/`Load()` 虚方法在运行时和场景调用图中没有直接使用，主要作为 Manager override 与测试表面存在。 | 不建议立即删除 override 链。可先标注测试/兼容用途；若后续统一存档入口，再整体移除该表面并同步删除 override。 | P3 |
| `Misc/Tool.cs` | 同一文件混合 Pair 序列化、资源列表转换、UI 颜色格式化和枚举描述，职责过宽。 | 按调用方拆分为 serialization、resource conversion、UI formatting 小类；仅移动代码和命名空间，不改变转换逻辑。 | P3 |
| `Validation/ProgressionMilestoneRecorder.cs` | 这是运行时 MonoBehaviour 诊断组件，但放在 `Validation/`，与静态定义验证器混在一起。 | 移动到 diagnostics/runtime instrumentation 目录，或文档明确该目录同时包含启动验证与运行诊断。 | P3 |
| `Runtime/*State.RestoreExact` 系列 | 这些方法绕过普通验证，但当前用于事务回滚和快照恢复，不是普通 save-load 路径；直接删除会破坏回滚。 | 保留方法，改名为 `RestoreSnapshot`/`RestoreRollback` 或在 XML/docs 中写明“仅内部事务恢复，禁止普通加载使用”。 | P3 |
| `Runtime/GameState.RestoreCore` | 恢复时先给 FoodCapacity 设置下界，依赖后续 Manager 重建修正，隐含顺序契约。 | 保持行为不变的前提下抽取“临时下界 + 后续重建”的显式方法名或文档说明；不要在审查后直接改变恢复顺序。 | P3 |
| `Runtime/WorkshopUpgradeState.cs` | 其他核心 State 有 `ResetForLoad`，该 State 没有对应模式，恢复路径风格不一致。 | 如果存在实际重建需求，补齐同语义 reset；若不需要，在 State 模式文档中说明差异。 | P3 |
| `docs/architecture/runtime-state.md` | 文档中存在字面 `\n`、重复的 “Current implementation note” 段落，且未完整列出 Sector、Story、Workshop 等子状态所有权。 | 修正文档格式并补全 State 所有权矩阵；这不改变代码。 | P3 |
| `Data/StoryArchiveDefinition.cs`、Manager/StoryManager | 固定章节 ID 在定义和 Manager 两处重复，后续修改容易漂移。 | 提取到单一常量或定义入口，并保留现有 ID 字符串以兼容保存。 | P3 |

## 死代码清单

以下结论均经过全仓库调用点搜索验证。搜索范围包含 `Assets/`、`tools/`、`docs/` 中的可执行示例、场景与 Prefab 文本；排除 `.meta`、生成目录、`.codex/archive/` 和旧审查文档。对同名方法均确认了所属文件作用域，避免把其他文件中的同名调用误判为本文件调用。

| 位置 | 成员 | 调用点验证结果 | 建议 |
|---|---|---|---|
| `Runtime/PopulationState.cs` | `AdvanceDeparture()` | 全仓库零调用。 | 可删除。 |
| `Runtime/PopulationState.cs` | `CalculateDepartureRate()` | 仅被已死亡的 `AdvanceDeparture()` 调用。 | 可随 `AdvanceDeparture` 一并删除。 |
| `Runtime/GameState.cs` | `HappinessScore` | 全仓库零调用。 | 可删除。 |
| `Runtime/GameState.cs` | `RefundFood()` | 全仓库零调用。 | 可删除。 |
| `Runtime/ResearchState.cs` | 三参数 `Restore` 重载 | 全仓库零调用；实际路径使用四参数重载。 | 可删除三参数重载，保留现有四参数路径。 |
| `Data/SectorDefinition.cs` | `MapY` 属性 | 全仓库零调用；序列化资产使用字段 `mapY`。 | 可删除属性，但必须保留序列化字段 `mapY`。 |
| `Data/SectorDefinition.cs` | `SetRewardsForEditor()` | 全仓库零调用。 | 可删除。 |
| `Data/SectorDefinition.cs` | `SetLocationForEditor()` | 全仓库零调用。 | 可删除。 |
| `Data/SectorDefinition.cs` | `SetColonizationCostsForEditor()` | 全仓库零调用。 | 可删除。 |
| `Data/SectorDefinition.cs` | `SetCampaignCostsForEditor()` | 全仓库零调用。 | 可删除。 |
| `Data/Building.cs` | `SetRequiredResearchForEditor()` | 全仓库零调用。 | 可删除。 |
| `Data/Building.cs` | `SetRequiredWorkshopUpgradesForEditor()` | 全仓库零调用。 | 可删除。 |

| `Runtime/BuildingState.cs` | 私有 `Change(ref bool, bool)` 重载 | 本文件与全仓库零调用。 | 可删除。 |
| `Runtime/TerritoryState.cs` | 文件内 `NormalizeFiniteNonNegative` | 本文件零调用；未发现跨文件可访问调用。 | 可删除。 |
| `Misc/Tool.cs` | `NullOrEmpty()` | 全仓库零调用。 | 可删除。 |
| `Misc/Tool.cs` | `GetDescription(this Enum)` | 全仓库零调用。 | 可删除。 |
| `Validation/SectorValidator.cs` | 文件内 `BuildCycleError()` | 本文件零调用；`ResearchValidator` 中的同名方法存在调用，不能一并删除。 | 仅删除 `SectorValidator` 内的版本。 |
| `Validation/EconomyDependencyValidator.cs` | `if (false && cycles.Count > 0)` 块 | 恒不可达。 | 可删除。 |
| `Validation/EconomyDependencyValidator.cs` | 注释掉的旧 `FindProductionCycles` 代码 | 注释代码，无运行时调用。 | 可删除注释块；如需历史，由 Git 保存。 |
| `Validation/SectorValidator.cs:278-279` | 立即被覆盖的乱码错误赋值 | 该赋值对最终错误输出没有影响。 | 可删除被覆盖赋值，输出不变。 |
| `Validation/BuildingTransactionRules.cs` | `UpgradeCostDelta()` | 全仓库零外部调用。 | 可删除。 |
| `Validation/BuildingTransactionRules.cs` | `GeometricUnitCost()` | 仅被已死亡的 `UpgradeCostDelta()` 调用。 | 可一并删除。 |

预计可安全移除约 **170–180 行**。该估算包含方法体、孤立 using、注释块和无效赋值，不包含下述“测试-only/兼容 API”。

### 不是死代码的保留项

| 成员 | 实际用途 | 结论 |
|---|---|---|
| `BuildingTransactionRules.TryNormalizePositiveWhole` | EditMode 测试与运行时交易规则共用。 | 不应按零玩家路径删除。 |
| `BuildingTransactionRules.Total` | 测试调用。 | 测试-only API，需标注而非删除。 |
| `EconomySimulationParity` 大多数方法 | EditMode 测试调用。 | 测试-only 或待共享的 parity 层，不是零调用死代码。 |
| `HappinessFormula.CalculateMultiplier` 较短重载 | 测试或调用便利入口。 | 不应按零运行时调用直接删除。 |
| `Singleton.Save()` / `Load()` 及 Manager override | 存在 override 链与测试/兼容用途。 | 属于遗留表面，需整体设计后处理。 |

## 逻辑不变精简建议

1. **删除全部已验证死代码。** 为什么逻辑不变：所有成员在当前调用图中不可达或零调用；删除它们不改变可达执行路径。`SectorDefinition.MapY` 需要保留序列化字段，只删属性。
2. **删除 `EconomyDependencyValidator` 的 `if (false ...)` 与注释代码。** 为什么逻辑不变：布尔常量使分支不可达，注释代码没有编译语义。
3. **删除 `SectorValidator` 中立即被覆盖的错误赋值。** 为什么逻辑不变：最终错误字符串已经由后续赋值决定，前置赋值不可观察。
4. **`EconomyDependencyValidator` 可达性辅助改用参数 `resources`。** 为什么当前逻辑不变：现有调用传入的列表与 `DataBase<Resource>.All` 等价；改完后应补一个非等价列表测试，把参数契约变成显式行为。
5. **为 `SectorDefinition` 标量字符串建立解析缓存。** 为什么逻辑不变：首次和后续访问返回同一解析/clamp 结果，仅减少重复解析；不要在缓存重构中同步改变 clamp 语义。
6. **将验证器中重复的“有限且非负”判断抽成一个私有 helper。** 为什么逻辑不变：helper 必须逐字保留 `double.IsNaN`、`double.IsInfinity`、`< 0` 的组合顺序和边界；只减少重复，不扩大接受范围。
7. **将 `EconomyDependencyValidator` 六处 `TechLevel > TechLevel.Industrial` 谓词抽成一个命名函数。** 为什么逻辑不变：第一版只封装同一谓词，不改变过滤结果；参数化时代上限作为第二步单独测试。

8. **移动 `SectorState.cs` 到 `Runtime/`。** 为什么逻辑不变：仅改变文件位置和命名空间引用，保留 `.meta` 与序列化字段；需要 Unity 编译验证。
9. **按职责拆分 `Tool.cs`。** 为什么逻辑不变：只移动现有静态方法，输入输出和转换顺序不变；调用方更新 using 后行为一致。
10. **集中固定 Story 章节 ID 常量。** 为什么逻辑不变：保留完全相同的字符串值，仅消除两处字面量重复，从而不影响保存兼容性。
11. **将 `ProgressionMilestoneRecorder` 移出 `Validation/`。** 为什么逻辑不变：只调整目录/命名空间和引用，不改记录逻辑；需随 asmdef 或引用一起验证。
12. **将 `MusicCatalog.ReplaceEntries` 包在 `#if UNITY_EDITOR`。** 为什么玩家行为不变：当前唯一调用方是 Editor 工具；但会改变编译表面，应通过 Editor 与玩家构建双验证。

以下事项**不属于**逻辑不变精简，必须作为行为修复单独处理：原始 `CostGrowth` 验证、资源重复拒绝、`ResetPaidResourceCosts` 版本递增、Sector 错误信息保留、Campaign 错误字段名、幸福度测试容差。

## 明确不建议动的部分及理由

- **Runtime State 核心架构**：扫描到的 State 类普遍使用 private 字段、只读公开属性和内部 mutator，整体符合“Runtime State 是唯一可变权威”的方向。不要为了形式统一而大规模重写。
- **Food 唯一上限**：未发现普通资源容量、MaxAmount 或仓储建筑；不要添加普通资源容量。`GameState` 中与 FoodCapacity 相关的恢复下界应只做文档化或顺序显式化。
- **workforce**：四个目录中未发现 workforce 概念，不要在重构中重新引入。
- **反射**：四个目录中未发现 `System.Reflection`、`BindingFlags`、运行时 `Invoke` 或 `Activator`。后续修改也不应引入。
- **`SectorBuilding` 纳入 `DataBase<Building>.All`**：这是当前设计的一部分，不能因“看起来是特殊建筑”而从全量定义中剔除。
- **`RestoreExact` 系列**：它们服务事务回滚和快照恢复，不能按“绕过普通验证”直接删除；应命名或文档化。
- **`Pair.Deconstruct`**：架构文档要求保留该 API；本报告不将其列为死代码。
- **测试-only API**：`BuildingTransactionRules`、`EconomySimulationParity`、`HappinessFormula` 中部分入口虽不在玩家路径，但被测试使用，不能按运行时调用点为零直接删除。
- **保存兼容与稳定 ID**：不要为了代码整洁改变 DTO 字段名、版本号语义、稳定 ID 或 v7+ 兼容路径。任何字段重命名都必须有迁移和测试。
- **数学高级未调用 API**：不属于代理 C 范围。本报告不对其做删除建议；相关未来价值评估由数学 API 代理负责。
- **当前幸福度公式**：测试失败由 `ExpantaNum` 六位小数量化与过严精确断言引起，不应修改公式迁就测试。
- **`StoryArchiveDefinition.OnValidate`**：已覆盖固定章节数量/顺序、空字段、重复 ID、时代倒退、重复引用和探索进度，当前结构较完整；只建议处理固定 ID 重复，不建议重写验证器。

## 与 2026-09-10 旧审查的交叉复核

| 旧审查主题 | 本次独立复核结果 |
|---|---|
| Economy validator 止步 Industrial | 仍成立，是本报告 P1。 |
| `if (false)` 与注释掉的循环代码 | 仍成立，可安全删除。 |
| Sector validator 死 `BuildCycleError` 与乱码覆盖行 | 仍成立；注意 `ResearchValidator` 中同名方法仍在使用。 |
| `GeometricUnitCost` / `UpgradeCostDelta` 死代码 | 仍成立，两者可一并删除。 |
| 三参数 `ResearchState.Restore` 死重载 | 仍成立。 |
| `ResourceState` 有限值检查缺口 | 仍成立，列为 P3。 |
| `SectorDefinition` 标量重复解析 | 仍成立，列为 P2；建议先缓存再改验证。 |
| GameState Restore 版本语义不一致 | 仍成立，列为 P2。 |
| `WorkshopUpgradeState` 缺 `ResetForLoad` 模式 | 仍成立，列为 P3。 |
| `Tool.cs` 职责混杂 | 仍成立，列为 P3。 |
| `SectorState.RestoreExact` 是普通保存 bug | 本次修正：它用于回滚/快照恢复，不应删除；问题是命名与契约不清晰。 |
| 可删除 `Pair.Deconstruct` | 本次修正：架构要求保留，不应删除。 |
| Population epsilon 是公式 bug | 本次未确认公式缺陷；当前幸福度失败可由 `ExpantaNum` 六位小数量化解释。 |
| `GameDefinition.SetIdForEditor` 暴露运行时修改 | 本次修正：该方法已在 `#if UNITY_EDITOR` 内。 |
| Pair 文档应要求资产直接迁移为 `Pair` | 本次修正：资产仍序列化 `ResourceAmountDefinition`，运行时 API 转换为 `Pair`；文档应区分两层契约，而不是重写资产。 |

## 建议验证清单

1. 为 `EconomyDependencyValidator` 添加 Industrial、Spacer、Ultra、Archotech 四组可达性测试；先参数化，再考虑默认上限。
2. 为 `Building.CostGrowth` 增加原始值边界测试：NaN、Infinity、0、0.99、1、1.15。
3. 为资源数量列表增加重复、空引用、未知 ID、NaN、Infinity、负数测试，并覆盖 Research、Building、Sector 奖励、殖民成本、战役成本和 occupied rates。
4. 为 Research `BaseCost` 的有限性与定义不可变性添加测试。
5. 为 State 恢复和 reset 的 `Version` 变化添加显式断言，覆盖 GameState、ResearchState、WorkshopUpgradeState。
6. 为 `EconomySimulationParity.ValidateDelta` 增加 NaN/Infinity 测试。
7. 将幸福度测试改为容差不小于 `5e-7` 的断言，并保留量化说明。
8. 若执行任何死代码删除或目录移动，必须运行 Unity 编译、EditMode 全量测试和相关 PlayMode 测试；本审查阶段未执行。

## 假设与边界

- 本报告把 `Manager/GameBootstrap.cs` 的调用行为作为验证器上下文读取，但没有审查 Manager 层内部实现；Manager 层结论由代理 A 负责。
- `SectorBuildingTests` 的失败涉及 Manager/测试路径，本报告只记录测试证据，不越界归因。
- `04-math-api-future-value.md` 与 `05-story-content.md` 是其他代理的输出；本报告未读取其结论作为证据，也未修改。
- 本报告对“死代码”的定义是当前仓库调用图不可达或零调用，不包含计划中的 Editor 工具、测试-only API 和保存兼容表面。
- 所有建议均遵守：不引入反射、不添加普通资源上限、不引入 workforce、保持 Runtime State 唯一可变权威、保持 Manager/UI/State 边界。
