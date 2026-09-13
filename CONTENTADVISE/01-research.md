# 科研树只读审计
> 2026-09-12 · 只读静态扫描，未改动任何代码/资产/文档。审计范围：Assets/Resources/Datas/Research/ 全部 129 个 .asset、Research.cs、ResearchEffect.cs、ResearchManager.cs、ProgressionModifierManager.cs、EraGoalEvaluator.cs、researchPowerGranted 建筑字段、data/content-closure-static.md、docs/content/progression-roadmap.md。未运行模拟器/Unity/测试。

## 1 领域清单（各时代科研数量表、字段结构、成本数值范围）

### 1.1 时代分布与成本（BaseCost 为 ExpantaNum 字符串，直接取自资产）

| 时代目录 | TechLevel | 科研数 | 成本范围 | 时代成本总和 | 时代门节点（AdvancesTechLevel=1） | 门成本 |
|---|---|---|---|---|---|---|
| Animal | 0 | 16 | 60（ControlledFire）至 2800（NaturalPhilosophy） | 约 19,410 | StoneAgeSettlement | 1,600 |
| StoneAge | 1 | 17 | 1,600 至 21,000（Smithing_Bronze） | 约 162,900 | FeudalAdministration | 130,000 |
| Medieval | 2 | 12 | 13,392（MechanicalEngineering）至 270,000（Gunpowder） | 约 1,891,536 | Industrialization | 336,000 |
| Industrial | 3 | 36 | 75,000（SteamPower）至 2,700,000（TitaniumAlloyEngineering） | 约 30,771,000 | InterstellarNavigation | 227,500,000 |
| Spacer | 4 | 47 | 36,000,000（OrbitalCarbonizationProcessEngineering）至 4,500,000,000（MatterStateControlTheory） | 约 38,243,700,000 | TechnologicalSingularity | 550,000,000,000 |
| Ultra | 5 | 1 | 550,000,000,000 | 550,000,000,000 | （自身即门） | — |

相邻时代成本总和比：8.4x、11.6x、16.3x、1243x（TL3→TL4）、14.4x（TL4→TL5 门对全 Spacer）。TL3→TL4 的 1243x 明显脱离此前 8 至 16x 的节奏（详见第 4 节）。

### 1.2 字段结构（Assets/Resources/Script/Data/Research.cs 与 ResearchEffect.cs）

- Research：Label、Description、BaseCost（字符串 ExpantaNum）、prerequisites（Research 引用列表）、resourceRequirements（Resource+amount 列表）、effects（ResearchEffectDefinition 列表：Type/Building/Resource/value）、TechLevel、AdvancesTechLevel。
- ResearchEffectType 共 29 种（1,3 至 8,10 至 17,22 至 33）+ 4 个系统解锁（18 Workshop、19 HomeSystemSurvey、20 DeepSpaceFleet、21 InterstellarNavigation）。
- 全树 128/129 节点至少 1 条效果；唯一 effects 为空的是 Ultra/TechnologicalSingularity（第 3 节 BAL-1）。
- 资源需求覆盖 40 个资源中几乎所有非食物资源；Bronze 在 TL2 至 TL3 仍有 10 处研究需求、Electronics 是 Spacer 时代最大研究消耗品（约 35 个节点，单笔最高 480,000，InterstellarKnowledgeCoordination）；Machinery 在 Spacer 约 22 个节点。老资源作为研究消耗品延续良好，但作为生产加成对象几乎无科研（见 EXP-4）。

### 1.3 运行时关键语义（已读源码核实）

