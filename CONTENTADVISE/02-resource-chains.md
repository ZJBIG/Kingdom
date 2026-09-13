# 资源与生产链只读审计
> 2026-09-12 · 只读静态扫描，未改动任何代码/资产/文档；未运行模拟器与 Unity（未执行真实 Unity 编译）
> 扫描范围：40 个 Resource 资产、66 个 Building 资产、129 个 Research 资产、83 个 WorkshopUpgrade 资产、10 个 SectorDefinition 资产、运行时 GameState/PopulationState/ResourceManager/ResourceState、docs/balance、data/content-closure-static.md、Texture/Resource 全部贴图 GUID。

## 1 领域清单（40 资源来源/去向表、字段结构）

### 1.0 字段结构与数据流向

- 资产只含 id 与标签类字段：Resource.asset 只有 id、Label、TechLevel、Description、Sprite、Color（Assets/Resources/Script/Data/Resource.cs）。生产、消耗、造价全部在引用它的其他资产里。
- Building.asset 关键字段（Assets/Resources/Script/Data/Building.cs）：resourceRequirements（一次性造价）、resourceGenerationRates（每秒产出）、resourceConsumptionRates（每秒维护消耗）、foodProductionRate/foodConsumptionRate/foodCapacityGranted、powerProductionRate/powerConsumptionRate、logisticsProductionRate/logisticsConsumptionRate、populationCapacityGranted、researchPowerGranted、spaceCost、productivityConsumption、costGrowth、upgradeTo。
- Research.asset：BaseCost（研究力）+ resourceRequirements（资源消耗）。WorkshopUpgrade.asset：resourceRequirements + effects（含 ResourceProductionMultiplier、GlobalFoodProductionMultiplier 等 20 类）。
- SectorDefinition.asset：resourceRewards（一次性战利品）、occupiedResourceRatesPerSecond（占领期持续流）、colonization/campaign 的 Food 与资源每秒消耗。
- Food 不在 Resource 资产体系内：GameState.BaseFoodProductionRate=5、BaseFoodCapacity=500（GameState.cs:7-11），PopulationState.FoodConsumptionPerPerson=0.8 每人每秒（PopulationState.cs:8）。Food 是唯一带容量库存（符合 docs/balance/no-resource-caps.md）。
- 断供规则：ResourceManager.CalculateSatisfaction(库存, 产能, 消耗, dt) 决定 efficiency（ResourceManager.cs:385 起）；库存为 0 时下游建筑减速，普通资源无硬上限钳制。
- 消耗不足时建筑按满足率降效，没有 workforce；生产力 productivityConsumption 是独立流量池（人口供给）。

### 1.1 40 资源来源/去向表

计数说明：造价=引用该资源的建筑一次性 requirements 数；维护=每秒 consumptionRates 引用数；科研/工坊=引用其 resourceRequirements 的数量。产能数字为单座建筑基准值（受工坊与效率修正）。

