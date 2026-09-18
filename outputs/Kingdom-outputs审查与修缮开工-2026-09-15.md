# outputs 审查与修缮开工记录（第二轮）

- 日期：2026-09-15
- 基线：HEAD `a85f0e7` + 已有未提交工作树；原有改动全部保留，未回滚任何并行成果。
- 性质：只读审查 + 有界静态核算 + 一次运行证据尝试。**未执行真实 Unity 编译**，未运行任何 EditMode/PlayMode/Console。
- 与上一份的关系：`Kingdom-修缮启动与审查结论-2026-09-15.txt` 记录首批 D09 修缮；本文件是续接批次的审查结论与开工记录，不改写上一份内容。

## 一、outputs 文档审查

1. 两份当日进展文本（`Kingdom-修缮启动与审查结论`、`P0-01-D12-进展与下一步`）与 `.codex/handoffs/2026-09-15-p0-01-research-milestones-handoff.md` 口径一致，都明确写了「实现已推进、运行验证待完成」，没有把静态检查说成运行验收。
2. `DeepAudit.txt` 的 D01-D12 分级与证据级别（A/B/C）仍成立，本轮未发现需要推翻的分级。当前状态：D01/D02/D03 已被严格 v9 单主档决策取代（P0-04 已落盘）；D09/D12 已实现待运行；D04/D11 仍为条件风险；D05/D06/D07 归 P0-02/P0-03；D08/D10 未动。
3. **新发现：P0-02 的证据文件已不在工作树，属悬空引用。** `CONTENTADVISE/` 整个目录在 HEAD 提交 `a85f0e7`（“报告改进前”）中被删除，但以下位置仍指向它：
   - `ToDoList.txt` P0-02 的证据行与「当前事实」；
   - `docs/balance/balance-model.md:95`；
   - `docs/repository-map.md:57`。
   影响：P0-02 的「下一动作」无法从章程原文开始。**本轮已修复**：从 git 历史只读恢复 `CONTENTADVISE/` 全部 16 个文件到 `.codex/archive/recovered-20260915/CONTENTADVISE/`（未覆盖任何现有文件、未把该目录放回活动目录），并在三处引用旁加注归档位置：`ToDoList.txt`（P0-02 当前事实、末尾证据清单）、`outputs/ToDoList.txt`（同步副本）、`docs/repository-map.md`、`docs/balance/balance-model.md`。
4. 指导体系引用完整性核对：根 `AGENTS.md` 与主技能引用的 17 个路径（3 个规范技能、6 份 references/assets、8 份 docs/data 目标）**全部存在**，合并后的导航没有断链。
5. 历史交付（指导合并报告与 ZIP、Ultra 设计、猫国对比、音效审查、插画策划）均为快照或未批准提案，没有被误当作当前完成状态。本轮未解压旧 ZIP、未逐页审查 PDF、未重新试听音频。

## 二、已完成修缮的复核（静态）

### 2.1 引用完整性：通过

新增与改造的测试所引用的入口**全部存在且签名匹配**，不存在「引用了不存在的 API」这类会直接编译失败的问题：`SimulationManager.ManualTick`、`BuildingManager.TryBuild/GetMaxBuildable/States`、`ResearchManager.CanPayResearchCost/HandleResearchAction/TotalFinishedResearchCount`、`KingdomUIRoot.RefreshUI`、`SaveManager.LastLoadCreatedNewGame/KingdomSaveData`、`GameBootstrap.Completed`、`ExpantaNum.Abs`、`ResearchState.CostPaid/Status`、`ResearchActionResult.Started`、`BuildFailure.ResearchPrerequisiteIncomplete`、`SaveManager.StorySaveData.CompletedChapterIds`，以及各 Manager 的 `CaptureSaveData`。

### 2.2 语义核对：三处关键前提成立

