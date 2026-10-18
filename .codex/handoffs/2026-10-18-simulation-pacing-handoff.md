# 模拟修复与节奏交接（2026-10-18）

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
