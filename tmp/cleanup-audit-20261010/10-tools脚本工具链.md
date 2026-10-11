# 10 · tools/codex 脚本工具链审查

> 只读盘点。事实基准＝当前磁盘文件（`git status tools/codex/` 为空，磁盘与 HEAD 一致）。
> 已读 `00-审查准则.md`。范围不含 `tools/NewEconomySimulator/`。

## 1. 范围

实际审查 `tools/codex/` 下 **28 个脚本**（22 个 `.ps1` + 6 个 `.py`），全部逐行读完：

- PowerShell(22)：analyze-unity-log、apply-definition-ids、audit-ui、build-android、build-developer-assembly、building-resource-flow-check、compile-developer-tests、compile-unity、content-closure-check、find-unity、inspect-kingdom、reserialize-definitions、run-unity-tests、sync-solution、validate-android-settings、validate-guidance、validate-performance-fix、validate-todolist-gates、validate-ui-contract、validate-unity-perf-log、verify-yaml-references、wait-for-unity-perf
- Python(6)：audit-upgrade-continuity、check-building-sustainability、compile-developer-tests、kingdom_project_probe、test_kingdom_project_probe、test_validation_tools

引用面检索：全仓（排除 `Library/`）grep 脚本名 + `tools/codex`；重点核对 `AGENTS.md`、`.agents/skills/**`、`.codex/handoffs/**`、`docs/**`、`ToDoList.txt`、根 `*.csproj`、`*.sln`、`tools/README.md`。另按脚本内硬编码路径逐一 `Test-Path` 核对。

**关键背景确认**：Unity 装于 `D:/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe`（`find-unity.ps1` 能解析到）；项目版本 `2022.3.62f3c1`。凡带 `-batchmode -executeMethod`/`-runTests` 的脚本"本机不可用"，但**不等于脚本失效**，两类分开记。

---

## 2. 结论清单

### 2.1 建议归档 / 删除

- [置信度 High] `tools/codex/validate-performance-fix.ps1` — 维度 D1+D2+D3；证据：脚本第 90 行 `Assert-Contains "Kingdom.Runtime.csproj"`（该文件不存在，根目录只有 `Kingdom.Runtime.Developer.csproj`，且 `*.csproj` 被 `.gitignore:54` 忽略，属 Unity 按需生成、当前缺失）；第 256 行 `Get-ChildItem (Join-Path $ProjectPath "Assets/Resources/Musics")`（该目录已删：`git log --diff-filter=D -- Assets/Resources/Musics` 命中 `b9acf27`、`6cc7f79`，音乐现位于 `Assets/Musics` 与 `Assets/AddressableAssets/Music`）；第 271 行 `dotnet build Kingdom.sln`（不存在，仅 `Kingdom.Developer.sln`）。全仓（排除自身）**零引用**。最后提交 `22de990 2026-08-23 "Before free"`，是一次性"性能修复"断言快照。建议：归档（`outputs/` 或 `.codex/archive/`）；理由：三处硬路径均已失效、无任何流程引用，保留只会造成"看似有验证脚本"的误导。
- [置信度 Med] `tools/codex/wait-for-unity-perf.ps1` — 维度 D2；证据：全仓零引用；仅依赖 `validate-unity-perf-log.ps1`，而后者也无人调用；需要一次真实 Unity 会话写 `Temp/KingdomPerf.log`（本机批处理不可用）。建议：与 `validate-unity-perf-log.ps1` 成对归档；理由：性能日志校验流程无任何文档/技能入口，属一次性 perf 轮次残留。
- [置信度 Med] `tools/codex/validate-unity-perf-log.ps1` — 维度 D2；证据：唯一引用来自 `wait-for-unity-perf.ps1:8`（该脚本本身零引用）；解析 `[KingdomPerf] SessionStart/ManualTickStats/FrameStats/Memory` 标记。建议：随 `wait-for-unity-perf.ps1` 一并归档；理由：无独立调用点，其判定口径也已被 `analyze-unity-log.ps1` 覆盖（后者仍在 `validation.md:143` 被登记）。
- [置信度 Low] `tools/codex/audit-upgrade-continuity.py` — 维度 D2+D3；证据：全仓唯一提及是 `.codex/handoffs/2026-10-09-repo-cleanup-handoff.md:18`，原文即写"升级连续性临时审计脚本由 `tools/codex/audit-upgrade-continuity.py` 覆盖，且**无当前引用**"。建议：归档；理由：作者已自认无调用点的一次性审计。

### 2.2 建议更新

