# 模拟修复与节奏交接（2026-10-18）

## 当前续接入口（2026-10-19）

当前正常新档唯一命令表已验证282条：517小时进入Spacer，565.1664小时完成曙光环，661小时完成碧池星，744小时建成首座幽影材料厂，781小时建成首座相位材料阵列（Spacer后264小时，即11天）。`phase-supported`1/1且runner正常退出；终点后10秒真实tick及33资源终态原始产耗率检查通过，不等于跨世界事件的长期稳态。工业前57命令保持72小时结果，全程77小时工业行动缺口仍在。后文各轮“尚未验证相位”“仍运行”等均为当时历史；当前结果与下一动作以末尾“第十轮”及`data/space-progression-20261018/validation.md`为准。本恒星系后续、首次远征和Ultra仍未新档全通，不把阶段通过写成整项计划验收。

## 本轮范围与输入
用户确认：从新档至工业时代约1–3天，开局反馈密集，后续适合分次游玩；只保留一条策略树，阻止膨胀。沿用既有主线，不新增平行路线、决策AI、搜索或自动调参。设计目标登记在docs/content/progression-roadmap.md；尚非节奏实测结论。
本轮开始前tools/NewEconomySimulator与该路线图无未提交修改。其余工作树已有多轮修改，pre-existing.patch只记录当时已跟踪文件diff，不覆盖未跟踪成果。source-inputs.json记录最终输入指纹。运行代码历史见2026-10-17-runtime-refactor-handoff.md。

## 实现
- SimulationState不再自行实现人口增长/饥荒公式；csproj直接链接现有Runtime PopulationState.cs，复用零时长、模式切换和容量关系处理。游戏源码与资产数值未改。
- ApplyGame以起始粮食和净消耗拆分有粮/断粮时间；整段粮食积分保持一次，恰够覆盖不判饥荒。
- 研究调度共用队首选择/原子付款helper，不跳过未满足前置或材料不足的队首；付款成功才出队。在线完成后启动下一项但不推进；离线使用全窗口判断完成，再按完成耗时扣余量并重建倍率。
- 已激活但未付款的输入与Runtime一致保持等待。队首付款通知在真正成功付款时发出。
- 新增固定fixture回归，不新增路线/策略系统；数值关系或容差比较，研究余量容差1e-5覆盖现有ExpantaNum标量中间值1e-6量化，避免对不同运算顺序作虚假精确要求。
- 字段前后值：无生产/研究资产价格、倍率、稳定ID、GUID或存档schema变化。

## 验证及命令
- dotnet build tools/NewEconomySimulator/NewEconomySimulator.csproj --no-restore：exit0，0错误，18个既有nullable警告（build.log）。
- dotnet run --project tools/NewEconomySimulator/NewEconomySimulator.csproj --no-build -- --json：最终38/38通过，exit0（verified.json）；原有23/23通过（before.json）。
- 将两份模拟器实现临时换回当前HEAD原字节，保留最终fixture复测：38项中10项失败（expanded-before.json，exit1），覆盖恰够粮、模式切换、研究余量/完成边界/队首/付款；finally恢复最终字节后重新编译及38/38复验。原始reproduction为早期34项fixture，不作最终计数。
- tools/codex/content-closure-check.ps1 -ProjectRoot . -ReportPath data/simulation-pacing-20261018/closure.md：exit0；工业Research81/81、Workshop37/37、Building50/50可达。仅静态定义闭包。
- 真实Unity 2022.3.62f3c1：EditMode过滤KingdomLogicTests、ResearchPaymentAutoTests、ResearchPaymentSaveTests、SimulationDeterminismTests、LongHorizonSimulationTests，191/191、0失败/跳过，1.6858602秒（editmode.xml/log）。日志有compile time=1215ms、无error CS；Console的坏存档日志来自预期拒绝测试。写XML后退出挂起，包装60秒仅停止自启动PID，runner exit1（unity-status.txt），最终无Unity进程。不把XML通过称作正常退出。
- git diff --check通过；只读子代理复核无确定新增错误，并已补其要求的研究完成线/准确余量/倍率更新与满住房恰耗尽检查。
- 未运行PlayMode、完整测试或真人体验；未commit/push，未触及真实用户存档。

## 尚未验证与下一步
已补真实Manager的新档至工业固定路线证据，详见第二轮；42.34小时仅对应连续在线、固定串行命令。分次离线推进尚未核验，不能把此结果称作耐玩性验收。第四轮已补人口进度、住房恢复顺序及一个真实Unity同输入tick对照；下一具体动作是沿同一命令表核验分次离线推进。模拟器的post-population幸福度偏差已在第三轮修复。未调整资产数值、不扩展第二条策略树。

