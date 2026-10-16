# 资源降产与幸福度：本轮验证

输入：HEAD `769446de95eab285585f127d314b2b1fa4db6604` 加当前未提交工作树。相关源码、测试及模拟器的 SHA256 见 `source-inputs.json`。Unity 2022.3.62f3c1。未改经济资产定义，未创建临时项目。

| 检查 | 本轮结果 | 证据 |
|---|---|---|
| 静态内容闭包 | 修改前后 exit 0，Industrial 闭合，Spacer/Ultra 定义可达 | closure-before.md、closure-after.md |
| 离线确定性诊断 | executed=true，23/23 通过 | diagnostic-after.json |
| 开发程序集编译 | dotnet build Kingdom.DeveloperTests.csproj --no-restore，exit 0 | compile.txt |
| 最终真实 Unity EditMode | 65/65 通过，0 失败、0 跳过，进程正常 exit 0 | edit-final.xml、edit-final.log |
| 最终真实 Unity PlayMode | 3/3 断言通过，0 失败、0 跳过；原生退出超时，包装器 exit 1 | play-final.xml、play-final.log |
| 本次相关文件 diff 检查 | git diff --check exit 0 | 本轮命令输出；只有行尾转换提示 |

EditMode 过滤：FoodEfficiencyTests、FlowEfficiencyTests、EconomyIntegrationRegressionTests、SimulationDeterminismTests、SimulationBudgetTests、LongHorizonSimulationTests、KingdomLogicTests.BuildingEfficiency_UsesTheLeastSatisfiedResourceInput。包含循环生产零库存、0.02、1e-8、1e-100 及充足库存，独立链不受影响，Food/幸福度、电力与物流反馈，以及在线/离线和长时回归。

PlayMode 过滤：KingdomPlayModeTests.ProductionBudget_ManualTicksFundFoodFeedbackAndRecoverWithInventory、FoodProducer_RecoversAfterInventoryAndPriorEfficiencyReachZero、IndustrialProduction_StartsOilAndMachineryWithoutCircularSupply。执行 SampleScene 和实际 ManualTick；补料后生产效率恢复。测试使用 Temp 下隔离存档根，结束后已核验不存在 KingdomPlayModeTests-* 目录。

运行命令共用 `-batchmode -disableManagedDebugger -job-worker-count 2 -projectPath D:\GitHub\Kingdom -runTests -testPlatform <EditMode|PlayMode> -testFilter <上述过滤，分号分隔> -testResults <本目录 XML> -logFile <本目录 log>`；EditMode 另加 `-runSynchronously`，PlayMode 不加，以保留协程用例。

最终 EditMode 自身耗时约 210.50 秒，正常退出。PlayMode 用例耗时约 2.68 秒，随后 Unity PID 24268 停在 Input System Shutdown、Licensing disconnect 与 Curl error 42，300 秒期限到达后包装器停止自己启动的进程。未终止用户 Editor。日志的许可证刷新/网络错误与脚本编译错误分开记录；本轮未发现脚本编译或测试异常。

PlayMode 的正常 runner 退出仍未通过，不能把 XML 通过称作完整 runner 验收。未通过修改第三方测试框架、过滤协程用例或强制退出来制造正常退出码。`TestResults/Latest-Test-Errors.txt` 是旧日期结果，不能用作本轮依据。

早期失败证据保留：before.xml 展示旧幸福度分支的回归失败；budget.xml/budget3.xml 暴露候选效率量化次序错误，随后修正，未放宽严格预算断言。budget2.log 的 -noUpm 实验缺包，不计通过；后续标准加载已恢复。budget4.xml 30/30 通过但原生退出超时，由最终 EditMode 正常退出结果补足相关 EditMode 验证。

模拟器诊断只证明 fixture 的确定性回归；原有约束合并等模型差异仍保留，没有声称完整 Unity parity 或节奏验收。未执行全仓库所有测试或调整玩法平衡。

后续只读连通性检查：PowerShell 对 `https://public-cdn.cloud.unitychina.cn/config/production` 发起 HEAD 请求，10 秒后 HttpClient.Timeout；未获取 HTTP 状态码。这与 Unity 日志中同地址超时一致，表明该网络请求问题仍可复现；不单独证明原生退出卡住的根因，也不证明许可证无效。未修改网络、代理、许可证或包配置。
