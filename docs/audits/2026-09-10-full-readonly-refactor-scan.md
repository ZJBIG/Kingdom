# Kingdom 全仓库只读重构扫描报告（2026-09-10）

> 历史快照：统计、行号和候选问题未按当前源码重新验收，不作为当前待办或实现事实；后续实施须先复现。

- 性质：**只读审计**。本报告未修改任何代码、资产或场景文件。
- 范围：`Assets/Resources/Script`（75 文件 / 31,107 行）、`Assets/Tests`（46 文件 / 约 20k 行）、`Assets/Editor`（13 文件）、`tools/NewEconomySimulator`、`Assets/Resources/Datas` 全部内容资产、`docs/` 契约文档。
- 方法：6 个领域并行扫描（Manager / UI / Runtime+Data+Validation / 数学+模拟器 / 测试+Editor 工具 / 内容资产+文档），全部发现经 grep 调用点交叉验证；主代理对全部 P1 结论做了独立复核（见附录 B 复核记录，其中 1 项子代理结论被推翻）。
- 与旧审计的关系：根目录 `code-audit-refactor-candidates.csv`（2026-09-07）的旧条目已逐项复核，行号全部更新为当前工作树，若干旧项已失效（见附录 A）。
- 环境说明：未执行真实 Unity 编译。

## 总览

按优先级统计：P1（正确性/结构性债务，应优先处理）11 项；P2（明确的重复/性能/契约漂移，计划内处理）约 40 项；P3（卫生/一致性/防御性）约 45 项。死代码总量（可删除或移出运行时程序集）粗估超过 **2,300 行**。

全局横向主题（跨领域反复出现的同类问题）：

1. **双实现漂移**是当前仓库最大的结构性风险，至少 5 处：Unity Manager 公式 ↔ NewEconomySimulator `SnapshotDrivenRules`（已发现人口离场 `departureAllowance` 与食物上限 `UncappedFoodCeiling` 两处公式级失同步，且无任何测试锁定两侧公式形状）；`SimulationState.GeometricCost` ↔ `ExpantaNumExtensions.GeometricSeriesCost`（后者有 `PowMinusOne` 数值稳定路径，前者没有）；`EconomyParitySnapshotExporter` DTO ↔ `SnapshotModels` DTO（已出现 `CostGrowth` 默认值 "1" vs "1.15" 漂移）；快照验证规则双份；`BuildingTransactionRules.ResearchSpeedEffect` ↔ `ResearchManager.ResearchSpeedEffect` 公式写法不一致。
2. **固定 UI 运行时创建**与仓库"非必需 UI 组件必须在 Prefab 创作"合同形成系统性割裂：DetailUI v2 外壳（整页运行时构建且无 docs 决策记录）、Sectors 建造菜单、Story/Era 手搓按钮、MusicTrack 缺失的 Category 列。同一 UI 层同时存在 prefab 库与裸 GameObject 两套行创建体系。
3. **乱码（双重编码事故）**：ResearchManager.cs:938/967/1125 异常消息为 GBK↔UTF-8 双重编码产物（码点级确认含 U+20AC € 与私有区字符）；`KingdomUIRoot.DetailPanel.cs:1045` 的 `heading.Contains("鐮旂┒")` 已把乱码变成**运行时匹配逻辑**；SectorValidator.cs:278 有一条永远被覆盖的乱码赋值行。TutorialManager/Era/LiveRefresh 存在 `\u` 转义与直接中文混写（非乱码，但同源事故痕迹）。
4. **每 tick / 每刷新的热路径浪费**：StoryManager 每 tick 3 处 `FindObjectOfType`；GameManager 属性 getter 内 `FindObjectOfType`；DetailPanel 每前置行一次 `FindObjectOfType`；Sectors 页每秒 ForceUpdateCanvases 三连；Max 模式下 `GetMaxBuildable` 每 250ms 每建筑双算；模拟器 `EnsureFrame` 每 tick 重新 Parse 全部定义字符串。
5. **验证覆盖缺口**：`EconomyDependencyValidator` 硬编码 `TechLevel.Industrial` 天花板，Spacer/Ultra/Archotech 内容可达性启动时不验证。

规则符合性正面结论（不可协商规则全部未被违反）：食物唯一上限 ✓、禁止 workforce ✓、批量购买闭式函数 ✓、Manager 层无反射 ✓、事件订阅无泄漏 ✓、字典遍历中修改未发现 ✓、CanvasScaler 硬合同（2640x1200 / ScaleWithScreenSize / Match Width，Prefab 与运行时双重保险）✓。

