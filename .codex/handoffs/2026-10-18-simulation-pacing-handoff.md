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

### 第十一轮：巨构供给闭环、购买优先响应与诊断步长

用户明确普通玩家会优先购买当前用途高的可负担项目，并授权调整有实证的不合理数值。继续复用唯一命令表；无自动购买、路线搜索、评分或第二策略树。另按用户本轮明确要求，把固定路线诊断的在线`ManualTick`步长加长；这只改变测试采样，不改游戏运行时与`AdvanceOffline`实现。

- 在`EarlyVerticalSlicePacingTests.cs`相位/发射扩产前增加1口OilDerrick。100个0.1秒终态tick的巨构路线检查过去因CrudeOil轻微负流失败；追加后`orbital-habitat-reusable.xml`为1/1、0失败/跳过、runner exit0，终态原油141.50175产/136.217125耗，所有资源产耗非负，巨构效率/电力/物流均满足。
- 诊断步长由0.1s→1s→10s；相应将10秒终态采样调整为1次tick、将600秒与每小时检查点换算为新的tick数。`orbital-habitat-step1.xml`、`orbital-habitat-step10.xml`均1/1、runner exit0；1秒版约51.39s，10秒版约28.91s，终态时间约960.0小时/342命令，两者一致。10秒下仍用真实`SimulationManager.AdvanceOffline`推进回访间隔；它不等于游戏实际在线tick配置或玩家体验验证。
- 10秒trace实际量化了发射级门槛：原Reusable要求Rubber 8.1m、PhantomAlloy 131,250、PhantomWeave 93,750。降Rubber到4.5m后，终点从960h提前至949h，购买不再受Rubber阻塞，仍被幽影材料卡住；949h时终点后库存PhantomAlloy 27,174、PhantomWeave 1,429，二者净流仍正。
- 根据trace：864h时PhantomWeave为49,888/93,750、PhantomAlloy为78,381/131,250；逐次访问分别于941h、925h达到改价前门槛。因此Reusable资产只改三笔成本：Rubber 8,100,000→4,500,000；PhantomWeave 93,750→50,000；PhantomAlloy 131,250→80,000。稳定ID/GUID、其他成本、效果和存档v1不变。
- 最终`orbital-habitat-cost-tuned2-step10.xml`为1/1、0失败/跳过、runner exit0，测试时长25.31s。唯一表342/342命令在869.0083h（Spacer后352.0083h≈14.67天）购买Reusable并于同回访建成OrbitalHabitatMegastructure；最长动作间隔从原证据107h降至77h。终态CrudeOil 141.50175/136.217125、PhantomAlloy 0.54424/0.20823、PhantomWeave 0.362825/0.23347、Rubber 16.011626/0.772975、TitaniumAlloy 7.027767/6.30369；巨构效率/电力/物流=1，Food净流4842.358148；测试还逐项断言全33资源在10秒窗内无负净流。此局部trace不证明全事件长期稳态或主观不无聊。
- 最终成本后静态closure报告`content-closure-after-reusable-costs.md` exit0：工业81/81 Research、37/37 Workshop、50/50 Building；Spacer 47/47、46/46、19/19；Ultra 15/15、3/3、3/3；33资源均列入。模拟器核心JSON诊断exit0、passed=true、apiGaps=0、failingResults=0；不读取Workshop价格，不是此次定价行为证据。
- Unity EditMode两项`ContentProgressionValidatorTests`内容/source-sink定向调用未取得XML：runner进程在Unity云/许可证配置请求超时后卡在关闭，status仍NotStarted；主代理仅停止自己启动的PID 50792，wrapper以UnityExitCode -1返回。该批不计通过。另有一轮双类调用达到1200s超时、无XML。最终单一路线Unity测试正常退出且实际购买了改价资产，提供该资产运行时支付证据。

**下一步：** 当前最大实测空档在工业期，而非太空：t=240h购ReinforcedBoilers后，Steel只有277,738；生产10.764/s、消耗1.13984/s，t=317h才购RotaryKilns（需求Steel 2.0m）。接着只核对这段Workshop效应与同价工业替代项，再单独选一次“钢供给投入或RotaryKilns成本”的最小变动，同一10秒命令表回归；暂不把已验证本地投资批量削减。太空终点仍仅到轨道巨构，系外远征、Ultra全通和Archotech实作没有新档证据。

### 第十二轮：100秒诊断步长与炼钢等待复测

- 按用户要求将固定路线的`ManualTick`从10秒提高到100秒；600秒与每小时检查点仍由`step`计算。末端产出检查改为至少执行一个tick，防止100秒步长时原`(int)(10/step)`变为0。每日回访时间与完整`AdvanceOffline`窗口不变，游戏运行时步长未改。
- 复用当前唯一命令表的345条玩家命令，终点仍为`OrbitalHabitatMegastructure`。真实Unity EditMode `orbital-habitat-cost-tuned2-step100.xml` 1/1通过、0失败/跳过，测试用时23.03秒，runner正常退出；状态文件`orbital-habitat-cost-tuned2-step100-status.txt`为Passed。日志出现Unity Licensing服务超时提示，但本次测试正常结束并通过；未见C#编译错误。
- 新 trace 于829小时（Spacer后349小时）建成巨构；全33资源的100秒采样净流非负、巨构效率/电力/物流及Food断言通过。Steel净流在RotaryKilns购买前为约9.624/s，购买后约10.820/s；旋转窑实际费用已为Steel 1.4m（旧XML里2.0m记录属于成本调整前证据）。
- 旧路线77小时RotaryKilns等待已被当前已批准的成本与供给投入改动消除；本次最大观察空档为201,500秒（约56.0小时），从ElectricalInstrumentation付款至PressurizedReactors付款，唯一短缺项为Steel，期间净累积约11.96-1.13984=10.82016/s。故当前首要瓶颈已转为PressurizedReactors的Steel 2.2m付款；本轮未再改其成本，避免同轮叠加第二笔数值调整。
- 与10秒版本相比，本轮命令表已由342扩至345条（已加入本地钢/住宅投资），模拟终点829h，不能把总时长差归因于步长单变量；100秒路线耗时23.03秒，10秒路线旧记录25.31秒。更大步长略省测试时间，但单次运行仍约23秒，且100秒采样会牺牲事件/支付时点精度；当前不再提高。
- 本轮静态闭包检查在测试前完成：Industrial Research 81/81、Workshop 37/37、Building 50/50，Spacer 47/46/19、Ultra 15/3/3，33资源均可达。没有更改资产、稳定ID/GUID或存档版本；真实Unity日志、XML和status均保存在`data/space-progression-20261018/`。

**下一步：** 如继续数值节奏校准，先沿同一固定命令表评估PressurizedReactors的Steel 2.2m成本及当前可买的钢供给投资，单次只改一个主字段，再用100秒路线验证实际等待和33资源终态；本次不根据该局部证据宣布一般玩家体验已不无聊。此路线只完成到轨道巨构，首次系外远征、Ultra全通及Archotech实作仍无新档证据。

### 第十三轮：本恒星系六据点节奏收束

本轮范围仅限本恒星系主线，星球建筑、系外、Ultra及其他内容未推进。

