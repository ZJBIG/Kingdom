# UnsafeAreaTicker 交接摘要

## 本轮

- 修正 `BuildingControls/ShowDetails` 状态映射：`isOn` 与内部详情状态反向对应，保留 Toggle 原有颜色过渡。

- 新闻仅使用世界新闻池，不传入 `recentActionFeedbackEntries`。
- 新闻单条反向竖排，字形旋转 90°（字顶朝左、字底朝右）。
- 从 Viewport 顶部涌入，沿 Y 轴向下滚动到底部离场；速度 `28 × 1.8`，无淡入淡出。
- Era、Research、Ticker、SectorBuilding、SectorManager 的本轮相关断言/夹具已同步当前实现。
- UI contract 与 diff check 已通过；音乐 Addressables `music/catalog` 错误不在范围内，未执行真实 Unity 编译。
- 下一步：刷新 Unity 后只根据新的非音乐失败继续修改。