- **暂停自动模拟后手动推进是有效的**：`SimulationManager.ManualTick` 不读取 `running` 标志，`SetRunning(false)` 只清空累积秒数。十分钟 smoke 的 `ManualTick(0.1)` 循环不会空转。
- **农场的失败枚举断言成立**：`TryBuild` 先执行 `ArePrerequisitesMet`，早于资源/领土/生产力校验；`Farm.asset` 的 `requiredResearch` 非空。因此锁定农场返回 `ResearchPrerequisiteIncomplete` 而非 `ResourceInsufficient`/`SpaceInsufficient`。
- **农业研究的 `Started` 断言成立**：`Agriculture.asset` 的 `prerequisites: []`，`BuildPrerequisiteBatch` 不会把别的前置排到活动位；其资源需求为单一资源 28。新增研究用例依赖的是公开命令与真实支付，不依赖反射。

### 2.3 D12 口径修复：静态语义成立

`ResearchState` 构造为 `costPaid = !definition.HasPositiveResourceRequirement`，即有偿研究在被选择前 `CostPaid` 为 false。`SampleResearchMilestones` 先 `if (!state.CostPaid) continue;`，所以 `FirstResearchPaid` 不会被「未选择的有偿研究」误触发；免费研究必须 `selected` 才记录。D12 要求的三项分离（Queued/Paid/Progressed）在静态语义上成立。

### 2.4 仍需运行才能定论的风险（本轮无法执行）

- `NewGameCommands_BuildResearchAndReloadWithoutGrants` 假设自然积累能在 600 秒预算内建起木屋、并支付农业的 28 资源；`GetMaxBuildable` 不覆盖科技/研究/空间/生产力校验，所以「可负担」不等于 `TryBuild` 必然成功。
- 十分钟 smoke 的 `CalendarDays` 增量 59-60 依赖「10 秒/天」的既有口径；若日历速率被调整，该断言会失效。
- `CaptureOverviewState()` 依赖 `TutorialManager.Current`、`WorkshopManager.Instance`、`GameManager.Sectors` 在 SampleScene 中就绪。

以上都是**待运行验证项**，不是已确认缺陷。

## 三、Unity 运行证据尝试（本轮实测，未成功）

目的：为 D09/D12/P0-04 取得当前源码的编译与测试结果。结论：**未取得**，但排除了三个假设并定位了阻断位置。

1. **环境变量假设（部分补齐，未解决）**：实测 Bash 与 PowerShell 工具环境都缺 `ComSpec`、`ProgramFiles`、`ProgramFiles(x86)`、`ProgramW6432`、`ProgramData`、`ALLUSERSPROFILE`、`APPDATA`（`PATHEXT` 仅为 `.CPL`）。本轮显式补齐后重试。
2. **`-nographics` 假设（已否定）**：对比 9/12 成功日志 `EditMode-20260912-run6.log`，成功那次命令行是 `-batchmode`（**无** `-nographics`），而 9/15 三次失败都带 `-nographics`。本轮去掉该参数重试（`run4`），**仍在 `Application.AssetDatabase Initial Refresh Start` 停住**；`Library/ArtifactDB` 自 21:20 打开后无任何写入。
3. **资源库损坏假设（已否定）**：将 `Library/SourceAssetDB`、`Library/ArtifactDB` 以可恢复改名方式移出后重试（`run5`），Unity 明确输出 `Rebuilding Library because the asset database could not be found!`，但此后约 8 分钟**未创建任何新的 DB 文件、日志零增长**。说明阻断发生在「决定重建」之后、创建数据库之前，与资源库状态无关。
4. **陈旧锁文件假设（已否定）**：把 `Library/SourceAssetDB-lock`、`Library/ArtifactDB-lock` 以可恢复改名方式移出后重试（`run6`，`-batchmode -quit` 只做编译/刷新），**仍在同一行停住**。至此四个假设全部排除。
5. **对照证据（关键）**：`%LOCALAPPDATA%\Unity\Editor\Editor.log` 显示 **交互式 Unity 在 12:20 编译与导入成功**（`Tundra build success (4.54 seconds)`，逐项 `Start importing ... in 0.003s`）。即：交互式路径可用，批处理路径在刷新前段卡住。
6. **结论**：批处理阻断与 `-nographics`、标准环境变量缺失、资源库状态、陈旧锁文件均无关，且发生在「决定重建」之后、创建数据库之前。同时排除了 `Assets` 侧的可疑内容：无重复 GUID、无非 ASCII 文件名、无符号链接、`Packages/manifest.json` 无 `file:`/`git`/网络引用、D: 为本地 NTFS。因此判断为**本机批处理启动路径的环境性问题**，不是项目内容问题。
7. 本轮的诊断产物：`TestResults/EditMode-D12-20260915-run4.log`、`run5.log`、`Unity-CompileCheck-20260915-run6.log`（均无测试 XML）。
8. **清理与安全**：已终止全部自有批处理进程；Library 缓存与锁文件已改回原名；已删除陈旧的 `Temp/UnityLockfile`；未终止用户 Editor、未修改全局环境/PATH/包/缓存、未绕过任何执行保护、未删除任何用户文件。

