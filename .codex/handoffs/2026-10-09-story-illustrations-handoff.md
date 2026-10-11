# E 剧情插画实装

用户本轮授权：将已完成插画配合剧情实装并上传仓库；恢复此前暂缓的 E。工作分支 `codex/ultra-project-manager-tick-tests`，起点 `5f623c7`。

## 实装范围

- 接续已有未提交 Story 改动，加入 18 张 `Assets/Resources/Art/Story/Story_<章节名>_Main.png` 及原有 GUID 的 `.meta`；各章 `illustration` 从无引用变为对应单 Sprite。Runtime 只透传引用，不修改剧情进度。
- 章节稳定 ID、顺序、标题、摘要、正文、时代、教程/研究/建筑/工坊/星区门槛语义保持原样；Unity 作者工具产生了等价字段排序和 Unicode 序列化变化。
- `StoryChapterCard.prefab` 创作固定 Header、按钮、图片占位和正文布局；最新完成章节展开，历史章节可展开/收起，未完成章节不绑定图片。保持图片比例，隐藏占位回收高度，相邻卡片随高度变化移动。
- 作者工具导入配置：Single Sprite / FullRect、2048 上限、无 mipmap、CompressedHQ、Bilinear、Clamp。运行时实例化数据驱动重复卡片，不创建固定卡片内部层级。
- 恢复两个按钮的既有 `UIPageScrollDragForwarder`；沿用现有声音、导航与页面滚动。
- 更新 Onboarding 测试以匹配 authored Header；在卸载 SampleScene 的保存写入对象后才解除测试存档隔离。新增 EditMode 与 PlayMode 插画覆盖及数据维护文档。
- 无经济定义、v9 存档格式或真实用户存档操作。未纳入字体缓存、客户端个人记忆、孤立 Android.meta 或 tmp 文件。

## 本轮验证

- 真实 Unity 2022.3.62f3c1 作者工具：`-batchmode -quit -executeMethod StoryIllustrationAuthoring.AuthorStoryPresentation`。`Logs/outputs-story-authoring-20261009.log` 记录 18 张导入和绑定及正常退出；实际完成 Unity 源码编译与 Prefab 作者流程。
- 章节/插画 EditMode：`-runTests -testPlatform EditMode -testFilter StoryIllustrationTests;StoryManagerTests`。r1/r2/r3 XML 均 17/17，无失败/跳过；最新为 `TestResults/outputs-story-editmode-r3-20261009.xml`，日志 `Logs/outputs-story-editmode-r3-20261009.log`。三次在完成 XML 后的网络请求清理/原生退出阶段停滞，只终止核验命令行后的本代理进程；不能作为正常 exit0 的测试轮次。改变图形模式未解决，停止重复。
- 真实 Scene/Canvas PlayMode：`-batchmode -force-d3d11 -runTests -testPlatform PlayMode -testFilter StoryIllustrationPlayModeTests;KingdomOnboardingPlayModeTests`。`TestResults/outputs-story-playmode-r2-20261009.xml` 19/19，无失败/跳过，Unity exit0；同名日志位于 Logs。覆盖 18 章图片对应、锁定隐藏、最新导航、展开收起、标题/正文容纳、相邻卡片不重叠、页面切换位置保留、滚动，以及 EventSystem 指针事件从按钮开始拖动。实测内容从 `(0,2103.84)` 移动到 `(0,2598.84)`。
- PlayMode 首次误在前一 Editor 进程仍退出中启动，因项目占用失败，无有效用例；记录 `Logs/outputs-story-playmode-20261009.log`，未算通过。
- EditMode 后追加已通过的 PlayMode 没有业务源码变更；移除批处理未产出的截图请求，最终测试代码不声称像素截图验收。章节空字段尾空白清理和 `git diff --check` 通过。
- 子代理只读复核运行时传递、Prefab 绑定和章节语义；另一个子代理验证现有代理下远端 SHA 与本地起点一致。Git 上传使用单次 `-c http.proxy=http://127.0.0.1:7890 -c http.sslBackend=openssl`，不改变全局配置或关闭 TLS 校验。

