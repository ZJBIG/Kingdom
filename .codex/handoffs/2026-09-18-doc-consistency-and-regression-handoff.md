# 2026-09-18 第三轮修缮交接：文案一致性、回归补强、D10 驳回与构建脚本修复

## 2026-10-07 outputs 阶段上传与 C1 六态续接

- 用户授权每阶段验证后上传 GitHub，授权游戏实现依赖基线，并要求最终合并 main；E 插画/G 遗迹暂缓，F 工坊收益/本星系目标/离线摘要获准实施。
- A 工具测试补强：移除替身的策略绕过，补 XML 缺/负/非数字计数与聚合 gate 自定义根/失败传播，`python -B -m unittest tools.codex.test_validation_tools -v` 3/3、退出 0；临时目录实际清理。提交 `787581c` 已核对远端分支一致。
- C1 新增真实三条材料链的六态、原子失败、真实支付、v9 Apply、逐资源/Food/领土/生产力与综合工厂扩产。首轮 r3 XML 12/9/3/0（总/过/失败/跳过）、exit 2；三例失败于 Workshop 夹具未初始化。修正为现有公开 CaptureSaveData 的索引初始化入口，不改玩法或资产。
- 修正后只跑六态，`TestResults/outputs-C1-six-states-r4-20261007.xml` / `Logs/outputs-C1-six-states-r4-20261007.log`，3/3、0 failed/skipped、Unity exit 0。原九项组合/Food 已在 r3 实际通过，保留两份独立结果。
- DeveloperTests 构建 0 errors/0 warnings；当前静态闭包达到 Ultra，Research 15/15、Workshop 3/3、Building 3/3；72 建筑、40资源、OpposingRawResourceFlows=0；确定性诊断总体 passed=true，仅 fixture 回归。
- 暂存的依赖基线保留既有实现，排除字体/临时目录/个人记忆/旧文档清理；Unity 序列化空值行末既有空白不另行改资产，源码和工具 diff 空白检查通过。
- Git 直连 TLS 曾失败，使用本机已配置系统代理（仅命令参数，证书校验保留）；提交作者使用最近本仓库作者，仅命令参数，不改全局配置。
- B–D 依赖基线/C1 已上传 `fdcfd3f`。
- F1 工坊详情按当前数量/效率/幸福加成预览购买前后生产与持续投入，复用真实叠加效果、不改权威状态。新增3项规则测试及真实按钮/刷新/扣款/速率 PlayMode；已购、无建筑、零效率覆盖。
- F 验证：真实 Unity 编译 outputs-F-compile-20261007.log exit0；EditMode 首次35/35 XML后原生退出超时，保留失败；分组重跑 outputs-F12-editmode-r2-20261007.xml 30/30 与 outputs-F3-editmode-r2-20261007.xml 5/5，均exit0；最终完整 outputs-F-playmode-r3-20261007.xml 41/41、0失败/跳过、exit0。首轮39/41、第二轮40/41及新增断言格式失败保留。标量比较允许已有1e-6量化的一单位边界，不改数学层。
- F 最终日志仅有 Licensing/Curl 环境噪声，无游戏级 Error/Exception/MissingReference；DeveloperTests 0错误/警告，工具反例3/3，确定性fixture诊断exit0；新meta GUID独占，UI/guidance/YAML引用检查通过。
- F1 已上传 `496b895`；F2 新增10场景、首据点后返回远航、真实Overview导航回归，修复共享导航缺少 Sectors 分支，只打开现有详情、不改解锁。27项Tutorial测试与完整41项PlayMode均通过。
- F2 已上传 `660998a`；F3 SaveManager 实际 AdvanceOffline 前后差异，会话只读Summary，v9不变；Overview复用正文，区分完整离开/截断结算跨度，Food封顶与阻碍只描述结算结束状态。5项EditMode与真实Overview显示/快照/载入清空回归通过，完整PlayMode 41/41。
- F3 已上传 `770e692`；当前 outputs/规则/既有交接/根待办整合提交 `9c13b71` 已上传。outputs正文116个本地链接通过，文本空白检查通过；仅移除12份新输出文档的额外结尾空行，旧PDF及Unity空值序列化行不改格式。
- 2026-10-08 main 已常规快进至 `9c13b71cdbf9f60fb6b79ab9ce2701f69c783896`；远端main/codex均该SHA，fetch后main...origin/main=0/0。包括本地main原有112个未上传祖先+本轮6提交。推送曾返回ref已是目标SHA的并发锁提示，随后ls-remote/fetch确认；无force/rebase/amend。
- 此完成确认仅文档提交并同步两分支。F1–F3已完成，没有新增未验收游戏代码；E插画/G遗迹继续按用户要求暂缓。个人记忆、临时目录、字体缓存、孤立Android.meta保留本地，不操作真实存档或外部体验。后续任务先核对远端与本地分支、保留这批明确排除内容。

## 任务与状态

用户要求「参考目前进展和 outputs 内容往下完成任务，不改代码风格、不碰不相关内容，多子代理并行」。本批为**有界实施 + 只读审计 + 一项驳回 + 一项构建工具链修复**，产物为 `outputs/Kingdom-第三轮修缮-文案一致性与回归补强-2026-09-18.md`。

- 已完成：P1-06 文案修复、P0-03-B 定位定案、P0-04 缺口 A 重启路径回归、悬空引用收口、D10 驳回、**`build-developer-assembly.ps1` 路径缺陷修复（使 Developer 构建恢复可用）**。
- **未完成**：任何 Unity 运行验证（未执行真实 Unity 编译、未跑 EditMode/PlayMode/Console、新增测试从未执行）。
- 不关闭项：P0-01（D09/D12 待运行）、P0-02（待拍板甲/乙案）、P0-04 缺口 B/C（未动）。

## 起始基线与保留

- 基线 HEAD `b14157b`（“重构-step2”），**接手的是一棵干净工作树**（与上一轮“大量未提交改动”不同，本轮开始时 `git status` 为空）。
- 未回滚、未触碰任何既有成果；`.codex/archive/`、`Library/`、`TestResults/` 等均按 `.gitignore` 保持未跟踪。

## 本批实际变更（6 文件）

1. `Assets/Resources/Datas/Research/Spacer/MatterStateControlTheory.asset` — `Description` 由「管理高级材料的装配、服役与回收，降低建设损耗并提高拆除返还。」改为「管理高级材料的装配与服役，降低建设损耗。」
   - 依据：effects 只有 Type 5 value 1.12（=建造成本降低 12%，经 `BuildingManager.GetConstructionCostMultiplier` :1172-1180 的 `1/efficiency` 反算确认），**无 Type 22**；不为保文案加高返还，因 `SetDeconstructionReturnRate` 取 Max 不叠加且 `OrbitalHabitation` 已占位。
2. `Assets/Resources/Datas/Building/Spacer/DeepSpaceRelay.asset` — `Description` 补「本身持续占用物流运力」。
   - 依据：净 −23/s；真正的净供给者是 OrbitalStation(+180)、EarthMoonLogisticsHub(+240)；两处定向加成叠加后 34.5/s 仍 < 48 无法转正；`fleetPowerGranted 100` 佐证舰队支援定位。**未改任何数值。**
3. `Assets/Tests/Editor/KingdomLogicTests.cs` — 新增 `ApplicationPause_SaveFailureThenRestart_ResettlesTheStaleInterval`（+83 行，无反射）。
   - **范围要点**：`ApplicationPause_SaveFailureSettlesOnlyThePauseIntervalOnce`（:462）**早已覆盖“进程存活恢复”**，本轮不重复；只补**重启路径**。触发点 `GameBootstrap.cs:33-34`。
   - **断言鉴别力补强**：初版末断言 `afterRestart > settledAmount` 有**平凡满足**风险 —— `GameManager.InitializeStartingInventory`（`:79-83`）新局固定发 `60` 木材，若任一 `LoadOrCreateGame()` 静默回退成新局，金额照样变大、断言照样通过。已加前置守卫 `settledAmount > NewGameStartingWood`（常量 `new ExpantaNum(60)`，定义在类首），链条变为 `afterRestart > settledAmount > 60`，双重排除"新局重发木材"混淆。断言仍用关系式、不写死结算值（`AdvanceOffline` 走 `SimulationManager.cs:288-332` 分步 tick，精确产出不宜固定）。
   - 注解写明锁定当前行为、不改语义（改语义上一轮已实测驳回）。
4. `ToDoList.txt` / 5. `outputs/ToDoList.txt` — 证据索引 A 的 `.agents/skills/kingdom-content-expansion/SKILL.md`（已删除，悬空）改为 `kingdom-project-dev/SKILL.md（唯一任务路由）`，两份同步。
6. `tools/codex/build-developer-assembly.ps1`（`:56-72`）— **阻断性修复**（见下节）。

## 驳回：D10「引导晚于星区可用时点」

