# 资源降产与幸福度审查、修复

## 授权与起始状态

用户选择 B：解释当前实现，深入审查并修复有明确依据的缺陷，玩法规则及平衡变更另行确认。起始 HEAD 为 `769446de95eab285585f127d314b2b1fa4db6604`，工作树有大量已有内容、UI、教程及清理改动；这些改动保留。本主题新建交接，不覆盖其他任务交接。

只读分工 happiness_review、supply_review 检查两条调用链；happiness_review 随后仅获授权修改模拟器的 SimulationCore.cs、Tests/ValidationSuite.cs，主代理负责 Runtime、Unity 测试和最终复核。

## 已确认缺陷与实现

1. `HappinessFormula` 原来在满足率小于 1 时提前返回满足率，绕开负净粮产惩罚。净产 -100、人口 10 时，满足率 1→0.999 导致幸福度约 .0909→.999。改为负净产约束与满足率取较小值；正向奖励曲线、科技加成和上限均保持。
2. `BuildingManager.PrepareTickResourceSatisfaction` 的有界迭代不是资源守恒证明。亏损资源循环、能源/物流反馈、Food→幸福度→原料奖励反馈，都会在迭代结束后留下未支付的消耗，库存 clamp 掩盖欠料。新增最终物料及能源预算修正：只对受影响的依赖组件缩放；库存支持的部分生产保留，独立生产保持。Food 改变会传播幸福奖励依赖。使用 ExpantaNum 的可行下界与有界二分，不引入路线搜索或调参。
3. 预算候选按 `Amount * (Efficiency * scale)` 逐栋重新汇总，与最终 State 的量化顺序相同；不能用汇总 rate*scale 替代。修正后普通资源派生率从正贡献重建，避免原有 delta 消减把合法微小速率 snap 为零。
4. 在线和离线均在建筑预算准备后采集本 tick 的资源幸福奖励，资源结算使用同一快照；人口在中间增长或离开不再改变已核算的资源生产预算。

没有修改资产定义、费用、稳定 ID、GUID、存档版本、容量规则或幸福奖励上限。所有原有文件的已有改动均保留；BuildingManager/ResourceManager/SimulationManager 和 PlayMode 测试也有本任务前的变更，不能把整份 Git diff 当成本任务贡献。

## 当前算法说明

- 资源满足率：`clamp01((库存 + 本 tick 实际可实现生产 * dt) / (潜在完整消耗 * dt))`，无需求时为 1；占领星区供给纳入，建筑多输入取最小满足率。
- 建筑效率：全局效率 × 资源约束 × 食物幸福约束 × 电力满足率 × 物流满足率，再 clamp01；消耗电力且不耗食物的自动化建筑免食物约束。最终预算修正可进一步降低不可支付的效率。
- `P=max(1,人口)`，`N=粮食生产-建筑耗粮-人口*0.8`。负净产时 `H=min(满足率,1/(1+(-N)/P))`。非负净产但供给不足时 H 为满足率。
- 满供给且 N≥0 时 `s=log10(1+N/P)`，`H=min(1.5,1+0.5*s/(s+1)+科技幸福加成)`。N=90、P=10、无科技时 H=1.25。
- 普通资源产出、电力、物流使用 max(1,H)；原料消耗不乘奖励，粮食产出不乘幸福奖励。人工建筑受 min(1,H) 约束。研究实际速度、人口增长使用完整 H。饥饿人口离开另要求食物耗尽及负净产，不能把库存缓冲认成无效。

## 验证证据（本轮）

证据目录 `data/supply-happiness-20261016/`。前后 closure 均 exit0，Industrial 主线和当前后续定义静态可达；未改定义。诊断从 13 项增至 23 项，最终 JSON executed=true、passed=true。模拟器是确定性 fixture，不是实际 Unity parity 或节奏验收；保留其既有约束合并和幸福奖励参与满足率的差异。

`dotnet build Kingdom.DeveloperTests.csproj --no-restore` exit0，Runtime/Editor/EditMode/PlayMode 编译门均通过，日志 `compile.txt`。这不替代 Unity 测试。

