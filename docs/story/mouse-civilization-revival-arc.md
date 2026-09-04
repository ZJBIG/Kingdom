# 鼠族文明复兴：叙事总纲

> 供后续代码、关卡与编剧使用。本文只描述当前仓库已经存在或能由现有系统直接表达的内容；未实现的玩法不得被当成既定事实。

## 一、总命题

鼠族的复兴不是找回一座失落的超级城市，而是把破碎的记忆重新变成可持续的共同生活。玩家每次建造、研究、加工、扩张和远征，都是在回答同一个问题：这一代留下的东西，能否让下一代拥有更多选择？

叙事语气应保持“小型族群面对巨大遗产”的尺度感。资源不是抽象奖励，而是生存、生产链、知识传承与远行的证据；科技不是凭空降临的答案，而是从前置研究、建筑能力、Workshop 改良和真实代价中逐步恢复的方法。冲突的核心不是单纯征服，而是如何在食物、生产、研究、领土、能源、物流与军事准备之间作出可承担的选择。

## 二、系统与叙事的共同约束

- 当前时代由 `GameManager.TechLevel` 管理，顺序固定为：原始时代、石器时代、中古时代、工业时代、太空时代、Ultra、Archotech。
- 研究由 `ResearchManager` 按科技树和资源前置推进；时代跃迁由带有 `AdvancesTechLevel` 的研究完成，不应写成剧情按钮或过场自动赠予。
- 建筑由 `BuildingManager` 实际建造并形成生产、消费、能源、物流、人口与领土结果；普通资源没有仓储上限，只有 Food 有容量语义。叙事不可写“仓库满了所以生产停摆”，应写成供应链、研究、建造成本或运输能力不足。
- Workshop 是工业研究 `IndustrialWorkshop` 解锁后的购买式改良层。它通过研究/升级前置和一次性资源支付，改变生产、建造、物流、战斗或远征效率；它不是独立的时代，也不是凭空出现的新工厂。
- Spacer 的星区由 `SectorDefinition`/`SectorManager` 管理。本星系探索先建立行星与轨道航行图；之后玩家才可解锁、殖民（本地星系）、远征并占领星区。行动消耗持续 Food 与配置资源，远征受有效战力、舰队、军力、供给、能源、物流、敌方强度和伤亡影响。所谓“Battle”在叙事中应表现为持续的远征/战役压力，而非当前不存在的即时战斗场面。
- StoryManager 读取只读档案并把完成历史写入 `GameState.StoryProgress`：章节按 Archive 连续顺序和真实条件永久完成，少数原始时代章节还要求 TutorialStep；故事本身不改变研究、资源、建筑、星区或战斗结果。

## 三、七时代主线

### 1. 原始时代：余烬中的名字

主题：活下来，并让经验不再随个体死亡。

玩家行动：维持 Food，采集 WoodLog、StoneChunk、Clay、Fiber 等基础资源；建立 Farm、Lumberyard、Quarry、ClayPit、FiberGatheringCamp、Smokehouse、KnowledgeCircle、WoodHouse 与 StoneCuttingWorkshop；完成 ControlledFire、StoneTools、Woodworking、KnowledgeSharing、VillageOrganization、Agriculture、AnimalHusbandry、OrganizedDefense 等研究。

冲突：族群仍受季节、食物和脆弱居所威胁；旧文明遗迹只能提供碎片线索，不能替代当前生产。防卫是协作和准备，不写成已有完整军队。

章节节奏：以短章、行动后记忆为主。先是“守住火种”，再是“建第一座能留下来的房子”，随后出现人口与知识传承，最后以研究把散落经验串成方法。对应 StoryManager 的 `PrologueAshes_00`、`FirstFire_01`、`WallsAndShelter_02`、`TheGrowingClan_03`、`RememberedKnowledge_04`、`TheFirstChain_05`；前几章可用 TutorialStep 解锁，不能要求时代跃迁。

真实系统对应：资源生产/消费、建筑首批回本、FoodCapacity、Population/Happiness、研究前置与原始生产链。

### 2. 石器时代：把季节写进土地

主题：从流动的幸存者变成能承担未来的定居共同体。

