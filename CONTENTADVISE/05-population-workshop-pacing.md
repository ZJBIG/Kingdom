# 人口/工坊/剧情/节奏只读审计
> 2026-09-12 · 只读静态扫描，未改动任何代码/资产/文档；未运行模拟器/Unity/测试；data/economy-simulation 为空，全部数字来自定义资产与运行时代码，增长时长为静态推算

## 1 领域清单（人口/食物模型、工坊 83 条概览、剧情教程覆盖表）

### 1.1 人口/食物模型（运行时常量与公式）

| 常量/机制 | 值 | 位置 |
| --- | --- | --- |
| FoodConsumptionPerPerson | 0.8 每 人 每秒 | PopulationState.cs:8 |
| ProductivityGrantedPerPerson | 2 每 人 每秒 | PopulationState.cs:9 |
| 基础人口增速 | 1/60 logistic 每秒乘 happiness 与增速乘数 | PopulationState.cs:10;302-312 |
| 饥饿离场速率 | shortage × max(1,人口)/3600 每秒；shortage=clamp01(1−happiness) | PopulationState.cs:327-336 |
| 饥饿离场触发 | FoodAmount≤0 且 FoodNetRate<0 | GameManager.cs:169-170 |
| 基础食物产能/容量 | 5 每秒 / 500；新档 Food=300 | GameState.cs:7-8;99-101 |
| 幸福公式 | score=log10(1+盈余/人)；mult=1+0.5×score/(score+1)+bonus；上限 1.5 | HappinessFormula.cs:6-7;81-94 |
| 食物可用性 | availability=clamp01(库存+产能×dt)/需求×dt)；<1 时 mult=availability | HappinessFormula.cs:23-39;67-69 |
| 净亏惩罚 | mult=1/(1+人均亏损)；净亏 −10/10 人 → 0.0909 | HappinessFormula.cs:72-79 |
| 幸福作用面 | RewardMultiplier 乘建筑与资源产出；ConstraintMultiplier 只约束无电或耗粮建筑 | BuildingManager.cs:1273;1357-1379; ResourceManager.cs:435-443 |
| 容量乘数 | FoodCapacityMultiplier 仅研究 1.21x（FoodPreservation 1.1 × FoodStorage 1.1）；人口容量无研究乘数 | ProgressionModifierManager.cs:270-274; BuildingManager.cs:1549-1597 |

人口容量建筑（每座）：WoodHouse 5（领土 3）；StoneHouse 12（3）；TownHouse 40（3）；IndustrialHabitationComplex 240（42 领土；耗 160 生产力）；OrbitalHabitatMegastructure 3000（650 领土；耗 900 生产力）。Ultra 0 座。

食物产能建筑（每座基础值）：Farm 8（领土 4；耗 3 生产力）；Smokehouse 2.5（食物容量 +400）；Granary 2（+1000）；IrrigationWorks 20（6 领土；8 生产力；+200 容量）；PlantingField 48（16 领土；55 生产力）；OrbitalAgroecologyArray 720（520 领土；720 生产力）。食物容量另见 CeramicKiln 250、RailHub 2500、OrbitalStation 30000。

Spacer 建筑新增食物维护（每座每秒）：OrbitalStation 10、EarthMoonLogisticsHub 4、Shipyard 4、DeepSpaceObservatory 3、QuantumComputingArray 3、PhaseMaterialSynthesisArray 3、DeepSpaceRelay 2.5、PhantomMaterialsFabricator 2、LaunchCenter 1.5、OrbitalCarbonizationComplex 1。

星区食物（Sector 资产）：殖民 16.67 至 4000 每秒（DawnRing 16.67；AzurePool 1333.33；Terminus 2333.33；ShardCrown 2500；ThunderGate 3000；HeliosCore 4000）；战役 2000 至 4666.67 每秒（AlphaCentauri 2000；ProximaB 3333.33；TauCetiFoundry 3666.67；SiriusResourceBelt 4666.67）。

### 1.2 工坊 83 条概览