- 速度公式（ResearchManager.cs:512-516、565-574）：speed = ResearchSpeedEffect(当前TL, 目标TL) x GlobalEfficiencyFactor x ResearchPower x GlobalResearchMultiplier x HappinessMultiplier。
- ResearchSpeedEffect（ResearchManager.cs:829-834）：同代=1；跨代=1/(代差+0.5)。时代门节点（目标代=当前+1）恒为 1/1.5=0.667x。
- ResearchPower（ResearchManager.cs:34、1160-1184）= 4（基数）+ Σ 建筑 researchPowerGranted x 数量 x 效率 x BuildingResearchPowerMultiplier。
- 乘数堆叠语义（ProgressionModifierManager.cs:184-188）：除 GlobalFoodProductionMultiplier、FoodCapacityMultiplier、FleetRepairCostMultiplier、CampaignSupplyCost、CampaignCasualty 为乘法（*=）外，其余全局/按建筑乘数全部为加法堆叠 Max(1, 1+Σ(v-1))。
- DeconstructionReturnRate（ProgressionModifierManager.cs:149-156）：取历史最大值并夹在 0 至 1，基线 0.05。Runtime 侧 StoneTools 0.1、UrbanHousing 0.25、Industrialization 0.5、MatterStateControlTheory 0.12、OrbitalHabitation 0.9。
- MilitaryMultiplier（Type 10）仅当 科研.TechLevel >= Spacer 才生效（ProgressionModifierManager.cs:294-296）；全树仅 DeepSpaceFleet 1.25 与 InterstellarCombatLogistics 1.30 两处。
- 时代门（EraGoalEvaluator.cs:177-191）： AdvancesTechLevel 且 TechLevel=目标代 的唯一节点，条件=其全部前置完成+资源需求。
- 提供 ResearchPower 的建筑（researchPowerGranted）：KnowledgeCircle 1（Animal）、ScribeHut 5（StoneAge）、Library 25、Academy 40（Medieval）、University 250（Industrial）、DeepSpaceObservatory 220、QuantumComputingArray 500、InterstellarTheoryNexus 900（Spacer）；Ultra 无。

### 1.4 前置结构

- 前置数量分布（近似）：0 前置 4 个（Agriculture、AnimalHusbandry、ControlledFire、Quarry，均在 Animal）；1 前置约 20；2 前置约 33；3 前置约 43；4 前置约 19；5 前置 10；6 前置 1（InterstellarOccupationAdministration）。
- 最深线性链约 30 级：ControlledFire 起，经 KnowledgeSharing、Mathematics、Calendar、WrittenRecords，入 Medieval Smithing 链至 FeudalAdministration、Bookmaking，经 ScientificMethod、ElectricalEngineering、Standardization 至 RailwayEngineering、ScientificInstrumentation，入 TitaniumAlloyEngineering，经 OrbitalEngineering、QuantumComputing、MatterTransmutationTheory、InterstellarCausalCoordination、PhaseFieldNavigation、PhaseFieldStabilizationTheory、MatterStateControlTheory，止于 TechnologicalSingularity。
- 跨时代回环前置：无（前置全部指向更早或同代科研；运行时另有 ValidateNoCycles 兜底，ResearchManager.cs:102）。
- 后向引用（老节点作新内容前置）正常；时代门前置均含上一代关键节点（如 Industrialization←MechanicalEngineering+Steelmaking）。

### 1.5 效果逐条核对结论

- 空效果：1 个（TechnologicalSingularity，effects: []）。
- 完全无操作（死效果，运行时语义证实）2 处：Woodworking 的 GlobalConstructionMultiplier 0.95（加法堆叠地板 Max(1,0.95)=1，ProgressionModifierManager.cs:184-188）；MatterStateControlTheory 的 DeconstructionReturnRate 0.12（取最大值，Industrialization 的 0.5 已先期生效）。
- 重复（多节点同一目标同一幅度且无差异化定位）：未发现完全相同的三元组；同建筑多次加成属设计堆叠，最集中者为 OrbitalResourceExtractionArray（4 条，合计 1.95x，见 BAL-8）。
- 幅度离群：IndustrialAgriculture 的 GlobalFoodProductionMultiplier 6.075（同类次大值 1.5）；InterstellarOccupationAdministration 的 TerritoryGranted 50000（次大值 Industrialization 1000）；OrbitalHabitation 的 DeconstructionReturnRate 0.9（次大值 0.5）。

## 2 内容拓展机会（# | 事项 | 证据 | 为什么值得 | 建议方向 | 优先级 | 工作量）