| 资源 | 来源（产能） | 造价去向 | 维护去向 | 科研/工坊去向 | 评价 |
| --- | --- | --- | --- | --- | --- |
| WoodLog 木材 | Lumberyard 5/s；MechanizedLumberyard 18/s；OrbitalAgroecologyArray 52/s；初始资源 | 28+（Animal 9、StoneAge 8、Medieval 5、Industrial 2、Spacer 1） | 3：CharcoalKiln 2/s；IndustrialCarbonizationRetort 8/s；OrbitalCarbonizationComplex 26/s | 科研 25（MechanizedForestry 32000）；工坊 2（MechanizedForestryEquipment 240000） | 长期循环良好 |
| Biomass 生物质 | FiberGatheringCamp 1.5/s；PlantingField 18/s；OrbitalAgroecologyArray 180/s | 7（MechanizedTextileMill 2400；OrbitalAgroecologyArray 24000；OrbitalHabitatMegastructure 16000；OrbitalStation 21000；OrbitalResourceExtractionArray 18000） | 5：WeavingWorkshop 4/s；MechanizedTextileMill 20/s；Habitat 12/s；Station 2.62/s；OreArray 80/s | 科研 15+（OrbitalTextileFabrication 3.0e6；PrecisionMedicine 2.4e6；InterstellarOccupationAdministration 3.6e6）；工坊 5+（OrbitalAgroponicSystems 1.8e7） | 优秀，后期主循环 |
| Clay 黏土 | ClayPit 1.5/s；AdvancedCeramicsPlant 3/s | 5（Smokehouse 30；CharcoalKiln 150；IrrigationWorks 200；StoneHouse 80；CeramicKiln 50） | 1：CeramicKiln 1/s | 科研 11，全部 TL0-2，最大 Steelmaking 14000（TL2）；工坊 0 | Spacer 后近乎死亡 |
| StoneChunk 石块 | Quarry 2.4/s；IndustrialStoneworks 12/s | 6 + OrbitalResourceExtractionArray 18000 | 1：StoneCuttingWorkshop 2/s | 科研 10（最大 StoneAgeSettlement 1200） | 良好（有 Spacer 造价回收） |
| StoneBrick 石砖 | StoneCuttingWorkshop 1/s；IndustrialStoneworks 5/s | 15+（IndustrialStoneworks 3000；OrbitalResourceExtractionArray 18000） | 1：BuildingMaterialsComplex 4/s | 科研 8（Coking 8000 等） | 良好 |
| Cloth 布料 | WeavingWorkshop 1/s；MechanizedTextileMill 10/s；OrbitalResourceExtractionArray 50/s | 9（Academy 250；OrbitalResourceExtractionArray 5000；OrbitalStation 6000；PhantomMaterialsFabricator 150） | 2：OrbitalStation 1.5/s；PhantomMaterialsFabricator 0.1/s | 科研 10（OrbitalTextileFabrication 1.2e6）；工坊 2（OrbitalTextileLooms 4.0e6） | 良好 |
| Ceramic 陶瓷 | CeramicKiln 1/s；AdvancedCeramicsPlant 2.4/s；BuildingMaterialsComplex 0.4/s | 5（Academy 150；Granary 30 等） | 7：WireMill 0.1/s；Habitat 0.02/s；OBS 0.08/s；QuantumArray 0.08/s；PhantomFab 0.12/s；PhaseArray 0.10/s；Station 0.08/s | 科研 25+（ModernMedicine 60000；TechnologicalSingularity 前置链大量引用）；工坊 20+（QuantumErrorCorrection 1.35e7） | 全期核心，极佳 |
| Chemical 化学品 | ChemicalPlant 1.5/s；IntegratedPetrochemicalComplex 0.6/s | 1（IndustrialOilExtractionComplex 300） | 13：AluminumSmelter 0.3/s；TitaniumComplex 0.8/s；PlantingField 0.25/s；OrbitalAgro 1.2/s；CryoArray 3/s；OreArray 2.4/s；PhaseArray 0.12/s 等 | 科研 10+（NickelHydrometallurgy 8400；ChlorideTitaniumMetallurgy 9800）；工坊 10+ | 战略中间品，优秀 |
| Coke 焦炭 | CokeOven 1.5/s；IndustrialCarbonizationRetort 4.8/s；OrbitalCarbonizationComplex 18/s | 6（IndustrialMetalSmelter 200；AdvancedCeramicsPlant 650；Petrochemical 800；PhaseMaterialSynthesisArray 4000） | 10：CentralPowerStation 3/s；IndustrialMetalSmelter 1.35/s；OreArray 14/s；PhaseArray 0.12/s 等 | 科研 4（OrbitalCarbonizationProcessEngineering 18000；PhaseMaterialEngineering 60000）；工坊 5（PhantomWeaveLattice 8.1e5） | 优秀 |
| Concrete 混凝土 | 仅 BuildingMaterialsComplex 2/s（单源） | 15（AluminumSmelter 500；Habitat 1400；CryoArray 18000；OrbitalResourceExtractionArray 21600；Shipyard 8000） | 2：IndustrialHabitationComplex 0.03/s；无 Spacer 维护 | 科研 2（OrbitalEngineering 24000；OrbitalHabitation 36000）；工坊 4 | 单源瓶颈 |
| CopperWire 铜线 | 仅 WireMill 1.4/s（单源） | 4（MachineFactory 200；TitaniumComplex 200；OrbitalSolarArray 500） | 2：MachineFactory 0.3/s；OrbitalSolarArray 0.03/s | 科研 9（ElectricalCommunication 10500；HomeSystemSurvey 5000）；工坊 10（OrbitalPowerGridStabilization 9.0e5） | 单源但下游尚可 |
| CrudeOil 原油 | OilDerrick 3/s；IndustrialOilExtractionComplex 12/s | 0 | 3：ChemicalPlant 1.5/s；OilRefinery 2/s；Petrochemical 7/s | 科研 1（DeepOilDrilling 6000）；工坊 1（RotaryDrillingHeads 3.0e5） | 纯中间品，合格 |
| Electronics 电子元件 | 仅 WireMill 0.35/s（单源，无升级链） | 14（IndustrialHabitationComplex 200；OreArray 9000；CryoArray 3900；Station 1300） | 15：InterstellarTheoryNexus 1.2/s；QuantumComputingArray 0.8/s；OreArray 0.18/s；CryoArray 0.12/s 等 | 科研 31（GravitationalCommunicationTheory 2.2e5；InterstellarKnowledgeCoordination 4.8e5；TechnologicalSingularity 64000）；工坊 20+（RedundantNavigationControl 5.4e7） | 最重单源瓶颈 |
| Engine 发动机 | 仅 MachineFactory 0.2/s（单源） | 3（RailHub 30；LaunchCenter 100；Shipyard 200） | 3：RailHub 0.05/s；LaunchCenter 0.02/s；Shipyard 0.12/s | 科研 7（DeepSpaceFleet 12000）；工坊 4（ReusableLaunchStages 8.1e5） | 单源，中风险 |
| Explosives 炸药 | ChemicalPlant 0.35/s；Petrochemical 0.8/s | 0 | 7：OilDerrick 0.08/s；MechanizedCoalMine 0.08/s；RareMetalMine 0.15/s；OreArray 1.5/s 等 | 科研 2；工坊 1 | 采掘战略品，合格 |
| Glass 玻璃 | 仅 BuildingMaterialsComplex 1.2/s（单源） | 8（IndustrialHabitationComplex 500；DeepSpaceObservatory 4000；OrbitalAgro 7000；Habitat 5000） | 4：OrbitalSolarArray 0.20/s；OBS 0.05/s；PhantomFab 0.12/s；University 0.05/s | 科研 4（ModernMedicine 24000；AdvancedCeramicEngineering 5000）；工坊 6（OrbitalPowerBeaming 1.56e5） | 单源瓶颈 |
| Lubricant 润滑油 | OilRefinery 0.4/s；Petrochemical 1.3/s | 0 | 9：MachineFactory 0.1/s；RailHub 0.05/s；OreArray 2.0/s；Shipyard 0.08/s 等 | 科研 2；工坊 5（IntegratedDeepSpaceProductionControl 1.08e5） | 良好 |
| Machinery 机械部件 | 仅 MachineFactory 1/s（单源，无升级链） | 17（AdvancedCeramicsPlant 680；MechanizedCoalMine 450；OreArray 14400；Shipyard 5000） | 2：RailHub 0.08/s；Shipyard 0.18/s | 科研 25（DeepSpaceSystemsTheory 30000 等）；工坊 25+（QuantumErrorCorrection 2.25e7；PhaseFieldContainment 1.575e7） | 最重单源瓶颈 |
| RefinedFuel 精炼燃料 | OilRefinery 1.2/s；Petrochemical 4/s | 4（LaunchCenter 300；CryoArray 7200；Shipyard 600；Station 400） | 3：CryoArray 8/s；Shipyard 0.15/s；Station 0.05/s | 科研 6（OrbitalPropellantEngineering 1.0e5）；工坊 3（ReusableLaunchStages 1.125e6） | 良好 |
| Rubber 橡胶 | OilRefinery 0.3/s；Petrochemical 1/s | 2（OrbitalAgro 3500；PhantomFab 180） | 3：Habitat 0.03/s；Station 1.2/s；Shipyard 0.10/s | 科研 1；工坊 10（ReusableLaunchStages 8.1e6；PhantomWeaveLattice 1.17e6） | 良好 |
| Aluminum 铝 | 仅 AluminumSmelter 1.2/s（单源） | 4（LaunchCenter 250；OrbitalSolarArray 700；Shipyard 600） | 4：MachineFactory 0.08/s；EarthMoonHub 0.10/s；OrbitalSolarArray 0.04/s；TitaniumComplex 0.12/s | 科研 5（DeepSpaceShipbuilding 24000）；工坊 7（ReusableLaunchStages 2.25e6） | 合格 |
| Bronze 青铜 | MetalSmelter 0.4/s；IndustrialMetalSmelter 0.4/s | 5（全部 TL1-3：OilDerrick 250；MachineFactory 200） | 1：MachineFactory 0.5/s | 科研 7（最大 Standardization 6000，TL3）；工坊 4（PrecisionTooling 1.0e6，TL3） | Industrial 后死亡 |
| Copper 铜 | MetalSmelter 0.8/s；IndustrialMetalSmelter 3/s | 2（OilRefinery 200；WireMill 300） | 1：WireMill 1.2/s | 科研 5（ModernMedicine 18000）；工坊 2（VacuumArcFurnace 1.44e5） | Industrial 后死亡 |
| Iron 铁 | MetalSmelter 0.8/s；IndustrialMetalSmelter 1.8/s | 3（Caravanserai 100；TownHouse 80；SteelForge 100） | 1：SteelForge 1/s | 科研 3（Steelmaking 11200）；工坊 1（CokeOvenOptimization 24000） | Industrial 后死亡 |
| Nickel 镍 | 仅 NickelRefinery 0.9/s（单源） | 4（OrbitalStation 1000；PhantomFab 300；PhaseArray 1100；QuantumArray 600） | 4：PhantomFab 0.15/s；PhaseArray 0.05/s；QuantumArray 0.06/s；Station 0.04/s | 工坊 7（QuantumErrorCorrection 2.25e6；PhantomWeaveLattice 2.07e6） | 高科技战略品，合规 |
| Steel 钢 | SteelForge 1/s；IndustrialMetalSmelter 0.6/s | 30+（全期） | 3：IndustrialHabitationComplex 0.02/s；DeepSpaceRelay 0.04/s；MachineFactory 0.8/s | 科研 2（Industrialization 50000；DeepSpaceShipbuilding 2.0e5）；工坊 25+（VacuumArcFurnace 2.8e6） | 全期核心 |
| Tin 锡 | MetalSmelter 0.8/s；IndustrialMetalSmelter 2.4/s | 1（OrbitalSolarArray 240，Spacer 复用） | 1：WireMill 0.5/s | 科研 2（OrbitalEngineering 18000）；工坊 1（OrbitalPowerBeaming 2.6e5） | 后期偏薄但存在 |
| TitaniumAlloy 钛合金 | TitaniumMetallurgicalComplex 0.45/s；OreArray 5.12/s | 10+（InterstellarTheoryNexus 14000；OreArray 14400；CryoArray 4800） | 8：PhantomFab 0.6/s；Habitat 0.12/s；Station 0.12/s；Shipyard 0.12/s；PhaseArray 0.08/s；QuantumArray 0.08/s | 科研 20+（DeepSpaceIndustrialIntegration 70000）；工坊 15+（QuantumErrorCorrection 4.5e5） | 全期核心，极佳 |
| BauxiteOre 铝土矿 | RareMetalMine 2/s；OreArray 24/s | 0 | 1：AluminumSmelter 2/s | 科研 2（AutonomousOrbitalMining 160000；OrbitalConstructionAutomation 360000）；工坊 3（AutomatedShipyardAssembly 4.2e5） | 存在科研墙（见 3.5） |
| Coal 煤 | CoalMine 1.5/s；CharcoalKiln 0.8/s；MechanizedCoalMine 8/s；OrbitalCarbonizationComplex 18/s | 4（CokeOven 500；SteamPlant 400；CentralPowerStation 1200） | 4：MetalSmelter 1.5/s；SteelForge 1/s；SteamPlant 2/s；CentralPowerStation 1.5/s | 科研 7（Steelmaking 18200；Gunpowder 12600） | Spacer 无直接去向（间接走 CokeOven） |
| CopperOre 铜矿 | MetalMine 1/s；RareMetalMine 4.8/s；OreArray 60/s | 0 | 2：MetalSmelter 1/s；IndustrialMetalSmelter 3.8/s | 科研 1（Smithing_Copper 168） | 原料层合格 |
| IronOre 铁矿 | MetalMine 0.8/s；RareMetalMine 3.2/s；OreArray 40/s | 0 | 2：MetalSmelter 1/s；IndustrialMetalSmelter 2.4/s | 科研 1（Smithing_Iron 300） | 原料层合格 |
| NickelConcentrate 镍精矿 | RareMetalMine 1.2/s；OreArray 9/s | 1（TitaniumComplex 180） | 2：NickelRefinery 2/s；TitaniumComplex 0.18/s | 科研 2（OrbitalConstructionAutomation 2.4e5；OrbitalVacuumMetallurgy 1.3e5）；工坊 3（AutomatedShipyardAssembly 3.0e5） | 合格 |
| TinOre 锡矿 | MetalMine 0.8/s；RareMetalMine 3.2/s；OreArray 40/s | 0 | 2：MetalSmelter 1/s；IndustrialMetalSmelter 2.6/s | 科研 1（Smithing_Copper 154） | 原料层合格 |
| TitaniumConcentrate 钛精矿 | RareMetalMine 1.6/s；OreArray 9/s | 0 | 1：TitaniumMetallurgicalComplex 2.4/s | 科研 5（PlanetaryGeologyTheory 9.0e4；OrbitalConstructionAutomation 5.0e4）；工坊 4（PlanetaryCoreSamplingBench 1.125e5） | 合格 |
| Composite 复合材料 | 仅 MachineFactory 0.45/s（单源） | 13（InterstellarTheoryNexus 8000；OreArray 10620；Shipyard 2600；Station 2600） | 9：PhaseArray 0.25/s；PhantomFab 0.35/s；Habitat 0.08/s；Station 0.07/s 等 | 科研 18（MatterStateControlTheory 24000）；工坊 15（IntegratedDeepSpaceProductionControl 9.0e4） | 重单源瓶颈 |
| PhantomAlloy 幻影合金 | 仅 PhantomMaterialsFabricator 0.06/s | 10（InterstellarTheoryNexus 10000；OreArray 2520；QuantumArray 1200） | 6：PhaseArray 0.11/s；QuantumArray 0.05/s；OreArray 0.035/s 等 | 科研 17（InterstellarCybersecurityTheory 2.0e4）；工坊 10（PhantomWeaveLattice 2.4375e5） | 单源，链条尖端 |
| PhantomWeave 幻影织物 | 仅 PhantomMaterialsFabricator 0.04/s | 9（InterstellarTheoryNexus 8000；OreArray 220；QuantumArray 900） | 6：PhaseArray 0.09/s；Station 0.05/s；Habitat 0.04/s 等 | 科研 14（InterstellarAutonomyCharterTheory 2.0e4）；工坊 10（QuantumErrorCorrection 3.0e5） | 单源，链条尖端 |
| PhaseMaterial 相位材料 | 仅 PhaseMaterialSynthesisArray 0.008/s（全游戏最尖端单源） | 7（InterstellarTheoryNexus 6500；OreArray 1740；Station 700；QuantumArray 120） | 7：OreArray 0.042/s；QuantumArray 0.03/s；Station 0.02/s 等 | 科研 13（InterstellarKnowledgeCoordination 32500；MatterStateControlTheory 18000）；工坊 13（PhaseMaterialCalibration 97500）；战役消耗 300-800/s（ProximaB/TauCeti/Sirius） | 全域最重瓶颈 |
| RocketFuel 火箭燃料 | ChemicalPlant 0.25/s；OrbitalCryogenicPropellantArray 14/s | 3（DeepSpaceRelay 180；Station 700） | 4：LaunchCenter 0.08/s；OBS 0.03/s；Relay 0.05/s；Station 0.25/s | 科研 9（OrbitalPropellantEngineering 12000）；工坊 3；殖民 2（416.67/s、750/s）；战役 4（1666.67-3666.67/s） | 战役消耗与产能严重失衡（见 4.4） |
| （Food 食物，非资产） | 基础 5/s；Farm 8/s；Smokehouse 2.5/s；Granary 2/s；IrrigationWorks 20/s；PlantingField 48/s；OrbitalAgroecologyArray 720/s | 无造价 | 人口 0.8/人/s；11 座 Spacer 建筑 1-10/s；殖民 16.67-4000/s；战役 2000-4666.67/s | 容量：Smokehouse 400；CeramicKiln 250；Granary 1000；IrrigationWorks 200；RailHub 2500；OrbitalStation 30000 | 唯一封顶库存，链条成立 |