**建议**：批处理阻断未定位根因前，不再重复批处理尝试。改用交互式 Unity 让当前程序集编译并运行 Test Runner，是最短路径。

## 四、本轮实际修缮推进（P0-02）

按 ToDoList「P0-02 任何 E08 实施之前」的要求，完成 E08 升级链章程的合同审定，产物：

- `outputs/Kingdom-P0-02-E08升级链合同审定-2026-09-15.md`
- 恢复并归档章程原文：`.codex/archive/recovered-20260915/e08-single-source-upgrade-chain-spec.md`、同目录 `batch2-payback-and-duration-table.md`

三条经字段核算确认的结论：

1. **§3.1 的 GUID 错误会直接导致实施失败**：章程要求 Script 字段用 `755e6eabd0fdcbc4c92dd540b39a0ec9`，该 GUID 实际属于 `Research.cs`；`Building.cs.meta` 是 `c1df40775f864874083816b75d1cf5ba`。处理原则是改章程、不改既有 `.meta`。
2. **漏项 + 升级停建 = 阻断级断链**：章程只继承 WireMill 的 Electronics 0.35，漏了它同为净产出的 CopperWire 1.4；而 CopperWire 的净产出建筑**只有 WireMill 一个**。叠加 `CanConstructNew` → `IsHighestUnlockedChainTier`（新层级解锁后旧层级立即停建）后，解锁新厂会同时失去 CopperWire 的新建来源。
3. **4x 倍率与「2-4 座」目标不自洽**：按 `SiriusResourceBelt.asset` 的 `campaignResourceRatesPerSecond`（Electronics 11.2/s、Engine 4.8/s、Machinery 26/s）与章程草案基础值核算，需 8/6/7 座。结论是「章程缺可验证的倍率与规模前提」，不是「游戏现在一定缺供」。

**对上级文档的一处口径纠正**：ToDoList 把「漏陶瓷」与「漏铜线」并列同等风险。实测 Ceramic 有 3 个净产出者（AdvancedCeramicsPlant 2.4、BuildingMaterialsComplex 0.4、CeramicKiln 1.0，**合计 3.8/s**），**不是单源**；升级后是容量回退（合计 **3.8/s → 2.4/s** 且变为单源），不是断链。两者风险等级应分开：CopperWire 为阻断级，Ceramic 为容量回退级。

### 4.1 把结论推广到全部 37 条既有升级边

为回答 D05/D08 的验收问题（「每个阶段所有仍需副产物都有可达且可扩产的来源」），本轮把同一口径推广到全部 37 条既有升级边，并固化为只读工具 `tools/codex/audit-upgrade-continuity.py`（只解析 `.asset`/`.meta`，不写文件、不访问 Unity）：