| # | 事项 | 证据(文件:行/资产id+数值) | 为什么值得 | 建议方向 | 优先级 | 工作量 |
|---|---|---|---|---|---|---|
| 1 | ResearchPower 产出链在 Spacer 内断档且无 Ultra 延续 | University.researchPowerGranted=250（Industrial）；DeepSpaceObservatory=220（Spacer，低于 University）；QuantumComputingArray=500；InterstellarTheoryNexus=900；Building/Ultra 目录为空 | RP 是科研速度唯一产能变量；Spacer 中段（Obs 220 至 Nexus 900 之间）无中间档；Ultra 0 建筑意味着终局科研速度被冻结在 Spacer 水平 | 在 Spacer 增设一档 400 至 600 的中位 RP 建筑（可由 Obs 升级线承担），Ultra 规划 1 至 2 个 RP 建筑与对应科研效果 | H | S |
| 2 | 11/47 个 Spacer 科研为叶子（无后继） | 叶子：AutonomousOrbitalMining、DeepSpaceBiosecurityTheory、FleetDamageControlTheory、InterstellarAutonomyCharterTheory、InterstellarCybersecurityTheory、InterstellarRouteControlTheory、OrbitalTextileFabrication、PlanetaryAtmosphereEngineering、PlanetaryGeologyTheory、PrecisionMedicine、SpaceWeatherForecastingTheory（均无任何节点引用其 guid） | 占 Spacer 23%；这些主题（地质、大气、医疗、舰队安全、网络安全）都是天然续篇入口，补后继可拉长 Spacer 尾盘而不新增平行系统 | 每条叶子延 1 至 2 层：如 PlanetaryGeologyTheory→区域地质勘探线（接 Sector）；SpaceWeatherForecastingTheory→舰队安全线；PrecisionMedicine→人口极限线 | H | M |
| 3 | Ultra 时代仅 1 条空效果科研，0 工坊 0 建筑 | data/content-closure-static.md:12-14（Ultra 1/1、0/0、0/0）；TechnologicalSingularity.asset effects:[] | 终局内容为零；路线图 docs/content/progression-roadmap.md:40 明确 Ultra 在 Milestone A-D 验收前冻结，故应作为 A-D 后的首个拓展方向 | 规划 Ultra 科研 6 至 10 条（以既有 Phase/Phantom/Quantum 资源为消耗），2 至 3 个建筑（含 RP 建筑），并给 TechnologicalSingularity 补 1 条真实全局效果 | M（受路线图冻结约束） | L |
| 4 | 老资源后期无生产向科研支撑 | 全树 ResourceProductionMultiplier（Type 3）仅 6 条：Steelmaking(Steel 1.15)、ChlorideTitaniumMetallurgy(TiConcentrate 1.15)、OrbitalAgroecology(Biomass 1.35)、OrbitalPropellantEngineering(RocketFuel 1.18)、PlanetaryAtmosphereEngineering(Biomass 1.12)、PhaseFieldStabilizationTheory(PhaseMaterial 1.15)；TL4 无任何 Steel/Coal/Copper/Chemical/Concrete 生产研究 | 锁定规则要求老资源后期仍有用；目前老资源只靠"作为研究/建筑消耗品"延续，生产效率冻结在 TL2 至 TL3 水平 | 在 Spacer 加 2 至 4 条"经典工业精炼"研究（Steel/Chemical/Concrete/Coal 的 Type 3 或 Type 1 加成），用现有效果类型即可，无代码改动 | M | S |
| 5 | 军事与战役类科研 TL4 前空白 | Type 10 仅 DeepSpaceFleet 1.25、InterstellarCombatLogistics 1.30 且运行时仅 Spacer 生效；Type 25/26/27（战役进度/补给/伤亡）全树 4 条全在 Spacer；Medieval 的 StandingArmy→Gunpowder 是断头 2 节点支线 | Fortification/StandingArmy/Gunpowder 线在 Medieval 就断头；战役系统若在早期时代已存在，则缺科研支撑 | 在 Medieval/Industrial 增 2 至 3 条战役向研究（用 Type 25/26/27，运行时已支持非战斗全局效果，无需 allowCombat 白名单改动） | M | S |
| 6 | 领地(TerritoryGranted)链断层 | 现有 7 条：StoneAgeSettlement 300、FeudalAdministration 25、Steelmaking 500、ConcreteEngineering 150、FactoryOrganization 250、Industrialization 1000、InterstellarOccupationAdministration 50000 | 1000→50000 之间 50 倍跳变无过渡；Spacer 前中期区域扩张无科研支撑 | 在 Spacer 前中期加 1 条领地研究（如 OrbitalSurvey 线，5,000 至 10,000 量级），并用 ExplorationPowerMultiplier（Type 29，现仅 2 条）联动 | M | S |
| 7 | 人口/幸福科研线 Spacer 中后期单薄且性价比失衡 | Spacer 人口向仅 BioregenerativeLifeSupport(增长1.25)、OrbitalHabitation(1.25)、DeepSpaceBiosecurityTheory(1.12)、PrecisionMedicine(1.5+幸福0.08)；InterstellarAutonomyCharterTheory 成本 1,564,000,000 仅换幸福+0.06 | 终局人口-幸福玩法（ HappinessBonus 全树合计仅 +0.26 ）没有承接；AutonomyCharter 是全树单位成本收益最差节点 | 将 AutonomyCharter 扩为"星际治理"小线（幸福+生产力组合），或直接给其追加人口相关真实效果 | M | S |
| 8 | 工坊系统与科研树仅一个连接点 | IndustrialWorkshop.asset Type 18（UnlockIndustrialWorkshop）value=1，成本 90,000，是全树唯一工坊解锁科研；Spacer 46 条工坊无对应解锁节点 | 工坊是核心系统但 Spacer 玩家无"科研解锁工坊内容"的仪式感；可复用现有 ResearchSystem 枚举模式 | 为 Spacer 工坊加 1 条解锁型科研（需在 ResearchEffectType/ResearchSystem 增枚举值，属小代码改动+编辑器迁移） | M | M |
| 9 | StoneAge/Medieval 断头叶子可作跨时代回填研究 | 叶子：FoodStorage、FoodPreservation、OrganizedWatch、VillageCrafts、CropRotation、AnimalFodder、CouncilGovernance（CouncilGovernance→OrganizedWatch 是 2 节点死支）；Medieval：GuildSystem、ScholasticInstitutions、Gunpowder | 玩家在 TL4 补修老代科研受 1/(代差+0.5) 减速惩罚（TL4 修 TL1 为 1/3.5），这些节点实际会被跳过；给它们加"时代缩放收益"或把其效果接进后期数值才有存在感 | 优先级低；可在平衡阶段为粮食/仓储类叶子补充与 FoodCapacityMultiplier 联动的真实收益 | L | S |