分布：Industrial 37 条（TechLevel 3）、Spacer 46 条（TechLevel 4）、Ultra 0 条（冻结决策）。全部为一次性购买（无重复购买，几何增长规则不适用）；成本全部支付材料，且高时代支付高级结构材料（Steel、Concrete、Electronics、Machinery、TitaniumAlloy、Composite、PhantomAlloy、PhantomWeave、PhaseMaterial、RocketFuel、Biomass 1800 万级），符合锁定规则。

成本跨度：Industrial 内 9,000（IndustrialFoodProcessEngineering 合计）到 3,776,000（VacuumArcFurnace 合计）约 419 倍；Spacer 内 30,000 级到 18,862,700（OrbitalAgroponicSystems 合计）约 600 倍。

效果类型覆盖 19/20 种（Type 7 PowerMultiplier 未在工坊出现）；乘数区间 1.08 到 1.5，另有 6 条减量型（0.8 至 0.94：舰队维修/战役补给/战役伤亡）；一次性 TerritorryGranted 一条（InterstellarOccupationGovernance +30000）。

食物乘数（Type 20 GlobalFoodProductionMultiplier）工坊 6 条：1.5、1.69、1.25、1.25、1.20、1.30，合计 6.18x；研究侧 7 条（Type 32）合计 30.13x（Agriculture 1.2 × Calendar 1.331 × IrrigationEngineering 1.5 × AnimalFodder 1.2 × MechanicalEngineering 1.25 × IndustrialAgriculture 6.075 × SyntheticFertilizers 1.38）。全堆叠约 186x。

人口增速乘数：研究 OrbitalConstructionAutomation 1.2+1.15（加法堆叠 +0.35）；工坊 IndustrialHousingStandards 1.25、ClosedLoopBiosecurityModules 1.15、ColonialAssemblyCouncilTerminal 1.16、OrbitalLifeSupportNetworks 1.5 → 全堆叠约 2.16x。

### 1.3 剧情与教程覆盖表

剧情 18 章（StoryArchive.asset 固定顺序）+ 档案资产共 19 文件：

| 时代 | 章节 | 解锁条件要点 |
| --- | --- | --- |
| Animal 6 章 | 00 至 05 | 01 需教程 resources；02 需教程 building；03 需教程 population；04 需研究 KnowledgeSharing；05 需教程 production-chain + 2 研究 |
| StoneAge 1 章 | 06 | 需 StoneAgeSettlement（跃迁研究）+ 2 建筑 |
| Medieval 1 章 | 07 | 需 FeudalAdministration（跃迁研究）+ Caravanserai 等 |
| Industrial 6 章 | 08 至 13 | 需 Industrialization、PrecisionTooling 工坊、Coking、PetroleumExtraction、RailwayEngineering、TitaniumAlloyEngineering 等 |
| Spacer 3 章 | 14 至 16 | 需 HomeSystemSurvey、AzurePool 解锁并占领、Terminus/HeliosCore 占领、DeepSpaceFleet 等 5 研究 + 2 建筑 |
| Ultra 1 章 | 17 终章 | 需 TechnologicalSingularity（Ultra 唯一研究）+ InterstellarKnowledgeCoordination、InterstellarOccupationAdministration、QuantumComputing、GravitationalCommunicationTheory、MatterTransmutationTheory + 建筑 + 系外星区 AlphaCentauri/ProximaB 等占领 |

教程 8 步链：orientation（calendar-days>=1）→ resources（resource-detail-viewed）→ building（building-owned）→ population（population-grown）→ research（research-complete）→ production-chain（production-chain-owned）→ era-goal（era-reached）→ long-term（long-term）。全部 TriggerCondition=game-state，无 RewardId。教程 6/8 步与剧情 01-05 联动；第 06 章起剧情不再引用教程。

## 2 内容拓展机会

