# 剧情/背景内容只读审查（2026-09-11）

> 历史快照：统计、行号和候选问题未按当前源码重新验收，不作为当前待办或实现事实；后续实施须先复现。

## 扫描范围与方法

本报告为只读审查，未修改任何代码、资产、场景、Prefab、`.meta` 或配置文件。

**覆盖范围**

- `Assets/Resources/Datas/Story/`：19 个 `.asset`，其中 1 个 `StoryArchive.asset` 档案资产、18 个章节资产；章节正文合计 3,896 字，单章 200–252 字，平均 216.4 字。
- `Assets/Resources/Datas/Tutorial/`：8 个教程资产，覆盖 `orientation → resources → building → population → research → production-chain → era-goal → long-term` 链。
- `docs/story/`：2 个设定/规范文档，合计 167 行。
- `Assets/Resources/Script/Manager/StoryManager.cs`：740 行。
- `Assets/Resources/Script/Manager/CampaignManager.cs`：181 行，未发现中文或剧情/任务叙事文本。
- `Assets/Resources/Script/Manager/TutorialManager.cs`：2,568 行，审阅其中全部 240 处含中文的行，并区分静态教程、动态引导、时代背景和错误信息。

**方法与交叉验证**

- 以 UTF-8 直接读取 Unity YAML 字段，提取标题、摘要、正文、时代、教程、研究、建筑、工坊和星区条件。
- 用 `.meta` 的 GUID 建立全仓库映射，验证章节资产内 99 个 YAML GUID 引用；全部可解析，无断链。`FrontierSectors_14.asset` 中 `AzurePool` 同时出现在解锁与占领列表，属于两个字段的语义重复，不是坏引用。
- 将 `StoryArchive.asset` 的 18 个章节引用映射回资产名，顺序与 `StoryManager.ExpectedChapterIds` 完全一致。
- 逐章计算正文字数并检查 `StoryManager` 禁用的元叙事词；18 章全部通过 200–300 字与禁用词校验。
- 检查 `€`、U+E000–U+F8FF、U+FFFD、`锟斤拷` 和 `\u` 转义；剧情/教程资产与 `docs/story/` 未发现乱码码点。`TutorialManager.cs` 存在 12 行合法 `\uXXXX` 转义，属于可维护性问题而非乱码。
- 对照 `docs/story/mouse-civilization-revival-arc.md` 与 `docs/story/story-data-authoring.md` 检查时代顺序、探索距离、Food 唯一上限、无 workforce、章节不发放奖励等叙事约束。
- 交叉核对 2026-09-10 旧审查：其“StoryArchive 末三章 GUID 断链”结论已被复核推翻，本次仍确认引用健康；其“Tutorial fallback 文案漂移”和“`\u` 转义混写”结论仍成立。

**范围澄清**：任务描述中的“19 章”应更正为“18 个章节资产 + 1 个 `StoryArchive.asset` 档案资产”。这不是仓库错误，而是计数口径问题；`docs/story/story-data-authoring.md` 也明确当前基线为 18 章。

## 发现清单