## 第二轮完成：真实Manager固定主线trace
新增到既有EarlyVerticalSlicePacingTests文件的NewGame_FixedMainlineTrace，正常新档60木材，经公开建造/研究命令和ManualTick(.1)推进；无SaveManager、不补料、不改状态/资产。只有一条手写顺序表，不作搜索或评分；KINGDOM_RUN_PACING_TRACE=1才运行，默认Ignore，避免日常套件变大。初版Explicit触发本机旧Unity TestRunner的AndFilter.IsExplicitMatch InvalidCastException，无玩法执行；改用诊断分类+显式环境开关，未改框架。开发编译最初用了该NUnit不支持的Is.AnyOf，已改其支持的Or.EqualTo；真实Unity后续已编译并执行。
opening/trace.xml：首600秒1/1通过，0.9089181秒；木屋16.5s、农业完成101.2s、农场123.2s、采石研究完成288.5s、采石场365.4s、第二木屋445.9s。最长日志里程碑间隔80.5s，非玩家无事可做判定。
opening/stone.xml：同一路线扩至22/22命令，2406.4s进入Stone，1/1通过，2.9648214秒；最长日志间隔439.7s来自末段较晚启动纤维生产。这是手写串行路线耗时，不是最早到达时间，不是实际玩家操作时长。两批XML后退出挂起，包装仅清自启动PID，runner1。只读复核确认无补料，指出旧Workshop可污染fixture，现已补Workshop/Simulation不存在断言。
industrial.xml：同一条49命令固定路线已完成49/49，152436.8游戏秒（42.3436小时，1.7643天）进入Industrial；真实Unity EditMode 1/1通过，219.9194443秒，runner正常exit0。源码编译3248ms、无error CS。活动session65367已经结束，不应重新poll或重启。未补料、未设置研究完成/时代，未使用真实存档。
主要节点：StoneAge 2406.4秒；Medieval 34572.3秒；Industrial 152436.8秒。后期主要研究耗时：封建管理22875.4秒、炼钢21245.7秒、机械工程15681.8秒、工业化57957.2秒；工业化付款前积料等待20425.6秒。串行脚本研究时不继续下一条建设，因此不是最快路线、真人操作延迟或纯等待统计。longestMilestoneGap=2662.7秒含人口事件，不是无事可做时长。
opening/mainline-trace.txt、research-timings.csv、mainline-summary.json和source-inputs.json保存输出及输入；review.md记录证据边界。最终49命令版本由主Agent核对；子代理最终独立复核因transport失败，不能称作已完成。
不设置KINGDOM_RUN_PACING_TRACE时，routine.xml共4项：3通过、0失败、1诊断跳过，0.407166秒。XML后退出挂起，60秒仅清本轮PID，runner1；与industrial正常exit0分别记录。当前无本轮Unity进程。
开发编译最终补跑dotnet build Kingdom.DeveloperTests.csproj --no-restore，exit0，0错误、6个既有API废弃警告（opening/compile-verified.log）；替代旧compile.log中的初版NUnit API错误作为最终开发编译结果。
本轮另纠正文案：ExpantaNum当前标量中间值量化为1e-6，不是六位有效数字；余量容差1e-5未变，算法未变。继续保留单主线、无PlayMode/真实存档/commit/push边界。

## 第三轮：人口变化后的幸福度读取一致性
- SimulationCore原来把tick开始时的幸福度用于研究与有粮/断粮两段人口推进。真实GameState.HappinessMultiplier按当前人口动态读取；SimulationManager则特意提前锁定资源奖励以保持投入预算。
- 现在TickFrame保存PreparedPopulation；人口后续阶段与研究使用当前人口修正粮食净流后计算幸福度，资源仍使用准备阶段倍率。直接链接真实Runtime/HappinessFormula.cs，删除模拟器重复幸福度公式。无游戏Runtime源文件、资产定义或存档schema变更。
- 新增在线/离线固定回归：人口从0增至2后，研究期望552.32784；旧实现585.02832，同时检查资源仍按原预算倍率积分。post-population-before.json为40项中38通过、2失败，exit1；修复后post-population-verified.json为40/40、exit0。
- dotnet build tools/NewEconomySimulator/NewEconomySimulator.csproj --no-restore：exit0、0错误、18个既有nullable警告（post-population-build.log）；未恢复包/安装依赖。新输入指纹见post-population-inputs.json。
- 此修改仅.NET诊断器，本段未再启动Unity。第二轮真实Unity编译/工业trace不受此次工具源码变更影响。未运行PlayMode；未commit/push。