- **37 条边中只有 1 条**净产出集合减少：`CharcoalKiln → IndustrialCarbonizationRetort` 丢失 Coal 0.8/s，而 Coal 另有 MechanizedCoalMine、CoalMine 两个净产出者。**独立复核了 DeepAudit 第三节的判断，现有内容不存在升级断链。**
- 全库 **12 项单源资源**（Machinery/Engine/Composite ← MachineFactory；Electronics/CopperWire ← WireMill；Glass/Concrete ← BuildingMaterialsComplex；Aluminum、Nickel、PhantomAlloy、PhantomWeave、PhaseMaterial）**当前全部没有升级后继**，这正是它们安全的原因；E08 会给其中三个建筑加上后继。
- E08 三座新建筑的合同完整度**不对称**：OrbitalMachiningComplex 完整继承了 MachineFactory 的全部三项净产出；OrbitalWireWorks 漏 CopperWire（阻断级）；OrbitalBuildingMaterialsWorks 漏 Ceramic（容量回退级）。修订成本低于推翻整案，但 CopperWire 必须先修。

### 4.2 补充：乙案「完整继承」的合同歧义（本轮续写）

本轮继续核对乙案前置条件时发现一个文档层面的歧义，虽不改变「先走甲案」的结论，但会直接决定乙案实施者是否踩坑：

- 章程说新厂「完整继承」老厂产出，但没说清继承的是**名义 `production` 字段**还是**净产出**（名义扣除 `consumption`/维护自耗）。审计工具输出的 CopperWire 1.4/s、Ceramic 0.4/s 均为**净产出**。
- 若实施者照名义字段复制，而新旧厂维护自耗结构不同，则「完整继承」在净口径下不成立。CopperWire 恰为单源资源，漂移为负即直接构成第 4 节的阻断级断链。
- 已写入审定文档 §9.4：乙案前置须新增一条「新厂对老厂全部净产出资源的**净产出速率**逐项相等（容差比较，不做精确相等）」的合同条款；同时 §6 验收口径第 6 条（D06 定向加成归属）也必须在净口径上给出结论——若加成只作用于老厂，升级后新厂净产出低于继承前老厂，对单源资源同样是断链风险。

这条是**文档条款层面的缺口**，不是已确认的运行时缺陷；它同时说明为什么「按净产出定义验收」比「按字段复制定义验收」更安全。

## 五、未做与限制

- 未执行真实 Unity 编译；未运行 EditMode/PlayMode/Console；未做缺陷注入红灯验证。
- 未修改游戏代码、资产、数值、ID/GUID/meta、存档结构、Scene/Prefab。
- 未实施 D04/D11 的代码修复：这两项需要运行验证才能证明修复有效，在运行环境不可用时实施只会叠加未验证改动。
- 未修改 ToDoList、DeepAudit 或任何历史 outputs；未解压指导合并 ZIP；未逐页审查 PDF；未重新试听音频。
- 未新增或删除 `CONTENTADVISE` 文件；恢复件只落在归档目录。
- 内容闭包脚本、经济 parity、模拟器均未运行；本轮全部数值结论来自 `.asset` 文本与 `.cs` 源码的静态核算。

## 六、下一步（按优先级）

1. **[最高] 取得运行证据**：交互式打开 Unity，确认当前程序集编译完成；用 Test Runner 依次跑 `ProgressionMilestoneRecorderTests`、3 项新档用例、Overview，再全量 EditMode 与相关非零 PlayMode。批处理根因未定位前不再重复批处理。
2. **P0-02 拍板**：在「复用既有科研/工坊强化老厂」与「修好合同后的升级链」之间决策；本轮建议先走前者，并把后者降级为「甲案实测后仍不足时再评估」。若最终选乙案，前置条款已按 4.2 补全（净产出口径继承 + D06 归属）。
3. ~~**悬空引用收口**~~ **本轮已完成**：`CONTENTADVISE/` 全量恢复件落在 `.codex/archive/recovered-20260915/CONTENTADVISE/`，并在 `ToDoList.txt`、`outputs/ToDoList.txt`、`docs/repository-map.md`、`docs/balance/balance-model.md` 四处引用旁加注「已在 a85f0e7 删除 + 归档位置 + 复核命令」。未把该目录放回活动目录。
4. **D04/D11**：运行环境可用后各做一次有界条件复现与最小修复，不重建事务框架。
5. **P0-03 / P1-01 / P0-05**：按原计划保留，真人首局与 P40 Pro 设备门另做真实验收。

