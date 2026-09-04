# Kingdom 当前版本主审查

更新时间：2026-08-30

本文件只描述当前工作树。旧问题清单、过期测试结果和历史模拟结论已归档，不能再作为当前阻塞项引用。

## 1. 当前结论

Todolist 对应的玩法功能已经在当前源码和资产中形成完整实现，当前阶段不需要继续扩充 Resource、Building、Research、Workshop、Sector 或 Ultra 内容。

当前真正剩余的是外部验收证据，而不是继续堆功能：

- Huawei P40 Pro 真机安装、触控、长文本和性能验收；
- 使用当前版本进行一次无作弊的新游戏节奏记录；
- Android APK 构建工具链需要解决隔离副本 Bee 编译冻结后重跑。

## 2. 项目与内容快照

- Unity：`2022.3.62f3c1`
- 主场景：`Assets/Scenes/SampleScene.unity`
- Resource：40
- Building：66
- Research：129
- Workshop：83
- Sector：10
- Tutorial：8
- Story：24 个运行时章节

内容静态闭包：

- Industrial 及以前：Research 81/81、Workshop 37/37、Building 50/50
- Spacer：Research 47/47、Workshop 46/46、Building 16/16
- Ultra：Research 1/1
- 不可达 Research、Workshop、Building：0
- `OpposingRawResourceFlows=0`

## 3. Todolist 实现状态

### Batch 1：可信基线

- PlayMode 测试通过 `SaveManager.SetSaveRootOverrideForTests()` 使用独立 D 盘临时目录，不读取或删除真实玩家存档。
- Research Tree 以 `UIResearchGraphGesture.IsInitialized`、节点数量和正尺寸共同作为 Ready 条件。
- Unity 版本已统一为 `2022.3.62f3c1`。
- 当前资产计数、闭包和静态门禁已更新。
- 过期 master audit 已移入 `.codex/archive/`，本文件不再混用历史失败和当前状态。

### Batch 2：开局 0~10 分钟

- 新游戏起始库存为 60 WoodLog，只在真正创建新档时应用一次；加载、备份恢复和离线恢复不会补库存。
- Resources 教程改为解释 Food/WoodLog 的库存和净产出，并能直接打开目标资源详情。
- Resources 步骤必须查看 Food 或 WoodLog 详情，单纯打开页面不会完成。
- Population 会按真实状态定位 Food 或当前住房方案；Animal 默认住房方案为 WoodHouse。
- ProductionChain 会分析缺失的生产者、消费者、Research、Workshop、生产力或资源阻碍，并导航到真实对象。
- `ProgressionMilestoneRecorder` 在开发环境记录首个资源详情、建筑、人口、研究、生产链和时代里程碑，不写入玩法存档，也不执行自动决策。

### Batch 3：前两次时代跃迁

- `EraGoalEvaluator` 将时代推进的前置研究、资源和实际满足状态输出为可验证清单。
- Era 页面区分硬条件与建议准备度，不修改 `AdvanceTechLevel`、存档格式或研究成本。
- 当前没有根据冻结的 EconomySimulator 调节数值；真实节奏仍等待玩家实测。
- 瓶颈审查记录在 `docs/audits/p1-09-bottleneck-source-review.md`。

### Batch 4：Research UX

- Overview 工具栏可定位当前研究目标和当前时代研究。
- 研究详情与图节点显示直接前置及当前状态，目标定位不会改变 Research State。
- 节点按钮转发拖动生命周期，短按仍为点击；空白拖动面可接收射线。
- 研究图按真实 viewport/content 溢出分别启用横向和纵向拖动。

当前运行日志记录：

- nodes=129，uniqueCells=129
- duplicates=0，backwardsEdges=0
- viewport=(1630,1588)，content=(11220,2100)
- horizontalOverflow=true，verticalOverflow=true
- canPanHorizontal=true，canPanVertical=true
- 真实指针拖动产生内容移动
- legacyRootChildrenInactive=true

### Batch 5：中期内容去同质化

- 没有新增 TechLevel。
- 中古时代核心研究已按建设、生产、研究和人口等不同效果角色审查，并有 Editor 回归测试。
- 本轮玩家文案重写不改变经济数值、前置、效果或 GUID。

