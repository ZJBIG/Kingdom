# 区域/军事/领土/战役只读审计
> 2026-09-12 · 只读静态扫描，未改动任何代码/资产/文档；未运行模拟器/Unity/测试

## 1 领域清单

### 1.1 十个 Sector 明细表（数据源：Assets/Resources/Datas/Sector/*.asset）

本星系 6 个（Sol，殖民制，无 enemyPower），远星 4 个（殖民制被 SectorValidator 禁止，战役制）。

| Sector | 星系/前置 | 占领成本（殖民=Food/秒+资源/秒×时长） | 一次性战利品 ResourceRewards | 占领持续流 OccupiedRates（/秒） | TerritoryReward | 战役进度乘数 |
|---|---|---|---|---|---|---|
| DawnRing 曙光环 | Sol；无前置 | Food 16.67 + RefinedFuel 166.67 ×600s（合计 Food 1万；RefinedFuel 10万） | 无（本星系禁止） | Electronics 0.04 | 500 | 无战役 |
| AzurePool 碧池星 | Sol；DawnRing | Food 1333.33 + RocketFuel 416.67 + Composite 250 + Aluminum 333.33 + Electronics 166.67 ×3600s（合计 Food 480万；RocketFuel 150万；Composite 90万；Aluminum 120万；Electronics 60万） | 无 | TitaniumAlloy 0.08；Composite 0.09；Electronics 0.07 | 12000 | 无战役 |
| Terminus 终焉星 | Sol；AzurePool | Food 2333.33 + RocketFuel 750 + Composite 583.33 + Aluminum 750 + Electronics 416.67 + TitaniumAlloy 333.33 ×7200s（合计 Food 1680万；RocketFuel 540万；Composite 420万；Aluminum 540万；Electronics 300万；TitaniumAlloy 240万） | 无 | Nickel 0.18；RocketFuel 0.12；Electronics 0.07 | 100000 | 无战役 |
| ShardCrown 碎冠带 | Sol；Terminus | Food 2500 ×10800s（合计 Food 2700万） | 无 | TitaniumConcentrate 0.18；NickelConcentrate 0.16；IronOre 0.22；BauxiteOre 0.14 | 200000 | 无战役 |
| HeliosCore 曜心 | Sol；ShardCrown | Food 4000 + Electronics 120 + Aluminum 80 ×21600s（合计 Food 8640万；Electronics 259.2万；Aluminum 172.8万） | 无 | Aluminum 0.24；Electronics 0.12 | 800000 | 无战役 |
| ThunderGate 雷门环 | Sol；ShardCrown | Food 3000 ×14400s（合计 Food 4320万） | 无 | RocketFuel 0.08；Nickel 0.16；TitaniumConcentrate 0.12 | 400000 | 无战役 |
| AlphaCentauri 半人马座前沿区 | AlphaCentauri；ShardCrown；enemyPower 900 | 战役：Food 2000/s + Biomass 800 + Machinery 800 + RocketFuel 1666.67 + Composite 666.67 + TitaniumAlloy 533.33 + PhantomAlloy 266.67 + PhantomWeave 200 + Nickel 266.67 + Lubricant 50 + Engine 266.67（全为每秒流） | TitaniumAlloy 50000；Composite 35000 | Composite 0.09；TitaniumAlloy 0.08；PhantomAlloy 0.012；PhantomWeave 0.008 | 700000 | 0.015 |
| ProximaB 比邻星前哨 | AlphaCentauri；AlphaCentauri；enemyPower 3000 | 战役：Food 3333.33/s + Biomass 1200 + Machinery 900 + RocketFuel 2666.67 + Composite 1200 + TitaniumAlloy 1000 + PhantomAlloy 1333.33 + PhantomWeave 1666.67 + PhaseMaterial 300 + Nickel 533.33 + Lubricant 100 + Engine 533.33 | Composite 50000；TitaniumAlloy 50000 | Composite 0.09；TitaniumAlloy 0.08；PhantomWeave 0.008；PhaseMaterial 0.0016 | 1500000 | 0.010 |
| TauCetiFoundry 鲸鱼座工业前哨 | TauCeti；ProximaB；enemyPower 4200 | 战役：Food 3666.67/s + Biomass 1800 + Machinery 1200 + RocketFuel 3333.33 + Electronics 1066.67 + TitaniumAlloy 800 + PhantomAlloy 1066.67 + PhantomWeave 1333.33 + Nickel 800 + Lubricant 150 + PhaseMaterial 533.33 + Engine 800 | Electronics 50000；TitaniumAlloy 35000；PhantomAlloy 30000；PhantomWeave 20000 | Composite 0.09；Electronics 0.07；PhantomAlloy 0.012；PhaseMaterial 0.0016 | 3000000 | 0.006 |
| SiriusResourceBelt 天狼资源带 | Sirius；TauCetiFoundry；enemyPower 6000 | 战役：Food 4666.67/s + Biomass 2600 + Machinery 1800 + RocketFuel 3666.67 + Electronics 1666.67 + TitaniumAlloy 1466.67 + PhantomAlloy 1200 + PhantomWeave 1000 + Nickel 1200 + Lubricant 200 + PhaseMaterial 800 + Engine 1200 | TitaniumAlloy 90000；PhantomWeave 80000；Nickel 60000；PhantomAlloy 50000；PhaseMaterial 40000 | TitaniumAlloy 0.08；PhantomAlloy 0.012；PhantomWeave 0.008；PhaseMaterial 0.0016 | 7000000 | 0.003 |