---

## 一、P1 清单（正确性 / 结构性债务）

### A. 正确性缺陷

**P1-1 乱码字符串已扩散为运行时逻辑**
- `Manager/ResearchManager.cs:938、967、1125` — 三处异常消息为双重编码乱码（"存档中的研究状态索引缺少"被写成 `瀛樻。涓殑鐮旂┒鐘舵€佺储寮曠己灏戯細`，含 € 符号）。码点级复核确认。
- `UI/KingdomUIRoot.DetailPanel.cs:1045` — `heading.Contains("研究") || heading.Contains("鐮旂┒")`：UI 用乱码字符串做匹配兜底，说明乱码值曾被真实读出过。
- `Validation/SectorValidator.cs:278` — 永远被 279-280 行覆盖的乱码赋值行（紧随其后用 `\u661f\u533a...` 转义重写为正确中文）。
- 建议：统一修复源文件编码为 UTF-8，恢复中文消息；删除 UI 的乱码兼容分支；顺手统一 TutorialManager.cs:787-812、859-862 的 `\u` 转义混写。

**P1-2 EconomyDependencyValidator 硬编码 Industrial 验证天花板**
- `Validation/EconomyDependencyValidator.cs:156、175、189、213、226、235` — 可达性验证只覆盖到工业时代；游戏 TechLevel 枚举到 Archotech，且 Spacer 已有 47 个研究、16 个建筑的内容。星际内容的前置错误不会被启动验证捕获。
- 另：`cs:145-147` 与 `cs:731` 硬编码 `"WoodLog"` 种子资源，绕过 `Validate` 的 `resources` 参数。
- 建议：上限参数化（默认取 `TechLevel` 最大值），种子资源引用 `ResourceManager.StartingResourceId` 常量。

**P1-3 PopulationState epsilon 补偿与 ExpantaNum 舍入粒度冲突**
- `Runtime/PopulationState.cs:210-211、272-274` — `(accumulated + PopulationStepEpsilon).Floor()` 中 `PopulationStepEpsilon = 1e-9` 低于 ExpantaNum 的舍入粒度（DecimalPlaces=6 / SmallestRoundedMagnitude=5e-7，ExpantaNum.cs:68-69），要么无效、要么在进度贴近整数边界时与自身舍入叠加产生 ±1 人偏差；`PopulationRemainderEpsilon = 1e-3`（cs:219-223）是 double 算术时代遗产。需要为 ExpantaNum 进度明确唯一容差来源，或整数化进度。
- 附带发现 `ExpantaNum.cs:2941` `Quantize` 的 `magnitude >= 9000000000d` 阈值疑似 `9007199254740991d`（2^53-1）的截断笔误——若为笔误，9e9~9e15 区间跳过量化存在确定性风险。需作者确认意图。

**P1-4 5 处同构的线性稳定 ID 查找 + WorkshopManager 注册路径不一致**
- `Manager/ResourceManager.cs:86-105`、`Manager/BuildingManager.cs:349-368`、`Manager/ResearchManager.cs:128-147`、`Manager/SectorManager.cs:229-250`、`Manager/WorkshopManager.cs:228-251` — 五份完全同构的 `TryGetStateByStableId` 线性扫描（约 120 行重复）。WorkshopManager 还因此出现两条不一致注册路径（Initialize 直接 `states.Add` 假设无重复，EnsureStateIndex 却做去重）。
- 建议：提取共享 `StableIdIndex<TDef, TState>`（O(1) 字典），五处替换。

**P1-5 SectorManager 双实实现与硬编码内容表**
- `Manager/SectorManager.cs:867-1062` — `TryRepairFleet`（全局版）与 `TryRepairFleetForState` 约 100 行高度重复，仅"状态定位"不同；全局版运行时唯一调用方是测试。
- `Manager/SectorManager.cs:521-530` — `TryOccupy` 两个互补 if 分支返回完全相同（复核确认逐字符相同），等价于单个判断，疑似重构残留。
- `Manager/SectorManager.cs:1767-1790` — `CalculateFleetRepairCosts` 在 Manager 层硬编码 4 个资源 ID 与系数（TitaniumAlloy×2、Composite×1、PhantomWeave×1、RocketFuel×0.5），违反"内容优先在资产定义"的方向。
- 附：cs:804 与 813-816 `CalculateColonizationBillableSeconds` 重复计算两次。