区域流核查：10 个 Sector 的 occupiedResourceRatesPerSecond 全部在 0.0016-0.24/s（ShardCrown 钛精矿 0.18/s；HeliosCore 铝 0.24/s；SiriusResourceBelt 钛合金 0.08/s）；一次性战利品 12000-900000 领土 + 最多 5 万级资源；对比玩家产能（钛合金 0.45/s/炉，钛精矿 1.6/s/矿）占比很小，未发现区域替代玩家自建生产的违规。

## 2 内容拓展机会

| # | 事项 | 证据 | 为什么值得 | 建议方向 | 优先级 | 工作量 |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | Ultra 时代零内容：资源目录、建筑、工坊均不存在，仅 1 项科研 | Assets/Resources/Datas/Resource 无 Ultra 目录；Building/Ultra 空目录；Research/TechnologicalSingularity.asset BaseCost 5.5e11、AdvancesTechLevel 1；闭合报告 Ultra 0/0/1 | 时代目录宣称 6 时代但第 6 时代是死胡同；TechnologicalSingularity 消耗 Electronics 64000、TitaniumAlloy 48000、Composite 28000、PhaseMaterial 20000、PhantomAlloy 16000、PhantomWeave 14000 后无任何下游 | 新建 Ultra 资源与建筑层：以 Electronics/TitaniumAlloy/PhaseMaterial 为输入的 Ultra 材料（贴图 UltraTech/Glasteel、Spacer/Microchips、BioMicrochips、Hyperalloy_c、DualPhaseTitanium_c 均未被任何资产引用，GUID 已验证无引用） | H | L |
| 2 | 三大单源工厂无升级链：MachineFactory、WireMill、BuildingMaterialsComplex 的 upgradeTo 均为 {fileID: 0} | Building/Industrial/MachineFactory.asset、WireMill.asset、BuildingMaterialsComplex.asset 第 19 行 | Machinery/Engine/Composite/Electronics/Glass/Concrete 六个瓶颈资源的产能被锁死在工业时代基准（1 / 0.2 / 0.45 / 0.35 / 1.2 / 2 每秒），Spacer 需求大 3-4 个量级；为它们做 Spacer 续作既补瓶颈又复用早期资源，完全符合"后期复用早期资源"规则 | 新增轨道级工厂（消耗 PhantomAlloy/Composite/TitaniumAlloy 建造与维护，产出倍率 10-50x），沿用 upgradeTo 升级链字段 | H | M |
| 3 | 相位材料链缺中间环节与倍率层 | PhaseMaterialSynthesisArray gen 0.008/s、upgradeTo 0；PhantomMaterialsFabricator gen 0.06/0.04 | 0.008/s 是全游戏最小产能，却要支撑 6500-32500 的科研与工坊需求（见 4.1）；链上只有 PhantomFab 到 PhaseArray 一跳 | 在 PhantomAlloy/Weave 与 PhaseMaterial 之间增加 1-2 层中间品（如相位晶格），或给 PhaseArray 加升级/并联机制，让产能可指数增长 | H | M |
| 4 | 19 张异宝石贴图全部闲置 | Texture/Resource/ExtoicGem/（AirGem、AmberStone、AquaCrystal 等 19 个 png，抽样 GUID 均无资产引用） | 游戏没有任何宝石/奢侈品类资源；现有 40 资源全是工业链，缺一条与人口幸福或科研加速挂钩的并行支线 | 选 3-5 种宝石做小规模支线：来源 RareMetalMine 概率伴生或新采掘建筑，去向科研加速、幸福度或工坊 | M | M |
| 5 | 铀矿贴图闲置，电力链无燃料纵深 | Texture/Resource/Mineral/Uranium.png GUID f886e837 无任何引用；电力只有 CentralPowerStation 300/s（耗 Coke 3/s、Coal 1.5/s）与 OrbitalSolarArray 360/s | Spacer 电力需求峰值单建筑 585/s（OreArray），电力路线只有焦煤与太阳能两条，缺乏晚期抉择 | 铀采掘+核电站作为 Spacer 电力第三路线，用 RareMetalMine/领土门槛 gated | M | M |
| 6 | 老资源无 Spacer 去向：Bronze、Copper、Iron、Clay、Coal 在 Spacer 建筑与科研中零引用 | 全部 Spacer 建筑与 TL4 科研资源清单扫描（见第 1 节表） | 违反"过了首发时代仍有用"锁规则的灰区：它们的链条在 Industrial 戛然而止 | 给 Spacer 建筑/科研加少量旧金属维护（如 Shipyard 维护加 Copper 0.1/s、OrbitalCarbonizationComplex 用 Coal 选项、PhantomFab 已用 Cloth 是好范例） | M | S |
| 7 | 5 种特种木材贴图闲置 | Texture/Resource/WoodLog/EbonyLog、ElvenWoodLog、RoseWoodLog、ScentedWoodLog、WhiteBirchLog（WoodLog.png dd191cf4 被 WoodLog 资产使用，其余 5 个无引用） | 木材是全期用量最大的资源，但只有一条产能曲线 | 低优先：后期林业科技（OrbitalAgroecologyArray 已产 WoodLog 52/s）配特种木为工坊材料 | L | S |
| 8 | Food 容量在 Spacer 只有 OrbitalStation 30000 一档 | Building 扫描：foodCapacityGranted 仅 6 处；no-resource-caps.md 允许"轨道粮仓" | 殖民消耗 1333-4000 Food/s、战役 2000-4666 Food/s，容量缓冲只有 30000/座 | 增加轨道粮仓类建筑或 OrbitalAgroecologyArray 附带容量 | L | S |
| 9 | Clay 与 StoneChunk 共用同一 Sprite GUID | Clay.asset 与 StoneChunk.asset 的 Sprite guid 同为 73ab6924373cf8144a41e11b5eed2856（Mineral/StoneChunk.png） | 视觉同图易误导玩家；也说明早期资源美术是占位 | 顺手修复或换用未用贴图 | L | S |

