# 既有证据与文档基线

> 2026-09-12 · 只读静态扫描，未改动任何代码/资产/文档。
> 证据边界：未引用 .codex/archive 下任何材料；docs/audits 仅取 2026-09-10 报告；data/economy-simulation 三个目录实测为空（冻结诊断已无文件实体，详见第 2 节）；data/content-closure-static.md 按当前有效证据引用；未运行模拟器、dotnet、Unity 或任何测试。

## 1 文档承诺 vs 资产现状差异表

格式：承诺（出处）→ 当前状态（证据）→ 差异结论。

1. Food 是唯一有容量的库存（AGENTS.md 不可协商经济规则；docs/balance/no-resource-caps.md 第 5-42 行）→ Resource 资产 40 个（Animal 6、Ingot 8、Mineral 7、Industrial 14、Spacer 5）未见 maxAmount/capacity 数值字段；Building.foodCapacityGranted 全仓扫描仅 6 处非零：Smokehouse 400、CeramicKiln 250、Granary 1000、IrrigationWorks 200、RailHub 2500、OrbitalStation 30000 → 规则与资产一致；但没有任何校验器针对真实资产守卫该规则（模拟器探针用合成状态，不算资产校验），属自动拦截盲区。另 no-resource-caps.md 第 34-40 行列举的食物容量来源（陶器、食物仓、冷藏、轨道粮仓）中"冷藏"无对应建筑，工业期容量主要由 RailHub（铁路枢纽，2500）承担，疑似代位，建议与人口/工坊代理交叉确认。
2. 建筑成本几何增长，growth 建议：原料 1.12~1.14、加工 1.15~1.17、研究/基础设施 1.18~1.20、住房 1.18~1.22（docs/balance/balance-model.md 第 11-19 行；AGENTS.md Number rules）→ 运行时校验 Building.HasValidCostGrowth 只要求非 NaN、非 Infinity、大于等于 1（Building.cs 第 141-147 行），小于 1 时静默回退默认值（CostGrowth getter 回退 DefaultCostGrowthValue）；已知越带样本：InterstellarTheoryNexus costGrowth "1.3"（Building/Spacer/InterstellarTheoryNexus.asset 第 20 行，超出全部建议带）、EarthMoonLogisticsHub 1.01（2026-09-10 审计 P3，maxAmount=1 的 SectorBuilding，当前无实际影响）→ 校验器只拦 NaN/Inf/小于 1，不拦数值偏离建议带；越带发现只能靠资产扫描。
3. 资源必须有可达来源加至少两个耐久去向或一个战略去向（.codex/prompts/CODEX_ECONOMY_PROMPT.md 第 40-46 行；AGENTS.md 内容质量门槛）→ 测试层 GlobalEconomyDefinitionTests.所有资源都必须有来源和多个真实消费节点（第 623-664 行）对硬编码 ReleasedResourceIds 40 个资源强制 source>=1、sink>=2（统计建筑产出/需求/消耗、研究需求、工坊需求、星区战役与殖民成本）→ 规则被执行，但名单是硬编码 40 个字符串（第 94-103 行）：新增第 41 个资源默认逃逸该审计；"耐久/战略"语义只被数量阈值粗略代替。
4. 新研究必须有真实效果（AGENTS.md 内容质量门槛）→ GlobalEconomyDefinitionTests.EveryResearchAndWorkshopEffectHasAValidTarget（第 399-462 行）强制研究要么 AdvancesTechLevel 要么有 Effects，效果 NumericValue 大于 0 且目标存在；所有效果类型必须有运行时分支（第 505-536 行）→ 覆盖良好。
5. 新建筑必须有明确定义角色与第一副本回本目标（AGENTS.md 内容质量门槛；balance-model.md 第 48-55 行回本目标：原料 45~120 秒、加工 90~240 秒、研究 5~15 分钟）→ 测试层有"每个建筑都必须承担明确的独立职责"（独立功能字段检查）与"生产力建筑必须同时承担明确独立功能"→ 独立角色被拦截；但全仓没有任何回本目标的校验器、测试或登记表 → 回本目标纯手工承诺，是节奏审查的空白。
6. 研究成本按目标耗时倒推，目标：首项 30~90 秒、原始普通 2~8 分钟、原始跃迁 10~20 分钟、石器普通 5~20 分钟、石器跃迁 20~60 分钟、中古完整阶段 4~12 小时（balance-model.md 第 31-46 行）→ 仅 EarlyVerticalSlicePacingTests 锁定 Industrialization 336000、Steelmaking 18144、MechanicalEngineering 13392（第 72-89 行）与 KnowledgeCircle growth 1.20~1.22（第 16-18 行）；无全时代研究时长校验；AGENTS.md 明示 offline pacing acceptance 当前失败且冻结诊断文件已清空 → 目标时长无自动守护，中古/工业阶段 4~12 小时是否达成目前没有任何在档数字可对账。
7. 可达性：从新档静态推演原始主线、StoneAgeSettlement、石器资源链、FeudalAdministration、中古（docs/testing/content-balance-tests.md 第 13-23 行）→ EconomyDependencyValidator.ValidateReleasedReachability（第 133-251 行）实现该推演，但硬编码 TechLevel.Industrial 天花板（第 156、175、189、213、226、235 行）与硬编码 WoodLog 种子（第 145-147 行）→ Industrial 及以前由启动 CLI 与测试双重拦截；Spacer 可达性只在测试层覆盖（SpaceProgressionTests 含 IndustrialProgressionAuditReachesTheUltraTransition 等 60 余用例）；Ultra/Archotech 的启动验证为空白（与 2026-09-10 审计 P1-2 一致，该修复尚未实施）。
8. 路线图后续方向：进入 Spacer 后优先完善远征补给、舰队维修、星区反馈；Ultra 与 Archotech 在 Milestone A-D 和移动端验收完成前冻结（docs/content/progression-roadmap.md 第 35-40 行）→ 资产现状：Spacer 47 研究、46 工坊、16 建筑、5 资源、10 星区；Ultra 仅 TechnologicalSingularity 1 个研究，Building/Ultra 目录为空，0 工坊 0 资源；Archotech 只有 TechLevel 枚举值无任何定义（GameManager.cs 第 5-14 行；data/content-closure-static.md 第 12-14 行）→ 冻结承诺与资产一致；任何"Ultra 缺内容"的拓展建议都违反当前文档决策，拓展前必须先过 A-D 验收。
9. 叙事总纲对 Ultra 的当前边界：仅 TechnologicalSingularity 研究加既有 Spacer 生产链承担代价；Archotech 不得虚构任何内容（docs/story/mouse-civilization-revival-arc.md 第 90-112、116 行）→ 与资产一致 → 无差异；Story 终章 TheOldBoundary_17 依赖该研究与系外驻留条件。
10. Story 基线固定 18 章，存档 v8 必须含按序连续前缀的完成记录（docs/story/story-data-authoring.md 第 29 行）→ Story 目录 19 个 asset（18 章 + StoryArchive），StoryManagerTests 存在 → 一致。
11. 本星系探索只消耗 Food 与行动资源，只授予 Territory，ResourceRewards 必须为空（docs/content/home-system-exploration-rewards.md 第 1-11 行）→ SectorValidator.ValidateRewards（第 290-319 行）强制本星系 ResourceRewards 为空、远星必须有正奖励 → 已由校验器拦截，超出文档承诺。
12. 星际战役必须材料与时间昂贵：持续 food、燃料、物流、高级材料成本；奖励不得停留在数百或数千（CODEX_ECONOMY_PROMPT.md 第 54-58 行）→ SectorValidator 硬数值守卫：远星 TerritoryReward>=100000（第 6-7、103-110 行）、CampaignProgressMultiplier<=1/60 即战役至少约 30 分钟（第 8-10、84-90 行）、战役成本必须持续消耗 TitaniumAlloy、Composite、PhantomAlloy、PhantomWeave、PhaseMaterial 之一（第 113-147 行）→ 资源奖励的数量级下限没有守卫（只查领土与高级材料消耗），是残差盲区。
13. Sector 长期产出不得明显取代玩家自建高级生产（CODEX_ECONOMY_PROMPT.md 第 72-75 行）→ 2026-09-10 审计正面确认 Sector 持续产出约为自建单座 10%~20%（合规但建议成文量化边界，审计第 189 行）→ 行为合规，量化边界未成文。
14. Food 满足率影响效率、人口消耗食物、食物不足停止增长（balance-model.md 第 57-68 行）→ FlowEfficiencyTests、FoodEfficiencyTests 存在；最新失败之一正是 FlowEfficiencyTests 的幸福惩罚精度断言（TestResults/Latest-Test-Errors.txt FAILURE 1）→ 行为在实现中且被测试锁定，但该测试当前为红（精度断言问题，见第 4 节）。
15. 领土与生产力初始值必须足够小以形成实际选择（balance-model.md 第 70-77 行）；数值符号量级方向：原始/石器 1~1e6、中古 1e4~1e10、工业 1e8~1e20、太空 1e18~1e50（balance-model.md 第 79-89 行）→ 未发现任何校验器或测试检查这两条 → 纯盲区。
16. 研究资源成本在进度开始前原子支付（CODEX_ECONOMY_PROMPT.md 第 29 行；docs/testing/acceptance-checklist.md 交易节）→ 模拟器 ValidationSuite.CheckAtomicResearchPayment（不可支付零变化、可支付全额一次扣款、防重复）与测试层 ResearchPaymentAutoTests 覆盖 → 覆盖。
17. 大数规则：批量购买必须用闭式 ExpantaNum 扩展函数（AGENTS.md Number rules）→ 模拟器 CheckGeometricCost 与测试层 GeometricSeriesCost、MaxAffordableGeometricSeries 用例覆盖；审计确认批量购买闭式函数未被违反（审计第 21 行）→ 覆盖；但 GeometricCost 双实现漂移见第 6 节 P1-9。
18. 时代步长契约：Unity parity 默认 0.1 秒实时/60 秒离线；时代自适应步长为显式长周期诊断模式（tools/NewEconomySimulator/README.md 第 49-73 行）→ ValidationSuite.CheckEraStepSchedule 锁定 7 时代步长表 → 覆盖。

