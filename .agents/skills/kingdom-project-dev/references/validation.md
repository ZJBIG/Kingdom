# 验证命令与副作用手册

先读取当前脚本 `param(...)` 与实现，再执行命令。以下是核对过的入口，不代表已执行或通过。不使用全局安装、自动升级、策略绕过或杀已有进程来让检查变绿。

## 0. 只读巡检

可选工具：`tools/codex/kingdom_project_probe.py`。使用已配置 Python 3.10+ 的绝对路径：

```text
python -X utf8 tools/codex/kingdom_project_probe.py --project-root . --check-skill --format json
```

上行 `python` 表示当前客户端提供的 Python 可执行文件，并非要求使用全局解释器；将路径替换为已确认的运行时。工作目录必须为 Kingdom 根目录，或为所有参数提供绝对路径。

该工具不联网、不调用 shell/Git/Unity、不写文件、不读存档；仅扫描白名单内项目文件的元数据，对指定一方文本内容计算 SHA-256，读取少量项目标记及技能文档。拒绝非项目目录；跳过符号链接、junction、缓存与归档。`--check-skill` 验证主技能、必要参考文件和本地简单内联 Markdown 链接；不解析引用式链接或锚点。仅使用可信本地磁盘上的静止工作树；拒绝 UNC 根，不能保证映射网络盘识别或并发恶意替换下的原子隔离。若报告需要持久化，另由主代理在用户授权的输出目录保存 stdout。

工具回归使用 `tools/codex/test_kingdom_project_probe.py`，默认failfast。文档整理时使用绝对Python路径加 `-B`，显式传入 `GuidanceContractTests ProbeHarnessTests`：前者只读当前仓库验证单入口、兼容指针、关键约束保留与缺文件/损坏frontmatter反例，后者以mock验证停止、链接清理分支和拒绝边界；两者不创建fixture或真实删除。

不带类名的完整运行仍会在项目 `.codex/archive/` 创建临时fixture并尝试逐文件清理，不是只读操作；只读契约及控制逻辑测试不能替代完整文件系统/真实链接回归。不要为了纯文档修改默认运行完整套件。

客户端删除保护或链接权限阻断时立即停止重跑，保留失败日志和隔离残留，不关闭保护、不改用绕过方式。外部测试runner也必须开启failfast；重跑前确认清理授权及执行环境。真实链接测试须独立确认symlink创建结果，不能用被测is_link作为跳过条件；Windows目录symlink使用rmdir，仅删除已核验归属的链接本身并检查残留。控制流测试通过不证明真实链接可用，用例失败不能跳过后宣称全绿。

## 1. 选择最小充分验证

| 改动 | 最低验证 | 不能替代的证据 |
|---|---|---|
| 仅技能/文档 | 格式、链接、GuidanceContractTests、ProbeHarnessTests、只读副作用、diff | 不需启动Unity；不等于完整文件系统工具回归 |
| 改巡检/清理算法 | 上述检查 + 对应文件系统/边界测试（先确认副作用与授权） | 保护阻断时记录未验证，不以mock替代真实链接测试 |
| 纯规则/State/事务/保存 | Unity 编译 + 相关 EditMode + Console | 保存事务需隔离存档测试，不能只检查编译 |
| 定义/经济/生产/研究 | 修改前后 closure + 模拟器诊断 + 定义/source-sink + EditMode + 相关真实运行 | closure 不证明可玩性；人工 fixture 不是真实 Unity trace |
| UI/场景/手势/生命周期 | Unity 编译 + 相关 EditMode + 非零 PlayMode + 实际交互日志 | 正 overflow、内容移动、P40 横屏设备/模拟器检查不能由静态配置替代 |
| Android 发布 | 独立授权、构建配置检查、真实构建与设备验证 | 开发程序集通过不等于 Android 构建通过 |

## 2. 现有脚本参数（PowerShell）

### 定义闭包

```powershell
& "./tools/codex/content-closure-check.ps1" -ProjectRoot "." -ReportPath "./data/content-closure-static.md"
```

- 参数是 `ProjectRoot`，不是 `ProjectPath`；默认写报告，**并非只读**。
- 完整性错误退出 3，不可达退出 2；检查范围以当前实现为准。
- `-IndustrialBaseline` 预置早期完成状态，不得冒充从新游戏开始的闭包。
- 改经济定义前运行；缺运行条件就记录阻断，不跳过门槛宣称通过。

### Unity 编译

`tools/codex/compile-unity.ps1` 参数：`ProjectPath`、`UnityPath`、`LogPath`、`TimeoutSeconds`（默认180）。会打开批处理 Unity、导入/编译并写缓存和日志；先检查项目是否正在由用户的 Editor 占用。当前脚本缺少与测试包装器相同的占用检查，且缺日志可能被视为空文本；必须独立确认退出码和真实新日志。

### Unity 测试