## 第四轮：真实快照闭环与基础木材供给
- 快照协议由v3升为v4（独立于游戏存档v1）：新增PopulationChangeProgress和Resource.BaseProductionRate。旧v3必须重新捕获，不作默认兼容或迁移。人口容量从建筑数量派生后再恢复进度，防止首tick容量关系变化清掉小数进度；允许[0,1]，拒绝负值、超过1及NaN。DetailedSummary也包含人口进度。
- 导出器和SnapshotValidation均停止对QueuedResearchIds排序，保留玩家队列语义。真实非字母顺序ControlledFire→Agriculture已由JSON round-trip测试覆盖。
- 真实Unity首次导出失败揭示原校验跨类型强制唯一ID（Quarry建筑/研究合法同名）；现在仅同类型禁止重复ID，引用按类型解析；GUID仍全局唯一。新增反例检查同类型重复、错误类型引用和跨类型共用GUID。
- 真正送入.NET Loader后，又发现两个输入缺口：未建造建筑没有实例时漏零状态；SectorDefinition没有TechLevel却被要求提供时代。导出器现在从定义补齐零数量，不创建Manager状态；Sector仍保留原domain/system/prerequisite语义，不虚构时代。游戏资产/GUID/ID不变。
- 首次真实同输入tick的差异只有WoodLog（模拟0，Unity0.120863）：模拟器遗漏1/s基础生产。v4明确导出BaseProductionRate，模型在满足率、预算纠偏、最终资源积分中计入，不乘建筑专属倍率，照常乘幸福度奖励。
- 追查并复现真实Runtime bug：BuildingManager预算纠偏末尾调用ResourceManager.ResetBuildingRates，清掉了1/s基础木材。现在该方法保留WoodLog的1/s，再叠加实际建筑产出；仅此运行时方法本轮新增改动，文件其他既有修改保留。没有提高生产资产数值。
- Unity反证snapshot-base-before.xml：4项中2通过/2失败，零库存与1e-100库存纠偏后基础木材产率0（期望保留约1）。修复后snapshot-base-final.xml：4/4通过，1.1141146秒，包含快照导出/JSON round-trip。两批XML后退出挂起，包装60秒只清本轮PID，runner1。
- .NET分段反证：snapshot-before.json为46项中40通过/6失败；snapshot-namespace-before.json为47项中46通过/1失败。最终snapshot-final-verified.json为49/49、exit0。早期中间版边界校验漏负值/NaN的异常已修复，不能将这些中间日志当作最终通过。
- snapshot-base-final-input.json及.after.json是修复后Unity导出的同一状态前后（1木屋、1人口、progress0.625、两项等待研究；这是强类型夹具，研究资源置零以测试队首等待，不是正常新档推进路线，未使用真实存档）。snapshot-base-final-comparison.json：.NET直接加载这两份未经加工的完整快照，执行同一个0.1s在线tick；人口、进度、Food、每种资源、active/queue均无差异，exit0。此为一个fixture/一个tick的真实对照，不是完整路线或离线parity。
- 诊断probe源码保存在snapshot-load-probe.txt；只临时替换工具Program.cs入口，finally按原字节恢复，并重新构建默认入口；未创建临时Unity项目。snapshot-build-restored-final.log记录还原后的编译，snapshot-developer-compile-verified.log记录最终开发测试程序集编译（exit0，0错误，6个既有废弃API警告）。
- Runtime修复后的固定主线已重跑：passive-supply-industrial.xml为1/1通过，49/49命令完成，226.3951645墙钟秒，runner正常exit0。Industrial仍在152436.8游戏秒（42.3436小时）完成，StoneAge 2406.4秒、Medieval 34572.3秒均与修复前一致；没有改变价格/产出资产值。源码编译无error CS。session51279已结束，最终无Unity进程。passive-supply-industrial-trace.txt/summary.json为本轮终态证据。
- 第四轮只读子代理复核再次因transport失败，未获得独立结论；主Agent自行核查类型/顺序/库存及源码恢复。未运行PlayMode、未修改真实存档、未commit/push。下一步仍是同一主线的分次离线诊断，不增加第二策略树。

第四轮收尾：所有本轮Unity实例均已结束，Program.cs与原字节一致，git diff --check通过。未建立临时Unity项目或新增策略分支。下一具体动作：在既有NewGame_FixedMainlineTrace同一命令表上记录一个明确标注假设的分次在线/离线时间表，复用SimulationManager.AdvanceOffline，检查回访后研究队首、付款、人口和可继续操作的节点；不新增第二份命令表，不把连续在线42.34h直接称作离线1–3天已验收。


## 第五轮：太空准入解耦与分次回访（实施中）

用户批准太空与后时代方案：首次30分钟，此后每天08/13/21各15分钟；工业1–3天、Spacer至现有Ultra主线2–4周为目标。后者尚无完整新档运行证据。Archotech仅交付完整策划，不创建资产。开工HEAD为ac50a3e，工作树干净；不把上轮已提交修改混为本轮新增。