玩家行动：完成 StoneAgeSettlement、Agriculture、FoodStorage、IrrigationEngineering、CropRotation、CeramicFiring、TextileCraft、WrittenRecords、VillageCrafts 与金属加工分支；建设 StoneHouse、Granary、IrrigationWorks、CeramicKiln、CharcoalKiln、MetalMine、MetalSmelter、ScribeHut、WeavingWorkshop 等。

冲突：定居带来稳定，也带来土地边界、粮食分配、知识记录和守护聚落的责任。Food 仍是唯一有容量的库存；其他物资的压力来自生产链和建造成本，不写成普通资源仓库上限。

章节节奏：以“建立制度—发现代价—留下记录—准备下一次扩张”四拍推进。`StoneAgeReturn_06` 是从生存转向制度的时代门槛；每个新建筑都应成为一段可见的公共承诺，而非背景装饰。

真实系统对应：人口容量与 Food 消耗、定居建筑、陶器/纺织/煤炭/多金属链、研究解锁和领土占用。

### 3. 中古时代：道路、规则与共同尺度

主题：让陌生聚落之间可以长期合作。

玩家行动：完成 FeudalAdministration、TradeRoutes、Bookmaking、GuildSystem、ScholasticInstitutions、PublicHealth、Fortification、StandingArmy、Steelmaking、MechanicalEngineering 与 UrbanHousing；建设 TownHouse、Academy、Caravanserai、Library、SteelForge。

冲突：贸易、行政、城市住房和常备防卫同时争夺资源与研究能力。规则既可能保护合作，也可能固化权力；剧情应保留争论、失败和制度修补，不把“时代跃迁”写成王冠自动授予的胜利。

章节节奏：从道路和交换切入，经过学院/书籍带来的知识扩散，转入城市卫生、城防与标准化组织，最后以工业化的可行性作为悬崖。`MedievalOrder_07` 负责把“规模”转译为“治理”。

真实系统对应：中古时代研究树、城市/教育/贸易建筑、钢铁链、人口与 Food 约束、领土和军事准备。当前静态闭环虽覆盖至 Industrial，动态 pacing 报告仍有失败项，编剧不得把报告中的目标时长写成剧情事实。

### 4. 工业时代：让系统学会自我修正

主题：复兴不再依靠少数天才，而依靠可重复、可维护的文明工程。

玩家行动：建设 SteamPlant、CentralPowerStation、RailHub、MachineFactory、ChemicalPlant、OilDerrick、OilRefinery、WireMill、University、TitaniumMetallurgicalComplex、NickelRefinery 等；研究 Industrialization、FactoryOrganization、MassProduction、PowerGridEngineering、LogisticsManagement、ScientificMethod、ModernUniversity、IndustrialChemistry、TitaniumAlloyEngineering 等。

冲突：速度、规模、污染、能源、物流和战争准备互相牵制。玩家需要让矿山、冶炼、化工、机械、铁路和电力互相接得上；工业不是一键无限产出，而是更长的依赖链与更高的维护责任。

章节节奏：工业时代压缩为六个节点：`IndustrialAwakening_08` 的规模危机、`WorkshopMemory_09` 保存的修复经验、`IndustrialPower_10` 带来的能源共同约定、`IndustrialMaterials_11` 中铁路与冶炼组成的生命线、`IndustrialChemistry_12` 的事故与规矩，以及 `IndustrialFrontier_13` 把责任推向母星之外。每章都由真实的 Research、Building 或 Workshop 前置解锁，但正文只写鼠族经历的事件与代价，不把页面操作或内部 ID 写进叙事。

实现约束：`IndustrialAwakening_08` 必须读取真实 `Industrialization` Research 的完成状态；它不是 TutorialStep ID。只有确实对应玩家引导动作的章节，才使用 `RequiredTutorialStepId`。

工坊前置约束：`WorkshopMemory_09` 使用真实 `PrecisionTooling` Upgrade；其直接 Research 前置是 `PrecisionManufacturing`，而 `IndustrialWorkshop` 只负责解锁 Workshop 系统。剧情条件必须与 `WorkshopUpgrade.requiredResearch` 保持一致，不能用间接前置替代直接前置。