## 3 平衡风险（# | 事项 | 证据 | 对玩法的影响 | 建议验证方式 | 严重度）

| # | 事项 | 证据 | 对玩法的影响 | 建议验证方式 | 严重度 |
|---|---|---|---|---|---|
| 1 | Ultra 唯一科研无任何效果 | Ultra/TechnologicalSingularity.asset effects:[]；BaseCost 5.5e11；TechLevel 5；AdvancesTechLevel 1 | 玩家付出约等于 14.4 倍全 Spacer 总量的科研点后获得零收益，终局体验空转；违反"新科研必须有真实 effect"门 | 编辑器校验器补"AdvancesTechLevel 节点允许为空"白名单或补效果；运行时打点完成时刻玩家状态 | H |
| 2 | TL3→TL4 科研点总量跳升 1243x，产能仅增约 5x | 时代总和 30,771,000→38,243,700,000（表 1.1）；RP 产能 University 250→Spacer 顶配 Nexus 900（约 3.6x 单体），GlobalResearchMultiplier 3.19→3.47（1.09x） | 按 1.4 节假设推算 Spacer 纯科研时长约为 Industrial 的 200 倍以上；与已知冻结诊断 offline pacing acceptance 失败方向一致（data/economy-simulation 为冻结输出，仅作旁证） | 用离线模拟器只读跑"科研时间轴"对比两时代时长（不改策略搜索）；或 Unity 内打点完成时刻 | H |
| 3 | TechnologicalSingularity 单节点 5.5e11 = 全 Spacer 其余 47 条总和的 14.4x | 5.5e11 / 38.2437e9 = 14.38 | 终局门前出现"永远点不完"的墙；且门节点另受 0.667x 跨代减速 | 第 4 节情景计算复核；如需保留大数，应同步给 Ultra 配 RP 产能与效果 | H |
| 4 | Woodworking 存在运行时死效果 | Animal/Woodworking.asset Type 5 value=0.95；ProgressionModifierManager.cs:184-188 加法堆叠 Max(1,1+(0.95-1))=1 | 节点宣称的建造效果不存在，玩家感知收益缺失；属内容质量门"真实 effect"违例 | 运行时断言 Woodworking 完成前后 GlobalConstructionMultiplier 不变即证实；改 value 至 1.02+ 或删效果 | M |
| 5 | MatterStateControlTheory 的拆除返还 0.12 为死效果；OrbitalHabitation 0.9 过强 | Type 22 序列：StoneTools 0.1、UrbanHousing 0.25、Industrialization 0.5、MatterStateControlTheory 0.12（无效）、OrbitalHabitation 0.9；ProgressionModifierManager.cs:149-156 取最大值 | 0.12 是浪费的效果槽（4.5e9 成本的顶点科研有一条无效效果）；0.9 退款接近免费搬迁，可能架空建筑几何成本增长 | 读 BuildingManager 拆除退款代码确认 0.9 的实际退款率；若确为 90% 退款，压到 0.5 至 0.6 并验证重建套利 | M |
| 6 | 食物生产乘数与容量乘数严重失衡 | 全局食物生产乘法堆叠 1.2x1.331x1.2x1.25x1.5x6.075x1.38 约 30.1x（Agriculture/Calendar/AnimalFodder/MechanicalEngineering/IrrigationEngineering/IndustrialAgriculture/SyntheticFertilizers）；科研侧 FoodCapacityMultiplier 仅 FoodStorage 1.1 x FoodPreservation 1.1 = 1.21x；IndustrialAgriculture 6.075 为同类次大值 1.5 的 4 倍 | Industrial 后食物必然长期顶在容量上限，6.075 这类科研的边际价值趋近于零，玩家感知"点了没用"；也使同期 1.15M 成本性价比失真 | 记录 Industrial 完成后食物溢出率（产量/上限）；将 6.075 拆为 2 至 3 段或转部分为容量/人口收益 | M |
| 7 | Medieval 两节点成本脱离时代带 10 至 15 倍 | MechanicalEngineering 13,392、Steelmaking 18,144 vs 同代其余 130,000 至 270,000；两者又是时代门 Industrialization 的前置 | 工业入口被这两条"免费"研究拉平，Medieval 中后段（20 万级）反而成为相对高峰；成本曲线非几何单调 | 以同带中位数复核推导成本（目标时长 x 预期 RP）；确认是否漏一个数量级（133920/181440） | M |
| 8 | OrbitalResourceExtractionArray 被四条科研加成堆叠至 1.95x | AutonomousOrbitalMining 1.30、OrbitalTextileFabrication 1.25、OrbitalVacuumMetallurgy 1.28、PlanetaryGeologyTheory 1.12；加法堆叠 1+0.30+0.25+0.28+0.12=1.95；该建筑同时输出 8 种资源（asset resourceGenerationRates，含 Cloth 50/份） | 单一建筑吃掉 Spacer 采掘侧全部科研收益，且"纺织理论"无专属建筑可加成（仅有工坊 OrbitalTextileLooms），主题与数值集中度都偏高 | 拆分其中 1 至 2 条改加资源向（Type 3）或新建筑；运行时打点该建筑实际产出占比 | L |
| 9 | TL3 科研前向引用 Spacer 建筑 | Coking（TL3）加成 OrbitalCarbonizationComplex 1.10；MechanizedForestry（TL3）加成 OrbitalAgroecologyArray 1.15（Building guid f2a3b4c5d6e708192a4b5c6d7e8f9012、7a4b2c1d9e8f6071528394a5b6c7d8e9） | 玩家在工业时代买的科研有一部分收益要等进入 Spacer 才兑现，跨代平滑可能是有意为之，但会让 TL3 阶段性价比评估失真 | 与内容组确认意图；若非有意，改指向 Industrial 同类建筑（如 IndustrialCarbonizationRetort 已同时被 Coking 与 MechanizedForestry 加成） | L |
| 10 | 时代门节点恒定 0.667x 减速 | ResearchManager.cs:829-834：门节点 TechLevel=当前+1，速度 x1/1.5 | 五个时代门全部额外慢 50%；在门成本已最高的情况下叠加惩罚，放大 BAL-2/BAL-3 | 确认是否故意；若非，门节点按当前代速度计（传 current 而非 target） | L |

