# 建筑与生产只读审计

> 2026-09-12 · 只读静态扫描，未改动任何代码/资产/文档；未运行模拟器/Unity/测试。数值全部取自资产原文与运行时代码。

## 1 领域清单

### 1.1 数量与角色分布

- 建筑总数 66：Animal(TL0) 9、StoneAge(TL1) 10、Medieval(TL2) 5、Industrial(TL3) 26、Spacer(TL4) 16、Ultra(TL5) 0（Ultra 目录为空；progression-roadmap.md 第 40 行附近已将 Ultra/Archotech 冻结至 Milestone A-D 验收完成，属有意决策，仅作背景）。
- 生产力池确认全部来自人口：66 建筑的 productivityGranted 全为 0；PopulationState.cs:9 定义 ProductivityGrantedPerPerson=2/s、FoodConsumptionPerPerson=0.8/s（第 8 行）、基础增长 1/60 每秒。
- 效率模型（BuildingManager.cs:1394-1407）：efficiency = GlobalEfficiencyFactor × 资源满足率 × 食物幸福约束 × 电力满足率 × 物流满足率，clamp 0-1；只耗电不吃粮的建筑豁免食物约束（IsFoodConstraintRequired，BuildingManager.cs:1373-1379）。电力/物流是全局池（GameState.PowerSatisfaction/LogisticsSatisfaction），不是局部电网。
- 成本模型：unitCost(k)=BaseCost×costGrowth^k，批量用 GeometricSeriesCost；CostGrowth 属性在 NaN/Inf/<1 时静默回退 1.15（Building.cs:142-143），只有升级链成员会被 ValidateChainEconomy 抛错（BuildingManager.cs:240-258），非链建筑无资产级校验。
- 拆除退款 = 建造成本 × 建造效率 × DeconstructionReturnRate，默认 0.05（ProgressionModifierManager.cs:35）。
- 升级链（upgradeTo）覆盖良好：ClayPit/CeramicKiln→AdvancedCeramicsPlant；Farm/IrrigationWorks→PlantingField；FiberGatheringCamp→PlantingField；Lumberyard→MechanizedLumberyard；Quarry/StoneCuttingWorkshop→IndustrialStoneworks；Smokehouse→Granary（终止）；KnowledgeCircle→ScribeHut→Library→Academy→University→DeepSpaceObservatory→QuantumComputingArray→InterstellarTheoryNexus；WoodHouse→StoneHouse→TownHouse→IndustrialHabitationComplex→OrbitalHabitatMegastructure；CoalMine→MechanizedCoalMine；MetalMine→RareMetalMine；MetalSmelter/SteelForge→IndustrialMetalSmelter；CharcoalKiln/CokeOven→IndustrialCarbonizationRetort→OrbitalCarbonizationComplex；WeavingWorkshop→MechanizedTextileMill→OrbitalResourceExtractionArray；RareMetalMine/TitaniumMetallurgicalComplex→OrbitalResourceExtractionArray；OilDerrick→IndustrialOilExtractionComplex（终止）；OilRefinery→IntegratedPetrochemicalComplex（终止）；SteamPlant→CentralPowerStation（终止）；Caravanserai→RailHub（终止）；MechanizedLumberyard→OrbitalAgroecologyArray。
- upgradeTo 为空且无后继的关键单源建筑：MachineFactory（Machinery/Engine/Composite 唯一来源）、WireMill（CopperWire/Electronics 唯一来源）、BuildingMaterialsComplex（Glass/Concrete 唯一来源）、ChemicalPlant（Chemical 主源）、AluminumSmelter、NickelRefinery、CentralPowerStation、RailHub、LaunchCenter、OrbitalStation、Shipyard、DeepSpaceRelay、OrbitalSolarArray、OrbitalCryogenicPropellantArray、PhantomMaterialsFabricator、PhaseMaterialSynthesisArray、EarthMoonLogisticsHub。

### 1.2 字段数值范围（66 建筑全体）

