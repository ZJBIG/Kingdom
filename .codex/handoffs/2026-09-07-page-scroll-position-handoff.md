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
- 未执行真实 Unity 编译、PlayMode、Console 和 Huawei P40 Pro 实机验证。

## 遗留风险与下一步

- 需要在 Unity 中运行新增剧情位置恢复测试及现有研究/音乐/页面滚动测试。
- 需要确认真实运行时动态重建后研究图和音乐列表的位置均按内容边界正确限制。
- 工作树中存在本轮之前的用户改动，未清理或覆盖。
