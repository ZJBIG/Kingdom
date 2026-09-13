# 批次2 静态对账基线：建筑首份回本全量表与科研目标时长全量表（E34/B29）

> 2026-09-12；纯静态审计；未运行模拟器/dotnet/Unity/测试；除本文件外未改动任何文件。
> 证据边界：定义资产（Assets/Resources/Datas/Building 66 座、Datas/Research 129 条、Datas/Resource 40 个及其 .meta）＋运行时代码（Building.cs、Research.cs、ResearchManager.cs、PopulationState.cs）＋ data/content-closure-static.md。data/economy-simulation 目录为空，未引用；.codex/archive/ 未引用。
> 折价假设沿用 CONTENTADVISE/03-buildings-production.md 第 4.1 节并做全资源扩展；RP 情景沿用 CONTENTADVISE/01-research.md 第 4.1 节。目标带来自 docs/balance/balance-model.md（价格带 11-17 行、回本带 48-55 行、研究目标 39-46 行）。表内多值一律用分号分隔。

## 0 方法与折价假设（先读本节再读表）

0.1 回本口径：第一座建筑；满效率；不计科技乘数、工坊倍率、电力/物流/生产力机会成本（与 03 第 4.1 节同口径）；净产=Σ(产出量×单价)−Σ(投入量×单价)，食物产出按粮价折算。
0.2 资源折价表（链深加权，03 §4.1 原值；标注〔扩〕为本报告为覆盖全 40 资源的扩展假设）：
木/石块/黏土/生物质=1；煤=1.5；矿石（铜/锡/铁/铝土/镍精矿）=1.5；钛精矿=15；石砖=2.5；陶=5；布=5；焦炭=2.5；青铜=4；〔扩〕铜锭/锡锭/铁锭=3；钢=6；玻璃=8；混凝土=5；铜线=8；化学品=8；电子=25；机械=15；复合材料=12；铝=8；镍=6；钛合金=45；幻影合金=60；幻影织物=40；相位材料=250；〔扩〕原油=2；精炼燃料=6；橡胶=8；发动机=40；炸药=12；润滑剂=12；火箭燃料=10；食物=2.5（1 粮/s 支撑 1.25 人=2.5 生产力/s，依据 PopulationState.cs:8-9 每人耗粮 0.8/s、供 2 生产力/s）。
0.3 建筑分类带（balance-model §4）：原料 45-120s；加工 90-240s；研究 5-15min（早期口径）；基础设施按新增能力衡量。注意：该带是时代无关的早期口径，对 Spacer 巨构直接套用必判"偏慢"，本表按带判定并加注"时代折算"提示；时代折算后的建议带在第 3 节给出。
0.4 RP 情景（01 §4.1 中性情景，全部取自代码常量）：各时代末 RP=6/54/364/2004/11204，全局研究乘数 G=1.00/1.05/2.40/3.19/3.47，有效点速=6/56.7/873.6/6393/38878 点/s；时代门（AdvancesTechLevel=1，EraGoalEvaluator.cs:177-191）按 ResearchManager.cs:829-834 跨代减速 0.667x（时长×1.5），且用上一时代末产能计算；普通节点用本时代末产能。时长=BaseCost/点速（资源需求为并列闸门，不叠加计时长，单独列出供批次 4 校验）。
0.5 静态局限：绝对时长依赖建筑数量情景；折价对 Spacer 深链材料（幻金/幻布/相位）严重失真，凡净值为负者一律判"存疑"并给出保本折价，不直接当结论。

## 1 建筑首份回本全量表（66 座；造价与净产单位=折算价值）