- 最新六据点固定命令表在唯一真实Unity EditMode诊断中1/1通过、0失败/跳过，runner正常退出；XML用例耗时192.9637秒：`sol-six-sectors-final-tuned-step100.xml`、`sol-six-sectors-final-tuned-step100-status.txt`、`sol-six-sectors-final-tuned-step100.log`。终点288/288命令完成，六区全占领且未占领AlphaCentauri。
- 曙光环、碧池星、终焉星、碎冠带、雷门环、曜心的占领时刻分别为新档504.17h、605h、733h、744h、757h、864h；OrbitalEngineering在456h完成。因此曜心及六区闭环耗时408h（17天），其中前五区在Spacer后301h（12.54天）完成。整条新档路线耗时864h；这是固定命令和每天三次回访的诊断，不是所有玩家的唯一玩法承诺。
- 前一份100秒trace在925h、Spacer后469h（19.54天）完成，最长行动间隔155h。本轮终点864h、Spacer后408h（17天），最长间隔88h。期间终焉星Composite消耗率175/s→100/s、曜心Electronics消耗率36/s→24/s两项已授权本地殖民成本都变更，因此61h改善不能全部归因于其中单一字段。终焉星Composite实际预算由1,260,000降至720,000；曜心Electronics预算由777,600降至518,400。殖民时长、Food成本、领土与无奖励/无自动产出契约不变。
- 曜心在本轮trace首个可行动样本缺Electronics：库存155,170，预算518,400，净生产约1.317/s；修订后的最大间隔为88h，已明显低于上一份trace的155h。本轮不继续改其他区域价格。
- 固定trace现对相同星区/资源只记录首个短缺样本，避免每个100秒tick重复写长日志；命令表、游戏运行步长与离线AdvanceOffline路径未改。
- 内容闭包报告`content-closure-after-sol-electronics-cost.md` exit0：Industrial 81/81研究、37/37工坊、50/50建筑；Spacer 47/47、46/46、19/19；Ultra 15/15、3/3、3/3；33资源。模拟器既有net9程序集执行62/62、passed=true；`dotnet run --no-restore`未能重建项目（当前SDK缺net9目标包），所以该结果只作为既有二进制诊断，且模拟器不读取本地星区成本，不能替代本次Unity路线。
- 等待采样：材料41,700秒、研究46,200秒、供给10,200秒、等待玩家0秒；这些是在线诊断片段采样，不外推为完整864小时分类，也不自动判断无聊与否。

本轮未触碰星球建筑、后代内容、真实存档、稳定ID/GUID、运行时游戏tick、Unity版本或提交/推送。下一轮若要继续本地主线以外内容，须另按用户范围授权；当前本地路线已有17天到六区终点的可复现Unity证据。

### 第十四轮：本恒星系主动建筑开发与氢原料（2026-10-20）

用户已明确授权增加星球建筑及特殊资源。本轮在工作树 `e899f2f` 的已有未提交成果上实施，保留第十三轮的路线、成本调整和证据；不扩展系外与 Ultra。采用三个子代理分别处理文档、UI、针对性测试，主代理核对资产并运行验证。

**内容与字段变化**

- 新增六个 `SectorBuilding` 资产及唯一新资源 `Hydrogen`，均有新独立 `.meta`；既有ID/GUID、枚举与存档v1不变。仍使用同一 BuildingManager、生产效率、研究前置、原子交易和离线结算，无独立星球状态、地图、策略树或自动购买。普通资源包括氢没有库存容量。
- `DawnOrbitalObservatory`（曙光环，最多2座）：基础研究力600/s；电力80、物流12、生产力120，维护电子元件/玻璃/火箭燃料。
- `TerminusMaterialsWorks`（终焉星，最多3座）：基础Composite 6/s、Machinery 3/s；消耗基础金属锭6、特殊矿料0.8、化学品3、陶瓷2、精炼燃料1、电子元件0.08/s；电力180、物流24、生产力420。
- `ShardCrownOreSorter`（碎冠带，最多3座）：基础矿料96/24/6每秒，分别为基础/高级/特殊矿料；维护炸药1、精炼燃料2、机械0.08、电子元件0.04/s；电力160、物流28、生产力360。仍需既有冶炼加工。
- `ThunderGateHydrogenCollector`（雷门环，最多2座）：基础Hydrogen 48/s；维护机械0.1、钛合金0.03、电子元件0.08、精炼燃料0.3/s；电力240、物流28、生产力400。
- `ThunderGatePropellantWorks`（雷门环，最多3座）：基础Hydrogen 32 + Chemical 4/s → RocketFuel 32/s，另维护机械0.08、电子元件0.1/s；电力300、物流36、生产力480。氢的持续战略用途是既有发射/物流/远航消费链，未强塞一次性研究税；原油精炼路线保留。
- `HeliosPowerArray`（曜心，最多3座）：基础电力1200/s；维护复合材料0.08、钛合金0.06、电子元件0.16、玻璃0.1/s；物流24、生产力400。
- 六座新设施 `SpaceCost=0`、`UpgradeTo=null`、`CostGrowth=1.25`；建设材料逐项定义于资产，依次复用测绘、钛合金、自动采矿、推进剂、轨道输电研究。不把新增设施全部变成系外航行的强制门槛。首件设计目标为占领后1–3次回访可承担投资，具体等待与多座投入回报尚待同一条新档命令表校准，不能将受控fixture当作达标证据。
- 既有 `EarthMoonLogisticsHub` 仅将名称“地月物流中枢”改为“碧池星物流中枢”，描述改为地表与轨道货运；绑定、数值、稳定ID/GUID不变。`ThunderGate` 描述由冰冷资源带补为气态巨行星的环带与轨道区，原行动成本不变。
- 六区 `ResourceRewards` 与 `OccupiedResourceRatesPerSecond` 继续全部为空；占领只给状态及开发面积，只有主动建成设施后才生产。
- UI `KingdomUIRoot.Sectors.cs` 按真实字段显示核心产出与功能，明确为每座基础值；维护仍在既有详情面板，避免固定卡片溢出。下一座成本按已有几何公式及当前建设倍率计算，初建和周期刷新共用，建造/拆除后刷新。
- 三份现有内容文档同步主动开发合同，不复制第二份数值表。`SpaceProgressionTests` 的普通科研升级链断言完整保留；有界本地科研设施单独验证有效星区绑定、数量限制、无升级链和测绘前置，避免将专属设施误判为普通升级分支。

**本轮验证与证据边界**

