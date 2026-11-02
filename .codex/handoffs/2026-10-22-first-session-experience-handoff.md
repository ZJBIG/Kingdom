# 2026-10-22 首局节奏、系统解锁与详情界面

## 授权与边界

- 用户授权改善首局剧情、教程、系统解锁、概览/时代/详情、图形信息与外显有效数值，并修复研究成本资源的前置链。明确不需旧存档兼容。
- 用户追加确认：定居聚落改普通研究，之后完成时代规划开放时代页，再单独研究石器时代跃迁。
- 初始工作树已有应用图标、其输出目录和 repo-cleanup handoff 修改，全部保留。无 commit/push、无真实存档操作，无临时 Unity 项目。
- 三个子代理分别处理解锁/剧情/教程、研究依赖/有效数值、UI/Prefab；主代理处理研究图缩放与预览关闭，并负责整合验证。

## 实现与字段变化

- `StoneAgeSettlement` 原稳定 ID/GUID 保留，改为 Animal 普通研究，BaseCost 1600→800，原资源费用和经济效果保留。
- 新增 `CivilizationPlanning`（Animal、BaseCost 200、前置定居聚落，完成后开放时代页）、`StoneAgeTransition`（StoneAge 跃迁、BaseCost 600、前置文明规划）。两项不重复收取原材料费用。
- 研究页在首次成功建成住房后永久开放；`HasBuiltHousing` 随新 v1 保存/恢复，失败建造不解锁，拆除最后住房不收回入口。
- 生产页在现有工业研究 `FactoryOrganization` 完成后开放；导航、跳转和教程共用强类型解锁规则。
- 早期剧情第3/4章分别要求实际完成农业/知识传承，第5章要求真实采石场与切石工坊，不再由几个教程点击连续完成。
- 教程引入情境叙述；首次显示先同步布局，后续每帧追踪目标几何，业务状态仍按既有集中节奏刷新。
- 研究图双指缩放固定手指中心对应的内容点；预览图内部空白可点击关闭，图片点击、拖动释放不会误关。
- UI 和研究依赖最终字段列表以本轮最终 diff 及下方验证记录为准，尚在集成时不将中间数量当作最终结论。

## 验证记录（持续补充）

- 修改前静态闭包 `data/first-session-20261022/closure-before.md`：退出0，143研究/92工坊/79建筑全部可达。
- 离线确定性诊断已尝试：`dotnet run --project tools/NewEconomySimulator/NewEconomySimulator.csproj --no-restore -- --json`，NETSDK1004，缺少 obj/project.assets.json；没有安装包，未得到本轮离线诊断通过结果。
- 开发程序集编译已尝试：`dotnet build Kingdom.Runtime.Developer.csproj --no-restore`，因清理后缺少 Unity Bee 编译元数据而退出1；该结果不是 C# 编译失败证据。
- 当前图变更后的静态闭包已达145研究/92工坊/79建筑；最终报告将随研究工坊来源链审计完成后重跑。
- 真实 Unity 编译、EditMode、GPU PlayMode 与具体退出情况待下方补充；此前历史输出不作为本轮通过证据。

## 收尾要求

- 运行前已将本轮无修改的 SIMSUN SDF 字体备份至 `data/first-session-20261022/font-backup/SIMSUN.asset`，SHA256 `6D5414E3DA1EE84328734DD7D283BBBF38BAAF144ABDAFAAA0EAC1E220FE6F50`。验证后仅恢复本轮动态字形变化并核对哈希，再清理此临时备份。
- UI 作者工具执行并保存正式 Prefab 后迁出 Assets 到本轮证据目录，不留下临时作者组件或脚本。
- 后续依据本轮 XML、真实日志、截图及最终差异完成验收；不得将静态可达或 fixture 测试描述为真人首局节奏实测。

## 最终实施记录

- 顶部八项改图形标签，正式 Prefab 的 GridLayoutGroup 固定两行、纵向填充；顺序 Food/Population、ResearchPower/Happiness、Territory/CurrentResearch、Power/Logistics。前六项靠左连续，基础设施未有可用生产建筑前隐藏。
- 新生成 HudStatusFrame.png（2172×724）和 TooltipHelpFrame.png（1536×1024），均导入为 sliced Sprite，保留独立 .meta；顶栏与长按说明使用不同皮肤。完整生成提示词见 data/first-session-20261022/ui-image-prompts.json。
- 概览使用实际住房、材料缺口、库存、净产出和预计等待，跳过教程后仍能导航到第一座木屋；时代卡整卡选择，右侧显示条件、阻碍、跃迁效果及队列操作；研究材料显示 required (owned)。
- 资源 pin 复用建筑56点击区域/44图标/同一横向位置；建筑材料充足与不足状态进入右侧详情。生产入口按钮也遵循 FactoryOrganization 解锁。
- ResearchManager.EffectiveResearchPower 统一基础研究力×全局效率×研究倍率×幸福度，实际研究速度复用并加时代倍率。详情、资源来源、pins、星区和工坊预览等使用对应实际公式。Food 的实际公式没有幸福度奖励，显示未凭空增加此项。
- 新增15条研究资源来源前置边：HerbalKnowledge→ClayExtraction；VillageOrganization→ClayExtraction；StoneAgeSettlement→Quarry；CeramicFiring→Mining；WrittenRecords→TextileCraft；IrrigationEngineering→CeramicFiring；AnimalFodder→CeramicFiring；CropRotation→TextileCraft；MechanicalEngineering→Steelmaking；Fortification→Steelmaking；IntegratedMetalResourceDevelopment→MassProduction；CombustionEngines→ScientificInstrumentation、ConcreteEngineering；MilitaryIndustry→ScientificInstrumentation、ConcreteEngineering。
- 研究来源审计包括建筑替代生产来源、隐式时代祖先和工坊 AND 链。中间曾添加 FeudalAdministration→Steelmaking 导致时代死锁，已撤回；最终该资产无语义 diff。失败静态报告 closure-after-research.md 留存，不能当作最终结果。