| 建筑 | 时代 | 造价(价值) | 净产(价值/s) | 回本 | balance-model 带 | 判定 |
|---|---|---|---|---|---|---|
| ClayPit | Animal | 80 | 1.5 | 53s | 原料 45-120s | 带内 |
| Farm | Animal | 50 | 20（粮 8/s） | 2.5s | 原料 45-120s | 偏快（03 §4.1 判达标；粮价口径敏感） |
| FiberGatheringCamp | Animal | 60 | 1.5 | 40s | 原料 45-120s | 偏快（低于下沿 11%） |
| KnowledgeCircle | Animal | 120 | 按研究力 1RP/s | — | 研究 5-15min | 存疑口径（03 §4.4：8.3 RP/千价值，早期带内） |
| Lumberyard | Animal | 170 | 5 | 34s | 原料 45-120s | 偏快 |
| Quarry | Animal | 120 | 2.4 | 50s | 原料 45-120s | 带内 |
| Smokehouse | Animal | 210 | 6.25（粮 2.5/s）＋容量 400 | 34s（仅食物口径） | 基础设施 | 按能力计（食物口径带内） |
| StoneCuttingWorkshop | Animal | 200 | 0.5（产砖 2.5−耗石 2） | 400s | 加工 90-240s | 偏慢 1.7x |
| WoodHouse | Animal | 80 | 按能力（住房） | — | 基础设施 | 按能力计 |
| CeramicKiln | StoneAge | 100 | 3.8 | 26s | 加工 90-240s | 偏快 |
| CharcoalKiln | StoneAge | 550 | −0.8（产煤 1.2−耗木 2） | 永不回本 | 加工 90-240s | 存疑：净值为负；煤折价须 ≥2.5 才保本（03 B9 被支配） |
| CoalMine | StoneAge | 250 | 2.25 | 111s | 原料 45-120s | 带内 |
| Granary | StoneAge | 550 | 5（粮 2/s）＋容量 1000 | 110s（仅食物口径） | 基础设施 | 按能力计（食物口径带内） |
| IrrigationWorks | StoneAge | 1375 | 50（粮 20/s） | 27.5s | 原料 45-120s | 偏快 |
| MetalMine | StoneAge | 320 | 3.9 | 82s | 原料 45-120s | 带内 |
| MetalSmelter | StoneAge | 300 | 2.05 | 146s | 加工 90-240s | 带内 |
| ScribeHut | StoneAge | 650 | 按研究力 5RP/s | — | 研究 5-15min | 存疑口径（03 §4.4：7.6 RP/千价值） |
| StoneHouse | StoneAge | 540 | 按能力（住房） | — | 基础设施 | 按能力计 |
| WeavingWorkshop | StoneAge | 90 | 1 | 90s | 加工 90-240s | 带内 |
| Academy | Medieval | 3975 | 按研究力 40RP/s | — | 研究 5-15min | 存疑口径（03 §4.4：10.5 RP/千价值） |
| Caravanserai | Medieval | 3700 | 按能力（物流 25/s） | — | 基础设施 | 按能力计 |
| Library | Medieval | 1120 | 按研究力 25RP/s | — | 研究 5-15min | 存疑口径（03 §4.4：22.3 RP/千价值，同类最优） |
| SteelForge | Medieval | 1375 | 1.5 | 917s | 加工 90-240s | 偏慢 3.8-10x（03 B5） |
| TownHouse | Medieval | 1940 | 按能力（住房 40） | — | 基础设施 | 按能力计 |
| AdvancedCeramicsPlant | Industrial | 35225 | 9.64 | 3654s（61min） | 加工 90-240s | 偏慢 15-40x（含 3/s 黏土废产出 E7） |
| AluminumSmelter | Industrial | 8200 | 4.2 | 1952s（33min） | 加工 90-240s | 偏慢 8-22x（03 B10） |
| BuildingMaterialsComplex | Industrial | 7600 | 8.75 | 869s（14.5min） | 加工 90-240s | 偏慢 3.6-9.7x（03 B7） |
| CentralPowerStation | Industrial | 7100 | 按能力（300 电/s；燃料耗 9.75 价值/s） | — | 基础设施 | 按能力计（单位电约 0.0225-0.024 价值/电，03 §4.2） |
| ChemicalPlant | Industrial | 4875 | 14.95 | 326s（5.4min） | 加工 90-240s | 偏慢 1.4-3.6x |
| CokeOven | Industrial | 4050 | 0.75 | 5400s（90min） | 加工 90-240s | 偏慢 22-60x（03 B6） |
| IndustrialCarbonizationRetort | Industrial | 13350 | 4 | 3338s（56min） | 加工 90-240s | 偏慢 14-37x（03 B6） |
| IndustrialHabitationComplex | Industrial | 22650 | 按能力（住房 240） | — | 基础设施 | 按能力计 |
| IndustrialMetalSmelter | Industrial | 5920 | 10.2 | 579s（9.7min） | 加工 90-240s | 偏慢 2.4-6.4x |
| IndustrialOilExtractionComplex | Industrial | 26200 | 21.1 | 1241s（20.7min） | 原料 45-120s | 偏慢 10-28x |
| IndustrialStoneworks | Industrial | 23200 | 22.8 | 1017s（17min） | 原料 45-120s | 偏慢 8.5-23x（12 石/s 无后续消费者见资源矩阵） |
| IntegratedPetrochemicalComplex | Industrial | 40800 | 46.5 | 877s（14.6min） | 加工 90-240s | 偏慢 3.7-9.7x |
| MachineFactory | Industrial | 6500 | 16.6 | 393s（6.5min） | 加工 90-240s | 偏慢 1.6-4.4x（03 B7） |
| MechanizedCoalMine | Industrial | 21550 | 11 | 1952s（32.5min） | 原料 45-120s | 偏慢 16-43x（03 B7） |
| MechanizedLumberyard | Industrial | 22250 | 17.1 | 1300s（21.7min） | 原料 45-120s | 偏慢 11-29x（03 B7） |
| MechanizedTextileMill | Industrial | 22600 | 28.6 | 791s（13.2min） | 加工 90-240s | 偏慢 3.3-8.8x |
| NickelRefinery | Industrial | 16780 | −2.0（产镍 5.4−耗 7.4） | 永不回本 | 加工 90-240s | 存疑：净值为负；镍折价须 ≥8.2 才保本 |
| OilDerrick | Industrial | 4000 | 5.0 | 794s（13.2min） | 原料 45-120s | 偏慢 6.6-17.6x |
| OilRefinery | Industrial | 7500 | 8.8 | 852s（14.2min） | 加工 90-240s | 偏慢 3.6-9.5x |
| PlantingField | Industrial | 18200 | 118（粮 48/s＋生物质 18−化 2） | 154s | 原料 45-120s | 略慢 1.28x（贴上沿） |
| RailHub | Industrial | 8700 | 按能力（物流 100/s＋粮仓 2500；维护 4.9 价值/s） | — | 基础设施 | 按能力计 |
| RareMetalMine | Industrial | 9850 | 43.8 | 225s（3.75min） | 原料 45-120s | 偏慢 1.9-5x（与 B1 生产力 12 异常并存，回本偏快于同档矿场 8.7 倍） |
| SteamPlant | Industrial | 3350 | 按能力（120 电/s；燃料耗 3 价值/s） | — | 基础设施 | 按能力计 |
| TitaniumMetallurgicalComplex | Industrial | 24320 | −24.8（产钛合金 20.25−耗 45.0） | 永不回本 | 加工 90-240s | 存疑：净值为负；钛合金折价须 ≥100 才保本（03 B2/§4.1 收率 18.75%） |
| University | Industrial | 5840 | 按研究力 250RP/s | — | 研究 5-15min | 存疑口径（03 §4.4：42.8 RP/千价值；B3 支配 Spacer 研究建筑） |
| WireMill | Industrial | 4500 | 14.35 | 314s（5.2min） | 加工 90-240s | 偏慢 1.3-3.5x |
| DeepSpaceObservatory | Spacer | 140950 | 按研究力 220RP/s | — | 研究 5-15min（早期口径） | 存疑：0.85 RP/千价值（本折价口径），03 §4.4 判 1.8；受 B3/E4 |
| DeepSpaceRelay | Spacer | 161600 | 物流净 −23/s（产 25−耗 48） | 永不回正 | 基础设施 | 存疑：物流产出 25 疑似与消耗 48 写反（资产行 31-32），需运行时核对 |
| EarthMoonLogisticsHub | Spacer | 116300 | 按能力（物流 320/s；maxAmount=1 奇观） | — | 基础设施 | 按能力计（03 B8：growth 1.01 地雷） |
| InterstellarTheoryNexus | Spacer | 3121500 | 按研究力 900RP/s | — | 研究 5-15min（早期口径） | 存疑：0.29 RP/千价值；B3/B4（growth 1.30） |
| LaunchCenter | Spacer | 54450 | 按能力（战役/发射） | — | 基础设施 | 按能力计 |
| OrbitalAgroecologyArray | Spacer | 462600 | 2008（粮 1800＋生物质 232−耗 24） | 230s | 原料 45-120s | 偏慢（巨构定位；03 §4.1 判合理） |
| OrbitalCarbonizationComplex | Spacer | 165800 | 9.3（产焦 45−耗 35.7） | 17790s（4.9h） | 加工 90-240s | 偏慢约 74-198x；焦折价须 ≥2.0 保本（已达标但回本仍极慢） |
| OrbitalCryogenicPropellantArray | Spacer | 723940 | 58.8（产箭燃 140−耗 81.2） | 12303s（3.4h） | 加工 90-240s | 偏慢（时代折算后仍慢；箭燃折价须 ≥5.8，已达标） |
| OrbitalHabitatMegastructure | Spacer | 424900 | 按能力（住房 3000；维护耗 26.6 价值/s） | — | 基础设施 | 按能力计 |
| OrbitalResourceExtractionArray | Spacer | 2087640 | 679 | 3073s（51min） | 原料 45-120s | 偏慢 26-68x（八资源枢纽；带为早期口径） |
| OrbitalSolarArray | Spacer | 60720 | 按能力（360 电/s；维护 6.3 价值/s） | — | 基础设施 | 按能力计（单位电 0.0175 价值/电，03 §4.2） |
| OrbitalStation | Spacer | 456000 | 按能力（物流 230/s＋粮仓 30000；维护 55.6 价值/s） | — | 基础设施 | 按能力计 |
| PhantomMaterialsFabricator | Spacer | 49420 | −32.6（产幻金幻布 5.2−耗 37.8） | 永不回本 | 加工 90-240s | 存疑：净值为负；幻金+幻布组合折价须约 7.3 倍才保本（深链折价失真） |
| PhaseMaterialSynthesisArray | Spacer | 173400 | 计投入 −17.9；不计投入 2.0 | 永不回本；不计投入约 24h | 加工 90-240s | 存疑：相位折价须 ≥2483 才保本；03 §4.1/E3 同向 |
| QuantumComputingArray | Spacer | 246100 | 按研究力 500RP/s | — | 研究 5-15min（早期口径） | 存疑：2.0 RP/千价值；B3/B4（growth 1.28） |
| Shipyard | Spacer | 274600 | 按能力（舰队产能；维护 16.6 价值/s） | — | 基础设施 | 按能力计 |