- costGrowth：1.01（EarthMoonLogisticsHub，maxAmount=1）至 1.30（InterstellarTheoryNexus）；排除奇观后区间 1.13-1.30。
- spaceCost：2（KnowledgeCircle/Smokehouse）至 1000（OrbitalResourceExtractionArray）；Spacer 一份全家桶约 8370 领土。
- productivityConsumption：0（住房/奇观）至 1680（OrbitalResourceExtractionArray）；Spacer 15 座耗生产力建筑各一份合计 11980/s。
- researchPowerGranted：1（KnowledgeCircle）至 900（InterstellarTheoryNexus）。
- foodProductionRate：2（Granary）至 720（OrbitalAgroecologyArray）；foodCapacityGranted 200 至 30000（OrbitalStation）。
- powerProductionRate 仅 3 座：SteamPlant 120、CentralPowerStation 300、OrbitalSolarArray 360；powerConsumptionRate 5（CokeOven）至 585（OrbitalResourceExtractionArray）。
- populationCapacityGranted：5/12/40/240/3000（五个住房梯队）。
- 单建筑最大建造成本项：OrbitalAgroecologyArray 24000 Biomass；OrbitalResourceExtractionArray 21600 Concrete、14400 Machinery、14400 TitaniumAlloy；InterstellarTheoryNexus 14000 TitaniumAlloy、12000 Electronics、10000 PhantomAlloy。

### 1.3 costGrowth 全表（对照 balance-model 建议带：原料 1.12-1.14；加工 1.15-1.17；研究/基础设施 1.18-1.20；住房 1.18-1.22；奇观仅 1 座）

Animal：ClayPit 1.14；Farm 1.14；FiberGatheringCamp 1.14；KnowledgeCircle 1.20；Lumberyard 1.13；Quarry 1.14；Smokehouse 1.17；StoneCuttingWorkshop 1.15；WoodHouse 1.18（全部带内）。
StoneAge：CeramicKiln 1.16；CharcoalKiln 1.16；CoalMine 1.14；Granary 1.18；IrrigationWorks 1.18；MetalMine 1.15（原料带 +0.01）；MetalSmelter 1.17；ScribeHut 1.20；StoneHouse 1.18；WeavingWorkshop 1.16。
Medieval：Academy 1.22（研究带 +0.02）；Caravanserai 1.20；Library 1.22（+0.02）；SteelForge 1.20；TownHouse 1.20。
Industrial：AdvancedCeramicsPlant 1.22；AluminumSmelter 1.20；BuildingMaterialsComplex 1.20；CentralPowerStation 1.22；ChemicalPlant 1.20；CokeOven 1.18；IndustrialCarbonizationRetort 1.20；IndustrialHabitationComplex 1.22；IndustrialMetalSmelter 1.20；IndustrialOilExtractionComplex 1.20；IndustrialStoneworks 1.20；IntegratedPetrochemicalComplex 1.22；MachineFactory 1.20；MechanizedCoalMine 1.20（原料带 +0.06）；MechanizedLumberyard 1.20；MechanizedTextileMill 1.20；NickelRefinery 1.20；OilDerrick 1.15（原料带 +0.01）；OilRefinery 1.21；PlantingField 1.20；RailHub 1.20；RareMetalMine 1.18（原料带 +0.04）；SteamPlant 1.20；TitaniumMetallurgicalComplex 1.22；University 1.20；WireMill 1.20（加工类整体高于 1.15-1.17 带 +0.01~+0.05）。
Spacer：DeepSpaceObservatory 1.24；DeepSpaceRelay 1.24；EarthMoonLogisticsHub 1.01（maxAmount=1，奇观，增长惰性）；InterstellarTheoryNexus 1.30（研究带 +0.10）；LaunchCenter 1.20；OrbitalAgroecologyArray 1.24；OrbitalCarbonizationComplex 1.24；OrbitalCryogenicPropellantArray 1.24；OrbitalHabitatMegastructure 1.24；OrbitalResourceExtractionArray 1.24；OrbitalSolarArray 1.22；OrbitalStation 1.22；PhantomMaterialsFabricator 1.24；PhaseMaterialSynthesisArray 1.26；QuantumComputingArray 1.28（研究带 +0.08）；Shipyard 1.24。
结论：早期两时代基本带内；Industrial 一刀切 1.20 系统性高于类别带；Spacer 普遍 1.22-1.30，研究类最重（1.24/1.26/1.28/1.30）。除已知 ITN 1.30 与 EMLH 1.01 外，QuantumComputingArray 1.28 与 PhaseMaterialSynthesisArray 1.26 是最显著偏离者。

## 2 内容拓展机会

