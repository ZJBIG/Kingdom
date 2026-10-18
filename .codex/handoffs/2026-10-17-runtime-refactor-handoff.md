# 实际运行代码重构交接（2026-10-17）

## 授权与边界
用户明确要求转向游戏运行代码重构精简，非继续删断言；保留先前“不做PlayMode”，不调经济、不变存档schema、不自动commit/push。入口与Runtime scope已读，原测试精简在supply-happiness-handoff的round3收尾。

## 当前成果
8个runtime文件净删55行：ResearchManager队列索引提交段合并；WorkshopManager研究/工坊前置检查合并；SimulationManager离线余量复用；CampaignState/GameState删无调用4参exact重载；ResearchState恢复去无效bool并复用CostPaid；MilitaryState复用相同初始化；UltraProjectManager删无调用进度方法。保留原有canonical定义、付款账本/事务回滚、事件顺序、计数版本、5参doctrine恢复与资源预算。字段前后值：无资产/经济/存档字段值变化。

## 验证
开发测试程序集exit0/0errors/6既有警告；静态闭包exit0；模型诊断23/23（不覆盖本轮Manager/State，不称parity）。真实Unity首批226项225通过1旧夹具失败；因OilDerrick无耗材，把工坊预览首fixture换CokeOven，保留全部断言，整类3/3通过，其他223项沿用首批通过结果。Unity每批XML落盘后退出挂起，60秒清理自启动PID，runner exit1；未PlayMode/全套，无性能测量。git diff --check通过。

## 证据与下一动作
详见data/runtime-refactor-20261017/review.md、source-inputs.json、两批XML/log；baseline核对包含此前6份未提交修复，pre-existing-changes.patch已保存，不能把git diff HEAD全当此轮修改。未动真实存档/用户Editor。继续重构时可看ResearchManager.ValidateRestoreInput重复ledger查询；建筑预算与JSON解析边界不同，需完整语义核查后再改。

## 第二轮（2026-10-18）
生产4文件：BuildingManager活跃索引、纠偏固定需求局部缓存及资源索引循环；UltraProjectManager共用成本聚合，预算复用缓冲并绕开展示预览；WorkshopManager预览补世界事件倍率；ResearchManager复用付款账本判断。无资产/经济/存档字段值变化。新增5项针对性用例，Surge使用已完成第一阶段夹具，Expedition使用最终Committed夹具，保留状态校验。最终真实Unity编译与EditMode 233/233（1.9796196秒），开发程序集编译0错误/6既有警告；Unity XML落盘后退出挂起，60秒清理自启动PID，runner exit1；未PlayMode/全套，无性能实测。证据data/runtime-refactor-20261017/round2/review.md、source-inputs.json、editmode-verified.xml/log、runner-verified-status.txt。用户允许酌情新增内容，本轮未增加。下一项可审查工坊详情候选增益的重复重建，缓存前验证失效条件；持续目标保持active，不以本批测试证明全项目无错。

## 第三轮（2026-10-18）：工坊预览
WorkshopManager单候选增益缓存净增10行；Current引用或候选定义变化时失效，所有动态速率每次读取。2个新增EditMode方法覆盖动态数量/效率/事件、候选切换、研究重建、已购/恢复及独立分配检查。最终真实Unity编译+235/235（1.9924023秒）；GC.Alloc固定无建筑全研究夹具采样112 vs1225字节/次，约91%降低，仅此调用证据。临时旁路缓存后关系门槛测试按预期失败，已恢复最终源码并复验；.NET计数为0及初版弱门槛的中间结果不当最终证据。Unity XML后退出挂起，60秒清理自启动PID/runner1；未PlayMode/全套。详见round3/review.md、verified.xml/log与source-inputs.json。未资产/存档字段变更，无commit/push。持续目标active。