- before.xml：33/34，新增幸福度单调测试按预期失败；Unity 写结果后退出超时，包装器停止自己启动的进程。
- budget.xml、budget3.xml：严格预算回归暴露量化次序问题。失败日志保留，没有放宽预算断言。
- budget2.log：一次尝试 -noUpm 造成 TMP/UI 包引用缺失，未修改包配置，下轮恢复标准包加载；该次不算通过。
- budget4.xml：真实 Unity 30/30，0跳过，覆盖幸福度、资源循环、微小库存、Food反馈、电力/物流循环；Unity 退出超时，因此不能记为完整 runner 通过。
- 最终扩大 EditMode 与 PlayMode 的结果、进程退出状态需在本交接后续补齐。当前不得宣称目标已完成。

所有测试使用现有项目，未创建临时 Unity 副本；新增 PlayMode 使用隔离保存根，个人存档不属于操作范围。未安装、升级、发布、commit 或 push。

## 下一具体动作

最终 EditMode 已完成：65/65 通过，0 失败、0 跳过，Unity 正常 exit 0。包括最后的电力/物流满足率刷新修改，是真实 Unity 当前源码编译和执行证据。

最终 PlayMode 三项均执行并通过，0 失败、0 跳过，XML 耗时约 2.68 秒；Unity PID 24268 随后停在原生 Shutdown/Licensing disconnect/Curl error 42，300 秒后包装器终止自己启动的进程并 exit 1。正常 PlayMode runner 退出未通过。没有终止用户 Editor，没有修改测试框架或用同步过滤跳过协程用例。

证据汇总：`data/supply-happiness-20261016/validation-final.md`，当前相关输入 SHA256：`source-inputs.json`。相关文件 `git diff --check` exit 0；测试隔离存档根 `Temp/KingdomPlayModeTests-*` 已核验不存在。`TestResults/Latest-Test-Errors.txt` 是旧日期，未用于本轮结论。

代码实现与针对性断言验证已完成；剩余阻塞为 PlayMode Unity 原生进程正常退出。下一具体动作是在可正常退出的 Unity 环境复跑同三项 PlayMode，核对非零用例、XML、Console 和进程 exit 0；不需继续扩展玩法修改或重复宽泛审计。完整 runner 验收达成前保留未完成状态。

续接复核：退出前日志还明确记录 `Leftover web requests after shut down`（play-final.log:1428 附近），运行阶段有 UnityConnect 对 `https://public-cdn.cloud.unitychina.cn/config/production` 的超时。该证据定位到 Unity 自身网络请求收尾，但不能单凭相邻日志证明具体根因。不修改 Packages、许可证、系统网络或第三方退出逻辑；没有新的运行环境变化时，不重跑相同命令。

## 后续用户指令与代码修复

用户明确要求先继续审查改代码，暂不做 PlayMode；随后要求减少测试体量，允许取消各种不必要的精确性断言。此前“先解决 Unity 退出才能继续”的顺序已不适用，本轮没有继续追问 Hub 状态或运行 PlayMode。

新增修复：预算 guard 不再清除 full-load/Ultra/Relic 需求和已算满足率，改为只刷新实际供给并重新计算；食物满足率不再把缺料农场的潜在满产当成实际供给，候选预算与最终满足率一起更新。相关文件 BuildingManager.cs、ResourceManager.cs、ResourceState.cs；未改资产、序列化或玩法参数。

测试精简：两次 2h/8h 全系统集成合并为一次 60s 集成（独立长窗口边界用例保留）；22 个 Food/Flow 单元测试减为 17 个，删除计算小数精度/重复/反射结构检查；循环库存 5 参数减为零、极小、充足 3 参数。短测试暴露的研究活动队列冲突仅修复测试夹具，以公开 RemoveQueuedResearch 在人工完成设置前清理队列。

最终针对性真实 Unity EditMode 为 37/37，0 跳过，执行约 1.55s；短集成从原两用例合计 209.73s 减至约 0.72s。Unity 原生退出仍卡住，60s 后自有包装器超时终止，不能称正常 runner 通过。详见 `data/supply-happiness-20261016/review-final.md`、`edit-review-final.xml`、`edit-review-final.log`。先前 source-inputs.json/validation-final.md 保留为旧版本证据，不覆盖冒充当前结果。

未复现的后续审查线索：外部 Ultra/Relic 的电力负荷与建筑实际耗电在最终预算中有分开计算的数值疑点，仅有手工推导，尚无运行时失败用例；不要当作已确认缺陷或直接改平衡。用户本轮要求的测试减负与上述两处修复已落地；PlayMode 按用户要求暂不执行，模拟器的新源码 parity 未验证。

