# P0-02 / E08 升级链合同审定交接

## 任务与状态

用户要求「审查 outputs 并着手开启修缮任务」。本批为**只读审查 + 有界静态核算 + 一次运行证据尝试**，实施范围仅限新增审查/审定文档与归档恢复件。

- 已完成：outputs 文档审查；已落盘修缮（D09/D12/P0-04）的静态复核；P0-02 E08 章程合同审定；章程原文从 git 历史恢复归档。
- 未完成：**运行验证**（未执行真实 Unity 编译，未运行 EditMode/PlayMode/Console）；D04/D11 未实施；悬空引用未收口。
- 不关闭项：P0-01（D09/D12 待运行）、P0-02（待用户拍板甲/乙案）。

## 起始状态与保留工作

- 起始 HEAD `a85f0e7`，接手大量未提交改动（P0-04 存档 v9、D12 里程碑、D09 PlayMode、指导体系、音效与插画交付），**全部保留，未回滚**。
- 本任务拥有的文件：`outputs/Kingdom-outputs审查与修缮开工-2026-09-15.md`、`outputs/Kingdom-P0-02-E08升级链合同审定-2026-09-15.md`、`.codex/archive/recovered-20260915/`、本交接、`.workbuddy-ai/memory/2026-09-15.md` 追加段、`tmp/verify_e08*.py` 与 `tmp/recovered/`、`TestResults/EditMode-D12-20260915-run4.log`、`run5.log`、`tmp/env-probe.txt`、`tmp/proc-probe.txt`。
- 未触碰：游戏代码、资产、数值、ID/GUID/meta、存档结构、Scene/Prefab、`ToDoList.txt`、`DeepAudit.txt`、历史 outputs。

## 文件与行为变更

1. `outputs/Kingdom-P0-02-E08升级链合同审定-2026-09-15.md`（新增）：合同审定结论。核心三条——§3.1 的 Building.cs GUID 写成 Research.cs 的 GUID（照抄实施必失败）；章程漏记 WireMill 的 CopperWire 1.4/s 与 BuildingMaterialsComplex 的 Ceramic 0.4/s，其中 CopperWire 为单源且新厂不产，叠加「新层级解锁后旧层级停建」构成阻断级断链；4x 倍率与「2-4 座」目标不自洽（实测需 8/6/7 座）。
2. `outputs/Kingdom-outputs审查与修缮开工-2026-09-15.md`（新增）：outputs 审查、已完成修缮静态复核、Unity 尝试实测记录、下一步。
3. `.codex/archive/recovered-20260915/CONTENTADVISE/`（新增归档，16 个文件）：由 `git show a85f0e7^:CONTENTADVISE/<文件名>` 全量恢复。**只读恢复，未覆盖现有文件，未把 CONTENTADVISE 重新纳入活动目录。**
4. 悬空引用加注（四处，仅加括号说明，未改原有正文结论）：`ToDoList.txt`（P0-02 当前事实、末尾证据清单）、`outputs/ToDoList.txt`（同步副本）、`docs/repository-map.md`、`docs/balance/balance-model.md`。同时按本轮核算结果，在 ToDoList P0-02 的 b 项下补了 CopperWire（阻断级）与 Ceramic（容量回退级）的风险分级说明。
5. `Library/SourceAssetDB`、`Library/ArtifactDB` 与两个 `*-lock`：运行诊断期间曾可恢复改名，**均已改回原名**；`Temp/UnityLockfile`（陈旧）已删除。当前 Library 与接手时一致。
6. `tools/codex/audit-upgrade-continuity.py`（新增，只读）：建筑升级边「净产出连续性」审计，回答 D05/D08 的验收问题。只解析 `Assets/Resources/Datas/**/*.asset` 与其 `.meta`，不写文件、不访问 Unity。用法 `python tools/codex/audit-upgrade-continuity.py .`。如不需要可整文件删除，不影响其他工具。
7. 无经济字段变更；无 ID/GUID/序列化变化。

## 验证执行

### 静态核算（本轮执行）