1.1 带内/带外统计：66 座中可算回本者 44 座：带内 10、偏快 6、略快 1、略慢 1、偏慢 25、净值为负 5（CharcoalKiln、NickelRefinery、TitaniumMetallurgicalComplex、PhantomMaterialsFabricator、PhaseMaterialSynthesisArray）＋物流净负 1（DeepSpaceRelay）；研究 8 座与基础设施 14 座按能力/研究力口径另计。偏慢最重：OCC 约 4.9h、PMSA（不计输入约 24h）、CokeOven 90min、ICR 56min、ACP 61min、OREA 51min、ASM 33min、MCM 32.5min。

## 2 科研目标时长全量表（129 条；时长=BaseCost/点速，门节点×1.5）

2.1 情景：见 0.4。目标带仅 balance-model 覆盖 TL0-TL2；TL3/TL4 文档无带，标"无带"并给出观察。【重点】标记者为 Spacer 47 条与 TechnologicalSingularity。

| 科研 | 时代 | BaseCost | 资源需求 | 预估时长 | 目标带 | 判定 |
|---|---|---|---|---|---|---|
| ControlledFire | Animal | 60 | 木20 | 10s | 首项 30-90s | 偏快 |
| Quarry | Animal | 120 | 木200 | 20s | 首项 30-90s | 偏快 |
| ClayExtraction | Animal | 240 | 木40 | 40s | 首项 30-90s | 带内 |
| AnimalHusbandry | Animal | 240 | 木80 | 40s | 首项 30-90s | 带内 |
| Agriculture | Animal | 300 | 木28 | 50s | 首项 30-90s | 带内 |
| StoneTools | Animal | 700 | 木30；石35 | 117s | 原始普通 2-8min | 偏快 |
| HerbalKnowledge | Animal | 850 | 生物质100；黏土40 | 142s | 原始普通 2-8min | 带内 |
| Woodworking | Animal | 1100 | 木84；石35 | 183s | 原始普通 2-8min | 带内 |
| KnowledgeSharing | Animal | 1200 | 无 | 200s | 原始普通 2-8min | 带内 |
| Mathematics | Animal | 1700 | 木98；石49 | 283s | 原始普通 2-8min | 带内 |
| Mining | Animal | 1800 | 木180；石80 | 300s | 原始普通 2-8min | 带内 |
| FoodPreservation | Animal | 1800 | 木120；黏土120 | 300s | 原始普通 2-8min | 带内 |
| Calendar | Animal | 1900 | 木126；石63 | 317s | 原始普通 2-8min | 带内 |
| VillageOrganization | Animal | 2200 | 木200；石100；黏土50 | 367s | 原始普通 2-8min | 带内 |
| OrganizedDefense | Animal | 2400 | 木220；石160 | 400s | 原始普通 2-8min | 带内 |
| NaturalPhilosophy | Animal | 2800 | 木140；黏土40 | 467s | 原始普通 2-8min | 带内 |
| StoneAgeSettlement（门 TL0→TL1） | StoneAge | 1600 | 木2000；石1200；黏土800；生物质800 | 400s（×1.5 门减速，TL0 产能） | 原始跃迁 10-20min | 偏快 |
| TextileCraft | StoneAge | 1600 | 生物质50 | 28s | 石器普通 5-20min | 偏快 |
| CeramicFiring | StoneAge | 1800 | 黏土235；煤105；砖140 | 32s | 石器普通 5-20min | 偏快 |
| Measurement | StoneAge | 3200 | 黏土80；石60 | 56s | 石器普通 5-20min | 偏快 |
| FoodStorage | StoneAge | 4200 | 木300；石200 | 74s | 石器普通 5-20min | 偏快 |
| CharcoalMaking | StoneAge | 6000 | 木250；黏土120 | 106s | 石器普通 5-20min | 偏快 |
| WrittenRecords | StoneAge | 6500 | 黏土84；木182；陶14；布7；砖35 | 115s | 石器普通 5-20min | 偏快 |
| AnimalFodder | StoneAge | 7000 | 生物质300；陶100 | 123s | 石器普通 5-20min | 偏快 |
| Masonry | StoneAge | 8500 | 木308；石182；砖280；黏土140 | 150s | 石器普通 5-20min | 偏快 |
| IrrigationEngineering | StoneAge | 11500 | 砖210；黏土266；陶70；木126；石84 | 203s | 石器普通 5-20min | 偏快 |
| VillageCrafts | StoneAge | 12000 | 铜200；砖250；木250 | 212s | 石器普通 5-20min | 偏快 |
| CropRotation | StoneAge | 13000 | 陶200；布120；黏土200 | 229s | 石器普通 5-20min | 偏快 |
| CouncilGovernance | StoneAge | 14000 | 陶200；布150；砖350 | 247s | 石器普通 5-20min | 偏快 |
| Smithing_Copper | StoneAge | 15000 | 铜矿168；煤308；砖140；锡矿154 | 265s | 石器普通 5-20min | 偏快 |
| OrganizedWatch | StoneAge | 17000 | 青铜250；砖300；木200 | 300s（5.0min） | 石器普通 5-20min | 带内下沿 |
| Smithing_Iron | StoneAge | 19000 | 铁矿300；煤420 | 335s（5.6min） | 石器普通 5-20min | 带内 |
| Smithing_Bronze | StoneAge | 21000 | 铜126；锡126；煤210；木175 | 370s（6.2min） | 石器普通 5-20min | 带内 |
| FeudalAdministration（门 TL1→TL2） | Medieval | 130000 | 砖5000；布2000 | 3439s（57.3min；×1.5 门减速，TL1 产能） | 石器跃迁 20-60min | 带内上沿 |
| MechanicalEngineering | Medieval | 13392 | 钢3500；青铜1610；砖4200 | 15s | 中古无单节点带（阶段 4-12h） | 极端偏快（成本脱离时代带 10-15x，01 BAL-7） |
| Steelmaking | Medieval | 18144 | 铁矿2100；煤18200；铁11200；黏土14000；青铜700 | 21s | 同上 | 极端偏快（同 BAL-7） |
| UrbanHousing | Medieval | 135000 | 砖9000；钢1800；布2500 | 154s | 同上 | 阶段口径偏快 |
| GuildSystem | Medieval | 150000 | 钢2500；布3000；陶2500 | 172s | 同上 | 阶段口径偏快 |
| StandingArmy | Medieval | 150000 | 钢2500；青铜2000 | 172s | 同上 | 阶段口径偏快 |
| PublicHealth | Medieval | 175000 | 布5000；陶4000；砖6000 | 200s | 同上 | 阶段口径偏快 |
| Bookmaking | Medieval | 200000 | 陶1050；布5600；钢2450；木6300 | 229s | 同上 | 阶段口径偏快 |
| Fortification | Medieval | 210000 | 砖16800；钢4550；青铜2100 | 240s | 同上 | 阶段口径偏快 |
| ScholasticInstitutions | Medieval | 220000 | 布4550；陶1750；砖3500；青铜2100；钢1260 | 252s | 同上 | 阶段口径偏快 |
| TradeRoutes | Medieval | 220000 | 布4550；青铜2100；砖6300；钢2100；木4200；陶1750 | 252s | 同上 | 阶段口径偏快 |
| Gunpowder | Medieval | 270000 | 煤12600；钢5600；青铜2800 | 309s | 同上 | 阶段口径偏快 |
| SteamPower | Industrial | 75000 | 钢6000；煤600 | 12s | 无带 | 极端偏快（同代最便宜） |
| IndustrialWorkshop | Industrial | 90000 | 钢5000；青铜2000；陶2000 | 14s | 无带 | 极端偏快 |
| PetroleumExtraction | Industrial | 110000 | 钢5000；青铜2400 | 17s | 无带 | 极端偏快 |
| Coking | Industrial | 140000 | 煤16000；砖8000 | 22s | 无带 | 偏快 |
| IndustrialChemistry | Industrial | 240000 | 焦4200；钢2800；陶2800 | 38s | 无带 | 偏快 |
| DeepOilDrilling | Industrial | 420000 | 钢12000；化2000；原油6000 | 66s | 无带 | 偏快 |
| IndustrialExplosives | Industrial | 460000 | 钢10000；焦6000 | 72s | 无带 | 偏快 |
| ConcreteEngineering | Industrial | 460000 | 砖11200；化1680；钢5600；陶2100；机4520 | 72s | 无带 | 偏快 |
| ElectricalEngineering | Industrial | 300000 | 铜10000；钢2000 | 47s | 无带 | 偏快 |
| IndustrialHabitationEngineering | Industrial | 520000 | 钢40000；混28000；陶16000；电子10000；机12000 | 81s | 无带 | 偏快 |
| PrecisionManufacturing | Industrial | 520000 | 钢11200；铜线7000；陶4200；化2100 | 81s | 无带 | 偏快 |
| RailwayEngineering | Industrial | 560000 | 机2100；发动机360；钢16800；焦4200 | 88s | 无带 | 偏快 |
| ModernMedicine | Industrial | 650000 | 陶60000；化36000；玻璃24000；电子18000 | 102s | 无带 | 偏快 |
| ElectricalCommunication | Industrial | 650000 | 铜线10500；电子4620；玻璃1400；机1680 | 102s | 无带 | 偏快 |
| Standardization | Industrial | 620000 | 钢12000；青铜6000 | 97s | 无带 | 偏快 |
| ScientificMethod | Industrial | 980000 | 钢4000；青铜3000；陶6000 | 153s | 无带 | 偏快 |
| IndustrialResearchCoordination | Industrial | 1000000 | 电子10000；机7000；玻璃6000；铜线4000；化3000；钢12000 | 156s | 无带 | 偏快 |
| RareMetalResourceDevelopment | Industrial | 1250000 | 混7000；钢8400；机2800；电子1400；化1400 | 196s | 无带 | 偏快 |
| AluminumMetallurgy | Industrial | 1300000 | 铜线4200；陶2520；化3920；钢4200；机1680；混24000 | 203s | 无带 | 偏快 |
| MilitaryIndustry | Industrial | 1350000 | 机3500；化5600；橡胶2800；发动机700；钢11200 | 211s | 无带 | 偏快 |
| FactoryOrganization | Industrial | 1450000 | 机5600；电子7000；布7000；陶2800；钢16100；发动机1400；化3500 | 227s | 无带 | 偏快 |
| CombustionEngines | Industrial | 1500000 | 发动机1400；精燃10500；润3500；机2800 | 235s | 无带 | 偏快 |
| LogisticsManagement | Industrial | 1200000 | 机6720；电子3500；发动机560；钢3500；铜线2100 | 188s | 无带 | 偏快 |
| IndustrialAgriculture | Industrial | 1150000 | 机7700；化2800；发动机980；钢5600；精燃3500 | 180s | 无带 | 偏快 |
| AdvancedCeramicEngineering | Industrial | 1800000 | 陶6000；玻璃5000；焦3600 | 282s | 无带 | 偏快 |
| NickelHydrometallurgy | Industrial | 1650000 | 镍精14000；化8400；钢5600；铜线3500；陶2800；机2100 | 258s | 无带 | 偏快 |
| ChlorideTitaniumMetallurgy | Industrial | 1900000 | 钛精14000；化9800；焦5600；陶4200；机2100；铜线2800 | 297s | 无带 | 偏快 |
| TitaniumAlloyEngineering | Industrial | 2700000 | 铝4200；镍精1400；机3500；电子2800；陶2100 | 422s | 无带 | 偏快 |
| Industrialization（门 TL2→TL3） | Industrial | 336000 | 钢50000 | 577s（9.6min；×1.5 门减速，TL2 产能） | 无单节点带；中古阶段 4-12h | 存疑：门仅 9.6min，中古阶段整体被拉平（阶段合计约 36min） |
| OrbitalEngineering【重点】 | Spacer | 38400000 | 混24000；铝14000；电子10000；精燃6000；钛合金900；锡18000 | 988s（16.5min） | 无带 | TL4 最便宜档 |
| OrbitalCarbonizationProcessEngineering【重点】 | Spacer | 36000000 | 焦18000；钢70000；机10000；钛合金1300 | 926s（15.4min） | 无带 | TL4 最便宜 |
| OrbitalPowerTransmission【重点】 | Spacer | 108800000 | 钛合金9000；复合7000；电子32000；陶16000；机18000 | 2799s（46.6min） | 无带 | — |
| OrbitalStructuralDynamicsTheory【重点】 | Spacer | 144000000 | 电子18000；机16000；钛合金8000；复合6000；陶8000 | 3704s（61.7min） | 无带 | — |
| OrbitalLogisticsInfrastructure【重点】 | Spacer | 160000000 | 机20000；电子14000；钛合金4000；复合3000；箭燃3000；精燃6000 | 4115s（68.6min） | 无带 | — |
| OrbitalHabitation【重点】 | Spacer | 162000000 | 混36000；铝18000；电子14000；玻璃10000；润5000；钛合金1100；生物质180000 | 4167s（69.5min） | 无带 | — |
| DeepSpaceSurvey【重点】 | Spacer | 154000000 | 钛合金5500；复合4500；箭燃3250；电子18000 | 3961s（66min） | 无带 | — |
| DeepSpaceShipbuilding【重点】 | Spacer | 204000000 | 钢200000；铝24000；机20000；发动机7000；电子14000；钛合金8000；复合6000；化10000；润8000 | 5247s（87.4min） | 无带 | — |
| OrbitalPropellantEngineering【重点】 | Spacer | 198000000 | 箭燃12000；精燃100000；机52000；钛合金9000；电子20000 | 5093s（84.9min） | 无带 | — |
| BioregenerativeLifeSupport【重点】 | Spacer | 285000000 | 生物质120000；陶12000；复合6000；电子18000；玻璃10000；布8000 | 7330s（2.0h） | 无带 | — |
| AutonomousOrbitalMining【重点】 | Spacer | 328000000 | 钛合金11000；复合5000；机28000；电子18000；玻璃12000；铝土160000；镍精100000；钛精22500 | 8436s（2.3h） | 无带 | — |
| PhantomMaterials【重点】 | Spacer | 320000000 | 钛合金4500；复合3250；化9000；电子10000；镍精8000；玻璃7000；橡胶5000；布18000；焦4400 | 8231s（2.3h） | 无带 | — |
| OrbitalAgroecology【重点】 | Spacer | 300000000 | 生物质40000；陶3200；电子1332；复合173；钛合金100；相位33 | 7717s（2.1h） | 无带 | — |
| OrbitalVacuumMetallurgy【重点】 | Spacer | 336000000 | 钛精45000；镍精130000；钛合金21000；复合15000；幻金8000；相位2500 | 8642s（2.4h） | 无带 | — |
| DeepSpaceThermalExchangeTheory【重点】 | Spacer | 308000000 | 电子24000；机18000；陶12000；复合10000；钛合金8000 | 7922s（2.2h） | 无带 | — |
| DeepSpaceFleet【重点】 | Spacer | 360000000 | 钛合金10000；复合7000；机20000；发动机12000；化12000；箭燃6000；电子12000 | 9260s（2.6h） | 无带 | — |
| HomeSystemSurvey【重点】 | Spacer | 382500000 | 钛合金1000；电子8000；铜线5000 | 9836s（2.7h） | 无带 | — |
| GravityAssistTrajectoryTheory【重点】 | Spacer | 380000000 | 电子20000；机18000；箭燃6000；钛合金7500；复合5000 | 9773s（2.7h） | 无带 | — |
| PlanetaryGeologyTheory【重点】 | Spacer | 352000000 | 电子30000；机24000；钛精90000；钛合金10000；陶10000 | 9054s（2.5h） | 无带 | — |
| DeepSpacePowerGridResilience【重点】 | Spacer | 384000000 | 电子28000；机20000；钛合金10000；复合8000；陶10000 | 9878s（2.7h） | 无带 | — |
| DeepSpaceNavigationReliability【重点】 | Spacer | 399000000 | 电子22000；机16000；箭燃4000；钛合金6000；复合4000 | 10263s（2.9h） | 无带 | — |
| SpaceWeatherForecastingTheory【重点】 | Spacer | 420000000 | 电子36000；机28000；陶14000；复合10000；钛合金8000 | 10803s（3.0h） | 无带 | — |
| OrbitalConstructionAutomation【重点】 | Spacer | 420000000 | 钛合金26000；复合18000；幻金3500；电子48000；机38000；陶28000；铝土360000；镍精240000；钛精50000 | 10803s（3.0h） | 无带 | — |
| DeepSpaceBiosecurityTheory【重点】 | Spacer | 450000000 | 电子24000；机18000；生物质80000；陶12000；布10000 | 11576s（3.2h） | 无带 | — |
| PhaseMaterialEngineering【重点】 | Spacer | 525000000 | 幻金6000；幻布5000；钛合金8000；镍20000；电子24000；复合3500；焦60000 | 13502s（3.8h） | 无带 | — |
| DeepSpaceSystemsTheory【重点】 | Spacer | 546000000 | 电子36000；机30000；钛合金18000；复合14000；陶10000 | 14039s（3.9h） | 无带 | — |
| OrbitalTextileFabrication【重点】 | Spacer | 608000000 | 生物质3000000；布1200000；电子100000；钛合金17500；复合12500；幻布4000；相位1250 | 15634s（4.3h） | 无带 | — |
| PlanetaryAtmosphereEngineering【重点】 | Spacer | 630000000 | 电子40000；机32000；生物质100000；钛合金12000；陶12000 | 16202s（4.5h） | 无带 | — |
| DeepSpaceIndustrialIntegration【重点】 | Spacer | 676000000 | 钛合金70000；复合55000；机180000；电子140000；幻金12500 | 17388s（4.8h） | 无带 | — |
| GravitationalCommunicationTheory【重点】 | Spacer | 700000000 | 电子220000；机140000；相位11000；幻金7500 | 18005s（5.0h） | 无带 | — |
| PrecisionMedicine【重点】 | Spacer | 720000000 | 生物质2400000；陶180000；电子90000；复合14000；钛合金8000；幻布3000 | 18519s（5.1h） | 无带 | — |
| InterstellarLogisticsDoctrine【重点】 | Spacer | 742500000 | 钛合金13000；复合9000；幻金6000；相位4500 | 19094s（5.3h） | 无带 | — |
| InterstellarCombatLogistics【重点】 | Spacer | 900000000 | 钛合金16000；复合11000；幻金9000；幻布7000；相位5000；电子24000 | 23147s（6.4h） | 无带 | — |
| InterstellarSupplyChainTheory【重点】 | Spacer | 992000000 | 钛合金21000；复合15000；幻金11000；相位7000；橡胶18000 | 25512s（7.1h） | 无带 | — |
| QuantumComputing【重点】 | Spacer | 1100000000 | 幻金7500；幻布6000；钛合金9000；电子20000 | 28288s（7.9h） | 无带 | — |
| InterstellarOccupationAdministration【重点】 | Spacer | 1120000000 | 生物质3600000；钛合金32500；复合24000；幻金17500；幻布12500；相位11000；电子60000；箭燃9000 | 28802s（8.0h） | 无带 | — |
| FleetDamageControlTheory【重点】 | Spacer | 1105000000 | 钛合金24000；复合17000；幻金13000；相位9000 | 28422s（7.9h） | 无带 | — |
| InterstellarKnowledgeCoordination【重点】 | Spacer | 1300000000 | 电子480000；机360000；相位32500；幻金25000；幻布21000 | 33437s（9.3h） | 无带 | — |
| InterstellarCybersecurityTheory【重点】 | Spacer | 1320000000 | 电子40000；机32000；陶28000；幻金20000；幻布16000 | 33952s（9.4h） | 无带 | — |
| InterstellarRouteControlTheory【重点】 | Spacer | 1218000000 | 电子36000；机30000；陶30000；幻金22000；幻布18000 | 31328s（8.7h） | 无带 | — |
| InterstellarAutonomyCharterTheory【重点】 | Spacer | 1564000000 | 电子40000；机36000；生物质120000；陶28000；幻布20000 | 40227s（11.2h） | 无带 | 单位成本收益最差节点（01 EXP-7） |
| MatterTransmutationTheory【重点】 | Spacer | 2100000000 | 电子4800；机3732；钛合金800；幻金300；幻布233；相位333 | 54002s（15.0h） | 无带 | 尾盘墙开始 |
| PhaseFieldNavigation【重点】 | Spacer | 2400000000 | 幻金2100；幻布1400；钛合金4500；箭燃3000；电子10000；相位900 | 61731s（17.1h） | 无带 | 尾盘墙 |
| PhaseFieldStabilizationTheory【重点】 | Spacer | 3740000000 | 相位8000；幻金6000；幻布5000；钛合金18000；电子12000 | 96175s（26.7h） | 无带 | 尾盘墙 |
| MatterStateControlTheory【重点】 | Spacer | 4500000000 | 相位18000；幻金14000；幻布12000；钛合金42000；电子56000；复合24000 | 115756s（32.2h） | 无带 | 全树第 2 贵；含死效果 DeconstructionReturnRate 0.12（01 BAL-5） |
| InterstellarNavigation（门 TL3→TL4）【重点】 | Spacer | 227500000 | 箭燃1250；钛合金1250；铜线6000；电子7000 | 53386s（14.8h；×1.5 门减速，TL3 产能） | 无带 | 存疑：门时长为前三门的 92 倍（01 §4.3） |
| TechnologicalSingularity（门 TL4→TL5；effects 为空）【重点】 | Ultra | 5.5e11 | 电子64000；钛合金48000；复合28000；相位20000；幻金16000；幻布14000 | 21186000s（约 246 天；×1.5 门减速，TL4 产能） | 无带 | 存疑（最严重）：单节点=全 Spacer 总量 38.24e9 的 14.4x；且无任何效果（01 BAL-1/BAL-3） |

