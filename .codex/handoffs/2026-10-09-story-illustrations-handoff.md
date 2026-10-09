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