- [置信度 High] `tools/codex/kingdom_project_probe.py:17` — 维度 D1；证据：`SCAN_ROOTS = (..., ".codex/handoffs", "CONTENTADVISE")`，而 `CONTENTADVISE/` 已在 HEAD `a85f0e7` 删除（`Test-Path CONTENTADVISE` 为 MISS）。运行时它只落一条 `skipped: "missing optional scope"`，不报错，但属陈旧白名单项。建议：更新（移出该 scope）；理由：白名单应只列当前存在范围，避免"可选范围永久缺失"噪声。
- [置信度 Med] `.agents/skills/kingdom-project-dev/references/validation.md:56` — 维度 D1(文档↔脚本漂移)；证据：文档写"**没有 TestFilter 参数**"，但 `run-unity-tests.ps1:9` 已声明 `[string]$TestFilter`，`:69-71` 实际拼入 `-testFilter`。建议：更新文档；理由：脚本已演进，文档未同步，会误导调用者。
- [置信度 Med] `.agents/skills/kingdom-project-dev/references/validation.md:58` — 维度 D6/D1；证据：文档称包装器存在"零用例时先写 Passed 后退出失败""存在 XML 时忽略 Unity 退出码"两处缺陷；但当前 `run-unity-tests.ps1:140-151` 已对非零退出码、零用例分别 `exit 1`，且 `test_validation_tools.py:91-99` 已用反例覆盖这些路径。建议：更新文档为"已修复（附测试）"；理由：缺陷描述已过时，与 `test_validation_tools.py` 的证据冲突。

### 2.3 建议合并（功能重复，标注权威）

- [置信度 Med] `tools/codex/compile-developer-tests.ps1` 与 `tools/codex/compile-developer-tests.py` — 维度 D8；证据：`.py:4` 自述"Python twin of …ps1. Same filtering, same outputs, same pass/fail semantics"；两者目标/过滤/输出目录（`Temp/DeveloperTests`）一致。权威：**`.py` 为受限 shell 下的可用入口**（`validation.md:85` 说明 PS 无法 spawn 原生进程，`.py` 能）。差异点：`.py` 编译前删除旧 DLL/refDLL（`:153-155`），`.ps1` 不删。建议：保留双份但明确 `.py` 为默认，`.ps1` 仅供 csproj `Exec` 链使用；理由：二者职责重叠，需在一处声明主次避免"哪份是权威"混淆。
- [置信度 Low] `tools/codex/audit-ui.ps1` 与 `tools/codex/inspect-kingdom.ps1` — 维度 D8；证据：二者均为"正则扫描 `Assets/Resources/Script/**/*.cs` 并落 `Logs/codex-*.txt`"的报告生成器；`inspect-kingdom` 扫描全脚本目录（10 组模式），`audit-ui` 仅扫 `\\(UI|Resource|Building|Research|Setting)\\`（8 组模式），模式集高度重叠（Off-screen hiding / Scene searches 等）。权威：`inspect-kingdom.ps1` 覆盖更广。建议：合并或让 `audit-ui` 降级为 `inspect-kingdom` 的一个 `-Scope UI` 模式；理由：两个近义静态扫描器长期并存，输出格式一致。
- [置信度 Low] `tools/codex/content-closure-check.ps1` / `check-building-sustainability.py` / `audit-upgrade-continuity.py` — 维度 D8；证据：三者都解析 `Assets/Resources/Datas/**/*.asset` 做静态内容审计；`check-building-sustainability.py:5` 自述是 `content-closure-check.ps1` 的"companion"，回答"可达性之外的维持条件"；`audit-upgrade-continuity.py` 回答"升级链净产出"。权威：`content-closure-check.ps1`（唯一被 `kingdom-economy-simulation/SKILL.md:24`、`validation.md:38`、`validate-todolist-gates.ps1:20` 登记）。建议：保留三者（问题不同），但在技能里显式声明 `content-closure` 为唯一权威、另两者为一次性补充；理由：避免后续维护者误以为三个都是常规门槛。

### 2.4 保留观察