2.2 时代合计与节奏（中性情景）：TL0 19410 点≈54min；TL1 162900≈48min；TL2 1891536≈36min（对照中古阶段带 4-12h，偏快 7-20x）；TL3 30771000≈80min；TL4 38243700000≈11.4 天；门点 400s/57min/9.6min/14.8h/246 天。TL3→TL4 成本跳 1243x 而产能仅 6.1x，是科研树唯一断崖（01 §4.1）。

## 3 结论：偏离带最严重的 10 项（供批次 4 定案；只给目标区间，不改数值）

| # | 对象 | 现状（本表口径） | 建议目标区间（批次 4 定案用） |
|---|---|---|---|
| 1 | TechnologicalSingularity | 约 246 天；=全 Spacer 总量 14.4x；空效果 | 门节点 1-3 天量级；或成本降至全 Spacer 总量 10-30% 并配套 Ultra RP 产能＋至少 1 条真实效果 |
| 2 | InterstellarNavigation 门 | 14.8h（前三门 6.7min/57min/9.6min） | 30-90min；或按"门时长≈上一时代总量的 10-20%"重推 |
| 3 | MatterStateControlTheory；PhaseFieldStabilizationTheory；PhaseFieldNavigation；MatterTransmutationTheory | 单条 15-32h，四条合计约 91h | 单条 1-4h（Spacer 尾盘总量控制在 1-2 天内） |
| 4 | TitaniumMetallurgicalComplex | 净值为负（钛合金须 ≥100 保本） | 加工带回本（时代折算后 15-40min）内闭合：折价重估或提产率二选一定案 |
| 5 | NickelRefinery | 净值为负（镍须 ≥8.2 保本） | 同上口径 15-40min；并复核 B1 同类生产力异常 |
| 6 | PhaseMaterialSynthesisArray | 计投入净值为负（相位须 ≥2483）；不计投入约 24h | 相位全链按 E3 重定（0.05-0.15/s 量级或折价重估），使回本落入时代折算加工带 |
| 7 | PhantomMaterialsFabricator | 净值为负（幻链折价失真） | 幻金/幻布折价按链深重估后回本 30-90min；或下调钛合金输入 |
| 8 | OrbitalCarbonizationComplex；CokeOven；ICR（焦链） | 4.9h；90min；56min | 焦炭折价重估后：焦链建筑回本 15-40min（时代折算） |
| 9 | MechanizedCoalMine；MechanizedLumberyard；AluminumSmelter | 32.5min；21.7min；33min | 原料带时代折算后 5-15min；或下调建造成本 40-60% |
| 10 | MechanicalEngineering；Steelmaking；SteamPower；IndustrialWorkshop | 15s/21s/12s/14s | ≥2-5min（对齐所在时代中位数的 1/5-1/10）；确认是否漏一个数量级（01 BAL-7） |