## 限制与续接

EditMode 断言完成但原生退出问题尚未解决，不能把该轮称为正常完成；真实 Unity 编译与 PlayMode 正常完成。自动审批拒绝了设置 Unity 子进程代理的组合命令，仅返回 `blocked by policy`，未绕过。未执行全仓测试或设备触摸体验；外部运行由用户自行验收，不构成代理待完成门槛。后续针对 EditMode 退出问题先核对网络清理日志，不盲目重复测试。

本轮交付到上述现有分支；提交推送与远端精确 SHA 以最终回复为准，不自动合并 main。

## 2026-10-09 main 合并续接

用户追加授权：把全部待合并提交合入 main 并上传 GitHub/云端 repo。现有唯一 remote 是 `origin=https://github.com/ZJBIG/Kingdom.git`。

- 远端 main 起点 `11b3e26e4c31ec4923b88afd50d151a052358ce5`；远端两个开发分支分别为剧情/遗迹 `ea3a22c46be597371abbd2f70268f7dafe07bba6`、保存修复 `da8d6a5e8f89b0f93f547f97971019540208b578`。先快进 main 至剧情提交，再常规合并保存分支，产生 `d2323a0b37478e64d5abcc7061214ae0b30808fa`，无冲突、无 force/rebase/amend。
- 保存修复仅包含必要嵌套列表的 JSON/DTO 校验及原提交测试；另一个工作树 `D:/GitHub/Kingdom-source-audit` 尚未提交的 BuildingManager/KingdomLogicTests 修改保留，不因“全部合并”上传未完成工作。原工作树字体、个人记忆、临时文件同样保留。
- 保存修复与遗迹旧测试夹具不兼容：`RelicSaveTests.SaveJson` 原先 Resources/Buildings/Researches/Workshop/Sectors 为 `{}`，违反新必填列表契约。仅将夹具补成含合法空数组的各段，未修改运行逻辑、存档格式、拒绝规则或放宽断言。
- 首轮 `TestResults/main-integration-editmode-20261009.xml` 162 项中 160 通过、2 个上述夹具失败；修正后 `TestResults/main-integration-editmode-r2-20261009.xml` 162/162，无失败/跳过，覆盖 KingdomLogicTests、RelicSaveTests、StoryIllustrationTests。日志同名位于 Logs。两轮均在 XML 完成后的原生/网络请求清理阶段停滞，仅终止核验命令行后的本代理进程，不能声称 Unity exit0。
- 合并状态执行真实 Unity 源码编译，并运行 `StoryIllustrationPlayModeTests;KingdomOnboardingPlayModeTests;RelicPlayModeTests`：`TestResults/main-integration-playmode-20261009.xml` 25/25，无失败/跳过，Unity exit0；日志 `Logs/main-integration-playmode-20261009.log`。最终源文件无 C# 编译错误/NullReference/MissingReference。
- Git ancestor 检查确认两个开发分支的全部已提交历史都在 main 中。仅提交此次合法夹具调整与本交接；最终 main 上传及精确远端 SHA 由最终回复记录。没有删除开发分支或改变其他 remote。

当前限制仍为 EditMode 原生退出停滞；本轮没有重跑全仓测试，未改变先前外部体验验收边界。

## 2026-10-11 插画背景卡片与独立预览

用户授权：展开章节改为完整 16:9 插画背景，标题、摘要、正文与按钮叠在上层；正文溢出内部滚动；增加“预览插画”按钮及可缩放拖动的纯图预览。保留已有工作树改动，不改变剧情文本、门槛、插画导入、GUID、经济或 v9 存档。

