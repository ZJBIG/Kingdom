# P0-01 / P1-01：D12 科研里程碑口径交接

## 范围与状态

用户要求读取 outputs 后尽快进入下一任务，不在 P0-04 或历史报告小点上持续投入。本批推进 DeepAudit 的 D12：诊断误把缺料排队当作研究开始。实现已完成，开发程序集编译通过；6 项专项测试已编写但尚未执行，不能将 D12 或 P0-01 标为运行验收完成。

## 基线与保留

- 接手存在 P0-04、项目指导和旧输出清理的未提交改动，全部保留。
- P0-04 以当前 handoff 为准：严格 v9，单主档，已有 .bak 忽略；不恢复旧版迁移、备份轮换或确认弹窗。
- 已阅读 outputs/ToDoList.txt、DeepAudit.txt、猫国比较与摘要、Ultra 20260914 正文、指导合并报告及验证摘要；旧 Ultra HTML 按结论节检索。未完整重读所有 PDF 或打开指导快照 ZIP，也不需要用旧包覆盖当前代码。
- 独立存档复核未确认阻断缺陷，生命周期和冷启动仍缺真实 Unity/运行期证据，不据静态复核声称验收通过。

## 本批实际变更

1. `Assets/Resources/Script/Validation/ProgressionMilestoneRecorder.cs`
   - 原 `FirstResearchStarted`（活动研究或队列非空）拆为 `FirstResearchQueued`、`FirstResearchPaid`、`FirstResearchProgressed`。
   - 仅已支付且有正进度才记录 Progressed；未被选择的免费研究不触发 Paid。已完成研究保留的进度用于识别两次采样之间完成的情况。
   - 150 秒诊断目标转用于 Progressed，不增设新节奏数值。
   - `Update` 转发公开 `SampleNow()`，三项 elapsed 属性只读；不修改 Manager State，不改变经济、研究支付、存档结构或资产。
   - 延续帧采样设计，不宣称精确事件时间；未支付且立即取消的帧间短暂入队不保证记录。
2. `Assets/Tests/Editor/ProgressionMilestoneRecorderTests.cs` 及新 `.meta`
   - 6 项：未选择不记录、缺料/支付/推进分离、采样间完成、重复采样只读且去重、旧档会话排除、重新初始化清空诊断。
   - 使用公开 Manager 命令与受保护初始化的强类型测试子类，不新增反射、不加载场景、不创建 SaveManager、不读写真实存档；只销毁自己创建的对象。
   - 补入实际要求的资源仅为付款夹具，不声称无作弊首局或真实节奏。
3. `docs/testing/acceptance-checklist.md` 添加指标定义与证据限制。

## 验证

- 首次 `dotnet build Kingdom.Editor.Developer.csproj --no-restore` 因 Bash 子进程缺 ProgramFiles 环境变量，在脚本选路径时失败，未得到 C# 编译结果。
- 仅对子进程补齐 `ProgramFiles=C:\Program Files` 后，原命令成功：Kingdom.Runtime.Editor 与 Kingdom.Editor 编译，0 错误、0 警告。没有修改构建脚本或安装依赖。
- `git diff --check` 通过；新增测试为 6 项，定向检索未发现新增反射。
- 额外测试源码编译尝试被执行保护拒绝，停止该路径，未绕过保护。因此开发程序集通过不能覆盖测试程序集编译。
- 未执行真实 Unity 编译。未执行本批 EditMode、PlayMode、Console 或真人验证。Unity 占用查询未返回可用信息，不把空输出当作项目空闲证明。

## 后续顺序

1. 环境允许时执行 `ProgressionMilestoneRecorderTests`，确认非零用例且零失败；之后与 P0-04 专项一起做全量 EditMode、相关非零 PlayMode 与 Console 验证。旧 XML 不算本轮结果。
2. 下一代码批次 D09：修 `KingdomPlayModeTests.OverviewDevelopmentGuidance_IsReadOnlyAndUnique` 的真实刷新与唯一性断言。当前源码在读取 Version 后立即比较，仍没有中间刷新。已定位 `KingdomUIRoot.RefreshUI()` 为公开刷新入口。fixture 的 SetUp 已设置隔离存档，但 TearDown 仅销毁 createdObjects，场景加载对象是否登记仍须核对，必须在所有可能保存的对象销毁后才清除 override；本轮未修改该 PlayMode 文件。不沿用反射，不以 Ignore/Pass 消除失败。
3. 然后 P0-02 E08 升级合同与 P0-03 实际布局诊断；不直接新增工厂或调倍率。P1-01 真人首局继续保留，外部运行体验不形成代理待办。
4. Ultra/Archotech、普通仓储、自动策略、新迁移器等不在本批范围。历史 outputs 不改写为当前完成状态。