## 最终验证与证据

- author-final.log/status：真实 Unity 导入/编译、执行 EarlyGameUiAuthor.Run 并保存正式 Prefab，正常退出0。
- play-final.xml/log/status：GPU PlayMode 13/13通过，0失败/0跳过，Unity正常退出0。包含首次HUD/真实长按/时代选择/材料详情、研究图双指缩放、预览缩放拖动与关闭、教程高亮追踪及145节点研究树布局。
- edit-verified.xml/log/status：EditMode 60/60通过，0失败/0跳过；XML写出后Unity退出阶段超时180秒，包装进程结束了自己启动的PID。不能将其描述为正常退出。此前 edit-final.xml 也为60/60，其退出同样超时；保留原始记录。
- 上述真实Unity运行完成当前源码及测试编译。最终日志未发现 C# 编译错误、NullReference、MissingReference 或测试异常；许可证初始化提示保留在原始日志中。
- closure-final-verified.md：从新游戏静态闭包退出0，145研究/92工坊/79建筑全部可达，33资源。不是玩家节奏实测或真实tick parity。
- GPU2640×1200截图：overview-hud.png、tooltip-help.png、era-selected-detail.png、resource-pins.png、building-materials-detail.png、hud-infrastructure-unlocked.png。主代理与UI子代理检查了布局、隐藏与解锁位置、长按皮肤、概览行动和时代详情。上一轮截图已可恢复归档到 .codex/archive/first-session-20261022/first-play/。
- git diff --check通过；仅修剪本轮作者新增字段的尾部空白，没有格式化整个资产。
- 未运行全量项目测试；离线确定性诊断仍因 NETSDK1004 缺 obj/project.assets.json 未完成，未安装包。未做真人首局节奏实测。

## 清理与当前状态

- EarlyGameUiAuthor.cs及其.meta已迁至本轮data证据目录，Assets内不存在该临时工具；未创建临时Unity工程/worktree。
- SIMSUN SDF恢复到本轮运行前SHA256 6D5414E3DA1EE84328734DD7D283BBBF38BAAF144ABDAFAAA0EAC1E220FE6F50；临时font-backup文件及空目录已删除并确认不存在。
- 测试隔离存档目录由fixture清理，Temp内未遗留本轮 EarlyGameUi/StoryBackground/Onboarding/Gesture 名称目录；没有修改真实存档。当前无本轮Unity进程。
- 保留初始应用图标与repo-cleanup handoff修改；未commit/push。实现与限定范围验证已完成，无待实施事项；上述离线诊断缺口、EditMode退出超时和未跑全套保留为证据限制。

## 顶栏羊皮纸试作与按用户要求恢复

- 用户曾要求顶栏改偏黄羊皮纸、左上微卷；使用内置 image_gen 生成试作，改深棕图标/数字，并运行真实Unity作者导入退出0及GPU PlayMode 1/1通过、正常退出0。试作提示词、作者脚本、日志和截图保存在 data/first-session-20261022/parchment/。
- 随后用户明确要求“用原来的那个”，已恢复原深色铜边纹理、原Sprite Border80、pixelsPerUnitMultiplier16、白色图标与原数字颜色；恢复后 KingdomUIRoot.prefab SHA256 9994281C41723B0AF57BBFC30C95BD40C019E9054129CDBC8A059EAD21A5F6C7，与试作前记录完全一致。纹理与试作前备份SHA256相同；正式截图恢复为原深色版。
- 羊皮纸未保留为正式皮肤。临时作者脚本在data证据目录，Assets无残留；字体恢复并移除临时备份。此次恢复没有修改代码或玩法，也未重新运行已恢复相同输入的完整测试。

## 2026-10-26 农业研究后的新手目标