## PlayMode 测试精简（用户明确追加要求）

本轮只修改测试与对应验收指针，不运行 PlayMode。保留现有其他任务修改、.meta 和全部生产代码。

- KingdomPlayModeTests：将新档状态检查与模拟 smoke 合并成 `NewGameStartup_InitializesCoreStateAndAdvancesSimulation`，少一次隔离场景加载；6000 次 0.1s ManualTick 缩为 30 次，保留真实 bootstrap、初始状态、实际资源增长和有限值检查。原十分钟稳定性覆盖退出 PlayMode 日常 smoke；不声称缩短后仍覆盖十分钟过程。删除研究队列固定 y=0、标签 pivot=.5 精度断言，CanvasGroup alpha 改为可见/不可见范围，交互与射线检查保留。
- KingdomOnboardingPlayModeTests：删除仅检查首个 TMP 的弱重复用例（逐章正文几何和 Story 总览测试仍覆盖），少一次场景加载；删除两处固定导航颜色值。
- ResourceProductionTreePlayModeTests：删除 8 次预热和 128 次无性能门槛的 benchmark 采样及对应日志；保留一次自动刷新、同一快照复用、显式刷新失效和新会话失效验证。
- StoryBackgroundInteractionPlayModeTests：章节布局宽度由 600/1440/1920 减为 600/1920，保留全部章节的窄/宽边界、可达和不重叠检查；测试名改 NarrowAndWideWidths。
- KingdomCollectionPlayModeTests：删除 Pin 图标固定 36px 宽高断言，保留图标状态、命中区与 raycast 检查。
- docs/testing/acceptance-checklist.md 同步新启动测试名与短程 smoke 边界，避免保留不存在的十分钟测试门槛。

静态计数 PlayMode 测试方法 99→97（不是实际 runner 执行数）。没有删除生命周期、图手势、真实点击、存档隔离等独有测试。`dotnet build Kingdom.DeveloperTests.csproj --no-restore` exit 0，0 errors、6 条既有 TextureImporter 废弃 API 警告，约 11.33s；证据 `data/supply-happiness-20261016/compile-playmode-trim.txt`。相关六文件 `git diff --check` 通过。未执行真实 Unity 编译。未运行 PlayMode，运行通过和实际加速幅度未验证；没有启动或留下 Unity 进程。

## 最新续接：旧断言清理与原始玩法审查收尾

用户最新要求同时清理过时断言、完成原始降产/幸福度审查，继续暂不运行 PlayMode。此前要求先解决 PlayMode 原生退出的门槛已被后续指令替代；本次不将环境修复列为玩法代码交付的前置条件。

清理四份 PlayMode 中固定 16:9、颜色与 zoom 数字断言，改为实际拟合/放大缩小/边界关系，保留动态比例、用户视图恢复、交互和可读性。没有批量删除协议或资源守恒检查。

外部 Ultra/Relic 流量疑点已转为真实失败回归：external-flow-before.xml 两项均违反建筑+真实 Ultra 实付流量总预算。BuildingManager 新增按外部 raw 需求×全局满足率预留，物料反馈先校正、下一轮再处理流量预留；第一版过早预留导致停工的失败输出 external-flow-after.xml 保留，最终修复不放宽守恒和正推进检查。真实煤电定义追加回归通过，没有据手工推导继续扩大修改。

SimulationCore.cs 同步实际农场供给、每轮/候选/最终食物满足率；无模型的 Ultra/Relic 不扩展，原有模型合并规则差异保留，不能宣称真实运行时 parity。

当前最终证据统一见 `data/supply-happiness-20261016/current-review.md`，输入 SHA256 为 `source-inputs-current.json`。真实 Unity EditMode 三类共39/39、0跳过，1.5997477s，短集成0.674890s；原生退出仍超时，自有包装器60s后清理进程exit1，不是正常 runner退出。测试程序集编译exit0（6既有警告）；静态闭包exit0、模型诊断23/23。相关git diff --check通过。当前无自有Unity进程或临时项目残留；本轮未运行PlayMode、未做全仓库测试。

本轮请求的代码修复、旧断言清理和算法说明已完成。下一次若用户要求运行验证，按其范围运行精简后的PlayMode；不自动重试原生退出、不回到Hub/许可证追问，也不将本轮针对性证据说成完整项目验收。