- 修改前后运行 `content-closure-check.ps1 -ProjectRoot . -ReportPath data/space-progression-20261018/planet-buildings-{before,after}-closure.md`，均exit0。后置Industrial 81/37/50、Spacer 47/46/25、Ultra 15/3/3全部可达，资源33→34；无新增依赖环。
- 修改前后直接运行已有net9 DLL `dotnet tools/NewEconomySimulator/bin/Debug/net9.0/NewEconomySimulator.dll --json`，两份 `planet-buildings-{before,after}-simulator.json` 均62/62、passed=true。未重建模拟器；该既有二进制核心自测不读取这批新资产，也不提供新设施的parity或节奏证明。
- 已执行真实Unity导入、编译与测试。EditMode filter `HomeSystemDevelopmentTests;ContentProgressionValidatorTests;SectorBuildingTests;SpaceProgressionTests`：首轮 `planet-buildings-edit.xml` 74/75（旧科研链测试不允许任何星区科研设施）；更新上述测试范围后 `planet-buildings-edit-final.xml` **75/75、0跳过、0.7170572秒**，包含新设施12项。覆盖七设施的占领门、缺料原子付款、上限、免本土领土、氢采集维护、缺氢停产/恢复、ManualTick/AdvanceOffline、隔离v1保存恢复及全资源source/sink。
- PlayMode filter `HomeSystemDevelopmentPlayModeTests`：首轮 `planet-buildings-play.xml` 0/1，fixture仅占领雷门、未占领其前置，导致实际星区行隐藏；补齐fixture前置并增加行实际可见断言后，`planet-buildings-play-final.xml` **1/1、0跳过、1.6602578秒**。实际场景验证两卡摘要、展开高度、卡片无重叠、点击建造、数量与下一座几何成本刷新，以及普通建筑页排除星区设施。
- 每轮对应 `.log`、`-status.txt` 均在同目录。四次runner都在写完XML和Shutdown日志后未退出；主代理核对命令行后仅停止自己启动的PID 24076、10796、28508、14364，包装器返回UnityExitCode -1。**通过的是最终XML用例，runner没有正常退出，不将状态文件Failed改成Passed。** 最终EditMode/PlayMode日志未检出C#编译错误、Null/MissingReference或断言异常；有Unity网络/Curl退出提示。
- `git diff --check`通过。隔离存档的 `Temp/PlanetDevelopment*` 目录已由测试清理并核查不存在；未创建临时Unity项目、未操作用户真实存档、无遗留本轮Unity进程，未提交/推送。

本轮交付功能可用路径及有界受控验证，尚未将新设施购买加入唯一回访命令表，因此不能沿用旧17天结果为新内容背书，也不能声称“耐玩/无聊”已自动验收。下一具体动作如继续节奏工作：在同一命令表相应占领后插入一座当前有用的设施及其必要供给投入，每次只核对一个阶段的真实供给和等待，不新增策略搜索或批量调价。

### 第十五轮：唯一建筑与分阶段巨构策划修订（2026-10-20）

用户指出氢仅进入推进剂加工，缺少独立资源角色；希望雷门直接建低温推进剂厂，并让部分地点拥有独一无二、像工坊一样分阶段购买的建筑，以曙光戴森球为例。本轮先查阅原太空策划、本恒星系合同、路线图及第十四轮交接，并核对当前Building/Workshop/UltraProject代码；两个只读子代理分别核查复用边界与内容设计。保留进入本轮时全部未提交资产、代码、测试与证据。

**已修改：** 只续写三份原有内容文档及本交接。`docs/content/space-and-beyond-design.md`新增“唯一建筑与史诗建设”策划：目标由氢库存+采集厂+加工厂改为唯一低温推进剂厂的内部流程；曙光唯一戴森球按“总装与试验阵列→首批集能卫星→环恒星集能带→戴森群联控网”分四次主动投资，每阶段实际供电；曙光负责工程入口，曜心负责近恒星部署。碎冠质量投射器、碧池冰海方舟、曜心日冕灯塔列为后续候选，终焉继续综合工业职责，不要求每处都有奇观。原合同和路线图增加修订指针，明确上一轮氢链仍是当前资产事实，目标设计尚未实施，不回写历史测试为新功能证据。

**实现边界与下一动作：** 一座`SectorBuilding(maxAmount=1)`作本体，后续阶段优先复用工坊前置、原子付款及PurchasedUpgradeIds，不重构Ultra工程或创建另一条树。工坊当前缺所需建筑/星区前置，必须补Manager购买、读档、闭包与快照消费端，并补星区Prefab阶段展示；不能只锁UI。购买要求本体存在；阶段永久记录允许合法拆除本体后的存档，重建付本体费用并恢复既有阶段。氢链合并先核算原采集+加工的完整负荷，不能仅删除氢消耗送出免费产能。移除定义ID会影响当前v1合法性校验，实施前核对，不添加未经授权的旧氢档映射/迁移，不操作用户真实存档。下一批先完成雷门合并与曙光工程的定义、事务、持久化及UI闭环，再扩候选，不将戴森完工追加为首次系外硬门槛。

**验证：** 核对当前源码支持与缺口；检查本轮文档本地链接、修订边界及空白。未改游戏资产、数值、稳定ID/GUID、代码或测试，未运行闭包/模拟器和Unity测试。未执行真实 Unity 编译。本轮仅交付策划，不能称氢已移除、唯一厂已限建或戴森球已可建；新价格、倍率、维护和回访节奏仍待实施及真实Unity证据。无提交/推送、安装或临时工程。

### 第十六轮：雷门唯一工厂与曙光分阶段戴森工程实施（2026-10-20）

用户明确要求“持续完善，不用停”，本轮直接实施第十五轮首批，不再停在方案确认。资产/EditMode、UI/PlayMode、运行时/静态工具分工写入互斥文件；主代理独立审查、接管运行时代理传输中断后的工具编译修正，统一串行运行真实Unity。原有脏工作树、工业成本与回访命令表全部保留，没有把本批设施插入已有新档节奏命令表。

**实际内容与字段**

- `ThunderGatePropellantWorks`保留原ID/GUID，`maxAmount`由3改1。内部承接大气采集、分离、液化，不再读取氢库存；基础`RocketFuel`仍32/s，`Chemical`仍4/s。原采集站及`Hydrogen`资产连同meta共4文件完整移入`.codex/archive/2026-10-20-unique-projects/retired-hydrogen/`，运行资源目录已无这些定义或GUID引用。没有旧ID映射、存档迁移或真实存档操作。
- 工厂建造费由加工厂单独费用改为原采集站+加工厂全额相加：Steel 24000→42000、TitaniumAlloy 3000→5400、Composite 6000→10000、Electronics 3200→5600、Machinery 3600→6600、RocketFuel 2400→4200。运行按32/48采集份额合并：生产力480→746.666666666667、电300→460、物流36→54.666666666667，Machinery .08→.146666666666667/s、Electronics .1→.153333333333333/s，增加TitaniumAlloy .02/s和RefinedFuel .2/s，移除Hydrogen 32/s。不是只删中间料后白送采集能力。
- 新增`DawnDysonSphere`及独立meta，绑定DawnRing、限建1、无升级链、无本土空间费，研究使用测绘与既有轨道输电。总装/试验阵列首建即基础供电1200；生产力400、物流24，维护Composite .08、TitaniumAlloy .06、Electronics .16、Glass .1/s。建造费复用钢/合金/复合材料、玻璃、电子和机械，并投入CopperWire 6000、RocketFuel 3600；完整数值见资产。
- 新增三个真实工坊阶段及meta：`DysonCollectorFleet`→`DysonStellarBelt`→`DysonControlNetwork`。全部要求本体；首段要求DawnRing占领，后两段要求HeliosCore占领；末段增加既有相态材料研究与PhaseMaterial 6000。定向发电效果Type10分别为3/7/15，沿现有加法叠加语义累计倍率23，不误写为3×7×15；其他既有全局电力倍率仍正常适用。卫星与集能带额外消耗铜线、陶瓷及部署燃料；基础维护配方不随本批阶段改变，明确为集能效率扩展，不虚构持续消费接口。
- 本批没有实施雷门后续模块、质量投射器、冰海方舟、日冕灯塔，没有改变首次远航或Ultra准入，不把所有工程完工新增为主线硬门槛。