本批状态：审查完成，P0-02 合同审定完成，运行验证仍缺。D09/P0-01/P0-02 均不关闭。

## 七、outputs 其余文档逐份审查（本轮补完）

本轮把 outputs 目录全部文件读完或核对到可用结论，逐份结论如下。所有「已核对」均为只读静态核对，**不构成运行验收**。

### 7.1 DeepAudit.txt（已通读，D01-D12 + N01-N08）

- 12 项发现的分级（A/B/C）与证据级别自洽，本轮未发现需要推翻的分级。
- **独立复核其中 3 条，全部成立**：D05（升级替代语义，见第 9.3 节）、D07（大学与深空观测站的字段与倍率，见 7.5）、DeepAudit 第三节「37 条升级边中唯一集合减少为 CharcoalKiln→Coal 且煤另有矿山链」（见第 9.1 节，本轮独立复算一致）。
- N01-N08 的反证清单仍成立，没有需要升级为缺陷的项。

### 7.2 ToDoList.txt（已通读，含第十一节）

- 结构与优先级自洽；「最先开工的五件事」与 M0-M3 里程碑放行条件清晰。
- **规模盘点核对通过，两处差异均可解释**：运行时 C# 现为 **74**（文档记 75，差 1 = P0-04 按 v9 决策删除 `RetiredDefinitionMigration.cs`）；EditMode 测试文件现为 **41**（文档记 40，差 1 = 新增 `ProgressionMilestoneRecorderTests.cs`）。建筑 66、研究 129、工坊 83、资源 40、星区 10、剧情资产 19、PlayMode 5、PNG 132 全部与文档一致。
- **发现第二处悬空引用**：第十节证据索引 A 列出 `.agents/skills/kingdom-content-expansion/SKILL.md`，该技能已在指导体系合并中删除（`.agents/skills/` 现只有 project-dev、economy-simulation、ui-redesign 三个）。索引中其余 17 个路径全部存在。
- P2-05 已经预见到这类问题（「收敛文档与验收入口，防止下一轮继续做旧任务」），但索引本身尚未收敛。

### 7.3 Kingdom-Kittens-Overview.md / Kingdom-Kittens-Comparison.md

- 交付摘要的盘点数字与 7.2 一致；Comparison 给出 P0-A/P0-B、P1-A~P1-D、P2-A/P2-B 八项建议与「明确不建议照搬」清单（普通资源仓储上限、猫民岗位、转生/挑战）。
- 建议本身与项目护栏（Food 唯一库存上限、无 workforce）不冲突，属**未批准提案**，本轮不据此改任何内容。
- 边界声明诚实：未改源码/资产，661/661 是历史结果而非本轮重跑。

### 7.4 Kingdom-音效审查与改进建议-2026-09-15.md（+ PDF）

- **核对 2 条关键事实，全部成立**：
  - `Assets/Musics/` 下只有 `PMusic`（56 首 OGG），`Assets` 内无任何 `.wav`/`.mp3` → 「未发现独立导入的音效素材」成立；
  - `KingdomUIRoot.AuthoredRows.cs:190-193` 与 `:205-209` 确实**先** `UIButtonSoundManager.Play(Purchase/Sell)` **再**调用 `PerformBuildingAction`/`DeconstructBuilding` → 「点击声不等于成功声」成立，即建造失败也会响购买音。