| # | 事项 | 证据 | 为什么值得 | 建议方向 | 优先级 | 工作量 |
|---|------|------|-----------|---------|--------|--------|
| E1 | Medieval 无食物生产建筑 | 全部 5 座 Medieval 建筑均无 foodProductionRate；上一档 IrrigationWorks 20 food/s（StoneAge），下一档 PlantingField 48（Industrial）；TownHouse 40 人口×0.8=32 food/s 需 1.6 座 IrrigationWorks 支撑 | Medieval 人口扩张完全压在上一时代 IrrigationWorks（growth 1.18）上，食物是中古时代事实瓶颈且无本时代玩法 | 增设 1 座 Medieval 农业建筑（如轮作庄园 35-45 food/s，成本用 StoneBrick/Cloth/Ceramic/铁） | H | S |
| E2 | MachineFactory/WireMill/BuildingMaterialsComplex 无 Spacer 后继 | 三者 upgradeTo 均为 {fileID: 0}；Spacer 需求侧 OREA 建造成本即 14400 Machinery+9000 Electronics+21600 Concrete，均衡态 Electronics 流量约 4.3-5/s 而 WireMill 单座仅 0.35/s | 八种关键中间品（Machinery/Engine/Composite/CopperWire/Electronics/Glass/Concrete 及 Chemical 主源）整个 Spacer 时代无升级路径，只有无限堆 Industrial 老建筑一条玩法 | 为三者各设计一座 Spacer 升级（建造与维护按规则使用 Phantom/Phase/钛合金等高级材料，产能约 3-5 倍） | H | M |
| E3 | PhaseMaterialSynthesisArray 0.008/s 疑似占位值 | PMSA 产 0.008 PhaseMaterial/s；OREA 建造需 1740、OrbitalStation 700、OrbitalHabitatMegastructure 500；单 PMSA 攒 1740 需 217500s 约 60 小时 | 相位材料是 Spacer 全部巨构的唯一瓶颈，当前速率下该链实际不可作为常规玩法推进 | 复核目标节奏后将产能提至 0.05-0.15/s 量级，或增设前置产能建筑；需离线模拟+运行时验证节奏 | H | S |
| E4 | Spacer 研究建筑缺独特价值（详见 4.4） | University 250 研究力/60 生产力 vs QCA 500/900、ITN 900/1600、DSO 220/620 | 研究力/生产力比 University 4.17 vs 0.35-0.56，Spacer 研究建筑可被老建筑完全平替，时代内容失去意义 | 给 DSO/QCA/ITN 挂 GlobalResearchMultiplier、每建筑 researchPower 倍率（工作坊已有该机制）或独占研究系统 | M | M |
| E5 | Medieval 建筑最少且角色单薄 | Medieval 仅 5 座：Library/Academy（研究）、TownHouse（住房）、SteelForge（金属）、Caravanserai（物流 25/s） | 中古是内容最薄的完整时代，无军事、无疆域、无食物角色 | 补 1-2 座（食物+E1、军事/疆域各一），成本复用铁/布/陶 | M | M |
| E6 | RailHub 升级链终止 | RailHub upgradeTo 空；物流生产 100/s；Spacer 侧 EarthMoonLogisticsHub 320/s（maxAmount=1）为独立奇观 | 铁路枢纽是 Industrial 物流核心却无 Spacer 后继；均衡态 Spacer 物流消耗（DSR 48+OS 50+OREA 96+OAA 48 等）远超 RailHub 单座 | 为 RailHub 增设 Spacer 物流后继或把 EMLH 之外补第二档可重复物流建筑 | M | S |
| E7 | Clay 在 StoneAge 后无消费者 | Clay 产者 ClayPit 1.5/s 与 AdvancedCeramicsPlant 3/s；流耗仅 CeramicKiln 1 Clay/s（StoneAge）；Industrial/Spacer 无一消费 | ACP 每秒 3 黏土是纯废输出，违背"老资源后期仍有用"的精神（Clay 直接死亡而非转化） | 给 Industrial 增加 Clay 消耗（如 ACP 自身消耗黏土产陶瓷的净配方）或移除 ACP 黏土盈余 | L | S |
| E8 | Granary 升级链终止+Industrial 无粮仓 | Smokehouse→Granary 后 upgradeTo 空；Industrial 时代 foodCapacity 仅 RailHub 2500；IHC 240 人口×0.8=192 food/s 时缓冲约 13 秒 | 食物是唯一有上限库存，Industrial 人口段无对应储粮内容 | 增设 Industrial 粮仓（Concrete/玻璃建造，5000-10000 容量）衔接 OrbitalStation 30000 | L | S |