**运行时、界面和工具闭环**

- `WorkshopUpgrade`增加可选强类型`RequiredBuilding`/`RequiredSector`，旧配置默认空；Editor配置API尾加可选参数。`WorkshopManager.GetPurchaseFailure`统一完整预检，购买仍原子付款/回滚，失败枚举仅尾追加`BuildingMissing`/`SectorNotOccupied`。从未创建本体状态时使用States.TryGetValue返回正常缺本体，不能用GetState抛异常。
- 本体数量继续由BuildingState保存，阶段继续由PurchasedUpgradeIds保存，存档版本仍1。买阶段必须持有本体；拆除后保留已购阶段，重建恢复；恢复工坊时不错误要求当前持有本体。SaveManager在恢复Sectors后校验已购阶段的占领条件，非法失去曜心的阶段档拒绝；既有时代、研究、前序阶段校验保留。
- 星区卡片由真实RequiredBuilding关联工坊阶段，显示当前阶段、完整下一费用、具体研究/占领/本体/材料缺口、满效率阶段电力及阶段详情。建造按钮依次执行建本体、购买下一阶段、完成禁用；拆除后可付费重建。卡片固定Stage链接在Prefab创作，刷新后恢复其TMP射线，不能只绑定onClick但实际点不到。普通工坊详情使用相同Manager预检，避免两入口条件不一致。现有按钮材质测试仅增加同类Stage文字导航入口的合法项，其余约束保留。
- `EconomyDependencyValidator`及静态闭包检查加入工坊本体/星区前置引用和图可达；修正脚本原先误用`requiredWorkshopUpgrades`读取工坊前序的错误，工坊改读真实`requiredUpgrades`，建筑仍读原字段。星区静态只检前序图和资源来源，不模拟实际驻留时序/全部Manager门槛。Unity快照Exporter、工具模型/购买及状态校验同步新字段，并加入2项确定性自测；无策略搜索、自动购买或自动调参。

**本轮命令、结果与证据边界**

- 前后`content-closure-check.ps1 -ProjectRoot . -ReportPath data/space-progression-20261018/unique-projects-{before,after}-closure.md`均exit0；资源34→33，Spacer工坊46→49全部可达，Spacer建筑仍25全部可达，Industrial/Ultra既有节点全部静态可达。`building-resource-flow-check.ps1 -ProjectPath .`：78资产、0正向产耗重叠。
- `dotnet build Kingdom.DeveloperTests.csproj --no-restore`通过，Runtime/Editor与两测试程序集均compiler_exit0；仅既有3处TextureImporter.spritesheet弃用警告（汇总重复计6条）。随后真实Unity也完成新源码导入编译，无C#编译错误。
- 当前机器有SDK10和net10引用包、net9运行时，但无net9目标引用包，原工具net9重建报NETSDK1127。未安装任何包、未改工具csproj。使用`dotnet restore tools/NewEconomySimulator/NewEconomySimulator.csproj -p:TargetFramework=net10.0 --source D:/GitHub/Kingdom/tools/NewEconomySimulator -p:NuGetAudit=false`只使用本地SDK，再`dotnet build ... --no-restore -p:TargetFramework=net10.0`；修正新增自测调用正确SnapshotRules API及补齐fixture的Initial/Save星区状态后，当前源码重建通过。`dotnet tools/NewEconomySimulator/bin/Debug/net10.0/NewEconomySimulator.dll --json`为**64/64、passed=true**，含新增2项。输出`unique-projects-after-simulator.json`及`unique-projects-simulator-build.log`；修改前旧net9 DLL自测为62项。二者是工具自测，不是新增资产的真实Unity parity或节奏证明。
- 真实EditMode filter：`HomeSystemDevelopmentTests;ContentProgressionValidatorTests;SectorBuildingTests;SpaceProgressionTests;IndustrialWorkshopAvailabilityTests;WorkshopBenefitPreviewTests;KingdomButtonMaterialTests`。最终`unique-projects-edit-verified.xml` **91/91、0跳过、1.4574203秒**，其中本主题HomeSystem用例14/14；覆盖缺本体/研究/前序/占领/材料及重复拒绝、全部资源不部分付款、唯一上限、化工断供在线/离线恢复、每阶段在线/离线实际供电、完整v1拆建读回及非法占领档拒绝。此前`unique-projects-edit.xml`为88过/2失败/1跳过（额外parity导出需环境变量而跳过）；`unique-projects-edit-final.xml`90/91，分别暴露fixture未建工坊索引和真实缺本体查询抛异常，已修正，保留原失败证据。
- 真实PlayMode filter `HomeSystemDevelopmentPlayModeTests`：最终`unique-projects-play-final.xml` **2/2、0跳过、2.700344秒**。真实SampleScene验证雷门单卡建造/上限、曙光本体及3阶段、曜心占领锁/刷新、阶段详情不误购买、完成禁用、拆建阶段保留；8区块边界和相互无重叠、TMP不截断/高度足够，Stage目标可射线/按钮可交互。前轮`unique-projects-play.xml`1/2，暴露两用例之间先销毁Manager后UI还刷新；修fixture拆卸时先停用UI再销毁Manager，未吞日志或放宽断言。测试以无图形批处理运行，几何/交互断言不是GPU画面或玩家手感证据。
- **五次runner均在写完XML及Shutdown日志后未正常退出，包装器超时结束自己启动的Unity进程。** Edit依次240/120/90秒、Play120/90秒，所有对应`-status.txt`如实保留`Failed(Timeout)`；不能把最终通过XML说成runner正常exit0。最终两日志未检出C#错误、Null/MissingReference、InvalidOperation或断言异常；Unity仍有Curl/网络关闭提示，不扩展修引擎退出问题。
- 最后核对文档本地链接及`git diff --check`；`Temp/PlanetDevelopment*`隔离存档目录无残留，归档4文件存在且运行原路径缺席，无本轮遗留Unity进程。没有创建临时Unity项目，没有操作真实存档、升级/安装、提交/推送或PR。
- 最终工作树盘点发现PlayMode生成了`Assets/TextMesh Pro/Fonts/SIMSUN SDF.asset`的动态字形/图集缓存；该文件本轮进入时干净，已仅恢复此项测试副产至原HEAD，没有回退其他源码/资产或已有脏修改。`git diff`与cached diff均空，工作文件/index/HEAD对象均为`8da8976c89b1bfa0d58550d24d444ce78d0c8107`；但Git status仍显示该文件`.M`，刷新stat后仍然存在该状态标记。这里记录内容已恢复及残留状态，不擅改Git全局配置或index内容掩盖它。未因该可再生成缓存复跑已通过的行为用例。

首批行为与针对性场景用例已交付，**新成本与倍率仍是实现初稿，尚未加入唯一新档回访命令表校准**；不能沿用此前17天或轨道巨构终点记录宣布本批价格与节奏合格。后续若继续：先在同一手写命令表插入雷门唯一厂与戴森首建/一个阶段及必要供给，记录实际付款、净流、效率和等待，再决定是否调整一个主字段；首批闭环后再逐项落实后续巨构，不批量塞空倍率资产。含旧Hydrogen/采集站ID的开发v1档未做兼容迁移，继续遵守当前定义与新v1规则。