## 2 冻结诊断摘要

- data/economy-simulation/Normal、Fast、Conservative 三个目录实测均为空（0 文件，目录时间戳 2026-09-07 19:02）；data/content-dependency 同样为空；data/economy-parity 目录不存在。因此不存在可提取的总时长、各时代时长、里程碑时刻或失败项数字；冻结诊断目前只是目录壳。
- 当前唯一在档的节奏失败事实：AGENTS.md Current known blockers 明示 offline pacing acceptance currently fails and remains diagnostic evidence；docs/story/mouse-civilization-revival-arc.md 第 121 行佐证"模拟 pacing 有失败项"。其余节奏数字一律不可引用。
- 模拟器本身已转型：README 第 43-45 行明示历史独立模拟器文件与 pacing 报告已退役；当前 SimulationMode 只有 Realtime 与 Offline（ScenarioRunner.cs 第 46-47 行），旧 Normal/Fast/Conservative 场景体系已无代码对应。引用任何"冻结诊断"数字前必须先重跑并重新冻结。

## 3 校验器强制规则清单

格式：文件 → 强制规则 → 拦截不了什么。

1. Assets/Editor/Content/RuntimeClosureValidatorCommand.cs → 命令行入口：调用 EconomyDependencyValidator.Validate，失败抛异常并 Exit(2)，成功打印 RUNTIME_CLOSURE_VALIDATION_PASS → 它本身不新增规则；覆盖范围受被调用方限制（见下）。
2. Assets/Resources/Script/Validation/EconomyDependencyValidator.cs → 六步：建筑研究/工坊前置非空且不重复；研究无环（含空引用、自指、重复前置）；工坊升级必须有至少一个前置、非空、不重复、不自指、无环；生产配方图无不可自举循环；直接解锁死锁（建筑所需资源唯一生产者是自己则报错，含经工坊/研究间接情况）；可达性推演（WoodLog 种子起步，工坊系统需 IndustrialWorkshop 解锁，所有不超过 Industrial 的研究/工坊/建筑必须可达）→ 拦截不了：Spacer/Ultra/Archotech 可达性（硬编码 Industrial 天花板）；costGrowth 数值质量；Food 唯一上限与普通资源容量字段；资源 sink 数量；任何数值与节奏；另第 492-501 行有 if(false) 死分支（审计登记）。
3. Assets/Resources/Script/Validation/ResearchValidator.cs → 研究前置空引用、自指、重复声明、依赖环（EconomyDependencyValidator 第 2 步调用）→ 拦截不了：前置缺失导致的时代不可达（由可达性步骤兜底）、效果与成本数值。
4. Assets/Resources/Script/Validation/SectorValidator.cs → 星区 Id 非空唯一、名称与描述非空、本星系与星系 ID 配置一致、EnemyPower 与 TerritoryReward 非 NaN 且前者>=0 后者>0、本星系 ResourceRewards 必须为空、远星必须至少一种正资源奖励、探索/战役成本非 NaN 非负、CampaignProgressMultiplier>0 且<=1/60、远星 TerritoryReward>=100000、远星战役成本必须持续消耗 5 种高级材料之一、前置星区非空不重复不自指、无环、每个远星星区必须能从本星系探索区到达、本星系不得依赖远星 → 拦截不了：资源奖励数量级上限、敌方强度曲线合理性、星区产出与自建生产的平衡比、Food 消耗速率与产能的匹配。
5. Assets/Resources/Script/Data/Building.cs 第 141-147 行（由 BuildingManager.ValidateChainEconomy 第 240-244 行抛异常强制）→ costGrowth 非 NaN、非 Infinity、>=1；小于 1 时 CostGrowth 静默回退默认值 → 拦截不了：growth 恰为 1（线性成本）、growth 低于或高于 balance-model 建议带。
6. Assets/Editor/Content/ContentDependencyAnalyzer.cs → 只诊断不拦截：不可达研究/建筑及阻断路径、无生产者资源、研究环、资源死锁；输出 data/content-dependency-analysis.md（当前该文件不存在，目录为空）→ 非门禁；三份闭包实现之一（审计建议收敛）。
7. Assets/Resources/Script/Validation/ProgressionMilestoneRecorder.cs → 运行时证据记录：10 个里程碑时刻与 7 时代乘 5 类瓶颈（资源等待、研究等待、生产力受阻、人口受阻、有动作可做）采样，仅 UNITY_EDITOR 或 DEVELOPMENT_BUILD → 不是门禁，是节奏取证工具；当前无其输出在档。
8. 测试层补充（非 Editor/Content 目录，但构成自动拦截的第二层）→ GlobalEconomyDefinitionTests：效果目标与数值、研究/工坊效果目标去重、建筑独立职责、重复产出签名禁止、生产力必须伴随人口容量、40 资源 source/sink、数值字段字符串序列化合同；ResearchBalanceTests：PhantomAlloy/PhantomWeave/PhaseMaterial/TitaniumAlloy 消费必须先过对应理论研究且时代有序；C5ContentClosureAuditTests、C6IndustrialClosureAuditTests、ContentProgressionValidatorTests：新档可达工业、稳定 ID 唯一、中古战略资源 source/sink；SpaceProgressionTests（约 60 用例）：Spacer 建筑必须用高级结构材料建造并持续维护、高级材料必须有来源与长期战略消耗、工业到太空上位替代链、战役/补给效果职责边界、IndustrialProgressionAuditReachesTheUltraTransition；EarlyVerticalSlicePacingTests：KnowledgeCircle growth 1.20~1.22、9 条早期产出率、3 个工业化节点成本；BuildingCostGrowthTests：Farm 1.14、链验证与合并资源流验证 → 拦截不了的共性盲区：Food 唯一上限对真实资产的静态守卫（模拟器探针用合成状态）、普通资源容量字段守卫、回本目标、研究目标时长、领土/生产力初值、数值符号量级。