**P1-6 测试层反射依赖（356 处 API 命中 + ~200 处 `.Invoke(`）**
- 17 个测试文件构建在非公共反射之上（与"新测试禁止反射"规则冲突）。重灾区：KingdomLogicTests（4 个通用反射助手 :2498-2547）、SectorManagerTests（7 个助手 :2559-2660）、KingdomOnboardingPlayModeTests（`SetPage` 反射内联 50 处）、C6IndustrialContentTests（反射调用静态私有 `ValidateWorkshopPrerequisites` 4 处）。
- 低垂果实：`TutorialManagerTests.cs:165` 与 `SectorManagerTests.cs:1871-1873` 仍在反射调用**已经公开**的 `CaptureSaveData`。
- 建议：为高频私有入口（SetPage、RestoreSaveData、ResearchState.Restore、SetStatus、InitializeNewGame、ResourceManager tick 链、ValidateWorkshopPrerequisites）开公开测试 API——仓库已有先例（`CaptureSaveData()`、`SetOccupiedForEditor`、`SetCampaignProgressForEditor`、`ConfigureForEditor`）；渐进替换、禁止新增。

### B. 结构性 UI 债务

**P1-7 DetailUI v2 外壳整套运行时构建**
- `UI/KingdomUIRoot.DetailUI.cs:14-251` — Header/Viewport/Footer/Action/RequirementRow/FlowRow 全套固定结构以 `new GameObject`/`AddComponent` 手工搭建（复核确认 6 处 new GameObject、10 处 CreateRect），含硬编码颜色、尺寸（82f/72f/48f）与中文标签。代码注释自述为 "replacement detail surface"，但**没有任何 docs/decisions 记录**——不满足 AGENTS.md 豁免条件（既非数据驱动重复内容，也非"明确记录的兼容回退"）。
- 连带：`DetailPanel.cs:1170-1290` `PlaceRequirementsAfterDescription` 每次结构重建执行 **4 次 `Canvas.ForceUpdateCanvases()`**（:1203、:1253、:1262、:1289），是详情面板卡顿的主要嫌疑。
- 建议：外壳迁入 KingdomUIRoot.prefab 或独立 DetailPanel.prefab，运行时只绑数据；补 docs/decisions 记录。

**P1-8 Sectors/Story/Era/Music 四处裸 GameObject 行创建体系**
- `UI/KingdomUIRoot.Sectors.cs:139-199` — 星区建造菜单运行时 `AddComponent<UIPageScrollDragForwarder>` ×5 + `AddComponent<Button>`（复核确认），硬编码布局（416f/-40x100@-76-i*112）。
- `UI/KingdomUIRoot.Story.cs:365-397、489-522`、`UI/KingdomUIRoot.Era.cs:103-190` — 两套手搓 240x60/250x58 按钮实现（每章/每时代一个固定模板按钮）。
- `UI/KingdomUIRoot.Music.cs:134-159` — `KingdomUIMusicTrack.prefab` 缺 Category 子物体（复核确认 prefab 内 0 命中，PlayPause 存在），导致每个曲目行动态 `new GameObject` 创建分类图标列；同方法邻域对其他缺失子物体一律 LogError 拒绝，唯独 Category 用运行时创建兜底——回退已成常态路径。
- 建议：新增 SectorBuildingCard / StoryChapterCard / EraCard prefab，Category 列补入 MusicTrack prefab；运行时只绑数据与回调。

### C. 模拟器 parity 债务

**P1-9 Unity↔模拟器公式失同步（无防线）**
- `tools/NewEconomySimulator/SimulationCore.cs:303-1029` `SnapshotDrivenRules` 复制了 BuildingManager/ResourceManager/GameState/SectorManager/ResearchManager 的推进公式。当前常量一致，但：
  - 人口离场：Unity 侧 `AdvancePopulation` 有 `departureAllowance` 参数（SimulationManager.cs:305-308 传入 `SafePopulationDepartureAllowance`），模拟器侧（SimulationState.cs:422-450）无此参数——**已失同步**。
  - 食物上限：Unity 侧有 `UncappedFoodCeiling` 概念（GameManager.cs:177），模拟器 `AdvanceFood`（SimulationState.cs:415-420）无对应——第二个失同步点。
  - `SimulationState.GeometricCost`（:467-477）与 `ExpantaNumExtensions.GeometricSeriesCost`（:37-58）双实现：扩展版有 `PowMinusOne` 数值稳定路径，模拟器版没有，ratio≈1 临界时同一购买可能得到不同可负担数量。
