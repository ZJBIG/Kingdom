# 14 · Data 与 Validation 层 C# 源码审计

审查人：general-purpose-20 ｜ 只读盘点，未修改任何文件
基准：当前磁盘文件（工作树含未提交改动）

---

## 1. 范围

实际完整阅读并逐字段核查的文件：

- `Assets/Resources/Script/Data/` 下 **16 个 .cs**：
  Building、DataBase、GameDefinition、MusicCatalog、RelicDefinition、Research、ResearchEffect、Resource、SaveFormat、SectorBuilding、SectorDefinition、SectorState、StoryArchiveDefinition、StoryChapterDefinition、UltraProjectDefinition、WorkshopUpgrade
- `Assets/Resources/Script/Validation/` 下 **5 个 .cs**：
  BuildingTransactionRules、EconomyDependencyValidator、ProgressionMilestoneRecorder、ResearchValidator、SectorValidator

消费侧核实范围：`Assets/Resources/Script/{Manager,UI,Runtime,Math,Misc}/`、`Assets/Editor/`、`Assets/Tests/`（70 个测试文件）、`tools/NewEconomySimulator/`、`tools/codex/`。

定义资产核实：`Assets/Resources/Datas/` 共 **380 个 .asset**（Building 72 / Research 143 / Resource 40 / Sector 10 / Story 19 / Tutorial 8 / Ultra 2 / Workshop 86），抽样并全量比对顶层字段名。

方法：对每个 public 字段/属性用 `grep -rn "\.<属性名>\b"` 统计 Data 目录之外的命中文件数；对 Data 类字段与 .asset 顶层字段名做并集比对。

---

## 2. 结论清单

### A. 建议删除 —— 完全无人消费的定义字段（核心产出）

- [置信度 High] `Assets/Resources/Script/Data/SectorDefinition.cs` — 维度 D9/D10；证据：`SectorDefinition.cs:83 public Sprite Background => background;`（序列化字段 `background` 在 `:50`）。全仓搜索 `\.Background\b` / 字段 `background`：Data 目录外 0 命中；`Assets/Resources/Datas/Sector/` 全部 10 个资产均为 `background: {fileID: 0}`（空引用）。建议：删除 `background` 字段与 `Background` 属性；理由：定义即空、且无任何读取点，纯冗余序列化槽位。
- [置信度 High] `Assets/Resources/Script/Data/SectorDefinition.cs` — 维度 D9/D10；证据：`SectorDefinition.cs:84 public Sprite Icon => icon;`（字段 `:51`）。全仓 `\.Icon\b` 命中 0（`background/icon` 仅出现在各 `.cs.meta` 的 `icon: {instanceID: 0}`，非引用）；10 个 Sector 资产均为 `icon: {fileID: 0}`。建议：删除 `icon` 字段与 `Icon` 属性；理由：同上。
- [置信度 High] `Assets/Resources/Script/Data/SectorDefinition.cs` — 维度 D9/D10；证据：`SectorDefinition.cs:86 public float MapY => mapY;`（字段 `:53`）。全仓 `MapY` / `mapY` 仅命中定义本身，无任何读取点；10 个 Sector 资产都填了 `mapY:` 值（如 `AzurePool.asset:49 mapY: 1`）。建议：删除 `mapY` 字段与 `MapY` 属性（连带评估 `mapX`）；理由：作者填了坐标但代码从不消费，属陈旧地图遗留。
- [置信度 High] `Assets/Resources/Script/Data/SectorDefinition.cs` — 维度 D9/D10；证据：`SectorDefinition.cs:85 public float MapX => mapX;`（字段 `:52`）。全仓唯一命中为测试 `Assets/Tests/Editor/SectorDefinitionTests.cs:63 Assert.That(mars.MapX, Is.GreaterThan(moon.MapX));`，运行时/UI 无任何读取；10 个 Sector 资产均填 `mapX:` 值。建议：删除（或若保留地图布局，需补 UI 消费方）；理由：仅剩一个自证式断言，无实际玩法/渲染用途。
- [置信度 High] `Assets/Resources/Script/Data/UltraProjectDefinition.cs` — 维度 D9；证据：`UltraProjectDefinition.cs:44 public string RequiredResearchId => ...`、`:45 public string RequiredBuildingId => ...`。全仓搜索 `RequiredResearchId\b` / `RequiredBuildingId\b`（`\b` 排除复数 `Ids`）除定义行外 0 命中。同文件 `:42 RequiredResearch`、`:43 RequiredBuilding`（强引用）才是被 `UltraProjectManager.cs:472-474` 消费的入口。建议：删除这两个字符串 getter；理由：R2 迁移遗留的 ID 便捷属性，无调用点。

