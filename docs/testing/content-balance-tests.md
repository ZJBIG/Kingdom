# 内容与数值验收

## 定义验证

- 稳定 ID 非空且唯一
- 所有 Pair 的 Resource 非空
- 同一列表无重复 Resource
- 所有金额非负
- Building costGrowth > 1
- 时代跃迁研究标记正确
- 资源有来源和用途

## 可达性

从新游戏开始静态推演：

- 原始主线可完成
- NeolithicSettlement 可完成
- 新石器资源链可建立
- FeudalAdministration 可完成
- Medieval 可进入

失败时打印完整阻断路径。

## 数值模拟

至少模拟：

- 10分钟
- 1小时
- 4小时
- 24小时
- 30 FPS / 60 FPS
- 开启/关闭自动建造
- 粮食短缺
- 多建筑竞争资源

## PlayMode

当前 PlayMode 0 用例不算通过。

必须新增：

- 新游戏启动
- 前10分钟 smoke
- 页面关闭后模拟继续
- 保存/加载
- 后台/恢复
- P40 Pro 横屏与安全区