| # | 事项 | 证据 | 为什么值得 | 建议方向 | 优先级 | 工作量 |
| --- | --- | --- | --- | --- | --- | --- |
| E1 | StoneAge 与 Medieval 各只有 1 章剧情 | StoneAgeReturn_06、MedievalOrder_07 是对应时代全部章节；Animal 6 章、Industrial 6 章 | 石器/中古是 Milestone B/C 的主舞台，时长数小时却只有一拍叙事，叙事总纲的四拍结构（建制度/发现代价/留记录/备扩张）未兑现 | 各补 2 至 3 章，复用现有 RequiredResearch/RequiredBuildings 字段引用 FoodStorage、IrrigationEngineering、GuildSystem、UrbanHousing 等 | H | M |
| E2 | 教程无 Spacer 段 | Tutorial 仅 8 步全为早期；剧情 14-16 不再引用教程；arc 文档明确要求引导 HomeSystemSurvey→DeepSpaceFleet→InterstellarNavigation→Sectors（mouse-civilization-revival-arc.md 第 88 行） | Spacer 引入星区/远征/持续补给等全新系统，零引导直接进入 2000 至 4666 每秒的食物消耗 | 按 arc 文档补 3 至 5 步 Spacer 教程（复用 TutorialStepKind 与 CompletionCondition 机制） | H | M |
| E3 | 教程无 Workshop 步 | 8 步教程无工坊；WorkshopManager.IsSystemUnlocked 依赖 IndustrialWorkshop 研究 | 工坊是 Industrial 核心购买层（37 条），玩家只能靠 UI 自发现 | 增加 1 步：解锁 IndustrialWorkshop 后引导首购（如 IndustrialFoodProcessEngineering 成本仅 9000，适合教学位） | M | S |
| E4 | TechnologicalSingularity effects 为空 | Ultra/TechnologicalSingularity.asset:37 effects:[]；BaseCost 5.5e11；AdvancesTechLevel 1 | 违反新研究至少一个真实效果的内容门槛；完成 2418 倍于上一跃迁的成本后无任何玩法反馈，仅解锁终章 | 给它一个真实但克制的效果（如全局研究或建造小乘数）；不新增 Ultra 建筑/工坊，不违反冻结 | H | S |
| E5 | OrbitalAgroecologyArray 无专属建筑乘数 | OrbitalAgroponicSystems 的 Type 0 乘数目标 guid=b1c2d3e4（已核实为 PlantingField），非 OrbitalAgroecologyArray；全部 Spacer 工坊均无针对 Array 的乘数 | Spacer 唯一食物生产建筑（720 每秒）缺联动；工坊反而二度强化上时代 PlantingField | 新增 1 条 Spacer 工坊或给 OrbitalAgroecology 研究加 BuildingProductionMultiplier（Array 720 每秒） | M | S |
| E6 | Medieval 无新食物生产者 | Medieval 5 建筑无 foodProductionRate；食物仍靠 StoneAge IrrigationWorks 20 每秒 | TownHouse 40 容量 与 20 每秒产能在同一时代增长脱节；依赖堆叠旧建筑，玩法单调；也不符合每时代有 food 内容的节奏 | 给 Caravanserai 或新建筑一个 Medieval 档食物产能（约 40 至 60 每秒） | M | S |
| E7 | 食物容量研究线过薄 | FoodCapacityMultiplier 仅 FoodPreservation 1.1 + FoodStorage 1.1 = 1.21x；Spacer 仅 OrbitalStation +30000 | 满载容量缓冲从 Animal 约 56 秒降到 Industrial 约 8 秒（见 4.1），断供容错随进度恶化；容量是 Food 唯一上限语义，内容空间未用 | StoneAge/Industrial 各补 1 条容量研究（如 1.3x 档），维持 10 至 20 秒缓冲 | M | S |
| E8 | 饥饿离场机制对玩家不可见 | PopulationState.cs:327-336 离场速率公式；教程/剧情/文案均未提及 | 玩家首次遭遇 Food=0 离场会视为 bug；机制本身是好的压力设计 | 在 Resources 教程步描述或 UI 文案中标注；不动数值 | L | S |
| E9 | 幸福加成研究线已有 5 档但无 UI 叙事 | Type 31：HerbalKnowledge 0.02、PublicHealth 0.04、ModernMedicine 0.06、PrecisionMedicine 0.08、InterstellarAutonomyCharterTheory 0.06，合计 +0.26 | 这条线跨越四个时代且直接对冲饥饿惩罚，是幸福系统唯一可运营的杠杆，值得在故事/UI 中显性化 | 剧情 08/12 或教程补充；也可作为 E4 的效果候选（不叠加新机制） | L | S |

## 3 平衡风险