### B. 建议更新 —— 定义/数据在、但仅测试或仅 Editor 消费（疑似陈旧）

- [置信度 Med] `Assets/Resources/Script/Data/UltraProjectDefinition.cs` — 维度 D9/D10；证据：`UltraProjectDefinition.cs:41 public string PrerequisiteStageId => prerequisiteStageId;`（字段 `:23`）。全仓唯一命中为测试 `Assets/Tests/Editor/UltraContentSliceTests.cs:78`；运行时 `UltraProjectManager.cs:479-499 GetCurrentStageDefinition()` 用硬编码 id（`"PhaseStabilityCertification"` 等）按 `StageId` 定位阶段，**从不读取 `PrerequisiteStageId`**。资产确实填写了该字段（`UltraCivilizationEngineering.asset:21/45/71`）。建议：更新——要么在 `UltraProjectManager` 用 `PrerequisiteStageId` 做阶段前置校验，要么删除字段与资产值；理由：数据已写、逻辑未接，属半成品/陈旧。
- [置信度 Med] `Assets/Resources/Script/Data/WorkshopUpgrade.cs` — 维度 D9；证据：`WorkshopUpgrade.cs:11 public int SortOrder;`。Data 目录外唯一命中为 `Assets/Editor/EconomyParitySnapshotExporter.cs:132`（仅把值写进 parity DTO）；UI 工坊列表（`KingdomUIRoot.DetailPanel.cs` 等）排序未使用 `SortOrder`。86 个 Workshop 资产均填 `SortOrder`（如 `AdaptiveArmorRepairSystems.asset:18 SortOrder: 690`）。建议：更新——要么让 UI 按 `SortOrder` 排序，要么删除；理由：排序字段无实际排序消费方。
- [置信度 Med] `Assets/Resources/Script/Validation/BuildingTransactionRules.cs` — 维度 D9；证据：`BuildingTransactionRules.cs:6 TryNormalizePositiveWhole`、`:36 Total` 在 Data 目录外仅命中测试 `Assets/Tests/Editor/KingdomLogicTests.cs:132/137/146`；运行时 `BuildingManager` 只调用 `ClampToAvailable`（`BuildingManager.cs:804/874/1009/1067`）。建议：更新/删除——若为输入校验预留则补 UI 调用，否则移除这两个仅测试方法；理由：运行时代码路径未使用。
- [置信度 Med] `Assets/Resources/Script/Validation/BuildingTransactionRules.cs` — 维度 D9/D7；证据：`BuildingTransactionRules.cs:3 #if !ECONOMY_SIMULATOR` 包裹整个 `BuildingTransactionRules` 类。全仓（含 `ProjectSettings/`、`*.csproj`、`*.sln`、`*.asmdef`）搜索 `ECONOMY_SIMULATOR` **仅在定义处出现，无任何位置定义该符号**。建议：删除该条件编译指令；理由：符号从未定义，`#if` 恒为真，属永久开启的死分支（同时它是唯一残留的模拟器隔离宏）。

### C. 建议合并 —— 校验职责重叠（重复实现）