- 建议：建立共享常量模块 + Unity↔模拟器 parity trace diff 自动化测试（现有 ValidationSuite 只测模拟器自身确定性）；模拟器 csproj 链接 ExpantaNumExtensions 复用唯一闭式实现。

**P1-10 SimulationCore.cs（实际 1531 行）应拆分**
- 8 个独立职责挤在一个文件：规则接口、状态工厂、SnapshotDrivenRules（核心 730 行）、tick 编排、定义索引、SimulationModifiers（260 行）、TickFrame、TickContext。其中 `ApplyResearchEffect`（:1326-1425）与 `ApplyWorkshopEffect`（:1427-1500）两段 switch 约 15 个重复 case——新增效果类型必须双改，漏一边即静默经济漂移。
- 建议：拆出 SnapshotDrivenRules.cs / SimulationModifiers.cs / SnapshotDefinitionSet.cs；双 switch 合并。

**P1-11 TutorialManager 2413 行：19 段复制粘贴 + 求值重复**
- `Manager/TutorialManager.cs:996-1269` `BuildIndustrialGuidance` 手写 19 个研究/建筑 ID 的逐个 if 判断，每段 8-14 行同构模板；同文件 `BuildSpacerGuidance`（:1271-1331）已示范数组+循环的更优写法。
- 同一次 `Evaluate` 内：`FindProductionChainRecommendation`（最坏 O(建筑²×资源)）被计算 2 次（:754/1952）、`EraGoalEvaluator.Evaluate` 被调用 2 次（:948/2435）。
- `TutorialManager.cs:2076` `IsProductionChainEndpoint` 全仓库零调用（复核确认）。
- 建议：主线数据表化（预计减 200 行）；Evaluate 内缓存一次传递。

---

## 二、P2 清单（按领域分组）

### Manager 层

| 位置 | 问题 | 建议 |
|---|---|---|
| GameManager.cs:120-167 | Tick/TickOffline 食物人口日历推进逐行重复 | 提取 `AdvanceSimulationCore(simSeconds, calSeconds, allowance)` |
| GameManager.cs:35 | 属性 getter 内 `FindObjectOfType<BuildingManager>`（复核确认），UI 刷新路径触发全场景扫描 | 缓存引用或用 `BuildingManager.Instance` |
| GameManager.cs:172-177 | 4 参 `AdvanceFood` 的 `UncappedFoodCeiling=1e1000000` 伪容量仅测试使用 | 删除或移入测试程序集 |
| 全 Manager 层 | deltaSeconds 三连校验重复 18+ 处（GameManager/Research/Resource/Campaign/Sector/Simulation 各 3-4 处） | 提取共享 `TickTime.ValidateSeconds` |
| BuildingManager.cs（1855 行） | 8 类职责上帝类：成本边界/链验证/ID 注册/建造事务/效率收敛（每 tick 热路径 :1239-1337）/修改器差值/存档/活跃索引 | 拆出 `BuildingEfficiencySolver` 与 `BuildingUpgradeTransactions` |
| BuildingManager.cs:940/1042 vs 1085 | `TryUpgrade`/`GetMaxUpgradeable` 二分循环内重复解析升级目标与全部资源 delta | 目标解析提升到循环外 |
| BuildingManager.cs:498-509/526-528 | "沿 UpgradeTo 走到最高已解锁层" while 循环双写 | 提取 `FindHighestUnlockedTier` |
| ResearchManager.cs:50/65/851/853 | `researchCountByTech` 死字典（复核确认仅自引用） | 删除 |
| ResearchManager.cs:187-188 | `CanAccessResearch` 二次调用后的不可达 `Blocked` 分支（复核确认） | 删除 |
| StoryManager.cs:525/538/720 | 3 处 `FindObjectOfType`（复核确认），`HasWorkshopPurchase` 每 tick 经 AllWorkshopsPurchased→RefreshProgress 触发 | 改 `WorkshopManager.Instance`（一行修复收益大） |
| StoryManager.cs:47-48 vs 69-70 | `First()` 私有静态方法双定义，StoryChapter 版零调用 | 删除 |
| MusicManager.cs:232-283 | 三个 `Play` 重载运行时零调用 | 确认后删除 |
| SimulationManager.cs:160-286 | ManualTick 约 80 行 `#if UNITY_EDITOR` 插桩切碎核心逻辑 | 抽 `EditorTickProfiler` |
| WorkshopManager.cs:222-226 | `TryGetState` 纯转发包装 | 内联 |
| EraGoalEvaluator.cs:92 | `(TechLevel)((int)currentEra + 1)` 无 Archotech 越界保护，`(TechLevel)7` 非法定义值流入评估结果 | 入口显式返回空评估 |