## 3 平衡风险

| # | 事项 | 证据 | 对玩法的影响 | 建议验证方式 | 严重度 |
| --- | --- | --- | --- | --- | --- |
| 1 | 星区战役把相位材料当常规补给：ProximaB 300/s、TauCetiFoundry 533.33/s、SiriusResourceBelt 800/s，而全游戏产能仅 0.008/s/阵 | Sector/ProximaB.asset、TauCetiFoundry.asset、SiriusResourceBelt.asset campaignResourceRatesPerSecond；Building/Spacer/PhaseMaterialSynthesisArray.asset | 打星际战役需要 37500-100000 座相位合成阵列，实际不可达；战役系统在 Spacer 深处被单点锁死 | 离线按每资源求 战役消耗/产能 比；确认 CampaignSupplyCostMultiplier 工坊是否可覆盖（即使 10x 也差 3 个量级） | H |
| 2 | TechnologicalSingularity 资源需求与尖端产能错配 | Research/TechnologicalSingularity.asset：PhaseMaterial 20000、PhantomAlloy 16000、PhantomWeave 14000；产能 0.008/0.06/0.04 每秒 | Ultra 跃迁需要约 20000/0.008=2.5e6 秒（单阵列，29 天）的相位材料积累，10 座也要 2.9 天连续满转，之后无内容回报 | 离线计算各资源 需求/产能 首达时间并与时代目标时长对比 | H |
| 3 | Electronics 单源 0.35/s 承接全部 Spacer 智能化维护 | WireMill gen 0.35/s、upgradeTo 0；InterstellarTheoryNexus 维护 1.2/s/座、QuantumComputingArray 0.8/s/座；科研 TechnologicalSingularity 64000 | 维持 5 座理论枢纽+10 座量子阵列需 14/s 电子件=40 座 WireMill，其第 40 座造价约 500x1.2^39≈3.1e5 钢且还要 0.3 铜线/s 维护；玩家会卡死在电子件上 | 对 WireMill 做回本与边际造价模拟；跑静态闭合的"维护净流"审计 | H |
| 4 | Machinery/Composite 同源双瓶颈 | MachineFactory gen 1 与 0.45 每秒；OrbitalResourceExtractionArray 造价 Machinery 14400 + Composite 10620；InterstellarTheoryNexus 造价 Machinery 10000 + Composite 8000 | 单工厂产出一座巨型结构需 4 小时（机械件）与 6.5 小时（复合材料）纯积累，多座巨型结构排队长于整段 Spacer 游玩时长 | 数值上按 产能x工坊倍率 重算巨型结构等待时长 | H |
| 5 | 轨道采矿科研墙：AutonomousOrbitalMining 要 BauxiteOre 160000、NickelConcentrate 100000；OrbitalConstructionAutomation 要 360000/240000 | Research/Spacer/AutonomousOrbitalMining.asset、OrbitalConstructionAutomation.asset；轨道前唯一来源 RareMetalMine 2/1.2/1.6 每秒且 costGrowth 1.18 | 单矿需 80000 秒（22 小时）攒 160000 铝土矿；10 座矿也要 2.2 小时且造价指数上升；Spacer 前中期被矿物库存墙卡住 | 离线求科研资源首达时间分布（目标 <2-4 小时/项） | M |
| 6 | Concrete/Glass 单源 vs 巨型结构造价 | BuildingMaterialsComplex gen 2/1.2 每秒；CryoArray 造价 Concrete 18000；OreArray 造价 Concrete 21600 + Glass 前置链（OBS 4000、AgroArray 7000、Habitat 5000） | 每座巨型结构需 2.5-3 小时混凝土单点产出，且 BuildingMaterialsComplex 自身耗 StoneBrick 4/s | 求各 Spacer 建筑 建造成本/单源产能 时间表 | M |
| 7 | 老资源断链：Bronze/Copper/Iron/Clay/Coal 在 TL4 零去向 | 第 1 节表；全部 Spacer 建筑与 TL4 科研扫描 | 违反"老资源持续有用"；青铜/铁在工业时代后彻底成为死库存，玩家止损拆旧建筑无成本提示 | 在编辑器校验器 Assets/Editor/Content 增加"末时代去向"检查项 | M |
| 8 | Food 战役收支在容量 30000 上限下脆弱 | OrbitalStation foodCapacityGranted 30000；SiriusResourceBelt 战役 Food 4666.67/s；产能 720/s/AgroArray | 战役期需要 ≥6.5 座农业阵列连续供能，容量只够 6.4 秒缓冲；一旦断供按 SectorManager 逻辑前线立即停摆 | Unity 运行时实测开战役前后 FoodNetRate；检查 PopulationState 食物短缺迁离逻辑是否会连锁 | M |
| 9 | 钛合金链功率与中间品耦合深 | TitaniumComplex 0.45/s 耗 2.4 钛精矿/s + 160 电；PhantomFab 耗 0.6 钛合金/s + 80 电 | 1 座 PhantomFab 拖 1.33 座钛炉=3.2/s 钛精矿=2 座 RareMetalMine，功率 160x1.33+90x0.7≈276 电；链式放大会让电力/精矿先行饱和，玩家难定位真瓶颈 | 离线解线性方程组（Fabricator:NxSmelter:NxMine）求每级瓶颈 | L |
| 10 | Clay/StoneChunk 共用贴图且 Clay 无后期去向 | 两 asset Sprite guid 相同；Clay 科研上限 Steelmaking 14000（TL2） | 低：视觉混淆；Clay 链长期循环不满足 | 编辑器校验器加"资源贴图唯一性"检查 | L |