- [置信度 High] `Assets/Editor/Content/ContentDependencyAnalyzer.cs` 与 `Assets/Resources/Script/Validation/EconomyDependencyValidator.cs` — 维度 D8；证据：两者都对同一张图做"可达闭包 + 研究循环 + 资源死锁"分析：`ContentDependencyAnalyzer.cs:88-137`（定点迭代求可达研究/建筑、`:218-231` 研究循环、`:233-255` 资源死锁）对应 `EconomyDependencyValidator.cs:165-285`（`ValidateReleasedReachability`）、`ResearchValidator.cs`、`:479-536`（`ValidateProductionGraph`）、`:655-727`（`ValidateDirectUnlockDeadlocks`）。权威应为 **`EconomyDependencyValidator`**：它被运行时启动路径 `GameBootstrap.cs:58` 与命令行 `Assets/Editor/Content/RuntimeClosureValidatorCommand.cs:17` 调用，且额外覆盖 Workshop 与时代上限。建议：合并——保留 `EconomyDependencyValidator` 为唯一权威，`ContentDependencyAnalyzer` 降级为调用它的报告外壳或归档；理由：同一规则两套实现，长期必然漂移。
- [置信度 Med] `tools/codex/content-closure-check.ps1`、`tools/codex/check-building-sustainability.py` 与 `EconomyDependencyValidator` — 维度 D8；证据：`content-closure-check.ps1` 头部注释即声明做 research/workshop/building 可达闭包；`check-building-sustainability.py` 文档自述为 `content-closure-check.ps1` 的"另一半"，校验产出/维护资源的生产者存在性——与 `EconomyDependencyValidator.ValidateProductionGraph`（`EconomyDependencyValidator.cs:479`）职责重叠。权威为 C# 侧（可被 `GameBootstrap` 启动即校验）。建议：合并/明确边界——静态 YAML 脚本仅保留 C# 未覆盖的"时代先后"必要条件，其余复用权威实现；理由：C# 与 PS/Python 双份闭包逻辑。
- [置信度 Med] `tools/codex/building-resource-flow-check.ps1` 与 `Building.ValidateResourceFlowDefinitions` — 维度 D8；证据：`Building.cs:149-174 ValidateResourceFlowDefinitions()`（同一建筑同时生产与消耗同一资源即抛异常）已在运行时被 `BuildingManager.cs:255` 调用；`building-resource-flow-check.ps1` 在 YAML 上做同样的"generated ∩ consumed"检测。建议：合并——以运行时 `ValidateResourceFlowDefinitions` 为权威；理由：同一禁令两处维护。

### D. 保留观察

- [置信度 High] `Assets/Resources/Script/Manager/GameBootstrap.cs` — 维度 观察；证据：`GameBootstrap.cs:52-66` 对 `SectorValidator.ValidateDefinitions` 与 `EconomyDependencyValidator.Validate` 的失败只做 `Debug.LogWarning`，不阻断启动。建议：保留观察（非陈旧问题，属校验强度设计）；理由：Data/Validation 本身无脏，仅记录校验为"软门槛"。
- [置信度 Low] `Assets/Resources/Datas/Ultra/EchoFoundryRing.asset` — 维度 D10；证据：该文件脚本 guid `ec4edfc2108b4a8daa7193038f22c010` = `RelicDefinition.cs.meta`，即它是 **RelicDefinition** 资产，却与 `UltraCivilizationEngineering.asset`（UltraProjectDefinition）同放 `Datas/Ultra/`。`DataBase<T>` 递归加载故功能无碍（`RelicManager.cs:21` 按 id 命中）。建议：保留观察/整理目录；理由：仅命名归属不一致，无功能影响。
- [置信度 High] `Assets/Resources/Script/Validation/BuildingTransactionRules.cs:43-107 EconomySimulationParity` — 维度 D2；证据：类 XML 注释自称 "shared with the standalone economy simulator"，但 `tools/NewEconomySimulator/NewEconomySimulator.csproj:8-11` 仅编译 `ExpantaNum.cs`/`ExpantaNumExtensions.cs`，且模拟器在 `SimulationCore.cs:955/965/979` **自带私有** `CalculateFlowSatisfaction`/`CalculateSatisfaction`/`ResearchSpeedEffect` 实现；`EconomySimulationParity` 在 Data 目录外仅被 `Assets/Tests/Editor/SimulationDeterminismTests.cs:86-123` 引用。建议：保留观察/更新注释——它是"仅测试使用的运行时 parity 辅助类"，与模拟器无共享关系；理由：注释与事实不符，易误导。

### E. 未发现问题的区域（不凑数）

