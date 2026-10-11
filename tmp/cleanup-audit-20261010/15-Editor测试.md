# 15 · Editor 测试审计（Assets/Tests/Editor/）

> 只读盘点，未修改任何文件，未运行 Unity/测试。基准：当前磁盘文件 + `git log`。
> 审查准则：`tmp/cleanup-audit-20261010/00-审查准则.md`。

---

## 1. 范围

- 路径：`D:\GitHub\Kingdom\Assets\Tests\Editor\`
- 文件：**62 个 .cs**（含 `.meta` 共 124 项），合计 **24294 行**。
- 测试方法：约 **600+** 个 `[Test]`/`[TestCase]`（其中 `[TestCase]` 多例展开后更多）。全部 `[Test]` 统计：**0 个 `[UnityTest]`**，均为 NUnit EditMode 测试。
- 未覆盖：`Assets/Tests/PlayMode/`（其他子代理负责）；`.meta` 内容；测试引用的 `Assets/Resources/Script/**` 生产源码（仅按需 grep 交叉核对）。
- 全目录 **无 `[Ignore]`、无 `[Explicit]`、无 `#if` 条件编译、无被注释掉的测试**（grep 全 62 文件零命中）——即当前不存在"被关闭的测试"。
- 全目录 **无 TODO/FIXME/HACK/XXX/OBSOLETE 注释**（grep 零命中；仅 4 处方法名含 "Legacy"，属正常命名）。
- git 状态：62 个 `.cs` 均在 HEAD 中；当前工作树另新增 4 个（`AuditRuntimeRegressionTests`、`EraInventoryShortfallTests`、`InvestmentChoiceTests`、`StrategicPreviewRegressionTests`，已 staged 未提交）+ 修改 4 个（`RelicCampaignTests`/`RelicManagerTests`/`StoryManagerTests`/`TutorialManagerTests`）。

### 1.1 文件清单与主题分组（行数）

| 主题 | 文件（行数） |
|---|---|
| **核心逻辑/存档（巨型文件）** | `KingdomLogicTests.cs`(3771, 123 测试)、`SectorManagerTests.cs`(2899, 95)、`EconomyIntegrationRegressionTests.cs`(728)、`SaveArchivePressureTests.cs`(301)、`SaveManagerUltraProjectEraValidationTests.cs`(92) |
| **内容闭合/审计** | `C6IndustrialContentTests.cs`(1609, 63)、`C5ContentClosureAuditTests.cs`(91)、`C6IndustrialClosureAuditTests.cs`(92)、`ContentProgressionValidatorTests.cs`(222)、`ContentVerticalSliceAuditTests.cs`(267)、`FirstThreeErasContentDefinitionTests.cs`(78)、`GlobalEconomyDefinitionTests.cs`(659) |
| **建筑/生产/部门** | `BuildingCostGrowthTests.cs`(299)、`BuildingVerticalSliceTests.cs`(324)、`SectorBuildingTests.cs`(353)、`SectorDefinitionTests.cs`(436)、`IndustrialWorkshopAvailabilityTests.cs`(209)、`LateFactoryConsolidationTests.cs`(86) |
| **研究** | `ResearchBalanceTests.cs`(634)、`ResearchEffectTests.cs`(835)、`ResearchPowerTests.cs`(68)、`ResearchPaymentAutoTests.cs`(120)、`ResearchMedievalContentTests.cs`(103)、`MedievalResearchRoleTests.cs`(59)、`SpacerResearchPlacementTests.cs`(187) |
| **经济/效率** | `FlowEfficiencyTests.cs`(134)、`FoodEfficiencyTests.cs`(152)、`ResourceContinuityTests.cs`(644)、`EarlyVerticalSlicePacingTests.cs`(101)、`InvestmentChoiceTests.cs`(159)、`AuditRuntimeRegressionTests.cs`(251)、`StrategicPreviewRegressionTests.cs`(116) |
| **太空/星际/材料** | `SpaceProgressionTests.cs`(2169, 53)、`SpaceContentTests.cs`(146)、`AdvancedMaterialProgressionTests.cs`(594)、`PreSpacerCombatRemovalTests.cs`(78) |
| **遗迹** | `RelicCampaignTests.cs`(338)、`RelicManagerTests.cs`(323)、`RelicSimulationTests.cs`(214)、`RelicStateTests.cs`(169)、`RelicSaveTests.cs`(52) |
| **Ultra 项目** | `UltraContentSliceTests.cs`(352)、`UltraProjectStateTests.cs`(396)、`UltraProjectManagerTickTests.cs`(306)、`UltraR2EconomyBoundaryTests.cs`(199) |
| **离线/模拟** | `OfflineProgressSummaryTests.cs`(142)、`OfflineSimulationBoundaryTests.cs`(20)、`SimulationDeterminismTests.cs`(165)、`SimulationBudgetTests.cs`(357)、`LongHorizonSimulationTests.cs`(67)、`UnsafeAreaTickerTests.cs`(188) |
| **剧情/教程/进度** | `StoryManagerTests.cs`(393)、`StoryIllustrationTests.cs`(68)、`TutorialManagerTests.cs`(702)、`ProgressionMilestoneRecorderTests.cs`(181) |
| **UI/配置/音频** | `P40UiConfigurationTests.cs`(176)、`MobileOrientationConfigurationTests.cs`(64)、`SafeAreaFitterTests.cs`(33)、`AudioFeedbackRegressionTests.cs`(130)、`KingdomUiLifecycleTests.cs`(28)、`WorkshopBenefitPreviewTests.cs`(113)、`EraInventoryShortfallTests.cs`(52) |

---

## 2. 反射测试清单（重点 · 单独一节）

**硬约束 D7：新增代码与测试禁止使用反射**（`System.Reflection` / `BindingFlags` / `Activator` / 反射式 `Invoke`）。以下为现存反射测试**完整清单**（`using System.Reflection;` 出现在 4 个文件）。**未发现** `Activator.CreateInstance`、反射式 `MethodInfo.Invoke`、`MakeGenericType`（grep 零命中；`ScriptableObject.CreateInstance<T>` 与 `Enum.GetValues/GetValue`、`Exception.GetType().Name`、字典 `TryGetValue` 均为 Unity/BCl 常规 API，**不算反射**）。

| # | 文件:行 | 测试方法 | 使用的反射 API | 断言性质 |
|---|---|---|---|---|
| R1 | `GlobalEconomyDefinitionTests.cs:130-141` | `DefinitionNumericEditorFieldsRemainStringBacked` | `using System.Reflection`(:4)、`GetFields(BindingFlags.Instance\|Public\|NonPublic)`(:130-133)、`FieldInfo.FieldType` | 遍历所有定义类字段，禁止字段类型为 `ExpantaNum` |
| R2 | `GlobalEconomyDefinitionTests.cs:158-182` | `定义序列化字段必须使用字符串数值且统一采用每秒变化率` | `BindingFlags`(:158)、`GetFields(flags)`(:163)、`FieldInfo.GetCustomAttributes(false)`(:169)、`GetProperties(flags)`(:175)、`PropertyInfo` | 字段/属性名不得含 Minute；序列化字段不得为 `ExpantaNum` |
| R3 | `GlobalEconomyDefinitionTests.cs:252-256` | `ResearchNoLongerOwnsBuildingUnlockData` | `BindingFlags`(:252)、`typeof(Research).GetField("BuildingUnlock",flags)`(:254)、`typeof(ProgressionModifierState).GetField("UnlockedBuildings",flags)`(:256) | **负向存在性**：字段必须不存在 |
| R4 | `ResearchBalanceTests.cs:173-179` | `研究定义不再携带布局坐标或系统路由字段` | `using System.Reflection`(:3)、`BindingFlags`(:173)、`GetField("x"/"y"/"X"/"Y"/"SystemID"/"systemID",flags)` | **负向存在性**：字段必须不存在 |
| R5 | `KingdomLogicTests.cs:220-235` | `普通资源不应拥有独立容量上限` | `using System.Reflection`(:5)、`GetMembers(BindingFlags.Instance\|Public\|NonPublic)`(:221,:225)、`MemberInfo.Name` | **负向存在性**：成员名不得含 Capacity/MaxAmount |
| R6 | `SectorBuildingTests.cs:40-44` | `SectorBuildingInheritsBuildingWithoutDuplicateBuildingFields` | `using System.Reflection`(:3)、`GetFields(BindingFlags.Instance\|Public\|NonPublic\|DeclaredOnly)`(:40-41)、`FieldInfo`、`System.Array.Exists` | **正向存在性**：私有字段 `sector`/`maxAmount` 必须存在且恰为 2 个 |
| R7 | `FlowEfficiencyTests.cs:32-35` | `PowerAndLogisticsAreDerivedFieldsNotSaveInventoryFields` | `typeof(SaveManager.GameSaveData).GetField("PowerProduction"/"LogisticsProduction"/"PowerSatisfaction"/"LogisticsSatisfaction")`（默认 flag） | 混合：2 个负向 + 2 个正向存在性 |
| R8 | `FlowEfficiencyTests.cs:119-121` | `FoodAvailabilityAndHappinessFieldsReplaceLegacyFoodState` | `typeof(GameState).GetProperty("FoodSatisfaction"/"FoodAvailability"/"HappinessMultiplier")`（默认 flag） | 1 负向 + 2 正向属性存在性 |
| R9 | `C6IndustrialClosureAuditTests.cs:83-89` | `C604_PowerAndLogisticsRatesRemainDerivedOutsideSaveDtos` | `fieldType.GetFields()`（默认 flag，遍历 `SaveManager.GameSaveData`/`KingdomSaveData`） | **负向存在性**：字段名不得为 `PowerProductionRate` 等 |

**共性判定（建议：更新，全部改写为强类型 DTO/公开 API 断言；不照抄历史反射测试）：**

- [置信度 High] 上述 9 条全部命中 **D7（与硬约束冲突）**。R1–R9 是历史遗留的反射测试，项目规则明确"不照抄历史反射测试"，现仍留在测试集里。证据：`using System.Reflection;` 于 `GlobalEconomyDefinitionTests.cs:4`、`KingdomLogicTests.cs:5`、`ResearchBalanceTests.cs:3`、`SectorBuildingTests.cs:3`。
- [置信度 High] **R3/R4/R5/R9 的"负向存在性"断言无保护力**：`GetField("BuildingUnlock")` 之类断言"字段不存在"，一旦真实字段被**改名**（而非删除），断言依旧通过——它只能捕获"新增同名冲突"，不能捕获"改名漂移"。这是本次审查最值得上报的反射测试脆弱点。建议改为对公开强类型 API（如 `Research.Prerequisites`/`Building.RequiredResearch`）的行为断言。
- [置信度 Med] **R6 直接断言私有序列化字段名** `sector`/`maxAmount`（`SectorBuildingTests.cs:43-44`）：重命名即红，且强制实现细节。建议改为断言 `SectorBuilding.Sector`/`.MaxAmount` 公开属性语义。
- [置信度 Med] **R1/R2 依赖 `GetCustomAttributes(false)` 按字符串名匹配 `"SerializeField"`**（`GlobalEconomyDefinitionTests.cs:169-170`）：用字符串比较属性类型名，等价弱反射，同样随命名/程序集变化而失效。
- [置信度 Low] R7/R8 未传 `BindingFlags`，仅扫 public 成员，实际等价于"公开字段/属性存在性检查"，风险最低但仍属反射。

---

## 3. 结论清单

### 3.1 建议更新（按维度）

- [置信度 High] `Assets/Tests/Editor/GlobalEconomyDefinitionTests.cs`、`ResearchBalanceTests.cs`、`KingdomLogicTests.cs`、`SectorBuildingTests.cs`、`FlowEfficiencyTests.cs`、`C6IndustrialClosureAuditTests.cs` — 维度 **D7**；证据：见 §2 表 R1–R9；建议：**更新**，将 9 条反射断言改写为公开强类型 API 断言；理由：命中硬约束，且多数为弱负向断言。

- [置信度 Med] `Assets/Tests/Editor/C6IndustrialContentTests.cs:696` — 维度 **D9（死断言）**；证据：`Assert.That(1.5d * 1.25d * 1.25d, Is.EqualTo(2.34375d).Within(0.000001d))` 为**常量恒等式**，不触碰任何生产代码（该测试上文 `:682-694` 已真实校验 WorkshopUpgrade 效果与 Label）；建议：**删除该行**（或改为断言三级全局食物倍率通过 `ProgressionModifierManager` 合成后的真实结果）；理由：纯算术自证，零覆盖价值。

- [置信度 Med] `Assets/Tests/Editor/EarlyVerticalSlicePacingTests.cs:78,83,88` — 维度 **D10/脆弱断言**；证据：`double.Parse(DataBase<Research>.Find("Industrialization").BaseCost)` `Is.EqualTo(336000d)`、`Steelmaking`→`181440d`、`MechanicalEngineering`→`133920d`（无 `Within`）；建议：**更新**为范围/关系断言（如 `> 上一时代成本` 且 `< 预算上界`）或加容差；理由：把设计平衡数值写死进测试，数值调参即红。

- [置信度 Med] `Assets/Tests/Editor/BuildingVerticalSliceTests.cs:103` — 维度 **脆弱断言**；证据：`lumberyard.ProductivityConsumption.ToDouble(), Is.EqualTo(4d)`（无容差）；建议：**更新**加容差或改为关系断言；理由：手写数值精确相等，浮点/平衡调参易碎。

- [置信度 Low] `Assets/Tests/Editor/SimulationDeterminismTests.cs:24-35` — 维度 **脆弱断言**；证据：`CalculateOfflineElapsedSeconds(...)` 结果 `Is.EqualTo(270d/120d/0d/0d)` 无容差；建议：**保留观察**（输入为整秒，精确结果可接受）；理由：当前语义是整数秒计算，风险低。

### 3.2 建议合并（重复覆盖）

- [置信度 High] `C5ContentClosureAuditTests.cs` ↔ `C6IndustrialClosureAuditTests.cs` — 维度 **D8/D2**；证据：两者都调用同一 `ContentProgressionAudit.Run(DataBase<Resource>.All, DataBase<Building>.All, DataBase<Research>.All, new[]{"WoodLog"}, TechLevel.Animal)`（`C5:31-36`、`C6:50-55`），且都断言 `HighestTechLevel >= TechLevel.Industrial`（`C5:38`、`C6:57`）；C6 的 `IndustrialBuildingIds`/`IndustrialResourceIds` 清单覆盖并超越了 C5 的 `C5ResearchIds`/`C5BuildingIds`；建议：**合并**为单个参数化内容闭合审计（保留最全的 C6 清单）；理由：C5 是 C6 的严格子集。

- [置信度 Med] `ContentProgressionValidatorTests.EveryReleasedResource_HasSourceAndSink`(`:22`) ↔ `GlobalEconomyDefinitionTests.ReleasedResourcesExistAndIntegratedDependencyGraphIsReachable` ↔ `C5ContentClosureAuditTests.C504_MedievalStrategicResourcesHaveSourcesAndSinks`(`:51`) ↔ `C6IndustrialClosureAuditTests.C604_...`(`:48`) — 维度 **D8**；证据：四者均为"资源有源有汇"的同构检查，前两者断言全局 `ResourcesWithoutSource/Sink` 为空，后两者断言指定 ID 子集不含于缺源/缺汇集合；建议：**合并**为单一全局源汇闭合测试；理由：同一职责在 4 处维护，清单会漂移。

- [置信度 Med] `FlowEfficiencyTests.cs` ↔ `FoodEfficiencyTests.cs` — 维度 **D8**；证据：`FlowEfficiencyTests.EffectiveEfficiency_MultipliesPowerAndLogisticsSatisfaction`(`:17`) 与 `FoodEfficiencyTests.EffectiveEfficiency_MultipliesFoodAndResourceSatisfaction`(`:34`) 都测 `BuildingManager.CalculateEffectiveEfficiency` 多因子相乘；`FlowEfficiencyTests.HappinessCurveIsMonotonicAndBounded`(`:39`)、`HappinessOwnsFoodShortagePenalty`、`HappinessPenalizesNegativeFoodNetRate...` 与 `FoodEfficiencyTests.FoodAvailability_UsesInventoryAndPotentialFlow`(`:20`) 同属"食物/幸福度满意度"域；建议：**合并**或至少统一到单一文件；理由：`CalculateEffectiveEfficiency` 与 Happiness/FoodAvailability 被两个文件交叉重复。

- [置信度 Med] `StoryIllustrationTests.cs:13` ↔ `StoryManagerTests.cs:9` — 维度 **D8**；证据：两处均 `Chapters.Count, Is.EqualTo(18)`（协议常量）；建议：**合并**该常量断言到一处（StoryManagerTests），StoryIllustrationTests 只保留插图绑定断言；理由：18 章协议在两个文件各写一份。

- [置信度 Med] 遗迹修复委托幂等：`RelicStateTests.cs:41`(`RepairCommission_GrantsOneSupportAndCannotRepeatCompletion`) ↔ `RelicManagerTests.cs`(`RepairCommission_OnlyOnePreparedSupportAndNoDoubleCompletion`) ↔ `RelicCampaignTests.cs`(`Workshop_RepairRouteRejectsWithoutPayment`) — 维度 **D8**；证据：三者都验证"修复委托只能完成一次/不能重复"；建议：**保留观察**（分层测试 State/Manager/Campaign 各有价值，但幂等语义重复）；理由：不同抽象层的同义覆盖。

### 3.3 建议保留观察

- [置信度 Med] `KingdomLogicTests.cs`(3771 行 / 123 测试)、`SectorManagerTests.cs`(2899 行 / 95 测试)、`SpaceProgressionTests.cs`(2169 行 / 53 测试)、`C6IndustrialContentTests.cs`(1609 行 / 63 测试) — 维度 **D9（可维护性）**；证据：单文件体量与测试数远超其他文件；建议：**保留观察**；理由：非"脏"，但巨型测试文件降低可定位性，后续可拆。

- [置信度 Low] `Assets/Tests/Editor/AuditRuntimeRegressionTests.cs`、`EraInventoryShortfallTests.cs`、`InvestmentChoiceTests.cs`、`StrategicPreviewRegressionTests.cs` — 维度 **D3（历史快照）**；证据：`git status` 显示 4 文件为新增未提交（`A`），mtime `Oct 10 13:11-13:26`，命名含 "Audit/Regression"；建议：**保留观察**；理由：均为当前新增的回归测试，尚未进入历史，不构成陈旧。

### 3.4 未发现

- 未发现 `[Ignore]`/`[Explicit]`/`#if` 关闭的测试、注释掉的测试、长期挂起的 TODO/FIXME。
- 未发现 `Activator`、反射式 `Invoke`、`MakeGenericType` 等动态反射调用。
- 未发现测试向 `outputs/` 或正式技能发现目录写产物（见 §4）。
- 未发现测试引用当前源码中已不存在的**类型/字段/方法**导致编译失效（62 文件当前可编译；内容层陈旧由 §2 的弱负向反射断言掩盖，见 §5）。

---

## 4. 文件副作用（写仓库/临时文件）

- [置信度 Med] `Assets/Tests/Editor/KingdomLogicTests.cs:60-62` — 维度 **D3/副作用**；证据：`projectRoot = Application.dataPath/..`；`root = Path.Combine(projectRoot, "Temp", prefix+"-"+Guid)`；`Directory.CreateDirectory(root)`；多处在 `finally` 中 `Directory.Delete(root, true)`（`:331,:370,:406,:449,:488,:567,:628,:664,:2728`）；并 `File.WriteAllText(Path.Combine(root,"KingdomSave.json"), ...)`（`:355,:389,:427,:515,:616,:650,:2678`）。写入位置在**仓库根 `Temp/`**（Unity 临时目录，未被 git 跟踪：`git ls-files Temp` 为空）。建议：**更新**为写入 `Path.GetTempPath()`（同 `AuditRuntimeRegressionTests`），或确认 `Temp/` 始终在 `.gitignore`；理由：向仓库根目录写文件属副作用，`Temp/` 目前未被 `.gitignore` 显式覆盖（仅 `*.log`、`/[Aa]ssets/Addressables_Temp*`），异常中断可能残留。

- [置信度 Med] `Assets/Tests/Editor/SectorBuildingTests.cs:170-176,247` — 维度 **D3/副作用**；证据：同样 `Path.Combine(projectRoot, "Temp", "SectorBuildingInvalidSave-"+Guid)` → `Directory.CreateDirectory` → `SaveManager.SetSaveRootOverrideForTests`，`finally` 中 `Directory.Delete(saveRoot, true)`；建议：**更新**同上（改用系统临时目录）；理由：同款仓库根 `Temp/` 写入。

- [置信度 Low] `Assets/Tests/Editor/AuditRuntimeRegressionTests.cs:21-22,45,199-220` — 维度 **副作用**；证据：`Path.Combine(Path.GetTempPath(), "KingdomAudit-"+Guid)`，`TearDown` 中 `Directory.Delete(saveRoot, true)`；建议：**保留**；理由：写入系统临时目录（仓库外），已正确清理。

- [置信度 Low] `P40UiConfigurationTests.cs:13,26,37-39`、`MobileOrientationConfigurationTests.cs:10,30,49`、`SectorBuildingTests.cs:170` — 证据：这些仅 `File.ReadAllText`/`Path.Combine` **读取** `ProjectSettings/ProjectSettings.asset`、UI 源码与字体路径，无写操作；建议：**保留**；理由：只读校验配置，无副作用。

---

## 5. fixture 与真实定义表的脱节

- [置信度 Med] 反射测试中的**字符串字段名**与定义类当前字段的耦合是最主要的脱节风险面。证据：`GlobalEconomyDefinitionTests.cs:254,256`、`ResearchBalanceTests.cs:174-179`、`FlowEfficiencyTests.cs:32-35,119-121`、`C6IndustrialClosureAuditTests.cs:85-88` 均以硬编码字符串（`"BuildingUnlock"`/`"UnlockedBuildings"`/`"x"`/`"SystemID"`/`"PowerProduction"`/`"PowerProductionRate"` 等）查询定义类型字段。当前 `Assets/Resources/Script/Data/` 定义类为 `Building.cs`/`Research.cs`/`ResearchEffect.cs`/`Resource.cs`/`SectorDefinition.cs`/`SectorBuilding.cs`/`WorkshopUpgrade.cs`/`UltraProjectDefinition.cs`/`RelicDefinition.cs`/`StoryArchiveDefinition.cs` 等；建议：**更新**（随 §2 一并处理）；理由：负向字符串断言在字段改名后静默通过，形成"看似在防脱节、实则无保护"的假象。

- [置信度 Low] `C6IndustrialContentTests.cs:12-13,38-40,72-73,100-101,1584-1591`、`GlobalEconomyDefinitionTests.cs:33-37`、`KingdomLogicTests.cs:204,956-962,3041,3596-3682`、`BuildingCostGrowthTests.cs:81,280` 等 — 证据：测试用 `ScriptableObject.CreateInstance<T>()` 手工构造定义对象并赋字段，属"测试自造 fixture"。由于均走强类型字段（可编译），未发现与定义类当前字段**不匹配**（不存在编译失效字段）；建议：**保留观察**；理由：强类型赋值保证字段存在，但手工 fixture 不会自动跟随定义表新增字段，属潜在漂移，非当前缺陷。

---

## 6. 不确定项（需用户决策）

1. `KingdomLogicTests.cs` 与 `SectorBuildingTests.cs` 向仓库根 `Temp/` 写入的存档 fixture——`Temp/` 是否为 Unity 运行时保证清空？是否需要显式加入 `.gitignore`？需用户确认清理策略（§4）。
2. `C5`↔`C6` 内容闭合审计是否可合并（§3.2）——需内容负责人确认 C5 的九项研究/建筑清单是否仍需独立回归保护。
3. `EarlyVerticalSlicePacingTests` 把平衡数值（336000/181440/133920）写死——需确认这些是否为"设计协议常量"（若为协议则保留精确）还是可调平衡值（则应改范围）。

## 7. 未覆盖项

- `Assets/Tests/PlayMode/`（其他子代理）。
- 测试所依赖的 `Assets/Resources/Script/**` 生产源码本体（仅按需 grep 交叉核对字段/类型存在性）。
- 第三方框架（NUnit、Unity Test Framework）代码，非项目代码。
- 62 个 `.meta` 文件内容（仅确认成对存在）。