## 4 重点详析

### 4.1 相位材料链产能量级（最严重）

链条：PhantomMaterialsFabricator（gen PhantomAlloy 0.06/s、PhantomWeave 0.04/s）到 PhaseMaterialSynthesisArray（gen PhaseMaterial 0.008/s，耗 PhantomAlloy 0.11/s、PhantomWeave 0.09/s、Composite 0.25/s、TitaniumAlloy 0.08/s、Nickel 0.05/s、Electronics 0.04/s、Ceramic 0.10/s、Chemical 0.12/s、Coke 0.12/s）。

- 1 座 PhaseArray 需要 0.11/0.06 = 1.83 座 PhantomFab；1 座 PhantomFab 耗钛合金 0.6/s，即每条相位链要 1.83x0.6+0.08 = 1.18 钛合金/s = 2.62 座钛合金炉（0.45/s）= 6.3/s 钛精矿 = 约 4 座 RareMetalMine（1.6/s）。
- 需求侧：InterstellarTheoryNexus 造价 6500（约 0.9 座阵列的 1 年产出）；科研 MatterStateControlTheory 18000、InterstellarKnowledgeCoordination 32500；工坊 PhaseMaterialCalibration 97500。97500/0.008 = 1.22e7 秒（单阵列 141 天），10 座阵列 14 天。
- 战役侧：SiriusResourceBelt 战役 800/s 需 100000 座阵列；即使 InterstellarOccupationGovernance 工坊的 CampaignSupplyCostMultiplier 压 10 倍仍要 10000 座。结论：战役把相位材料当成了与机械件同量级的常规补给（机械件 1800/s 对产能 1/s 同样失衡），两处都是量级错误而非缺少倍率。