| # | 事项 | 证据 | 对玩法的影响 | 建议验证方式 | 严重度 |
| --- | --- | --- | --- | --- | --- |
| B1 | IndustrialAgriculture 全局食物乘数 6.075 离群（同类次大 1.5） | Industrial/IndustrialAgriculture.asset:38 value 6.075；研究侧合计 30.13x、加工坊 6.18x ≈ 186x | Industrial 起食物永远过剩：1 座 PlantingField（48×1.6×30.13≈2314 每秒）养 2892 人；幸福恒顶格 1.5；6 条 Type 20 食物工坊购买时点边际价值趋零 | 把 6.075 拆为 1.5 至 2 档进工坊或后续研究；先在 Unity 运行时记录盈余/人 曲线 | H |
| B2 | 幸福净亏惩罚过陡且与可用性双通道叠加 | HappinessFormula.cs:67-79；净亏 −10/10 人 → 0.0909；可用性 0.5 → 0.5 | 短暂断供即把全部无电/耗粮建筑砍到不足 10% 产出，连锁掉资源；玩家无预警 | PlayMode 记录 Food=0 后各建筑 efficiency 曲线；考虑给惩罚加下限或缓冲 | H |
| B3 | 满载食物容量缓冲随时代收紧至约 8 秒 | Industrial 满载 960 人 768 每秒 vs 容量约 6050（(500+2500+2000)×1.21）；Spacer 3000 人 2440 每秒 vs 约 42230 | 断供后库存约 8 至 17 秒见底，随后按 人口/3600 每秒 离场（半数约 42 分钟）；中途资源链已先崩 | 静态核算已给数字；建议 PlayMode 断供场景验证体感 | M |
| B4 | FlowEfficiencyTests 幸福惩罚用精确等值断言 | FlowEfficiencyTests.cs:61-66 Is.EqualTo(new ExpantaNum(1d/11d)) | 最新 2 失败之一；公式意图（1/(1+人均亏损)）正确，是断言卫生问题，违反数值断言规则 | 改为 Within 容差或关系断言 | M |
| B5 | 超容量离场代码为死代码 | PopulationState.AdvancePopulation 仅调用 Growth 或 FoodShortageDeparture；AdvanceDeparture（230-257 行）无调用点；CurrentDepartureRatePerSecond(departureAllowance) 恒返回 0（159 行） | 人口超容量永不回落；departureAllowance 参数链（GameManager.Tick→BuildingManager.SafePopulationDepartureAllowance）实际无效；与模拟器双实现漂移点一致 | 与 2026-09-10 审计合并处理；先写测试固化现状再清理 | M |
| B6 | 模拟器硬编码 0.8 双份 | tools/NewEconomySimulator/SimulationCore.cs:391;455 | 运行时改 FoodConsumptionPerPerson 时模拟器静默漂移 | 以 PopulationState 常量为唯一来源（只读审计不建议，交由后续任务） | M |
| B7 | 终章依赖零效果的超高成本研究 | TheOldBoundary_17 需 TechnologicalSingularity 5.5e11 + 4 项 Spacer 研究 + 系外星区占领 | 玩家在当前内容终点投入 5.5e11 研究力后无玩法回报，节奏死胡同（属冻结决策的副作用） | 见 E4；同时把终章条件在剧情 UI 中前置提示 | M |
| B8 | Industrial 工坊成本跨度 419 倍 | 9,000（IndustrialFoodProcessEngineering）到 3,776,000（VacuumArcFurnace） | 中段（100K 至 1M）购买力平坦，PrecisionTooling 3,640,000 与 PoweredMining 1,920,000 之间缺 200K 至 500K 档位承接 | 补 2 至 3 条中档 Industrial 工坊（可复用 E5/E6 角色） | L |
| B9 | Medieval 食物全靠跨时代建筑 | Medieval 0 个食物建筑；IrrigationWorks 20 每秒 ×3.594 全局乘数 | 不撑不住的风险低（400 人仅 320 每秒），但时代内无食物成长线 | 见 E6；先按 4.1 核算表复核满载档 | L |

## 4 重点详析

### 4.1 各时代人口-食物核算表（满载人口 × 0.8 vs 同时代产能；静态推算）