- 报告已明确「未实施音效改造」「未执行真实 Unity 编译」，定位正确：这是提案，不是已完成工作。

### 7.5 Kingdom_Ultra_SF_Design_2026-09-14.md、Kingdom_Ultra_三体启发审查与路线建议_2026-09-13.html（+ PDF）

- 结构完整（U00 授权与证据标签 → U01 现状 → U02 六部作品的借鉴边界 → U03 定位与路线 → U04+ 机制），并带有「对上一轮建议的修正」一节，自我纠偏意识良好。
- 与 ToDoList「暂不纳入本版：Ultra/Archotech 新内容」一致，属**冻结中的提案**。本轮未逐条复核其机制细节，也未解压或对照历史 PDF。

### 7.6 插画三份（Kingdom_十八章剧情插画策划、Kingdom_三张定调插画深化制作书、Kingdom_剧情插画_原文与条件附录）

- 事实核对通过：剧情资产 19 份（18 章 + 1 档案）、`Assets` 内 PNG 132 张，与附录/策划的盘点一致；附录含逐章原文、解锁条件与「输入文件内容指纹」，可复核性较好。
- 三份都在显式区分「事实 / 提案 / 禁止推断」，并把外观、色板、角色造型、画幅标为**待确认提案**；未生成新插画、未改游戏资产。
- 本轮未逐张审美验收、未做浏览器截图，也未验证 36 段提示词的出图效果。

### 7.7 指导体系三件（Kingdom-Guidance-Consolidation-20260914.md、-Verification.json、-Integration-20260914.zip）

- 报告与 1,736 文件白名单巡检的结论仍成立；**本轮核对主技能与根 AGENTS 引用的 17 个路径全部存在**，合并后的导航没有断链。
- Verification.json（434 KB）本轮只核对结构与结论节，未逐条重放；ZIP 为历史快照，按报告要求**未解压、不覆盖当前工作树**。

### 7.8 仍保留的历史交付

`Guidance-Consolidation-Tests-20260913.txt`、`Guidance-Verification-20260914.txt`、`Kingdom-Guidance-Consolidation-20260913.txt`、`Probe-Fixture-Cleanup-Manifest-20260913.json` 已在工作树中处于删除状态（此前经用户确认入回收站）。旧 PDF 与 1 MB 级 HTML 未逐页审查。

## 八、下一步最需要做的事（按优先级）

### 第 1 位｜拿到 M0 的基线证据（其余全部工作的前置）

这是当前**唯一真正卡住整个项目**的事项。M0 的放行条件是「当前编译与相关测试有证据」，而现在：

- P0-01/D09/D12、P0-04/v9 存档、P0-02 审定全部处于「实现已推进、运行未验证」；
- 上次可用的 Unity 结果仍是 **9/12 EditMode 661/661** 与 **9/8 PlayMode 33+1**，都早于 v9、D12、D09 与美术改动；
- 本机批处理已四次排除（`-nographics`、环境变量、资源库、锁文件），**交互式路径可用**（12:20 日志有 Tundra build success）。

**具体动作**：交互式打开 `D:\GitHub\Kingdom`，等编译完成，然后按此顺序跑 Test Runner：

1. `ProgressionMilestoneRecorderTests`（应非零用例、零失败）——验证 D12；
2. `KingdomPlayModeTests` 的四项：`NewGameStartup_InitializesCoreRuntimeState`、`NewGameFirstTenMinutes_SimulationSmokeRemainsStable`、`NewGameCommands_BuildResearchAndReloadWithoutGrants`、`OverviewDevelopmentGuidance_IsReadOnlyAndUnique`——验证 D09；
3. 全量 EditMode + 相关非零 PlayMode + Console——验证 P0-04/v9 未回归。

拿到结果后，M0 才有可能放行；在此之前不要再堆新的未执行测试。