### 修改
- `Assets/Resources/Script/Manager/TutorialManager.Stages.cs`：研究引导优先定位农业；研究完成后触发独立 `farm-goal` 阶段，跳转建筑页并引导选择、建造农场。农场建成后完成该阶段；跳过农场目标只记录 `farm-goal`，不跳过研究阶段。
- `Assets/Resources/Script/Manager/SaveManager.cs`：教程存档新增 `FarmGoalStageSchemaApplied` 标记。旧存档若已完成/跳过/基线化研究阶段，且农业已完成但尚无农场，则把新增目标记入基线，避免已推进存档重播；农业尚未完成的存档仍会在未来完成时触发目标。
- 原有 `research` 阶段 ID、研究/建筑资产 ID 与 GUID 未改。

### 验证与未完成
- 已静态核对 `TutorialManager`、`KingdomUIRoot.Onboarding` 的调用链和现有阶段调用点；UI 每 0.2 秒重新评估引导。
- `git diff --check -- Assets/Resources/Script/Manager/TutorialManager.Stages.cs Assets/Resources/Script/Manager/SaveManager.cs` 通过。
- 本轮未添加或运行测试，未执行真实 Unity 编译；需要 Unity 证据时再跑对应的阶段 EditMode/PlayMode 验证。
- 无玩法经济数值、建筑成本或研究效果变化。

## 2026-10-27 引导等待状态与文案收敛

- 保留上述农业完成后的独立农场目标；标题缩为“建造农场”，正文为“农业已完成，建造一座农场。”，复用现有引导层。
- `TutorialManager.Stages.cs`：研究排队时 `CanInteract` 从默认 true 改为 false，不能再通过“下一步”误记完成；只显示材料已支付/未支付的简短状态。
- `KingdomUIRoot.Onboarding.cs`：等待时保留排队状态；建造/研究受阻时显示目标名称和 Manager 返回的实际原因。沿用等待路径解除输入门禁，隐藏下一步；资源恢复后沿用正常刷新。
- 已有测试文件补充排队不可误完成的 EditMode 回归，以及排队状态/农场缺料恢复的两个 PlayMode 用例。修改前已执行单个 EditMode 回归，1/1 按预期失败于 `CanInteract=true`，证据 `data/experience-20261027/edit-before.xml` 和对应日志。该测试进程写出 XML 后退出停滞，核验为本轮 PID 24608 后终止，退出 -1；不记为正常退出。
- 最终真实 Unity 编译通过，进程正常退出 0：`tools/codex/compile-unity.ps1 -ProjectPath D:/GitHub/Kingdom -UnityPath D:/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe -LogPath D:/GitHub/Kingdom/data/experience-20261027/compile-final.log -TimeoutSeconds 180`。首次编译发现局部变量同名，已改为 waitingInstruction；首次失败日志保留为 compile-first.log。
- 最终代码未执行 EditMode/PlayMode 测试，不宣称修复后回归或 UI 运行验收通过。触及源码/测试的 `git diff --check` 通过；未修改经济资产、ID/GUID或真实存档。
- 原有未提交字体从本轮原样备份恢复，SHA256 与 font-before.sha256 相同，备份已移除；无 Unity 进程或 StagedOnboarding 临时目录残留。
- 下一步如需运行验证，使用 TutorialStageTests 与新增两个 PlayMode 用例；保持隔离存档与现有字体修改。

## 2026-10-30 首局连续成长引导实施与验证

### 起点与范围

- 按用户的首局成长实施清单完成：木屋 → 农业 → 农场实际供粮 → 采石研究/建筑 → 石料加工研究/切石工坊实际产出 → 真实定居前置 → 定居聚落 → 文明规划 → 石器跃迁；人口反馈并行。
- 基础 HEAD `5ade66a7d409edda4ca60805bc015686389c0e07`；修改前相关未提交差异保存在 `data/experience-20261030/first-session/pre-existing.patch`。保留既有 SaveManager、StoryManager、其他 UI/测试及清理变更；不归入本轮成果。
- 继续遵循本轮已读取的 Ponytail、Kingdom 项目/UI/验证规则；子代理只做指定测试补充与终审，最终整合和 Unity 验证由主代理完成。

### 文件与行为