## 3 平衡风险

| # | 事项 | 证据 | 对玩法的影响 | 建议验证方式 | 严重度 |
|---|------|------|-------------|-------------|--------|
| B1 | RareMetalMine productivityConsumption=12 疑似少写一个 0 | RareMetalMine.asset 第 22 行 12；同梯队 MechanizedCoalMine 130、MechanizedLumberyard 140、MechanizedTextileMill 120；其前继 MetalMine 10 | 工业矿石经济（铜锡铁铝土钛镍六矿流）在生产力轴近乎免费，10 座矿场只占 120 生产力，矿石链严重低定价 | 与设计意图核对后改至 100-140；离线模拟对比矿石链投产节奏 | H |
| B2 | TitaniumMetallurgicalComplex productivityConsumption=12 疑似异常 | TMC.asset 第 22 行 12；同级 NickelRefinery 95、powerConsumption 160 vs Nickel 90；TMC 是 OREA 之前唯一 TiAlloy 源 | 钛合金门槛建筑的运行成本失真；配合 B1 使 Spacer 门槛（OREA 需 14400 TiAlloy）比设计更廉价地通过 | 核对意图值（约 90-160）；模拟 OREA 解锁时间前后对比 | H |
| B3 | University 支配全部 Spacer 研究建筑 | University 250 research/s；60 生产力；10 电；成本约 5840 价值单位（100 Machinery+80 Chemical+100 Electronics+150 Glass 折算）；QCA 500/900/300 电/约 18.7 万价值；ITN 900/1600/500 电/约 293 万价值 | 理性玩家永远堆 University（wood/机械/电子/玻璃 inputs 终生可用），Spacer 三座研究建筑成一次性门槛物 | 工作坊每建筑 researchPower 倍率是否已覆盖三者需运行时核对；否则按 E4 重做效果 | H |
| B4 | QuantumComputingArray growth=1.28、PhaseMaterialSynthesisArray 1.26、Spacer 研究类 1.24-1.30 全超带 | 各 asset costGrowth 字段；balance-model 建议研究/基础设施 1.18-1.20 | 多副本策略被超重惩罚，与 B3 叠加后 Spacer 研究多副本唯一解是老建筑，新建筑一次购买后废弃 | 与 ITN 1.3 一并按类别带回归 1.18-1.22 或给出奇观化定位（限购） | M |
| B5 | SteelForge 回本约 15 分钟，超加工带 3.8-10 倍 | 成本 300 Wood+250 StoneBrick+100 Coal+100 Iron（约 1375 价值）；净产出 1 Steel/s−1 Iron−1 Coal 约 1.5-3.5 价值/s（Steel=6-8 折算）；Medieval 唯一钢源 | 中古工业化的钢门槛期被拉长；而 TownHouse/后续 Industrial 全要钢 | 按 4.1 的折算假设做模拟回本曲线；目标带回 90-240s 或上调产出至 1.5-2 Steel/s | M |
| B6 | CokeOven/ICR 绝对回本约 90/56 分钟（互相一致） | CokeOven 成本约 4050 价值、净 0.75/s（2700 价值/单位 Coke 流）；ICR 13350、净 4/s（2781） | 焦炭作为 CPS 燃料前置，投资回收极慢但两档内部一致；问题在 Coke 单价低导致燃料链回本失衡 | 以电力-焦炭折价重估；或提高 CokeOven 产率至 2-2.5/s | M |
| B7 | MechanizedLumberyard/MechanizedCoalMine 回本 22/33 分钟，超原料带（45-120s）11-40 倍 | ML 成本约 22250 价值净 17/s；MCM 21550 净 11/s | 原料档回本带完全按早期数值写死，Industrial 全档失真（MachineFactory 369s、BMC 869s 也超 1.5-4x） | 文档按时代重定回本带并新增 Editor 校验器（当前 ContentDependencyAnalyzer.cs 只查研究环与资源死锁，无成本/回本检查） | M |
| B8 | EarthMoonLogisticsHub growth=1.01 的潜在地雷 | EMLH asset costGrowth 1.01、maxAmount 1、sector 字段、320 物流/s | maxAmount=1 时惰性（奇观规则匹配）；若日后放开 maxAmount，即可以近平价成本无限复制 320 物流/s + 80 舰队 | 在 maxAmount 保持 1 的前提下加断言或注释；如放开需先改 growth | M |
| B9 | CharcoalKiln 被 CoalMine 同代支配 | CharcoalKiln 成本 200 Wood+150 Clay+80 StoneBrick、产 0.8 Coal/s、耗 2 Wood/s；CoalMine 成本 150 Wood+100 Stone、产 1.5 Coal/s、无输入 | 同时代同产物，性价比全面落后，仅剩"无煤矿点"的叙事价值（领土模型下无矿点差异） | 赋予差异化（如 CharcoalKiln 附带 Wood 消耗换 Concrete 时代前置）或下调其成本上移其定位 | L |
| B10 | AluminumSmelter 回本约 33 分钟且 productivityConsumption=12 偏低 | 成本约 8200 价值（500 Concrete+400 Steel+180 Machinery+120 Ceramic）；净约 4.2/s；同档 NickelRefinery 95 生产力 | 铝链投资回收慢、运行成本低；铝主要用于 OSA/OMS 维护与船坞，需求侧较薄 | 复核产率 1.2→1.8-2 或成本下调；并入 B7 的回本校验器 | L |

