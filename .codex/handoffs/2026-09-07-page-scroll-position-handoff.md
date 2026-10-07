# 页面滚动位置记忆交接报告

## 本轮变更

- 普通页面按页面名称缓存 `PageHost` 外层 `ScrollRect.verticalNormalizedPosition`，切换页面后恢复；首次进入默认顶部。
- 剧情页沿用同一套通用恢复流程，并保留剧情动态内容刷新时的原位置。
- 研究页缓存研究图平移位置和缩放比例，通过 `UIResearchGraphGesture.RestoreView` 恢复并重新限制边界。
- 音乐页缓存曲目列表内层 `ScrollRect` 位置；保留固定控制区、播放状态、音量和间隔行为。
- 位置仅存在当前 `KingdomUIRoot` 生命周期，不写入存档或 `PlayerPrefs`。
- 新增 `Assets/Tests/PlayMode/PageScrollPositionPlayModeTests.cs`，覆盖剧情页离开后再次进入的位置恢复。

## 验证

- `dotnet build Kingdom.Runtime.Developer.csproj --no-restore --nologo --verbosity quiet`：通过，0 错误；保留 3 个既有警告。
- `dotnet build Kingdom.Editor.Developer.csproj --no-restore --nologo --verbosity quiet`：通过，0 错误。
- 未执行真实 Unity 编译、PlayMode、Console 和运行期验证。

## 遗留风险与下一步

- 需要在 Unity 中运行新增剧情位置恢复测试及现有研究/音乐/页面滚动测试。
- 需要确认真实运行时动态重建后研究图和音乐列表的位置均按内容边界正确限制。
- 工作树中存在本轮之前的用户改动，未清理或覆盖。

## 2026-10-07 B 交互回归补充

- `Assets/Tests/PlayMode/KingdomOnboardingPlayModeTests.cs`：`OuterPageScroll_DirectDragReportsMeasuredBounds` 现在要求 Overview 运行时存在正溢出，直接以 authored `PageHost` 背景作为拖拽面；验证拖拽位置变化、上下边界钳制、短点击不位移及切到 Research 再返回的位置恢复。移除无溢出时的 `Assert.Ignore`，无真实溢出会使回归失败。
- `Assets/Tests/PlayMode/KingdomPlayModeTests.cs`：新增 `UltraLockedOverview_NavigatesToPhaseEnergyArrayDetailAndRefreshesConditions`，以 Ultra + 文明工程 Locked 隔离状态经 Overview authored 导航打开 `PhaseEnergyArray` 详情，检查目标、阻碍、下一步和 Locked→Ready 刷新。
- 本轮未修改生产 UI。尚未执行真实 Unity 编译/PlayMode：当前工作树编译被既有错误阻塞（`ResearchManager.cs` `suppressCompletionAudio` 未定义；`MusicManager.cs` `OnResearchCompleted` 委托签名不匹配）。
- 下一动作：修复/解除上述工作树编译阻塞后，运行两条新增 PlayMode 与 Console 检查；若 Overview 实测无溢出，需回到 authored 长内容布局或测试隔离视口，而不是跳过测试。

## 2026-10-07 B 真实回归收口

- 首轮真实 PlayMode 已复现两项缺口：Overview 在 640×480 批处理窗口无 overflow；Ultra Locked 详情阻碍分支没有“下一步”文案。
- 修复：`KingdomOnboardingPlayModeTests` 仅在测试内缩小 `PageHost` viewport，保持 authored 内容与生产页面不变；`KingdomUIRoot.UltraProject.cs` 在 Locked/Ready 阻碍分支追加“下一步: 先排除阻碍，再启动本阶段”。
- 修复后真实命令：`run-unity-tests.ps1 -Platform PlayMode -ResultsPath TestResults/codex-B-playmode-20261007-r3.xml -LogPath Logs/codex-B-playmode-20261007-r3.log`；结果 37/37 passed、0 failed、0 skipped、退出码 0。B1 实测 overflow=794，拖拽 396.9999→794，边界/短点击/Research 返回 normalized=0.42；B2 目标、阻碍、下一步与条件刷新通过。
- Console：日志无游戏 Error/Exception/MissingReference；仅 Licensing access-token/Curl cleanup 环境噪声。