## 4 测试错误要点

- TestResults/Latest-Test-Errors.txt（生成于 2026-09-11 13:14:30，Mode 0，Result Failed(Child)）：总计 660，通过 658，失败 2，跳过 0。
- 失败 1：FlowEfficiencyTests.HappinessPenalizesNegativeFoodNetRateEvenWhenInventoryIsAvailable（Assets/Tests/Editor/FlowEfficiencyTests.cs 第 65 行）：期望 0.090909090909090912 容差 1e-9，实际 0.090909000000000004 → 属精度/断言卫生问题（符合审计登记的裸精确断言债），非明确行为缺失；修复方向是放宽容差或换关系断言。
- 失败 2：SectorBuildingTests.SectorBuildingBuildAndSavePathIsOccupiedAndTerritoryFree（Assets/Tests/Editor/SectorBuildingTests.cs 第 223 行）：期望 True 实际 False → 行为级失败：星区建筑建造与存档路径上的占领与领土释放，值得由区域军事代理优先复现。
- 旁证（TestResults XML）：EditMode-results.xml（2026-09-08）660 全绿；PlayMode-results.xml（2026-09-08）34 例 33 过 1 跳过；PlayMode-Isolated-results.xml（2026-08-30）32 例。AGENTS.md 中"现有 PlayMode 报告包含零测试用例"的表述相对这些 XML 已过时，但 content-balance-tests.md 第 49-58 行列出的必须新增 PlayMode 场景（新游戏启动、前 10 分钟、页面关闭模拟继续、保存/加载、后台恢复、P40 横屏）仍未被覆盖，基础用例通过不构成这些场景的验收。