- 定义：OrbitalEngineering.AdvancesTechLevel 0→1；InterstellarNavigation 1→0并加DeepSpaceFleet；HomeSystemSurvey将舰队前置换为轨道工程；舰队加测绘并移除原Navigation直接前置，否则会构成二节点循环；AlphaCentauri保留碎冠带前置并加HeliosCore。没有改费用、产出、ID/GUID/meta或v1 schema。
- 教程继续推荐本恒星系未完成目标，不在首个据点后直接退出；Spacer概述说明先本地经营再准备远航。时代目标复用现有数据驱动入口。新增真实Manager的轨道时代门、曜心系外准入回归；夹具状态授予不冒充新档路线。
- 开工前before-closure.md完整可达，before-simulator.json passed=true；资产稳定后after-closure.md仍完整可达。首次visits-before运行撞上尚未完整改好的舰队/导航循环，失败已保留，不能当未改资产的节奏基线；完成依赖修正后重新运行。
- 唯一命令表抽出为MainlineCommands，两种时间表共用。原49条串行命令按三次回访在120小时进入工业（visits-initial.xml，1/1通过但节奏目标未达）。先加3文书小屋为85小时（gates-visits.xml）。再配科研与炼钢时，住房不足阻塞Lumberyard（visits-investment.xml，失败反例）。最终补2知识圈、3文书小屋、1木屋、2炼钢炉，57命令于72小时进入工业（visits-supported.xml，1/1，正常runner exit0）。只改手写玩家命令，不调资产或补料。最终测试加入<=3天断言；后续记录另见终态段。
- gates-visits.xml：EditMode 194/194，0失败/跳过，22.6398819秒，runner exit0；包括时代门/星区/教程/受控快照导出。PlayMode两项时代推进与本地导航XML 2/2、2.7214814秒，但XML写出后退出挂起，包装240秒仅结束自己启动的PID，runner失败。不得把该批称作正常退出或完整PlayMode全绿。
- 新SnapshotParityCommand是永久CLI，不替换Program。v4受控fixture导出连续在线10秒及离线60/7260/28860/60秒；5个终态对照全部无差异、exit0。夹具隔离真实存档，完成后删除自己的临时目录。v4不覆盖世界事件、Ultra、遗迹、日历余数、战役pending；不得称为完整新档/全系统parity。默认工具增加CLI参数边界，50/50通过。
- 策划落于docs/content/space-and-beyond-design.md，路线图只保留阶段入口；世界观区分母恒星系/银河内近邻/银河际，Archotech三章列行动、投入、成果、复用与缺口。剧情14–16按真实本地资产纠偏；阶段交付纠正旧v9可选Relic说法，当前仅完整v1。

本轮未创建临时Unity项目、未操作用户存档、未commit/push。正在扩展同表的工业生产/能源/物流/科研闭环至轨道工程；后续本地/远征/Ultra无作弊trace尚未完成，不能以静态闭包或有授予夹具冒充。

### 第五轮续接：工业供给反证与遗迹夹具

- progression-regression.xml：147项，143通过/4失败，459.3203753秒，runner exit2；最终57命令连续在线45088.3s（12.5245h），指定回访259200s（72h）。失败包括轨道路线工业化学因无物流停滞，以及遗迹3项。
- 工业段补TradeRoutes/3Caravanserai；修正机械→建材/玻璃→实验与反应工坊→炼油顺序，明确列工坊采购前置。添加4Farm、UrbanHousing/TownHouse和2Library作为明确投资；不改价格/产出。Workshop在EditMode需显式ResetForLoadForEditor初始化空状态，之前149h的InvalidDefinition是fixture遗漏，不是游戏缺陷（orbital-bootstrap.xml）。
- orbital-logistics.log：480秒超时，无XML；orbital-initialized.xml：28天仍停PrecisionTooling，89/134，runner exit2。后续诊断在连续3天无命令成果/付款时打印缺料、记录节奏缺口并停止，不宣称不可达；增加每回访日志避免超时不可诊断。
- orbital-gap.xml：221h打印BasicMetalIngot约222085/1000000，production3.931198、consumption5.0648；Steel约1049075/2400000。确认供给有净消耗，不是应该继续拉长研究。下一批只补工业feedstock链：额外10CoalMine、2MetalMine、2MetalSmelter、3SteelForge与2CokeOven，其他定义值不变，尚在验证。
- 遗迹fixture原同ID clone经EnsureBuilding被解析回正式资产，未隔离正式消耗。改为真实前置建筑+唯一ID中性供给；零供给选择排除正式前置的产出。6项遗迹在orbital-gap.xml全过；默认付款容差不变。仅600tick跨步材料对照使用600*1e-6局部预算，输出观测Phase0.00012、Phantom0.00024、其余0；不推广至缺料/事务。orbital-gap批共7项6过1节奏失败，XML后wrapper240秒超时仅清自己进程。
- 4个系外Description明确银河近邻和数光年尺度；Story17只改正文一句，仍252字，不改触发/奖励。当前最终after-closure.md重跑exit0，完整可达。工具最终构建与50项回归通过，5个parity对照重跑均exit0，缺快照参数exit2。
- 新证据入口data/space-progression-20261018/validation.md；outputs旧失效证据链接已指向当前报告。当前未创建副本/操作用户存档/commit/push。


### 第五轮再续接：工业材料、升级档、住房与石砖反证

本段读取 `data/space-progression-20261018` 的既有 XML/log/status 和 CLI JSON 后追加，文档续接未启动 Unity。原历史记录保留；Archotech 提案现为五幕（矛盾档案、可重复实验、分散连续、更远恒星、留给后来者），不是已接入章节或资产。