## 17. 2026-10-20：戴森工程归属曜心，核对环境与远航科技链

用户修正：戴森工程放在曜心更合理，各星球设施应符合当地条件，以本系经营支撑科技并最终走向系外。本轮先复核上述第16节成果及当前定义，没有重新建立建筑、工坊或研究系统。

### 实施与字段差异

- `DawnDysonSphere.asset` 的 `sector` 从 `DawnRing` 改为 `HeliosCore`；`DysonCollectorFleet.asset.requiredSector` 同步从曙光改为曜心。后两阶段原已绑定曜心，现本体及三阶段全部一致。稳定ID、GUID、文件名、数量限制、成本、产率、维护和倍率均保留。描述改为曜心近恒星总装与部署；曙光不再承载戴森入口。
- 曙光观测站、碧池物流中枢、终焉材料联合厂说明补齐环境与能力关系：轨道观测提供研究力、冰海基地连接轨道转运、荒原承接结构材料和机械加工。碎冠矿料分选、雷门气态取料加工、曜心近恒星采能与当前环境定义一致，未作无依据搬迁或调参。
- 更新 `docs/content/space-and-beyond-design.md`、`home-system-exploration-rewards.md`、`progression-roadmap.md`，增加“环境、建设与科技的因果链”。沿用真实 `OrbitalEngineering → HomeSystemSurvey → DeepSpaceFleet → InterstellarNavigation`，首次 `AlphaCentauri` 保留碎冠与曜心占领门槛。
- 逐项核对研究与设施防循环：观测站供研究力，矿料/材料/物流/推进剂/能源进入既有经营与远征。研究不新增六处设施全部建满条件。相位材料工程在幽影材料及星际导航之后，戴森最终联控阶段因此属于远航科技之后的长期建设；不能让戴森完工反向锁住星际导航。曜心驻留也不依赖戴森工程。
- Editor测试更新曜心归属及阶段占领原子拒绝，保留付款、功率、唯一数量、存档与重建；新增真实曙光观测站在线/离线研究贡献及维护支付测试。PlayMode在曜心菜单建造和扩建，验证未占领拒绝、最终阶段缺相位材料工程研究时锁定、研究完成后恢复、详情入口和卡片几何、拆除重建保留阶段。

### 本轮验证（全部当前源码与定义）

证据前缀 `data/space-progression-20261018/helios-revision-`：

- 定义前后 `content-closure-check.ps1 -ProjectRoot . -ReportPath ...` 均退出0。Industrial研究81/81、工坊37/37、建筑50/50；Spacer研究47/47、工坊49/49、建筑25/25；Ultra研究15/15、工坊3/3、建筑3/3；资源33。报告 `before-closure.md`、`after-closure.md`。
- 修改前后本地已构建的 net10 模拟器确定性自测64/64，`passed=true`，分别 `before-simulator.json`、`after-simulator.json`。这是工具自测，不是本轮资产trace parity或新档节奏验收；未修改工具源码或项目框架版本。
- `building-resource-flow-check.ps1 -ProjectPath .`：78建筑，0原始同资源正向生产/消耗冲突。本轮无生产配方或资源cost/source/sink增删；本体与阶段引用的新星区均可达。
- `dotnet build Kingdom.DeveloperTests.csproj --no-restore` 退出0，0错误，仅既有spritesheet废弃警告；日志 `developer-compile.log`。此命令只是编译，不作测试通过证据。
- 真实Unity EditMode `edit.xml`：93/93，零失败/跳过；包含 HomeSystemDevelopmentTests、ContentProgressionValidatorTests、SectorBuildingTests、SpaceProgressionTests、IndustrialWorkshopAvailabilityTests、WorkshopBenefitPreviewTests、KingdomButtonMaterialTests。真实编译成功，wrapper退出0、正常完成；对应 `edit.log`、`edit-status.txt`。
- 真实Unity PlayMode `play.xml`：2/2，零失败/跳过，HomeSystemDevelopmentPlayModeTests；wrapper退出0、正常完成，对应 `play.log`、`play-status.txt`。本轮没有延续第16节的退出超时问题，未为此扩大修改范围。
- 本轮两份Unity日志无 C# 编译错误、NullReferenceException、MissingReferenceException、InvalidOperationException、AssertionException。`git diff --check` 通过。测试独有隔离存档目录已清除（Unity结束后整个Temp目录不存在），无遗留Unity进程；未创建临时工程、未操作真实存档。
- Unity再次生成SIMSUN动态字形缓存；仅恢复此文件到已核实的本轮基线HEAD内容，恢复后hash与HEAD一致，仍可能存在第16节记录的status标记异常，不追改Git索引或全局设置。

### 边界与后续

本轮已完成地点归属、环境职责、当前科技链核对及针对性真实行为验证。未调整成本/维护/倍率，未把候选质量投射器、冰海方舟、日冕灯塔写成已实现内容。新增设施与戴森阶段尚未加入统一新档回访命令表，不沿用旧17天结果宣称当前节奏平衡。后续如推进节奏校准，先以现有同一手写回访表实际购买设施/阶段，再依据供给与研究等待证据决定改动；不增加策略搜索或自动调参。

## 18. 2026-10-20：独立星球勘探、唯一推进剂基地与冰海方舟

用户批准实施“驻留→当地环境勘探→发现→专属建筑→分阶段产业”计划，阶段建设整笔付款，探索持续供给；同时明确允许按现有manifest与lock恢复缺失Unity包缓存继续真实验证。沿用当前Manager、建筑、工坊、存档与唯一手写新档路线，无平行工程系统、额外资源或策略搜索。

### 实施与字段差异

- `SectorDef`增加可选内联`SectorInteriorExplorationDefinition`，包含描述、完成发现、时长、研究、耗材和三个发现里程碑。Unity序列化出的完全空内联对象视为未配置，部分配置仍严格拒绝；OnValidate同步清除金额/时长解析缓存。
- `SectorState`增加独立勘探进度、开启、手动暂停状态；`SectorManager.InteriorExploration.cs`统一前置、预览、开始/暂停、结算和恢复校验。实际时长取请求、剩余和所有材料可支付时长的最小值，多料与进度走已有原子交易；缺料不关任务、不丢进度，暂停停止付费，完成立即登记并停止消耗。共享材料按稳定星区ID顺序分配。普通占领逻辑不变。
- `SimulationManager`在线/离线均在驻留结算后调用同一探索逻辑。勘探不支付封顶Food，使用普通离线时间片；殖民/战役保留原固定操作步长。最初将探索也接入0.1秒步长导致真实新档回访在第一段大气勘探长时间重算整个工业体系、600秒仍无完成XML，已据此移除该接入，未改有效时间倍率或付料规则。新增2.75秒回归检查完整进度、对应付款且无殖民余量。存档v1增加`InteriorExplorationProgress`、`InteriorExplorationActive`、`InteriorExplorationPaused`，数值字段必须明确存在且合法，不增加旧档映射/迁移；恢复前全量验证。测试的旧显式DTO夹具补写零进度。
- `SectorBuildingDef.RequiresInteriorExploration`默认false，只有三处专属设施开启。UI、直接建造和存档恢复复用该门槛；工坊阶段即使已拆本体，恢复也要求其星区占领及勘探完成。本体仍最多1个，阶段永久保留，重建付本体费用。

