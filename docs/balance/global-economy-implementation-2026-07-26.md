# Kingdom 全局资源、反向前置与递归经济实施记录

日期：2026-07-26  
Unity：2022.3.62f2c1  
实施范围：原始时代至工业时代；太空内容冻结

## 已实现的运行时规则

- `Building` 持有 `RequiredResearch` 与 `RequiredWorkshopUpgrades`，两个列表均为 AND。
- `BuildingManager` 在买 1、买 10、Max、自动建造时统一验证时代、全部研究和全部工坊前置。
- `Research` 不再保存建筑解锁列表，`ResearchEffectType.UnlockBuilding` 与运行时已解锁建筑集合已删除。
- 工坊为独立永久升级系统，含定义、运行时状态、原子购买、存档恢复、效果重建和可滚动 Viewer。
- 工坊系统由 `IndustrialWorkshop` 的 `UnlockSystem("industrial-workshop")` 效果开放。
- 建筑锁定提示分别显示缺失时代、研究与工坊；工坊项目显示其关联建筑。
- 递归生产按 Tick 汇总共享供需。每种输入得到同一个满足率，多输入满足率逐项相乘；产出和消耗使用同一最终效率。
- Tick 库存可补足缺口；实际同 Tick 上游产出可进入下游满足率；潜在产量不会穿透。
- 生产配方循环与建筑/研究/工坊综合可达性在启动时验证。
- 新存档人口为 0，因此人口食物消耗为 0；人口出现后才按每人口食物率消费。
- 旧存档中的 `StoneTool`、`MetalTool`、`StoneToolWorkshop`、`Blacksmith` 被忽略，四项共用一次迁移日志。

## 内容目标

- 项目 Resource 资产：52；运行时早期/工业白名单：30；未发布太空资源另行冻结。
- Building：43，其中原始至工业 40、太空 3。
- Research：61。
- WorkshopUpgradeDefinition：18。
- 删除两种工具库存资源与两座工具建筑；`StoneTools`、`Mining`、`ControlledFire`、`Smithing` 改为可度量倍率。
- 新增 11 种工业递归资源、6 座工业建筑，并重配全部 40 座原始至工业建筑。

## 为消除硬循环所做的必要偏差

原计划中的三项首座费用无法与“新存档零注入可完成工业阶段”同时成立：

| 建筑 | 原计划循环 | 实施费用 |
|---|---|---|
| OilDerrick | 费用含 Machinery，但 Machinery 依赖 CrudeOil | Steel 500、Bronze 250 |
| SilicaQuarry | 费用含 Machinery，但 Machinery 依赖其 Silica 链 | Steel 350、Bronze 200 |
| OilRefinery | 费用含 Machinery，但 Machinery 前置需要其 Lubricant | Steel 850、Bronze 250、Copper 200、Chemical 100 |

`PoweredMining` 与 `RotaryKilns` 保留为永久生产增益，但不再作为
`SilicaQuarry`、`Glassworks` 的首座硬前置。否则两项升级的 Machinery
费用会把基础硅链锁在 MachineFactory 之后。

## 迁移与验收状态

一次性 Editor 迁移实现于
`Assets/Editor/Balance/GlobalEconomyMigration.cs`。它负责反向写入前置、
重配数值、创建新定义、删除退役定义、保 GUID 移动 Deferred 资源、
创建工坊 Viewer、重序列化资产并导出 CSV。

当前 C# 运行时、Editor 脚本与全部 Editor 测试源码已通过本地
`dotnet build Kingdom.sln --no-restore` 编译（0 error）。Unity 批处理
权限审批通道在启动前断开，因此迁移尚未实际执行，资产数量、EditMode、
PlayMode、Console 与 CSV 仍不能标记为通过。

未执行真实 Unity 编译。  
未执行 Huawei P40 Pro 真机验收。