## 4 重点详析（数值级）

### 4.1 时代节奏核心矛盾：成本增长远超科研产能增长

假设（全部来自代码常量，无臆测）：速度 = RP x G x SpeedEffect；RP = 4 + Σ(researchPowerGranted x 数量)；G = 1+Σ(全局研究加成-1)（加法堆叠）。设各时代末玩家 RP 建筑数量为中性情景：TL0 末 2 个 KnowledgeCircle；TL1 末 10 ScribeHut；TL2 末 10 Library+4 Academy；TL3 末 8 University；TL4 末 20 Obs+10 QC+2 Nexus。

| 时代末 | RP | G | 有效速度（点/秒） | 时代总成本 | 该时代纯科研时长 |
|---|---|---|---|---|---|
| TL0 | 6 | 1.00 | 6 | 19,410 | 约 54 分钟 |
| TL1 | 54 | 1.05 | 57 | 162,900 | 约 48 分钟 |
| TL2 | 364 | 2.40 | 874 | 1,891,536 | 约 36 分钟 |
| TL3 | 2,004 | 3.19 | 6,393 | 30,771,000 | 约 80 分钟 |
| TL4 | 11,204 | 3.47 | 38,878 | 38,243,700,000 | 约 11.4 天 |

有效速度逐代倍率：9.5x、15.3x、7.3x、6.1x；成本倍率：8.4x、11.6x、16.3x、1243x。前三次过渡时代时长基本守恒（0.9x 至 2.3x），TL3→TL4 突变为约 200 倍。即 Spacer 入场是科研树唯一的断崖，与 offline pacing acceptance 长期失败方向吻合。注意这只是量级结论，绝对时长依赖建筑数量情景；结论对情景不敏感（数量 x10 也只能缩短 10 倍，仍是几十倍差距）。