### Batch 6：Industrial / Spacer 反馈

- 工业主线会展示当前目标、下一步和后续路线，不新增第二套任务系统。
- Spacer 星区详情提供真实远征补给预览。
- 60 秒回归测试比较预览 Food/战略资源消耗与实际 Tick 扣除，使用同一运行时费率合同。
- Sector 持续产出不替代玩家的高级工业生产链。

### Batch 7：Story、存档、移动端与文档

- Story 保持只读文明记忆，不提供经济奖励，不强制跳转。
- 教程详情访问记录是瞬时 UI 状态，不进入经济存档；已完成步骤不会倒退。
- 存档压力测试覆盖新档、时代、活动研究、队列、已付款等待、Workshop 和 Sector 状态。
- Android 静态配置为 IL2CPP、ARM64、minSdk 22、targetSdk 35、横屏。
- CanvasScaler 合同为 ScaleWithScreenSize、2640x1200、Match Width。
- `docs/content/progression-roadmap.md` 已以 Milestone A-D 描述当前推进顺序，Ultra/Archotech 保持冻结。

## 4. 当前验证证据

| 门禁 | 当前结果 |
|---|---|
| Todo 静态综合门禁 | 通过 |
| 内容静态闭包 | 通过 |
| 原始建筑资源流冲突 | 0 |
| YAML 本地脚本引用 | 无未解析 GUID |
| Android 配置 | 通过 |
| P40 UI 静态合同 | 通过 |
| Unity EditMode 隔离运行 | 644/644 通过，0 skipped |
| Unity PlayMode 原项目最新报告 | 32/32 通过，0 skipped |
| Unity PlayMode 隔离批处理 | 31 passed、1 skipped、0 failed；跳过项为无外层溢出时的拖动条件用例 |
| Research Tree 专项运行日志 | 129 唯一节点、正尺寸、横纵溢出与真实拖动通过 |
| Android APK | 未生成；隔离副本在 Bee `ScriptAssemblies` 阶段冻结并于 1800 秒超时 |
| P40 Pro 真机 | 未执行，当前没有可用 ADB 设备证据 |
| 当前新游戏真实节奏 | 未执行完整无作弊实玩 |

当前测试证据：

- `TestResults/Latest-Test-Errors.txt`
- `TestResults/EditMode-results.xml`
- `TestResults/PlayMode-Isolated-results.xml`
- `data/content-closure-static.md`
- `Logs/codex-yaml-reference-audit.txt`

## 5. Android 构建结论

2026-08-30 在 D 盘隔离项目执行了真实 Android 构建：

- 静态 Android、UI 和 YAML 门禁均通过；
- Unity 授权成功；
- 没有 C# 编译错误、Gradle 错误或磁盘不足信息；
- 构建在 Bee `ScriptAssemblies` 后端启动后不再写日志；
- 1800 秒后按脚本上限终止，未生成 APK；
- 父进程消失后残留的隔离 `bee_backend` 已停止；原项目 Unity 进程未受影响。

不要把这次超时描述为 Android 构建通过，也不要把它误写成玩法代码失败。下一次构建应先清理隔离副本的 Bee 缓存或使用原项目关闭后的现有 Library，再重跑一次，不需要修改经济或 UI 功能。

## 6. 不可违反的规则

- Food 是唯一有库存容量的资源。
- 不增加普通资源 MaxAmount、仓库或隐藏 Clamp。
- 不重新引入 workforce。
- 不运行或扩展冻结的 EconomySimulator。
- 不用旧模拟报告调节当前数值。
- 不新增第二套 Quest、Lore 或 Research Tree 系统。
- Ultra / Archotech 在 Milestone A-D 和移动端验收前保持冻结。
- 不把静态检查、无图形批处理或模拟器结果冒充 P40 真机验收。

## 7. 下一步

1. 在可用的 Android 构建环境中解决 Bee 缓存冻结并生成 ARM64 APK。
2. 连接 Huawei P40 Pro，验证安装、启动、横屏、Research Tree 拖动、长文本和后台恢复。
3. 使用 `ProgressionMilestoneRecorder` 完成一次当前版本无作弊新游戏实测，再根据真实瓶颈决定是否调数值。

在完成这三项外部验收前，不继续新增大量内容。