### UI 层

| 位置 | 问题 | 建议 |
|---|---|---|
| LiveRefresh.cs:244-527 | `RefreshUI` 单方法 283 行、9 页硬编码字符串分支；10 个独立 timer 字段（且 eraPageRefreshTimer 声明与递增分离在两个 partial） | 拆分：editor-only 性能统计（~200 行）独立 partial；Observe*/通知系统独立；每页刷新归 presenter |
| KingdomUIRoot.cs:739-754 vs LiveRefresh.cs:973-986 | `PageLabel` 与 `GetPageTitle` 两份中文页名映射互相矛盾（"Sectors"= "星区" vs "区划"） | 合并单一映射 + PageId 常量消除约 20 处页面名字面量 |
| LiveRefresh.cs:1279-1281 | `else SetTextIfChanged(pair.Value, "0")` 语义错误：按钮缺失分支把真实数量覆写为 "0" | 删除 else 或改日志 |
| LiveRefresh.cs:1270-1277、AuthoredRows.cs:185-195 | Max 模式 `GetMaxBuildable` 每 250ms 每建筑双算 | `CanPerformBuildingAction` 接受已算好的 maximum |
| DetailPanel.cs:642-652 | `IsResearchCompleted`/`IsWorkshopPurchased` 每前置行一次 `FindObjectOfType` | 改单例（同文件 :803 已有正确做法） |
| DetailPanel.cs:1045 | 标题参数含乱码匹配且实际无效（参数只可能是两个候选值，随后被无条件改写） | 改 bool/enum 参数，删乱码分支 |
| SceneLayout.cs:181-199 | 音乐滑条闭包捕获当次局部 `manager`，若构建时 MusicManager 未就绪则音量滑条永久无效 | 监听内改 `FindMusicManager()` |
| Music.cs:346-350 vs SceneLayout.cs:273 | 音乐行两套奇偶底色（构建时一组、首次刷新被覆盖为另一组内联值） | 统一引用 ListRowEven/Odd |
| Sectors.cs:256-276 | 每秒 `ConfigureOuterPageScroll` 触发 ForceUpdateCanvases+ForceRebuildLayoutImmediate 三连 + 全行无条件构建诊断字符串 | 仅几何变化时重建；诊断串条件化 |
| ResearchTree.cs:1936-1979、2299-2316 + ResearchQueueGraphic.cs:212-221、298-301 | 研究"状态→颜色"映射 4 处重复（含 4 份 new Color 字面量） | 提取静态方法 + static readonly 颜色 |
| AuthoredRows.cs:132-138、245-251、381-387 | 三个排序比较器完全重复（旧 CSV 项，仍成立） | 泛型比较器 |
| KingdomUIPrefabLibrary.cs | 14 个常量 7 个零使用者但 `PreloadAll` 全量加载（含死 prefab ResearchCard） | 删死常量与死资产，按需预载 |

### Runtime / Data / Validation 层