快侧补充（不进前 10 但需定案）：Farm 2.5s、IrrigationWorks 27.5s、CeramicKiln 26s、Lumberyard 34s 偏快；TL1 科研 13/16 条低于石器普通带下沿；DeepSpaceRelay 物流产 25/耗 48 疑似字段写反。
带口径结论：balance-model 回本/研究带是早期时代口径，TL3/TL4 无带是文档缺口；建议批次 4 按"时代折算带"（原料 5-15min、加工 15-40min、研究单条 10-60min、门 0.5-3h）补入 balance-model.md 后再定数值。

## 4 证据清单

- 建筑资产 66：Assets/Resources/Datas/Building/{Animal 9,StoneAge 10,Medieval 5,Industrial 26,Spacer 16}（Ultra 目录空）；字段模型 Assets/Resources/Script/Data/Building.cs（resourceRequirements/resourceGenerationRates/resourceConsumptionRates/productivityConsumption/researchPowerGranted/foodProductionRate 等）。
- 科研资产 129：Datas/Research/{Animal 16,StoneAge 17,Medieval 12,Industrial 36,Spacer 47,Ultra 1}；Research.cs（BaseCost/resourceRequirements/TechLevel/AdvancesTechLevel）。
- 运行时：ResearchManager.cs:829-834（跨代/门减速）、:1160-1184（RP 公式）；EraGoalEvaluator.cs:177-191（门判定）；PopulationState.cs:8-9（0.8 粮与 2 生产力/人/s）；ProgressionModifierManager.cs:184-188（加法堆叠）。
- 闭合基线：data/content-closure-static.md（Research 81/81+47/47+1/1；Workshop 37+46；Building 50+16；资源 40）。
- 折价与情景沿用：CONTENTADVISE/03-buildings-production.md §4.1；CONTENTADVISE/01-research.md §4.1（中性情景 RP/G 值）。
- 局限：折价静态假设（Spacer 深链失真，已逐项标注保本折价）；时长依赖建筑数量情景（结论对情景不敏感至数量级）；未运行模拟器与 Unity；工坊每建筑研究倍率、幸福乘数需运行时证据。
