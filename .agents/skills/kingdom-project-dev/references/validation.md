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
| UI/场景/手势/生命周期 | Unity 编译 + 相关 EditMode + 非零 PlayMode + 实际交互日志 | 设备体验由用户自行验收，不列入代理交付门槛 |

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

- **产物 dll 时间戳不刷新 ≠ 构建失败**：脚本用 `/deterministic`，源文件未变时 csc 跳过写盘。判定失败须看 `MSB3073` / `生成失败` / 退出码；要确认写盘可先 `touch` 一个源文件（记得 `git checkout` 还原）。
- **`ProgramFiles` 在本机沙箱未定义**（`ProgramFiles(x86)`、`ProgramW6432` 同样是）。任何脚本对这类变量不判空就 `Join-Path` / `Test-Path -LiteralPath` 会抛 `ParameterBindingValidationException`，且脚本内的 `exit $LASTEXITCODE` 会把包装层直接带崩。此类故障**只在 Unity Editor 未运行时暴露**（列表首项来自 `Get-Process -Name Unity`，运行时会掩盖异常）。已修 `build-developer-assembly.ps1:56-72`；新增脚本勿再重复此模式。
- **脚本输出落盘要合并全部流**：`& script.ps1 | Out-File` 只接 stdout，脚本的 `Write-Host`（信息流）会丢成空文件；写成 `& script.ps1 *>&1 | Out-File -Encoding utf8` 才能完整捕获（实测 `validate-ui-contract.ps1` / `validate-guidance.ps1` 均正常落盘）。Bash 侧调 `powershell.exe` 会被安全策略直接拒绝，必须走 PowerShell 工具。

### 测试程序集编译门

```text
dotnet build Kingdom.DeveloperTests.csproj --no-restore
```

`build-developer-assembly.ps1` 主动剥离 `Assets/Tests/*`、nunit 与 TestRunner 引用，因此测试程序集使用独立编译门。该 csproj 驱动 `tools/codex/compile-developer-tests.ps1`：复用 Unity 已生成的 Bee rsp，保留测试源与测试框架引用，把过期的 `Kingdom.Runtime.ref.dll` 换成当前源码构建的 `Temp/DeveloperBuild/Kingdom.Runtime.Editor.dll`；**只编译，不运行任何测试**。

脚本参数：`Target`（All/Editor/PlayMode，默认 All）、`Configuration`（Debug/Release）、`ProjectPath`、`UnityPath`。csproj 的 `Build` 目标先 `Exec` 一次 `build-developer-assembly.ps1 -Assembly Editor`，再跑本脚本，所以单独 build 该 csproj 即自足，且比对的是刚刚生成的 Runtime 程序集而非陈旧产物；`Kingdom.Developer.sln` 也已登记，`dotnet build Kingdom.Developer.sln` 一并覆盖。Unity 定位复用 `find-unity.ps1`；rsp 按文件名在 `Library/Bee/artifacts` 下递归取最新，不写死会变的 dag 哈希目录名。

- **Python与PowerShell的产物检查不同**：`compile-developer-tests.py`在每次编译前删除目标DLL及refDLL，再检查新DLL存在；`compile-developer-tests.ps1`不先删除旧产物，只检查DLL存在、编译器确实运行及退出码。不能把两者都描述成保证新产物。Python汇总当前仅由产物存在构成`all_ok`，虽然记录了`compiler_exit`，却没有将退出码合入汇总判断；使用该入口仍须独立核对每项编译器退出码和新产物，不将脚本退出0单独作为通过证据。此处记录现有实现，未修改编译工具。

- **legacy csproj 的 `ProjectReference` 在直接 build 时不生效**：这些项目只导入 `Kingdom.Developer.References.props`，未导入 `Microsoft.Common.targets`，`ResolveProjectReferences` 不存在，构建顺序实际来自 `.sln` 的 `ProjectDependencies`。实测只 build `Kingdom.DeveloperTests.csproj` 时，引用的 Runtime/Editor 项目不会被构建（产物时间戳不动）。需要顺序保证就写进 `Build` 目标的 `Exec` 链，不要指望 `ProjectReference`。

写 `Temp/DeveloperTests`（`Temp/` 已在 `.gitignore` 内）：每目标一份 `<Assembly>.compiler-output.txt`，加一份 `summary.txt`；不改动任何跟踪源码。退出码非 0 即门失败。

- 覆盖范围仅“测试代码能否针对当前 Runtime 编译”，**不能替代 Unity EditMode/PlayMode 结果**，也不证明用例会通过。
- 前置条件是 Bee 元数据存在且 `Temp/DeveloperBuild/Kingdom.Runtime.Editor.dll` 已生成；缺任一项**脚本**直接抛错（csproj 的第一条 `Exec` 只覆盖 Editor 侧前置构建，不自动 restore、不安装依赖）。
- summary 的 `sources_added_beyond_bee_snapshot` 非 0 表示 rsp 快照落后于工作树，脚本已按 `Assets/Tests/Editor|PlayMode` 目录补入并列名；长期非 0 需回 Unity 重新导入。
- 测试编译进独立程序集，`internal` 成员在 Unity 生成的 `.ref.dll` 中被整条剥离，且项目内没有 `InternalsVisibleTo`，跨程序集不可见。测试要调用的成员必须是 `public`；此门首次运行即抓出过一处此类真实缺陷。

### 离线状态沙箱（纯托管 Runtime 类型）