## 4 重点详析

### 4.1 首份回本估算（代表性建筑，第一座，满效率，无科技乘数）

资源折价假设（链深加权）：Wood/Stone/Clay/Biomass=1；Coal=1.5；矿石=1.5；StoneBrick=2.5；Ceramic=5；Cloth=5；Coke=2.5；Bronze=4；Steel=6；Glass=8；Concrete=5；CopperWire=8；Chemical=8；Electronics=25；Machinery=15；Composite=12；Aluminum=8；Nickel=6；TiConc=15；TiAlloy=45；PhantomAlloy=60；PhantomWeave=40；PhaseMaterial=250；食物按 1 food/s 支撑 1.25 人口=2.5 生产力/s 计。

| 建筑 | 成本(价值) | 净产出(价值/s) | 回本 | 文档带 | 判定 |
|------|-----------|---------------|------|--------|------|
| Farm(A0) | 50 | 8 food/s≈20 生产力/s | <60s | 原料 45-120s | 达标 |
| Lumberyard(A0) | 170 | 5 | 34s | 原料 45-120s | 略快 |
| Quarry(A0) | 120 | 2.4 | 50s | 原料 45-120s | 达标 |
| CeramicKiln(A1) | 100 | 3.8 | 26s | 加工 90-240s | 偏快 |
| MetalSmelter(A1) | 300 | 2.05 | 146s | 加工 90-240s | 达标（另需 1.25 座 MetalMine 配套，Sn/Fe 矿 0.8<1 不足单矿供养） |
| CoalMine(A1) | 250 | 2.25 | 111s | 原料 45-120s | 达标 |
| SteelForge(A2) | 1375 | 1.5-3.5 | 393-917s | 加工 90-240s | 超带 1.6-10x（B5） |
| CokeOven(A3) | 4050 | 0.75 | 5400s | 加工 90-240s | 超带（B6） |
| MachineFactory(A3) | 6500 | 17.6 | 369s | 加工 90-240s | 超 1.5-4x |
| WireMill(A3) | 6000 | 14.35 | 418s | 加工 90-240s | 超 1.7-4.6x |
| BuildingMaterialsComplex(A3) | 7600 | 8.75 | 869s | 加工 90-240s | 超 3.6-9.7x |
| MechanizedCoalMine(A3) | 21550 | 11 | 1959s | 原料 45-120s | 超 16-43x（B7） |
| MechanizedLumberyard(A3) | 22250 | 17 | 1309s | 原料 45-120s | 超 11-29x（B7） |
| AluminumSmelter(A3) | 8200 | 4.2 | 1952s | 加工 90-240s | 超 8-22x（B10） |
| TitaniumMetallurgicalComplex(A3) | 23130 | 约 -26.6（TiAlloy=45 时） | 永不回本 | 加工 90-240s | 折价敏感：TiAlloy 须约 104+ 才保本（TiConc 2.4/s 入 0.45/s 出，收率 18.75%），需按真实用途重估（B2 关联） |
| OrbitalSolarArray(A4) | 60720 | 360 电/s（维护 6.3 价值/s） | 基础设施按能力计 | — | 与 CPS 单位电成本相当（0.0175 vs 0.0225 价值/电），并存合理 |
| OrbitalAgroecologyArray(A4) | 约 483000 | 720 food/s（支撑 1800 生产力/s）+180 Biomass+52 Wood | 生产力轴极强，瓶颈在建造成本 | — | 合理巨构 |
| QuantumComputingArray(A4) | 约 187000 | 500 研究力/s | 见 4.4 | 研究 5-15min（早期口径） | 受 B3/B4 |
| PhaseMaterialSynthesisArray(A4) | 约 145000 | 0.008 PM/s≈1.6-8/s 价值 | 25 小时（PM=250 折价） | — | 最长回本（E3） |