| 位置 | 问题 | 建议 |
|---|---|---|
| GameState.cs:175-183、329-350、442-466 | Restore* 系无条件 `Version++`，与同文件"无变化不递增"约定并存，SaveManager 脏检测误报 | 统一为先比较后递增 |
| GameState.cs:15-16、100、131 | FoodCapacity 恢复依赖 `Max(Base, Amount)` 巧合下界，真实容量靠后续重建 | 文档化顺序或加断言 |
| ResourceState.cs:61-65 | tick 瞬态字段不校验 NaN/Infinity（对比 SetAmount 有归一化） | 补 EnsureFinite |
| ResearchState.cs:89-92 | 3 参 `Restore` 死重载 | 删除 |
| CampaignState.cs:38-39、97-98 | 非法参数统一报 `nameof(casualties)`，combatRatio 无效时错报参数名 | 区分 |
| Building.cs:101-116、287-301 | 合并缓存语义（MergeOpposingPairs 静默净额归零）与验证语义（原始两侧声明即抛错）对同一数据两种结局 | 单独使用 MergeOpposingPairs 时无保障，加验证 |
| SectorDefinition.cs:41-54 | EnemyPower/ColonizationFoodPerSecond/CampaignFoodPerSecond/ColonizationDurationSeconds 无缓存无归一化（对比 Building 17 字段全缓存） | 补 ParsedExpantaNumCache |
| EconomyDependencyValidator.cs:492-501、565-591 | `if (false && ...)` 死分支 + 整段注释的坏代码 | 删除 |
| SectorValidator.cs:451-466 | `BuildCycleError` 零调用；:84-94 错误信息覆盖丢失具体原因 | 删除 / 修控制流 |
| SectorValidator.cs:278 | 乱码死行（码点级确认） | 删除 |
| BuildingTransactionRules.cs:100-116 | `GeometricUnitCost`/`UpgradeCostDelta` 死 API；`ResearchSpeedEffect` 与 ResearchManager 公式写法不一致（当前数值等价，写法漂移） | 删死 API，统一公式实现 |
| WorkshopUpgradeState.cs | 缺自有 `ResetForLoad`，与其他 State 模式不一致 | 收进 State |

### 数学层 + 模拟器

| 位置 | 问题 | 建议 |
|---|---|---|
| ExpantaNumExtensions.cs:519-620 | **CSV 之后新增**的统计族（Erf/Erfc/NormalPDF/CDF/Quantile/InverseErf）整族零调用，且违背文件自述的"只组合已有数学能力"契约 | 整族删除或移独立程序集 |
| ExpantaNum.cs 正则热路径 | `TryParseCore` 每次解析走非预编译 `Regex.Match`（:310、:382-397），模拟器每 tick 每建筑 6-10 次字符串解析放大成本 | 预编译 static Regex；模拟器侧定义预解析缓存 |
| ExpantaNum.cs:1325/1392 | `Tetrate`/`Pentate` 对不可空 struct 用 `ExpantaNum?` 注解——模拟器 csproj Nullable=enable 链接编译时产生全部 18 个警告（机制已静态确认） | 随死代码删除或改 `= default` 签名 |
| ExpantaNum.cs:2023-2072 | `ToGameString` 每次 1-3 次 double.ToString + 2 次 Replace；LiveRefresh 22 处调用每刷新帧执行 | TryFormat 无分配版本或 UI 层缓存格式化签名 |
| ExpantaNum.cs:1916 vs 1969-1970 | Scientific 与 Suffix 回退路径两套尾数算法，极端值第 6 位有效数字可能不一致 | 统一 |
| SimulationCore.cs:741-744 | ApplyResearch 一次 60s 离线步内每个研究完成都触发完整 EnsureFrame 重建，最坏 O(研究数×建筑×收敛轮) | 批量完成后重建一次 |
| SimulationCore.cs:1242-1253 | `SnapshotDefinitionSet.Amounts` 每次新建 SortedDictionary 并逐项 Parse | 构造时预解析 |
| SnapshotValidation.cs:635-639 | Normalize 在 Validate 之前对含 null 事件的快照抛 ArgumentNullException 而非友好错误 | Normalize 跳过 null 或 Validate 提前 |

### 测试 + Editor 工具层

| 位置 | 问题 | 建议 |
|---|---|---|
| KingdomPlayModeTests.cs:1094-1128 vs SectorBuildingPlayModeTests.cs:235-269 | `GrantResearchTestResources`（~35 行）逐字复制 | 建共享 TestSupport 程序集 |
| 6 个测试文件 | 反射 Invoke 助手 6 套平行实现 | 过渡期统一为一个 ReflectionBridge |
| Editor 测试 757 处 `Is.EqualTo` / PlayMode 64 处 | 其中运算结果类裸精确相等约 40 处违反数值断言规则（AdvanceFood==120、1d/11d、CombatRatio==0.8d、"0.99999" 字符串等；对照 .Within 仅 102 处） | 按清单改 Within/关系断言 |
| EconomyParitySnapshotExporter.cs:399 vs SnapshotModels.cs:35 | `CostGrowth` 默认值 "1" vs "1.15" 已漂移；DTO 字段集/默认值/校验规则双份手写 | 加契约测试锁定字段集与默认值（反射用于契约测试合理） |
| tools/README.md:6/9、docs/repository-map.md:14 | 引用不存在的 `content-dependency/ContentDependencyAnalyzer.ps1` 与 `data/economy-parity/`；`data/economy-simulation/` 三个空目录与 handoff "已删除"声明矛盾 | 修引用、清空目录壳 |
| ContentDependencyAnalyzer.cs:53-137 | 闭包遍历与 ContentProgressionAudit.Run、EconomyDependencyValidator 三份平行实现 | 收敛到单一实现 |