## 5 模拟器场景清单与用途

- 入口：Program.cs 只执行 ValidationSuite.RunCore()，退出码 0/1；默认 Markdown 输出，--json 与 --csv 可选；报告合同测试强制输出中禁止出现 pacing、balance、acceptance 词汇（ValidationSuite.cs 第 470-517 行）。README 第 32-39 行说明其为库加 .NET 文件入口，不改动 csproj。
- 校验场景（ValidationSuite.cs 第 123-150 行及各 Check 方法）：determinism 同种子轨迹与终态一致；ordinary resource capacity 普通资源无容量且可增长超过 Food 上限探针；Food capacity 恰一个正上限并钳制超填；atomic research payment 不可支付零变化、可支付全额一次扣款、激活后拒绝重复支付；save restore 存档恢复等于保存点且续跑轨迹与无中断一致；building geometric bulk cost 闭式批量成本一致性；workshop production chain 单次购买与 WoodLog-Plank-Alloy 链序事件；realtime/offline parity 状态与事件序一致（忽略 mode 标记）；large ExpantaNum 1e308 加法与批量成本有限性；era step schedule 七时代步长表（Animal 0.1/1 秒、StoneAge 0.5/2、Medieval 1/5、Industrial 5/10、Spacer 15/20、Ultra 30/30、Archotech 60/60）与 Unity parity 默认 0.1/60；trace first difference 与 JSON/CSV/Markdown 报告合同。
- 用途边界：所有场景运行在合成状态（WoodLog、Plank、Alloy、Sawmill、AlloyForge 等夹具），不是真实资产；README 与 docs/testing/content-balance-tests.md 明文规定其输出不是玩法、节奏、平衡或 Unity 验收证据；仅作确定性 parity 事实与维护回归。禁止据其输出调数值、扩展策略搜索（AGENTS.md known blockers）。