**前提错误。** `HomeSystemSurvey.asset` 的 `prerequisites` 含 `7a2e5c9d4f1b48e6b3c8d0a7f5e2c914`，该 GUID 属 **`DeepSpaceFleet.asset.meta`**。即 `HomeSystemSurvey` **本身以 `DeepSpaceFleet` 为前置**，故当前引导顺序 `HomeSystemSurvey → DeepSpaceFleet → InterstellarNavigation` 恰是真实前置顺序。

局部仍成立的事实（本轮不改，供参考）：本星系星区解锁门槛只需 `HomeSystemSurvey` + `LaunchCenter`（`SectorManager.GetUnlockFailure` :475-491），不需 `InterstellarNavigation`；但这由解锁门槛决定，与由研究前置链决定的引导顺序不矛盾。

**已阻止的错误实施**：曾据此派发“调整 `BuildSpacerGuidance` 顺序”任务，反证发现后在文件被改前叫停；`TutorialManager.cs` 经核实**零改动**。

## 验证执行

- `dotnet build Kingdom.Runtime.Developer.csproj --no-restore` → **0 错误、3 既有警告**；`dotnet build Kingdom.Editor.Developer.csproj --no-restore` → **0 错误、0 警告**。产物 `Temp/DeveloperBuild/*.dll` 已随源文件变更刷新（21:23 首次修复、21:50 加固后）。

### 阻断性修复：`build-developer-assembly.ps1` 的 `ProgramFiles` 空值缺陷

- **根因**：原 `:60-63` 用 `Join-Path ${env:ProgramFiles} "Unity\Hub\Editor\..."`。本机 **`ProgramFiles`/`ProgramFiles(x86)`/`ProgramW6432` 三者均未定义**（`SystemRoot` 正常），`Join-Path` 收到 `null` 的 `Path` 抛 `ParameterBindingValidationException` → 脚本退出码 1 → `dotnet build` 报 `MSB3073`，**Developer 构建整体不可用**。此前记录的“Developer 编译通过”因此**不可复现**，已用真实产物刷新纠正。
- **为何此前没暴露**：候选列表首项来自 `Get-Process -Name Unity`；Unity 运行时列表已能解析出目标，异常被 `Where-Object` 惰性求值时序掩盖。**Unity 未运行**时首个真实候选可用，脚本却因 `ProgramFiles` 为空在同一行抛出。
- **修法**（`:56-72`）：判空后构造（`-not [string]::IsNullOrWhiteSpace(...)`）+ 补 `ProgramFiles(x86)` 回退 + 下游加 `Where-Object { $_ -and $_.Trim() }`。
- **双向对照实验（定论哪一层承重）**：
  | 变体 | Runtime 构建 |
  |---|---|
  | (a) 留判空、**删**筛选器 | **成功**（EXIT 0） |
  | (b) 留筛选器、条件改回**裸 `Join-Path`** | **失败**（`MSB3073`，退出码 1） |
  | 还原最终态（md5 `25ab9a4a6309b0e4f63d872863c5411f`） | **成功** |
  → **判空条件是承重点**，筛选器层单独不够；两层保留，但**不得声称筛选器修了本机问题**（它只兜住"候选列表混入空/空白项"的一般情况）。
- **诊断方法**：逐段把中间值写盘（`Add-Content`）再 Read 读回。因本机 PowerShell 工具**所有命令固定 exit 0 且 stdout 为空**，无法靠回显定位。

- `tools/codex/content-closure-check.ps1`：**退出码 0**，Industrial 81/81、37/37、50/50；Spacer 47/47、46/46、16/16；Ultra 1/1；资源 40；Unreachable 段全空。两处文案改动未影响定义图。
- 研究前置链逐个 GUID 反查、10 个 Spacer 建筑物流字段横向解析。

## 风险与证据限制

- **未执行真实 Unity 编译。** 未运行任何 EditMode/PlayMode/Console；新增测试**从未执行**。
- **新增测试的编译未单独验证**：`Kingdom.Editor.Developer.csproj` 的 `Compile Include` 只覆盖 `Assets/Resources/Script/**` 与 `Assets/Editor/**`，**不含 `Assets/Tests`**；`build-developer-assembly.ps1` 在 Editor 分支亦显式 `continue` 掉 `Assets/Tests/`（`:103`）——即**开发者通道按设计就不覆盖测试程序集**，脚本修复不改变这一点。按 `Assembly-CSharp-Editor.rsp` 单独编译测试源码的路径**被执行保护拒绝**（同上一轮），未绕过。静态自查已确认所用 API 可见性并对齐 `:419-441` 既有模式。
- 两处文案改动的**游戏内显示未在 Unity 查看**。
- 未复算三座 E08 新建筑（仍不在 `Datas`）。
- **产物 dll 时间戳不刷新 ≠ 构建失败**：脚本用 `/deterministic`，源文件未变时 csc 跳过写盘。实测 `touch Assets/Resources/Script/Manager/SaveManager.cs` 后 `Kingdom.Runtime.dll` 立刻刷新（21:52:43），随后已 `git checkout` 还原该文件。**判断构建失败须看 `MSB3073` / `生成失败`，不能只看时间戳**（本轮有子代理据此误判过一次）。
- 环境勘误（沿用）：本机 Python 3.13.12 的 `re` 对「多字符字面量后接 `\s`」会静默失配，改脚本需避开。
- 环境勘误（本轮新增）：本机沙箱中 **`ProgramFiles` 及 `ProgramFiles(x86)`/`ProgramW6432` 均未定义**；**PowerShell 工具所有命令固定返回 exit 0 且 stdout 为空**，定位脚本内部错误须逐段把中间值写盘再用 Read 读回，不能依赖回显。子代理的 bash/PowerShell 输出捕获可能中途完全失效（全空 + exit 0），而主代理同刻仍正常——委派的验证任务失败时先排除工具故障，关键实验由主代理补跑。

## 下一具体行动

1. **交互式** Unity Test Runner（**不要再试批处理**，已**五次**证实卡在 `Application.AssetDatabase Initial Refresh Start`）：先跑本轮新增的重启用例与 `:462` 既有用例，再 `ProgressionMilestoneRecorderTests` → 3 项新档用例 → Overview → 全量 EditMode → 相关非零 PlayMode → Console。重点看跨用例静态残留。
2. P0-02 拍板甲/乙案（甲案仍建议；乙案四项必修项含 CopperWire 阻断项）。
3. 运行可用后做 D04/D11 有界复现与最小修复。
4. P0-04 缺口 B/C 维持现状，除非用户重新授权改语义。
## 2026-09-21 Ultra 扩展续接

- 本轮实施：新增 `Assets/Resources/Datas/Research/Ultra/PhaseFieldEngineering.asset`、`DistributedCognitionProtocol.asset`、`AutonomousMatterAssembly.asset` 及 `Assets/Resources/Datas/Building/Ultra/PhaseEnergyArray.asset`、`UltraComputingNexus.asset`；新增 `Assets/Tests/Editor/UltraContentSliceTests.cs`。
- 行为：Ultra 研究由 1/1 扩展为 4/4，建筑由 0/0 扩展为 2/2；所有新增定义均可达，未新增 Resource、Workshop、Runtime State、存档字段或反射。
- 输出：新增 `outputs/Kingdom-Ultra最小闭环扩展-2026-09-21.md`；同步修正当前路线与剧情文档对 Ultra“仅有入口”的过时描述。
- 验证：`content-closure-check.ps1` exit 0；`check-building-sustainability.py` exit 0；Runtime/Editor Developer build 0 error；Editor/PlayMode 测试程序集编译 exit 0；`validate-todolist-gates.ps1` exit 0；项目探针 exit 0。
- 限制：未执行真实 Unity 编译、EditMode/PlayMode、Console 或完整运行期体验验收；上述静态/开发编译结果不替代真实运行验收。下一动作是取得 Unity 运行断言后补跑相关 Ultra 测试；在此之前可继续其他独立 outputs 审查。

## 2026-09-21 存档缺段键修复续接

- 触发证据：`TestResults/Latest-Test-Errors.txt` 时间为 2026-09-20 20:31:50，677 项中 676 通过、1 项失败；`SaveLoad_MissingRequiredSectionStartsNewGame` 删除 `Workshop` 顶层键后仍被当作有效存档载入。
- 根因：`JsonUtility.FromJson<KingdomSaveData>` 会为缺失的顶层对象字段补默认实例，原有对象级空值校验无法识别“键缺失”。
- 实施：`Assets/Resources/Script/Manager/SaveManager.cs` 的 `TryReadPath` 在反序列化前检查当前 v9 存档要求的八个顶层键（`General`、`Resources`、`Buildings`、`Researches`、`Workshop`、`Sectors`、`Tutorial`、`Story`）；使用轻量 JSON 字符串扫描，不新增字段、版本迁移、备份恢复或反射。
- 输出：`outputs/Kingdom-存档缺段键失败修复-2026-09-21.md`。
- 验证：`dotnet build Kingdom.Runtime.Developer.csproj --no-restore` 0 error（保留 3 个既有 warning）；`dotnet build Kingdom.Editor.Developer.csproj --no-restore` 0 error；`tools/codex/compile-developer-tests.ps1` 的 Editor/PlayMode compiler 均 exit 0。
- 边界：最新 Unity 日志尚未因本修复刷新，未执行真实 Unity 编译、EditMode/PlayMode/Console；不能把编译门结果写成运行验收。下一动作是 Unity 可用时重跑该存档失败用例及相关回归，再补 Ultra 相关运行断言。