补注：全 10 区 repeatable=0（一次性）；远星前置链严格线性：ShardCrown → AlphaCentauri → ProximaB → TauCetiFoundry → SiriusResourceBelt；ThunderGate 与 HeliosCore 是无下游的叶子分支。SectorBuilding 全库只有 1 栋：EarthMoonLogisticsHub（绑 AzurePool，maxAmount 1，costGrowth 1.01，物流+320/s、舰队+80、防御+50，SpaceCost=0）。

### 1.2 战役补给需求 vs 玩家产能表（需求为资产每秒流；产能为建筑基础速率）

玩家唯一生产建筑及基础速率（resourceGenerationRates）：Food 720 与 Biomass 180 与 WoodLog 52（OrbitalAgroecologyArray）；RocketFuel 14（OrbitalCryogenicPropellantArray，另 ChemicalPlant 0.25）；Machinery 1、Engine 0.2、Composite 0.45（MachineFactory）；TitaniumAlloy 0.45（TitaniumMetallurgicalComplex）；PhantomAlloy 0.06、PhantomWeave 0.04（PhantomMaterialsFabricator）；PhaseMaterial 0.008（PhaseMaterialSynthesisArray）；Electronics 0.35（WireMill）；Nickel 0.9（NickelRefinery）；Lubricant 0.4（OilRefinery）。

表中"需建数"= SiriusResourceBelt 需求 ÷ 单建筑速率（乘数 1.0；建筑 costGrowth 1.2-1.26 决定 50 份以上实际不可达）。

| 资源 | AlphaCentauri 需求 | ProximaB | TauCetiFoundry | SiriusResourceBelt | 单建筑产能 | Sirius 需建数 | 供需比（Sirius，50 建筑产能） |
|---|---|---|---|---|---|---|---|
| Food | 2000 | 3333.33 | 3666.67 | 4666.67 | 720 | 6.5 | 0.09x（可行） |
| Biomass | 800 | 1200 | 1800 | 2600 | 180 | 14.4 | 0.29x（可行） |
| RocketFuel | 1666.67 | 2666.67 | 3333.33 | 3666.67 | 14 | 262 | 5.2x 缺口 |
| Machinery | 800 | 900 | 1200 | 1800 | 1 | 1800 | 36x 缺口 |
| Composite | 666.67 | 1200 | — | — | 0.45 | —（Alpha 1481） | — |
| TitaniumAlloy | 533.33 | 1000 | 800 | 1466.67 | 0.45 | 3259 | 65x 缺口 |
| Electronics | — | — | 1066.67 | 1666.67 | 0.35 | 4762 | 79x 缺口 |
| PhantomAlloy | 266.67 | 1333.33 | 1066.67 | 1200 | 0.06 | 20000 | 667x 缺口 |
| PhantomWeave | 200 | 1666.67 | 1333.33 | 1000 | 0.04 | 25000 | 833x 缺口 |
| PhaseMaterial | — | 300 | 533.33 | 800 | 0.008 | 100000 | 3333x 缺口 |
| Nickel | 266.67 | 533.33 | 800 | 1200 | 0.9 | 1333 | 27x 缺口 |
| Lubricant | 50 | 100 | 150 | 200 | 0.4 | 500 | 8x 缺口 |
| Engine | 266.67 | 533.33 | 800 | 1200 | 0.2 | 6000 | 120x 缺口 |