每档取该时代容量建筑的代表满载组合。乘数情景：无乘数=仅基础值+base 5；有乘数=该时代可获得的科技乘数（研究侧累计：Animal 1.597x；StoneAge 2.875x；Medieval 3.594x；Industrial 30.13x；Spacer 186x 含工坊）。

| 时代 | 满载人口 | 食物需求 每秒 | 无乘数所需产能建筑 | 有乘数所需 | 生产力核算 | 结论 |
| --- | --- | --- | --- | --- | --- | --- |
| Animal | 20（4 WoodHouse） | 16 | base5 + Farm 2 座（16）| Farm 1 座（12.8+8=20.8）| 人口供 40；2 Farm 耗 6 | 成立 |
| Animal 边界 | 40（8 WoodHouse） | 32 | Farm 4 座（32+5）| Farm 2 座 | 人口供 80；4 Farm 耗 12 | 成立 |
| StoneAge | 120（10 StoneHouse） | 96 | IrrigationWorks 5 座（100+5）| 2 座（115+14.4）| 人口供 240；灌溉耗 16 至 40 | 成立 |
| StoneAge 边界 | 240（20 StoneHouse） | 192 | IrrigationWorks 10 座 | 3 至 4 座 | 人口供 480；灌溉耗 80 | 成立但偏重领土（60） |
| Medieval | 400（10 TownHouse） | 320 | IrrigationWorks 16 座（325）| 5 座（359）| 人口供 800；灌溉耗 40 | 成立；零 Medieval 食物内容 |
| Medieval 边界 | 800 | 640 | IrrigationWorks 32 座（领土 192）| 9 座 | 领土压力先于产能 | 结构性单调 |
| Industrial | 960（4 HabitationComplex） | 768 + 建筑维护 | PlantingField 16 座（768+5）| 1 座（48×1.6×30.13≈2314）| 人口供 1920；住房 160+田 55 | 成立（已抽查 240 人同理） |
| Industrial 边界 | 1920（8 Complex） | 1536 | PlantingField 32 座 | 1 座 | 人口供 3840；住房 1280+田 55 | 成立 |
| Spacer | 3000（1 Megastructure） | 2400 + 建筑维护约 20 至 40 | OrbitalAgroecologyArray 4 座（2880+5）| 1 座（720×186≈134k）| 人口供 6000；舱体 900+Array 720 | 成立（已抽查 3000 人同理） |
| Spacer 边界 | 9000（3 舱） | 7200+ | Array 10 座 | 1 座 | 人口供 18000；舱 2700+Array 720 | 成立 |

交叉结论：产能从不卡人口；真正的约束是领土（IrrigationWorks 6/座、PlantingField 16/座、HabitationComplex 42/座）与食物容量缓冲（4.3）。满载容量缓冲秒数：Animal 约 900/16≈56s；StoneAge 约 2950/96≈31s；Industrial 约 6050/768≈7.9s；Spacer 约 42230/2440≈17s。

### 4.2 幸福公式关键阈值（HappinessFormula.cs）

| 人均食物盈余 每秒 | score=log10(1+x) | 乘数（无 bonus） | 备注 |
| --- | --- | --- | --- |
| 0 | 0 | 1.000 | 无惩罚无奖励的临界点 |
| 0.1 | 0.041 | 1.020 | 无感区间：前 10% 盈余只换 2% 产出 |
| 1 | 0.301 | 1.116 | |
| 9 | 1.000 | 1.250 | 半饱和点 |
| 99 | 2.000 | 1.333 | |
| 999 | 3.000 | 1.400 | |
| 1e100000（测试上限） | 巨大 | 趋近 1.5 | 硬上限 1+MaximumBonus |

惩罚侧：净亏 −1/10 人 → 0.909；−5/10 人 → 0.667；−10/10 人 → 0.909 的倒数级崩塌 0.0909。可用性通道独立：availability<1 时直接 mult=availability，优先于盈余奖励。HappinessBonus 合计 +0.26 与奖励共享 1.5 上限，即 bonus≥0.26 后基础乘数只需 ≥1.24（人均盈余约 7.4）即顶格。

结论：Animal 至 Medieval 幸福在 1.02 至 1.33 之间浮动，是有意义的弱杠杆；Industrial 后因 B1 恒顶格，系统失效。死区不存在（单调连续），但 0 至 0.1 盈余区间近乎无感。