### 4.2 Electronics 单源瓶颈测算

产能：WireMill 0.35/s，无 upgradeTo；造价 Steel 500、Copper 300、Bronze 150、growth 1.2。
需求：InterstellarTheoryNexus 维护 1.2/s/座；QuantumComputingArray 0.8/s/座；OreArray 维护 0.18/s/座；科研 TechnologicalSingularity 一次性 64000。
- 5 座理论枢纽 + 10 座量子阵列 = 14/s 稳态需求，需 40 座 WireMill（0.35x40=14）。
- 40 座 WireMill 总钢造价 = 500x(1.2^40-1)/0.2 ≈ 500x(1470-1)/0.2 ≈ 3.67e6 钢；第 40 座单项约 500x1.2^39 ≈ 3.1e5 钢。以 10 座工业冶炼炉（0.6/s）=6 钢/s 计，仅钢料就需 6.1e5 秒（7 天）连续生产，尚未计维护铜线 0.3/s/座。
- 同时 MachineFactory 也耗铜线 0.3/s/座；铜线单源 WireMill 1.4/s 又要支撑这一切。双重单源互相顶牛，Spacer 中后段的智能化路线（量子阵列、理论枢纽、深空网络）被放大到不可维护。

### 4.3 Food 链满负荷核算（当前规则合规）