## 同轮续接：D09 Overview 测试补强（实现，待运行）

用户继续要求推进，故完成上文第2项中的 Overview 子项，不扩展其他玩法。

- 修改 `Assets/Tests/PlayMode/KingdomPlayModeTests.cs`：Overview 用例两次清空展示正文，再调用公开 `RefreshUI()`，必须恢复非空正文；禁用刷新不再能靠原始文字蒙混通过。
- 在真实 Bootstrap 完成、隔离新档已建立且暂停模拟后，比较 GameState.Version 和各 Manager 保存数据快照。Story 直接读取只读历史集合，避免 `SaveManager/StoryManager.CaptureSaveData` 自带刷新掩盖副作用。
- 计数可见 PrimaryCard（含 Clone 命名）、其直属导航 Button 及卡内重复正文；要求单一主目标、导航与正文。此项是当前结构约定，不依赖中文文案或固定坐标。
- fixture 记录自己加载的 SampleScene；TearDown 在清除 override 前销毁场景根（包括后来生成的 UI）、本次新增的持久 Tutorial/Music 和原有 createdObjects。没有新增对真实存档的访问，也没有实际执行目录删除。
- WaitForRuntimeUiRoot 同时要求 Bootstrap.Completed 并再让出一帧，不再把仅出现 UI 组件等同启动完成。
- 本次未改生产 UI 代码、Scene/Prefab 或资产。源码静态检查与 diff 检查不能证明 PlayMode 通过；未执行真实 Unity 编译，未运行该用例，也未执行人为注入缺陷后的红灯验证。
- D09 的其他子项（无作弊玩家操作、新档入口旧反射用例）仍待处理；P0-01 整体不关闭。下一步优先在可用 Unity 环境合并验证 P0-04、D12 和本 Overview 用例，期间可独立审定 P0-02 E08 合同，不继续扩本批测试框架。

## 再次续接：outputs 审查与 D09 新档行为修缮（2026-09-15）

用户要求审查 outputs 并开启修缮。本轮从当前 dirty 工作树（HEAD `a85f0e7`）继续，不恢复旧报告方案，不覆盖已有 P0-04、D12、Overview、指导或美术改动。

### 审查结论

- 主要精读 ToDoList、DeepAudit、当日进展/交接；辅助只读复核猫国、音效、Ultra、插画及指导合并文本。没有逐页审查 PDF，未解压旧 ZIP；旧 HTML/大 JSON 仅局部核对。期间新出现的并行输出不计入已完整审查。
- D01/D02 原旧档/备份方案被严格 v9 单主档决策取代；D03、D12 和 Overview 已有实现，但缺真实 Unity 验收。
- D04 工坊域回滚、D11 同 ID 别名交易边界在当前源码仍成立，保留下一局部复现批次；没有确认正常玩家路径已触发。
- E08 的旧层级停建、副产物、定向加成仍应先审合同；不新增三工厂、不调数值。猫国/音效是候选改良；Ultra 和角色外观方案不因本轮笼统修缮授权变为已批准实施。

### 本轮改动

- `Assets/Tests/PlayMode/KingdomPlayModeTests.cs`：2 个现有新档用例不再反射调用 `InitializeNewGame/ResetForLoad`；共用 `LoadIsolatedNewGame` 走 `SampleScene` 与真实 Bootstrap，验证新档标记并暂停自动模拟。
- 十分钟 smoke 从自动 Update 与手动 Advance 混跑改为暂停后的 `ManualTick(0.1)`，日历比较使用起始差值；仍只声称稳定性覆盖。
- 新增 `NewGameCommands_BuildResearchAndReloadWithoutGrants`：固定路线自然积累，调用公开 TryBuild / HandleResearchAction / ManualTick / SaveNow / LoadOrCreateGame；覆盖木屋扣款与人口容量、锁定农场失败无资源/领土/生产力变动、农业真实库存扣款和支付账本、研究完成、农场增量食物生产、人口增长及隔离磁盘存读。
- 加载前手动 tick 改变木材，避免空操作 Load 通过；数值使用差额/容差和关系，离散建筑数量、枚举才精确比较。600 秒是回归预算，不是真人耗时。
- `docs/testing/acceptance-checklist.md` 追加 D09 范围与非覆盖项；更新已有 `outputs/P0-01-D12-进展与下一步-2026-09-15.txt`。未改生产代码、资产、ID/GUID/meta、经济参数、存档结构。

