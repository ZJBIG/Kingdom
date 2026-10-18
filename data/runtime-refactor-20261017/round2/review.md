# Runtime 性能与逻辑修复第二轮（2026-10-18）

## 本轮范围
以 baseline.json 与 pre-existing-changes.patch 区分此前未提交代码。4个生产文件、2个测试文件；生产代码净增34行。本轮不改定义/经济数值/存档字段/meta/GUID，不执行 PlayMode，不自动提交或推送。

## 改动
- BuildingManager：RefreshEfficienciesCore 复用已维护的活跃建筑索引；保留非零建筑顺序与 Manager 懒获取。每次预算纠偏只捕获一次固定全负载电力/物流需求；候选仍重算动态产出、幸福度和资源满足率。资源列表接口 foreach 改索引循环，保持数值顺序、资源字典和预算集合遍历顺序。
- UltraProjectManager/BuildingManager：预算读取不再构建完整 UltraProjectPreview 或执行展示用校验。复用聚合列表/索引；共用原聚合实现，仍先合并重复资源再乘方针倍率。战略粮食需求也直接读取。Running/ResearchManager 条件保持，暂停/完成不增加需求。
- WorkshopManager：实际工坊购买预览传入当前世界事件倍率；只缩放建筑资源产出及建筑粮食产出，耗材和基础粮食不受事件倍率影响。既有静态重载继续默认倍率1。
- ResearchManager：恢复预校验的相同付款账本判断只计算一次，所有拒绝条件保留。

## 验证
- dotnet build Kingdom.DeveloperTests.csproj --no-restore：compile-tests-final.log，0错误、6项既有 TextureImporter 警告。之后仅修正新增测试夹具，最终源码由真实 Unity 重新编译并运行。
- 最终真实 Unity EditMode：editmode-verified.xml，233/233，1.9796196秒。范围 FoodEfficiencyTests、FlowEfficiencyTests、SimulationBudgetTests、EconomyIntegrationRegressionTests、WorkshopBenefitPreviewTests、ResearchPaymentAutoTests、KingdomLogicTests、UltraProjectStateTests。
- 中间 editmode-final.xml 为231/233：新增Surge/Expedition夹具违反已有合法状态校验，已改为Surge第二阶段、Expedition最终Committed；未弱化校验。早期 editmode.xml 的213/213不能代表后来Ultra改动。
- 新增工坊事件两例与Ultra三例；Ultra测试覆盖Stable/Surge预览成本一致、重复pass不累加、暂停释放需求，以及Committed Expedition无工程负载。
- git diff --check通过。独立只读复核确认固定需求缓存/活跃索引顺序/聚合运算顺序/缓冲所有权；无确定新问题。
- Unity测试结果已落盘；原生退出仍挂起，包装60秒后只终止本轮启动PID，runner退出1，详见runner-verified-status.txt。未执行PlayMode或全套测试，无FPS/内存分配实测；不声称全项目无bug。

## 后续
继续实际运行代码审查；工坊详情每250ms仍重放候选增益的缓存候选尚未实施，需确认所有有效失效边界并验证动态速率刷新。用户允许酌情扩展内容，但本轮未新增玩法。