### 4.3 工坊回本表（选 10 条；成本为资产合计；静态推算）

| 工坊（时代） | 成本明细（合计） | 效果 | 边际价值评估 |
| --- | --- | --- | --- |
| IndustrialFoodProcessEngineering（T3） | Steel 5000+Bronze 2000+Ceramic 2000（9,000） | 全局食物 ×1.5 | 教学位佳；但购买时点食物已过剩，实际体感弱 |
| EnzymaticConversionSystems（T3） | Steel 8000+Bronze 3500+Ceramic 3500（15,000） | 全局食物 ×1.25 | 同上 |
| ContinuousCultureBioreactors（T3） | Steel 14000+Bronze 6000+Ceramic 6000（26,000） | 全局食物 ×1.25 | 同上 |
| AgriculturalMachinery（T3） | Steel 100000+Engine 3600+Rubber 9000+Machinery 7200（119,800） | 全局食物 ×1.69 + PlantingField ×1.6 | 成本最高的食物条；价值同样被 B1 抵消 |
| IndustrialResearchCoordinationCenter（T3） | Electronics 24000+Machinery 16000+Glass 12000+CopperWire 8000+Chemical 6000+Steel 30000（96,000） | 全局研究 ×2 | 全场最高价值：直接减半全部研究时间，96K 成本回本极快 |
| IndustrialHousingStandards（T3） | Concrete 54000+Steel 120000+Glass 15000+Ceramic 10800+Electronics 7200（207,000） | 人口增速 ×1.25 | 人口增长本已快于研究（4.4），价值偏低 |
| PoweredMining（T3） | Steel 1800000+Machinery 120000（1,920,000） | 6 座采矿/加工建筑各 ×1.2 | 老建筑保持有用的范例条目；1.9M 成本需配合后期产能评估 |
| OrbitalLifeSupportNetworks（T4） | TiAlloy 9000+Composite 6000+PhantomWeave 3500+PhaseMaterial 2500+Electronics 36000+Biomass 9000000+Chemical 14400（9,067,400） | 人口增速 ×1.5 | Spacer 增速核心；900 万 Biomass 是 Biomass 主要后期去向之一 |
| OrbitalAgroponicSystems（T4） | Biomass 18000000+Glass 360000+Ceramic 252000+Electronics 132000+Chemical 108000+Composite 6000+TiAlloy 3500+PhaseMaterial 1200（18,862,700） | 全局食物 ×1.30 + PlantingField ×1.25 + Biomass ×1.25 | 全场最贵；食物 ×1.30 部分无感（B1）；Biomass ×1.25 与 +18M 消耗自洽但目标建筑错位（E5） |
| InterstellarOccupationGovernance（T4） | Biomass 18000000+TiAlloy 22500+Composite 16000+PhantomAlloy 13000+PhantomWeave 12000+PhaseMaterial 9000+Electronics 132000+RocketFuel 5000（18,189,500） | 领土 +30000 + 占领钛精矿 ×1.25 | 唯一领土条目；30000 领土对比 OrbitalHabitatMegastructure 650/座 约 46 座当量，战略价值真实 |

结论：6 条食物工坊在购买时点均无感（B1 连带）；真正驱动购买决策的是研究 ×2、增速 ×1.5、领土 +30000、减补给/维修 6 条。建议把食物工坊的价值重心移到建筑乘数或容量/效率等仍稀缺的维度，而不是继续堆全局乘数。

### 4.4 节奏静态推算（定义成本/产能推算；非运行时证据）

跃迁研究成本链（AdvancesTechLevel 节点）：StoneAgeSettlement 1,600 → FeudalAdministration 130,000（×81）→ Industrialization 336,000（×2.6）→ InterstellarNavigation 227,500,000（×677）→ TechnologicalSingularity 5.5e11（×2418）。

