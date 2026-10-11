# Kingdom 从新档到现有终点的策划审查

审查日期：2026-10-09。源码基线：`main` / `ee6e1739d7f5a5e9bea1190dbab434f2a4137922`。起始 `git status --short` 为空。

用户目标：只做审查与策划，从游戏最开始一步一步向上，全面检查，不偏离初衷；多个子代理协作，将结论写入文档。本轮只新增审查文档和交接，不修改代码、资产、数值、GUID、存档或项目规则。

## 1. 初衷与判断标准

权威设计入口是 [内容路线图](../content/progression-roadmap.md)，规则仍由根 AGENTS 与现有项目技能维护。本报告是一次策划审查，不成为另一套长期规则。

核心体验：带领鼠族从失落文明的火种重新定居，理解 `资源 → 建筑 → 人口与生产力 → 研究 → 加工链 → 时代目标`，再走向工业与星际。乐趣应来自看懂因果、作出投资选择、看到文明能力变化，以及离开后再回来时理解经营结果。

每项建议都用以下问题检查是否符合初衷：

1. 玩家在这一刻已经知道什么，下一步只需学会什么？
2. 这个行动解决哪一个真实问题，和另一种投入有什么区别？
3. 成功后有什么能看见、能使用的回报？失败时是否知道原因和恢复办法？
4. 旧生产链、人口与资源是否仍有意义？新能力是否建立在已有能力之上？
5. 能否复用当前系统完成，而不引入额外货币、仓储墙、随机任务或整套新玩法？

保留 Food 唯一容量、普通资源长期积累、原子交易、强类型 State/Manager/UI、固定 UI 资产创作、旧产业跨时代价值与剧情不发经济奖励。不同名称或相同效果类型不自动构成同质化；研究还可能解锁建筑或下游前置，必须一起检查。

## 2. 方法、分工与证据边界

| 审查者 | 范围 | 交付与主代理复核 |
|---|---|---|
| 子代理 economy_review | 新档、Animal、StoneAge 准入 | 新档常量、住房/人口/粮食、首链、研究、教程与早期剧情；主代理核对实际初始化、故事 GUID 和首时代门 |
| 子代理 gameplay_review | StoneAge、Medieval、Industrial 准入 | 加工、冶金、科研升级、支线、住房与叙事衔接；主代理核对冶炼炉、Library、工业门和第7章 |
| 子代理 late_progression | Industrial、Spacer、Ultra、现有终点 | 真实解锁顺序、产业供给、星区、文明工程与遗迹使用窗口；由主代理整合到后段 |
| 主代理 | 阶段接缝与贯穿体验 | 当前源码/证据版本、引导/详情、失败恢复、离线/声音/阅读、范围纠偏、统一文档和链接检查 |

三个子代理均只读，未写文件、未启动 Unity。正文不是自动最优路线，也不扩展模拟器策略搜索或自动调参。

证据标签：**静态缺陷**指当前代码/资产即可确认的不一致；**策划风险**指有实现依据、但是否造成不良体验仍需观察；**设计候选**指应先验证需求，不立即实施。阶段路径表示真实依赖与能力顺序，不表示所有支线都必须完成。