| 位置 | 问题 | 建议 | 优先级 |
|---|---|---|---|
| `Assets/Resources/Script/Manager/StoryManager.cs:571`、`:602`、`:667` | 解锁/进度提示只检查 `RequiredResearchIds` 与 `RequiredBuildingIds` 的第一项；多条件章节在第一项完成、后续项未完成时可能给出误导性提示。`GetUnlockHint(chapter)` 还先返回工坊/星区提示，未按 AND 条件逐项排序。 | 提示逻辑应遍历全部研究、建筑、工坊、星区条件，优先返回第一个未满足项；顺序应与真实完成判定一致。 | P1 |
| `Assets/Resources/Datas/Story/FirstFire_01.asset:17`、`:18` | 文案声称“稳定的食物与木材”“储备渐渐充足”，但实际条件只有 `resources` 教程的“查看资源详情”，没有 Farm、Lumberyard 或净产出条件。 | 二选一：把正文改成“开始看清食物与原木的来源与净产出”；或给章节增加真实生产条件。前者改动更小。 | P1 |
| `Assets/Resources/Datas/Story/StoneAgeReturn_06.asset:18` | 正文以现在时写“灌溉、储粮、陶瓷、纺织与文字治理”，但条件仅 `StoneAgeSettlement`、`StoneHouse`、`Granary`；玩家刚进入石器时代时尚未完成灌溉、陶瓷、纺织和文字。 | 将这些内容写成本时代方向或后续承诺，或增加代表性真实前置。不要一次性追加全部条件，避免门槛过重。 | P1 |
| `Assets/Resources/Script/Manager/StoryManager.cs:588`、`:596`、`:622`、`:647`、`:651`、`:680`、`:683` | 解锁提示把任意时代的研究/建筑都称为“工业能力”“工业记忆”；原始、石器、中古、太空和 Ultra 章节也会得到工业措辞。 | 改成时代中性的“这段记忆”“对应能力”，或按 `RequiredEra` 选择措辞；这是文案修正，不应改变判定逻辑。 | P1 |
| `Assets/Resources/Datas/Story/TheFirstChain_05.asset:18` | 正文写“另一处工坊”“加工”，但条件只有 `Farm` 与 `Lumberyard`，没有加工建筑；教程 `production-chain` 又要求生产与加工相连。 | 增加一个真实加工建筑条件，或将正文改为采集、运输与记录之间的协作，不宣称加工链已经闭合。 | P2 |
| `Assets/Resources/Datas/Story/MedievalOrder_07.asset:18` | 正文覆盖贸易、学院、行政、城市住宅、城防、法典和城市钟声，条件仅 `FeudalAdministration`、`TownHouse`、`Academy`。 | 保留时代总述可以接受，但应避免“已经拥有”的强表述；如需强契约，则补充真实前置。 | P2 |
| `Assets/Resources/Datas/Story/IndustrialAwakening_08.asset:18` | “铁路旁的仓棚”早于 `RailwayEngineering`/`RailHub` 的第十一章出现，工业首章就引入铁路意象。 | 改为“货栈旁”“道路旁”，或把铁路相关句子后移到第十一章。 | P2 |
| `Assets/Resources/Datas/Story/FrontierSectors_14.asset:17`、`:18` | 摘要写“近地基地”，正文写“月面”“月球基地”“近地轨道”；`AzurePool.asset` 描述为“近月基地”。月面与近地轨道不是同一概念。 | 统一为“近月基地/月面基地”；“近地轨道”仅在确实指地球轨道时使用。 | P2 |
| `Assets/Resources/Datas/Story/WarBetweenStars_15.asset:18` | 总纲要求该章写“第一次远征受挫、伤亡和撤退判断”；现文有故障、备用船和返航，但没有明确伤亡与撤退决策。 | 补一两句轻量描写即可，不需要新增系统或奖励。 | P2 |
| `Assets/Resources/Script/Manager/TutorialManager.cs:207`、`:229`–`:268`；`Assets/Resources/Datas/Tutorial/Resources.asset:16`–`:18` | 资产版 `resources` 步骤是“理解库存与净产出”，回退链是“理解资源来源”，描述与叙事也漂移。资产加载失败时玩家会看到另一套教程。 | 先同步回退文案；再考虑用测试锁定资产与回退链一致。删除回退链会改变失败路径行为，不属于逻辑不变精简。 | P2 |
| `Assets/Resources/Datas/Story/BeyondTheSky_16.asset:18` | 总纲要求“远征结果带回母星后的资源取舍”，现文主要写太阳前哨和能源回传，母星侧取舍较弱。 | 淡化即可：保留“能源必须传回需要它的地方”，可补一句母星工业如何使用这些成果。 | P3 |
| `Assets/Resources/Datas/Story/TheOldBoundary_17.asset:18` | “两份互相矛盾的记录”在终章首次具体出现，前文没有足够铺垫。 | 在早期遗迹或太空章节中轻量埋一次“记录不一致”的线索；不需要新增章节。 | P3 |
| `Assets/Resources/Datas/Tutorial/ProductionChain.asset:18` | “资源停在仓库里”容易与“普通资源无容量”的规则产生联想；虽未写“仓库满”，但隐喻不精确。 | 改为“停留在账面上”“停留在采集点”或“没有被下游使用”。 | P3 |
| `Assets/Resources/Datas/Story/FirstFire_01.asset:17`、`:18`；`Assets/Resources/Datas/Story/PrologueAshes_00.asset:18`；`Assets/Resources/Datas/Resource/Animal/WoodLog.asset:17` | `WoodLog` 的正式标签是“原木”，部分剧情用“木材”作泛称。泛称可读性更好，但精确指资源时可能漂移。 | 指具体资源时优先用“原木”；泛指建筑材料时可保留“木材”。 | P3 |
| `Assets/Resources/Script/Manager/TutorialManager.cs:787`–`:795`、`:811`、`:859`–`:862`、`:2465`–`:2468` | 12 行中文使用 `\uXXXX` 转义，与同文件直接中文混写，降低检索和审阅效率。这不是乱码，运行时语义正确。 | 在单独的编码清理批次中统一为直接 UTF-8 中文；不改字符串内容。 | P3 |
| `Assets/Resources/Script/Manager/TutorialManager.cs:486`–`:512` | 七个时代的文明背景文案硬编码在 Manager 中。它们是静态内容，但与 `TechLevel` 强绑定。 | 若后续做文案集中管理，可移入轻量定义；当前不建议为了“纯数据驱动”新增资产类型。 | P3 |