- 新增 `Assets/Resources/Script/Manager/TutorialManager.FirstSession.cs` 及 `.meta`（GUID `58d50a9fc87346b5a88df62fde960aae`），在现有 Manager 内生成唯一首局行动。`TutorialManager.cs` / `.Stages.cs` 将概览与操作课接入同一目标、页面、对象和阻碍；增加 ActionId、Benefit、NextGoal、IsWaiting、CanAct、CompletedFeedbackId 快照字段，无新任务框架。
- 开局从木屋开始，买不起也保留目标；真实住房建成后直接研究农业，不等人口。资源教学整合到木屋缺口与实际建造支付，真实 HasBuiltHousing 后幂等完成 `resources` / `building`，保留 FirstFire 剧情所需 ID。研究排队/进行中不记完成。
- `SimulationManager.cs` 在在线/离线正时长生产段后记录农场和加工产出；零秒刷新、关闭/零效率建筑不得完成。成果 `farm-supply` / `stone-supply` 使用原 CompletedStepIds 列表保存，恢复白名单接受这两个 ID；没有更改存档版本或新增序列化字段。
- 新操作阶段 `stone-chain` / `first-transition`；保留原 stage/step ID、完成与跳过记录。首次跃迁完成后解决 first-transition / era-goal 操作课，未完成人口课只记基线，接回 authored long-term；不会重新弹资源课或重置普通目标。
- `KingdomUIRoot.LiveRefresh.cs` / `.Onboarding.cs` 删除概览独立首屋决策，统一刷新快照；读档重绑选择，缺料/队列/研究/生产等待解除门禁。成果按稳定 ID 去重，原面板显示 1.5 秒现场反馈后接下一目标；概览保留最近进展。
- 修复真实点击发现的等待恢复后遮罩几何滞后：LateUpdate 之外在 Canvas.willRenderCanvases 同步目标位置，OnDestroy 退订。时代跃迁定位真实卡片与研究详情按钮。
- 同步 `Resources/Datas/Tutorial/Orientation、Resources、Building、Population、Research、ProductionChain、EraGoal.asset` 与内置回退文案。Orientation 条件从首日改为真实住房；无经济值、资产 ID、链路或已有 GUID 变化；现有正式面板足够，未改 Prefab。
- 回归覆盖在 `TutorialStageTests.cs`、`TutorialManagerTests.cs`、`KingdomStagedOnboardingPlayModeTests.cs`，没有反射。保留原文件其他未提交改动。

### 最终验证

- 完整命令、输入哈希、迭代与限制见 `data/experience-20261030/first-session/verification.md`，最终源码/资产指纹见 `verified-inputs.json`。
- 真实 Unity 编译通过，正常退出 0：`compile-final.log` / `.exit`。
- EditMode `TutorialManagerTests;TutorialStageTests;StoryManagerTests`：78/78，0 失败、0 跳过，正常退出；`edit-verified.xml` / `.log` / `-errors.txt`。
- GPU PlayMode `KingdomStagedOnboardingPlayModeTests;KingdomOnboardingPlayModeTests`：27/27，0 失败、0 跳过，Unity/包装退出 0；`play-gpu-complete.xml` / `.log` / `.exit`。早期失败迭代保留但不作最终通过证据。
- 无资源赠送的真实按钮路线已达 StoneAge，合计 4484.8677 秒模拟时间。`actual-waits.log/csv` 保存分段；定居聚落行动 3565.93 秒，其中缺料 3390 秒、研究 175.93 秒。未调整数值，也不把该时间视为真人节奏测评。
- 静态闭包 `closure.md`：145 研究/92 工坊/79 建筑全部可达。离线诊断仍为 NETSDK1004（缺 simulator obj/project.assets.json），未 restore/安装，不宣称该诊断通过。
- 八张 GPU 截图已检查首屏、缺料、建造高亮、建成反馈、研究等待、农场/加工等待及跃迁后页面；测试检查中文正文高度。最终日志未发现 C# 错误、相关异常或缺字警告；许可证启动提示保留。
- 限定源码/测试/教程资产的 `git diff --check` 通过；未执行全项目测试。

### 收尾与下一动作

- 测试使用 `Temp/StagedOnboarding-*` 隔离存档并已清空；无本轮 Unity 进程，没有创建临时项目/worktree。
- 字体精确恢复本轮运行前已有修改，SHA256 `15A052DF327A1E9D034458C890CCADCBB5F8723008D76C39D28213BF683C9A71`，恢复记录 `font-restored.txt`；32MB 临时备份已删除，哈希记录保留。
- `TestResults/Latest-Test-Errors.txt` 指向最终非零 PlayMode 通过证据；未 commit/push。授权的首局引导实施与相关 Unity 验证完成。后续若单独授权节奏调整，从上述定居材料等待记录开始，不重复实现基础系统。

## 2026-11-01 等待推荐与插图期间的引导让位

- 后续玩家体验修复更新FirstSession等待推荐：已排队/研究中查看进度与材料、已有建筑待产查看实际产出，覆盖来源研究分支；不改变成长路径或操作规则。打开图片时暂时释放操作gate，关闭后恢复原阶段，无完成/跳过记录变化。
- 四条相关EditMode用例XML 4/4通过但Unity退出超时；插图及正文PlayMode 2/2正常通过退出0；最终真实编译退出0。详见主交接2026-10-24-player-experience-qa-handoff.md的2026-11-01节和data/experience-20261101/。原first-session/verified-inputs.json与78/27用例证据属于2026-10-30版本，不覆盖本次源码增量。

## 2026-11-02 新手引导文案体系重写（玩家视角）

### 授权与范围

- 用户认为新手引导不到位，要求从"玩家需要看到什么文案、引导怎么引导、怎样才会沉迷"三个问题出发改进。本轮授权理解为教程/引导文案与文案拼装逻辑；经济数值、节奏、ID/GUID、存档结构、Prefab 均未动。定居聚落缺料 3390 秒等节奏问题仍属另一授权范畴，未触碰。

### 改动（纯文案，15 个文件）

