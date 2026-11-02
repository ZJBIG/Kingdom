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