结论：回本目标完全没有校验器（Assets/Editor/Content/ContentDependencyAnalyzer.cs 仅含 FindResearchCycles/FindResourceDeadlocks），早期两时代在带内、Industrial 起系统性超出，最异常者为 SteelForge、CokeOven/ICR、MechanizedCoalMine/MechanizedLumberyard、PMSA。

### 4.2 电力供需表

Industrial 一份全家桶（26 座各 1，含产电者）：
- 耗电合计 840/s（AluminumSmelter 100、AdvancedCeramicsPlant 62、CentralPowerStation — 产电、TitaniumMetallurgicalComplex 160、NickelRefinery 90、其余 5-50）。
- 供电：SteamPlant 120（耗 2 Coal/s）+ CentralPowerStation 300（耗 3 Coke+1.5 Coal/s），自身各一份仅 420/s，缺口 420/s 即需约 2.3 倍电厂副本。
- 燃料闭环：1 座 CPS 燃料 4.5 煤当量/s，1 座 MechanizedCoalMine 8 Coal/s 可供 1.45 座 CPS；净电约 270-406/s 每（矿+厂）组合，闭环成立。

Spacer 均衡态（15 座耗电建筑各 1 + 生产力/食物闭环解，见 4.3）：
- 一份全家桶耗电 3355/s（OREA 585、ITN 500、QCA 300、OAA 335、PMSA 260、OS 200、EMLH 180、DSO 160、DSR 150、OCPA 240、OCC 115、OHM 140、PMF 80、Shipyard 70、LaunchCenter 40）。
- 闭环规模约 12 座 OAA、4 座 OHM 时总耗电约 7460/s，需约 21 座 OrbitalSolarArray（360/s）。
- OSA 维护链（每座 0.2 Glass+0.08 Electronics+0.04 TiAlloy+0.04 Aluminum+0.03 CopperWire+0.03 Composite /s）放大后：Glass 约 4.2/s（需 3+ 座 BuildingMaterialsComplex）、Electronics 约 1.7/s 加上 QCA 0.8+ITN 1.2+OREA 0.18 等合计约 4.3-5/s（需 12-14 座 WireMill、15-17 Cu/s、5-6 座 IndustrialMetalSmelter、4-5 座 RareMetalMine）。
- 结论：峰值可达，但 Spacer 电力上限实际由 Industrial 老建筑维护链（玻璃/电子/钛合金）决定；CPS/SteamPlant 在 Spacer 仍可用且单位电成本与 OSA 相当（老建筑持续有用成立）。
- 交叉印证：PhantomFabricator 单链耗电约 276/s（1.33 TMC×160+PMF 80+配套），1 座 OSA 恰好覆盖一链，与既有发现一致。

### 4.3 Spacer 人口-生产力-食物闭环

设人口 P、耕地阵列 N=P/900（每座 720 food/s、耗 720 生产力/s、335 电）：
- 生产力供给 2P（PopulationState.cs:9）；需求 = 全家桶 11260（15 座去掉 1 座 OAA）+ 0.8P（OAA）+ OHM 超出份。
- 解得收敛点约 P≈11000-12000 人口，即 4 座 OrbitalHabitatMegastructure（3000 容量/900 生产力），约 12-14 座 OAA，食物约 8800-9600 food/s，全家桶+闭环总生产力需求约 21000/s。
- 食物校验（AGENTS.md 规则）：OHM 3000×0.8=2400 food/s 需 3.33 座 OAA，同代可建，闭合但余量极小（约 5-10%）；任何新增耗生产力建筑都会推高 P 与 OAA 数量形成二阶放大，内容规划时需按此模型预估。
- 领土：一份 Spacer 全家桶 spaceCost 约 8370，OREA 单座 1000、ITN 900，领土池须由科研/区域持续供给（另一领域，此处仅记录需求量）。

