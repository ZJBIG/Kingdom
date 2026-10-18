# 持续缺陷修复（2026-10-16）

## 授权与边界
- 用户要求持续抽查多个方向并修复确定 bug；本轮选取保存恢复、建筑/工坊交易、研究队列、人口方向切换。
- 暂不运行 PlayMode；不操作真实存档、不调整经济资产、稳定 ID 或 GUID。本轮新增修改尚未提交或上传。
- 上轮全工作区上传已完成：main `76913109bcf4b034929b15aa43b1cd04e044ff45`；不要把它当成本轮上传授权。

## 本轮行为变更
- GameState.ResetDerivedEconomy 从基础食物容量重建；SaveManager 在建筑、研究、工坊和星区状态恢复后按最终容量恢复合法食物库存，避免存量再次被计入容量和中间步骤丢粮。
- BuildingManager 的建造/拆除/升级及预览统一使用 State/数据库中的正式定义，防止同 ID 其他实例改变费用、前置或类型。
- BuildingManager 在建造、拆除、升级前要求两端数量实际增减；大数精度导致数量完全无变化时拒绝，避免扣费或无限退款。未改变大数舍入策略。
- WorkshopManager 购买和前置查询使用正式状态定义。
- ResearchManager.StartResearch 成功后移出原等待队列再发通知，避免 active 与 queued 重复导致自身存档无法恢复。
- PopulationState 跟踪非持久化的推进方向，出生/缺粮迁出切换时清除原方向进度；新建/恢复人口时清除方向标记，首次正时间推进采用实际模式并保留已有进度，零秒评估不决定或切换方向，维持现有 v1 字段。

## 复现及验证（输出位于 data/bugfix-rounds-20261016）
- round1-before.xml：22 项，15 通过、7 失败；证明粮仓容量膨胀、建筑同 ID 费用不一致、大数建造/拆除数量不变。此处工坊两项因 fixture 未建立状态索引而无效，不能算工坊复现。
- workshop-before.xml：2 项失败，仍是 fixture 前置失败，保留原始证据。
- workshop-fixture-before.log：一次 Unity 许可证启动失败，无 XML。
- workshop-ready-before.xml：fixture 调用 ResetForLoadForEditor 后，2 项均因本应拒绝却购买成功而失败，正式复现工坊漏洞。
- round1-after-research-before.xml：68 项，67 通过、1 失败；上述修复通过，唯一失败为新增研究队列回归；随后已修复研究生产代码，待最终验证。
- 以上有结果的 Unity 运行均写出 XML 后未在 60 秒内正常结束；仅结束本代理启动的 PID，wrapper 退出 1，不能报告正常退出。
- research-after-population-before.xml：6 项，4 通过、2 失败；研究队列已修复。此处人口两项先在过窄的小数前置容差处失败，不能算行为复现，随后已改成接近 1 的区间条件。
- population-before.xml：5 项，1 通过、4 失败；真实行为断言证明人口从 10 错变为 9/11，另外两项证明恢复后方向切换继续串用进度；既有人口推进用例通过。
- final-editmode.xml：81/81，2.2945299 秒，零失败/跳过。后续独立审查发现模式跟踪会在零秒评估时消耗恢复进度，已限制为正时间推进并补两项回归；最终结果改看 final-current-editmode.xml。
- final-current-editmode.xml：83/83，2.7293146 秒，零失败/跳过；日志无 error CS、NullReferenceException 或 MissingReferenceException。覆盖本轮交易、保存、研究、人口及现有食物/流量/经济集成路径，真实 Unity 编译完成。写出结果后退出超时，仅结束自建 PID 40548，wrapper 退出 1。
- content-closure.md：静态闭包通过，Industrial 闭合；offline-diagnostics.json：离线确定性诊断 23/23，passed=true。已去掉 stdout 前部构建警告以保持有效 JSON。不等同 Unity parity/玩法节奏验收。
- 当前 git diff --check 通过；有真实 Unity 编译及定向 EditMode 证据，未执行 PlayMode、全库测试或视觉验收。

## 继续动作
- 本轮确定缺陷及零秒复核问题已修复，最终83项定向用例通过。source-inputs.json 记录最终10个源码/测试输入的 SHA-256；后续改动需重新选择对应验证，不把本次结果作为新输入的证明。
- 下一轮可抽查教程/剧情推进、事件结算和资源交易取消路径；只修有具体输入与证据的问题。继续遵守用户暂不运行 PlayMode 的要求，不自动提交或上传。
- 主代理已逐项复核 production diff，另一次独立只读审查未发现本轮保存/交易/研究修复引入的具体回归。
- UltraProject 支付/暂停/阶段状态和 Relic 原子性只读抽查未发现新的确定缺陷；离线结算亦未发现可证实的新问题，不据此宣称模块无缺陷。