- 8 个教程资产 `Assets/Resources/Datas/Tutorial/*.asset` 与 `TutorialManager.cs` 内置回退链同步重写：标题动词化/画面化（"理解库存与净产出"→"族人的生计账"、"形成长期目标"→"这场复兴没有终点"等）；Description 统一为"做什么+为什么值得"；NarrativeText 给世界观钩子。Id/Kind/NextStepId/触发与完成条件/导航页面全部不变。Resources 文案保留"食物/原木"具体资源点名（测试与引导意图要求）。
- `TutorialManager.Stages.cs`：StageNarrative 与六条阶段指令改为世界语言（"查看上下游…"→"顺着原料往下走：上游供得上，下游才不停工。"等）。
- `TutorialManager.FirstSession.cs`：六条里程碑完成反馈改为成就陈述（"木屋已建成，研究已开放"→"第一处屋檐落成了——研究已经开放，居民正在路上。"）；两处"备齐"文案微调。
- 等待钩子：`EvaluateFirstSessionStage` 等待状态（排队/研究中/待产/不可行动）的 Instruction 追加"完成后：<NextGoal>"，复用现有 NextGoal 字段，新增静态方法 WaitingInstruction；无新字段、无存档变化、无逻辑分支变化。
- `KingdomUIRoot.Onboarding.cs`/`KingdomUIRoot.Story.cs`：完成反馈标题"刚刚改变"→"新进展"。

### 测试与验证

- 同步更新断言：`TutorialManagerTests.cs:168` 标题断言改"族人的生计账"；`KingdomOnboardingPlayModeTests.cs:328` 改"新进展：真实行动反馈测试"。保留"进度与材料"/"实际产出"/"建造 1 座"等被断言短语。
- 真实 Unity 编译退出 0：`data/experience-20261102/compile-final.log`（含 "return code 0"）。
- EditMode `TutorialManagerTests;TutorialStageTests`：62/62 通过，0 失败 0 跳过，`edit-verified.xml/.log`。
- PlayMode `KingdomOnboardingPlayModeTests;KingdomStagedOnboardingPlayModeTests`：28/28 通过，0 失败 0 跳过，包装器退出 0，`play-verified.xml/.log`；原始日志尾部为内存统计输出，无独立 "return code 0" 行，如实记录。
- SIMSUN SDF 字体运行前备份 SHA256 `15a052df…683c9a71`，验证后恢复并 `sha256sum -c` 通过，临时备份已删除。
- `git diff --check` 通过；无 Unity 进程残留，无本轮临时存档目录；未 commit/push，未做真人首局体验实测与截图验收。

### 未做与后续候选

- 节奏/数值（如定居聚落长等待）需单独授权；新视觉庆祝形式（里程碑 toast 等）受 Prefab 创作约束，需在 Editor 作者流程中另立项。
- 未跑全项目测试；首局真实游玩节奏仍无真人实测。

### 同轮续接：导航、真实预告与渲染复核（最终状态）

- 上节“纯文案15文件”是初始版本记录；最终增加阶段文案选择与回归验证。完整当前证据入口为 `data/experience-20261102/verification.md`，输入见同目录 `verified-inputs.json`，不把最初62条与无图形测试作为最终显示验收。
- 首次木屋导航明确建造收益与点击入口；缺料时先完成导航教学、进入详情后再显示阻碍。排队/研究中/生产等待保持释放输入；前置研究的 NextGoal 回到实际尚未完成的目标，材料来源分支同步 Benefit/NextGoal，避免预告跳步。
- 收紧事实：净产出“每天”改“每秒”；农场首次供粮不再承诺粮食充足；木屋完成反馈压缩为“屋檐落成，研究开放。居民将陆续入住。”；长期标题最终为“筹备下一次时代跃迁”，正式资产和回退一致。没有经济数值、ID/GUID或存档改动。
- 两项回归先复现失败；修正后 EditMode 63/63 正常退出。压缩正文后的再次 EditMode 63/63 写出，但Unity退出阶段超时180秒，由包装器结束其自有进程，不能记为正常退出。
- 真实编译退出0；GPU初次26/28抓到正文溢出，压缩文案后28/28、Unity退出0；最后长期目标字面量修改后，完整首局真实按钮/自然生产路线1/1、Unity退出0。GPU正文高度检查未放宽，相关截图已直接检查。详见 verification.md 的版本与证据范围。
- 字体按原内容恢复并删除临时备份；无本轮 Unity/Temp 存档残留，无临时项目。保留原有 `.workbuddy-ai/memory/`，未修改或归入本轮成果。未运行全项目测试、未调整经济节奏，不声称真人留存效果。

## 2026-11-02 续接：等待可读性与成果停留

