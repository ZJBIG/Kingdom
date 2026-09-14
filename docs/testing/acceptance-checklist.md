# 当前验收清单

## 静态与编译

- 稳定 ID、Definition/State/Manager/UI 边界保持一致。
- 新增或移动的 `.cs` 同步进入所有适用的 `.csproj`。
- 无 C# 编译错误、缺失脚本引用或旧数值类型残留。
- 普通资源无容量上限；Food 是唯一有库存容量语义的资源。

## 定义与可达性

- 稳定ID非空、同类型唯一；资源金额定义及其运行时Pair的Resource非空，同一列表无重复Resource，金额非负，Building costGrowth > 1。
- 时代跃迁研究标记正确；资源来源、用途及新增内容质量按经济技能的 `references/content-design.md` 验证。
- 从真实新档检查当前路线图的Animal → StoneAge → MiddleAge → Industrial主线，失败时打印完整阻断路径；不能以预置时代状态冒充新档可达。
- StoneAgeSettlement、石器资源链和FeudalAdministration是早期检查点，不是完整主线验收的终点。
- 静态可达不证明生产、消费或节奏正确；定义变更前后检查顺序按经济技能执行。

## 数值断言

遵守根 `AGENTS.md` 的数值断言规则：默认关系、范围或容差；精确比较必须有语义理由。独立合成输入不构成计算结果精确等于的额外豁免。

## 交易与存档

- 研究队首自动原子支付，资源不足不部分扣款。
- 建造、拆除、研究和工坊购买失败时不产生部分状态变更。
- 星区建筑不读取、提交或返还 `spaceCost`；数量使用既有 `BuildingState.Amount` 保存。
- 存档恢复后再校验星区建筑与占领状态的一致性。

## 页面与生命周期

按 `docs/testing/playmode-test-plan.md` 验证新档、页面/详情、星区、剧情、保存/后台恢复和P40横屏场景。页面操作契约在 `docs/ui/page-responsibilities.md`、布局/手势门槛在UI技能维护，State与生命周期边界在 `docs/architecture/ui-boundaries.md` 维护；验收不能把UI变成状态权威。

## 证据边界

报告必须区分静态检查、CLI 编译、Unity Test Runner 日志和真机验收。未有对应日志时不得声称通过。