### 4.4 研究建筑梯队与 ITN/QCA 对比

| 建筑 | 研究力/s | 生产力 | 电力 | 成本(价值折算) | 研究力/生产力 | 研究力/千价值 |
|------|---------|--------|------|---------------|--------------|--------------|
| KnowledgeCircle(A0) | 1 | 2 | 0 | 120 | 0.50 | 8.3 |
| ScribeHut(A1) | 5 | 5 | 0 | 655 | 1.00 | 7.6 |
| Library(A2) | 25 | 36 | 0 | 1120 | 0.69 | 22.3 |
| Academy(A2) | 40 | 60 | 0 | 3825 | 0.67 | 10.5 |
| University(A3) | 250 | 60 | 10 | 5840 | 4.17 | 42.8 |
| DeepSpaceObservatory(A4) | 220 | 620 | 160 | 约 12 万 | 0.35 | 1.8 |
| QuantumComputingArray(A4) | 500 | 900 | 300 | 约 18.7 万 | 0.56 | 2.7 |
| InterstellarTheoryNexus(A4) | 900 | 1600 | 500 | 约 293 万 | 0.56 | 0.3 |

- University 在成本效率与研究力/生产力两个维度支配 DSO（12x/7.6x）、QCA（7.7x/15x）、ITN（140x/6.7x）；ITN 相对 QCA 研究力/生产力完全持平（0.56），第二副本 3.8M 价值换 +900/s，比 QCA 第二副本（240k 换 +500/s）差 10.3 倍，与已知 growth=1.3 问题叠加后 ITN 近似纯门槛物。
- 唯一可能逆转的因素是工作坊每建筑 researchPower 倍率（ProgressionModifierManager.cs:53 GetBuildingResearchPowerMultiplier，DSO 挂 7d4c3b2a 工作坊前置），静态扫描无法确认其数值，需运行时核对（B3 验证方式）。

## 5 证据清单

- 字段模型：Assets/Resources/Script/Data/Building.cs（costGrowth 默认 1.15、CostGrowth 静默回退 142-143 行、HasValidCostGrowth 144-147、ConfigureEconomyForEditor 186-231）。
- 运行时：Assets/Resources/Script/Manager/BuildingManager.cs（ValidateChainEconomy 240-258、GeometricSeriesCost 调用 60-61/658-659/768-772、效率模型 1339-1407、食物豁免 1373-1379、退款×DeconstructionReturnRate 768-784）；Script/Runtime/BuildingState.cs（效率 clamp 0-1）；Script/Runtime/PopulationState.cs（0.8 食物/2 生产力/1-60 增长，第 8-10 行）；Script/Manager/ProgressionModifierManager.cs（DeconstructionReturnRate 0.05 第 35 行、全局乘数 16-34）。
- 资产原文：Assets/Resources/Datas/Building/{Animal,StoneAge,Medieval,Industrial,Spacer}/*.asset 共 66 份（本报告 1.3 全表与所有产能/成本数字均出自这些文件；关键异常行：RareMetalMine.asset:22、TitaniumMetallurgicalComplex.asset:22、EarthMoonLogisticsHub.asset:20 与 51 行 maxAmount 1、QuantumComputingArray.asset:20、PhaseMaterialSynthesisArray.asset:20、InterstellarTheoryNexus.asset:20）。
- 文档基线：docs/balance/balance-model.md（增长率带 11-17、回本带 48-55、电力/食物 57-68）。
- 闭合现状：data/content-closure-static.md（Industrial 50/50、Spacer 16/16、Ultra 0/0）。
- 校验器缺口：Assets/Editor/Content/ContentDependencyAnalyzer.cs（仅 FindResearchCycles 218 行、FindResourceDeadlocks 233 行，无 costGrowth 带与回本校验）。
- 交叉引用：InterstellarTheoryNexus 1.30、EarthMoonLogisticsHub 1.01、三单源建筑 upgradeTo 空、PhantomFabricator 链 0.6 TiAlloy/s 与 276 电等事实与其他代理结论一致，本报告补充了 B1/B2（RareMetalMine/TMC 生产力 12）、B3（University 支配）、B4（QCA 1.28/PMSA 1.26）等新条目。
- 局限：回本折价为静态假设（4.1 已列全表），未运行模拟器与 Unity；工作坊每建筑研究倍率、领土池规模、幸福约束数值需运行时证据。