- 沿用 `D:/GitHub/Kingdom` 当前工作树，没有创建新分支或临时项目。保留开始时已有的全部教程资产/源码/测试改动和 `.workbuddy-ai/memory/`，没有 commit/push。
- 读取主入口、UI/测试规范和本机 `ponytail/5.3.3/skills/ponytail/SKILL.md`（full正文），独立子代理只读审查等待与成果交互并复核最终增量。
- `TutorialManager.FirstSession.cs`：农场/工坊待产提示加入已有真实 `Benefit`；零劳力时说明等待居民入住，否则指向建筑页的产出/停工原因。研究等待补充研究/时代页查看位置。农场的紧接目标缩为“研究采石”，工坊紧接目标缩为“研究定居聚落”，适配原572×108正文，不增加面板或新任务系统。
- `KingdomUIRoot.LiveRefresh.cs`：成果停留1.5→4秒；`KingdomUIRoot.Onboarding.cs`复用已有Next提前收起，按实际显示的成果模式判断，只清成果timer，不确认下一课程。等待依旧隐藏Next并释放输入门禁。
- 新增1条EditMode（无居民农场停工原因及入住后推进），2条PlayMode（成果可读/真实Next关闭且不确认下一课；跨页待产真实收益、页面位置及正文高度）。既有路线夹具以真实点击收起成果再继续操作，保留等待Next=false和gate释放断言。
- 修复前 `edit-production-before.xml/.log`：1/1按预期失败，缺少入住说明；Unity退出2，非通过。第一次GPU `play-gpu-continuation.xml` 27/30（新成果按钮与旧夹具冲突）；第二次 `play-gpu-continuation-final.xml` 28/30（待产三行正文112.95>108）均保留。修复夹具交互并压缩文案，没有放宽断言。
- 最终GPU `play-gpu-continuation-accepted.xml/.log/.exit`：30/30，0失败/跳过，Unity退出0，RTX4060渲染；正文高度检查通过。真实按钮、自然资源、ManualTick完成StoneAge，4484.8677模拟秒；不是真人游玩时间。最终原始日志未发现C#编译错误、Null/MissingReference或缺字警告。
- 最终EditMode `edit-continuation-final.xml`：64/64，0失败/跳过。Unity退出状态与字体恢复将在收尾记录补充；不将XML写出等同正常退出。
- 本轮没有经济定义变更，不扩展策略搜索或自动调参。首局材料长等待仍未改善，用户“更愿意继续玩”的完整目标不能用教程测试通过代替。后续具体动作：从真实路线定居聚落约3390秒材料等待定位扩产教学/回本，再按经济技能前后闭包、诊断和Unity门槛推进；没有把经济节奏永久排除在用户原始目标之外。

### 续接收尾确认

- 最终EditMode用例64/64通过、0失败/跳过，但XML写出后退出停滞；180秒包装超时，仅结束自己启动的Unity PID5812。包装结果 `Failed(Timeout)`，不能称为正常退出。较早同轮 `edit-continuation.xml` 为64/64并正常退出，最终正文缩短增量另由上述GPU30/30覆盖；如需严格复现仍以最终XML和原始日志为准。
- 字体已恢复运行前SHA256 `15A052DF327A1E9D034458C890CCADCBB5F8723008D76C39D28213BF683C9A71`，核对成功，临时备份已删除且不存在。无Unity进程、无Temp/StagedOnboarding目录；未创建临时工程。
- 最终输入指纹 `data/experience-20261102/continuation-verified-inputs.json`；`git diff --check`通过。本轮已执行真实Unity编译（EditMode/GPU测试启动重新编译当前源码/测试），GPU全部通过并正常退出0。未运行全项目测试；未验证真人留存，首局定居长等待仍是下一具体工作项。


## 2026-11-02 续接：首局扩产教学与跃迁详情刷新