- **workforce 残留**：`Assets` 全目录 `grep -i workforce --include=*.cs` 0 命中。Data/Validation 层无残留。
- **普通资源容量/MaxAmount/仓储字段**：Data 目录内 `capacity` 仅命中 `foodCapacityGranted`（Food）与 `populationCapacityGranted`（人口），无任何非 Food 资源容量/仓储字段；380 个资产中亦无 `capacity/storage/stockpile/warehouse` 类字段。`SectorBuilding.cs:10 maxAmount` 是**单星区建筑建造数量上限**（运行时消费点 `BuildingManager.cs:678/937/1942`、UI `KingdomUIRoot.Sectors.cs:169/313`），非资源存储上限，不违反硬约束。
- **反射**：Data/Validation 层 `System.Reflection`/`BindingFlags`/`Activator`/`GetMethod`/`GetField`/`GetProperty`/`Invoke(` 0 命中。
- **TODO/FIXME/HACK/XXX/OBSOLETE**：Data/Validation 层 0 命中。
- **.asset 字段 vs 类字段匹配**：对 Building / Research / Resource / SectorDefinition / SectorBuilding / UltraProject(含 stage) / WorkshopUpgrade / RelicDefinition(含 RelicWorkDefinition) / StoryChapter / StoryArchive / MusicCatalog 抽取全部顶层字段名与类字段逐一比对，**未发现"资产有而类无"的旧字段**；Building 资产字段与 `Building.cs` 完全一致（抽样 `CeramicKiln.asset`）。
- **Building 全部 public 字段/属性**（Label/Description/TechLevel/SpaceCost/ProductivityConsumption/ProductivityGranted/PopulationCapacityGranted/ResearchPowerGranted/Food*/Power*/Logistics*/FleetPowerGranted/AttackPowerGranted/DefensePowerGranted/MilitaryManpowerGranted/CostGrowth/HasValidCostGrowth/UpgradeTo/ResourceRequirements/ResourceGenerationRates/ResourceConsumptionRates/RequiredResearch/RequiredWorkshopUpgrades）：Data 目录外命中文件数 2–46，均有真实消费点（军事四字段由 `BuildingManager.cs:1684-1690` 消费，非陈旧）。
- **Research / Resource / SectorDefinition（其余字段）/ SectorState / StoryChapter / UltraProject / WorkshopUpgrade / Relic / MusicCatalog 的其余字段**：均有 Data 目录外消费点。
- **Validation 层调用链**：`BuildingTransactionRules`（BuildingManager）、`ResearchValidator`（ResearchManager.cs:109）、`EconomyDependencyValidator`（GameBootstrap.cs:58、RuntimeClosureValidatorCommand.cs:17、多个测试）、`SectorValidator`（GameBootstrap.cs:52、SectorDefinitionTests）、`ProgressionMilestoneRecorder`（GameBootstrap.cs:39、TutorialManager.cs:534）——均仍在调用，无孤儿校验类。
- **枚举完整性**：`ResearchEffectType` 全部 30 个值在 `ProgressionModifierManager.cs:263-358` 有处理分支；`WorkshopEffectType` 全部 20 个值在 `WorkshopUpgrade.cs:92-163 ApplyTo` 有分支（default 抛异常但无遗漏值）。

---

## 3. 不确定项（需用户决策）

1. `SectorDefinition.MapX/MapY`：字段有作者填写的坐标值（`AzurePool.asset:48-49` 等 10 处），但无任何渲染/布局消费方。**若未来计划做星区地图 UI，应保留并补消费方；否则删除**。仅凭代码无法判断是有意预留还是废弃。需产品决策。
2. `UltraProjectStageDefinition.PrerequisiteStageId`：字段在资产中成链填写（PhaseStability→MatterAutonomy→CivilizationContinuity），但运行时按硬编码阶段顺序推进。是"冗余校验数据"还是"尚未接入的运行时前置"，需玩法侧确认。
3. `WorkshopUpgrade.SortOrder`：86 个工坊资产都填了序号，但 UI 未按它排序。需确认是否打算用它做工坊展示顺序。
4. `ContentDependencyAnalyzer` / `RuntimeClosureValidatorCommand` / codex 脚本三者与 `EconomyDependencyValidator` 的合并方向：C# 权威已定，但 PS/Python 静态脚本是否保留为"不启动 Unity 的快速检查"需团队取舍（本报告只指出重叠，不主张重建）。

---

## 4. 未覆盖项

- **Tutorial 定义类**：`Assets/Resources/Datas/Tutorial/` 下 8 个资产使用的字段（`Id/Kind/NarrativeText/NavigationPage/NextStepId/RewardId/TriggerCondition/CompletionCondition`）来自 `Assets/Resources/Script/Manager/TutorialStepDefinition.cs`，**不在本区块（Data/）范围内**，未逐字段核查（属 Manager 层子代理职责）。已确认这些字段名不与 Data 层类冲突。
- `Assets/Resources/Script/Misc/Tool.cs` 中的 `ResourceAmountDefinition` / `ResourceAmountDefinitionList` / `Pair<>`（被 Data 类大量引用）不在本区块，未展开。
- `.asset` 内容级语义校验（数值平衡、占位值）归 `Assets/Resources/Datas` 定义资产子代理（general-purpose-18）。
- `Library/`、第三方插件、二进制未触及（按准则排除）。