| 地点 | 原配置→当前配置 | 实际回报 |
|---|---|---|
| 碧池星 | 无内部勘探→21600有效秒；机械.1、电子.08、复合材料.12、燃料.05每秒 | 新增唯一`AzureIceSeaArk`，基础食物960/s、研究力480；消耗生产力360、电力240、物流32，维护化学品.8、机械.08、电子.1、复合材料.04每秒，不提供住房 |
| 雷门环 | 无内部勘探→28800有效秒；机械.12、电子.1、复合材料.16、陶瓷.08、燃料.08每秒 | 现有本体增加探索门槛及三个工坊阶段；基础燃料32/s，累计阶段倍率1→2→4→7；物流生产16→最终160，均进入真实结算，原维护保留 |
| 曜心 | 无内部勘探→43200有效秒；电子.16、玻璃.12、陶瓷.1、钛合金.08、燃料.1每秒 | 原戴森本体及三阶段增加探索门槛，稳定ID/GUID与原工程保留。基础供电1200，按现有加算效果累计1→3→9→23，不误写成4/8/16 |

- 雷门三个新增阶段为`ThunderCryogenicSeparation`、`ThunderParallelRefining`、`ThunderOrbitalLoadingRing`。探索物资和研究均来自奖励外的既有链；戴森最终阶段仍在相位材料研究后，不能反向锁住首次远航。
- UI复用详情主按钮开始/暂停/继续，显示发现、进度、供给、缺料、条件性剩余时间与开放设施；折叠行区分驻留、勘探与工程阶段。卡片提前显示具体锁因、当前能力、完整下一阶段费用/效果。Prefab卡高360→480，星区Metadata横向上界.43→.80，建筑展开按钮仍从.82开始；固定层级未运行时重建。
- 同一`EarlyVerticalSlicePacingTests`命令表在居住巨构后追加三处E勘探、雷门本体+3阶段、戴森本体+3阶段与冰海方舟；原`S:HeliosCore`仍只代表六处驻留完成。新增终点`B:AzureIceSeaArk`，逐项记录探索付料、供给等待与建设行为。修正诊断日志的字符串/ExpantaNum隐式转换，使用插值输出材料金额。

### 依赖恢复与工具一致性

- 缺失的是已锁定包文件，原`Library/PackageCache`为空、ProjectCache仍指向不存在目录。仅将陈旧生成ProjectCache及其md5归档到`.codex/archive/2026-10-20-interior-package-cache/`后由Unity恢复；不加依赖、不改版本。manifest SHA256仍`A8501FBC4E7A3994B2195387A1BFD15E2B86354016030B8181A5889269A036CB`，lock仍`F5392F52C53AFD600CAA2C6AF95D55EE290F9FD27AF37AC7ABEE230FF563C16E`。
- 快照导出/工具增加探索配置、状态、建筑探索门槛及唯一数量。工具修正Unity空内联配置识别、已占领星区不应继续衰减；新增回归不吞掉部分非法配置。真实快照夹具补齐研究效果触发的建筑缓存刷新；资源奖励仍取人口推进前预算，未误改结算时机。
- `interior-simulator-final.json`：72/72确定性自测通过。net10本地框架覆盖构建退出0，18个既有可空性警告、0错误；未改csproj目标框架。`interior-parity/manifest.json`的12个真实Unity窗口全部零差异，含在线、重复离线、驻留、探索运行/暂停/缺料/完成；`comparison-summary.json`可核查。这是受控真实快照对比，不是新档节奏证据。

### 真实Unity验证与限制

- 初次相关EditMode最终`interior-edit-final.xml`为381/381，0失败/跳过，但写完XML后退出超时；不称正常退出。前期缺包及首轮资产导入超时的无XML记录仍保留。
- 离线窗口性能修正后的`interior-offline-window-edit.xml`：85/85、0失败/跳过、正常通过，包含探索、工程、真实快照及材料链集成用例；工具72自测与12真实窗口也全部重跑通过。
- 全源码更名后完整EditMode最新`definition-rename-edit-final.xml`：1007总用例、992通过、4失败、11条件跳过，Unity退出2。新增工程门槛使原“购买全部工坊”夹具缺少合法星区记录，已补齐；三项材料链存档用例现通过。剩余四项是原有壳裁剪数2/实际3、OilDerrick维护断言、炸药持续用途断言、终焉/碧池驻留成本高低断言，不据此更改本轮玩法或资产。
- GPU交互`interior-gpu-interaction.xml`：2/2、0失败/跳过，`interior-gpu-interaction-status.txt`明确Unity退出0，D3D11/NVIDIA实际图形设备。探索主按钮经EventSystem真实射线和pointer事件启动/暂停/续供；其他既有卡片事件、阶段锁刷新、唯一数、重建、文本不截断/几何检查通过。夹具先按已有Spacer档建立引导基线，避免新手引导拦截。批处理GameView实际640×480；请求1920×1080未改变其实际分辨率，不能称1920截图验收。无图形模式/夹具未建基线的中间失败XML保留，已由此轮结果覆盖。
- 最终静态闭包`interior-closure-final.md`：Industrial81/37/50，Spacer47/52/26，Ultra15/3/3全部可达，资源33；79建筑、0原始同资源正向产消冲突。无新增资源或库存上限。
- 统一新档回访已完成，具体结果见第20节；当前证明手写路线可达，不宣称整体价格与节奏已平衡。外部运行体验由用户自行验收，不设代理交付门槛。

## 19. 2026-10-20：GameDefinition派生类统一Def后缀

用户明确追加代码标准化：所有直接/间接派生类用`*Def`。基类`GameDefinition`保留，非派生的内联勘探配置、StoryArchiveDefinition、MusicCatalog不在此次改名范围。

- `Building→BuildingDef`、`Resource→ResourceDef`、`Research→ResearchDef`、`SectorBuilding→SectorBuildingDef`、`SectorDefinition→SectorDef`、`RelicDefinition→RelicDef`、`StoryChapterDefinition→StoryChapterDef`、`UltraProjectDefinition→UltraProjectDef`、`WorkshopUpgrade→WorkshopUpgradeDef`。
- 使用已安装SDK的Roslyn类型符号更新Runtime、UI、Editor和测试134文件、5488处引用；同名属性、枚举、序列化字段、UI页名、JSON键、Datas目录不做字符串全词替换。9份脚本与`.meta`配对改名；`definition-rename-meta-check.json`核查9/9全文一致，旧源码路径均不存在。没有兼容别名或旧ID映射。只读复审完整继承图无遗漏。
- `definition-rename-symbols.json`保留映射/文件计数。临时符号工具位于`D:/GitHub/Kingdom/Temp/DefinitionRename`，用途仅本次源码更名；Unity清理Temp后已核验目录不存在，未创建Unity副本/worktree。开发编译`definition-rename-developer-build.log`退出0、0错误；真实编译与跨系统测试见第18节。
- 当前architecture/content/testing/UI说明、泛型例子及源码链接同步；`definition-database.md`记录派生类命名规则。历史审计、旧证据哈希及旧交接不批量改写；文档契约测试1/1通过。未操作真实存档、未commit/push。