### 文档契约

| 位置 | 问题 | 建议 |
|---|---|---|
| docs/architecture/ui-boundaries.md | 全文仍用 Viewer/Displayer/GameUIRefreshManager 幻影术语（当前代码 0 命中） | 按 KingdomUIRoot partial 实际架构重写 |
| docs/architecture/runtime-state.md:27-41、76-81 | "must be completed" 计划式表述与已实现矛盾；字面 `\n` 泄漏；同名段落完整重复两遍 | 重写为当前实现合同 |
| docs/architecture/serialized-pairs.md:31 | "serialized fields named first and second" 与实际磁盘形态（ResourceAmountDefinition{resource,amount}）不符；该文档同时要求保留 Pair.Deconstruct——与旧 CSV 建议删除 Deconstruct **直接冲突** | 修订文档；Deconstruct 取舍以文档为准先修文档 |
| .codex/handoffs/2026-09-05-music-playback-refresh-handoff.md | 音乐 56 首 → 实际 57 首（Tense15/Day15/Night14/AllTime13，目录与 Catalog 一致） | 刷新数字 |

---

## 三、P3 摘要（卫生与一致性，列代表性项）

- **死代码群**（全部经 grep 零调用复核）：ExpantaNum 三角/双曲/Gamma/Factorial/LambertW/超运算族约 700+ 行（FactorialTable 171 项死静态数据随类型加载初始化）；ExpantaNumExtensions Softcap 组/Prestige 组/Arithmetic 通道；ResearchTree.cs 布局死代码约 290 行（OrderResearchLayer + ImproveResearchLayerCrossings + CountResearchCrossingsAtBoundary 约 130 行、CreateResearchEraBands 63 行且与 :534 日志矛盾、LogResearchVisualDiagnostics 95 行）；AuthoredRows.BuildAuthoredResearchRows 33 行（连带 ResearchCard 死 prefab 资产）；MusicManager 三 Play 重载；StoryManager.FindNextLocked；ResourceManager.AddResource / BuildingManager.AddBuilding 死转发；BuildingManager.IsInBuildingChain/TryGetSectorBuilding；TutorialManager.IsProductionChainEndpoint（复核确认）；Sectors.CountSectorCards；SaveManager.ValidateStorySaveData 公共包装；UIPageScrollDragForwarder.IsAnyDragActive；Quantity.cs:18 死局部变量；KingdomUIRoot.cs:815 恒假分支；CampaignManager/ResearchManager 多个仅测试使用的便捷重载。
- **性能卫生**：ExpantaNum 比较路径 NaN 不对称（`==` 返回 false、`CompareTo` 抛异常）；StoryManager.GetProgressSignature 每刷新构造大字符串；UnsafeAreaTicker 每 5s 一条全量日志无 UNITY_EDITOR 包裹；FindNavigationButton 每秒 9 页循环字符串拼接 + Find；PageRows.SetBuildingActionButtonState 无条件重写 ColorBlock/Outline；TutorialManager Evaluate 每 100ms 无条件执行（与页面无关）。
- **一致性**：Singleton.cs 的 FindObjectOfType 弃用形态与域重载保护缺失；Tool.cs 三类职责混装且枚举显示映射与枚举定义分离（新增枚举成员忘了同步中文标签的长期隐患）；GameBootstrap 同一方法内"抛异常 vs LogWarning"两种失败策略并存；ProgressionModifierManager `AddXxxMultiplier` 名为 Add 实为乘；ResearchState 构造函数 TryParse 失败抛 FormatException 而 BaseCost 是 public 可写字段；TutorialManager fallback 文案与资产文案漂移（"理解资源来源" vs "理解库存与净产出"）；Story 叙事与前置轻微不一致（SiriusResourceBelt 描述"穿过比邻星"但前置是 TauCetiFoundry）；SectorState.RestoreExact 完全不校验；InterstellarTheoryNexus costGrowth 1.3 超 balance-model 建议区间 1.18-1.20（EarthMoonLogisticsHub 1.01 为 maxAmount=1 的 SectorBuilding 无实际影响）。
- **正面确认**：PlayMode 研究树审计 `ResearchTree_RuntimeLayoutAndOverflow_AreLoggedAndNonOverlapping` 三要素全部在位（79 唯一节点格、正向边界、真实拖拽内容移动）且质量高；食物唯一上限、无 workforce、批量购买闭式、无吞异常测试、存档 v8 契约、音乐目录 57 首全对齐、场景脚本引用零丢失、Sector 持续产出约为自建单座 10%-20%（合规但建议成文量化边界）。