- [置信度 High] `tools/codex/run-unity-tests.ps1` — 维度 保留；证据：`validation.md:53` 登记为测试入口；`test_validation_tools.py:84` 直接测试它；本机因 `-batchmode`（`:61`）不可用。建议：保留（本机不可用≠失效）；理由：别处环境仍是唯一能跑测试的包装器。
- [置信度 High] `tools/codex/compile-unity.ps1` — 保留；证据：`validation.md:48` 登记；`:14-20` 用 `-batchmode`；本机不可用。建议：保留；理由：同上。
- [置信度 High] `tools/codex/build-android.ps1` — 保留；证据：`:38-46` `-batchmode -executeMethod KingdomBuild.BuildAndroid`（`Assets/Editor/KingdomBuild.cs:12` 存在该方法），本机不可用；同时先调 `validate-android-settings/ui-contract/verify-yaml`（`:30-32`）。建议：保留；理由：Android 构建唯一入口。
- [置信度 High] `tools/codex/build-developer-assembly.ps1` — 保留；证据：被 `Kingdom.Runtime.Developer.csproj:19`、`Kingdom.Editor.Developer.csproj:23`、`Kingdom.DeveloperTests.csproj:39` 三处 `Exec` 调用；**不启动 Unity**（复用 `Library/Bee/artifacts` 的 rsp + Unity Roslyn），本机可用。建议：保留；理由：开发程序集编译门核心。
- [置信度 High] `tools/codex/compile-developer-tests.ps1` — 保留；证据：`Kingdom.DeveloperTests.csproj:40` `Exec` 调用；`validation.md:81-83` 登记。建议：保留。
- [置信度 High] `tools/codex/compile-developer-tests.py` — 保留；证据：`validation.md:85` 与 `ToDoList.txt:24` 均登记（后者记录其汇总口径缺陷，见 3.）。建议：保留（与 `.ps1` 成对）。
- [置信度 High] `tools/codex/apply-definition-ids.ps1` — 保留；证据：`docs/architecture/definition-database.md:51` 命令行等价入口；`validation.md:139`；调用 `Kingdom.EditorTools.DefinitionIdMigration.ApplyFromCommandLine`（`Assets/Editor/Migration/DefinitionIdMigration.cs:18` 存在）。本机不可用（批处理）。建议：保留。
- [置信度 High] `tools/codex/reserialize-definitions.ps1` — 保留；证据：`validation.md:139`；方法 `DefinitionIdMigration.ReserializeFromCommandLine`（同文件 `:20` 存在）。建议：保留。
- [置信度 High] `tools/codex/sync-solution.ps1` — 保留；证据：`validation.md:140`；方法 `Kingdom.EditorTools.SolutionSync.Run`（`Assets/Editor/Codex/SolutionSync.cs:14` 存在）。建议：保留。
- [置信度 High] `tools/codex/find-unity.ps1` — 保留；证据：被 `apply-definition-ids:8`、`reserialize-definitions:8`、`sync-solution:8`、`compile-unity:9`、`build-android:10`、`run-unity-tests:14`、`compile-developer-tests.ps1:61` 引用；`validate-guidance.ps1:43` 断言其存在；实测解析到本机 Unity。建议：保留。
- [置信度 High] `tools/codex/content-closure-check.ps1` — 保留；证据：`kingdom-economy-simulation/SKILL.md:24`、`validation.md:38`、`validate-todolist-gates.ps1:20`、`test_validation_tools.py:129`。建议：保留（唯一权威闭包工具）。
- [置信度 High] `tools/codex/validate-guidance.ps1` — 保留；证据：`validation.md:73` 登记；`test_kingdom_project_probe.py:253-264` 直接解析并校验其必需文件清单；实测其列出的全部路径均存在。建议：保留。
- [置信度 High] `tools/codex/validate-ui-contract.ps1` — 保留；证据：`validation.md:73`、`validate-todolist-gates.ps1:17`、`build-android.ps1:31`、`test_validation_tools.py:127`；引用 `Assets/Resources/UI/Kingdom/KingdomUIRoot.prefab`、`ProjectSettings/EditorBuildSettings.asset` 均存在。建议：保留。
- [置信度 High] `tools/codex/validate-android-settings.ps1` — 保留；证据：`validate-todolist-gates.ps1:18`、`build-android.ps1:30`、`test_validation_tools.py:128`；实测 `ProjectSettings.asset` 中 `bundleVersion: 0.5`(:144)、`AndroidMinSdkVersion: 22`(:174)、`AndroidTargetSdkVersion: 35`(:175)、`AndroidTargetArchitectures: 2`(:266)、`scriptingBackend.Android: 1`(:675) 全部命中。建议：保留。
- [置信度 High] `tools/codex/verify-yaml-references.ps1` — 保留；证据：`validate-todolist-gates.ps1:19`、`build-android.ps1:32`、`test_validation_tools.py:128`。建议：保留。
- [置信度 High] `tools/codex/building-resource-flow-check.ps1` — 保留；证据：`validate-todolist-gates.ps1:21`、`test_validation_tools.py:129`、`docs/testing/acceptance-checklist.md:13` 显式登记其职责；引用 `Assets/Resources/Datas/Building` 存在。建议：保留。
- [置信度 High] `tools/codex/validate-todolist-gates.ps1` — 保留；证据：`test_validation_tools.py:146` 直接测试其子脚本退出码传播与自定义根传递。建议：保留（静态门聚合器）。
- [置信度 High] `tools/codex/test_validation_tools.py` — 保留；证据：`validation.md:58` 描述的缺陷正是其反例覆盖目标；测试 `run-unity-tests.ps1`/`compile-unity.ps1`/`validate-todolist-gates.ps1`。建议：保留。
- [置信度 High] `tools/codex/kingdom_project_probe.py` — 保留；证据：`SKILL.md:17`、`validation.md:7-19` 登记为**可选只读**盘点工具；实现确实只 stdout、不写文件、拒绝 UNC/符号链接。建议：保留（除 2.2 的 `CONTENTADVISE` 项）。
- [置信度 High] `tools/codex/test_kingdom_project_probe.py` — 保留；证据：`SKILL.md` 未列但 `validation.md:17` 与 `acceptance-cases.md:34` 登记；fixture 只建在 `.codex/archive`（排除区）并校验清理。建议：保留。
- [置信度 High] `tools/codex/analyze-unity-log.ps1` — 保留；证据：`validation.md:143` 登记为日志诊断工具（并警示"某些日志被过滤，不代替 Console"）。建议：保留。
- [置信度 Low] `tools/codex/audit-ui.ps1` — 保留观察；证据：仅 `validate-guidance.ps1:47` 断言其存在，无任何技能/文档把它列入流程；`$root = Assets/Resources/Script` 存在，脚本可运行（仅扫 UI 子目录）。建议：保留观察（先并入 `inspect-kingdom` 再谈归档，见 2.3）。
- [置信度 Low] `tools/codex/inspect-kingdom.ps1` — 保留观察；证据：仅 `validate-guidance.ps1:46` 断言其存在；可运行。建议：保留观察（作为 `audit-ui` 的合并去向）。
- [置信度 Med] `tools/codex/check-building-sustainability.py` — 保留观察；证据：被 `outputs/Kingdom-建筑可持续性静态检查-2026-09-20.md:12`（历史报告）引用；`.codex/archive` 内多份文档引用；`validation.md`/主技能**未**登记。建议：保留观察（若 `audit-upgrade-continuity.py` 归档，可一并评估）。