## 2026-09-21 Ultra R1 产业链扩展续接

- 本轮实施：新增 `PhaseGridSynchronization`、`InterstellarResourceCoordination` 两项 Ultra 研究；新增 `AutonomousMatterFabricator`、`InterstellarLogisticsArray` 两座 Ultra 建筑及对应 `.meta`。
- 行为：Ultra 研究由 4/4 扩展为 6/6，建筑由 2/2 扩展为 4/4；新增研究分别使用现有电力倍率与全局物流倍率，新增建筑分别生产既有 PhantomAlloy/PhantomWeave 与提供既有物流/舰队字段。
- 边界：未新增 Resource、Workshop、Runtime State、存档字段、容量上限或独立路线系统；Ultra R2/新机制继续冻结。
- 输出：`outputs/Kingdom-Ultra-R1产业链扩展-2026-09-21.md`；同步更新 `ToDoList.txt`、`outputs/ToDoList.txt`、路线与剧情文档的当前计数/描述。
- 验证：`content-closure-check.ps1` 首次发现一个少 1 位的 GUID，修正后 exit 0；最终 Ultra 研究 6/6、建筑 4/4、无不可达项。新增 Ultra 测试已加入测试程序集编译范围。
- 未解决：Unity 进程仍未运行，`TestResults/Latest-Test-Errors.txt` 仍为 2026-09-20 旧日志；未执行真实 Unity 编译、EditMode/PlayMode/Console 或完整运行期体验验收。下一动作是 Unity 可用时先跑新增 Ultra 测试与存档缺段键回归，再评估 R1 运行期供应与节奏。

## 2026-09-21 Ultra R1 工厂合并续接

- 用户收窄目标：Ultra 朝极简靠近，能合并的工厂尽量合并；明确由一个工厂承担绝大部分材料生产任务。
- 实施：删除独立 `InterstellarLogisticsArray` 及 `.meta`；扩展 `AutonomousMatterFabricator` 为唯一 Ultra 综合材料工厂，承担 `PhantomAlloy`、`PhantomWeave`、`PhaseMaterial`、`Composite` 主要产出，并合并物流产出与舰队支援。
- 当前 Ultra 建筑由 4/4 收缩为 3/3：`PhaseEnergyArray`、`UltraComputingNexus`、`AutonomousMatterFabricator`；研究仍为 6/6，Workshop 仍为 0/0。
- 输出：`outputs/Kingdom-Ultra-R1工厂合并收缩-2026-09-21.md`；同步更新两份 ToDoList 与当前剧情文档。此前 4 座建筑的 Ultra outputs 保留为历史记录。
- 验证：`content-closure-check.ps1` exit 0；Ultra 研究 6/6、建筑 3/3，无不可达项。Unity 仍未运行，最新断言日志未刷新；未执行真实 Unity 编译、EditMode/PlayMode/Console 或完整运行期体验验收。

## 2026-09-21 outputs 状态同步续接

- 发现：`ToDoList.txt` 与 `outputs/ToDoList.txt` 的 D05/D06 标题仍写成“待实施方案/方案待定案”，与 2026-09-19 已落地的 E08 乙案冲突。
- 修正：两份清单同步标记 D05/D06 为“已实施，运行回归待补”，并保留原历史核算与方案背景；补充三座 Spacer 新厂、旧厂升级指向、已建旧厂继续运行/解锁后停止新建等当前状态。
- 输出：`outputs/Kingdom-outputs当前状态同步-2026-09-21.md`，汇总当前计数、Ultra 单工厂边界、D05/D06 状态和历史快照边界。
- 验证：两份清单的 D05/D06 文本已一致；未修改运行时代码或资产。该同步不需要等待 Unity 断言。
- 未解决：E08 六态 Unity 回归、真实净流/回本记录仍待 Unity 可用；`TestResults/Latest-Test-Errors.txt` 仍是旧日志。下一动作继续审查其他 outputs 中的“当前状态/历史背景”冲突，避免重复实施已完成条目。

## 2026-09-22 Ultra 输入描述校正续接

- 发现：`outputs/Kingdom-Ultra-R1工厂合并收缩-2026-09-21.md` 仍沿用了合并前的输入描述，把 `PhaseMaterial`/`Composite` 写成综合工厂的持续消耗。
- 修正：按当前 `AutonomousMatterFabricator.asset` 改为持续消耗 TitaniumAlloy、Electronics、Machinery、Nickel，并明确不把自身产物作为持续输入；产出、物流和舰队职责不变。
- 验证：报告与当前资产字段一致；`git diff --check` 无内容错误。未执行真实 Unity 编译。
- 同步：因该报告在本轮被修正，末尾证据边界已改为区分“报告形成时未运行”与“当前日志早于合并变更”，避免把历史状态误读为当前验收。

## 2026-09-22 后期综合工厂收敛续接

- 用户新方向：后期建筑尽量由一个建筑承担绝大部分资源生产，把建筑数量让给能源、研究、物流、舰队、生态和其他能力机制。
- 实施：扩展 `OrbitalResourceExtractionArray` 为 Spacer 综合材料工厂，覆盖 21 类主要材料；将七座 Spacer 材料厂的 `upgradeTo` 统一指向它。已建旧厂继续运行，综合工厂解锁后不再新增旧材料厂；`OrbitalAgroecologyArray` 保持 Food/生物质/木材独立角色。Ultra 的 `AutonomousMatterFabricator` 单工厂方案保留。
- 测试：新增 `Assets/Tests/Editor/LateFactoryConsolidationTests.cs`，覆盖升级入口收敛、21 类产出、物流/舰队职责和无相反资源流。
- 输出：`outputs/Kingdom-后期综合工厂收敛-2026-09-22.md`；同步路线图、剧情、两份 ToDoList 和当前 outputs 索引。
- 验证：closure exit 0；可持续性 72/40、0 失败；资源流 `BuildingAssets=72`、`OpposingRawResourceFlows=0`；Runtime/Editor Developer build 0 error（保留 3 个既有 warning）；Editor/PlayMode 测试程序集编译 exit 0；NewEconomySimulator 本轮修改前通过。
- 边界：未执行真实 Unity 编译、EditMode/PlayMode、Console 或完整运行期体验验收；当前综合工厂的实际供给、等待时间和 UI 展示仍待 Unity 运行证据。

## 2026-09-22 后期巨构消耗续接

- 用户新增约束：后期建筑的消耗必须磅礴大气；这不是等待 Unity 断言的理由，先完成可验证的定义与文档收敛。
- 实施：`OrbitalResourceExtractionArray` 调整为 `8000` 空间、`16000` 生产力、`24` Food/s、`5000` Power/s、`1200` Logistics/s、`1000` Fleet Power、`400` Defense；`AutonomousMatterFabricator` 调整为 `12000` 空间、`24000` 生产力、`64` Food/s、`12000` Power/s、`2000` Logistics/s、`2500` Fleet Power、`800` Defense。两者建造材料、持续原料和材料产出按巨构规模同步放大。
- 回归约束：未增加普通资源容量、Runtime State、存档字段或独立系统；能源、生态与科研仍由独立建筑承担。新断言已加入 `LateFactoryConsolidationTests` 和 `UltraContentSliceTests`，锁定巨构级消耗下限。
- 变更后验证：closure exit 0；可持续性 72/40、0 失败；资源流 `BuildingAssets=72`、`OpposingRawResourceFlows=0`；Runtime/Editor build 0 error（保留 3 个既有 warning）；Editor/PlayMode 测试程序集编译 exit 0；`TODO_SYNC=True`、相关资产/测试 `MISSING_META=NONE`。
- 下一动作：Unity 可用时重跑新增测试、六态供给/净流与回本记录；当前静态/开发编译证据不能替代运行期供给验收。

## 2026-09-22 Ultra R1 能力工坊扩展续接

- 旧 Ultra outputs 已读完；在不新增资源生产建筑、资源、库存、Manager/Runtime 状态或即时战斗系统的边界内，新增三个 `TechLevel.Ultra` WorkshopUpgrade：`AutonomousMatterOrchestration`（全局建筑生产 1.20）、`PhaseCognitiveCompute`（全局研究 1.25）、`InterstellarSupplyMesh`（全局物流 1.30）。
- 三个工坊分别依赖既有 Ultra 研究并消耗 PhantomAlloy/PhantomWeave/PhaseMaterial/Composite/Electronics/Machinery 等现有资源，继续强化“一个综合工厂 + 能力机制”的后期结构。
- 新增输出：`outputs/Kingdom-Ultra-R1能力工坊扩展-2026-09-22.md`；同步当前 outputs 索引、路线图、剧情和两份 ToDoList，历史 Ultra 报告保持原始计数。
- 当前静态证据：Ultra 研究 6/6、Workshop 3/3、建筑 3/3；closure exit 0；可持续性 72/40、0 失败；资源流 `BuildingAssets=72`、`OpposingRawResourceFlows=0`；Runtime/Editor build 0 error；Editor/PlayMode 测试程序集编译 exit 0。
- 边界：现有 Unity 日志早于本轮工坊资产与测试变更，不能作为运行验收。未执行真实 Unity 编译。下一步是 Unity 可用时重跑工坊断言、六态供给/净流、回本与 UI 展示。

