# 实际运行代码重构：2026-10-17

## 范围
用户明确切换到实际游戏运行代码重构；测试精简停在round3。当前运行代码8文件净删55行，保留已有6份未提交bugfix。baseline.json保存重构前全部Runtime/Manager源码hash和行数，已核对其中6份修改与此前source-inputs.json一致；pre-existing-changes.patch保存切换时工作树。最终8份输入见source-inputs.json。

## 重构
- ResearchManager：RemoveQueuedResearch与RemoveQueuedState复用私有RebuildQueue，同步Queue/HashSet/快照dirty；保留保留项顺序、取消State变化、缓冲清理及通知位置。删除同步块内重复dirty赋值。
- WorkshopManager：ArePrerequisitesMet与TryPurchase复用GetPrerequisiteFailure；canonical定义、系统/时代/已购校验、研究优先于工坊的失败顺序及原子支付回滚均保留。
- SimulationManager：AdvanceOffline切出campaign后复用现有FlushOfflineCampaignRemainder，先余量后普通离线tick；相同研究节奏和成功推进后清零。
- CampaignState/GameState：删除无调用的4参数exact恢复重载；所有实际事务回滚继续使用携带doctrine的5参数版本。非exact恢复链保留。
- ResearchState/ResearchManager：删除内部Restore未读取bool及调用者为其计算的无效表达式；完成态验证复用刚重算的CostPaid，少一次ledger扫描。存档CostPaid字段和ledger校验、Editor现有接口保持。
- MilitaryState：完全相同的ResetDerived复用InitializeNew，重置值与Version递增次数不变。
- UltraProjectManager：删除无调用的私有CalculateProgressRate；实际进度计算和完成边界搜索保留。

没有修改定义/经济值、存档schema/ID/GUID、资产meta或UI布局；没有新增依赖/反射/全局抽象，没有提交或推送。

## 验证与已知失败
- dotnet build Kingdom.DeveloperTests.csproj --no-restore：exit0，0errors/6条既有TextureImporter废弃警告，14.26s；compile-tests.log。
- 静态闭包content-closure-check：exit0，报告content-closure.md及log。仅运行修改后检查，定义未改。
- NewEconomySimulator：exit0，23/23，simulator.json。该模型不链接此次Manager/State代码，不替代真实Unity验证或证明runtime parity。
- 真实Unity EditMode首批226项225通过/1失败，0跳过，2.0291612s，editmode.xml/log。唯一失败WorkshopBenefitPreviewTests首方法使用OilDerrick并要求耗材preview，而当前OilDerrick.asset resourceConsumptionRates为空；预览计算代码本轮未改。该fixture改为实际一进一出的CokeOven，全部原断言保留。
- fixture修正后WorkshopBenefitPreviewTests整类3/3通过、0跳过、0.3023787s，editmode-preview-final.xml/log。其他223项沿用首批通过结果；不是单次226全绿。最后fixture一行修改由真实Unity重新编译。
- 两批Unity均在测试XML落盘后原生退出挂起，60秒包装器停止自启动PID，runner exit1；runner-status.txt和runner-preview-status.txt。测试通过与runner正常退出分开。
- git diff --check通过；未运行PlayMode或全套，无实际性能提速测量。

## 续接
此批等价重构已经完成。后续若继续，先核对现有输入与调用；ResearchManager.ValidateRestoreInput同一ledger连续调用两次AreAllResourceCostsPaid是下一低风险候选，尚未改。Building成本边界分支与SaveManager JSON深度解析有不同异常/边界语义，不能按代码相似直接合并。