---

## 3. 不确定项（需用户决策）

- **性能日志三件套的去留**：`analyze-unity-log.ps1` 仍被 `validation.md:143` 登记为常规诊断工具，但 `validate-unity-perf-log.ps1` + `wait-for-unity-perf.ps1` 无任何流程引用，且判定口径与前者重叠。源码侧 `[KingdomPerf]` 插桩仍在（`BuildingManager.cs`、`KingdomUIRoot*.cs` 等 11 个文件 + `Assets/Editor/KingdomPerfTestTools.cs`）。是否整体退出"性能专项"工具集，需用户确认。
- **`Kingdom.Runtime.csproj` 是"生成物"还是"项目契约"**：它被 `.gitignore` 忽略、当前缺失，但 `validate-performance-fix.ps1` 把它当输入断言。若项目实际约定 Unity 编辑器会生成该文件，则相关断言并非笔误而是"要求先开 Unity"；请确认是否保留这条依赖。
- **`compile-developer-tests.py` 汇总口径缺陷是否为待修项**：`ToDoList.txt:24` 与 `validation.md:85` 均记录"Python 汇总仅由产物存在构成 `all_ok`，未把 `compiler_exit` 合入判断"。核对当前实现（`:253-257` `all_ok = runtime_ok and all(ok ...)`，`ok` 仅为 `produced`）**该描述仍准确**，即缺陷未修。属"如实记录的已知开放项"，非陈旧 TODO，但需要用户决定本轮是否修。
- **`audit-upgrade-continuity.py` / `check-building-sustainability.py` 是否仍需要**：二者是 `content-closure-check.ps1` 的领域补充，但无常规入口；归档会否丢失"维持条件/升级链净产出"的静态口径，需经济技能负责人确认。

---

## 4. 未覆盖项

- `tools/NewEconomySimulator/`（另一子代理范围），但 `validate-guidance.ps1:25-28` 依赖其 4 个文件存在——已 `Test-Path` 确认均存在，未深入审计其内容。
- `tools/content-dependency/`：`tools/README.md:13-15` 已声明"retired and empty"，不属本次 28 个脚本范围，未审计。
- 脚本运行时行为：按准则**未执行**任何 Unity/测试/build；`compile-*`、`run-unity-tests`、`build-android`、`apply-definition-ids`、`reserialize-definitions`、`sync-solution` 的批处理路径仅做静态核对（方法名、路径存在性），未实际运行。
- `.meta`/`.csproj` 生成物与 `Library/`、`Temp/`、`Logs/`：按准则不审计。