```powershell
& "./tools/codex/run-unity-tests.ps1" -Platform EditMode -ProjectPath "."
```

真实参数：`Platform`（EditMode/PlayMode）、`ProjectPath`、`UnityPath`、`ResultsPath`、`LogPath`、`LatestErrorsPath`、`TimeoutSeconds`（默认600）。**没有 TestFilter 参数**。

副作用：删除指定旧 XML/log，覆盖 Latest 摘要，运行 Unity 并写入缓存；超时终止自己启动的进程。选择明确的本轮输出路径并保留旧证据，不向此脚本传个人目录。包装器有“零用例时先写 Passed 后退出失败”的顺序问题；存在 XML 时也不能只信摘要而忽略 Unity 退出码。

需过滤时使用已定位的 Unity CLI `-runTests -batchmode -projectPath ... -testPlatform ... -testFilter ... -testResults ... -logFile ...`，不要将未知参数塞给包装器。测试命令实际路径由环境检查得到，不猜 Unity 安装路径；不擅自关闭占用项目的 Editor。

### 开发程序集

```text
dotnet build Kingdom.Runtime.Developer.csproj --no-restore
dotnet build Kingdom.Editor.Developer.csproj --no-restore
```

这两个 Build 调用 `tools/codex/build-developer-assembly.ps1`，使用 Unity Bee rsp/编译器并写 `Temp/DeveloperBuild`，不是 Unity 测试。脚本参数：`Assembly`（必填 Runtime/Editor）、`Configuration`（Debug/Release）、`RuntimeFlavor`（Player/Editor）。缺依赖时报告缺口，不自动 restore/install。

### 确定性模拟器

```text
dotnet run --project tools/NewEconomySimulator/NewEconomySimulator.csproj --no-restore -- --json
```

实际为 net9.0 可执行程序，`Program.cs` 运行 `ValidationSuite.RunCore()`，只解析 `--json` / `--csv`；不要杜撰 snapshot 输入参数。构建会产生 bin/obj；程序报告输出 stdout。当前“Unity 对比”含人工 fixture，必须与真实运行采集明确区分。永不以该结果调节数值或声称节奏验收。

## 3. 测试定位与隔离

- 内容/source-sink：`Assets/Tests/Editor/ContentProgressionValidatorTests.cs`、`C6IndustrialClosureAuditTests.cs`、`GlobalEconomyDefinitionTests.cs`。
- 成本/生产：`BuildingCostGrowthTests.cs`、`FlowEfficiencyTests.cs`、`FoodEfficiencyTests.cs`。
- 原子支付：`ResearchPaymentAutoTests.cs`、`KingdomLogicTests.cs`。
- 保存/星区：`SaveArchivePressureTests.cs`、`SectorBuildingTests.cs`、`SectorManagerTests.cs`。
- 确定性/预算：`SimulationDeterminismTests.cs`、`SimulationBudgetTests.cs`。
- UI 静态与生命周期：`P40UiConfigurationTests.cs`、`KingdomUiLifecycleTests.cs`。
- 研究树 PlayMode：`KingdomPlayModeTests.ResearchTree_RuntimeLayoutAndOverflow_AreLoggedAndNonOverlapping`。
- 滚动/教程/星区/ticker：`Assets/Tests/PlayMode/` 下同主题测试类。

先检查 fixture 是否会加载 SampleScene、销毁 Manager 或写默认存档。使用现有公开接口 `SaveManager.SetSaveRootOverrideForTests`，在 Bootstrap/场景启动前设置隔离目录；在所有可能触发自动保存的对象销毁以后，再 `ClearSaveRootOverrideForTests`。不要修改或清空用户真实存档。默认 fixture 没有隔离时先报告风险并补获准的隔离方案，不把完整 PlayMode 当成无副作用命令。

新增测试采用 `SimulationManager.ManualTick`、Manager 公开 API 或 DTO；禁止沿用历史反射。复现缺陷的测试应在旧实现失败，在修复后通过；不得只弱化断言。

## 4. 高副作用工具：不默认运行

- `apply-definition-ids.ps1`、`reserialize-definitions.ps1`：资产/序列化变更。
- `sync-solution.ps1`：生成/修改工程文件。
- `build-android.ps1` / `Assets/Editor/KingdomBuild.cs`：构建产物及构建设置变更。
- 聚合 gate、UI audit、YAML audit：可能写报告、调用其他脚本；逐项检查退出码，不能仅信最后一条成功。
- `analyze-unity-log.ps1`：某些日志被过滤，不代替原始 Console 检查。

## 5. 结果记录

记录命令、运行时间、输入指纹/commit、工作区 dirty 状态、退出码、XML/log 路径、项目用例数、通过/失败/跳过数。排除第三方示例测试对项目通过数的污染。跳过关键交互就是未覆盖，不是验证成功。

没有本轮真实 Unity 调用时写：`未执行真实 Unity 编译。` 不引用旧 run 的通过数作为本轮结论。