- 沿用现有 D:/GitHub/Kingdom 工作树；保留已有修改，没有创建项目副本、worktree、分支或提交。
- TutorialManager.FirstSession.cs：first-transition 缺研究材料时检查已安装来源的产能盈余，建立尚缺的材料来源；现货足够或研究已付清时直接保留研究。已有生产力与空余住房容纳的潜在生产力不足以支撑下一来源时，先引导木屋，明确居民入住后才增加生产力。木屋缺木时保持等待，防止木屋与伐木场互相递归。
- 安装产能比较包含永久研究倍率，不使用临时幸福、事件或停工效率来决定反复加建。已有基础盈余但当前净产出为零时只提示到建筑页检查劳力/停工；避免居民尚未入住时连续要求加建。已有来源基础产能不足以覆盖加工消耗时，仍能引导补上游。普通建筑推荐由“检查并启用”改为真实的建造与查看产出操作。没有修改经济资产、GUID、存档字段或版本。
- 注意纠正中途诊断：单采石场的原始1.8不能作为实际供给，采石与石料加工研究具有永久倍率。最终自然路线没有强制第二座采石场，不能宣称实际存在石料倒挂。
- KingdomUIRoot.LiveRefresh.cs：时代页刷新时，仅将已完成、且不高于当前时代的跃迁详情切换到下一时代目标；其他研究选择保留。切换同步更新实际选中研究及按钮对象，并重置新详情滚动位置。解决中央“迈向中古时代”、右侧仍为已完成“迈入石器时代”的截图问题。
- Editor新增4条覆盖正收入首来源、住房容量后回来源、加工上游不足、零效率已有产能不重复扩产。正收入首来源/住房2条修前2/2失败；正收入上游修前1/1失败。上游最初夹具非正收入已能触发现有逻辑，随后修正夹具；中间一次测试使用internal API导致编译失败，已改用公开State。
- 最终EditMode：edit-expansion-complete.xml/.log/-errors.txt，68/68通过，0失败/跳过，包装正常Passed。中间edit-expansion-final为旧输入66/67并退出超时；重叠启动edit-expansion-accepted触发项目占用而无XML，不作为通过证据。后续等待原包装仅结束其自有PID19692后再串行验证；未结束用户Editor。
- 最终GPU PlayMode：play-gpu-onboarding-final.xml/.log/.exit，31/31通过，0失败/跳过，Unity退出0。含真实按钮、无资源赠送、自然生产首局路线，以及新增跃迁详情回归。针对详情回归修前1/1失败、修后1/1通过。测试检查引导正文符合原有面板高度与等待输入释放。
- 首次跃迁自然路线由4484.86770353102降至2939.60853572061模拟秒，约减少34.45%；新增伐木场、黏土坑、纤维采集营地及住房支持。此为ManualTick真实Unity路线，不是玩家实时时长或留存效果。中间首座扩产路线2939.6151秒同样到达StoneAge。
- 修改前后静态闭包closure-expansion-before.md/after.md均可达：145研究、92工坊、79建筑。未变经济定义。离线诊断前后均NETSDK1004，缺tools/NewEconomySimulator/obj/project.assets.json；未restore或安装，不能称该诊断通过。
- 最终GPU日志未发现C#编译错误、Null/MissingReference或缺字警告。本轮已执行真实Unity编译；未运行全项目测试。截图保存screenshots/；上一轮截图已可恢复保留在screenshots-before-expansion、screenshots-first-expansion、screenshots-before-era-detail-fix。用户发来的旧详情截图是问题证据；不把截图文件存在等同人工视觉检查。
- 字体已精确恢复到运行前SHA256 15A052DF327A1E9D034458C890CCADCBB5F8723008D76C39D28213BF683C9A71。删除本轮32MB临时备份的命令被执行策略拒绝，未换方式删除；保留data/experience-20261102/expansion-font.backup。无Unity进程、无Temp/StagedOnboarding临时存档。
- 已改善首局操作、收益解释、成果可读性、材料扩产与时代交接；没有证据宣称真人留存或“沉迷”效果。若继续评估首局节奏，当前剩余聚落材料积累应基于上述真实输入和日志，不重建基础系统或扩展模拟器策略搜索。

## 2026-11-02 续接：材料等待按实际瓶颈提示

- 原自然路线日志显示：定居聚落先提示原木约464秒，原木足够后才提示石块还需约1014秒。共享 FindMissingResearchResource 原来按定义顺序取首个缺料，不能代表材料备齐时间。
- 当前改为优先没有正净产出的缺料，否则选择（需求减已付款减库存）/当前净产出最大的缺料。继续使用当前幸福奖励后的实际速率，按刷新更新估计；不承诺固定倒计时。零净产出且已建来源时，明确材料名称并指向建筑页劳力/停工原因。没有修改生产、研究成本或经济定义。
- 新增参数化回归2例：较慢石块不能被较快原木遮蔽；停产石块不能被原木倒计时遮蔽；石块备齐后回到尚缺原木。修前 edit-material-eta-before.xml 为0/2，预期失败；修后 edit-material-eta-final.xml 为70/70，包装正常退出0。
- GPU play-gpu-material-eta-final.xml/.log/.exit：31/31，0失败/跳过，Unity退出0；实际自然路线从定居等待开始即显示石块瓶颈。原面板正文高度与真实点击/等待释放检查保持通过；未发现C#编译错误、Null/MissingReference或匹配的缺字警告。本轮真实Unity编译已执行；没有重复全项目测试。
- 截图前版本可恢复保留于 screenshots-before-material-eta，新截图仍为 screenshots/。不将截图文件存在当人工视觉检查。字体恢复原SHA256 15A052DF327A1E9D034458C890CCADCBB5F8723008D76C39D28213BF683C9A71，无字体资产diff；既有backup继续保留，不重试被拒绝删除。
- 沿用当前工作树，未新增项目/分支/提交，保留既有工作。此次消除误导等待预期，不声称减少实际材料等待或证明真人留存。首局仍有材料积累时间，真实留存效果没有实测证据。

### 2026-11-02 续接：总览缺料可直达来源