### 4.2 TechnologicalSingularity（5.5e11，空效果）等待时长

速度 = RP x 3.4675 x 0.667（门节点跨代减速）。
- 中性情景（RP=11,204）：38,878 x 0.667 = 25,913 点/秒；5.5e11/25,913 = 2.12e7 秒 ≈ 246 天。
- 重度情景（50 Obs+30 QC+10 Nexus，RP=35,004）：80,968 点/秒；≈ 78.6 天。
- 极端情景（RP=126,004，100 Nexus 级）：≈ 21.9 天。
即使极端堆建筑，最后一击也要数周纯科研；而其全 Spacer 总量仅 38.24e9（重度情景约 5.5 天）。单节点 = 全时代 x14.4，且完成后无任何效果。建议：成本降 1 至 2 个数量级或配套 Ultra RP 产能 + 真实效果。

### 4.3 InterstellarNavigation（227.5e6，TL3→TL4 门）

TL3 末 RP=2,004、G=3.19、门减速 0.667：速度 4,264 点/秒；时长 = 227.5e6/4,264 = 53,354 秒 ≈ 14.8 小时（单节点）。University 翻倍至 16 座（RP=4,004）也需约 7.4 小时。对比 TL2 门 Industrialization（336,000，约 1 分钟）与 TL1 门 FeudalAdministration（130,000，约 38 分钟），门成本从 TL2 到 TL3 放大 677 倍，而产能仅放大 7.3 倍。这是玩家实际感知的"Spacer 之门"，比 4.1 的时代总量更尖锐。

