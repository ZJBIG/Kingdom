# 通知异常与离线研究完成边界（2026-10-18）

## 可复现问题
1. ResearchManager完成通知抛异常时，SimulationManager已经推进资源/日历，却还没有扣除待处理时间；异常传播会中断事件时间推进，后续Advance(0)存在重复结算路径。BuildingStateAdded/UpgradeStateChanged同样会令已提交操作对调用方表现为失败，并阻断后续观察者。
2. ResearchManager.TickOffline将剩余研究量/速度转double，再Tick该小时间片；商乘回的舍入可使进度仍低于BaseCost，然后提前return，丢掉剩余窗口。已付款Quarry进度BaseCost-.01，离线1秒仍Researching，before.xml有真实证据。

## 修复
- 新增强类型ObserverNotifications(Action与Action<T>)，逐监听器捕获并Debug.LogException，继续后续监听器；只隔离外部通知，付款、回滚、派生重建和玩法异常不在捕获范围。Resource原有相同私有Publish迁入共享实现，Research的18处事件及Building/Workshop已提交通知复用；通知条件与顺序保持。
- TickOffline使用现有AdvanceResearchProgress判断完整可用窗口能否完成。不足则使用全余量推进一次；足够则直接完成当前研究，仅将商转double用于扣除耗时，下一循环重新计算后续研究速度/付款。无epsilon，无新数值阈值。
- 研究恢复复用预校验已解析的GlobalEfficiencyFactor，仍在原提交位置赋值；删除从空集合且已拒绝重复输入条件下不可能生效的completed.Remove分支。全部非法输入校验保留。
- 只新增辅助C#及Unity生成的meta（GUID e3d1b08f836e0fb49b3d493f1f7e2a77）；未改既有GUID/资产/经济数值/存档字段，不操作真实存档、不commit/push。

## 验证
- before.xml：旧通知/旧离线逻辑下5/5失败，4项为观察者异常传播（Building/Workshop/在线队列/在线完成），1项离线研究尚未完成；该离线项明确不是通知异常复现。
- verified.xml：最终真实Unity编译与EditMode 266/266，2.085824秒。包括此前8类、ResearchPaymentSaveTests、SimulationDeterminismTests、OfflineSimulationBoundaryTests。
- 新增8参数化用例：3通知模拟场景、建筑注册、工坊购买，以及离线完成线附近/时间不足/已达完成线3边界。验证后续监听器继续、支付只扣一次、Advance(0)不重复生产、在线事件时间同步、下一研究仅获得剩余时间。
- dotnet build Kingdom.DeveloperTests.csproj --no-restore：compile-final.log，0错误、6既有TextureImporter警告。git diff --check通过。独立只读复核确认零时间/零速度/未付款队列/速度变化和finally恢复，未发现新增确定问题。
- Unity在结果落盘后退出挂起，包装60秒清理自启动PID，runner exit1，见verified-status.txt。未运行PlayMode或全套，未作本轮性能量化。

## 续接
本轮前已有补丁保存在pre-existing-changes.patch，baseline.json与source-inputs.json区分本轮；ResearchManager.before.cs.txt保留反证输入，ResearchManager.final.cs.txt仅为通知隔离阶段快照，最终还包含离线边界修复，以活动源码指纹为准。持续目标保持active；下一轮继续实际运行逻辑与成本审查。
