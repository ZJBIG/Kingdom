# Kingdom 内容与测试交接报告

更新时间：2026-08-21

## 当前目标

完成研究、工坊、建筑、资源链和时代跃迁整理，并让 Unity EditMode、TestMode、PlayMode 测试回归稳定。研究内容遵循：飞船与深空内容位于 Spacer 前期；永生、意识升格、超凡内容不得提前加入，必须位于 `TechnologicalSingularity` 之后并经过至少两个 Ultra 前置。

## 已完成的主要内容

- 新增 `TechnologicalSingularity` 作为 Spacer → Ultra 独立跃迁。
- 调整 Spacer 前期研究顺序、研究点、资源需求和前置关系。
- 删除重复的深空教育、跨星区计量、星际结算和战役策略研究/工坊资产。
- 修正 Quantum、Matter Transmutation、Causal Synchronization、DeepSpaceSurvey 等 Label/Description。
- 批量调整 Spacer 研究和工坊资源需求、研究点、效果目标和前置。
- 修正多个 Spacer 建筑的生产力、物流、研究力和高级材料职责。
- 修正研究状态初始化、资源状态懒初始化、研究支付和 Restore 四参数兼容。
- 增加自动测试错误报告：每次 Unity Test Runner 运行后覆盖写入：
  `Kingdom/TestResults/Latest-Test-Errors.txt`
- 批量更新旧稳定 ID、迁移断言、P40 UI 断言、Sector 拓扑断言和 Spacer 旧断言。
- 工业闭包中发现并移除 `ContinuousDistillation` 对 Machinery 的循环依赖。

## 最近测试基线

自动报告最近一次记录：

- Total：12
- Passed：8
- Failed：4

这 4 个失败已在最新工作区批量修正，尚未由本轮重新运行确认：

1. DetailPanel 分帧建树等待时间过短。
2. ResearchTree 仍断言已经移除的 Search 节点。
3. NeolithicSettlement 仍要求立即存在可建造建筑。
4. NewGame 木材状态受到跨测试 ResourceManager 污染。

## 已确认不能删除的断言

- `MechanizedProduction` 不存在：这是删除/合并后的迁移契约。
- `StoneCuttingWorkshop_Marble → StoneCuttingWorkshop`：这是存档迁移契约。
- 当前 Sector 的 Tau Ceti、Sirius、Alpha 拓扑断言已经与资产同步。

## 下一步执行顺序

1. 运行 EditMode、TestMode、PlayMode，覆盖 `Latest-Test-Errors.txt`。
2. 若失败少于 4 个，只处理新报告中的真实失败，不回滚已确认的迁移断言。
3. 若出现编译错误，优先修复编译错误，再看测试失败。
4. 对仍然失败的测试先判断“过时断言 / 测试夹具污染 / 真实资产问题”，不要直接修改运行时去迎合旧期望。
5. 最后执行 Unity Console 检查和静态闭包报告。

## 工作边界

- 不运行或扩展离线模拟器，用户明确要求不依据模拟器。
- 不删除稳定 ID、GUID 或 `.meta` 文件。
- 不为修测试而恢复已删除的语义重复研究/工坊。
- 不把永生、意识升格、超凡节点放到 Spacer 前期。
- 普通资源不增加容量上限；Food 是唯一允许有库存上限的资源。

## 验证声明

- 尚未由本次交接报告执行真实 Unity 编译。
- 尚未执行 Huawei P40 Pro 真机验收。
- 需要下一位智能体重新运行 Unity 测试确认最近 4 个 PlayMode 修复。