## 章节连贯性审查

时代枚举对应：0 原始/Animal，1 石器，2 中古，3 工业，4 太空/Spacer，5 Ultra。当前没有 Archotech 章节；这符合总纲中“Archotech 仅作为远期边界，不虚构玩法”的约束。

| # | 章节 | 时代 | 真实前置摘要 | 衔接评估 |
|---|---|---|---|---|
| 0 | `PrologueAshes_00` | 原始 | 无 | 建立旧文明失落、鼠族复兴的总命题，适合无条件开场。 |
| 1 | `FirstFire_01` | 原始 | `resources` 教程 | 从灾变转入生存循环，主题衔接好；但“稳定储备”强于实际教程条件。 |
| 2 | `WallsAndShelter_02` | 原始 | `WoodHouse` | 从资源认知转入定居，条件与叙事匹配。 |
| 3 | `TheGrowingClan_03` | 原始 | `population` 教程 | 从居所转入人口与代际传承，衔接自然。 |
| 4 | `RememberedKnowledge_04` | 原始 | `KnowledgeSharing` | 从人口转入知识共享，符合“经验不再随个体消失”的主题。 |
| 5 | `TheFirstChain_05` | 原始 | `production-chain` 教程、`Farm`、`Lumberyard` | 主题进入协作链，但缺少加工建筑，叙事范围略大于条件。 |
| 6 | `StoneAgeReturn_06` | 石器 | `StoneAgeSettlement`、`StoneHouse`、`Granary` | 时代门槛明确；灌溉、陶瓷、纺织、文字的现在时表述过早。 |
| 7 | `MedievalOrder_07` | 中古 | `FeudalAdministration`、`TownHouse`、`Academy` | 从定居转入治理与公共制度，方向正确；覆盖面略宽。 |
| 8 | `IndustrialAwakening_08` | 工业 | `Industrialization`、`ConcreteEngineering`、`Coking`、`IndustrialStoneworks`、`IndustrialCarbonizationRetort` | 工业规模与相互等待的主题清楚；铁路意象早于后续章节。 |
| 9 | `WorkshopMemory_09` | 工业 | `PrecisionManufacturing`、`MachineFactory`、`IndustrialStoneworks`、`IndustrialCarbonizationRetort`、`PrecisionTooling` | 精密制造、工坊改良和可重复经验高度匹配。 |
| 10 | `IndustrialPower_10` | 工业 | `SteamPower`、`PowerGridEngineering`、`SteamPlant`、`CentralPowerStation`、`MachineFactory`、`ReinforcedBoilers` | 从机器转向能源责任，条件充分。 |
| 11 | `IndustrialMaterials_11` | 工业 | `IndustrialMetalSmelting`、`RailwayEngineering`、`ElectricalCommunication`、`IndustrialExplosives`、`IndustrialMetalSmelter`、`RailHub`、`SteamPlant`、`StandardizedFreightContainers` | 铁路、冶炼和标准货运形成清晰生命线。 |
| 12 | `IndustrialChemistry_12` | 工业 | `IndustrialChemistry`、`PetroleumExtraction`、`Coking`、`ChemicalPlant`、`RailHub`、`ContinuousDistillation` | 化工收益与污染/责任冲突匹配。 |
| 13 | `IndustrialFrontier_13` | 工业 | `TitaniumAlloyEngineering`、`ChlorideTitaniumMetallurgy`、`TitaniumMetallurgicalComplex`、`ChemicalPlant` | 钛合金与发射准备完成工业到太空的过渡。 |
| 14 | `FrontierSectors_14` | 太空 | `HomeSystemSurvey`、`LaunchCenter`、`ReusableLaunchStages`、解锁并占领 `AzurePool` | 本星系测绘、发射与第一次月面殖民顺序正确；“近地/近月”术语需统一。 |
| 15 | `WarBetweenStars_15` | 太空 | 深空舰队/导航/观测/造船/战斗后勤等研究、`Shipyard`、`DeepSpaceObservatory`、三项工坊、占领 `AzurePool` 与 `Terminus` | 从月面推进到太阳系边缘，补给主题强；伤亡与撤退判断不够显性。 |
| 16 | `BeyondTheSky_16` | 太空 | 太空补给/轨道能源/轨道工程/幻影材料、`OrbitalSolarArray`、`OrbitalPowerBeaming`、占领 `Terminus` 与 `HeliosCore` | 从远方行星转向太阳能源前哨，顺序合理；母星侧取舍可加强。 |
| 17 | `TheOldBoundary_17` | Ultra | `TechnologicalSingularity` 及多项星际研究、`InterstellarTheoryNexus`、两项工坊、占领 `HeliosCore`、`AlphaCentauri`、`ProximaB` | 作为当前可达终章保持克制，没有虚构 Archotech 玩法；矛盾记录的铺垫略晚。 |