## 2026-09-22 Ultra R1 深空机制扩展续接

- 在单一 Ultra 综合材料工厂和三个能力工坊之后，新增三项 `TechLevel.Ultra` 研究：`UltraExpeditionDoctrine`（远征效率 1.25）、`SelfRepairingFleetArchitecture`（舰队维修成本 0.75）、`CausalOccupationNetwork`（占领资源产出 1.30）。它们复用现有 `ResearchEffectType` 与 Sector/Campaign 运行时，不新增建筑、资源、Manager、Runtime State、存档字段或即时战斗模式。
- Ultra 当前计数：研究 9/9、Workshop 3/3、建筑 3/3；材料生产工厂仍只有 `AutonomousMatterFabricator` 一座。
- 新增输出：`outputs/Kingdom-Ultra-R1深空机制扩展-2026-09-22.md`；同步当前 outputs 索引、两份 ToDoList 和剧情文档，历史 R1 报告保留原始计数。
- 变更后验证：closure exit 0；Ultra 9/9 研究、3/3 Workshop、3/3 建筑；可持续性 72/40、0 失败；资源流 `BuildingAssets=72`、`OpposingRawResourceFlows=0`；Runtime/Editor build 0 error（保留 3 个既有 warning）；Editor/PlayMode 测试程序集编译 exit 0。
- 边界：现有 Unity 日志早于本轮研究资产与测试变更，不能作为运行验收。未执行真实 Unity 编译。下一步是 Unity 可用时重跑 Ultra 研究、远征/维修/占领行为、六态供给/净流和 UI 展示。

## 2026-09-22 Ultra 当前内容总览续接

- 为避免 Ultra 状态分散在多份 R1 报告，新增 `outputs/Kingdom-Ultra-R1当前内容总览-2026-09-22.md`，集中记录 9 项研究、3 座建筑、3 个 Workshop、单一综合材料工厂及证据边界。
- 当前事实保持：Ultra 研究 9/9、Workshop 3/3、建筑 3/3；没有新增 Resource、第二座材料工厂、Runtime State、存档字段或即时战斗系统。
- 当前 outputs 索引已链接总览；历史报告中的旧计数保留为历史快照，不作为当前状态。
- 路线图补充当前 Ultra R1 事实：9 项研究、3 座建筑、3 个 Workshop，深空机制覆盖远征推进、舰队维修和占领治理；仍保留单一材料工厂边界。

## 2026-09-22 Unity 最新断言续接

- 新日志：`TestResults/Latest-Test-Errors.txt` 于 2026-09-22 12:10:20 刷新，680 项中 679 通过、1 失败。
- 失败：`SpaceProgressionTests.ResearchPowerBuildingsFormOneContinuousUpgradeChain` 的旧白名单漏收 `UltraComputingNexus`；不是本轮 Spacer/Ultra 资源流失败。
- 修正：`SpaceProgressionTests.cs` 将 Ultra 研究中枢加入研究力建筑白名单，未改变既有升级链。
- 验证：修正后 `compile-developer-tests.ps1` Editor/PlayMode 均 exit 0；Unity 仍在运行，尚未取得修正后的重跑结果。未执行真实 Unity 编译。
- 证据边界补充：该 12:10 日志早于 12:20 的后期综合工厂资产和 `LateFactoryConsolidationTests.cs` 变更，不能作为本轮合并资产的运行验收。

## 2026-09-22 确定性回归续接

- 在 Ultra 当前总览、路线图和 ToDo 收敛后，运行 `dotnet run --project tools/NewEconomySimulator/NewEconomySimulator.csproj --no-restore -- --json`。
- 结果：exit 0，JSON `passed=true`，13/13 结果通过；这是确定性回归证据，不是 Unity 运行期供给、节奏、回本、UI 或完整体验验收。
- 当前仍未取得 Unity 修正白名单后的重跑结果；未执行真实 Unity 编译。下一具体动作仍是 Unity 可用时重跑新增 Ultra/后期工厂断言、六态供给/净流、回本与 UI 验收。

## 2026-09-22 Ultra 总览消耗口径续接

- 复核 Ultra 资产目录时确认 `InterstellarSupplyDoctrine` 是既有 TechLevel 4 Spacer 工坊，不属于 Ultra 新增 3 项 Workshop；当前 Ultra 3/3 计数保持正确。
- 在 `outputs/Kingdom-Ultra-R1当前内容总览-2026-09-22.md` 补充 Spacer/Ultra 两座综合工厂的空间、生产力、Food、Power、Logistics、舰队和防御消耗/支援表，明确“磅礴消耗”是可核对的资产字段，不是文案形容。
- 未新增玩法资产或运行时字段；未执行真实 Unity 编译。Unity 运行断言仍是后续唯一主要验收缺口。

## 2026-09-22 Ultra 深空战役纵深扩展续接

- 新增 3 项 `TechLevel.Ultra` 研究：`UltraCampaignContinuity`（战役进度 ×1.30）、`AdaptiveFleetLogistics`（补给成本 ×0.72）、`PredictiveCasualtyControl`（伤亡 ×0.72）。它们复用现有 Campaign 效果和既有资源，不新增 Runtime State、存档字段、资源或工厂。
- Ultra 当前收敛为 12/12 研究、3/3 Workshop、3/3 建筑；材料生产仍只有 `AutonomousMatterFabricator`。
- 新增输出：`outputs/Kingdom-Ultra-R1深空战役纵深扩展-2026-09-22.md`；同步当前总览、状态索引、路线图、剧情和两份 ToDoList，历史 9/9 报告保留为历史快照。
- 变更后验证：closure 12/12 exit 0；可持续性 72 建筑/40 资源、0 失败；资源流 `BuildingAssets=72`、`OpposingRawResourceFlows=0`；Runtime/Editor build 0 error（3 个既有 warning）；Editor/PlayMode 测试程序集 exit 0；ToDo 门禁通过；确定性模拟器 13 项总体 `passed=true`。
- 边界：`TestResults/Latest-Test-Errors.txt` 仍早于本轮资产变更，未取得修正白名单后的 Unity 重跑结果。未执行真实 Unity 编译。下一动作仍是 Unity 可用时重跑 Ultra 研究与深空行为、六态供给/净流、回本和 UI 验收。

## 2026-09-22 ToDo 历史声明收敛续接

- 修正 `ToDoList.txt` 与 `outputs/ToDoList.txt` 头部的时间口径：明确“原始两轮审查本身未修改代码/资产/数值”，后续实施批次以当前证据账本、outputs 和交接记录为准。
- 最终门禁：Ultra 12/12 研究、3/3 Workshop、3/3 建筑；`BuildingAssets=72`、`OpposingRawResourceFlows=0`；`TODO_SYNC=True`；文档 `git diff --check` 通过。
- Unity 运行证据仍未刷新；未执行真实 Unity 编译。

## 2026-09-22 outputs 当前索引日期收敛续接

- `outputs/Kingdom-outputs当前状态同步-2026-09-21.md` 文件名保留历史入口，但正文已承载 2026-09-22 的 Ultra 12/12 状态；标题日期已改为 2026-09-22，避免把现行索引误读为旧快照。

## 2026-09-22 Ultra 巨构支撑扩展续接

- 新增 3 项 `TechLevel.Ultra` 研究：`PhaseEnergyHarmonics`（定向强化 `PhaseEnergyArray` 电力产出 ×1.30）、`ClosedLoopAgroecology`（全局 Food 生产 ×1.25）、`AutonomousCivicProductivity`（人口生产力 ×1.18）。
- 三项研究复用既有能源、Food 和人口生产力效果消费者，不新增资源、建筑、工坊、Food 容量、workforce、Runtime State 或存档字段；Ultra 材料工厂仍只有 `AutonomousMatterFabricator`。
- Ultra 当前收敛为 15/15 研究、3/3 Workshop、3/3 建筑；新增输出：`outputs/Kingdom-Ultra-R1巨构支撑扩展-2026-09-22.md`，并同步当前总览、索引、路线图、剧情和两份 ToDoList。
- 本轮先通过 closure 15/15；后续需重跑资产变更后的可持续性、资源流、开发编译、测试程序集和 ToDo 门禁。Unity 仍未刷新；未执行真实 Unity 编译。

## 2026-09-22 Ultra 巨构支撑验证续接