## 6 已知问题基线（来自 docs/audits/2026-09-10-full-readonly-refactor-scan.md）

报告性质：全仓库只读重构扫描，未修改任何文件，6 领域并行扫描加主代理对 P1 的独立复核，未执行真实 Unity 编译。以下仅取与内容/平衡相关条目（P1-1 乱码、P1-7/P1-8 UI 等略，详见原报告）：

1. P1-2：EconomyDependencyValidator 硬编码 TechLevel.Industrial 可达性天花板（6 处行号）与硬编码 WoodLog 种子（第 145-147、731 行）；Spacer 已有 47 研究 16 建筑而启动验证不覆盖 → 本次审计复核属实（源码仍是 6 处 Industrial）。
2. P1-9：Unity 与模拟器公式双实现漂移：人口离场 departureAllowance 与食物 UncappedFoodCeiling 两处已失同步；GeometricCost 双实现（扩展版有 PowMinusOne 稳定路径，模拟器版没有，ratio 接近 1 时可产生不同可负担数量）；无测试锁定两侧公式形状。
3. P3 一致性：InterstellarTheoryNexus costGrowth 1.3 超 balance-model 研究基础设施建议带 1.18-1.20；EarthMoonLogisticsHub 1.01 为 maxAmount=1 的 SectorBuilding 无实际影响 → 本次已从资产 YAML 复核 1.3 属实。
4. P3 正面确认：Sector 持续产出约为自建单座 10%~20%（合规但建议成文量化边界）；食物唯一上限、无 workforce、批量购买闭式、CanvasScaler 硬合同等不可协商规则全部未被违反。
5. P3 叙事：Story 叙事与前置轻微不一致（SiriusResourceBelt 描述"穿过比邻星"但前置是 TauCetiFoundry）。
6. 工具与文档引用失效：tools/README.md 与 docs/repository-map.md 引用不存在的 content-dependency 分析脚本与 data/economy-parity；data/economy-simulation 三个空目录与 handoff"已删除"声明矛盾 → 本次实测三个目录确实为空。
7. 断言卫生：Editor 测试约 40 处运算结果裸精确相等违反数值断言规则（.Within 仅 102 处），最新失败之一（FlowEfficiency）即属此类。
8. P1-6：17 个测试文件构建在非公共反射上（356 处 API 命中加约 200 处 Invoke），与"新增代码和测试必须使用公开强类型 API"的仓库规则冲突；历史测试中的反射调用不作为新增实现范例（AGENTS.md 代码约束）。