- 总览中明确提及的短缺材料与已建但停产材料现在可点击，直接打开对应资源详情，显示现有供需净率及可定位的生产建筑。链接按精确短缺句式绑定，避免从任意说明文本中误匹配重名/子串资源。沿用当前工作树。
- KingdomUIRoot.ReviewActions.cs 增加 resource 详情导航；KingdomStagedOnboardingPlayModeTests.FirstSession_RealButtonsAndNaturalProductionReachStoneAge 在自然首局真实缺料节点检查文案、链接对应资源和资源详情；KingdomPlayModeTests 标题断言同步“当前条件”。
- 修前因测试类型未限定导致编译失败的证据保留在 data/experience-20261102/play-gpu-overview-material-final.log。修后 data/experience-20261102/play-gpu-overview-material-final2.xml/.log/.exit 33/33全通过、Unity退出0；包含自然路线、总览刷新只读与资源来源可点击测试。git diff --check通过。
- 字体恢复至运行前已有内容SHA256 15A052DF327A1E9D034458C890CCADCBB5F8723008D76C39D28213BF683C9A71；既有备份保留。无Unity进程，无临时项目/存档残留。
- 这一处让玩家从“缺什么”直接跳到“哪里产、谁在产”；不声称已证明真人留存或沉迷效果。

## 2026-11-02 收尾：材料积累期间给出可执行选择

- 研究尚未排队且缺料具有正净产出时，总览建议“查看缺料来源；保存后可暂离，回来查看离线进度。”沿用已有离线结算与保存机制；停产时继续优先提示恢复生产，已排队或研究中的操作说明保持对应状态。固定引导浮层未额外增加离线说明，避免挤占正文。
- TutorialStageTests.ResearchWaitIdentifiesTheLastMaterialToBecomeReady 两个分支同时覆盖正常积累与停产：正常积累包含来源/暂离选择，停产不显示等待估时或暂离建议。
- 最终 EditMode：edit-offline-choice-final.xml/.log/-errors.txt，70/70通过，0失败/跳过，包装正常Passed。最终GPU PlayMode：play-gpu-offline-choice-final.xml/.log/.exit，33/33通过，0失败/跳过，Unity退出0。已执行真实Unity编译；扫描这两份原始日志未发现C#编译错误、NullReferenceException、MissingReferenceException或匹配的缺字警告。没有运行全项目测试。
- 从既有备份恢复测试生成的SIMSUN SDF.asset，SHA256核对为15A052DF327A1E9D034458C890CCADCBB5F8723008D76C39D28213BF683C9A71，备份保留；收尾确认无Unity进程、无Temp/StagedOnboarding临时存档目录。沿用D:/GitHub/Kingdom现有工作树，未创建临时工程或新worktree。
- 最终针对性检查：git diff --check通过。经济定义未变；离线模拟器仍因缺project.assets.json未运行成功，未执行restore。首局自然路线到StoneAge的模拟耗时改善有测试证据，真人留存效果尚无实测数据。

### 2026-11-02 目标交付复核

- 主代理检查了当前关键截图：首次木屋导航、供粮等待、石材加工与跃迁后的下一时代详情。引导正文和下一步在既有面板内可读；成果可用“明白了”主动收起。部分截图文件名描述的是测试检查节点，画面中的状态以实际截图及相应测试日志为准。
- 独立只读子代理复核当前源码、diff、输入门禁、缺料来源和最新XML，未发现需要继续修复的明确缺陷。主代理另核对最新EditMode 70/70、GPU PlayMode 33/33、GPU退出0及自然路线2939.60853572061模拟秒；20个相关源码、测试及教程资产的修改时间均早于最终GPU测试启动。没有因收尾复核重复启动Unity。
- 本次目标的实现与相关验证已完成：具体行动、收益解释、真实生产反馈、下一个目标、瓶颈来源、扩产教学及等待选择连成首局循环。已改善的自然路线模拟耗时由4484.86770353102降至2939.60853572061，约减少34.45%；不将其换算成真人操作时间或留存结论。未运行全项目测试，既有离线诊断缺口与备份保留情况见上文。
- 收尾无Unity进程、无Temp/StagedOnboarding隔离存档目录；沿用当前工作树及原交接，没有新工程、提交或发布。后续真人留存分析需以实际玩家反馈为输入，本目标无待实施事项。

### 2026-11-02 清理与 Android 交付

- 将 screenshots-before-*、screenshots-first-* 和 screenshots-suite-passed 共 8 个旧截图目录移至 .codex/archive/2026-11-02-experience-screenshots/；逐项确认原路径不存在、归档路径存在。当前 screenshots/ 和验证证据保留。历史交接对这些旧截图的路径应以该归档目录为准。
- expansion-font.backup 此前删除被执行策略拒绝，继续保留，未绕过重试；个人笔记和其余验证证据保留。
- 使用 tools/codex/build-android.ps1 构建，Android 设置、UI 合约及 YAML 引用前置检查通过；Unity 正常退出 0，日志 Logs/codex-android-build-20261102.log。APK Builds/Android/Kingdom.apk，212178071 字节，SHA256 BFA74C7CA4421682AFB158901118269856AB31B83306EE2683C25C1A16FC07F6。apksigner 校验 v1/v2 通过，Android Debug 证书；报告两个 META-INF 条目未受 JAR 签名保护的警告。
- 此次构建使用当前首局改动；最终定向 EditMode 70/70、GPU PlayMode 33/33 为此前同主题验证，详细记录见本交接和 data/experience-20261102/verification.md。本次未重复测试。