- 资产变更后验证完成：closure Ultra 15/15、Workshop 3/3、建筑 3/3；可持续性 72 建筑/40 资源、0 失败；资源流 `BuildingAssets=72`、`OpposingRawResourceFlows=0`。
- Editor/PlayMode 测试程序集编译 exit 0；Runtime/Editor Developer build 0 error（保留 3 个既有 warning）；ToDo 门禁通过；`TODO_SYNC=True`；GUID 无重复；`git diff --check` 通过。
- 13 项确定性模拟器回归此前已总体 `passed=true`，本批仅增加定义资产，不改变其输入；仍不能替代 Unity 运行期供给、节奏或完整体验验收。
- `TestResults/Latest-Test-Errors.txt` 仍是旧日志；未执行真实 Unity 编译。

## 2026-09-22 Ultra R1 边界收口续接

- 新增 `outputs/Kingdom-Ultra-R1内容边界与后续门槛-2026-09-22.md`，记录 Ultra R1 当前 15/15 研究、3/3 Workshop、3/3 建筑、单一材料工厂和巨构消耗边界。
- 结论：R1 能力面已覆盖材料/能源/生态/人口/物流/深空战役，不再继续堆同类倍率；R2 的新 Runtime State、连续工程验证、遗迹或更大星门系统保持冻结，等待 R1 Unity 运行验收。
- 下一具体动作：Unity 可用时重跑 Ultra 新研究效果、巨构供给、深空行为、六态净流、回本和 UI；未执行真实 Unity 编译。

## 2026-09-22 Ultra 效果消费端审查续接

- 对 15 个 Ultra 研究逐项核对：涉及 14 类 `ResearchEffectType`，全部在 `ProgressionModifierManager` 找到消费端；`PhaseEnergyHarmonics` 的定向 Building GUID 解析为 `PhaseEnergyArray`。
- 审查结果：`EFFECT_CONSUMER_GAPS=0`、`PHASE_ENERGY_TARGET=True`。这是源码/资产覆盖证据，不替代 Unity 运行期效果验收。

## 2026-09-22 Ultra R1 运行验收矩阵续接

- 新增 `outputs/Kingdom-Ultra-R1运行验收矩阵-2026-09-22.md`，将当前 R1 的 Unity 收口拆成七个验收簇：Ultra 定义闭包、研究效果消费端、合并工厂六态净流、深空战役连续机制、升级回本与收敛、事务/存档边界、UI/真实启动链。
- 矩阵明确当前已取得的静态/编译/确定性回归证据，以及必须由当前 Unity Editor/PlayMode 日志证明的行为；不把 13 项模拟器 `passed=true` 或开发编译当成运行期验收。
- 当前设计继续冻结：Ultra 3/3 建筑、1 座 `AutonomousMatterFabricator`、3/3 Workshop；Spacer 1 座 `OrbitalResourceExtractionArray` 主材料综合工厂；巨构空间、生产力、Food、Power、Logistics、舰队与防御消耗保持磅礴规模；不新增同类倍率、第二材料工厂、普通资源容量、workforce 或 R2 Runtime State。
- 当前 Unity 证据仍是旧 `TestResults/Latest-Test-Errors.txt`（680 项、679 通过、1 失败），早于本轮后期工厂、巨构支撑资产和验收测试；因此下一具体动作仍是可用时按矩阵顺序重跑 Unity Editor/PlayMode，并取得新日志。未执行真实 Unity 编译。

## 2026-09-22 outputs 当前/历史口径审查续接

- 扫描 outputs、ToDoList、路线图和剧情文档后，确认当前权威计数统一为 72 座建筑、143 项研究、40 个 Resource、86 个 Workshop；Ultra 为 15/15 研究、3/3 建筑、3/3 Workshop，材料生产保持单一 `AutonomousMatterFabricator`。
- 未发现活动文档继续把已删除的 `InterstellarLogisticsArray` 当作当前资产；命中均位于明确的历史输出快照或变更轨迹中。
- 当前索引补充说明：`Kingdom-后期综合工厂收敛-2026-09-22.md` 是综合工厂收敛阶段的 6/6 历史快照，当前应以 15/15 总览和 R1 边界报告为准；`Kingdom-Ultra-R1当前内容总览-2026-09-22.md` 将“新增六项”修正为从 6/6 到 15/15 的后续九项。
- 当前索引的 diff-check 口径改为区分“本次 outputs/交接文档范围通过”和“全量工作树中的 Unity `.meta` 空字段尾随空格/LF→CRLF 提示”，避免把工作树提示写成全量无告警。
- 本轮没有改写历史报告中的原始计数、日期或阶段性验证结论；下一具体动作仍是按运行验收矩阵取得新的 Unity Editor/PlayMode 证据。未执行真实 Unity 编译。

## 2026-09-22 outputs 审查后门禁复核

- `content-closure-check.ps1`：exit 0；Industrial 81/81 Research、37/37 Workshop、50/50 Building；Spacer 47/47 Research、46/46 Workshop、19/19 Building；Ultra 15/15 Research、3/3 Workshop、3/3 Building；资源 40，无不可达项。
- `check-building-sustainability.py`：exit 0；72 座建筑、40 个有生产方资源，必要条件 0 失败；该检查仍不证明实际供给充足。
- `building-resource-flow-check.ps1`：exit 0；`BuildingAssets=72`、`OpposingRawResourceFlows=0`。
- `validate-todolist-gates.ps1`：exit 0；UI 静态配置、闭包、资源流和 ToDo 门禁通过。
- `NewEconomySimulator`：exit 0，JSON 总体 `passed=true`；结果作为确定性回归证据，不替代 Unity 运行期供给、节奏、回本或完整体验验收。
- 本轮仅修改 outputs/交接文档口径，没有新增代码、资产或测试行为；Unity 运行证据仍未刷新。未执行真实 Unity 编译。

## 2026-09-22 Ultra 历史设计导航续接

- 旧 `outputs/Kingdom_Ultra_SF_Design_2026-09-14.md` 及其同源 PDF 已复核为历史提案：其中 Ultra 1/1、工程验证、遗迹、星门和文明冗余均是当时的设计候选，不覆盖当前 R1 资产事实。
- 当前导航已明确：R1 以 15/15 研究、3/3 建筑、3/3 Workshop、单一 Ultra 综合材料工厂和巨构级维护为准；R2 只有在 R1 Unity 运行验收后才能进入实现评估。

## 2026-09-22 Ultra 目标级资产核对续接

- 直接读取当前资产确认：Ultra Research 15 项、Ultra Building 3 座、`TechLevel: 5` Workshop 3 项；Ultra 研究使用 14 种效果类型，均已有 `ProgressionModifierManager` 消费端。
- 直接读取两座综合工厂字段：`OrbitalResourceExtractionArray` 为 8000 space / 16000 productivity / 24 Food/s / 5000 Power/s / Logistics 2400 产出与 1200 消耗 / 1000 Fleet Power / 400 Defense；`AutonomousMatterFabricator` 为 12000 / 24000 / 64 / 12000 / 5000 与 2000 / 2500 / 800，符合后期巨构消耗方向。
- 读取 `.meta` GUID 后复核，七座 Spacer 后期材料厂的 `upgradeTo` 均指向 `OrbitalResourceExtractionArray`；活动资产、源码、docs 和 ToDo 中没有 `InterstellarLogisticsArray` 引用。
- 本次核对没有新增或修改玩法资产；此前一次按不存在的 `Workshop/Ultra` 子目录统计导致的 0/0 假象已用实际 `TechLevel` 字段纠正，不作为项目状态记录。

## 2026-09-22 Ultra 用户目标对照续接

- 新增 `outputs/Kingdom-Ultra-R1用户目标对照-2026-09-22.md`，将用户提出的“工厂尽量合并、单一工厂承担主要材料、后期巨构消耗、增加机制而非增加生产建筑、保持极简”逐条映射到当前资产和证据。
- 该对照报告明确 R1 静态落地与 Unity 运行期缺口，不把 15/15 定义闭包写成已经完成的供给、回本或 UI 体验验收。

## 2026-09-22 后期综合工厂维护闭环续接

- 最新真实 Unity EditMode 运行曾得到 686 项、613 通过、73 失败；失败根因集中为短暂加入 `OrbitalResourceExtractionArray` 自产自耗 PhantomAlloy/PhantomWeave 后触发既有 Runtime 自循环校验，并非 73 个独立玩法故障。
- 最终收敛方案：综合工厂不自产其高级维护输入，保留 18 类主材料产出；Coke 由焦化链供应，PhantomAlloy/PhantomWeave 由专门高阶工厂供应，综合工厂持续消耗二者。恢复后 `content-closure-check.ps1`、可持续性、`OpposingRawResourceFlows=0`、ToDo 门禁、Runtime/Editor build 与测试程序集编译均通过。
- 确定性模拟器 13 项总体 `passed=true`。Unity PID 12416 仍持有项目锁，最终资产修正后的 EditMode 重跑尚未取得；不得把中间 686/613/73 记为最终运行验收，PlayMode/UI/运行期体验证据仍待补。
- 下一具体动作：Unity 项目锁释放后重跑 EditMode；若通过，再跑 PlayMode 与矩阵中的六态供给、净流、回本和真实启动链。不要继续增加 Ultra 工厂或重复倍率。
## 2026-09-22 Unity 最终断言续接