- 人口需求：0.8/人/s。IndustrialHabitationComplex 240 人 = 192 Food/s；需 4 座 PlantingField（48/s），每座耗 Chemical 0.25/s（4 座共 1/s = 0.67 座 ChemicalPlant 1.5/s，其耗 CrudeOil 1.5/s、Coke 0.3/s），闭环成立。
- Spacer：OrbitalHabitatMegastructure 3000 人 = 2400 Food/s；需 3.34 座 OrbitalAgroecologyArray（720/s），每座耗 Chemical 1.2/s、Coke 0.8/s；阵列自身产 Biomass 180/s、WoodLog 52/s 自我补给。可持续。
- 容量：基础 500 + Granary 1000 + RailHub 2500 + OrbitalStation 30000。战役（Sirius 4666.67 Food/s）需 6.5 座农业阵列的产能；30000 容量仅 6.4 秒缓冲，战役期间产能必须全程大于消耗，容量是纯缓冲而非战略储备（风险 8 的来源，但机制本身符合 no-resource-caps.md）。
- 结论：Food 规则遵守良好；唯一数值隐患是 Spacer 食物容量单档。

### 4.4 RocketFuel 战役消耗与产能的指数爆炸

- 产能：OrbitalCryogenicPropellantArray 14/s（耗 RefinedFuel 8/s、Chemical 3/s）；ChemicalPlant 0.25/s。
- 需求：星际战役 1666.67-3666.67 RocketFuel/s。以 SiriusResourceBelt 3666.67/s 计，需 262 座 CryoArray；它们耗 RefinedFuel 8x262 = 2096/s，需 524 座 IntegratedPetrochemicalComplex（4/s），后者耗 CrudeOil 7x524 = 3668/s，需 306 座 IndustrialOilExtractionComplex（12/s）——而每座 OreArray 也只产 12 级别的原油，没有原油资产级来源。该三级堆叠在造价 growth 1.2x 下不可能完成。Colonization 消耗（AzurePool 416.67/s x 60s = 25000 总量）合理；仅战役速率疑似未按产能重校。