- 工业前 57 条命令未变。feedstock、steel、metal、habitation、stone 的日志都在 t=259200 秒（72h）完成 Industrialization；后续增加的供给命令不倒写原 49 条连续 42.34h 的历史结果。
- `orbital-feedstock.xml`：1失败、64.2531151秒、runner exit2；288h在 `ReinforcedBoilers` 等料。`orbital-steel.xml`：1失败、59.8532081秒、runner exit2；264h仍等该工坊，金属锭约302031/720000，production11.7936/s、consumption12.0172/s。不以继续拉长等待替代供给诊断。
- `orbital-metal.xml`：1失败、76.2900458秒、runner exit2；已越过锅炉，312h停 `RotaryKilns`。`orbital-closure.xml`：1失败、104.0834094秒、runner exit2；413h建 `OilRefinery` 返回 `BuildingTierSuperseded`，后续改当前合法升级档，不放宽 Manager 校验。
- `orbital-habitation.xml`：1失败、190.454543秒、runner exit2；581h在 `IrrigationEngineering` 耗尽七天无命令成果/付款的诊断预算。StoneBrick近0/210，production1.34784/s、consumption约1.360041/s，定位为净供给不足。该预算只约束诊断，不证明路线永远不可达。
- 提前投资石料链后，`orbital-stone.xml`：1失败、119.0630604秒、runner exit2；灌溉已付款并完成，石砖阻断已越过。437.2h前三座 `PlantingField` 建成，第4座因人口590/590、生产力仅52.4而即时失败；这是住房/生产力投资缺口。`orbital-agriculture` 现运行中，在农业段之前增加3居住综合体；本段不写其结果，不另起重复运行。
- 工具现有最终产物：`simulator-final.json` 为53/53、passed=true、apiGaps空；对应 `simulator-final-build.log`。5个真实Unity导出的有授予v4夹具终态对照仍全 passed、firstDifference=null；时间为在线10s、离线60/7260/28860/60s。只覆盖导出字段，不包含世界事件、日历小数、战役pending、Ultra和遗迹，不能写成全系统或正常新档路线parity。
- 已确认 Runtime 的离线殖民60秒 Food 付款问题，正在修复，尚未验证。既有5夹具通过不覆盖此反例，也不能作为该修复的验证。下一具体动作：完成付款边界的最小修复与针对性回归，读取当前agriculture实际终态再决定同表下一投资；在闭环与相关parity通过前不调资产、不推进后时代里程碑。
- 当前仍无正常新档至Spacer/本恒星系/首次远征/Ultra完整trace；2–4周与耐玩性保持目标状态。本段没有改价格、产出、ID/GUID、schema、用户存档，也没有commit/push。

## 第六轮：轨道与曙光环闭环、殖民离线修复及v5对照

本段依据2026-10-18实际XML/log/status与CLI结果续写，文档整理本身没有启动Unity。无价格或产出资产变化；ID/GUID/meta、枚举和游戏存档v1不变，未操作用户存档、创建临时Unity项目或commit/push。

- 同一命令表补齐住房及工业供给后，`colonization-orbital.xml`11/11、0跳过，149.4892712秒，runner正常exit0；206/206命令于517小时进入Spacer。`home-dawn.xml`的新档主线诊断通过，211/211命令于565.1664小时完成曙光环，进入Spacer后48.1664小时、72次会话。首个据点落在太空前期第1–3天设计窗口内；这不证明后续2–4周已达标，也不是最优路线。
- 曙光环日志在线等待采样：材料32116.0秒、研究30169.3秒、供给3113.6秒、等待玩家0秒。离线仅记录完整窗口及端点阻塞，不把这些在线采样外推为整个565小时的精确归因。诊断现三天无命令成果/付款记录`PACING-GAP`，七天才停止，保留已观测长等待，未改变游戏计时器。
- `SimulationManager.AdvanceOffline`发现活动殖民时，复用战役已有的真实tick分片与pending日历/模拟秒数处理，避免60秒一次Food付款超过容量而永久暂停。完成离线余量后再衔接在线；未提高Food容量，也未放宽原子付款校验。小容量但持续供应、缺料暂停、离线余量和战役行为由针对性测试覆盖。
- `home-dawn.xml`整批10/11、runner exit2：唯一失败`OfflineColonizationRemainderIsAppliedBeforeRealtimeResumes`来自参考路径直接推进0.15秒，单次Food付款超过夹具容量。改为真实tick的0.1+0.05秒后，`space-final-targeted.xml`163/163、0跳过，4.6901494秒，runner正常exit0。旧失败保留，不冒称原批全绿。
- 快照诊断schema v4→v5：新增真实tick间隔、日历小数余量及离线pending日历/模拟秒数，导出、加载、校验与终态比较同步。此协议与游戏存档v1独立；旧v4明确拒绝并需重捕获，不做迁移。`parity-v5.xml`11/11、0跳过，4.8555204秒，runner正常exit0。
- `parity-v5/`7组真实Unity受控夹具终态对照全exit0、passed=true、firstDifference=null：在线10秒，离线60/7260/28860/重复60秒，以及曜心活动殖民离线60秒后再在线10秒。不是正常新档路线或逐tick首事件比较，仍不覆盖世界事件、Ultra与遗迹。`simulator-v5.json`核实61/61、passed=true，对应`simulator-v5-build.log`构建完成；较早53项仅为当时版本。
- `space-final-playmode.xml`2/3、runner exit2，离线摘要测试错误读取PrimaryCard；按现有创作UI改读SecondaryCard，不改UI。随后`space-playmode-verified.xml`3/3、0跳过，4.0814341秒，runner正常exit0，覆盖时代、本地指引与离线摘要。当前有真实Unity编译及针对性非零PlayMode证据，不称完整套件已跑。
- 文档同步：模拟器README和规范验证手册均指向v5及真实覆盖边界。验证手册修改前字节备份在`data/space-progression-20261018/document-before-v5/validation-reference.md`，SHA256为`DC156235C74266A9F38856D67AF3D159BF002EB49744DD42A1D952A1B7E0EF3B`；原历史v4通过事实保留。