- 用托管 Python 3.13.12 解析 `Assets/Resources/Datas` 的 `.asset`/`.meta`：三工厂净产出、单源判定、`SiriusResourceBelt.campaignResourceRatesPerSecond`、三工厂 `requiredResearch`、章程提议的 Spacer 研究与新建筑存在性。脚本：`tmp/verify_e08b.py`~`verify_e08e.py`。
- 关键实测值：净产出单源资源 = CopperWire(WireMill 1.4)、Electronics(WireMill 0.35)、Machinery(MachineFactory 1.0)、Engine(MachineFactory 0.2)、Glass(BuildingMaterialsComplex 1.2)、Concrete(BuildingMaterialsComplex 2.0)；Ceramic 有 3 个净产出者（2.4/0.4/1.0）。战役需求 Electronics 11.2/s、Engine 4.8/s、Machinery 26/s。
- **全库升级边连续性审计**（`tools/codex/audit-upgrade-continuity.py`）：37 条既有升级边中**只有 1 条**净产出集合减少——`CharcoalKiln → IndustrialCarbonizationRetort` 丢 Coal 0.8/s，而 Coal 另有 MechanizedCoalMine、CoalMine 两个净产出者。**独立复核了 DeepAudit 第三节的判断：现有内容不存在由既有升级边造成的断链。**
- 全库 **12 项单源资源**当前全部没有升级后继（故今天可无限补建）：Machinery/Engine/Composite ← MachineFactory；Electronics/CopperWire ← WireMill；Glass/Concrete ← BuildingMaterialsComplex；Aluminum ← AluminumSmelter；Nickel ← NickelRefinery；PhantomAlloy/PhantomWeave ← PhantomMaterialsFabricator；PhaseMaterial ← PhaseMaterialSynthesisArray。
- E08 合同完整度不对称：OrbitalMachiningComplex 完整继承 MachineFactory 三项净产出；OrbitalWireWorks 漏 CopperWire（阻断级）；OrbitalBuildingMaterialsWorks 漏 Ceramic（容量回退级）。
- 三工厂当前合计定向倍率（加法叠加）：MachineFactory 2.16、WireMill 1.35、BuildingMaterialsComplex 1.75；据此当前实际需约 24 座 WireMill + 13 座 MachineFactory。
- `Building.cs.meta` GUID = `c1df40775f864874083816b75d1cf5ba`；`Research.cs.meta` GUID = `755e6eabd0fdcbc4c92dd540b39a0ec9`（章程写错）。
- 源码语义核对：`BuildingManager.CanConstructNew`(:431-434) + `IsHighestUnlockedChainTier`(:499-508)；`SimulationManager.ManualTick`(:160) 不读 `running`；`ResearchState` 构造 `costPaid = !HasPositiveResourceRequirement`；`ResearchManager.TryBuild` 前置校验顺序。
- 引用完整性：根 AGENTS 与主技能引用的 17 个路径全部存在；`.agents/skills/` 三个技能齐全。

### Unity 运行（本轮尝试，**未取得结果**）

- `TestResults/EditMode-D12-20260915-run4.log`：`-batchmode`（去掉 `-nographics`）+ 补齐标准环境变量；停在 `Application.AssetDatabase Initial Refresh Start`，`Library/ArtifactDB` 自 21:20 起无写入。无 XML。
- `TestResults/EditMode-D12-20260915-run5.log`：将 `SourceAssetDB`/`ArtifactDB` 可恢复改名后重试；Unity 输出 `Rebuilding Library because the asset database could not be found!`，此后约 8 分钟未创建新 DB 文件、日志零增长。无 XML。
- `TestResults/Unity-CompileCheck-20260915-run6.log`：再将两个 `*-lock` 可恢复改名、以 `-batchmode -quit`（只编译/刷新、不跑测试）重试；仍在 `Application.AssetDatabase Initial Refresh Start` 停住。无编译结果。
- 结论：**未执行真实 Unity 编译。** 未运行 `ProgressionMilestoneRecorderTests`、3 项新档用例、Overview、全量 EditMode、任何 PlayMode 或 Console。旧 XML 不算本轮结果。
- 已排除的假设（4 项）：`-nographics`（run4 与带该参数的历史失败表现相同）、资源库损坏（run5 空库重建同样卡住）、缺标准环境变量（补齐后无改善）、陈旧 DB 锁文件（run6 同样卡住）。同时排除 `Assets` 侧内容因素：无重复 GUID、无非 ASCII 文件名、无符号链接、`Packages/manifest.json` 无 `file:`/`git`/网络引用、D: 为本地 NTFS。判断为本机批处理启动路径的环境性问题。
- 对照：`%LOCALAPPDATA%\Unity\Editor\Editor.log` 显示**交互式** Unity 在 12:20 编译与导入成功（`Tundra build success (4.54 seconds)`）。
- 清理：已终止全部自有批处理进程；无 Unity 进程残留；未终止用户 Editor、未改全局环境/PATH/包、未绕过执行保护。