- StoneAge 入口平缓（1,600 与 Animal 普通节点同量级）；Medieval 130,000 是第一堵墙；Industrial 入口接续平缓（同档 IndustrialHabitationEngineering 520,000、IndustrialAgriculture 1,150,000）；InterstellarNavigation 227.5M 是第二堵墙；Ultra 5.5e11 是第三堵墙且无奖励（E4/B7）。
- 人口填充时长（r=1/60 logistic、happiness 1.0 静态推算）：C=240 从 1 至 239 约 11 分钟；C=480 从 240 至 479 约 6.2 分钟；C=3000 从 960 至 2999 约 8.8 分钟，叠加工坊/研究增速 2.16x 后约 4.1 分钟。人口远非节奏主轴，研究才是。
- 饥饿离场时长（静态）：availability=0 时半数人口约 42 分钟（ln2×3600s），是慢速压力而非惩罚性清场；但 B2 的产出惩罚会先于离场把经济打崩。
- 已知在档节奏事实：AGENTS.md 记录 offline pacing acceptance currently fails；本报告无该失败的任何数字，仅将其作为背景。

### 4.5 剧情终章依赖链（TheOldBoundary_17）

TechnologicalSingularity（5.5e11；前置 QuantumComputing、PhaseFieldNavigation、MatterStateControlTheory、InterstellarKnowledgeCoordination；资源 Electronics 64000+TiAlloy 48000+Composite 28000+PhaseMaterial 20000+PhantomAlloy 16000+PhantomWeave 14000；effects 空）+ 5 项 Spacer 研究 + 系外星区占领（AlphaCentauri、ProximaB 等）。即当前内容终点需要先打通全部 Spacer 星区战役（持续 2000 至 4666.67 每秒食物消耗）才能进入 Ultra 完成终章。剧情 14 至 16 三章与此链对齐良好；缺口在 Ultra 段无任何玩法承接（E4）。

## 5 证据清单

代码：Assets/Resources/Script/Runtime/PopulationState.cs（0.8;2;1/60;离场 3600;AdvanceDeparture 死代码）;Runtime/GameState.cs（BaseFoodProductionRate 5、BaseFoodCapacity 500、FoodNetRate、HappinessMultiplier）;Runtime/HappinessFormula.cs（0.5 上限、log10、1/(1+亏损)）;Manager/GameManager.cs（169-170 饥饿触发;207-222 PrepareHappiness）;Manager/BuildingManager.cs（129-130 离场 allowance;148-160 生产力;1357-1379 食物约束条件）;Manager/ProgressionModifierManager.cs（乘数堆叠规则）;Manager/WorkshopManager.cs;Manager/ResourceManager.cs（435-443）;Manager/SectorManager.cs（304;1566）;Manager/EraGoalEvaluator.cs;Manager/StoryManager.cs（54-98 固定 18 章）;Data/WorkshopUpgrade.cs（效果枚举）;Data/Building.cs;Data/StoryChapterDefinition.cs;Data/StoryArchiveDefinition.cs（97-108 星区条件校验）;Manager/TutorialStepDefinition.cs;Tests/Editor/FlowEfficiencyTests.cs:59-66;tools/NewEconomySimulator/SimulationCore.cs:391;455。

资产：Building/Animal/{WoodHouse 5,Farm 8,Smokehouse 2.5+400};Building/StoneAge/{StoneHouse 12,Granary 2+1000,IrrigationWorks 20+200,CeramicKiln 250};Building/Medieval/TownHouse 40;Building/Industrial/{IndustrialHabitationComplex 240,PlantingField 48,RailHub 2500};Building/Spacer/{OrbitalHabitatMegastructure 3000,OrbitalAgroecologyArray 720,OrbitalStation 30000+10};Research/Industrial/IndustrialAgriculture.asset（6.075）;Research/Ultra/TechnologicalSingularity.asset（5.5e11;effects 空）;Workshop/*.asset 共 83（本报告引用的 10 条成本/效果均来自资产原文）;Story/*.asset 19 文件（18 章+档案）;Tutorial/*.asset 8 文件;Sector/*.asset 10 文件（殖民/战役食物）。

文档：docs/balance/balance-model.md;docs/balance/no-resource-caps.md;docs/content/progression-roadmap.md（Ultra 冻结至 Milestone A-D 验收）;docs/story/mouse-civilization-revival-arc.md（88 行 Spacer 教程承接约束;94 行 Ultra 现状）。AGENTS.md（offline pacing acceptance currently fails 为唯一在档节奏事实）。