## 20. 2026-10-20：最终统一回访与收尾

- `interior-route-supplied.xml`真实Unity EditMode为1/1通过、无失败/跳过，日志完成退出流程，结束后无Unity进程。364/364操作、130次会话，累计游戏内1032小时（43天），Spacer后576小时（24天）；实际付完三处探索、三个唯一本体、雷门与戴森各三个工坊阶段。原始摘录与材料总额见`data/space-progression-20261018/interior-route-summary.md`。
- 前一次普通窗口路线`interior-route-normal-window.xml`在最终戴森阶段停滞：电子元件维护消耗高于产出，等待七天无法积累36000付款。按实际供给证据，在同一手写表的`DysonStellarBelt`后付费再建一座`OrbitalWireWorks`，利用工程已有供电恢复供给；不改价格、维护、倍率，不赠送资源、不增策略搜索。最终路线通过，并断言电子元件生产覆盖消耗。最长观察到操作间隔88小时；这些时间不是整体节奏已经平衡的结论。
- 实付探索总额：碧池复合2592/电子1728/机械2160/燃料1080；雷门陶瓷2304/复合4608/电子2880/机械3456/燃料2304；曜心陶瓷4320/电子6912/玻璃5184/燃料4320/钛合金3456。
- 仅恢复本轮GPU测试产生的`SIMSUN SDF.asset`动态字形缓存到已核实基线，git blob为`8da8976c89b1bfa0d58550d24d444ce78d0c8107`，保留其他工作树内容。临时符号更名工具`Temp/DefinitionRename`不存在；未创建临时Unity工程/worktree，未操作真实存档、未commit/push。
- 最终交付边界：针对性EditMode85/85，GPU交互2/2且退出0，模拟器72/72、真实快照12窗口零差异。完整EditMode仍有第18节所列4项既有断言失败及11项条件跳过；未扩大范围修复。实际批处理GameView为640×480，未声称1920视觉验收。

## 21. 2026-10-20：巨构建设使用实际结构名称

- 用户要求各步建设不用“xx阶段”，改用具体结构名称增强沉浸感。复用现有本体和工坊Label，不增加字段、不改ID、价格、收益或建设顺序。
- `KingdomUIRoot.Sectors.cs`首建/扩建按钮使用目标结构全名；卡片显示已建结构，扩建成本和收益标注目标结构，详情改称工程/结构详情。折叠星区摘要使用最新已建结构，移除编号进度称呼。工程累计收益文字使用“已建结构”“当前产出/物流”。
- 雷门：大气采集平台→低温分离系统→并联精馏阵列→轨道装载环；曜心：总装与试验阵列→首批集能卫星→环恒星集能带→戴森群联控网。文案直接复用各定义，后续内容无需在UI另写名称映射。
- `HomeSystemDevelopmentPlayModeTests`补充首建/扩建按钮与已建结构显示断言，保留真实交互、布局与运行检查；真实GPU PlayMode `structure-names-play.xml` 2/2通过、无失败/跳过。最终退出结果以`structure-names-play-status.txt`为准。`docs/ui/page-responsibilities.md`同步命名契约；`git diff --check`通过。

- 本轮Unity写完通过XML后在Input System shutdown之后停滞数分钟；核对PID41112的命令行确为本轮`-batchmode`和`structure-names-play.xml`后结束，仅停止代理启动的批处理，退出码-1，不称正常退出。已将异常退出写入status；不重复运行已通过用例。仅恢复测试新增SIMSUN动态字形缓存到已核实blob基线，其他工作树内容保留。

## 22. 2026-10-20：开发建议闭环与首次系外真实回访

用户要求继续星区拓展。本轮复核已实现首批内容后，沿既有系统补齐开发建议和首次远航验证，不新增候选奇观、资源或平行策略树。

### 实际改动

- `TutorialManager.BuildSpacerGuidance`加入本系内部探索和发现后专属本体的建议，复用实际探索预览和建筑失败原因；运行中的探索先于另一项未开始开发。显示等待材料名称、手动暂停、运行与完成后的建设资格，不代操作、不付款、不改状态。
- 系外导航保留优先级：已有可推进/已可解锁目标仍作为主入口，本地可选开发附加显示；未开始/手动暂停勘探不会遮住首次Alpha导航。六本系均已驻留而Alpha尚未解锁时，读取Manager真实准入，推荐稳定顺序第一个可解锁系外目标。
- 修正暂停远征已有部分进度时的误述：改为检查补给/舰队后继续，明确远征暂停会衰减进度，不再在37%等未完成状态建议占领确认。远征完成仍由现有Manager自动登记占领。
- `TutorialManagerTests`新增8个状态场景，覆盖开始、活动缺料优先、暂停、探索完成后本体、首次解锁、未开/暂停可选探索不阻远航、部分远征恢复。初次fixture忘重建研究效果使Alpha准入失败，补齐强类型Rebuild后通过；不修改产品准入来迁就夹具。
- 唯一`EarlyVerticalSlicePacingTests`手写命令表扩至`C:AlphaCentauri`，保留原364条前缀与旧终点；新增自动化船坞装配、六座实际付费船坞及首次远征。按真实UI检查HasSupply和正推进率再起航，记录实际军力、材料、进度与结果。无资源/军力/研究fixture授予、策略搜索或价格修改。
- 首轮远征在实际起航后被诊断记录器误将C命令当成ResearchDef的异常中断；补齐RecordShortages/Blocker的独立远征路径并重跑完整新档，不跳过前缀或弱化校验。`home-system-exploration-rewards.md`同步开发建议合同。

### 当前验证与界限

- `first-frontier-route-final.xml`：真实Unity EditMode37/37通过、零失败/跳过（36个引导/既有用例加1条完整新档路线），用例耗时204.9200691秒。包含当前源码真实编译；最终日志未见CS编译错误、NullReferenceException、MissingReferenceException或AssertionException。
- 372/372操作、131次会话，游戏内1037小时，Spacer后581小时；冰海方舟原终点1032小时，追加5小时。起航有效军力2212.001485、压力比2.457779、HasSupply=true、正推进率0.000276；完成后Alpha实际Occupied=true、VisitCount=1、全局战役停止，所有本地探索与工程付款记录仍满足。见`data/space-progression-20261018/first-frontier-route-summary.md`。
- Unity写出XML后再次在Input System shutdown之后停滞；核对PID38820命令行确为本轮batch后结束，runner退出-1。37/37是用例结果，不称正常退出。此前两次引导和首轮诊断失败/异常退出证据均保留。
- GPU PlayMode `sector-continuation-play.xml`实际2用例均失败：同一工作区另一个活动任务“为建筑生成5个图标”正在接入建筑图标，已将`KingdomUIRoot.Sectors.cs`改为绑定Icon，但本轮运行时`KingdomUISectorBuildingCard.prefab`尚无该固定Image，报`SectorBuildingCard is missing authored Icon Image: AzureIceSeaArk`。核对另一任务实际状态确认其正在处理图标，保留其变更，不回滚/吞日志/扩改Prefab。只释放本轮启动的GPU batch，退出-1。此输出不能算本轮PlayMode通过；需图标任务完成Prefab后再重跑该两项。
- 本轮未修改经济定义、Simulation或快照结算，未重复执行已有72自测和12parity，不把之前结果冒称本轮重跑。未重跑全量EditMode（此前4项既有断言失败仍另记第18节）。`git diff --check`通过；未操作真实存档、未commit/push。