### 本轮验证与限制

- `git diff --check` 退出 0；仅既有 CRLF 转换提示。核对实际 diff，保留原 Overview/TearDown 改动，未计作本轮新增。
- 绝对路径 managed Python 只读源码契约检查退出 0：协程名无重复、3 项用例及启动辅助方法不含反射/直接赠资源/改完成状态，存在真实加载、手动 tick、付款库存差额、食物增量与存读入口。仅文本约束，不是 C# 编译或行为测试。
- Unity 占用查询仅回空输出，不能确定空闲；一次经 Bash 调用 PowerShell 的查询被安全检查拒绝，已停止该路径并改用专用入口，未绕过。没有启动 Editor、构建或测试，不重试此前被保护拒绝的测试源码编译方案。
- 未执行真实 Unity 编译。未运行新增/修改的 3 项 PlayMode、D12、P0-04、全量 EditMode、Console、缺陷注入红灯或真人测试；不引用此前 Developer 编译作为本轮证明。文件内其他旧反射测试保留，不称整文件无反射。

### 下一动作

在可确认项目空闲的 Unity 环境执行当前测试程序集编译，优先跑 3 项新档用例 + Overview + D12/P0-04 专项，再全量 EditMode 和相关非零 PlayMode。新路线未包含 UI 点击、原料加工链、工坊/时代门、冷启动；D09/P0-01 整体仍不关闭。通过后再推进 E08 合同审定和 D04/D11 有界故障回归，不继续只堆未执行测试。

## 运行验证续接：已尝试启动，但尚未取得编译和用例结果

以下补充前述阶段之后发生的实际执行；不以早期“没有启动 Unity”描述当前状态。

- 使用明确进程查询确认无 Unity 后，直接筛选 `ProgressionMilestoneRecorderTests`，没有运行完整存档或场景测试。D12 fixture 不创建 SaveManager，也不加载 SampleScene。
- `TestResults/EditMode-D12-20260915-run1.log`：UPM 服务退出 101，IPC 连接 30 秒超时，Unity 日志终止码 1；无对应 XML。
- 从注册表只读核实标准路径，仅对子进程补齐 APPDATA、ProgramFiles/ProgramW6432、ProgramData/ALLUSERSPROFILE。`run2.log` 显示 UPM 已连接、62 个包注册完成，Mono 初始域加载完成，但停止于 `Application.AssetDatabase Initial Refresh Start`。运行 16m54s 无可用进展后停止本次自有批处理；随后进程查询无 Unity。无 `run2.xml`，不能声称当前源码编译、测试通过或零用例通过。
- 续接复核发现 ComSpec、PATHEXT、NUMBER_OF_PROCESSORS、CommonProgramFiles 等标准环境仍缺失；已只读核实注册表值与目录。这只是下一次受限诊断假设，不据此认定资产刷新阻断根因。未修改全局环境、PATH、安全保护、包或缓存。
- 独立 D09 静态复核正文曾经返回，之前任务与输出的“最终正文未取得”记录已过时；仅采纳已收到的静态结论。Explore-1 后续刷新调查再次 502 网络失败，本次没有新分析结果，由主代理接管；不把失败代理当成功。
- 主代理定向检索自有源码的启动/刷新回调：`LatestTestErrorReport.Register` 只注册测试回调，`PMusicAudioImporter.OnPreprocessAudio` 只调整指定音乐目录导入设置；没有从这两个入口确认刷新死循环。没有调用栈，暂不能排除其它 Unity/包/系统阶段阻断。
- 本轮没有继续修改 D04/D11，也没有以静态检查替代运行验证。