下一具体动作：只读核对碧池星前置、建造空间及持续Food/能源/物流/材料供给，再沿同一命令表增加可兑现的本地经营动作。每阶段相关行为与parity通过后再向后推进；当前没有碧池星及以后新档耗时结论，不为追目标盲调资产。Archotech仍只交付策划；Spacer至Ultra2–4周和耐玩性仍待后续完整流程与用户体验验收。

## 第七轮：本恒星系荒芜开发合同与碧池星供给反证

用户2026-10-19明确确认：Sol六据点保留开发面积，`ResourceRewards`与`OccupiedResourceRatesPerSecond`全部为空，成功不赠材料、占领不自动采集。母恒星系作为荒芜开发地，后续靠玩家建设；本轮以现有全局建筑及领土空间承接，不新增星球建造、独立地图或殖民地生产子系统。系外既有战利品和有限战略产出另行保留。

- 六资产仅清空占领产出并纠正说明；开发面积、行动费用、前置、ID/GUID保持不变。`home-development-assets.json`逐项确认onlyAuthorizedFieldsChanged=true：DawnRing原1条、AzurePool/Terminus/ShardCrown/ThunderGate各3条、HeliosCore原2条占领产出，全部变0；面积分别仍为500/12000/100000/200000/400000/800000。一次性资源列表继续为空，没有以别的奖励补偿。
- Runtime两处占领资源产出入口跳过HomeSystem；Validator及占领提交拒绝带本地资源收益的定义。此前已占领状态也不再自动产资源，不回退面积与占领成果。新增覆盖六据点“只增面积、不重复增加、资源库存不变”的针对性测试尚待运行，不先写成通过。
- `home-development-closure.md`完整可达：Industrial81研究/37工坊/50建筑、Spacer47/46/19、Ultra15/3/3，33资源可达；这仍是静态闭包。`home-development-simulator.json`核实62/62、passed=true。`home-development-parity-verified/`7组v5受控终态对照均passed=true、firstDifference=null，覆盖边界仍不包含世界事件、Ultra和遗迹。
- `azure-prepared-baseline.xml`214项、213通过/1失败、0跳过，226.0571771秒，runner exit2。213项相关行为均过；唯一失败为新档碧池星节奏诊断。取消本地自动收益后，565.1664小时完成曙光环仍复现；至733.17小时，`S:AzurePool`尚未启动，七天无行动的有界材料准备预算耗尽。RocketFuel约775332.77/1500000.0012，净流0.54424/s；Electronics约487717.22/600000.0012，净流0.13882/s；Composite约1525319.90/900000、AdvancedMetalIngot约4317808.65/1199999.9988，均已足够。这里的preparedBudget为固定命令表出发前准备策略，不是新增Manager准入门槛；不能将诊断上限说成永远不可达。
- 较早`azure-supply.log`仅有600秒wrapper超时及status，没有XML；不把它写成七天材料预算耗尽或用例失败结果，七天反证来自上述prepared-baseline。
- 同一命令表供给投资版`azure-investment`现运行中，共224指令：追加InsulatedWindings、DeepOilDrilling、住房、CokeOvenOptimization、电站、RailHub、WireMill及5座ChemicalPlant。不改费用、建筑生产率或时间目标，不写未结束的结果。只针对已观测燃料/电子净流增加玩家投入，没有策略搜索或自动调参。

下一具体动作：先读取azure-investment真实终态，再执行六据点只增面积/不重复/库存不变的新增回归，确认本地合同、供给与相关parity后才向后扩展。本轮文档续接未启动Unity；当前主工程验证进程由主代理负责，不重复开启。未操作真实存档、临时项目或commit/push。

### 第七轮续接：下游扩产反证与上游供给

- `azure-investment.xml`实际1项失败、286.9539843秒、runner exit2：733.2h仍等燃料，库存1362960.827333/1500000.0012，生产2.288249/s、维护0.1262/s；电子779145.647537已超过600000.0012。不是研究或殖民付款行为失败，仍是固定玩家备料阶段。
- `azure-fuel.xml`158项、157通过/1失败、runner exit2。六据点真实占领回调新增测试`HomeSystemOccupationAddsDevelopmentSpaceWithoutGrantingAnyResource`已通过，相关星区、离线与时代行为均通过；唯一失败是再加第七座化工厂时住房/生产力不足（565.2h、人口封顶、生产力43），不是燃料已足供的证据。
- 沿真实`ApplyResourceRateDelta`核查，建筑生产倍率也放大输入消耗。六化工厂满效需原油24.13575/s、焦炭4.82715/s，且石化联合体、电站等也持续消耗；原三油井/三焦炉无法同时满供。撤掉无上游支撑的第七厂，改为同表追加2居住综合体、3合法OilDerrick、2CokeOven；无定义产率、成本或时长调整。增记原油/焦炭/煤净流及对应厂效率，避免只看最终燃料。
- `azure-feedstock.log`首轮在421h后中断，无XML、无已完成runner状态，不作为用例失败或通过。确认没有遗留Unity进程后，使用新文件名`azure-feedstock-resumed`重跑相同输入；运行中不预写结果。
- 设计澄清：曜心挡首次系外星区，不挡Navigation研究本身；本地经营期间可以研究远航与后续产业能力，研究完成不代表已跨恒星。当前策划实际为Archotech三章；上文“五幕”记录不准确，以`docs/content/space-and-beyond-design.md`三章表为准。