## 7 证据清单

当前有效证据（本次直接读取）：
- data/content-closure-static.md（TechLevel 5；Industrial 81/81 研究、37/37 工坊、50/50 建筑；Spacer 47/47、46/46、16/16；Ultra 1/1、0/0、0/0；资源 40；各不可达清单为空）
- TestResults/Latest-Test-Errors.txt（2026-09-11；660 中 658 过 2 失败）
- TestResults/EditMode-results.xml、PlayMode-results.xml、PlayMode-Isolated-results.xml（仅 test-run 摘要行）
- 资产计数（dir 实测）：Research 129、Workshop 83、Building 66、Resource 40、Sector 10、Story 19、Tutorial 8 个 asset；Resource/Spacer 5 个；Research/Ultra 仅 TechnologicalSingularity；Building/Ultra 空
- Assets/Resources/Datas/Building 全部 foodCapacityGranted 扫描（6 处非零）；Building/Spacer/InterstellarTheoryNexus.asset 第 20 行 costGrowth "1.3"
- 校验器源码：Assets/Editor/Content/RuntimeClosureValidatorCommand.cs、Assets/Editor/Content/ContentDependencyAnalyzer.cs、Assets/Resources/Script/Validation/EconomyDependencyValidator.cs、ResearchValidator.cs、SectorValidator.cs、ProgressionMilestoneRecorder.cs、BuildingTransactionRules.cs（交易助手非内容校验器）、Building.cs 第 141-147 行、BuildingManager.cs 第 240-244 行
- 模拟器：tools/NewEconomySimulator/README.md、Program.cs、ScenarioRunner.cs、Tests/ValidationSuite.cs（EraStepSchedule.cs 经 README 与 ValidationSuite 引用，未单独通读）
- 测试样本：GlobalEconomyDefinitionTests.cs、ResearchBalanceTests.cs（前 100 行）、EarlyVerticalSlicePacingTests.cs、BuildingCostGrowthTests.cs（前 120 行）、Assets/Tests/Editor 目录清单（46 文件）
- 文档：AGENTS.md（仓库根）、.codex/prompts/CODEX_ECONOMY_PROMPT.md、docs/balance/balance-model.md、docs/balance/no-resource-caps.md、docs/content/progression-roadmap.md、docs/content/alien-war-first-version.md、docs/content/home-system-exploration-rewards.md、docs/content/kittens-game-reference-boundary.md、docs/testing/content-balance-tests.md、docs/testing/acceptance-checklist.md、docs/rules/kingdom-rules.md、docs/decisions/conservative-defaults.md、docs/story/story-data-authoring.md、docs/story/mouse-civilization-revival-arc.md（第 74-138 行）、.agents/skills/kingdom-content-expansion/SKILL.md（第 31-81 行）
- 审计：docs/audits/2026-09-10-full-readonly-refactor-scan.md（全文）
- 空目录实测：data/economy-simulation/{Normal,Fast,Conservative}、data/content-dependency（均 0 文件）；data/economy-parity 不存在
- docs/decisions/definition-database.md 与 research-queue-payment.md 在目录清单中存在但本次未读取（其内容已被 definition-database 相关测试与 acceptance-checklist 覆盖度较好的证据替代，如需可补读）

明示未做的事：未运行静态闭包或模拟器（未执行真实 Unity 编译，亦未执行 dotnet）；未读取 .codex/archive、docs/audits 其他文件与 .codex/handoffs；未修改任何文件（本报告除外）。