- 当前 Unity Editor 在最终资产状态下完成全量 EditMode：2026-09-22 20:03:19，686/686 通过、0 失败、0 跳过。
- 随后完成全量 PlayMode：2026-09-22 20:04:24，35/35 通过、0 失败、0 跳过；`TestResults/Latest-Test-Errors.txt` 当前保留该 PlayMode 报告，EditMode 结果已同步至 outputs/ToDo 与本交接。
- 之前 686/613/73 是 Phantom 自产自耗中间资产状态；最终资产已消除自循环，保持 18 类综合主产出与专门 Phantom 供应链。不得再把中间失败数写入当前验收结论。
- 静态复核仍为 closure Ultra 15/15、Workshop 3/3、Building 3/3；总建筑 72、资源 40；`OpposingRawResourceFlows=0`；ToDo 门禁通过；确定性模拟器总体 `passed=true`。
- 未完成边界：外部运行体验由用户自行完成，代理不执行或跟踪；代理继续只负责 Unity 编辑器断言、内容闭包、代码编译和玩法逻辑，不因此增加 Ultra 工厂或重复倍率。
## 2026-09-23 Ultra R2 文明工程核心循环

- 已新增 `UltraProjectState`、`UltraProjectManager`、`UltraProjectDefinition` 与当前 v9 可选 `UltraProjectSaveData`。缺少新增 Ultra 区段时初始化全新锁定状态，不迁移旧存档。
- 三阶段现在是“运行 -> ReadyToCommit -> 提交 -> 下一阶段/完成”，启动费按当前阶段只支付一次；暂停、恢复、放弃、幂等提交均由强类型状态管理。
- Tick 复用实时与离线同一入口，按阶段剩余进度切分边界，并以 ResourceManager 原子扣款回滚高级材料、Food 与工程状态；Power/Logistics 负载加入既有流量收敛。
- 现有建筑详情页新增文明工程信息与操作：阶段、进度、Food/Power/Logistics 消耗、供给阻碍、启动/暂停/恢复/提交，以及稳定/高压姿态切换。没有新增资源或第二座 Ultra 材料工厂。
- 本轮静态证据：Ultra closure 15/15 Research、3/3 Building、3/3 Workshop；总建筑 72、资源 40；`OpposingRawResourceFlows=0`；开发者 Runtime/Editor 与 Editor/PlayMode 测试程序集编译 0 错误。
- 真实 Unity EditMode 曾启动但一次因 180 秒导入/编译超时，后续短筛选因 Unity Licensing token unavailable 未产生结果 XML；当时不能声称真实 Unity 编译或完整断言已验收。
- 后续 UI 收口：连续性认证完成后，Sectors 详情页复用既有双按钮页脚提供“远征姿态：切换突进/切换稳健”，直接调用 `TrySetCampaignDoctrine`；不新增页面或资源。
- UI 收口后的开发者 Runtime/Editor 与 Editor/PlayMode 测试程序集再次编译通过，0 错误、3 个既有警告。
- 代理收口审查提出的 4 个问题已修复：最终 ReadyToCommit 存档要求 `CurrentStage=Completed`；自动暂停纳入 Ultra 状态版本刷新；运行/暂停/待提交状态不再显示错误的启动阻碍；启动材料不足会在预览中禁用启动按钮。
- 新增非法最终 ReadyToCommit 存档回归测试；修复后 Developer Runtime/Editor 与 Editor/PlayMode 测试程序集再次编译通过，0 错误、3 个既有警告。
## 2026-09-23 Ultra R2 Unity 运行证据

- 重新执行真实 Unity EditMode：`TestResults/EditMode-results.xml`，691/691 通过、0 失败、0 跳过。
- 重新执行真实 Unity PlayMode：`TestResults/PlayMode-results.xml`，35 总数、34 通过、0 失败、1 跳过。跳过项为 `KingdomOnboardingPlayModeTests.OuterPageScroll_DirectDragReportsMeasuredBounds`，原因是本轮没有检测到 Overview 溢出，因此背景拖拽未执行。
- `TestResults/Latest-Test-Errors.txt` 已更新为本轮 PlayMode 结果；Unity 进程在保存 XML 后正常退出。外部运行体验和真实节奏由用户自行验收。
## 2026-09-23 Ultra R2 UI 可玩性收口

- Ultra 建筑详情在追加文明工程信息后重新执行统一需求/流量布局，避免工程文本遮挡或挤压下方材料区，并保留当前滚动位置。
- 详情页显示阶段启动材料的“库存 / 需要 / 缺口”，以及高级材料持续消耗与当前库存；手动暂停与供给不足自动暂停分别提示。
- Ultra 姿态按钮显示当前姿态、切换条件和运行中/待提交/已完成的不可切换原因；远征按钮显示当前稳健/突进姿态及切换目标。
- 收口后的真实 Unity EditMode 仍为 691/691 通过；PlayMode 为 35 总数、34 通过、0 失败、1 跳过。一次 PlayMode 无 XML 是前一轮 EditMode 批处理进程未释放项目锁造成的环境冲突，清理后重跑通过。
- 外部运行体验按用户要求移出代理交付范围；不再记录或追踪该验收链路。

## 2026-09-23 事务审查增量

- `SectorManager.CalculateCampaignBillableSeconds` 使用实际推进倍率，修复 Surge 完成边界多扣补给。
- `GameManager`/`CampaignState` 拒绝非法 `CampaignDoctrine`，不再静默回退为 Stable。
- `ResourceManager` 原子变更预检增加本次请求内稳定 ID 去重。
- 新增三项 EditMode 覆盖上述边界；最新真实 Unity 结果为 EditMode 694/694 通过，PlayMode 35/34/0/1（总数/通过/失败/跳过）。
- 本轮不执行外部运行体验验收；该边界由用户自行完成。

## 2026-09-23 深审修复与最新验证

- Economy 深审修复已合并：Ultra 高级材料参与同 tick 满足率；实时/离线星区操作共用调度入口；无目标维修、非有限费用与非法完成边界均零副作用拒绝。
- Save/UI 深审修复已合并：缺少新增 Ultra 区段初始化全新状态，显式空/非法 DTO 拒绝；完整工程完成后远征姿态可达，UI 与 v9 重载回归已补齐。
- Developer Runtime/Editor 与 Editor/PlayMode 测试程序集编译 0 错误，保留 3 个既有警告。
- Unity EditMode XML 711/711 通过、0 失败、0 跳过；批处理在保存 XML 后收尾超时，因此记录为“结果全通过、脚本收尾超时”，不扩大解释为脚本成功。
- Unity PlayMode 36 总数、35 通过、0 失败、1 跳过；跳过项为无 Overview 溢出时未执行背景拖拽测量。
- 文档扫描 118 份，平台/设备/发布验收目标词残留为 0；外部体验由用户自行完成，不列为代理待办。

## 2026-09-23 文档范围清理续接

- 本轮仅清理 `docs/`、`outputs/`、`.codex/handoffs/`、`.agents/skills/kingdom-*` 与根 `AGENTS.md` 中的特定平台、终端和交付验收表述；未修改源码、测试、资产或工具。
- 历史日期、测试数量、退出码、超时与失败事实保留；涉及后续外部操作的条目已删除或改为中性范围说明，不形成代理待办。
- 删除一份仅用于外部构建审查、会引导后续平台操作的历史文档；其日期、超时和非通过结论已在其他历史记录中保留。
- 本轮未执行 Unity 编译、EditMode/PlayMode 或外部运行体验验收。

## 2026-09-23 文档边界修复续接

- 本轮仅修订文档边界，未修改源码、测试、资产或历史测试事实；`docs/testing/acceptance-checklist.md` 已由主线处理，本轮仅核对，未重复改写。
- 当前维护文档已将 R1/R2 的“冻结条件”“下一门槛”“仍需/必须验收”改为范围说明：R1 记录当前内容与 Unity 运行期观察范围；R2、新 Runtime State 和更大文明机制不属于对应 R1 文档的当前实施范围，不形成当前代理待办。
- `outputs/Kingdom-Ultra-R1*.md`、`outputs/Kingdom-Ultra-R2文明工程核心循环-2026-09-23.md` 与 `outputs/Kingdom-outputs当前状态同步-2026-09-21.md` 保留原有计数、日期、测试结果和中间失败事实；仅收口当前口径，避免把历史快照改写成当前门禁。
- 外部运行体验明确由用户自行验收；代理不执行、不跟踪，也不将其列为交付门槛。真实节奏、净流、回本等内容仅标为运行期观察范围。
- 核对：目标文件范围内执行 `git diff --check`；未执行 Unity 编译、EditMode/PlayMode 或外部运行体验验收，本轮文档修订不需要这些运行验证。

## 2026-09-23 深审修复与最终回归增量

- Ultra 启动姿态、最终提交幂等、放弃后重启启动费、显式非法 Ultra 存档、重复连续材料成本和倍率预览问题已修复。
- UI 修复姿态保持、资源库存驱动按钮刷新、普通详情安全边距和星区标题重复；内容测试增加三阶段闭包、一体化工厂唯一性和三座巨构持续消耗断言。
- 先修正 5 个旧保存测试的 v9 fixture，使其显式带合法 Ultra 锁定 DTO；并修正工程阶段二建筑期望以匹配现有资产。
- 最新真实 Unity：EditMode 699/699 通过、0 失败、0 跳过；PlayMode 35 总数、34 通过、0 失败、1 跳过。Developer 编译 0 错误、3 个既有警告。
- 本轮不执行外部运行体验验收；该边界由用户自行完成。

