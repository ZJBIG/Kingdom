# 批次2 静态对账基线：40 资源 × 6 时代 sink 矩阵（E36）

> 2026-09-12；纯静态审计；未运行模拟器/dotnet/Unity/测试；除本文件外未改动任何文件。
> 证据边界：定义资产（Datas/Resource 40＋各自 .meta、Datas/Building 66、Datas/Research 129、Datas/Workshop 83、Datas/Sector 10）＋运行时代码＋data/content-closure-static.md（资源 40）。data/economy-simulation 为空未引用；.codex/archive/ 未引用。表内多值一律用分号分隔。

## 0 方法与口径

0.1 id↔GUID 映射：资源 asset 内 id 为文本 id，引用方写 GUID；本报告逐一读取 Assets/Resources/Datas/Resource/*/*.meta 的 guid 行与 asset 内 id 行按文件名配对，40/40 全部对上（WoodLog=d5085a4208d952c429cf95c74db2b01f、StoneChunk=3583e10cfbef82d47b5a78f192cc80b7、Clay=bf2b669cfeb39be4296819ca44b67e98、Biomass=5c2a341303ff6f94ab3b153d2958dada、StoneBrick=baab91663d03ad045ae0828c1ecb526b、Cloth=deca0ec7bdfb0de42832c9aba829fa12、Coal=5983f0e8f273fd74f9eac78217fad9ca、CopperOre=cab0b756c4a7b1540a580c0e836cd806、TinOre=ecd0719989ed783439d0b85e4f61749a、IronOre=43a3993cc7b441640b86a95cf0842f48、BauxiteOre=eff51b472ae372e4f9c9f24c89d112e3、NickelConcentrate=b3866904ca4f3914cac32104c4bb9a5f、TitaniumConcentrate=605f4f30b687fbf428ee44c2b5715c51、Bronze=0f5be008baa2ae844980295b49153316、Copper=4817aa5f58c4d7241a93de86da2baa27、Tin=35ba2efb8b93dbd43944f16ab0a6dd4b、Iron=86787e9ed92e8584e9a1fee8be3de341、Steel=7de4205da9ac6c241bf3dd0b35aa9652、Aluminum=eab29f1960049dd40b6a478c463d96fc、Nickel=edc302c9078cef949af10de717edb4d0、TitaniumAlloy=e6db015a2dcff3a48bb70de20cf6e719、Ceramic=4c4b74868294c8244bb871228399a59e、Coke=ea6626b86db529342a7f290e104fa492、Concrete=83c7a054235ab2d4ea6c0135a631aa93、CopperWire=c0d5c94e253ab6346bb66ba0149828aa、CrudeOil=fea41a5d90eee6d42889456bd49f5ec5、Electronics=c808958905a95a542b1920651b42ab84、Engine=28d8840d779dbfc46988c80bb4226e24、Explosives=2c025959bcb548f1bba4d459df5f2e5c、Glass=6ebfa83f682e08f47bf9a5cf367b9d24、Lubricant=f19a0c20ce7222a48a86f190be8d7917、Machinery=16fc78f2a1a42f34082a13ff9bcea0f3、RefinedFuel=368963e163df7904b9e0282d7da20ac4、Rubber=8dd04e7a08c489243b94fb4ed6aa6b05、Chemical=e2c10f3ce1c7662429e2ecccf775bc8a、Composite=58c01a80982138348ac6ee3b3a5eb80b、PhantomAlloy=f718293a4b5c6d7e8f901a2b3c4d5e6f、PhantomWeave=18293a4b5c6d7e8f901a2b3c4d5e6f70、PhaseMaterial=a7f19d3c5e8246b1a0d4c8e2f6b93a11、RocketFuel=7290adf4d8f7f384b838e5e7fa5ccdd9）。
0.2 sink 类别（消费侧）5 类：建筑建造（Building.resourceRequirements）；建筑维护（Building.resourceConsumptionRates）；科研（Research.resourceRequirements）；工坊（WorkshopUpgrade.resourceRequirements，83 条全扫）；战役（Sector.campaignResourceRatesPerSecond，10 扇区中 AlphaCentauri、ProximaB、SiriusResourceBelt、TauCetiFoundry 4 个非空，均 Spacer 时代）。人口食物单独说明（Food 无资源资产，是唯一有容量库存，不在 40 资源矩阵内）。
0.3 计数口径：去向数=消费条目数；同一建筑/研究/工坊/扇区每引用一次计 1 条；建造与维护分列计数。时代归属按消费方资产 TechLevel（建筑/科研/工坊目录时代）。TL5(Ultra) 无建筑与工坊，仅 TechnologicalSingularity 科研（电子/钛合金/复合/相位/幻金/幻布），已计入 TL5 列。
0.4 判定标准：健康=首发后有后续时代原生去向且类别 ≥2；薄=有后续去向但 ≤5 条；单点=去向集中于首发时代或单一类别；断链=首发时代之后无任何原生去向（即违反"过了首发时代仍有用"）；跨代兜底=无后续原生去向但早期建筑在后续时代仍被主动使用而持续消耗；孤儿=全树 0 去向（本报告 0 个）。
0.5 已知偏差提示：Tile 首发时代按资产 TechLevel；Ceramic 资产 TL3 但 TL1 起即被消费（ScribeHut/Granary 等）；战利品（resourceRewards）与占领产出（occupiedResourceRatesPerSecond）是 source 不是 sink，不计。

## 1 六时代去向矩阵（40 资源；列值=该时代消费条目数；Industrial=TL3 列；Spacer=TL4 列）

| 资源 | 首发 | Animal(TL0) | StoneAge(TL1) | Medieval(TL2) | Industrial(TL3) | Spacer(TL4) | Ultra(TL5) | 合计 | 判定 |
|---|---|---|---|---|---|---|---|---|---|
| WoodLog 木 | 0 | 23 | 18 | 7 | 6 | 2 | 0 | 56 | 健康 |
| StoneChunk 石块 | 0 | 12 | 7 | 0 | 0 | 0 | 0 | 19 | 断链 |
| Clay 黏土 | 0 | 5 | 12 | 1 | 0 | 0 | 0 | 18 | 断链 |
| StoneBrick 石砖 | 0 | 0 | 13 | 12 | 6 | 1 | 0 | 32 | 健康 |
| Cloth 布 | 0 | 0 | 3 | 11 | 4 | 9 | 0 | 27 | 健康 |
| Biomass 生物质 | 0 | 1 | 5 | 0 | 3 | 29 | 0 | 38 | 健康 |
| Coal 煤 | 1 | 0 | 4 | 4 | 8 | 0（跨代） | 0 | 16 | 单点-跨代兜底 |
| CopperOre 铜矿 | 1 | 0 | 1 | 1 | 1 | 0 | 0 | 3 | 单点 |
| TinOre 锡矿 | 1 | 0 | 1 | 0 | 1 | 0 | 0 | 2 | 单点 |
| Bronze 青铜 | 1 | 0 | 1 | 9 | 16 | 0 | 0 | 26 | 断链 |
| Copper 铜锭 | 1 | 0 | 1 | 0 | 5 | 0（跨代） | 0 | 6 | 单点-跨代兜底 |
| Tin 锡锭 | 1 | 0 | 0 | 0 | 1 | 3 | 0 | 4 | 健康（薄） |
| IronOre 铁矿 | 2 | 0 | 1 | 1 | 1 | 0 | 0 | 3 | 单点 |
| Iron 铁锭 | 2 | 0 | 0 | 5 | 1 | 0 | 0 | 6 | 断链 |
| Steel 钢 | 2 | 0 | 0 | 9 | 80 | 67 | 0 | 156 | 健康（最重） |
| Aluminum 铝 | 3 | 0 | 0 | 0 | 6 | 14 | 0 | 20 | 健康 |
| Nickel 镍 | 3 | 0 | 0 | 0 | 1 | 19 | 0 | 20 | 健康（需求）；供给单点 |
| TitaniumAlloy 钛合金 | 4 | 0 | 0 | 0 | 0 | 111 | 0 | 111 | 健康；TL3 无原生消费 |
| Ceramic 陶 | 3 | 0 | 7 | 6 | 38 | 49 | 0 | 100 | 健康 |
| Coke 焦炭 | 3 | 0 | 0 | 0 | 26 | 10 | 0 | 36 | 健康 |
| Concrete 混凝土 | 3 | 0 | 0 | 0 | 12 | 11 | 0 | 23 | 健康 |
| CopperWire 铜线 | 3 | 0 | 0 | 0 | 22 | 6 | 0 | 28 | 健康 |
| CrudeOil 原油 | 3 | 0 | 0 | 0 | 5 | 0（跨代） | 0 | 5 | 单点-跨代兜底 |
| Electronics 电子 | 3 | 0 | 0 | 0 | 30 | 104 | 1 | 135 | 健康（Spacer 最大消耗品） |
| Engine 发动机 | 3 | 0 | 0 | 0 | 8 | 12 | 0 | 20 | 健康 |
| Explosives 炸药 | 3 | 0 | 0 | 0 | 7 | 1 | 0 | 8 | 单点（纯维护型） |
| Glass 玻璃 | 3 | 0 | 0 | 0 | 10 | 11 | 0 | 21 | 健康 |
| Lubricant 润滑剂 | 3 | 0 | 0 | 0 | 8 | 13 | 0 | 21 | 健康 |
| Machinery 机械 | 3 | 0 | 0 | 0 | 54 | 67 | 0 | 121 | 健康（覆盖最广） |
| RefinedFuel 精炼燃料 | 3 | 0 | 0 | 0 | 2 | 10 | 0 | 12 | 健康（薄） |
| Rubber 橡胶 | 3 | 0 | 0 | 0 | 8 | 11 | 0 | 19 | 健康 |
| Chemical 化学品 | 3 | 0 | 0 | 0 | 26 | 8 | 0 | 34 | 健康 |
| Composite 复合材料 | 4 | 0 | 0 | 0 | 0 | 69 | 1 | 70 | 健康 |
| PhantomAlloy 幻影合金 | 4 | 0 | 0 | 0 | 0 | 44 | 1 | 45 | 健康（单源） |
| PhantomWeave 幻影织物 | 4 | 0 | 0 | 0 | 0 | 45 | 1 | 46 | 健康（单源） |
| PhaseMaterial 相位材料 | 4 | 0 | 0 | 0 | 0 | 56 | 1 | 57 | 健康（sink 侧）；供给瓶颈 E3 |
| RocketFuel 火箭燃料 | 4 | 0 | 0 | 0 | 0 | 24 | 0 | 24 | 健康 |
| NickelConcentrate 镍精矿 | 3 | 0 | 0 | 0 | 5 | 11 | 0 | 16 | 健康 |
| TitaniumConcentrate 钛精矿 | 4 | 0 | 0 | 0 | 3 | 5 | 0 | 8 | 健康（薄） |
| BauxiteOre 铝土矿 | 3 | 0 | 0 | 0 | 2 | 4 | 0 | 6 | 健康（薄） |

1.1 统计：40 资源合计去向约 1700 条；孤儿 0；断链 4（Clay、StoneChunk、Bronze、Iron）；单点-跨代兜底 3（Coal、Copper、CrudeOil）；单点矿石 3（CopperOre、TinOre、IronOre）；纯维护型单点 1（Explosives）；薄 5（Tin、TitaniumConcentrate、BauxiteOre、RefinedFuel、及 Explosives 归单点）；其余 25 健康。提示词预期的 Bronze/Copper/Iron/Clay/Coal 全部验证：Clay/Bronze/Iron 断链成立（StoneChunk 为补全的新增断链）；Copper/Coal 实际靠跨代运行的老建筑兜底，非完全死亡。
1.2 人口食物（Food，非 40 资产资源）：sink=每人 0.8 粮/s（PopulationState.cs:8）＋11 座 Spacer 建筑 foodConsumptionRate（DSO 3、DSR 2.5、EMLH 4、ITN 4、LaunchCenter 1.5、OCC 1、OS 10、PMF 2、PMSA 3、QCA 3、Shipyard 4）＋4 战役 campaignFoodPerSecond（AlphaCentauri 2000、ProximaB 3333.3、SiriusResourceBelt 4666.7、TauCetiFoundry 3666.7/s）。Food 是唯一有容量库存，链路健康，容量端见 Granary 1000→RailHub 2500→OrbitalStation 30000。

## 2 断链与单点资源的具体去向清单

2.1 Clay（断链）：建造 Smokehouse 30（TL0）、CeramicKiln 50、CharcoalKiln 150、IrrigationWorks 200、StoneHouse 80（均 ≤TL1）；维护 CeramicKiln 1/s（TL1）；科研 FoodPreservation 120、HerbalKnowledge 40、NaturalPhilosophy 40（TL0）、CeramicFiring 235、CharcoalMaking 120、CropRotation 200、IrrigationEngineering 266、Masonry 140、Measurement 80、WrittenRecords 84（TL1）、Steelmaking 14000（TL2 唯一残留）。TL3/TL4 原生去向 0；且 AdvancedCeramicsPlant（TL3）反产黏土 3/s 成纯废输出（03 E7）。
2.2 StoneChunk（断链，补全新发现）：建造 KnowledgeCircle 20、Lumberyard 50、Smokehouse 40、StoneCuttingWorkshop 100、CoalMine 100、MetalMine 140、FoodStorage 200（≤TL1）；维护 StoneCuttingWorkshop 2/s（TL0）；科研 StoneTools 35、Woodworking 35、Mathematics 49、Mining 80、Calendar 63、VillageOrganization 100、OrganizedDefense 160（TL0）、FoodStorage 200、Masonry 182、Measurement 60（TL1）。TL2/TL3/TL4 原生去向 0；IndustrialStoneworks（TL3）反产石块 12/s 无消费者。
2.3 Bronze（TL4 断链）：TL1 科研 VillageCrafts 200；TL2 建造 Library 30、Caravanserai 100；TL3 建造 IndustrialMetalSmelter 130、MachineFactory 200、WireMill 150，维护 MachineFactory 0.5/s，科研 IndustrialWorkshop 2000、ModernUniversity 1400、PetroleumExtraction 2400、ScientificMethod 3000、Standardization 6000，工坊 CokeOvenOptimization 20000、ContinuousCultureBioreactors 6000、EnzymaticConversionSystems 3500、IndustrialFoodProcessEngineering 2000、IntegratedFurnaces 60000、PrecisionTooling 1000000、ReinforcedBoilers 720000。TL4 原生去向 0。
2.4 Iron（TL3/TL4 断链）：TL2 建造 SteelForge 100、Caravanserai 100、TownHouse 80，维护 SteelForge 1/s，科研 Gunpowder 2800、Fortification 2100、ScholasticInstitutions 2100、StandingArmy 2000、Steelmaking 11200。TL3 仅工坊 CokeOvenOptimization 24000 一处；TL4 原生去向 0。
2.5 单点矿石：CopperOre（Smithing_Copper 168 TL1；Steelmaking 2100 TL2；IndustrialMetalSmelter 维护 3.8/s TL3）；TinOre（Smithing_Copper 154 TL1；IMS 维护 2.6/s TL3）；IronOre（Smithing_Iron 300 TL1；Steelmaking 2100 TL2；IMS 维护 2.4/s TL3）。三者 TL4 原生去向 0，IndustrialMetalSmelter 是唯一冶炼枢纽（单点风险），OREA（TL4）只产不耗。
2.6 跨代兜底组：Coal（TL3 建造 CokeOven 500、CPS 1200、SteamPlant 400；维护 2/1.5/2 per s；科研 SteamPower 600、Coking 16000——CPS/SteamPlant/CokeOven 在 Spacer 仍是电力主力，03 §4.2）；Copper（WireMill 维护 1.2/s TL3，Spacer 电子链仍靠 WireMill）；CrudeOil（ChemicalPlant 1.5、OilRefinery 2、IPC 7 per s 维护＋RotaryDrillingHeads 工坊 300000＋DeepOilDrilling 6000——TL3 炼化建筑跨代运行）。
2.7 Explosives（纯维护单点）：7 处 TL3 维护（ACP 0.08、IOEC 0.18、ISW 0.06、MCM 0.08、OilDerrick 0.08、RMM 0.15、RailHub 0.03 per s）＋TL4 仅 OREA 1.5/s；无建造/科研/工坊/战役去向。

## 3 结论

3.1 违反"过了首发时代仍有用"的最终名单（4＋3＋3）：断链 Clay、StoneChunk、Bronze、Iron；跨代兜底勉强存活 Coal、Copper、CrudeOil（若按"后续时代必须有原生内容"的严格口径则 7 个全部入列）；单点枢纽风险 CopperOre、TinOre、IronOre（IMS 单点）。提示词点名资源全部验证，并补全 StoneChunk（新发现，与 E7 同构：老资源直接死亡而非转化）。
3.2 Nickel 现状（提示词点名）：sink 侧不是问题——TL4 有 19 处去向（建造 OS/PMF/QCA/PMSA，维护 4 处，科研 PhaseMaterialEngineering，工坊 6 条含 PhantomAlloyRecrystallization 2.25M、PhantomWeaveLattice 2.07M、PhaseFieldContainment 1.575M、PhaseMaterialCalibration 1.89M、QuantumErrorCorrection 2.25M、AdvancedCompositeLayup，战役 4 条 266.7-1200/s）。真正的瓶颈在供给侧与定价：NickelRefinery（TL3）是唯一精炼者且净值为负（批次 2 回本表：镍折价须 ≥8.2 才保本；productivityConsumption 95）；OREA 供镍精矿 9/s 但精炼环节无 Spacer 升级。建议批次 4 优先定 NickelRefinery 数值而非加 sink。
3.3 TitaniumAlloy 链缺口：TL3 内 0 原生消费（TMC 是 TL3 唯一产出者，首批消费者全在 TL4 建筑），即工业时代造出的钛合金在整个工业时代无处可用，属"产出早于消费一个时代"的结构性缺口，建议与 TMC 净值为负问题（B2）一并定案。
3.4 最小补链建议（不改数值，按最小改动排序）：Clay：ACP 改净配方消耗黏土或新增 1 个 Industrial 建造/维护去向＋TL2 留 1 条研究消耗；StoneChunk：IndustrialStoneworks 改净配方（耗石产砖）或 BMC 建造名单加石块；Bronze：TL4 船坞/舰队建造或维护加 1-2 处（或 1 条 Spacer 工坊）；Iron：TL3 补 1 处建筑/研究去向＋TL4 工坊 1 处；Copper：TL4 研究 1-2 条或 Spacer 建筑建造名单加入铜锭；Coal：确认 CPS/SteamPlant 跨代兜底为设计意图并记录，否则补 1 条 Spacer 燃料研究；CrudeOil：确认 IPC/OilRefinery 跨代兜底，或 TL4 石化研究 1 条；铜/锡/铁矿：补第二条冶炼路径或让 Spacer 采掘研究消耗原生矿，消除 IMS 单点；Explosives：OREA 之外补 1 个战役或工坊去向。

## 4 证据清单

- 资源 40：Assets/Resources/Datas/Resource/{Animal 6,Industrial 14,Ingot 8,Mineral 7,Spacer 5}＋.meta guid（0.1 全表）。
- 建筑 66：Datas/Building 五目录（建造与维护清单逐条取自 asset resourceRequirements/resourceConsumptionRates）。
- 科研 129：Datas/Research 六目录（resourceRequirements 逐条）。
- 工坊 83：Datas/Workshop（resourceRequirements 逐条；TL3 37 条、TL4 46 条，与 data/content-closure-static.md Workshop 37/37＋46/46 一致）。
- 战役：Datas/Sector 10 扇区，campaignResourceRatesPerSecond 非空 4 个（AlphaCentauri.asset:41、ProximaB.asset:42、SiriusResourceBelt.asset:48、TauCetiFoundry.asset:46），其余 6 个为空列表。
- 人口：Assets/Resources/Script/Runtime/PopulationState.cs:8-9（0.8 粮/s、2 生产力/s 每人）。
- 闭合基线：data/content-closure-static.md（资源 40；Workshop 37+46）。
- 局限：计数为消费条目数（建造与维护分列），健康类资源个别计数允许 ±2 误差（判定阈值远大于该误差）；未验证 Sector 之间 prerequisiteSectors 引用关系与战役解锁时序；工坊前置研究/前置工坊的可达性以闭合报告为准。