| 已有证据 | 本轮读取结果 | 能说明什么 |
|---|---|---|
| [静态闭包报告](../../data/content-closure-static.md) | 文件时间 2026-10-08；Industrial 81/81 研究、37/37 工坊、50/50 建筑；Spacer 47/47、46/46、19/19；Ultra 15/15、3/3、3/3 | 此报告输入下的定义图可达；不是库存、效率、等待或本轮运行验收 |
| [Latest 测试摘要](../../TestResults/Latest-Test-Errors.txt) | 10-08 隔离 G 检出，EditMode 833/833、PlayMode 47/47；排除当时 Story 改动 | 不覆盖后来整合的全部主工作树；不能称为当前全仓通过 |
| [10-09 整合 PlayMode XML](../../TestResults/main-integration-playmode-20261009.xml) | 25/25，配套 [日志](../../Logs/main-integration-playmode-20261009.log) 与 [交接](../../.codex/handoffs/2026-10-09-story-illustrations-handoff.md) 记录正常 exit0 | Story、Onboarding、Relic 相关整合行为；不是完整玩家流程 |
| [10-09 整合 EditMode XML](../../TestResults/main-integration-editmode-r2-20261009.xml) | 162/162；交接记录 XML 后原生退出停滞 | 断言完成，不能把这轮说成正常 exit0 |
| 当前真实经济 parity | `data/economy-parity/` 不存在 | 本轮未有真实 Unity trace/parity 证据；fixture 诊断不能代替 |
| 新档测试代码 | [KingdomPlayModeTests](../../Assets/Tests/PlayMode/KingdomPlayModeTests.cs#L127) 有自然资源住房→农业→农场→保存读回 | 不是玩家十分钟体验，也不是完整 Animal→Industrial 无作弊运行 |

未执行真实 Unity 编译。本轮没有新跑测试、静态闭包或会写报告的模拟命令；只读现有源码、资产、测试声明、XML、日志入口和交接。外部试玩由用户自行验收，不列为代理待完成门槛。后续实施所需内部 Unity 行为验证，与用户外部体验反馈分开记录。

## 3. 阶段总览：必须按这个顺序决策

| 顺序 | 玩家核心问题 | 已有能力与阶段回报 | 先审清再往上走 |
|---|---|---|---|
| 0. 新档观察 | 为什么没有人口？木材从哪里来？ | 基础采集、粮食、科研和资源详情 | 首次等待有目标，首屋回报可解释 |
| 1. Animal | 如何让住房、食物、生产力和加工接起来？ | 农业、采石/切石、黏土、生物质、可选科研/木材扩产 | 首链真实运转、剧情跟得上、首次时代缺口可信 |
| 2. StoneAge | 如何维护多条加工链并投资知识？ | 陶瓷/纺织/冶金、灌溉、石屋、粮仓、文书 | 看懂原料/燃料/人口取舍，不把矿物名称当新机制 |
| 3. Medieval | 先扩钢链、机械化，还是公共/科研建设？ | 专业生产、领土/生产力、住房/卫生/知识支线 | 升级收益可比，支线有理由，合法主线不丢文明记忆 |
| 4. Industrial | 多一座工厂需要怎样的能源物流？ | 工坊、机器、电网、铁路、化工、高级材料、大学 | 引导顺序符合前置，扩产前可解释效率瓶颈 |
| 5. Spacer | 如何把工业供给送到轨道和新据点？ | 轨道综合生产、本星系殖民、远星战役与维修 | 初次据点/远航准入明确；综合厂不成为看不懂的黑箱 |
| 6. Ultra | 如何维持文明工程并使用战略工具？ | 高级制造、阶段认证、工程姿态、遗迹、后解锁战役姿态 | 工具解锁后还有对象，各姿态代价成立 |
| 7. 现有终点 | 我完成了什么，接下来还能做什么？ | 现有工程、据点、剧情状态 | 给现有内容收束，不把 Archotech 框架当已实现阶段 |

下列编号按首次遇到的阶段排列。优先级用“先校正事实 / 再验证体验 / 最后扩展”表达；后段问题即使重要，也不挤占开局审查与修缮。

## 4. 新档观察与第一处定居

### 4.1 真实起点和已有保障

新档原木 60，基础原木产率至少 1/s；Food 300、容量500、基础产粮5/s；人口及人口容量均为0，领土500，没有基础生产力，但基础科研4。依据 [初始化库存](../../Assets/Resources/Script/Manager/GameManager.cs#L86)、[基础木流](../../Assets/Resources/Script/Manager/ResourceManager.cs#L566)、[GameState](../../Assets/Resources/Script/Runtime/GameState.cs#L95)、[人口](../../Assets/Resources/Script/Runtime/PopulationState.cs#L34)、[领土](../../Assets/Resources/Script/Runtime/TerritoryState.cs#L6)、[生产力](../../Assets/Resources/Script/Manager/BuildingManager.cs#L148)、[科研](../../Assets/Resources/Script/Manager/ResearchManager.cs#L33)。

玩家先经历 Orientation 一日，再看 Food/WoodLog 详情。首木屋花80木、3领土、无需生产力/研究，给5人口容量。每人提供2生产力、耗粮0.8/s；一间屋满员耗粮4/s，小于基础5/s，因此第一处聚落有安全缓冲。基础木材仍会生产，不因没有伐木场而软锁。此处没有证据支持赠人口、免费住房或重建开局系统。

### 4.2 开局审查项目

| 编号/身份 | 当前事实与玩家风险 | 最小策划动作及与初衷的关系 | 后续验证标准 |
|---|---|---|---|
| A01 策划风险：首屋到首居民的回报延迟 | 木屋提供容量，不立即给人口；生产建筑受人口生产力限制。[增长公式](../../Assets/Resources/Script/Runtime/PopulationState.cs#L163) 与 [木屋](../../Assets/Resources/Datas/Building/Animal/WoodHouse.asset#L20) 支持此事实，实际等待未测 | 在已有 Population 提示中关联“下一居民增加的生产力”与下一座可建建筑。让等待解释文明增长，不直接送人口 | 记录首屋、首居民、首生产建筑时点；玩家知道为何需要住房，能够辨认等待人口与缺木材 |
| A02 设计候选：首屏信息是否够用而不过量 | 资源按实际库存/流量显示，工坊和星区导航随系统解锁隐藏；Story/Research/Era 开局可见。[导航](../../Assets/Resources/Script/UI/KingdomUIRoot.SceneLayout.cs#L459)、[资源可见性](../../Assets/Resources/Script/UI/KingdomUIRoot.PageRows.cs#L45) | 保留逐步揭示，只核验首屏能读出原木来源、人口0原因、首行动；不先削页面或另造新手主页 | 首屋前不需理解工业/星区；一条既有 Overview 导航能到首个实际阻碍；早期按钮禁用原因清楚 |

开局节奏没有本轮实测。60到80木的差额可作公式估计，不能把估计直接写成玩家首屋耗时。现有里程碑记录器已经区分入队、支付、真实研究推进，后续复用 [ProgressionMilestoneRecorder](../../Assets/Resources/Script/Validation/ProgressionMilestoneRecorder.cs#L74)，不新增遥测系统。

## 5. Animal：从采集到第一条加工链

### 5.1 建议观察顺序，不是强制清单

1. 首屋后人口产生生产力；先观察 Agriculture（28木、研究工作量300）的粮食/增长收益，Farm 与 FiberGatheringCamp 的不同作用。
2. 采石研究开启 Quarry；ControlledFire/StoneTools 开切石。第一次实际资源加工是 Quarry→StoneCuttingWorkshop，分别产 StoneChunk 与持续加工 StoneBrick。
3. 扩住房需要补粮；KnowledgeSharing/KnowledgeCircle 是科研投资；Woodworking+StoneTools 后的 Lumberyard 是扩木材投资。它不是开局无前置可建木源。
4. StoneAgeSettlement 直接前置为 Agriculture、AnimalHusbandry、ClayExtraction；支付2000木、1200 StoneChunk、800 Clay、800 Biomass，再完成研究工作量1600。给300领土、100生产力和人口增长加成，不是只有时代标签变化。[时代定义](../../Assets/Resources/Datas/Research/StoneAge/StoneAgeSettlement.asset#L19)

### 5.2 原始阶段审查项目

| 编号/身份 | 当前事实与玩家风险 | 最小策划动作及与初衷的关系 | 后续验证标准 |
|---|---|---|---|
| A03 策划风险：第一加工链的合计生产力隐藏 | Quarry需6、切石需5，合计11；首屋满员基础10，畜养1.08后10.8仍不足。已有教程补人口分支主要以可用生产力<=0触发。[Quarry](../../Assets/Resources/Datas/Building/Animal/Quarry.asset#L22)、[切石](../../Assets/Resources/Datas/Building/Animal/StoneCuttingWorkshop.asset#L22)、[教程](../../Assets/Resources/Script/Manager/TutorialManager.cs#L775) | 把建下游所缺生产力、住房增长与补粮相连，覆盖“尚有正生产力但不够建目标”的情况。不先降低消耗，保留真实经营题 | 建下游前能看到合计需求并主动补住房/农场；人口/农业投资后可完成链，而非反复点击失败 |
| A04 静态验收缺口：持有链不代表运行链 | 教程只查建筑持有与定义输出/输入相同，不查实际效率或成品产出。[持有链判定](../../Assets/Resources/Script/Manager/TutorialManager.cs#L2347) | 首条链完成与一次真实加工成果对应；复用当前 State/库存，不造全产业任务系统 | 已建但零效率/无原料的链不被描述为已正常运行；已有有效链及读档也能识别成功 |
| A05 静态叙事错位：教程、剧情和主线不是同一经历 | 第4章额外需KnowledgeSharing；第5章需教程链完成及Farm+Lumberyard，两者不是加工链；正文写农场收成被工坊接住。Story严格前章完成才继续，合法进石器可仍卡在旧章。[第4章](../../Assets/Resources/Datas/Story/RememberedKnowledge_04.asset#L21)、[第5章](../../Assets/Resources/Datas/Story/TheFirstChain_05.asset#L21)、[顺序完成](../../Assets/Resources/Script/Manager/StoryManager.cs#L172) | 先决定这是主线文明记忆还是有意支线。推荐将首次研究/加工记忆对齐真实经历并修改对应条件与表达；若保留支线就明示缺项。保留按序永久完成与无经济奖励 | 两条合法原始→石器路径都能理解剧情为何完成/未完成；加工叙述与实际材料关系一致，不为了看故事被迫补隐藏无关建筑 |
| A06 策划风险：ControlledFire近期收益被未来倍率遮住 | 直接效果是石器CeramicKiln倍率，但当前承担黏土、工具与知识路线的前置；不是空研究。[ControlledFire](../../Assets/Resources/Datas/Research/Animal/ControlledFire.asset#L18) | 研究回报拆成“现在开启哪条已有路径”和“以后改善哪个建筑”。先提高收益表达，不新增效果 | 完成时能立即定位真实下游行动；玩家不把当前产量不变误认作失效 |
| A07 策划风险：首次时代门可能退化为四库存等待 | 全额支付四种材料后才研究；已有扩产/科研/人口投资供比较，实际等待未测。[StoneAgeSettlement](../../Assets/Resources/Datas/Research/StoneAge/StoneAgeSettlement.asset#L24) | 先测继续积累与投资上游两种合法人工选择，显示投资能缩短什么等待。一次只校准一个有证据的主要瓶颈 | 至少一种扩产投资在本次跃迁前兑现；支付材料与科研工作分别可辨认；不靠全局倍率缩短所有内容 |
| A08 静态显示缺陷：时代缺口/预计时间未减已有库存 | RemainingAmount=需求-已支付，不是欠缺库存；Era页却以其显示“缺口”并除净流。例如需求100/库存80/净流2/未支付时应欠20、约10秒，当前格式给欠100、约50秒。[求值](../../Assets/Resources/Script/Manager/EraGoalEvaluator.cs#L52)、[显示](../../Assets/Resources/Script/UI/KingdomUIRoot.Era.cs#L273) | 在显示语义里明确剩余应付与尚欠库存；预计只使用尚欠库存。它是可信经营信息的必要修正，不改支付规则 | 覆盖零库存、部分库存、已满足、已付款、零/负净流；预计随库存靠近目标缩短，不把流量估计承诺为完成时间 |

## 6. StoneAge：加工链、储粮和知识记录

### 6.1 实际能力顺序

进入石器已有旧建筑/研究继续生效以及Settlement的直接生产力；不是一切从零开始。Measurement、CeramicFiring、TextileCraft等延伸陶瓷、纺织和石材加工；IrrigationEngineering→灌溉、Masonry→石屋、FoodStorage→粮仓、WrittenRecords→文书提供不同投资用途。

Mining/CharcoalMaking/Smithing_Copper形成矿、燃料与冶炼链。当前MetalSmelter一旦解锁就同时产 Copper/Tin/Bronze/Iron，后续Bronze/Iron研究进一步改善工艺或全局产出。石器阶段没有交易式工坊，不应让玩家寻找尚未开放的 Workshop。中古入口 FeudalAdministration 直接前置 Bronze、Iron、WrittenRecords，支付5000 StoneBrick与2000 Cloth，研究工作量130000。

### 6.2 石器阶段审查项目

| 编号/身份 | 当前事实与玩家风险 | 最小策划动作及与初衷的关系 | 后续验证标准 |
|---|---|---|---|
| B01 策划风险：多条加工链同时出现，软目标不足 | 石器住房/灌溉/储粮/文书有不同作用，长期引导主要回到时代门；工业/太空已有专项引导。[TutorialManager](../../Assets/Resources/Script/Manager/TutorialManager.cs#L849) | 复用准备度与TutorialSnapshot，按真实状态给“先稳供给→选一条投资→准备时代门”建议；不能要求全建筑/全研究 | 粮食、科研或原料短缺时能定位实际对象；供给充足时允许直接推进，软建议不变成额外硬门 |
| B02 策划风险：住房容量看起来是纯增益 | StoneHouse容量12，满载基础耗粮9.6/s；基础产粮5/s，旧Farm基础8/s、灌溉20/s可提供支持。[石屋](../../Assets/Resources/Datas/Building/StoneAge/StoneHouse.asset#L24)、[每人耗粮](../../Assets/Resources/Script/Runtime/PopulationState.cs#L8) | 在住房扩张前解释满员需求及可用供给余量；FoodCapacity只表示缓冲，不能替代产粮。人口成长保留文明扩张感 | 建房后能判断何时需要补粮；拆减住房不会误称立即移除已有居民；不靠仓库墙限制普通资源 |
| B03 策划风险：青铜/铁器研究的“突破”与产出时点不一致 | Copper研究后炉已产青铜与铁；Bronze主要给炉×1.15、Iron全建筑×1.04。并非无效科技。[冶炼炉](../../Assets/Resources/Datas/Building/StoneAge/MetalSmelter.asset#L43)、[Bronze](../../Assets/Resources/Datas/Research/StoneAge/Smithing_Bronze.asset#L30)、[Iron](../../Assets/Resources/Datas/Research/StoneAge/Smithing_Iron.asset#L28) | 先将节点回报解释为合金成熟/铁器标准化，并指出改善哪个已有瓶颈。分步解锁既有炉产出仅作后续备选，需另定范围，不新建一批冶炼厂 | 玩家能解释研究前后能力变化；不把金属名变化当已发生的新机制；若调整产出准入须重验完整source/sink和时代门 |

本阶段应记录两种燃料投入的真实成本和供给，再判断炭窑/煤矿是否形成选择。未实测前不称某一种被另一种全面替代。FeudalAdministration授予领土25，不会自动交付中古科研和城市基础设施。

## 7. Medieval：经营投资与冲工业的选择

### 7.1 实际能力顺序

Steelmaking开SteelForge，并授予领土500、直接生产力300及钢产出改善；MechanicalEngineering需3500 Steel、1610 Bronze、4200 StoneBrick。Bookmaking及TradeRoutes都在机械工程后；Bookmaking开Library及后续学院/卫生，TradeRoutes开贸易设施并参与Guild前置；Fortification→UrbanHousing开TownHouse。

工业时代门 Industrialization 只要求MechanicalEngineering+Steelmaking、50000 Steel和研究工作量336000。学院、城市住宅、行会等是可选投资，不是工业硬门。保持“冲时代”与“先完善社会能力”的合法差别。

### 7.2 中古阶段审查项目

| 编号/身份 | 当前事实与玩家风险 | 最小策划动作及与初衷的关系 | 后续验证标准 |
|---|---|---|---|
| C01 策划风险：进入中古后较多支线仍在机械工程之后 | Library/卫生/学院/贸易路线要先过机械与书籍等节点；数量多不等于入口就有多种可选投入。[Bookmaking](../../Assets/Resources/Datas/Research/Medieval/Bookmaking.asset#L19)、[TradeRoutes](../../Assets/Resources/Datas/Research/Medieval/TradeRoutes.asset#L19) | 在当前软引导展示“钢材供给、机械化、公共建设”实际分支及其后续回报；实测中段等待后才评估挪一项现有前置 | 新时代能看见至少两种当前可行投入；不以不存在的工坊或未解锁建筑当推荐 |
| C02 策划风险：高档科研不是单位生产力更划算 | ScribeHut5科研/5生产力，Library25/36，Academy40/60；高档提高单座科研密度但不提高基础单位生产力科研，解锁高档后低档停建。[文书](../../Assets/Resources/Datas/Building/StoneAge/ScribeHut.asset#L22)、[图书馆](../../Assets/Resources/Datas/Building/Medieval/Library.asset#L22)、[学院](../../Assets/Resources/Datas/Building/Medieval/Academy.asset#L22) | 升级预览并列产出、额外人口生产力、土地、持续投入；保留已有低档和部分升级选择，不放开低档停建、不先加倍率 | 紧缺生产力时能选择暂不升级；宽裕时能说明科研密度收益；比较计入当前定向倍率而非只用基础比值 |
| C03 策划风险：Guild是偏晚的长期科研投资 | 前置Bookmaking/TradeRoutes/Feudal，工作量150000、材料投入较大、唯一全局科研增量0.05，无下游资产引用。前置串联不能当平行重复节点。[GuildSystem](../../Assets/Resources/Datas/Research/Medieval/GuildSystem.asset#L19) | 先解释跨时代收益和冲工业的机会成本。若要形成中古决策，选该节点做定向科研收益/准入位置样板，复用已有类型；幅度等待真实状态核算 | 玩家知道不是工业前必买加速器；选择此项能说明短期代价与后期用途，不要求清空所有叶节点 |
| C04 静态叙事衔接风险：合法冲工业路线会暂缓全部后续记忆 | 第7章要求TownHouse+Academy，但工业门不要求；按序完成让第8章及之后也等待回补支线。晚回补还需核查高档解锁后旧档停建，不能保证随时补得回来。[第7章](../../Assets/Resources/Datas/Story/MedievalOrder_07.asset#L24)、[工业门](../../Assets/Resources/Datas/Research/Industrial/Industrialization.asset#L19)、[Story顺序](../../Assets/Resources/Script/Manager/StoryManager.cs#L179)、[精确持有判定](../../Assets/Resources/Script/Manager/StoryManager.cs#L492) | 将主线记忆条件对齐实际文明经历，或明确叙事准备度及导航。先复用固定18章，不为可选记忆新增章节状态系统，不把学院偷偷加为时代硬门 | 合法进入工业能理解对应记忆是否完成；缺学院/住房时提示准确且有恢复路径；先升级/后补故事也不被旧档持有门卡住；已完成历史不倒退 |

## 8. Industrial：规模、能源物流与工坊

### 8.1 已有方向必须保留

机器、住房、铁路、冶炼、化工、电网、大学、钛合金与工坊共同承担工业闭环。工坊已有购买前后速率预览，资源详情已有生产/消耗明细和满足率；不能把这些现有能力说成未实现。[工坊预览](../../Assets/Resources/Script/UI/KingdomUIRoot.DetailPanel.cs#L803)、[资源详情](../../Assets/Resources/Script/UI/KingdomUIRoot.DetailPanel.cs#L364)

Industrial→Spacer 的时代门是 InterstellarNavigation（其实际资产直接前置TitaniumAlloyEngineering+SteamPower），不是进Spacer后普通研究OrbitalEngineering。实际可开远航还受系统/舰队/供给等条件约束，时代标签不等于全部系统已准备好。[星际导航](../../Assets/Resources/Datas/Research/Spacer/InterstellarNavigation.asset#L19)

### 8.2 工业阶段审查项目

| 编号/身份 | 当前事实与玩家风险 | 最小策划动作及与初衷的关系 | 后续验证标准 |
|---|---|---|---|
| D01 策划风险：三步工业路线与真实前置回溯的顺序不一致 | 作者路径先列精密制造/机器工厂，后列化工/电网等；精密制造实际依赖化工。运行有前置回溯，因此不是死锁。[作者路线](../../Assets/Resources/Script/Manager/TutorialManager.cs#L1046) | “当前/下一步/之后”与真正未完成的前置及经营作用一致；保留已有导航/回溯，不另造路线规划器 | 首次进工业时三步内容均可解释其前置；点当前目标不突然落到未说明的多层化工路径 |
| D02 静态信息缺口：效率百分比缺少行动原因 | 行显示总效率百分比；资源详情跳过零效率建筑流项，停产建筑可能从来源/消费明细消失。[效率显示](../../Assets/Resources/Script/UI/KingdomUIRoot.AuthoredRows.cs#L224)、[零效率过滤](../../Assets/Resources/Script/UI/KingdomUIRoot.DetailPanel.cs#L436) | 在现有详情区分潜在配方、实际流量、造成限制的输入/电力/物流，并保留停产对象的来源说明。不重做分配优先级 | 看见低/零效率时能定位一种真实原因和恢复行动；不会把停产误认成建筑没有用途 |
| D03 策划风险：买工坊的即时增产与再次扩厂不易比较 | 工坊已有按当前数量/效率的收益预览，包含持续原料与约束说明；建造/升级缺同级经营对比 | 把一次工业瓶颈放到“补供给、增一座、买定向工坊”比较中，先比较已有定义，不预设需换数值 | 同一真实库存/数量下，至少两种投入分别适合不同供给状态；无建筑/零效率时不显示虚假即时回报 |

## 9. Spacer：本星系据点、远航与综合工业

### 9.1 先学殖民，再理解远星战争

HomeSystemSurvey、LaunchCenter等条件支持本星系殖民；既有教程会优先续接活动殖民或提示真实可达的首个据点，首据点后返回DeepSpaceFleet/InterstellarNavigation路线。既有Occupied产出、舰队维修、供给、战力、进度与撤退共同构成经营式远征，不需要新增实时战斗。[本星系指引所在入口](../../Assets/Resources/Script/Manager/TutorialManager.cs#L851)

轨道材料综合厂在大量旧材料与人口、电力、物流上建立；普通库存仍可长期积累。远星当前为AlphaCentauri→ProximaB→TauCetiFoundry→SiriusResourceBelt，不能把它说成自由选目标或可无限刷材料。

### 9.2 太空阶段审查项目

| 编号/身份 | 当前事实与玩家风险 | 最小策划动作及与初衷的关系 | 后续验证标准 |
|---|---|---|---|
| E01 策划风险：综合厂多能力共用供给最短板 | 轨道阵列18种材料输出、11种持续材料输入，物流/舰队等同用效率；原料满足率取最小，再受电力物流等约束。[阵列配方](../../Assets/Resources/Datas/Building/Spacer/OrbitalResourceExtractionArray.asset#L62)、[效率计算](../../Assets/Resources/Script/Manager/BuildingManager.cs#L1380) | 保留少数综合厂方向，先讲清“卡哪个投入、影响哪些能力”，并比较上游/能源人口/工坊投资。副产富余不是砍产出的依据 | 用一个真实缺料状态能解释整厂下降；恢复后净流与说明一致；不拆成每资源一厂或加普通仓储 |
| E02 策划风险：探索回报可能只被理解为资源礼包 | 本星系/远星有持续资源、领土、建筑及战役供给等真实差异，但顺序拓展不提供任意目标选择 | 首次据点反馈突出它打开的能力和后续投入；远航预览说明库存能支撑多少、弱战力是否会伤亡、维修后是否可继续。不强迫创造平行星区 | 玩家知道占领后要做什么和为何要维修/补给；缺资格时导航到真实条件，撤退代价先可见 |

## 10. Ultra与现有终点：最后审战略工具，不先扩时代

### 10.1 实际解锁窗口

TechnologicalSingularity直接前置QuantumComputing、PhaseFieldNavigation、MatterStateControlTheory、InterstellarKnowledgeCoordination，推进Ultra；进入后依次把相位能源、分布认知/自主装配、材料制造与远征连续性接回原有工业供给。[时代门](../../Assets/Resources/Datas/Research/Ultra/TechnologicalSingularity.asset#L19)

首座AutonomousMatterFabricator需24000生产力、12000电力，不能把“解锁一个能源建筑”当成“已具备整套供给”。PhaseEnergyArray基础发电1500、内部耗电90；忽略倍率/幸福奖励、假定满效率且不计其他负荷，仅靠它支撑制造巨构时单座净1410，理论至少9座。旧能源、现有建筑数量和真实倍率会改变结果，所以这不是实际强制九座或平衡过难的结论。

文明工程已有阶段启动、持续负荷、按满足率推进、暂停/恢复与认证提交。[阶段定义](../../Assets/Resources/Datas/Ultra/UltraCivilizationEngineering.asset#L20) 中的顺序是：

| 工程阶段 | 既有准入能力 | 稳定满供给理论工期，不含筹备/提交 |
|---|---|---|
| PhaseStabilityCertification | PhaseFieldEngineering + PhaseEnergyArray | 3600秒 |
| MatterAutonomyCertification | 前段认证 + AutonomousMatterAssembly + AutonomousMatterFabricator | 5400秒 |
| CivilizationContinuityCertification | 前段认证 + UltraCampaignContinuity + AdaptiveFleetLogistics + AutonomousMatterFabricator | 7200秒 |

每段还有一次材料、持续材料/Food、电力与物流成本；理论工期不是实测玩家耗时。工程高压在首阶段之后可用；战役姿态必须完整工程Committed之后才解锁，不能把它当首次Spacer远征就存在的选择。[姿态准入](../../Assets/Resources/Script/Manager/UltraProjectManager.cs#L96)

遗迹位于已占领TauCeti，还要求Ultra、研究、巨构和工程首阶段认证；支援只服务活动远星战役。到这个时点正常链最多只剩Sirius，若已占领则没有支援目标。[遗迹准入](../../Assets/Resources/Script/Manager/RelicManager.cs#L49)、[派遣](../../Assets/Resources/Script/Manager/RelicManager.cs#L115)、[拒绝已占领战役](../../Assets/Resources/Script/Manager/SectorManager.cs#L613)

### 10.2 高阶阶段审查项目

| 编号/身份 | 当前事实与玩家风险 | 最小策划动作及与初衷的关系 | 后续验证标准 |
|---|---|---|---|
| F00 策划风险：首巨构没有整套供给预算 | 材料巨构电力/生产力远大于一座新能源阵列提供的能力，当前旧电力与倍率不能漏算。[能源](../../Assets/Resources/Datas/Building/Ultra/PhaseEnergyArray.asset#L29)、[制造](../../Assets/Resources/Datas/Building/Ultra/AutonomousMatterFabricator.asset#L22) | 用当前余量与新增负荷解释能源、生产力、材料筹备，保留整体工程投资感；不按理论九座自动调低成本 | 准备首巨构时能判断是否拖累现有链；首座与追加投资可分解；预算计入真实旧厂、倍率、效率和其他负荷 |
| F01 静态使用窗口缺口：重复遗迹制备通常只有终战可用 | TauCeti之后只有Sirius；调查、永久路线、委托/逆向认证的成本与长期能力缺足够对象。[遗迹](../../Assets/Resources/Datas/Ultra/EchoFoundryRing.asset#L19)、[Sirius](../../Assets/Resources/Datas/Sector/SiriusResourceBelt.asset#L21) | 策划先二选一：定位一次终战准备并匹配成本/表达，或明确调整发现/功能准入给至少两次既有使用机会。后者改变既有G合同，不作为顺手bug修复；不加刷资源战役 | 不先打终战/先打终战两种合法状态都没有误导推荐；若选择长期工具定位，真实进度下两路线均有多次应用机会 |
| F02 条件性数值取舍风险：工程高压更快且总持续成本更低 | 第二/三阶段进度×1.35/1.45，成本×1.25；满供给固定状态下持续材料/Food约为稳定的92.6%/86.2%，一次启动费不变。[阶段](../../Assets/Resources/Datas/Ultra/UltraCivilizationEngineering.asset#L64)、[成本](../../Assets/Resources/Script/Manager/UltraProjectManager.cs#L604) | 先测瞬时电力/物流压力是否构成真实代价，再决定高压要不要以更高总物料换时间。当前有条件优势不等于所有状态必选 | 同状态给两姿态时间、总持续投入和供给缺口；各自有明确适用场景；不靠风险骰子制造差异 |
| F03 条件性数值取舍风险：完整工程后战役稳定更慢更贵 | 高压进度/供给×1.35，稳定进度×0.85/供给×1.15；战力比>=1时两者伤亡零。满供给固定战力稳定总供给约1.353倍基础，高压约1倍。[姿态](../../Assets/Resources/Script/Manager/SectorManager.cs#L1672)、[伤亡](../../Assets/Resources/Script/Manager/CampaignManager.cs#L111) | 先保证姿态解锁后仍有目标，再校准稳定低总/瞬时成本与高压省时的关系；保留低战力下稳定伤亡优势 | 有对象且固定状态下各自存在理由；预览显示总投入，不只显示每秒补给；弱战力/缺料/维修均复核 |
| F04 设计候选：为现有内容收束而非无限推框架 | Overview无下一跃迁时显示最后时代；工程提交后主要引导切姿态；Story已有终章，不能说完全没有结局。[现有引导](../../Assets/Resources/Script/UI/KingdomUIRoot.LiveRefresh.cs#L1022)、[终章](../../Assets/Resources/Datas/Story/TheOldBoundary_17.asset#L15) | 复用工程/据点/剧情状态，说明哪些现有目标已完成、还有哪些选项仍有效。先整理表达，不新增成就经济、转生或Archotech整套机制 | 玩家知道完成的文明能力和可选余项；没有下一时代时不伪造目标；无战役对象时不继续推荐制备/切战役姿态 |

## 11. 从开局开始复查的贯穿体验

这些项目随各阶段首次遇到时检查，不能成为先做后期或新增系统的理由。

| 维度/编号 | 已有事实与保障 | 本次发现或验证重点 | 最小下一动作/通过标准 |
|---|---|---|---|
| G01 研究自主性 | 可排队、取消，自动加前置，队首足额原子付款；不是没有队列。[ResearchManager](../../Assets/Resources/Script/Manager/ResearchManager.cs#L182) | 点远端目标会排多项前置，缺料队首会挡后续；需避免把“排上”误认为“已研究” | 显示队首缺项和已付款/正在推进；从农业开始验证两种合法研究顺序，不新增自动跳队/付款优先级 |
| G02 研究回报 | 真实效果和建筑前置均存在；DescribeResearchRole有直接效果时不一定列出下游建筑。[角色说明](../../Assets/Resources/Script/Manager/TutorialManager.cs#L2276) | 科技同时有倍率和解锁时，玩家可能只看到倍率 | 一项科技完成后在既有反馈给最值得立即使用的能力和真实导航；不为每研究新增弹窗 |
| G03 时代成就感 | 每个时代门有实效，Era区分硬条件与建议；下一时代影响按列表限量展示。[时代预览](../../Assets/Resources/Script/UI/KingdomUIRoot.Era.cs#L241) | 列表前几项不一定是第一个可用或最代表时代的能力 | 从石器起逐时代指定已有“签名能力”，标清跃迁即得/后续建设才得；一段短行动内能用上且预览不误导 |
| G04 粮食与幸福 | 幸福基于人均净粮与可用率，有饱和上限；库存、产率和容量语义不同。[HappinessFormula](../../Assets/Resources/Script/Runtime/HappinessFormula.cs#L61) | 大人口更耗粮又改变人均余粮；高库存不代表长期净流良好，饱和后加成边际下降 | 初次扩第二间房就验证说明；比较库存缓冲/供给净流/人口需求，不增居民职业或复杂需求树 |
| G05 失败与恢复 | 建造失败有类型说明，升级/拆除/研究/工坊由Manager校验；有自定义数量与批量操作。[操作](../../Assets/Resources/Script/UI/KingdomUIRoot.PageRows.cs#L136)、[数量](../../Assets/Resources/Script/UI/KingdomUIRoot.Quantity.cs#L60) | 灰按钮、缺生产力、部分升级、拆除和取消是否可理解；关闭页面业务仍继续 | 按阶段用真实缺料/缺生产力输入验证解释和恢复；不修改交易/退款契约或State权威 |
| G06 离线回归 | 有库存前后、研究、据点、工程及结束时阻塞摘要，0~2h全速、2~8h60%、之后25%为现行结算规则。[结算](../../Assets/Resources/Script/Manager/SimulationManager.cs#L8) | 普通资源按ID排且仅前5项；高级摘要不含遗迹支援准备/服役消费。仅结束状态不能说明停了多久 | 先在原始/石器突出本次目标资源和完成结果；后期才补遗迹变化。不要声称有暂停时间证据，也不扩大离线奖励 |
| G07 剧情与文明身份 | 18章有绑定插画、展开历史和真实条件，不给经济奖励 | A05/C04是条件与经历接缝；Story精确检查定义持有，不沿升级链匹配，延后剧情且升级掉旧档须单独验证；除此之外文明身份的体感需看文本/画面 | 先校正首次采集、人口、科研、加工、时代故事一致性及晚补/升级组合；不把新增图集当核心经营问题的替代 |
| G08 音乐/声音 | 已有音乐控制、音量/静音、普通建造/研究/突破音效。[声音类型](../../Assets/Resources/Script/Manager/MusicManager.cs#L526) | 战略开始/结束、供给暂停、不可逆提交缺专用声音语义；背景乐随机排除当前曲 | 先试听早期成功/失败和研究反馈；后段按真实状态补少数声音，去重且保留静音。情境音乐只作低优先候选 |
| G09 阅读/输入/信息密度 | 既有 authored UI、研究图定位/高亮、滚动、页面位置保留；字号多处固定30 | 静态字号不能证明可读性差；长图、长配方、长剧情、累计Overview正文需分阶段检查 | 用目标横屏检查文本/对比/点击拖动/缩放，先复用现有控件；确认需求后才选舒适阅读偏好，不先全面换风格 |
| G10 重复游玩与长期动机 | 已有可选研究/建设顺序、工业投入、工程/遗迹路线；尚无证据要求另建重置经济 | 合法不同路径能否形成有意义差别，是否只是同一路线换先后 | 先证明原始和中古各两种投资顺序的机会成本；不加转生、每日任务、随机事件或新货币来掩盖主线重复 |

## 12. 排期：小步完成当前阶段，不跳到最远系统

| 批次 | 只做这一批的策划/验证 | 成功后再进入下一批 |
|---|---|---|
| R0 开局事实校正 | A08时代缺口；A05早期剧情/真实链；核查A03生产力正余量不足的提示 | 信息不误导；首屋→人口→农业→有效加工→首时代条件可解释。任何代码/资产改动另按实施授权与领域验证执行 |
| R1 原始节奏与回报 | A01、A03、A04、A06、A07，连同G01~G05早期路径 | 两种合法投资顺序能走到石器；真实支付/研究/生产分开记录；一次只调整一个已证实主要瓶颈 |
| R2 石器经营 | B01~B03；住房满员需求、燃料/冶金/文书；石器签名能力及离线 | 前序问题未重新出现；材料加工和知识投资有明确作用，不强迫全清支线 |
| R3 中古到工业 | C01~C04；科研升级、Guild长期价值、冲工业与先建设的差别 | 两种合法路径都能解释进度与记忆；未以学院/住宅新增硬门 |
| R4 工业闭环 | D01~D03；真实前置顺序、效率根因、扩产/工坊比较 | 工业主线、产供关系与使用反馈正确后，再谈节奏；保持已有工业静态闭合 |
| R5 太空及终点 | E/F与G06~G09高阶部分 | 先确认解锁窗口与供给，再调姿态；最后整理现有终局表达，不预先批量新建未来内容 |

每批记录：当前人口/住房/库存/建筑量/净流/倍率，正在追求的真实目标，可选动作及选择理由，支付/等待/完成时点，刚发生的可见回报，失败原因/恢复行动。复用现有State、日志和里程碑；不搭自动策略评分器。

实施验证顺序仍由现有经济技能规定：定义修改前后静态闭包与确定性诊断；之后真实Unity编译、相关EditMode/非零PlayMode与Console、source/sink/可达性及必要行为检查。纯报告不强行运行Unity。若内部行为/编译/parity失败，先修正确性，不能跳到下一内容里程碑或盲调数值。

## 13. 本轮结论及未验证项

最先需要的不是更多未来机制，而是开局的因果与回报可信：首屋如何变生产力，第一链是否运行，故事是否对应真实经历，时代缺口是否准确。石器/中古接下来验证不同投资是否真的改变当下处境；工业以后才验证跨系统供给和战略工具是否仍有可用窗口。

本报告没有给最终倍率、目标通关小时数或“已更好玩”的保证。没有逐行审查第三方/缓存、没有运行玩家全过程、没有新的Unity编译/测试、没有试听或设备触摸体验。本轮明确的交付是当前证据支持的逐阶段策划审查、可执行的验证标准和受约束的顺序；设计候选不等于实施授权。