### 4.4 食物乘数堆叠 vs 容量

生产侧（乘法）：Agriculture 1.2 x Calendar 1.331 x AnimalFodder 1.2 x MechanicalEngineering 1.25 x IrrigationEngineering 1.5 x IndustrialAgriculture 6.075 x SyntheticFertilizers 1.38 = 30.13x。
容量侧（科研乘法）：FoodStorage 1.1 x FoodPreservation 1.1 = 1.21x（其余容量靠 Granary 等建筑）。
Industrial 完成后生产 30x 于科研容量 1.21x，食物将长期满仓；IndustrialAgriculture（成本 1,150,000，全树第 6 贵）的主要实际收益只剩 PlantingField 单建筑 2.4x。建议 6.075 拆段或部分转容量/人口收益。

### 4.5 两处死效果的运行时证明

- Woodworking（成本 1,100）：Type 5 value 0.95 → AddGlobalConstructionMultiplier → AdditiveMultiplier(1, 0.95) = Max(1, 1+(0.95-1)) = Max(1, 0.95) = 1。全局建造乘数不变，效果无效。修复方向：value 提至 1.02 以上或删除该条。
- MatterStateControlTheory（成本 4.5e9）：Type 22 value 0.12 → SetDeconstructionReturnRate 取 Max(0.5, 0.12) = 0.5（Industrialization 的 0.5 在 TL3 已先于 TL4 生效）。该效果永远无效。修复方向：改 0.6 至 0.7 级或删；同时复核 OrbitalHabitation 0.9 是否过强。

## 5 证据清单

- 资产全集：D:\GitHub\Kingdom\Assets\Resources\Datas\Research\{Animal,StoneAge,Medieval,Industrial,Spacer,Ultra}\*.asset 共 129 个；本报告所有 Type/value/BaseCost/前置 guid 均逐一取自资产 YAML（字段行 15 至 47）。
- GUID 映射：各目录 .meta 的 guid 与文件名一一对照；资源 40 个来自 Assets\Resources\Datas\Resource\**\*.asset；建筑 69 个来自 Assets\Resources\Datas\Building\**\*.asset。
- 运行时语义：Assets/Resources/Script/Manager/ResearchManager.cs（:34 基数 4；:512-516、565-574 速度；:804-812 访问门；:829-834 跨代减速；:1155-1184 RP 公式）；Script/Manager/ProgressionModifierManager.cs（:35 拆除基线 0.05；:104-156 各类堆叠；:184-188 加法堆叠；:294-296 军事效果 Spacer 门）；Script/Manager/EraGoalEvaluator.cs（:177-191 时代门查找）。
- 数据结构：Assets/Resources/Script/Data/Research.cs、ResearchEffect.cs（29 效果类型 + 4 解锁类型）。
- RP 建筑数值：KnowledgeCircle/ScribeHut/Library/Academy/University/DeepSpaceObservatory/QuantumComputingArray/InterstellarTheoryNexus 的 researchPowerGranted 字段（分别为 1/5/25/40/250/220/500/900）。
- 闭合基线：data/content-closure-static.md（Industrial 81/81/50/50；Spacer 47/47/46/16；Ultra 1/1、0/0、0/0；资源 40）。
- 路线图约束：docs/content/progression-roadmap.md:40（Ultra/Archotech 在 Milestone A-D 与移动端验收前冻结）。
- 时代总量计算：各时代全部 129 条 BaseCost 逐条求和（见 1.1 表）；G 值由全树 13 条 Type 4 与 6 条 Type 5 等加法堆叠推得（TL4 末 GlobalResearch=3.4675）。
- 未读/未验证：BuildingManager 拆除退款的具体消费代码（BAL-5 的 0.9 退款率结论需读该代码确认）；GameState.HappinessMultiplier 基线；Workshop 资产效果明细（属工坊领域）；tools/NewEconomySimulator 仅确认存在，未运行。