### 第 2 位｜P0-02 拍板并落地甲案

审定已完成（含第 8 节的倍率算术与第 9 节的全库复核）。**建议选甲案**：只加倍率、不加建筑，CopperWire 与 Ceramic 随倍率一起放大、不断链，旧厂保持可建。落地形态已给到具体数值（Electronics 压到 8 座需新增 V≈3.65；Machinery+Engine 压到 4 座需 V≈5.5；Glass 无需新增），且建议挂在既有 Spacer 档工坊升级上，避开 P1-06 的限制。

若选乙案，四项合同必修项已列出；其中 **CopperWire 为阻断项**。

### 第 3 位｜P0-03 三悬案已可收窄到「只剩实测」

本轮把 P0-03 的两项从「悬案」推进到「字段已确认，只缺运行时净流」：

- **B 深空中继站**：`DeepSpaceRelay.asset` 实测 `logisticsProductionRate 25`、`logisticsConsumptionRate 48`（净 −23/s）、`fleetPowerGranted 100`、`productivityConsumption 680`、耗电 150、耗粮 2.5；而 Description 写的是「维持跨星区行动所需的**物流调度**与舰队支援」。**文案与字段方向相反**——这是需要定案的实质矛盾，不只是数值疑点。
- **C 研究建筑**：字段与倍率**全部核对通过**——University RP 250 / 生产力 60 / 领土 12，四个定向倍率 1.10/1.18/1.05/1.25（实测来源：ElectricalInstrumentation、LaboratoryGlassware、ElectricalCommunication、ModernUniversity）；DeepSpaceObservatory RP 220 / 生产力 620 / 领土 440 / 电 160 / 物流 40，三个倍率 1.35/1.20/1.15（AutonomousSurveyDrones、DeepSpaceSurvey、OrbitalEngineering）。按加法口径复算 395 vs 374，与 D07 完全一致。**结论仍是角色取舍，不是数值错误。**
- **A 生产力**：需要真实布局数据，仍待实测。

### 第 4 位｜两个「最小且明确」的修复（已定位，未实施）

这两项都是**验证后即可直接改**的小修复，不需要新的设计决策：

1. **`MatterStateControlTheory` 文案与效果不一致**（P1-06 已指出，本轮实测确认）：Description 写「降低建设损耗**并提高拆除返还**」，实际效果只有 `Type 5`（`GlobalConstructionMultiplier`）value 1.12。经 `BuildingManager.GetConstructionCostMultiplier` 确认该字段是「效率」并取 `1/efficiency`，即 1.12 = **建造成本降低 12%**（与「降低建设损耗」相符），但**没有任何 `DeconstructionReturnRate`（Type 22）效果**，「提高拆除返还」是空承诺。建议按 P1-06 的方向让文案如实描述剩余效果，而不是为保文案加一个高返还效果。
2. **ToDoList 第十节证据索引仍列已删除的 `kingdom-content-expansion` 技能**：按 P2-05 收敛入口时一并处理，或改指向 `kingdom-economy-simulation`。

### 第 5 位｜音效第一批（可选，且必须先解决触发时机）

音效报告已把「先修触发时机、再加素材」讲清楚，其中**「点击声不等于成功声」已由源码核实**（按钮先响再执行）。若要做第一批（独立音效音量 + 去重 + 研究完成/时代突破/建造与工坊结果声），必须先让结果声等待 Manager 明确成功返回，并处理离线补响与事务回滚后误响。素材目前不存在，需要另行准备。

### 明确不建议现在做

- 不再重复 Unity 批处理尝试（四个假设已排除）。
- 不在 M0 放行前实施 E08 任何一案的资产改动、不在无运行证据时改数值。
- 不因 Kitten/Ultra/插画提案去扩内容、加系统或改存档结构；这些都是未批准提案。
- 不为「收敛文档」而改写历史 outputs 或旧审计；P2-05 只收敛入口指向。

