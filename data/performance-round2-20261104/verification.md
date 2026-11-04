# 2026-11-04 第二轮性能优化

范围：用户要求先找出上一轮之外的10个问题，再修复，以减少CPU重复工作、提高游戏性能和降低持续负载为目标。起始HEAD为45438758efaf2f86a5f5ff88b30b693679ac5488，工作树包含上一轮3项性能改动及既有交接、未跟踪.workbuddy-ai/memory；均保留。不改经济定义/数值、存档协议、GUID、Prefab或手机固定30 FPS，不安装依赖，不提交推送，不打包。

## 找出的问题与实现

1. ResourceManager.Tick对production/consumption均为零的资源仍执行库存大数运算：双零直接跳过，保留参数校验和流量恢复。
2. Ultra阶段六个数字属性每次从序列化字符串解析：缓存解析结果，源字符串改变即失效，空值保留原默认值。
3. Ultra完成时间48轮二分反复分配、聚合相同持续成本：每Tick局部聚合一次，共用于满足率、搜索和付款，不修改事务、Food提交、SaveDTO或回滚。
4. StrategicFoodConsumptionRate只取Food耗率却构建完整战役/探索preview：与preview共享直接耗率公式，保留研究、姿态、遗迹及母星系边界。
5. Max/Custom建筑刷新重复求最大数量，状态文案又求一次：初始建卡及稳定刷新由3次合为1次，数量、可用状态、文案共用结果；升级模式与按钮回调/图标保持同一建卡快照，实际点击仍走Manager校验。
6. 稳定的建筑/工坊Outline仍反复赋值、调用SetVerticesDirty：只有颜色、alpha和距离变化才写属性。
7. 资源详情重复扫描全部建筑关系并测量相同长文本：缓存当前资源的定义关系及同文本/宽度测量高度；数量、效率、倍率、供给与可建性仍实时读取，重选/宽度/文案变化重新测量；包含星区建筑，缺失State跳过，新State能匹配。
8. 生产树轮询历史折叠后代，隐藏解锁触发整图重建：只轮询展开可见子树；重开时刷新直接children，保留原节点和展开状态。
9. Tutorial正常分支没有给旧null哨兵赋值，使Overview频繁刷新及强制mesh/layout：独立初始化标记、稳定内容2秒刷新、相同文本/宽度/导航状态跳过mesh/layout。行动、目标、阻碍、等待、反馈等展示输入变化同轮刷新；公开RefreshUI始终立即更新。
10. OnboardingInputGate每0.2秒扫描全UI（含隐藏Research）：首次完整安装过滤器，统一prefab factory为新子树一次安装，后续Configure复用；覆盖原直接Instantiate入口与inactive控件。

## 测量方法与边界

同机真实Unity 2022.3.62f3c1 Editor中预热后多批次取中位数，时间单位ms/调用。运行时7批、UI5或7批；具体迭代数与完整Unity参数见XML的output、log头部。UI模拟暂停、真实定义、受控资源/研究/建筑状态；资源详情计时为稳定文本路径，活跃数值变化仍重新测量。真实定义树使用RocketFuel、全部真实建筑/星区定义，受控allVisible展开最多100节点后折叠Root，旧轮询337次predicate/refresh、新为0；另保留合成160层压力测试，不用它代替真实定义结果。

GC.GetAllocatedBytesForCurrentThread本机Unity返回0，分配证据标NA，不声称零分配。以下是局部CPU路径和图形失效工作量，不能加总成全帧、真机功耗或温降。Ultra两项用中间版本隔离，不重复计算：整体完成Tick为0.197030→0.111058ms（-43.63%），并非两个百分比相加。