确需诊断**不调用Unity原生API的纯托管类型**时，可引用当前源码构建的`Temp/DeveloperBuild/Kingdom.Runtime.Editor.dll`在普通.NET宿主执行。不得沿用旧环境的批处理卡死结论来跳过当前真实Unity验证。

**适用边界**：

- 只选择经依赖核对无需Unity原生API的状态与数学路径；不能只凭`using System`判断间接依赖也可执行。
- `ScriptableObject.CreateInstance<T>()`及MonoBehaviour生命周期需要Unity宿主，不能在普通.NET沙箱中代替真实Manager/资产测试。

Unity定制测试程序集不保证能在普通.NET宿主使用。若只复现原测试的断言序列，输出标记`[project-reproduced]`，与自写`[smoke]`隔离；**复现不得表述为原测试通过**，不得为此未经授权安装另一测试框架。

`ExpantaNum`有字符串双向隐式转换；拼接诊断消息时显式`.ToString()`，避免字符串被数值加法重载解析。

沙箱只放未跟踪临时目录，不依赖某份历史scratch实现；其结果**不能替代**Unity EditMode/PlayMode。

### 确定性模拟器

```text
dotnet run --project tools/NewEconomySimulator/NewEconomySimulator.csproj --no-restore -- --json
```

- **本机沙箱需先补系统目录环境变量**：`APPDATA` / `ProgramData` / `ProgramFiles(x86)` / `ProgramW6432` 未定义时，NuGet `GetRestoreSettingsTask` 会抛 `Value cannot be null. (Parameter 'path1')`（restore 与 `--no-restore` 均失败，与项目内容无关）。可先 `env APPDATA='C:\Users\<user>\AppData\Roaming' ProgramData='C:\ProgramData' 'ProgramFiles(x86)=C:\Program Files (x86)' 'ProgramW6432=C:\Program Files' dotnet build`，再直接运行 `dotnet bin/Debug/net9.0/NewEconomySimulator.dll`。
- csproj 只链接 `Assets/Resources/Script/Math/` 下的 `ExpantaNum.cs` 与 `ExpantaNumExtensions.cs`。改动这两个文件必须重跑模拟器自测（退出码 0 且首行 `Passed: True`）；它**不覆盖** Manager / Runtime State 层改动。

实际为 net9.0 可执行程序，`Program.cs` 运行 `ValidationSuite.RunCore()`，只解析 `--json` / `--csv`；不要杜撰 snapshot 输入参数。构建会产生 bin/obj；程序报告输出 stdout。当前“Unity 对比”含人工 fixture，必须与真实运行采集明确区分。永不以该结果调节数值或声称节奏验收。

## 3. 测试定位与隔离

- 内容/source-sink：`Assets/Tests/Editor/ContentProgressionValidatorTests.cs`、`C6IndustrialClosureAuditTests.cs`、`GlobalEconomyDefinitionTests.cs`。
- 成本/生产：`BuildingCostGrowthTests.cs`、`FlowEfficiencyTests.cs`、`FoodEfficiencyTests.cs`。
- 原子支付：`ResearchPaymentAutoTests.cs`、`KingdomLogicTests.cs`。
- 保存/星区：`SaveArchivePressureTests.cs`、`SectorBuildingTests.cs`、`SectorManagerTests.cs`。
- 确定性/预算：`SimulationDeterminismTests.cs`、`SimulationBudgetTests.cs`。
- UI 静态与生命周期：UI 配置测试、`KingdomUiLifecycleTests.cs`。
- 研究树 PlayMode：`KingdomPlayModeTests.ResearchTree_RuntimeLayoutAndOverflow_AreLoggedAndNonOverlapping`。
- 滚动/教程/星区/ticker：`Assets/Tests/PlayMode/` 下同主题测试类。

先检查 fixture 是否会加载 SampleScene、销毁 Manager 或写默认存档。使用现有公开接口 `SaveManager.SetSaveRootOverrideForTests`，在 Bootstrap/场景启动前设置隔离目录；在所有可能触发自动保存的对象销毁以后，再 `ClearSaveRootOverrideForTests`。不要修改或清空用户真实存档。默认 fixture 没有隔离时先报告风险并补获准的隔离方案，不把完整 PlayMode 当成无副作用命令。

新增测试采用 `SimulationManager.ManualTick`、Manager 公开 API 或 DTO；禁止沿用历史反射。复现缺陷的测试应在旧实现失败，在修复后通过；不得只弱化断言。

## 4. 高副作用工具：不默认运行

- `apply-definition-ids.ps1`、`reserialize-definitions.ps1`：资产/序列化变更。
- `sync-solution.ps1`：生成/修改工程文件。
- `Assets/Editor/KingdomBuild.cs`：构建产物及构建设置变更。
- 聚合 gate、UI audit、YAML audit：可能写报告、调用其他脚本；逐项检查退出码，不能仅信最后一条成功。
- `analyze-unity-log.ps1`：某些日志被过滤，不代替原始 Console 检查。

## 5. 结果记录

记录命令、运行时间、输入指纹/commit、工作区 dirty 状态、退出码、XML/log 路径、项目用例数、通过/失败/跳过数。排除第三方示例测试对项目通过数的污染。跳过关键交互就是未覆盖，不是验证成功。

没有本轮真实 Unity 调用时写：`未执行真实 Unity 编译。` 不引用旧 run 的通过数作为本轮结论。