下一步读取补上游的真实终态，然后才延长下一阶段；所有未完成的系外/Ultra新档证据继续保留为缺口。没有操作真实存档、创建临时项目或commit/push。

### 第七轮再续接：碧池星实际闭环与回访空档

- `azure-feedstock-resumed.xml`1项失败、runner exit2：565.19h的旧`B:CokeOven`因工坊已开放高档而返回`BuildingTierSuperseded`。改用1座现有`IndustrialCarbonizationRetort`，不放松Manager校验、不改高档前置。
- `azure-retort.xml`1/1、201.0068065秒、runner正常exit0：230/230同表付费命令完成，661h新档时间、84会话、进入Spacer后144h（第6天）完成Azure。在线等待采样：材料41930.1s、研究30222.7s、供给3447.2s、玩家0s。最长全程行动空档316714.1s（约88h），仍需修复节奏；按日期达标不等于耐玩验收。
- 终态六化工、六油井、三焦炉、一干馏炉与联合体效率均1。燃料生产4.02263/s、维护0.1262/s，实际殖民持续扣料并完成；原油仍生产36.9135/s、消耗40.147375/s，库存约174万，不能把该受控阶段完成说成长线供给无缺口。后续扩张应先补原油净流。
- `home-development-playmode.xml`3/3、4.1038466秒、runner正常exit0，覆盖时代推进、真实本地引导及隔离离线摘要。`home-development-current-closure.md`完整可达；`home-development-current-simulator.json`62/62且passed=true。当前确有真实Unity编译，无新资产费用/产率/时长变更。
- 为修复本地备料88h空档，在同一表加入后续所需的MilitaryIndustry、LightAlloyFrames、BlockSignalling、2大学及轨道能源/物流/推进剂研究，让备料与明确的玩家科研投资重叠。`azure-overlap`运行中，未写结果，工业前57条及此前轨道/曙光环前缀不变。

下一具体动作：读重叠投资版终态与本地行动间隔；确认该阶段相关行为与parity后，才追加Azure后地月物流及Terminus准备。当前未取得Terminus及之后完整新档证据，不能宣布Spacer→Ultra的2–4周目标已验收。

### 第八轮：当前用途优先的购买习惯

用户补充：普通玩家倾向能买就买；价格接近时优先购买当前用途更大的选项。继续沿唯一手写命令表表达这个偏好，不新增自动购买器、策略搜索或路线评分。全程备料只能代表一种保守出发方式，不能称为全部正常玩家的行为；提前出发、持续消费和缺料恢复尚需后续实际流程覆盖。

- `azure-overlap.xml`实际36/36、214.1081255秒，238命令于685h、Spacer后168h完成Azure；测试后退出挂起，wrapper600秒超时，非正常退出。其本地最长行动间隔72h，发生在推进剂完成到出发之间；不能仅靠把远期研究插进路线就称购买行为合理。
- 根据真实原油净消耗与电子等待，在远期研究之前追加第7口OilDerrick及第3/4座WireMill，均是当前可合法购买的低价补缺投入；其后才执行大学与三项轨道研究。工业57命令、206命令轨道前缀、曙光环前缀均未变，现共241条。
- `azure-current-priority.xml`193/193、0跳过、259.168738秒；包含新档Azure诊断、SectorManager、SimulationBudget、SpaceProgression、TutorialManager与ContentVerticalSliceAudit。XML已完成，但Unity退出挂起；`azure-current-priority-status.txt`于2026-10-19 18:12:57确认wrapper最终Failed(Timeout)、600秒超时，不能称正常退出。
- 当前241/241付费命令于661h、84会话、Spacer后144h完成Azure；同时完成MilitaryIndustry、轨道能源/物流/推进剂三项研究。推进剂于624h完成、653h出发，出发前无行动空档29h；对照230命令版87.9761h。整个Spacer段最长行动间隔48h（原测绘研究付款到完成），全程最长277200s（77h，仍在工业段）。这些仅为实际日志间隔，不是无聊与否的体验结论。
- 终态：燃料产4.746703/s、维护0.1262/s；电子产2.981476/s、维护0.673593/s，净2.307883/s；原油产43.06575/s、耗40.147375/s，净+2.918375/s。七油井、六化工、三焦炉、一干馏炉、石化联合体均满效率。当前投入改善供给及等待结构，没有改价格、产率、时长、ID/GUID或存档版本。
- 在线等待采样为材料35716s、研究35791s、供给4093s、玩家0s。离线仍仅记录完整窗口与端点，不将在线采样外推为661h精确分解。三项当前合同PlayMode正常通过及62项确定性回归见上轮证据，产品代码/资产之后未改变。