结论：Food/Biomass 可持续覆盖；其余加工品缺口 1-4 个数量级；PhaseMaterial 缺口 3 个数量级以上（其他代理已抽查 ProximaB 300/s vs 0.008/s，本表补全四场并复核一致）。唯一能显著降低消耗的科研 InterstellarSupplyChainTheory 仅 ×0.88（CampaignSupplyCostMultiplier 乘性），不改变结论。

### 1.3 战役时长与总投入（重点详析 4.1 有完整投入产出表）

CampaignManager.CalculateProgressRate 上限 = (2/60)/秒（ratio→∞ 渐近）；ratio=2 时 0.25/60/秒；ratio<0.7 零推进且必产生伤亡。有效乘数 = 资产乘数 × 研究加性乘数（1 + 1.08 + 1.12 + 1.15 = 4.35，来自 DeepSpaceNavigationReliability/InterstellarCausalCoordination/InterstellarLogisticsDoctrine）。SectorValidator 上限乘数 1/60，注释"极限速度约 multiplier/30，远星战役至少半小时"。

| 战役 | 乘数 | 时长@ratio2（无研究加成） | 时长@ratio2（研究 4.35x） | 时长@ratio5（研究 4.35x） |
|---|---|---|---|---|
| AlphaCentauri | 0.015 | 16000s（4.4h） | 3678s（1.0h） | 1268s（21min） |
| ProximaB | 0.010 | 24000s（6.7h） | 5517s（1.5h） | 1902s（32min） |
| TauCetiFoundry | 0.006 | 40000s（11.1h） | 9195s（2.6h） | 3170s（53min） |
| SiriusResourceBelt | 0.003 | 80000s（22.2h） | 18391s（5.1h） | 6340s（1.8h） |

### 1.4 军事数值模型（CampaignManager.cs / MilitaryState.cs / BuildingManager.cs）

- 有效战力 = (Attack + Fleet) × clamp01(Manpower/(Attack+Fleet)) × FleetReadiness × SupplySatisfaction × PowerSatisfaction × LogisticsSatisfaction × MilitaryMultiplier。
- MilitaryMultiplier 研究加性：DeepSpaceFleet +1.25、DeepSpaceShipbuilding +1.15、InterstellarCombatLogistics +1.30 → 合计 4.70。
- 军事建筑（全 Spacer，平面数值，无升级链）：LaunchCenter 攻40/防40/兵20；Shipyard 攻220/舰队150/防220/兵180（spaceCost 520，维护：Food 4/s、RefinedFuel 0.15、Lubricant 0.08、TitaniumAlloy 0.12、Composite 0.04、Machinery 0.18、Engine 0.12、Rubber 0.10、电力70、物流30、生产力700）；OrbitalStation 攻40/舰队150/防100/兵100（Food 容量 30000）；OrbitalHabitatMegastructure 防120/兵300（spaceCost 650，人口+3000）；DeepSpaceObservatory 攻120/舰队60/防160；DeepSpaceRelay 舰队100。
- 单 Shipyard 有效战力 ≈ 370×0.486×4.70 ≈ 845（兵员受限）；配 OrbitalHabitatMegastructure 补兵员后 ≈ 1739/座。
- 打满四场战役所需舰队：AlphaCentauri 约 1-2 座 Shipyard；ProximaB 约 2-3；TauCetiFoundry 约 3；SiriusResourceBelt 约 4-5（配 habitat）。
- 伤亡：ratio<0.7 时 1/60/秒；0.7-1 时 (1-ratio)/0.3/60；≥1 时 0。维修单价（硬编码 SectorManager.cs:1767-1790）：每点伤亡 TitaniumAlloy×2 + Composite×1 + PhantomWeave×1 + RocketFuel×0.5，乘 FleetRepairCostMultiplier（FleetDamageControlTheory ×0.90）。整场战役伤亡至多数百点，维修物料数百至数千单位，与补给千万级相比可忽略。
- 三个满足度中 PowerSatisfaction/LogisticsSatisfaction 由 BuildingManager.PrepareFlowSatisfaction 实算并传入战力公式；MilitaryState.SupplySatisfaction 无任何运行时生产方（默认恒 1.0，仅测试写入）——是已预留但未接线的系统。
- 停滞衰减：远星非活动战役进度按 0.02/60 每秒衰减（100%→0 约 50 分钟）；本星系殖民进度 0.005/60（约 3.3 小时衰减完）。

### 1.5 领土系统（TerritoryState.cs / Research 效果 Type 8）