| 项目 | 优化前 | 优化后 | 耗时/工作量减少 |
|---|---:|---:|---:|
| 全33资源、稀疏流量Tick | 0.009839 | 0.002262 | 77.01% |
| Ultra数字缓存：完成Tick，仅加入缓存 | 0.197030 | 0.151972 | 22.87% |
| Ultra成本复用：在缓存版本基础上 | 0.151972 | 0.111058 | 26.92% |
| 活动战役+探索食物耗率 | 0.010361 | 0.000860 | 91.70% |
| Max建筑整卡刷新 | 6.976730 | 3.457480 | 50.44% |
| 稳定Outline：2图形、2次刷新dirty回调 | 8次 | 0次 | 100%（工作量，未独立计时） |
| 资源详情稳定刷新 | 0.507920 | 0.054635 | 89.24% |
| 真实定义折叠树可见性轮询 | 0.007056 | 0.000003 | 99.96%（接近计时下限） |
| Overview稳定自动刷新 | 0.397823 | 0.025293 | 93.64% |
| 已缓存Research页面的门禁Configure | 0.038284 | 0.000274 | 99.28% |

没有把Outline的工作量减少说成独立CPU耗时下降20%；它的效果也包含于整卡耗时中。树的主要行为收益还包括折叠期间隐藏变化不再销毁重建可见图，不以微秒轮询数字推算整页收益。

## Unity验证记录

| 证据前缀 | 结果 | 进程/runner说明 |
|---|---|---|
| edit-before | 31/32，折叠隐藏解锁回归按预期失败 | runner 2 |
| play-before | 4/6，稳定Overview mesh及Outline dirty回归按预期失败 | runner 2 |
| edit-intermediate | 4/4 | XML通过，150秒退出超时，不能称正常退出；补33资源/真实树旧基线及Ultra cache-only |
| edit-after | 70/70，无跳过 | 正常退出，runner 0 |
| play-after | 16/19 | runner 2：新增公开刷新fixture先抓到启动占位；另2项截图需要GPU。保留失败记录 |
| play-final | 19/19，无跳过 | 正常退出，runner 0；公开刷新fixture先正常初始化，两个截图测试另转GPU |
| play-building-verified | 4/4 | 最后一行升级模式修正后的XML通过，180秒退出超时，不能称正常退出 |
| play-gpu | 6/6，无跳过 | Direct3D11/RTX4060，Unity退出0、包装命令退出0；重跑上述4项建筑测试及2项需要GPU的生产树截图测试 |

Edit覆盖运行时流量恢复、战略耗率与完整preview两姿态对照、Ultra缺料/零Food/支付不部分提交/完成收费上限/数字缓存源变化、生产树隐藏解锁/重开/循环/升级及布局、Food/Flow和Ultra边界。Play覆盖Max/Custom与零库存、状态图标、实时资源来源/数量/效率/可建性/缩窄宽度重测、稳定零dirty、门禁inactive新控件、Overview稳定mesh/公开刷新/同目标可建性变化、真实raycast/嵌套pin/研究等待及生产树交互。

真实Unity编译随测试运行完成；没有执行全项目测试，没有进行手机温度/功耗测试。

GPU调用为find-unity.ps1定位Editor后Start-Process（Hidden），传入-batchmode -force-d3d11 -runTests -testPlatform PlayMode，未传-nographics；精确filter与输出路径记录在play-gpu.log头部，退出/数量记录在play-gpu-runner.txt。GPU计时建筑3.571110ms、资源详情0.056345ms，仅作交叉核对，不混入上表相同nographics后端比较。

## 收尾

- GPU生成26张截图留在`D:/GitHub/Kingdom/data/experience-20261023/production-pages/20261104-111244/`；抽看1760×800的折叠原木根节点与展开钢材来源图，节点及来源可见。截图包含fixture当时的教程/进展提示，不作为全流程视觉验收。
- 自动审批拒绝包含截图目录迁移、字体恢复、备份移除的组合命令，原因仅为`blocked by policy`；命令未启动。改用单独Copy-Item只恢复字体，已通过。
- 字体已恢复与备份相同的SHA256：722BFD776012F77B34C3C33AB7BC512BE7DDDDBC4FD9C44709CE0206689C539F，git无字体diff；本轮`font-before.backup`（32,084,075字节）及截图保留原位，不绕过拒绝清理。后续需要时由用户处理这两个明确路径。
- 无活跃自建Unity进程；测试临时存档目录未残留；未创建临时工程。git diff --check通过。上一轮源码和用户未跟踪内容保留。