## 风险与证据限制

- **Unity 批处理阻断根因未定位**：阻断位于「决定重建」之后、创建 DB 之前，与 `-nographics`、环境变量、资源库状态均无关。建议改走交互式路径，不要继续重复批处理。
- E08 的座数结论是**需求下限**，未计巨构建造瞬时消耗、维护吞吐、效率下降、其他消费端与研究/工坊加成；反证条件已写在审定文档第 3 节。
- 章程 §1 表内三座建筑 GUID 与 `.meta` 一致，但 §3.1 的脚本 GUID 错误，两者不能混为一谈。
- ToDoList 把「漏陶瓷」与「漏铜线」并列同等风险，实测风险不对称（Ceramic 非单源），已在审定文档纠正。
- `CONTENTADVISE/` 为悬空引用（`ToDoList.txt` P0-02、`docs/balance/balance-model.md:95`、`docs/repository-map.md:57`），本轮未改这三处正文。
- 未解压指导合并 ZIP；未逐页审查 PDF；未重新试听音频；未运行内容闭包脚本、经济 parity 或模拟器。

## 下一具体行动

1. 在**交互式** Unity 中打开 `D:\GitHub\Kingdom`，确认当前程序集编译完成；用 Test Runner 跑 `ProgressionMilestoneRecorderTests`（应非零用例、零失败），再跑 `KingdomPlayModeTests` 的 `NewGameStartup_InitializesCoreRuntimeState`、`NewGameFirstTenMinutes_SimulationSmokeRemainsStable`、`NewGameCommands_BuildResearchAndReloadWithoutGrants`、`OverviewDevelopmentGuidance_IsReadOnlyAndUnique`；随后全量 EditMode 与相关非零 PlayMode + Console。
2. 需要用户确认：P0-02 选甲案（复用既有科研/工坊强化老厂，本轮建议）还是乙案（修好合同后的升级链）。甲案的可执行形态已写入审定文档第 8 节：现成通道为 `ProgressionModifierManager` 的四类建筑/资源定向倍率（加法叠加，总倍率 = `1 + Σ(valueᵢ - 1)`）；三工厂当前合计倍率为 MachineFactory 2.16、WireMill 1.35、BMC 1.75；把 Electronics 压到 8 座需新增 V≈3.65、压到 4 座需 V≈7.65，Machinery+Engine 压到 4 座需 V≈5.5，Glass 无需新增。建议在既有 Spacer 档工坊升级上追加（本仓库现有 83 项工坊升级），避免触碰 P1-06「先复核研究角色再加新节点」。
3. **乙案「完整继承」存在合同歧义**（续写补记）：章程未区分「名义 `production` 字段」与「净产出（扣维护自耗）」。审计工具输出的 CopperWire 1.4/s、Ceramic 0.4/s 是净产出；照名义字段复制会因新旧厂维护自耗结构不同而在净口径漂移，CopperWire 为单源资源，漂移为负即构成阻断级断链。已写入审定文档 §9.4，要求乙案前置新增一条「按净产出逐项相等（容差比较）」的合同条款，并要求 D06 定向加成归属也在净口径上给定论。
4. 若选乙案，实施前必修：改章程 GUID、新厂**按净产出口径**完整继承 CopperWire 1.4/s 与 Ceramic 0.4/s、按 §9.4 新增净产出相等条款、按需求重述倍率与座数、明确旧层级停建后的补建路径。
5. ~~收口悬空引用~~ **本轮已完成**：`CONTENTADVISE/` 全量恢复件在 `.codex/archive/recovered-20260915/CONTENTADVISE/`，四处引用已加注归档位置与复核命令。
6. 运行环境可用后：D04 工坊提交回滚覆盖 `Purchased` 与派生效果；D11 事务入口按稳定 ID 规范化。各做有界条件复现与最小修复，不重建事务框架。