- 初始 500；科研 TerritoryGranted 累计 52225：StoneAgeSettlement 300、FeudalAdministration 25、Steelmaking 500、ConcreteEngineering 150、FactoryOrganization 250、Industrialization 1000、InterstellarOccupationAdministration 50000。
- Sector 奖励合计 13712500；全域理论上限 13765225。
- 后期建筑 spaceCost 360-760（Shipyard 520、OrbitalStation 760、Habitat 650、CryoPropellant 420、Agroecology 520、PhaseSynthesis 520、PhantomFab 360）；一座满配远征基地（舰队+轨道工业约 100-200 栋）约 6-15 万领土，仅占上限 0.4%-1.1%。
- SectorValidator 强制（Validation/SectorValidator.cs）：远星 TerritoryReward ≥100000；战役乘数 ∈(0, 1/60]；战役必须持续消耗 TitaniumAlloy/Composite/PhantomAlloy/PhantomWeave/PhaseMaterial 之一；本星系 ResourceRewards 必须为空；远星至少 1 条资源奖励且 >0；前置无环且可达本星系。奖励只有">0"下限，无数量级/投入比守卫。
- 占领流合规复核：最大单流 HeliosCore Aluminum 0.24/s ≈ 0.2 座铝冶炼厂（1.2/s）；PhantomAlloy 0.012/s ≈ 0.2 座幻影制造厂（0.06/s）；PhaseMaterial 0.0016/s ≈ 0.2 座合成阵列（0.008/s）。全部为玩家产能的小零头，符合"不替代玩家自建高级生产"；研究 OccupiedResourceProductionMultiplier 加性 1+1.15+1.20=3.35 后仍可忽略。

## 2 内容拓展机会

