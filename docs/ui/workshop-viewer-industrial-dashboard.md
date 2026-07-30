# WorkshopViewer 工业仪表盘

设计基准：1200 × 2652 竖屏，顶部安全区 80 px，底部安全区 96 px。视觉使用深钢色底、铜色主操作、电力蓝、化学紫和物流橙。

## 交互约定

- 卡片点击只选中，不触发购买。
- 详情抽屉显示 Description、研究前置、Workshop 前置、资源成本、Effects 和使用建筑。
- 购买需要再次点击确认；购买和失败都通过 `WorkshopManager` 完成。
- 分类只控制卡片显示，不重建定义，也不改变滚动状态。
- 系统未解锁、研究未完成、Workshop 前置未购买和资源不足分别显示具体状态。

## 实现映射

`WorkshopViewer.cs` 使用现有字段 `Label`、`Description`、`Category`、`RequiredResearch`、`RequiredUpgrades`、`ResourceRequirements`、`Effects`。`CardPrefab` 和 `DetailPanelPrefab` 为可选扩展点；场景尚未配置专用 prefab 时使用同样的工业样式运行时骨架，避免空引用导致整页失效。

草图见 [workshop-viewer-industrial-dashboard.svg](workshop-viewer-industrial-dashboard.svg)。