- `StoryChapterCard.prefab` 改为背景插画 → 黑色 55% 遮罩 → 固定标题/操作区与正文裁剪区。展开高度由 `StoryChapterLayout` 按宽度/16:9 提供，收起回到摘要高度；标题宽度不足时动作行移到下方。图片原素材不修改。
- 两字按钮：收起/展开维持 112×56，前往从 160×56 改为 112×56；预览插画 160×56，统一 28 字号和内边距。前往的现有条件保留。
- `StoryBodyScrollRect` 负责单一拖动所有者及边界交接；正文未溢出向外层转发，溢出先滚正文、到边界继续向外层滚。关闭惯性并 Clamped，拖动期间接入现有集中刷新抑制。
- `KingdomUIRoot.prefab/SafeAreaRoot/StoryIllustrationPreview` 为共享 authored 预览层。运行时只绑定既有节点，完整适配图片，滚轮/双指 1–4 倍缩放、拖动边界、复位、关闭/Esc/遮罩关闭，页面切换及 root 禁用清空图片并关闭。独立于研究图手势。
- `StoryChapterCard.Bind` 新增插画请求回调，卡片高度变化统一通知外层重排。更新现有 Story/Onboarding 测试路径并新增强类型配置、布局和交互回归；不使用反射。
- 作者入口 `StoryIllustrationAuthoring.AuthorBackgroundPresentation` 只创作卡片与预览，不重新导入插画、不重新写章节资产；原 AuthorStoryPresentation 同步采用新布局。

### 本轮验证

- Unity 2022.3.62f3c1 authoring 完成真实源码编译，日志 `Logs/story-background-authoring-20261011.log` 记录创作成功及 return code 0。
- 原有 StoryIllustrationPlayModeTests + KingdomOnboardingPlayModeTests：`TestResults/story-background-playmode-20261011.xml` 21/21，无失败跳过，Unity exit0，同名日志。
- 新交互测试初轮：`TestResults/story-background-interaction-20261011.xml` 3/3，无失败跳过，Unity exit0；覆盖 modal raycast、图片对应、zoom/pan/clamp/reset、关闭/遮罩/页面/root、滚动位置保留、内外滚动与交接、18章 × 3宽度全部正文可达及邻卡不重叠。
- 宽度为 Unity 卡片布局单位而非设备物理像素：600 宽 18/18 需内部滚动；1440、1920 宽均 18/18 无需内部滚动。测量日志前缀 `[StoryBackgroundLayout]`，保持原 28 字号及全部正文。
- EditMode `StoryIllustrationTests`：`TestResults/story-background-editmode-20261011.xml` 3/3 断言通过，无跳过；日志同名。XML 完成后进程在 native/network 退出清理停滞，最后日志 13:25:11，等待超过三分钟后只终止已核验命令行的本代理进程 24872，退出 -1。此轮不声称正常 exit0，不重复盲跑。

未跑全仓测试；双指真实硬件和实体 Esc 输入未自动合成验证，不把确定性缩放 API 覆盖说成设备触摸验收。外部运行由用户自行体验，不作为代理待完成门槛。截图与最终补充验证见后续本轮续记。
### 最终续记

- 真实 Canvas RenderTexture 渲染检查发现 Header 下的 Actions 无布局遍历桥接，首次截图中按钮有重叠；在布局所有者中明确重建 Actions，补充按钮间几何不重叠及文字宽度容纳断言。没有缩字号或弱化断言。
- 最终源码与资产再次真实 Unity 编译：`TestResults/story-background-final-playmode-20261011.xml` 25/25，无失败或跳过，Unity exit0；日志 `Logs/story-background-final-playmode-20261011.log`。覆盖原有插画、剧情 Onboarding 和新增交互/渲染用例；日志无 C# 编译错误、NullReference 或 MissingReference。
- 实际 Canvas（测试临时切换 ScreenSpaceCamera 并恢复，不是图片生成或效果图）渲染证据：`data/story-background-card-20261011.png`、`data/story-background-preview-20261011.png`。已人工查看：卡片背景图与文字层正确、操作按钮不重叠、完整纯图预览正确。截图场景的历史首章不显示“前往”，符合仅最新章节可导航的既有规则。
- 作者生成资产中的新增空字段尾空格已清理；根 Prefab 保留已有对象块内容与稳定 fileID，并恢复索引已有块顺序，减少 Unity 自动重排噪音。最终只做等价 YAML 顺序/空字段空格清理，未变动序列化值。
- 未创建临时 Unity 工程，测试隔离存档按 teardown 清理。未 commit/push、未操作真实存档、未终止用户 Editor。