真实系统对应：工业建筑与资源链、ResearchPower、Power/Logistics、WorkshopManager 的升级购买、几何成本增长、Titanium/Nickel 等晚期材料继续消耗早期工业输入。

### 5. 太空时代：边疆不是仓库

主题：离开母星之后，文明必须学会承担距离。

玩家行动：完成 HomeSystemSurvey（本星系测绘），建立太阳系内的观测与航行基础；再完成 DeepSpaceFleet、InterstellarNavigation 及轨道/深空研究，建设 LaunchCenter、OrbitalStation、Shipyard、DeepSpaceRelay、DeepSpaceObservatory、OrbitalSolarArray、OrbitalHabitatMegastructure、OrbitalResourceExtractionArray、PhantomMaterialsFabricator、PhaseMaterialSynthesisArray 等；在 Sector 页面解锁并殖民本地星系星区，随后才进行星际远征和占领。

遗产揭示节奏：本星系测绘只提供线索，不直接交出祖先答案；曙光环让残存的发射设施重新运转，碧池星与终焉星逐步带回可复用的材料和燃料，碎冠带与雷门环把零散发现接入工业与远航，最后在曜心触及日冕采集技术。跨出太阳系后，远征才会把这些碎片拼成更高阶的文明遗产。

冲突：每条补给线都要付出持续成本。远征不是一次点击后的胜利动画：有效战力不足会停滞并承受伤亡，供给、能源和物流不足会削弱战力，舰队损伤还需要维修。星区奖励应被理解为领土、原料、一次性奖励和有限战略流量，不能取代玩家建设的高级生产链。

章节节奏：`FrontierSectors_14` 先写测绘与第一次殖民；`WarBetweenStars_15` 只在前一章完成后写第一次远征受挫、伤亡和撤退判断；`BeyondTheSky_16` 再写远征结果带回母星后的资源取舍。每一段都以一项真实系统动作收束：解锁、殖民进度、开始/暂停远征、修复舰队或占领，而不是凭空出现新能力。

真实系统对应：Spacer 研究/建筑/Workshop、星区前置关系、Colonization、CampaignProgress、CombatRatio、Casualties、Food 与资源持续消耗、占领奖励。

引导承接约束：进入 Spacer 后，Tutorial 必须先引导真实 `HomeSystemSurvey`（本星系测绘），再引导 `DeepSpaceFleet`、`InterstellarNavigation`、`LaunchCenter`、`OrbitalStation`、`Shipyard` 和 `Sectors`；只有完成这段太空主线后，才回退到下一时代的 `TechnologicalSingularity` 目标。

### 6. Ultra：已知边界上的奇点

主题：文明开始触碰极限，但仍不知道极限是否可控。

玩家行动：当前真实内容只有 Ultra 时代的 `TechnologicalSingularity` 研究定义；静态报告显示 Ultra 研究 1/1 可达，Workshop 0、建筑 0。`TheOldBoundary_17` 将在完成这项研究并满足系外驻留条件后作为当前可达的档案终章出现。叙事最多把它写成对既有深空理论、材料、能源、计算和社会协调的综合性研究门槛，并要求玩家以现有 Spacer 生产与研究链承担代价。

冲突：不是“解锁神力”，而是解释、验证与自我约束。奇点研究可以改变文明对自身能力的理解，但在当前实现中没有对应的新建筑、新 Workshop、新资源、新战斗模式或即时世界改写。

章节节奏：短而克制，作为 Spacer 之后的悬念章。前半回看哪些系统已经能被可靠复用，后半留下“奇点究竟是突破还是失控前兆”的问题；不要承诺玩家已经获得时间旅行、现实编辑、意识上传或无限生产。

真实系统对应：仅 `TechLevel.Ultra` 与 `TechnologicalSingularity` 研究解锁/完成状态，以及既有研究、资源支付和系外星区占领。任何 Ultra 专属玩法都必须先有真实定义、Manager、状态和验证，再进入剧本。

### 7. Archotech：远古技术留下的选择题

主题：真正的复兴不是复制祖先，而是决定自己要成为什么样的文明。

玩家行动：当前没有可引用的 Archotech 研究、建筑或 Workshop 定义，也没有可兑现的 Archotech 专属操作。只能将其作为终局叙事边界与长期悬念：玩家面对来自远古文明的矛盾记录，继续用现有国家的选择来解释它们。