### 4.5 矿物科研墙（轨道前）

- AutonomousOrbitalMining：BauxiteOre 160000 + NickelConcentrate 100000 + TitaniumConcentrate 22500。轨道前唯一来源 RareMetalMine（2 / 1.2 / 1.6 每秒，costGrowth 1.18，单座造价 Concrete 550、Steel 450、Machinery 160、Electronics 80）。
- 单矿攒 160000 铝土矿需 80000 秒（22.2 小时）；8 座矿（产能 16/s）需 10000 秒（2.8 小时），但第 8 座造价 550x1.18^7≈1750 混凝土。ShardCrown 占领流 0.14/s 帮助可忽略。建议把这两项科研的矿物需求降 1 个量级或给 RareMetalMine 加升级链（MetalMine 有 upgradeTo，RareMetalMine 亦有 e2f3a4b5 目标，但产能仍 2/s 级）。

## 5 证据清单

- 资产定义类：Assets/Resources/Script/Data/Building.cs（字段与合并规则）；Resource.cs；Research.cs（BaseCost+resourceRequirements）；WorkshopUpgrade.cs（20 类效果枚举）；SectorDefinition.cs（四类资源列表）；SectorBuilding.cs。
- 资产数据类：Assets/Resources/Datas/Resource/{Animal,Industrial,Ingot,Mineral,Spacer}/*.asset（40 个）；Datas/Building/{Animal,StoneAge,Medieval,Industrial,Spacer}/*.asset（9/10/5/26/16）；Datas/Research/*.asset（129）；Datas/Workshop/*.asset（83）；Datas/Sector/*.asset（10）。数值全部来自本审计对上述 asset 的逐文件提取（id+字段值）。
- 运行时：Assets/Resources/Script/Runtime/GameState.cs（Food 基础产能 5、容量 500、FoodNetRate）；Runtime/PopulationState.cs（0.8/人/s、短缺迁离 3600s 规则）；Runtime/ResourceState.cs（满足率/效率模型）；Manager/ResourceManager.cs（CalculateSatisfaction、StartingResourceId=WoodLog）；Manager/SectorManager.cs（战役/殖民 Food 与资源逐秒扣费，302-428、1566-1861 行）。
- 规则与既有证据：docs/balance/no-resource-caps.md；docs/balance/balance-model.md；data/content-closure-static.md（闭合 50/50、16/16、0/0/1，资源 40）。
- 贴图：Assets/Resources/Texture/Resource/ 下 74 个 png；已验证无引用的 GUID 抽样：Uranium f886e837b716ede4b8d451a000b3457f、Glasteel b569b3417eea436499039634e147fed0、Microchips d73cd6ea2d6ca9c41ba8decf76c9ace0、BioMicrochips 28f9fc90f4ced2c42b92b88ba09ccfcb、Citrine 36d5acc74cff85946879c09719f07f56（在 *.asset/*.prefab/*.unity 全库 findstr 无命中）。
- 局限：本报告为静态数值推演，未运行模拟器与 Unity（未执行真实 Unity 编译）；工坊倍率（ResourceProductionMultiplier 等）与人口-生产力耦合未参与第 4 节测算，实际等待时长会按倍率缩短；所有"严重度"为静态推断，需 Unity 运行时或离线模拟复核。