## 2026-09-28 Ultra R2 暂停原因持久化增量

- 改动：`UltraProjectState`/`UltraProjectStateSaveData` 新增强类型 `UltraProjectPauseReason`；Manager 自动暂停写入 `InsufficientSupply` 或 `DefinitionMissing`，手动暂停写入 `Manual`，恢复/放弃/提交清除旧原因。
- UI：Ultra 建筑详情和 Overview 从 `GetPreview()` 消费持久化原因，重载后仍区分供给不足暂停与手动暂停；没有保存 UI 临时文案。
- 测试：新增手动暂停保存/恢复、缺原因存档拒绝、自动供给暂停原因断言。
- 验证：`dotnet build Kingdom.DeveloperTests.csproj --no-restore` 0 错误、0 警告；此前同轮 `dotnet build Kingdom.Developer.sln --no-restore` 0 错误、3 个既有警告；定向源码尾随空格与反射扫描通过。Unity Editor 被用户占用，本轮未执行真实 Unity 编译，也未重跑 EditMode/PlayMode。
- 下一动作：Unity 空闲后再运行相关 EditMode/PlayMode，重点观察保存重载后的暂停提示。

## 2026-09-28 前序任务续审与复核

- 前序代理已不可用，本轮新开三组独立只读复核（State/Save、Economy/Tick、UI/Docs），结论均以当前源码、测试和输出 XML 重新确认；没有把旧结论当作验收证据。
- State/Save 复核未发现可复现实现缺陷；补充低时代 `Paused`、`ReadyToCommit` 存档拒绝用例，合并到全量 EditMode 745/745 结果。
- Economy 复核确认 Ultra 原子付款与满足率渐进路径无负库存反例。离线 Campaign 按实时科研 tick cadence 处理；与普通 `TickOffline` 在研究中途完成时对下一队列研究的 tick 余量处理不同。该有限差异有现有实现意图，完整跨 2h/8h 系统 parity 尚未验证；不扩大断言范围。
- UI 复核找到 Ultra 尚处 `Locked` 时 Overview 不展示阶段/导航的问题，已修复为提示并导航至首阶段巨构；Doctrine/主操作固定布局现由 Footer Prefab 预先定义，运行时仅绑定状态/事件。代理移除共用按钮 helper 时曾触发两处编译错误，随后已恢复；最终构建通过。
- 文档复审移除了 ToDoList 与验收准则中的外部平台/实机交付门槛，并同步 `outputs/ToDoList.txt`；两份待办经 SHA-256 核对一致。
- 当前构建：`dotnet build Kingdom.Developer.sln --no-restore` 0 错误、3 个既有警告，含 Editor/PlayMode 测试程序集编译。EditMode XML：`tmp/ultra-r2-audit-editmode-20260928.xml`，745/745 通过、0 失败、0 跳过（2026-09-28 13:30:48Z）。PlayMode XML：`tmp/ultra-r2-audit-playmode-20260928.xml`，35/36 通过、0 失败、1 跳过（无 Overview 溢出时未测拖拽）；未包含 Ultra Locked 分支的专门断言。
- 下一具体动作：如继续收口 UI，可增加 Ultra Locked Overview 导航的直接 PlayMode 断言；Campaign 离线研究 tick 余量和跨 2h/8h 完整系统 parity 维持为已知覆盖范围，不冒充已验证。未执行真实 Unity 独立编译。

## 2026-10-07 A 验证工具可信性增量

- `tools/codex/run-unity-tests.ps1`：结果 XML 现在必须包含 `test-run` 及四个计数属性；非数字/负数计数按 `Failed(InvalidXml)` 处理。Unity 非零退出即使留下成功 XML 也写 `Result: Failed`、`UnityExitCode` 并退出 1；零用例先写失败摘要再退出 1。
- `tools/codex/compile-unity.ps1`：Unity 退出 0 但日志缺失或为空时失败，保留缺日志状态与路径。
- `tools/codex/validate-todolist-gates.ps1`：子 gate 退出码显式传播；自定义 `ProjectPath` 传给各子脚本，content closure 使用 `ProjectRoot`。
- 隔离 fake Unity 反例：无 XML、坏 XML、零用例、非零退出+成功 XML 均 exit 1 且摘要非 Passed；正常成功 exit 0/Passed。编译缺失/空日志及非零退出 exit 1，正常日志 exit 0。聚合失败子脚本 exit 1 且收到自定义项目根。临时 fixture 已清理。
- 未执行真实 Unity 编译；未运行 Unity EditMode/PlayMode。本增量只覆盖工具判定，不改变启动器。

## 2026-10-07 A-D 实施与运行增量

- A：`run-unity-tests.ps1`、`compile-unity.ps1`、`validate-todolist-gates.ps1` 已按隔离反例修复；`tools/codex/test_validation_tools.py` 2 tests OK。fake Unity 缺 XML、坏 XML、零用例、非零退出+成功 XML、编译缺/空日志和非零退出均失败；正常成功通过。
- B：新增 Overview authored 背景拖拽与 Ultra Locked `PhaseEnergyArray` 导航 PlayMode 覆盖。首轮 XML `TestResults/codex-B-playmode-20261007.xml` 为 35/37，确认两个真实问题：640×480 批处理视口下 Overview 无 overflow；Locked 详情阻碍分支缺“下一步”。测试只在运行时缩小测试 viewport；`KingdomUIRoot.UltraProject.cs` 为 Locked/Ready 阻碍补“下一步: 先排除阻碍，再启动本阶段”。修复后 XML `TestResults/codex-B-playmode-20261007-r3.xml` 为 37/37，0 failed/0 skipped，退出码 0；日志 `Logs/codex-B-playmode-20261007-r3.log` 无游戏 Error/Exception/MissingReference，仅 Licensing/Curl 环境噪声。
- C：`EconomyIntegrationRegressionTests` targeted EditMode `TestResults/economy-target.xml` 为 3/3；当前证据覆盖三条工业链一次升级支付、资源不足原子失败、拆除、v9 sections 和 2h/8h 通用离线安全。代码复核确认实际生产 tick、副产物全量 delta、战役/文明工程与保存组合跨 2h/8h 尚未覆盖，不能扩大为 C2 完成。子代理已受派补强测试，待其提交后运行。
- D：`MusicManager` 独立 SFX 音量/静音和结果声路径已落地，Prefab authored `SfxVolume`/`SfxMute`/`SfxVolumeValue` 已存在；`AudioFeedbackRegressionTests` 2/2 通过（`TestResults/audio-target.xml`）。真实 Unity compile `Logs/codex-compile-final-20261007.log` ExitCode=0；尚未有播放请求计数、完整 PlayMode/Console、音乐独立性或人工试听证据。

下一动作：接收 C 测试补强并运行 targeted EditMode；随后重跑 `dotnet build Kingdom.DeveloperTests.csproj --no-restore`、`git diff --check`，同步本交接与 outputs。C2 或 D 运行期缺口不得写成完成。

## 2026-10-07 A-D 收尾核对增量

- A：隔离工具反例与 `python -m unittest tools/codex/test_validation_tools.py -v` 均通过（2/2）；新增 `TestFilter` 仅用于定向 Unity 验证，不改变启动器主流程。
- B：真实 PlayMode `TestResults/codex-B-playmode-20261007-r3.xml` 37/37 通过，退出码 0；Overview 溢出拖拽和 Ultra Locked→Ready 详情刷新均有运行证据。
- C：`EconomyIntegrationRegressionTests` 的 C1 定向用例通过；组合回归曾稳定复现 2 项失败，先后定位为 Ultra fixture 生产力/供给初始化与 WireMill 产出被组合消费。当前最终定向 XML 以最新 `economy-target-final10.xml` 为准，未生成前不得标完成；组合测试保留失败日志和实际首差，不调资产、不弱化断言。
- D：真实 Unity compile `Logs/codex-compile-final-20261007.log` ExitCode=0；设置持久化/Prefab authored 定向测试 2/2 通过。播放请求计数、失败/批量/去重、离线静默、音乐独立性 PlayMode/Console 与人工试听仍未验证；Slider Fill/Handle 和静音按钮视觉子节点风险保留。
- 文档同步：`outputs/动工策划案.md`、`outputs/声音反馈策划.md`、`outputs/ToDoList.txt` 必须按最新 XML 更新；旧失败 XML 仅作诊断，不替代本轮结果。下一动作是读取 final10 XML，若通过再补 C2 证据、重跑开发测试构建并检查工作树。

## 2026-10-07 C2 最终定向结果修正