冲突：一份记录说祖先因傲慢毁灭，另一份说他们主动沉默以保护后来者。玩家不能靠“正确遗迹”自动得到答案；答案必须由后续真实玩法、资源取舍、远征结果或新增且经过验证的系统承载。

章节节奏：`TheOldBoundary_17` 是当前 Ultra 阶段可达的档案馆式终章/未完结卷轴。它只呈现“边界已被看见”，不宣称 Archotech 内容已经完成。

真实系统对应：当前 Archotech 仍只有 `TechLevel.Archotech` 枚举值，没有可引用的研究、建筑或 Workshop。不得虚构 Archotech 研究成本、建筑效果、Workshop 树、敌人、战斗、资源或结局分支。

## 四、不可兑现的叙事边界

1. 不把 Ultra 或 Archotech 写成当前已有完整玩法。尤其不得虚构 Ultra/Archotech 的建筑、Workshop、资源、战斗、时间操纵、无限生产、意识上传、现实重写或自动结局。
2. 不把 StoryManager 当作经济或战斗系统。它读取只读章节资料，并依据真实条件按顺序写入永久完成历史；章节完成不直接授予资源、研究、建筑、领土或战斗胜利。
3. 不把星区写成无限资源仓库或瞬间征服地图。殖民、远征、占领有不同状态与前置；星际战役需要持续供给，失败/停滞/伤亡都是真实可能性。
4. 不写当前不存在的即时操控战斗、单位编队、外交对话树、角色技能、随机事件系统或剧情分支存档。当前 Battle 叙事应对应 CampaignManager 的确定性比值、进度、供给和伤亡。
5. 不用剧情解释绕过经济规则：普通资源无容量上限，Food 才有容量；人口不等于可分配 workforce；资源短缺应落到实际生产、消费、研究、建造、能源、物流或远征供给。
6. 不把静态可达写成已经完成的玩家体验。当前闭环报告只证明定义图可达；模拟 pacing 有失败项，真实 Unity/设备验收仍需另行证明。

## 五、长期悬念

- 旧文明究竟是因傲慢毁灭，还是因保护后来者而主动沉默？两份记录都可能只说对了一半。
- 遗迹中的“远古技术”究竟是可复现的工程，还是只在特定社会组织与资源链中成立的失效方案？
- 为什么最早的灾变抹去了道路、文字与名字，却留下足以被重新拼接的生产痕迹？
- 星际边疆的警报来自敌对文明、失控遗迹，还是鼠族自身过去留下的自动防卫协议？在现有系统没有确认前，只能保持多解。
- HomeSystemSurvey（本星系测绘）负责建立太阳系内的观测与航行基础；InterstellarNavigation 才打开跨星系航线。它们不应在本星系探索阶段预先讲述星区占领或星际接触，后续的殖民、占领治理和补给选择再逐步回答“谁有资格代表鼠族前往远方”的制度问题。
- TechnologicalSingularity 是恢复祖先能力的终点，还是发现“能力越大，越需要限制”的转折点？
- Archotech 的最终答案不应由石碑替鼠族作出。真正的终局应取决于后续真实系统支持的选择：复原旧文明、维持多中心自治，或创造从未存在过的新秩序。

## 六、给后续实现者的最小接口约定

- 新章节优先复用现有 `StoryChapter` 字段：稳定 `Id`、标题、时代、摘要、正文、`RequiredEra`，只有确有教程动作时才增加 `RequiredTutorialStepId`。
- 章节解锁条件应引用玩家已经完成的研究、建造、Workshop、殖民/远征/占领或教程动作；若需要新的触发条件，先扩展并验证 StoryManager，再写剧本。
- 任何“剧情奖励”必须落到已有 Manager 的明确事务中，并有对应测试；不能在 Story UI 中偷偷修改运行状态。
- 代码、资产、叙事命名应优先使用稳定 ID（如 `IndustrialWorkshop`、`HomeSystemSurvey`、`DeepSpaceFleet`、`InterstellarNavigation`、`TechnologicalSingularity`），中文文案只负责表达，不改变系统含义。
