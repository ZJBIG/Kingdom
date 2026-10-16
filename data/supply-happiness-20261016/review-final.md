# 后续代码审查与测试精简

用户后续授权：继续审查并改代码，暂不运行 PlayMode；减少测试体量，取消不必要的精确性断言。本次不再把 PlayMode 复跑作为继续代码工作的前提。

## 修复

- BuildingManager 的最终预算检查不再 BeginTick 清空完整需求和满足率。ResourceState/ResourceManager 新增仅刷新本 tick 生产供给的方法，保留满负荷需求和 Ultra/Relic 持续成本；更新占领星区供给后重新计算最终资源满足率。回归覆盖“库存可支持部分生产，但缓存被清掉后错误返回零满足率”。
- 食物满足率以实际建筑效率对应的粮产计算，缺料停产的农场不再提供虚构满产供给。预算候选和最终收尾均根据对应粮产重算食物满足率、幸福奖励，避免只改主循环而留下预算口径不一致。基础粮产和全局粮食倍率仍只计一次，完整潜在耗粮需求保留。

## 删减

- 两次长窗口离线集成合并为一个 60 秒离线集成用例。原执行时间合计 209.727176 秒，新用例本次执行 0.721671 秒。独立 OfflineBoundary 的 2 小时和 8 小时边界用例保留；不再声称新短用例覆盖长时间全系统组合状态。
- FoodEfficiencyTests 和 FlowEfficiencyTests 合计从 22 项减至 17 项，删除重复计算/反射结构测试，移除计算小数的 Within/Is.EqualTo 断言，改用行为、范围和关系；仅保留离散 Version 精确比较。临时 Building 资产补充 try/finally 清理。
- 亏损循环库存参数从 5 组减至 3 组，保留零、极小、充足库存；删除小数速率比值校验和重复非负断言。收支预算、库存非负、独立链不受影响等检查保留。
- 短集成首跑暴露旧夹具问题：研究未自然完成时直接 CompleteAllResearch 会留下完成状态与活动队列冲突。现先通过公开 RemoveQueuedResearch 清理该测试研究，再进行原有存档阶段的人工完成设置；未修改运行时研究规则。

## 验证

- `dotnet build Kingdom.DeveloperTests.csproj --no-restore` exit 0：compile-review.txt。此编译在后续测试精简完成前；最终 Unity 测试执行使用最终测试源码，覆盖最终编译。
- 首次 EditMode：edit-review.xml，40 项中 39 通过，1 个短集成夹具失败；其中 Flow 文件采用精简前版本，不作最终输入证据。修复上述夹具后复跑。
- 最终 EditMode：edit-review-final.xml，37/37 通过，0 失败、0 跳过，测试执行 1.5537473 秒。范围：FoodEfficiencyTests 10、FlowEfficiencyTests 7、EconomyIntegrationRegressionTests 19、UltraContinuousMaterialDemandParticipatesInSameTickSatisfaction 1。
- 最终日志 edit-review-final.log 没有脚本编译或测试异常；原生退出依旧停在 Shutdown/Licensing disconnect/Curl error 42。60 秒期限后包装器停止自己启动的 Unity PID 38448，exit 1。测试执行耗时不等同整个 Unity 进程耗时，未把 runner 超时记为正常通过。
- 相关修改 `git diff --check` 通过，仅行尾转换提示。最终测试后没有运行 Unity 进程。没有运行 PlayMode、没有新建临时 Unity 项目、没有操作个人存档或更改包/网络/许可证。

本次没有重跑模拟器诊断或声明新版本的模型 parity。此前 validation-final.md 的 65/65、23/23 等结果对应此前版本，不能替代本次源码的证据。