| # | 事项 | 证据（文件：行/资产+数值） | 为什么值得 | 建议方向 | 优先级 | 工作量 |
|---|---|---|---|---|---|---|
| E1 | 战役补给率整体重定标，建立"需求≈可持续产能 1-3 倍"的校准线 | Sector/*.asset 战役流 vs 各生产建筑速率（1.2 表：Machinery 36x、TitaniumAlloy 65x、PhantomAlloy 667x、PhaseMaterial 3333x 缺口） | 战役是 Spacer 主玩法；当前数值使四场战役在数学上不可持续支付，重定标是解锁整条远星内容链的前提 | 按各资源"现实可建建筑数（20-50 座）× 单产"反推每秒流；保留持续消耗与高级材料构成以符合锁定规则 | H | M |
| E2 | 战役奖励重定标 + 校验器加"奖励/投入比"守卫 | SectorValidator.cs:303-318 只查 >0；AlphaCentauri 奖励 85000 vs 投入 2841万-1.24亿；Sirius 320000 vs 3.96亿-17.2亿 | 锁定规则要求后期战役奖励"大到值得持续投入"；当前奖励为投入的 0.02%-0.3%，为万亿分之一量级失衡的另一面 | 远星奖励提到与重定标后总投入同量级（百万级资源或领土+稀有料组合）；校验器加下限（如 sum(ResourceRewards) ≥ 最短战役时长×补给流合计×系数） | H | S |
| E3 | 星区建筑线扩充 | 全库仅 EarthMoonLogisticsHub 1 栋（maxAmount 1）；SectorBuilding.cs 支持任意 maxAmount 与星区绑定 | 10 个星区只有 1 栋专属建筑，占领后的"经营感"缺失；架构已就绪（SpaceCost=0、星区绑定、上限） | 每星区 1-2 栋：ShardCrown 矿机（强化占领矿流）、ThunderGate 燃料库（战役 RocketFuel 消耗系数下降）、HeliosCore 能量阵列、远星补给站（降 FleetRepairCostMultiplier）等 | H | M |
| E4 | 远星分支航线，激活 ThunderGate/HeliosCore 叶子分支 | 前置链严格线性（ShardCrown→AlphaCentauri→ProximaB→TauCeti→Sirius）；ThunderGate/HeliosCore 无下游 | 单线使远星玩法 4 连打完即终结；两条本星系叶子分支天然可作第二/第三星际入口（如 ThunderGate→新航线、HeliosCore→高能航线） | 新增 2-4 个远星区，前置指向 ThunderGate 或 HeliosCore，敌方强度 8000-20000，配 Story 15-17 章 | M | M |
| E5 | repeatable 赏金战役（终局循环） | SectorDefinition.repeatable 字段存在但 10 区全为 0；SectorManager.TryOccupy 已支持 Repeatable 重打（:516） | Sirius 之后、Ultra 解冻之前远星无任何可重复目标；repeatable=true + 轮换战利品是零新系统的终局循环 | 定义 1-2 个 repeatable 远星区：重打不重复给 Territory，只给资源/稀有料，成本随 VisitCount 增长 | M | M |
| E6 | 军事建筑梯队与战损消耗深化 | 军事建筑仅 7 栋且数值平面（Shipyard 220/150/220/180 无 upgradeTo）；战损维修经济占比可忽略（1.4 节） | 舰队成长无纵向内容；"战损"作为材料黑洞存在感低 | Shipyard→旗舰厂升级链；攻击平台（降伤亡率）；把维修配方从 SectorManager.cs:1767 硬编码迁到资产并扩容 | M | M |
| E7 | SupplySatisfaction 接线：前线补给链 | MilitaryState.SupplySatisfaction 无运行时生产方（恒 1.0）；战力公式已乘该系数（CampaignManager.cs:66） | 已预留的玩法钩子：物流覆盖不足应削弱远征战力，为 E4 的长航线提供深度 | 由 DeepSpaceRelay/补给站数量与目标星区距离计算 satisfaction∈(0,1]；UI 已有字段展示 | M | M |
| E8 | 占领流战略化与稀有流多样性 | 全部占领流 0.0016-0.24/s，仅 5 种高级料+矿物；加性乘数研究仅 2 条（AutonomousOrbitalMining、InterstellarOccupationAdministration） | 占领奖励感弱（Aluminum 0.24/s vs 8640万 Food 殖民投入）；可在不违反锁定规则前提下强化"有限战略资源流"定位 | 新增"远星科技样本"类独占流（仅占领可得、不可自建生产的新稀缺资源），或把 PhaseMaterial 占领流提到 0.004-0.006/s 作为唯一来源增益 | L | S |
| E9 | Territory 后期重定位 | 奖励上限 700 万 vs 全域用量约 6-15 万（1.5 节） | 领土在中期后失去约束意义；奖励数值随意膨胀 | 方向 A：抬升后期建筑 spaceCost 曲线；方向 B：领土换算为其他约束（如星区建筑槽位）；需模拟验证后再动 | L | M |
| E10 | 星区剧情/事件钩子 | Story 19 章中 14-17 章为星区/星际战争主题（FrontierSectors_14、WarBetweenStars_15、BeyondTheSky_16、TheOldBoundary_17）；SectorState.VisitCount 已存档但无消费方 | VisitCount、repeatable 与剧情章节可组成"首次占领叙事+重打事件"组合 | 按 VisitCount 触发一次性星区事件（即 Story 已有机制），远星区配 1-2 个抉择事件 | L | S |

## 3 平衡风险

| # | 事项 | 证据 | 对玩法的影响 | 建议验证方式 | 严重度 |
|---|---|---|---|---|---|
| B1 | 战役补给需求超出可持续产能 1-4 个数量级 | 1.2 表：Sirius 需建 PhaseMaterialSynthesisArray 10 万座（costGrowth 1.26）、Engine 6000 座（MachineFactory costGrowth 1.2）；RocketFuel 262 座（1.24） | 四场远星战役在稳态生产下不可支付；纯囤积路径 PhaseMaterial 需约 5 年（64M ÷ 0.4/s@50 座）、Engine 111 天、PhantomWeave 463 天，玩法死墙 | 用现有parity模拟器跑"Sirius 战役 steady-state 可行性"，输出各资源需求/产能比；无 Unity 时以静态算式复核 | H |
| B2 | 战役奖励为投入的 0.02%-0.3%，违反"后期奖励必须够大"锁定规则 | 4.1 投入产出表：AlphaCentauri 奖励 85000 资源 vs 投入 2841万-1.24亿；Sirius 奖励 320000 vs 3.96亿-17.2亿 | 玩家理性策略是完全跳过远星战役（无正收益），Spacer 终局内容作废 | 完成一次战役的账面投入/奖励审计（含 Territory 折算）；核对奖励 ≥ 投入的某个设计比例 | H |
| B3 | PhaseMaterial 战役流与产能差 3.75-10 万倍 | ProximaB 300/s、TauCeti 533.33/s、Sirius 800/s vs PhaseMaterialSynthesisArray 0.008/s | 校验器强制战役消耗高级料（SectorValidator.cs:130-134），但 PhaseMaterial 恰是全场最不可行的流；三场远星战役被单一资源锁死 | 定义审计脚本：对每个 campaignResourceRatesPerSecond 项输出 需求÷单建筑产能 并断言 ≤ 设定阈值（如 5） | H |
| B4 | Food 缓冲极薄：断供即停摆且已付费进度会被衰减抹除 | 战役 Food 2000-4666.67/s；OrbitalStation 容量 30000/座（缓冲 6.4-15s）；非活动衰减 0.02/60 每秒（SectorManager.cs:1089，100% 约 50 分钟清零）；舰队+轨道工业生产力 1.5-2.5 万 → 人口 0.75-1.25 万 → 人口吃 Food 6000-10000/s（0.8/人） | 电力/物流/资源任一闪断超过几十秒即 InsufficientCampaignSupply 停摆；停摆 50 分钟进度归零，之前数小时补给白付 | PlayMode：战役中人为断电/断料 60s，观察停摆与衰减路径；核对衰减是否有宽限 | M |
| B5 | 战役奖励无数量级下限守卫（校验器缺口） | SectorValidator.cs:303-318 仅要求 >0；其余守卫（领土≥10万、乘数≤1/60、高级料存在）齐全 | 未来新增星区可合法写出"领土 10 万+战利品 1 个Composite"这类定义；与 B2 同根 | 在 SectorValidator 增加：sum(ResourceRewards) 与 campaign 总投入（乘数×最短时长×流合计）的比例断言 | M |
| B6 | SectorBuilding 存档路径测试红（行为级回归） | TestResults/Latest-Test-Errors.txt:23-30（2026-09-11）；SectorBuildingTests.cs:223 SaveNow 返回 False；660 例中 2 失败 | 唯一星区建筑（EarthMoonLogisticsHub）的"占领校验+不占领土+存档回退"链路当前不被证明；E3 扩建筑线前必须先修复 | 修复后重跑 EditMode 全量，确认 Latest-Test-Errors 清零 | M |
| B7 | 敌方强度曲线过平，军事成长无压力 | enemyPower 900→3000→4200→6000；研究乘数 4.70 后 1-5 座 Shipyard 即满 ratio；Shipyard costGrowth 1.24 建造极快 | 军事数值层近似无门槛：真正门槛全在补给（B1）；enemyPower 对玩家几乎不可感知 | 记录每场战役 ratio 与所需建筑数；若目标是有感军事建设，重排 enemyPower 至 1e3-1e5 | L |
| B8 | SiriusResourceBelt 叙事与前置不一致 | 资产 Description"穿过比邻星前哨后"但前置是 TauCetiFoundry（2026-09-10 审计同记录）；数据链为 ProximaB→TauCeti→Sirius | 低危文案/引导混乱；玩家按描述找"比邻星后下一站"会漏掉 TauCeti | 改文案或插入 ProximaB→Sirius 前置；随 E4 分支化一并处理 | L |

## 4 重点详析

### 4.1 战役投入产出表（核心失衡）

口径：总投入 = 每秒流 × 时长；时长取 ratio=2 的推进速率 0.25/60 每秒，乘有效乘数（资产乘数×研究加性 4.35）；无研究加成为成本上界，有研究加成为当前可达基准。均不含舰队建造与维修。

AlphaCentauri（乘数 0.015；16000s / 3678s）：
- 无研究加成总投入：Food 3200万；RocketFuel 2666.7万；Biomass 1280万；Machinery 1280万；Composite 1066.7万；TitaniumAlloy 853.3万；PhantomAlloy 426.7万；PhantomWeave 320万；Nickel 426.7万；Engine 426.7万；Lubricant 80万；合计约 1.24 亿单位。有研究加成合计约 2841 万。
- 收益：Territory +700000；TitaniumAlloy 50000；Composite 35000。资源收益/资源投入 ≈ 0.30%（有研究）/0.07%（无研究）。纯囤积耗时（50 座产能）：RocketFuel 10.6h；Machinery 71h；Engine 5.0 天；TitaniumAlloy 4.4 天；PhantomAlloy 16.5 天；PhantomWeave 18.5 天。

SiriusResourceBelt（乘数 0.003；80000s / 18391s）：
- 无研究加成总投入：Food 3.73 亿；RocketFuel 2.93 亿；Biomass 2.08 亿；Machinery 1.44 亿；Electronics 1.33 亿；TitaniumAlloy 1.17 亿；PhantomAlloy 9600 万；Nickel 9600 万；Engine 9600 万；PhantomWeave 8000 万；PhaseMaterial 6400 万；Lubricant 1600 万；合计约 17.2 亿单位。有研究加成合计约 3.96 亿。
- 收益：Territory +7000000；TitaniumAlloy 90000；PhantomWeave 80000；Nickel 60000；PhantomAlloy 50000；PhaseMaterial 40000；资源收益合计 320000，为投入的 0.02%-0.08%。纯囤积耗时：RocketFuel 4.9 天；Machinery 33 天；TitaniumAlloy 60 天；Engine 111 天；PhantomWeave 463 天；PhaseMaterial 约 5.1 年。
- 对照锁定规则"成功的后期战役奖励必须大到值得持续投入"：TerritoryReward 700 万本身达标（≥10 万守卫），但 Territory 后期已无约束力（1.5 节），实际有效收益只有 32 万资源单位，比投入低 3 个数量级。

### 4.2 PhaseMaterial：被校验器规则放大的死锁

校验器要求战役必须持续消耗五类高级料之一；三场远星战役都选了 PhaseMaterial，而其唯一生产建筑 PhaseMaterialSynthesisArray 基础速率 0.008/s（costGrowth 1.26）。ProximaB 300/s = 3.75 万座；Sirius 800/s = 10 万座；即便只算囤积，6400 万单位需 0.4/s（50 座）产能攒 5.1 年。相对地，同为"高级料"的 TitaniumAlloy（0.45/s）缺口只有 65x。结论：PhaseMaterial 应从战役补给流中移除或降到 0.05-0.2/s 量级，由 PhantomAlloy/PhantomWeave/TitaniumAlloy 承担"高级料"合规角色；或为其开辟第二产线（当前唯一来源是三输入合成，与 PhantomFabricator 形成 0.11/0.09 → 0.008 的 25 倍质量递减链，本身偏紧）。

### 4.3 军事层"无门槛"与补给层"死墙"的错配

打满 Sirius 只需 4-5 座 Shipyard + 3-4 座 Habitat（军事乘数 4.70 后有效战力约 8000-8700，敌方 6000）；Shipyard 第 5 座造价约 base×1.24^4 ≈ 2.36 倍，约 Steel 2.1 万、Composite 6100，任何进入 Spacer 的玩家一小时内可达成。同一时刻其战役补给需求是产能的 1-4 万倍。这意味着现有内容里"战争准备"（舰队）与"战争进行"（补给）的成本相差 3 个数量级，玩家体验将是：轻松建军 → 永远打不动。重定标补给（E1）时建议同步抬 enemyPower（B7）到与舰队建设成本同尺度（如 5 万-50 万），否则补给修好后战役会变成纯点击结算。

### 4.4 Food 双重挤压

战役 Food 流 2000-4666.67/s 只是 Food 危机的一半；舰队+轨道工业的生产力占用（Shipyard 700/座、Habitat 900/座、OrbitalStation 1260/座、Agroecology 720/座）在 1.5-2.5 万区间，对应人口 0.75-1.25 万、人口口粮 6000-10000/s（0.8/人/秒）。两者叠加要求 12-28 座 AgroecologyArray（每座再吃 720 生产力、335 电力），是一个自洽但极重的闭环；而 Food 容量仅 30000/OrbitalStation，任何一次产能闪断在 7-15 秒内即触发 InsufficientCampaignSupply。这是"Food 唯一有上限"锁定规则与"战役长期消耗 Food"锁定规则相遇的必然张力点，重定标时建议把战役 Food 流压到 500-1500/s 区间，并给容量侧留出至少 60 秒缓冲（需 ≥9 万容量或降低流）。

### 4.5 领土：科研断层实际无感，真正的问题是后期失重

Industrialization +1000 → InterstellarOccupationAdministration +50000 的 50 倍断层在纸面惊人，但两节点之间玩家已依次拿到 Terminus 10 万、ShardCrown 20 万、ThunderGate 40 万、HeliosCore 80 万（共 150 万），领土早已不是瓶颈；断层之前（AzurePool 12000 之前）的 2725 科研领土 + DawnRing 500 才是真实约束段。反向问题更值得规划：全域领土上限 1376 万 vs 满配用量 6-15 万，TerritoryReward 数值（尤其 Sirius 700 万）在玩法上不可感知。建议把"领土"从纯累积量改造为有消耗的量（E9），或把星区建筑的槽位与领土挂钩（注意 SectorBuilding.SpaceCost=0 是现行校验规则，改动需先改规则文档）。

## 5 证据清单

代码（只读引用）：
- Assets/Resources/Script/Data/SectorDefinition.cs（字段与 Clamp01 乘数）
- Assets/Resources/Script/Data/SectorBuilding.cs:15-31（星区绑定/maxAmount/SpaceCost=0/禁升级链）
- Assets/Resources/Script/Data/ResearchEffect.cs:4-36（效果类型枚举）
- Assets/Resources/Script/Manager/SectorManager.cs（解锁/战役/殖民/维修/占领流/衰减：:1089 远星衰减 0.02/60、:1164 本星系 0.005/60、:1767-1790 硬编码维修配方、:300-308 补给乘数）
- Assets/Resources/Script/Manager/CampaignManager.cs（进度/伤亡/战力公式：:98-119、:48-69、:88-96）
- Assets/Resources/Script/Manager/BuildingManager.cs:620-640（星区建筑不占领土）、:1239-1337（电力/物流满足度）、:148-160（生产力=人口×2）
- Assets/Resources/Script/Manager/ProgressionModifierManager.cs:279-281、:319-329（TerritoryGranted 与战役乘数接线）
- Assets/Resources/Script/Validation/SectorValidator.cs:6-10、:103-147、:290-318（守卫与缺口）
- Assets/Resources/Script/Runtime/MilitaryState.cs、CampaignState.cs、TerritoryState.cs:6（InitialTotal 500）、SectorState.cs
- Assets/Resources/Script/Manager/ProgressionModifierManager.cs:16-34（SupplySatisfaction 等无运行时生产方的佐证：全库 SetSupplySatisfaction/SetPowerSatisfaction 仅 GameState 转发与测试调用）

资产（只读引用）：
- Assets/Resources/Datas/Sector/*.asset（10 个，全数值见 1.1 表）
- 产能建筑：Spacer/OrbitalAgroecologyArray（Food 720、Biomass 180）、Spacer/OrbitalCryogenicPropellantArray（RocketFuel 14）、Industrial/MachineFactory（Machinery 1、Engine 0.2、Composite 0.45）、Industrial/TitaniumMetallurgicalComplex（TitaniumAlloy 0.45）、Spacer/PhantomMaterialsFabricator（0.06/0.04）、Spacer/PhaseMaterialSynthesisArray（PhaseMaterial 0.008）、Industrial/WireMill（Electronics 0.35）、Industrial/NickelRefinery（Nickel 0.9）、Industrial/OilRefinery（Lubricant 0.4）、Industrial/AluminumSmelter（Aluminum 1.2）
- 军事建筑：Spacer/Shipyard、OrbitalStation、OrbitalHabitatMegastructure、LaunchCenter、DeepSpaceObservatory、DeepSpaceRelay、Spacer/EarthMoonLogisticsHub（唯一 SectorBuilding，绑 AzurePool）
- 科研：Spacer/DeepSpaceFleet（军力+1.25）、DeepSpaceShipbuilding（+1.15）、InterstellarCombatLogistics（+1.30、维修×0.85）、DeepSpaceNavigationReliability/InterstellarCausalCoordination/InterstellarLogisticsDoctrine（战役进度 1.08/1.12/1.15）、InterstellarSupplyChainTheory（补给成本×0.88）、FleetDamageControlTheory（伤亡×0.90）、InterstellarOccupationAdministration（领土+50000、占领流+1.20）、Industrialization（领土+1000）、AutonomousOrbitalMining（占领流+1.15）
- 剧情：Assets/Resources/Datas/Story/（19 章；FrontierSectors_14、WarBetweenStars_15、BeyondTheSky_16、TheOldBoundary_17）

文档与既有证据（只读引用）：
- docs/content/alien-war-first-version.md（战役设计意图与边界）
- docs/content/home-system-exploration-rewards.md（本星系无资源奖励规则）
- docs/content/progression-roadmap.md:40（Ultra 冻结至 Milestone A-D 验收完成）
- docs/audits/2026-09-10-full-readonly-refactor-scan.md（维修配方硬编码、Sirius 叙事-前置不一致、SectorValidator.cs:278 死行等）
- TestResults/Latest-Test-Errors.txt:23-30（SectorBuildingTests 存档路径失败，2026-09-11）
- 交叉引用同批审计：02-resource-chains.md（PhaseMaterial 产能 0.008/s、RocketFuel 14/s、Food 容量 30000 的独立抽查结论与本报告一致）

未执行真实 Unity 编译；未运行模拟器与测试；data/economy-simulation 目录为空故无在档节奏数字可引用，所有数值均来自定义资产与运行时代码静态读取。