下一具体动作：本轮wrapper终态已确认600秒超时；后续运行前仍需核对无遗留Unity进程，然后沿同表补Azure后的地月物流与下一行星所需产业投入，优先当前瓶颈。Terminus至Ultra无完整新档证据、工业77h空档及提前出发消费行为继续作为明确缺口，不以当前单一路线宣称一般玩家节奏或2–4周已验收。未操作真实存档、创建临时项目或commit/push。

### 第九轮：碧池星后首座幽影材料厂

- `phantom-first-status.txt`确认Passed；`phantom-first.xml`1/1、0失败/跳过，278.18095秒，runner正常退出。唯一命令表256/256条于744小时、94会话建成首座`PhantomMaterialsFabricator`；自进入Spacer起227小时（约9.458天）。241条Azure前缀逐项与prior XML相同，保留此前可复现开局与本地推进。
- Azure后追加4座工业住房、地月物流中枢、中央电站、第二座钛厂、2所大学，以及`OrbitalStationEngineering`、`DeepSpaceShipbuilding`、`DeepSpaceFleet`、`InterstellarNavigation`和`PhantomMaterials`研究，最后付费建造幽影厂。导航研究完成不等于跨恒星：终点未占领Helios或Alpha，未绕过曜心系外准入。
- 终点后执行100×0.1秒真实tick：幽影合金产出0.108848/s、幽影编织物0.072565/s；钛合金生产1.561726/s、消耗1.1358/s，复合材料1.53333/0.674382，化学品25.508175/9.985575，电子元件2.981476/1.187858。幽影厂效率、电力与物流满足率均为1，Food净流9710.158148/s。此证据说明首厂实际运行并产出，不是长稳态或后续相位材料供给闭环。
- 在线等待采样：材料35716秒、研究44747.4秒、供给4136.6秒、等待玩家0秒；离线仍只按窗口与端点记录。全程最大行动缺口277200秒（77小时）仍在工业段，不能以新阶段通过消除该节奏问题。
- 本轮仅延长同一玩家命令表及其真实产出检查，无资产数值修改、新存档或临时工程。文档续接读取既有产物，未另启动Unity。

下一具体动作：核对相位材料产业的现有前置、真实持续供给与建设空间，再沿同表推进；该阶段尚未验证，不写成通过。Terminus以后本地据点、首次系外与Ultra完整流程仍待逐阶段证据，2–4周及耐玩性继续作为待验目标。

### 第十轮：首座相位材料阵列与全资源终态供给检查

- 首次`phase-first.xml`1/1、0跳过，236.0444443秒，status Passed、runner正常exit0；281命令于781小时、Spacer后264小时建成相位阵列。当时7项指定资源净流断言通过，但全资源日志揭示BasicMetalIngot生产29.814751/s、消耗32.086349/s，因此不能称全链供给已闭合。
- 随后只在同表相位阶段前增加1座当前可合法建造的`MetalSmelter`，并强化终态验证为33资源原始生产率均不低于消耗率、建筑满效率、电力/物流满足及Food净流非负。`phase-supported.xml`1/1、0跳过，230.8331257秒，status Passed、runner正常exit0；282/282命令于781小时、99会话完成，Spacer后264小时=11天。原256命令前缀保持不变。
- 相对256前缀的25+1项玩家投资包括7座住房、林业研究及工坊、机械林场、干馏炉、4座综合矿、2座电站、3座钛厂、1座基础冶炼、1座机械厂、2座幽影厂，以及相位研究和首座阵列。所有投入复用现有资产，不改价格或建筑产率。
- 100×0.1秒终点运行后：BasicMetalIngot产35.777701/s、耗32.086349/s，Coal61.418385/58.761878；PhaseMaterial产0.15144/s；PhantomAlloy0.326544/0.20823、PhantomWeave0.217695/0.17037、TitaniumAlloy3.904315/3.46419、Composite3.06666/2.41752、SpecialMetalOre23.94645/20.823、WoodLog59.761875/43.25505、Coke50.330137/35.136451。Food净流8359.158148/s，相位阵列效率、电力及物流均1。10秒与终态raw率只能证明该输入窗口的实际供给，不等于跨全部世界事件的长期稳态。
- 在线等待采样：材料37453.3秒、研究47491秒、供给4155.7秒、玩家0秒；全程最大77小时工业行动空档仍在，不以相位阶段通过掩盖节奏缺口。
- Archotech完整策划已由设计任务扩写，仍为当前权威文档`docs/content/space-and-beyond-design.md`中的三章；玩家行动、投入、成果、现有系统连接及尚需实现能力均有明确边界。交付仅文档，无新资产或新系统；早先“五幕”历史措辞不作为当前合同。

下一具体动作：先核对`ReusableLaunchStages`至居住设施/推进剂链的既有前置与供给，再沿同表推进；尚未取得该后续阶段证据。未创建新存档或临时工程、未修改资产数值、未commit/push；本次文档续接没有另开Unity。