下一具体动作：图标任务补齐固定Prefab后，重跑HomeSystemDevelopmentPlayModeTests并核对Console和实际退出；在此相关UI恢复前不扩展到下一批系外/Ultra资产。当前首次远航固定路线已可达，不宣称43天整体节奏已平衡。

收尾：本轮测试进程均已释放；仅恢复本轮测试生成的SIMSUN动态字形缓存到已核实blob `8da8976c89b1bfa0d58550d24d444ce78d0c8107`。并行建筑图片/绑定/Prefab改动全部保留。

## 23. 2026-10-20：星区拓展缺口复核与修复

本轮沿第22节续接，对探索供给、保存恢复、专属工程、引导和界面作定向复核。运行时与UI分别只读审查后由主代理核对；不新增星球/资源/系统，不调整定义、价格、产量或远航压力。

### 修复内容

- `TutorialManager.cs`在未占领远星的远征进度已满时推荐确认占领；部分进度仍提示检查补给与继续远征。补充100%未确认占领场景；引导本身不改变状态。
- `KingdomUIRoot.Sectors.cs`区分未建工程的“建成后”、保留扩建后的“重建后”和已有本体的“当前”满效率能力。拆除后显示“已保留：结构名（工程本体待重建）”，避免把最新扩建误称为本次付款对象。
- `DetailPanel.cs`工程详情按现有SortOrder和稳定ID列出本体及全部扩建记录，分别显示已建、待建、记录保留且重建本体后恢复。已购买扩建但本体不存在时不再宣称收益已生效。
- `LiveRefresh.cs`将集中工坊详情正文刷新覆盖到Sectors入口，购买与拆除后不必重开详情。未新建刷新循环或运行时固定UI。
- UI规范仅补充上述记录/预览/刷新合同。未编辑并行建筑图标任务的Prefab、Icon绑定或图片。

### 补齐验证

- `SectorInteriorExplorationTests`增加8实例：正/反开始顺序共享有限补给，始终按稳定ID分配，实际可付时长同步扣料；六种非法探索存档布尔组合整批拒绝，包括前面已有有效变更记录的情况，所有星区Version保持不变。
- 扩展原`DysonPurchasedStagesSurviveDemolitionSaveAndRebuild`：同时真实付款购买雷门三结构，两工程拆除/存档/读档后六项购买记录保留；重复购买与第二座本体拒绝且库存不变；付费重建恢复燃料/物流倍率；雷门本体已拆除但探索记录被篡改时仍拒绝恢复。
- `HomeSystemDevelopmentPlayModeTests`修正建设按钮文字断言读取Build/Label，覆盖首建、各扩建和重建的完整高度；验证工程全部记录、当前/预览文案，以及购买和拆除后不重开详情的刷新。

### 当前证据

- `gap-baseline-play.xml` GPU PlayMode 2/2通过，退出0：此前图标缺失阻塞已由并行任务补齐固定Icon节点解除。
- `gap-fixed-play.xml`中间版GPU 2/2通过，但Unity退出清理停滞；核对PID38868命令行确为本轮batch后停止，退出-1。保留该证据，不称正常退出。
- 最终`gap-live-play.xml` GPU PlayMode 2/2通过、零失败/跳过，`gap-live-play-status.txt`确认UnityExitCode=0；包含购买/拆除后工程记录自动刷新断言。实际batch GameView不作为1920截图验收声明。
- `gap-fixed-edit.xml`真实Unity EditMode97/97通过、零失败/跳过，包含当前源码编译。具体退出清理结果另见该次status及本节收尾。两次最终日志未见CS编译错误、NullReferenceException、MissingReferenceException或AssertionException。
- 本轮不重复全新档372操作路线或模拟器/parity：结算和经济输入没有修改，既有证据见第20、22节；不把旧结果称为本轮重跑。未运行全量EditMode，原第18节4项断言失败和11项条件跳过未在本轮处理。

收尾：EditMode在97/97通过XML写完后停于Input System shutdown/LicenseClient断开，240秒runner超时结束该自建进程，`gap-fixed-edit-status.txt`为Failed(Timeout)，runner退出1；不称正常退出或全量验收。最终GPU为正常退出0。当前无Unity进程，PlanetDevelopment测试存档目录无残留；仅恢复本轮SIMSUN动态字形缓存到已核实blob `8da8976c89b1bfa0d58550d24d444ce78d0c8107`。`git diff --check`通过；无临时Unity项目/worktree，无真实存档改动，无commit/push。并行建筑图标改动保留。

本轮修复范围已由针对性真实Unity验证覆盖；未将Unity退出清理停滞或既有全量断言问题隐藏为已解决。下一步若继续开发，先复用本节97项与最终GPU两项作为相关回归，不因旧退出问题重复调数值或扩建平行系统。

## 2026-10-22：按生产依赖调整研究前置（本轮）

- 用户确认调整实际前置与解锁顺序。仅新增4条前置：PowerGridEngineering增加Coking（集中供电使用焦炭）；IndustrialMetalSmelting增加PowerGridEngineering（对应工业冶炼建筑需电网）；AutonomousOrbitalMining、OrbitalPropellantEngineering各增加HomeSystemSurvey（先本星系勘测再专业轨道开发）。成本、效果、时代、稳定ID、GUID保持。
- 修改前后分别执行 tools/codex/content-closure-check.ps1 -ProjectRoot . -ReportPath data/research-order-20261022/closure-before.md / closure-after.md，均退出0。143研究、92工坊、79建筑全部可达，33种资源可用；研究图无环、无逆时代前置。静态可达不代表实际等待节奏验收。
- 未强制深空舰队研究轨道推进剂，避免添加198M研究力及材料的额外强制路线；保留现有有效分支。
- 离线诊断 dotnet run --project tools/NewEconomySimulator/NewEconomySimulator.csproj --no-restore -- --json 构建失败NETSDK1004（缺obj/project.assets.json），证据diagnostic-before.json、diagnostic-before-status.txt。未安装包或修改工具目标框架，未运行测试套件。没有新等待时长/回本实测，不宣称节奏已实测平衡。
- 真实Unity编译随工坊资源导入统一执行；最终状态见本次工坊图片交接及data/workshop-art-20261022/compile-preview.log、unity-exit.txt。
- 最终统一Unity编译与工坊Prefab捕获已完成，NormalExit=True; UnityExitCode=0。closure-final.md重跑退出0，143研究/92工坊/79建筑全部可达；相关diff --check通过。未运行测试套件或新等待时长实测。

- 2026-10-22用户授权清理：research-order目录仅保留closure-final.md；早期closure-before/after及NETSDK1004失败诊断输出已删。错误与未验证边界记录在本交接，不将已删历史路径作为当前检查入口。当前四研究前置再次核对存在，源码/正式资源未改。