整体时代序列为 `0×6 → 1 → 2 → 3×6 → 4×3 → 5`，无倒退、跳章或档案乱序。太空章节遵循“月面 → 太阳系边缘 → 太阳深处 → 系外”的探索距离递进；终章停在 Ultra 并把 Archotech 留作悬念，符合设定文档。

## 乱码与编码问题

- `Assets/Resources/Datas/Story/`、`Assets/Resources/Datas/Tutorial/` 和 `docs/story/` 未发现 `€`、私有区字符、U+FFFD 或“锟斤拷”类双重编码产物。
- `TutorialManager.cs:787`、`:788`、`:794`、`:795`、`:811`、`:859`、`:861`、`:862`、`:2465`、`:2467`、`:2468` 存在合法 `\uXXXX` 转义；C# 编译后会得到正确中文，不是运行时乱码。问题仅是源码可读性和检索性。
- `Assets/Resources/Datas/Resource/Animal/WoodLog.asset:17` 等资源标签也使用 `\uXXXX`，但不在本代理修改范围内；若做全局编码清理，应与 `TutorialManager` 分开评估，避免一次性改动过大。
- 旧审查中 `ResearchManager`、`DetailPanel`、`SectorValidator` 的乱码结论不在本报告扫描范围内；本报告只确认剧情/教程/设定文档自身没有乱码。

## 硬编码文本应迁移项

| 位置 | 现状 | 建议 |
|---|---|---|
| `TutorialManager.BuildDefaultSteps` | 8 个教程步骤的标题、描述、叙事、条件与页面在代码中完整复制一份，作为资产无效时的 fallback。 | 先同步 `resources` 步骤文案，再增加一致性校验；是否移除 fallback 需要产品决策，因为它改变资产缺失时的行为。 |
| `TutorialManager.GetCivilizationContext` | 七个时代的静态背景句硬编码。 | 若后续集中管理文案，可迁移；当前迁移收益低，且会新增数据结构。 |
| `StoryManager` 解锁提示 | 大量动态提示硬编码。 | 不建议整体迁入 `.asset`；这些文案依赖运行时状态与定义标签，适合保留在代码或 UI/Presenter 层，仅修正错误措辞。 |
| `StoryManager` 异常/存档错误信息 | 硬编码中文错误信息。 | 不属于剧情内容，不建议放入 Story/Tutorial 资产。 |

