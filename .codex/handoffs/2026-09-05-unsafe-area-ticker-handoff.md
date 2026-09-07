# UnsafeAreaTicker / UI 交接摘要

## 当前实现

- 新闻仅使用时代世界新闻池，不传入 `recentActionFeedbackEntries`，不会显示“+5人口”等游戏反馈。
- 每次显示一条新闻：字符反向竖排，字形旋转 90°（字顶朝左、字底朝右）。
- 新闻从 Viewport 顶部涌入，沿 Y 轴向下滚动并从底部离场；速度为 `28 × 1.8`，无淡入淡出。

## 已处理

- Era、Research、Ticker、SectorBuilding、SectorManager 的本轮相关过期断言/测试夹具已同步当前实现。
- SectorBuilding 测试包含 `AzurePool → DawnRing` 前置条件。
- 删除了逐字旧布局和脆弱几何断言。

## 验证边界

- UI contract 与 `git diff --check` 通过。
- 未执行真实 Unity 编译；当前音乐 Addressables `music/catalog` 错误与本任务无关，未处理。

## 下一步

- 刷新 Unity 后只根据新的、非音乐链式失败继续修改。