交付完成；后续如调整遮罩透明度/排版，直接维护现有作者入口与 Prefab，不改原插画或剧情文本。本轮唯一验证限制仍是 EditMode 原生退出停滞，以及未合成真实双指硬件/Esc 输入；已正常完成真实编译和相关 PlayMode。
## 2026-10-11 补充：完整插画适配、生成按钮与统一上传

用户追加授权：核对并修复插画被截断、按钮稍下移、使用图片生成工具制作并套用按钮样式，全部待提交内容一次提交推送。

- 根因：背景 Image 本身 preserveAspect 且保持完整 Sprite；宽 16:9 卡片高于剧情页外层 viewport，图片底边被 RectMask2D 遮住。`StoryChapterLayout` 作为布局自控制器将展开宽度约束为 min(页宽, (viewport高-24)×16/9)，保持16:9并居中，收起仍通栏。添加真实 2640×1200 Canvas 下整图上下边缘在viewport内的回归断言。
- 卡片按钮下移14布局单位，header高度计入该偏移，窄屏动作换行仍不覆盖正文。
- 使用内置 image_gen 生成常态及高亮两张无文字透明 PNG：`Assets/Resources/Art/UI/Story/StoryButtonNormal.png`、`StoryButtonHighlight.png`。来源图不编辑，Unity Sprite矩形去掉透明外边距、9-slice保留铜色边框，常态/高亮/按下/选中状态绑定Sprite；剧情卡片及纯图预览按钮统一套用，其他页面不变。完整生成提示词在 `docs/story/story-button-art-generation.md`。
- 正常作者入口重新生成并绑定真实资源：`Logs/story-button-authoring-20261011.log`，Unity exit0，真实编译完成。
- 本轮 PlayMode：`TestResults/story-button-playmode-20261011.xml` 25/25，无失败/跳过，Unity exit0；同名Logs文件。包括完整边缘不被viewport截断、正文全量可达、按钮文字与间距、展开收起、导航、缩放/拖动/关闭与输入隔离。
- 实际 Canvas PNG 已更新并查看：`data/story-background-card-20261011.png` 中原图下边框完整可见，卡片两侧自然留边、纹理按钮下移生效；`data/story-background-preview-20261011.png` 中纯图完整，复位/关闭同样使用纹理按钮。
- 待上传包括本主题全部代码/Prefab/素材/测试/截图及既有场景、字体、规则、清理删除与审计报告；`tmp/cleanup-audit-20261010/` 是用户已有文字审计交付，不是测试fixture，保留并上传。既有删除与规则有原cleanup handoff授权记录。Git忽略的缓存、真实存档和本机配置不纳入。
### 本轮最终验证与提交范围

- 最新 EditMode `TestResults/story-button-editmode-20261011.xml` 3/3，无失败/跳过，Unity正常 exit0；日志 `Logs/story-button-editmode-20261011.log`。本轮没有退出停滞，也没有终止测试或用户进程；此前轮次的退出问题保留为历史记录，不声称专门修复了native退出问题。
- 当前修改均已纳入统一提交（81文件）：新生成素材及.meta、插画背景/完整预览功能、截图与生成提示词、既有场景与字体改动、AGENTS临时工程收尾规则、原cleanup交接与已授权删除、已有审计文字报告及客户端工作记录。Git忽略的Library/Temp/Logs/TestResults/配置/存档不上传。已fetch确认main与origin/main起点一致，无force/rebase/amend。
- `git diff --cached --check` 通过。按Unity作者输出只清理新增行尾空格，序列化字段值不变。统一提交与push结果/完整SHA由最终回复记录。
- 未运行全仓测试；真实硬件双指和实体Esc输入仍未验证。交付是仓库源码与素材，不包含外部安装包或设备体验跟踪。