## 第四轮（2026-10-18）：通知异常与离线研究
已复现并修复外部研究监听器中断tick、留下已提交经济与未扣积压的路径；ObserverNotifications强类型helper复用Resource既有隔离语义，接入Research18处及Building/Workshop提交后通知，异常记录后继续订阅者，玩法操作异常仍传播。另修TickOffline商转double再乘回导致完成线停滞、丢余量：整窗口判断完成，耗时只用于扣余量；下一项速度/付款重算。研究恢复复用效率解析，删无效completed.Remove。新增8例，旧代码before.xml 5/5失败（4通知/1完成停滞），最终真实Unity编译及EditMode 266/266，2.085824秒；开发编译0错误/6既有警告；独立复核与diff检查通过。Unity结果后退出挂起，60秒清理自启动PID/runner1，未PlayMode/全套，无本轮性能量化。详情round4/review.md、verified.xml/log、source-inputs.json。无资产/存档字段数值变化，无commit/push，持续目标active。


## 第五轮（2026-10-18）：人口恢复与零秒推进
PopulationState恢复保留满容量饥荒进度；exact回滚另保留合法端点1，其余归一化规则不变；AdvancePopulation校验全部参数后零秒返回，避免派生刷新清进度或消费待结算人口。正时间模式切换/正常满员停止增长及v1字段不变，运行代码净删3行，无资产/经济字段变化。新增4方法共7例，旧恢复3/3失败、仅修恢复时零秒3/3失败、端点1旧exact 1/1失败。完整244批先有1项旧测试跨真实时钟整秒失败（日志记录额外1秒离线）；仅将该测试第二次调用改为已有显式时间API，以同一保存时间验证不重复结算，保留全部原断言。最终真实Unity编译及EditMode 244/244，2.1331908秒，Unity正常退出/runner0，无剩余Unity进程；开发程序集编译0错误/6既有警告。前序中间批XML后退出超时/runner1不作最终退出证据。证据data/runtime-refactor-20261017/round5/review.md、accepted.xml/log、accepted-status.txt、source-inputs.json、round5-only.patch。未PlayMode/全套/性能实测/commit/push。持续目标active，下一项继续人口/粮食结算边界，须先具体复现。

## 第六轮（2026-10-18）：在线/离线人口粮食结算核心
修复整tick按末态库存判饥荒：起始库存覆盖时间与断粮时间分段推进人口，粮食仍整段扣一次、日历仍按calSeconds推进一次，现有窗口幸福度/建筑预算不变。4直接例旧代码2失败2对照通过，在线/离线集成2/2失败；真实Unity编译及EditMode 248/248，2.5507196秒，正常退出runner0。开发编译0错误/6既有警告，无PlayMode/全套/性能实测/资产或schema改变。证据round6/review.md、verified.xml/log、baseline、source-inputs与隔离patch。用户已更新目标要求优先大块内容；下一块生产预算纠偏pipeline：按每轮准备受影响资源固定输入、消除候选全表扫描，以及删除随即被重算覆盖的资源增量写入。持续目标active，无commit/push。

## 第七轮（2026-10-18）：生产预算纠偏整段优化
按更新目标优先大块运行逻辑：BuildingManager每correction一次准备受影响资源固定输入，low与候选检查不再逐次全表筛选/查询/重算external；拆共享game侧更新，纠偏提交删除立即被reset/rebuild覆盖的普通资源delta写入，保留全active正向重算、量化顺序及food/capacity/military。无资产/经济/schema变化，生产代码净增32行以减少重复运行工作。新增2方法5例，含两独立循环/256idle/库存0→充足→0刷新与四种性能场景。初版idle在后基准差异不足判收益，补idle在前而保留原场景；同输入前后纠偏中位耗时0.370550→0.327556、1.040425→0.911069、1.221031→0.952788 ms，约12-22%降低；无纠偏0.315756→0.318631基本持平，仅合成夹具调用耗时，不推FPS/整局。最终真实Unity编译/EditMode 272/272，2.5513403秒，正常退出runner0；开发编译0错误/6既有警告，独立复核及diff检查通过。旧版ordered-before断言15/15但退出超时runner1，最终不超时。证据round7/review.md、ordered-before/ordered-verified.xml/log、benchmark-comparison.json、baseline/source-inputs与隔离patch。旧版采样期间均核对原字节，优化最终源码sha C0BA2CB947A2BC939E30C2F8F14E90F5417A847671844B7C0225B609A737E977。无PlayMode/全套/真实存档/commit/push，持续目标active，后续继续成块生产/进度路径，先证据后改动。