- 夹具修正：`EconomyIntegrationRegressionTests` 移除对 `BuildingState.Amount` 的直接预写，统一由 `SetAmountAndRatesForEditor(0→1)` 登记生产、Power、Logistics 派生率；组合断言保留 authored 正生产率。被文明工程/战役等非 ResourceState 账本合法消费的净中性产出不再误判为未生产。
- 结果：`TestResults/economy-target-final13.xml`、`Logs/economy-target-final13.log`，5/5 passed、0 failed、0 skipped，Unity 退出码 0。覆盖三链升级/原子失败/拆除/v9、真实生产率、研究、战役、Ultra 工程及 2h/8h 离线组合。
- 边界：Food 当前仍只断言非负、容量和总人口消费关系；殖民、战役、工程扣粮与封顶损失尚未拆成独立账本用例。C2 的 Food 分项仍未完成，不能将该缺口写成已验证。
- 全量 EditMode 另行重跑中；旧 `EditMode-results.xml` 的 750/752 失败不代表 final13 定向结果，也不覆盖本次夹具修正。

## 2026-10-07 全量 EditMode 与 D authored 控件增量

- 全量 EditMode：`TestResults/EditMode-final-20261007.xml`、`Logs/EditMode-final-20261007.log`，752/752 passed、0 failed、0 skipped，Unity 退出码 0。
- D：修正 `SfxVolumeValue` authored TMP_Text 缺失，并为 `SfxVolume` 增加 authored Fill/Handle 子节点与 Slider 引用；`AudioFeedbackRegressionTests` 已收紧为检查 Slider 两个引用、Mute targetGraphic 和 SfxVolumeValue TMP_Text，等待最新定向 XML。
- 兼容性：恢复 `MusicManager.Play(string,string)`、`Play(string)`、`Play(AudioClip)` 原有公开重载；非建筑详情操作继续保留 Detail 点击声，建筑/升级/拆除/工坊只在成功分支发结果声。
- 仍未验证：运行期播放请求计数、失败零声、批量一次、研究完成/时代突破去重的 PlayMode 观测、离线静默、音乐独立性与人工试听。Prefab 已补结构，但听感和真实交互仍需单独证据。

## 2026-10-07 全量与音频证据更正

- 全量 EditMode `TestResults/EditMode-final2-20261007.xml`：752/752 passed、0 failed、0 skipped，Unity 退出码 0；这次运行包含修复后的 SfxVolume Fill/Handle 和 SfxVolumeValue TMP_Text authored 结构。
- 音频定向 `TestResults/audio-target-current3.xml`：断言 2/2 passed，但 Unity 在保存 XML 后以 -1 收尾，日志为 `Leftover web requests after shut down`；按 A 规则记为断言通过/进程失败，不作为独立 D 通过。全量 XML 是当前结构与设置回归的正常退出证据。
- D 仍未完成播放请求计数、失败零声、批量一次、研究/时代去重运行观测、离线静默、音乐独立性和人工试听；ToDo 中 D 保持未闭项。

## 2026-10-07 最终验证收口

- 全量真实 Unity EditMode：`TestResults/EditMode-final2-20261007.xml` / `Logs/EditMode-final2-20261007.log`，752/752 passed，0 failed，0 skipped，Unity 退出码 0。
- A 工具回归：`python -m unittest tools/codex/test_validation_tools.py -v`，2/2 passed；工具能将“XML 成功但 Unity 非零退出”判为整体失败。
- C2：`TestResults/economy-target-final13.xml` / `Logs/economy-target-final13.log`，5/5 passed，退出码 0；Food 分项扣粮账本仍是未完成覆盖。
- D：Prefab authored Fill/Handle/TMP 已补，全量 EditMode 已加载验证。`audio-target-current3.xml` 2/2 断言通过但 Unity 收尾 -1（leftover web requests），故记录为收尾异常；播放请求计数、失败/批量/去重、离线静默、音乐独立性 PlayMode/Console 与试听仍未完成。
- `dotnet build Kingdom.DeveloperTests.csproj --no-restore --nologo --verbosity quiet` 退出 0。保留工作树其他未提交修改，不 commit/push/PR。
## 2026-10-07 A-D 完成收口增量

- C2 Food 账本已补入 `Assets/Tests/Editor/EconomyIntegrationRegressionTests.cs`：DawnRing 殖民、ProximaB 战役、Ultra Prototype 工程分别按公开 preview/有效计费秒数核对实际 Food 扣减，并独立核对日常生产超过 FoodCapacity 的封顶损失；未改生产公式、资产或 FoodNetRate 语义。
- C 证据：`TestResults/food-ledger-final3.xml` 为 4/4 通过、0 失败/0 跳过、Unity 退出码 0；既有组合 `TestResults/economy-target-final13.xml` 为 5/5 通过、Unity 退出码 0。Food 账本不把 FoodNetRate 当作全系统净流。
- D：`UIButtonSoundManager.PlayRequested` 为强类型运行期观测点，在有效音效进入 `PlayOneShot` 前触发；新增 Editor 回归覆盖成功一次、批量一次、音量 `.5` 对应 AudioSource `.21`、静音为 0、静音仍保留语义请求。EditMode `TestResults/audio-target-final6.xml` 4/4 通过、Unity 退出码 0。
- D 全量验证：`TestResults/EditMode-final3-20261007.xml` 758/758、`TestResults/PlayMode-final3-20261007.xml` 37/37，均 0 失败/0 跳过、Unity 退出码 0；PlayMode 日志无本轮游戏 Error/Exception。研究完成/时代突破、失败零声、离线抑制由现有强类型事件与 `suppressCompletionAudio` 路径承载；未新增专门 UI 音频 PlayMode 夹具，避免为测试硬造生产层级。
- 兼容性修正：`UIButtonSoundManager` 在 EditMode 延迟初始化 AudioSource/合成片段，`DontDestroyOnLoad` 仅在运行态调用，避免测试环境误报；`dotnet build Kingdom.DeveloperTests.csproj --no-restore --nologo --verbosity quiet` 0 警告/0 错误。
- 文档同步：`outputs/动工策划案.md`、`outputs/声音反馈策划.md`、`outputs/ToDoList.txt` 已更新为 C2/D 完成口径；人工试听仍是外部体验确认，不作为本轮自动验证项。E/F/G 仍只核对定案问题，未实施。
- Console 收口：`tools/codex/analyze-unity-log.ps1 -Path Logs/PlayMode-final3-20261007.log -AsJson` 仅报告 1 条 Unity Licensing access-token 环境噪声；未发现游戏级 Error/Exception/MissingReference。该噪声不改测试结果，也不终止用户 Editor。

## 2026-10-07 A-D 音频真实运行补强与最终收口

- D 真实运行补强：`Assets/Tests/PlayMode/KingdomPlayModeTests.cs` 新增 `AudioFeedback_RealUiAndResearchPathsEmitOnlyCommittedResults`，从 SampleScene authored UI 实际建造成功、资源归零失败、拆除，以及真实研究完成和 `TickOfflineForEditor` 路径观测 `UIButtonSoundManager.PlayRequested`；验证成功结果声一次、失败零声、研究/时代结果声一次、离线完成静默。
- 首次完整套件 `TestResults/PlayMode-final4-20261007.xml` 为 38 总数、37 通过、1 失败；失败是完整套件先前场景留下的持久 `UIButtonSoundManager` 未重新订阅新场景 `ResearchManager`，位置为音频研究完成计数断言，不弱化断言、不将该 XML 计为通过。
- 修复：`Assets/Resources/Script/Manager/MusicManager.cs` 为持久音效对象保存当前订阅的 `ResearchManager`，每次初始化/复用时解绑旧对象并绑定当前场景对象；`OnDestroy` 对当前订阅安全解绑。无反射、无 State/存档合同变化。
- 修复后定向 `TestResults/audio-playmode-final2.xml`：1/1 通过，Unity 退出码 0；全量 `TestResults/PlayMode-final5-20261007.xml`：38/38 通过、0 失败、0 跳过，Unity 退出码 0。`tools/codex/analyze-unity-log.ps1 -Path Logs/PlayMode-final5-20261007.log -AsJson` 仅报告 1 条 Unity Licensing access-token 环境噪声，无游戏级 Error/Exception/MissingReference。
- 本轮源码变更后的真实 Unity 编译：`Logs/codex-compile-final2-20261007.log`，ExitCode=0。既有全量 EditMode `TestResults/EditMode-final3-20261007.xml` 仍为 758/758 通过；D 定向 `TestResults/audio-target-final6.xml` 为 4/4 通过。
- 跨场景订阅修复后的相关 EditMode `TestResults/audio-editmode-final2.xml` 为 4/4 通过，Unity 退出码 0；`dotnet build Kingdom.DeveloperTests.csproj --no-restore --nologo --verbosity quiet` 为 0 警告/0 错误，定向改动 `git diff --check` 通过。
- 文档已同步 `outputs/动工策划案.md`、`outputs/声音反馈策划.md`、`outputs/ToDoList.txt`：D 证据改为包含真实 UI/研究/离线音频测试与 PlayMode 38/38；保留首次失败 XML 作为诊断历史。人工试听仍由外部体验确认，E/F/G 只核对定案问题，未自动实施。