---

## 四、建议执行顺序（供规划参考，非本次执行）

1. **正确性优先**：乱码修复（P1-1）→ 验证器天花板参数化（P1-2）→ epsilon 语义确认（P1-3，需作者裁决 Quantize 9e9 阈值意图）。
2. **低成本高收益**：StoryManager/GameManager/DetailPanel 的 FindObjectOfType → 单例（三处独立一行级修复）；`CaptureSaveData` 两处反射替换；LiveRefresh "0" 覆写 bug；音乐滑条闭包。
3. **结构性**：StableIdIndex 统一 → BuildingManager/SimulationCore/LiveRefresh/TutorialManager 拆分 → UI Prefab 化三大重灾区（每项独立成批，配 EditMode+PlayMode 验证）。
4. **死代码清扫**：约 2,300+ 行，建议按"每触碰一个文件顺手清理该文件"策略推进，避免一次性大删除。
5. **parity 防线**：共享常量 + trace diff 测试 + DTO 契约测试，先于任何进一步模拟器演进。

---

## 附录 A：旧 CSV（2026-09-07）复核结果摘要

- **已失效项**：MusicManager.QueuePlay（方法已删除）；Music.cs 图标配置重复（已重构为单一 ConfigureMusicIcon）；musicCurrentLabel/lastDevelopmentGuidanceSignature 未使用字段（已清理）；ConfigureMusicText/BringMusicTextToFront（已删除）。
- **仍成立项**（行号已更新）：GameManager Tick 重复、deltaSeconds 校验重复（18+ 处）、线性稳定 ID 查找（扩大为 5 处）、AddResource/AddBuilding 死转发、IsInBuildingChain/TryGetSectorBuilding、FindNextLocked、三排序比较器、CountSectorCards、Runtime 层 8 项死代码、ExpantaNum 高级数学 API 死链、Arithmetic/Softcap/Prestige 组、docs 过期项（ui-boundaries/runtime-state/tools README）。
- **需要修正的旧结论**：`Pair.Deconstruct` 建议删除与 `docs/architecture/serialized-pairs.md` 的兼容契约（"preserve Pair equality/hash/deconstruction behavior"）**直接冲突**——删除前必须先修订文档。

## 附录 B：主代理复核记录（P1 项）

| 子代理结论 | 复核方法 | 结果 |
|---|---|---|
| ResearchManager.cs:938/967/1125 乱码 | 码点级解析（U+20AC €、U+E000 私有区确认） | **属实** |
| DetailPanel.cs:1045 乱码匹配分支 | 码点级解析（37934,26050,9490 = 鐮旂┒） | **属实** |
| SectorValidator.cs:278 乱码行 | 码点级解析 | **属实** |
| StoryArchive 末三章 GUID 断链（内容代理 P1-#1） | 档案 guid 引用与三个 meta 逐一比对 | **不属实，推翻**：引用与 meta 完全一致（`710a...7e8` 等 32 字符合法 guid），子代理把含 "guid: " 前缀的整行长度（38）误判为 guid 长度。Story 18 章引用链健康。 |
| TutorialManager.IsProductionChainEndpoint 死代码 | 全仓库 grep | **属实**（仅定义处 1 命中） |
| EconomyDependencyValidator Industrial 天花板 | grep 6 处行号确认 | **属实** |
| BuildingManager.cs:182 O(N×M) List.Contains | 读上下文确认 | **属实** |
| SectorManager.cs:521-530 互补重复分支 | 读原文确认 | **属实** |
| StoryManager 3 处 FindObjectOfType | 计数确认 | **属实** |
| GameManager.cs:35 getter 内 FindObjectOfType | 读原文确认 | **属实** |
| MusicTrack prefab 缺 Category / DetailUI 运行时构建 / Sectors AddComponent×5 | prefab 与源码计数确认 | **属实** |
| ResearchManager.cs:187-188 不可达分支 / researchCountByTech 死字典 | 读原文 + grep | **属实** |
