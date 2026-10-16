# 资源短缺、幸福度与测试清理：当前交付

本记录对应 source-inputs-current.json；此前 validation-final.md/review-final.md 为历史输入，不能替代本记录。

## 行为与修复

- 缺料降产以库存及本 tick 可实现供给对完整需求计算满足率，多输入取最小值；建筑效率再乘食物约束、电力和物流约束。末尾对受影响链做预算校正，避免亏损循环与幸福度反馈导致无来源生产。库存支持的部分生产及极小合法速率保留。
- 幸福度负净粮产分支同时受食物满足率与人均亏损约束，修复满足率从 1 降到 .999 反而提高幸福度的问题。正向对数饱和曲线、科技加成和 1.5 上限保持。
- 预算校正保留完整建筑/Ultra/Relic 需求，只刷新实际供给；缺料农场不能按满产提供食物满足率。在线与离线资源结算使用同一 tick 已核算的幸福奖励快照。
- 新确认并修复外部流量预算遗漏：Ultra/Relic 按 raw 需求乘全局流量满足率预留，材料不足只会降低实际消耗。先结清物料反馈，下一轮才在流量校正中应用外部预留，避免过早统一缩放导致停工。二分到量化端点时终止重复检查。
- 模拟器同步实际粮产及候选食物满足率；未添加模型没有的 Ultra/Relic，未声称真实 Unity trace parity。

## 测试清理

- Food/Flow 22→17 项，去掉重复及反射结构检查、计算小数固定值；保留关系、范围、守恒及离散 Version 契约。
- 两个长全系统集成合并为 60s 模拟 smoke，独立长窗口边界检查保留。原两项约 209.73s，当前短项 0.674890s；覆盖范围随缩短而减少。
- PlayMode 合并重复启动、6000→30 次手工 tick，去掉无性能门槛的重复采样、中间重复宽度及弱重复 TMP 用例；方法静态计数 99→97。
- 清理旧固定颜色、36px 图标尺寸、16:9 插画/卡片比例、固定 zoom 数值；保留动态宽高比、可达/不重叠/点击边界、用户视图恢复和资源图缓存行为。未以删除断言掩盖新发现的守恒失败。

## 当前证据

- external-flow-before.xml：新增电力/物流两项真实 Ultra 扣料回归 0/2，通过运行复现预算缺陷。
- external-flow-after.xml：第一版预留过早使 Ultra 停工，24/26；保留失败证据，未放宽断言。
- external-flow-final.xml：调整处理顺序后 26/26。
- coal-flow-before.xml：真实 CentralPowerStation + MechanizedCoalMine + Coal=.02 + Ultra 正推进/煤电守恒 1/1；追加疑点没有复现，未据此扩大算法修改，保留短回归。
- edit-current.xml：FoodEfficiencyTests、FlowEfficiencyTests、EconomyIntegrationRegressionTests 共 39/39，0 失败、0 跳过，1.5997477s。真实 Unity 导入、编译并执行当前源码。日志 edit-current.log 无脚本编译/测试错误。
- 上述 Unity 写结果后原生退出卡住；60s 包装器终止自己启动的进程，exit1，不能称 runner 正常退出。最终自有 Unity 进程已不存在。
- compile-assertion-cleanup.txt：dotnet build Kingdom.DeveloperTests.csproj --no-restore exit0，0 errors、6 条既有 TextureImporter 废弃警告；覆盖 PlayMode 测试编译，不能当作其运行结果。
- simulator-actual-food-diagnostics.json/.txt：现有确定性诊断 23/23，exit0；属于模型自测。
- closure-current.md/.log：当前定义静态闭包检查 exit0。未改资产数值、ID、GUID、存档协议或容量规则。
- 本轮按用户要求不运行 PlayMode、不运行全仓库测试；未操作用户 Editor、真实存档、许可证、包或网络。未创建临时项目。