## 死代码清单

| 位置 | 调用点验证 | 结论 |
|---|---|---|
| `StoryManager.cs:47` 的 `StoryChapter.First` | 同文件 `:585`、`:590`、`:612`、`:616`、`:677`、`:681` 均解析到 `StoryManager.First`；`StoryChapter.First` 仓库内零调用。 | 私有方法，可删除；逻辑不变。 |
| `StoryManager.cs:352` 的 `FindNextLocked` | `Assets/Resources/Script` 与 `Assets/Tests` 中仅有定义处命中。 | 仓库内零调用；因是 public static，删除前需确认无外部工程引用。 |
| `StoryManager.cs:630` 的 `GetPreviousLockedChapter` 第三个参数 `tutorial` | 方法体内未读取；调用点 `:578`、`:608` 传入 `TutorialManager.Current`。 | 可移除参数并同步两处调用；逻辑不变。 |
| `TutorialManager.cs:2076` 的 `IsProductionChainEndpoint` | `Assets/Resources/Script` 与 `Assets/Tests` 中仅有定义处命中。 | 私有方法，可删除；逻辑不变。 |
| 剧情/教程资产 | 18 个章节均在 `StoryArchive` 与 `ExpectedChapterIds` 中；8 个教程资产均会被 `Resources.LoadAll<TutorialStepDefinition>("Datas/Tutorial")` 读取。 | 无死章节或死教程资产。 |

## 逻辑不变精简建议

- 删除 `StoryChapter.First`、`TutorialManager.IsProductionChainEndpoint`，并移除 `GetPreviousLockedChapter` 的未用参数；这些均经仓库内调用点验证，不影响现有执行路径。
- 将 `TutorialManager` 的 12 行 `\uXXXX` 转义改为直接 UTF-8 中文，保持字符串值不变，仅提升可读性。
- 不建议把 `BuildDefaultSteps` 的 fallback 删除或抽成共享数据来“精简”；正常路径虽然重复，但 fallback 是异常路径的行为保障，删除会改变资产加载失败时的结果。
- 不建议把动态 `Blocker`/`RecommendedAction` 全部模板化。当前字符串拼接依赖研究状态、建筑状态、资源缺口和星区状态，强行迁入资产会增加模板语义与测试面。

## 明确不建议动的部分及理由

- **不改章节 ID 与 Archive 顺序**：`StoryManager` 和存档 v8 依赖固定顺序与稳定 ID，重命名会引入存档兼容风险。
- **不新增 Archotech 章节**：总纲明确当前没有可引用的 Archotech 研究、建筑或 Workshop；新增会违反“不虚构玩法”的叙事边界。
- **不为普通资源增加仓储叙事**：Food 是唯一有容量语义的资源；`ProductionChain` 的“仓库”只应润色，不应引出资源上限。
- **不把剧情奖励加回 StoryManager**：当前故事只写完成历史，不修改经济、研究、建筑或战斗状态，符合架构边界。
- **不强制所有叙事使用完整定义标签**：正文可用“石材厂”“炭化炉”等自然简称；只有含义冲突时才需要统一。
- **不通过手改 YAML 修改剧情资产**：后续实际修改应在 Unity Editor 中完成，以保留 GUID、序列化格式和 `.meta` 稳定性。
- **不在本批次重构 `CampaignManager`**：该文件没有内嵌叙事文本，本次未发现内容层修改需求。

## 验证记录

- 静态验证：章节顺序、章节 ID、GUID 引用、正文字数、禁用词、编码码点、教程链路、资源/建筑/研究/星区术语映射。
- 未执行真实 Unity 编译；本任务为纯只读内容审查，没有代码或资产行为变更。
