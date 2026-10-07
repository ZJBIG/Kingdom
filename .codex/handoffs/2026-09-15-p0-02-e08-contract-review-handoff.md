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

---

## 续写：2026-09-18（用户选定乙案 → 修订合同落盘）

### 决策记录

- 用户对 P0-02 选 **乙案：升级链换厂（章程原案）**，但要求**先修完 4 项合同缺陷**再实施。
- 用户对 Unity 运行证据明确表示「**我先不管，继续做静态可推进的项**」。故本批仍为只读静态，未触碰 Unity。

### 本批产出

1. **`outputs/Kingdom-P0-02-E08乙案修订合同-2026-09-18.md`（新增，可复核合同）**：把上一批的 4 项必修缺陷逐条落成可执行修订条款。核心内容：
   - **修订条款 1**：新建筑 `m_Script` 固定 `{fileID: 11500000, guid: c1df40775f864874083816b75d1cf5ba, type: 3}`；改章程不改 `.meta`。
   - **修订条款 2/2b**：新厂须完整继承老厂正净产出；OrbitalWireWorks 必须**同时**产 Electronics 1.4 + CopperWire（≥1.4），OrbitalBuildingMaterialsWorks 必须**同时**产 Glass/Concrete/Ceramic（≥0.4）。
   - **修订条款 3/3b**：4x 与「2-4 座」不自洽，须择一——(a) 抬到约 10x，或 (b) 承认 6-8 座；且必须用运行时净流验证。
   - **修订条款 4/4b**：显式**接受停建**（更正章程「老厂仍保留边际价值」的错误表述）；D06 的 12 项定向加成**不跟随换厂**，须补偿或显式声明下降。
   - §5 实施规格（新建筑清单、净产出硬约束、YAML 键序骨架、老厂侧唯一改动）、§6 验收口径、§8 待确认两项决策。
2. **本交接续写**、`.workbuddy-ai/memory/2026-09-18.md` 追加。

### 本批新核验（相对上一批的修正与新发现）

- **修正**：上一批称三座解锁科研「无反向依赖」是基于较小检索面的印象。本批全库反向引用扫描结果：DISII 被 1 项引用、OPT 被 4 项、**OE 被 13 项**（含 OPT、DeepSpaceShipbuilding、OrbitalVacuumMetallurgy）。**这是依赖方向向上游的引用，不构成环**，作为新建筑 `requiredResearch` 仍安全。
- **新发现**：三座解锁科研**均不携带 `Type 19/20/21` unlock 效果**；全库 19/20/21 恰 3 个（HomeSystemSurvey/DeepSpaceFleet/InterstellarNavigation）。故建筑物 `requiredResearch` 是**纯闸门引用**，不改变三座科研既有效果语义——章程 §2「复用既有 Spacer 科研」的写法可用。
- **新证据**：DISII 前置闭包**含 `DeepSpaceShipbuilding`**（闭包 58 项），证明造船线在闸门上游，**无自锁**。
- **量化阻断项**：全库 CopperWire 净产出非零者仅 3 个——WireMill **+1.4（唯一正）**、MachineFactory −0.3、OrbitalSolarArray −0.03。Electronics/Machinery/Engine/Composite/Glass/Concrete 同样各仅 1 个净正源。
- **消费端面比章程假设大得多**：`OrbitalResourceExtractionArray` 单座消费 Coke 14/s、Chemical 2.4/s、Lubricant 2.0/s；`InterstellarTheoryNexus` 单座消费 Electronics 1.2/s。净流验证必须纳入。

### 独立复核（两个并行只读批次，均从源码重算）

本合同承载性断言全部 **CONFIRMED**，无一处数值偏差：三厂净产出、单源判定、Ceramic 3.8/s、战役需求 11 值、五个 GUID、`ValidateResourceFlowDefinitions` 抛点（`Building.cs:149-174`，抛于 `:168-172`）、升级取代机制、12 项定向加成（13 条效果）、加法叠加与三厂合计倍率 2.16/1.35/1.75、三座科研 guid/效果/无 19-21、`Rebuild` 重放且产率类无 TechLevel 门控（门控只在 `:295` `MilitaryMultiplier` 分支）。

复核纠正初稿两处陈述性细节：闭包规模 46→**58**；`IsHighestUnlockedChainTier` 行号 499-508→**498-509**（`CanConstructNew:431-435`、`BuildingTierSuperseded:606-610`）。已在合同 §9 记录复核矩阵。

### 风险与限制

- **未创建任何 `.asset`**，故不存在可运行的升级边；本批全部结论是**定义级静态复核**，不构成 Unity 运行验收。
- **未执行真实 Unity 编译。** 本批未触及 `.cs` 文件。
- 座数结论仍只是需求下限；倍率定档与 D06 补偿方案**待用户确认**后才进实施批次。

### 下一具体行动

1. **等用户在两处决策上拍板**：(a) 倍率采「约 10x / 2-4 座」还是「承认 6-8 座」；(b) D06 采「补偿使净产出不下降」还是「显式声明允许下降并列出量」。
2. 确认后实施批次：按合同 §5 创建 3 个 `.asset` + `.meta`（Spacer 目录、`m_Script` 用正确 GUID、raw gen/cons 不相交）、改 3 处 `upgradeTo`。
3. 跑 `python tools/codex/audit-upgrade-continuity.py .` 与 `tools/codex/content-closure-check.ps1`，确认三条新边不出现净产出集合减少。
4. Unity 侧（用户解除暂缓后）：编译 + EditMode + 相关非零 PlayMode + Console；六态回归按合同 §6。
5. 仍未收口：Unity 批处理阻断根因（本机环境性，建议走交互式，勿重复批处理）；D04/D11 未实施。

---

## 续写：2026-09-19（两项决策拍板 → 乙案实施批次落盘）

### 决策记录（用户本轮确认）

- 修订条款 3：**选 3(b) 保持约 4x、承认 6-8 座**；用户附加硬约束：**「不希望游戏速率平衡被打破，时代越往后玩的时间应该要越久」**。
- 修订条款 4b：**选「基值补偿即满足，不加新定向加成效果」**。

### 节奏论证（本轮新增证据，支撑上述选择）

- 全库 37 条既有升级边**本就跨时代**（MechanizedLumberyard/PlantingField→OrbitalAgroecologyArray、University/Library→DeepSpaceObservatory、MetalSmelter/SteelForge→IndustrialMetalSmelter、CharcoalKiln/CokeOven→ICR 等）；典型加工链边产出比 **2.5-6.8x**（batch2 §1 全表：OilRefinery→IPC 5.3x、CoalMine→MCM 4.9x、MetalSmelter→IMS 5.0x、CeramicKiln→ACP 2.5x），**4x 落在既有规律带内**。balance-model §2 的「时代关键升级 ×1.5-2.5」约束的是**研究效果倍率**，「解锁新建筑」属「新生产方式」，不受该带限制。
- 「越往后越久」的既有承载轴（文档证据）：研究墙（TL3≈80min vs TL4≈11.4 天，batch2 §2.2）、建筑几何成本、时代门 ×1.5 减速、TL4 加工建筑回本暂定带 15-40min（时代折算，balance-model §8，比老厂 5.2-14.5min 绝对更慢）。
- 4x（对比 10x）→ 覆盖战役需 6-8 座 → 扩产坡道更长；新厂 costGrowth **1.24 > 老厂 1.20** → 升级链内递增变陡。两者均与用户节奏约束**同向**。
- 4b 基值补偿核算（新/老厂含 D06 加成有效值，6 项全升）：Machinery 4.0>2.16、Engine 0.8>0.432、Composite 1.8>0.972、CopperWire 5.6>1.89、Electronics 1.4>0.4725、Glass 4.8>2.1、Concrete 8>3.5、Ceramic 1.6>0.7。**换厂净产出不下降成立，无需任何新加成效果。**

### 本批实施内容（6 新文件 + 3 单行改动）

新建筑均在 `Assets/Resources/Datas/Building/Spacer/`，`m_Script {fileID:11500000, guid:c1df40775f864874083816b75d1cf5ba, type:3}`，TechLevel 4，`upgradeTo {fileID:0}`，`requiredWorkshopUpgrades: []`（合同 §5.1「待定」→取「无，走研究闸门」），其余能力字段全 0：

| 新建筑 | .meta guid | 解锁科研 | gen/s | cons/s | requirements | costGrowth | space | 生产力耗 | 电力 | 物流 |
|---|---|---|---|---|---|---|---|---|---|---|
| OrbitalMachiningComplex | `238504a21c41471ca4c817ede986faba` | DeepSpaceIndustrialIntegration(`0a1b2c3d4e5f60718293a4b5c6d7e8f9`) | 机械4/发动机0.8/复合1.8 | 钛0.6/电子0.5 | 钛12000/机械9000/复合8000/电子5000/幻金600 | 1.24 | 440 | 320 | 220 | 18 |
| OrbitalWireWorks | `7c77eecf71c64c558a43342a2618efa2` | OrbitalPowerTransmission(`a9f3c7e1d5b248609c4e2f7a1b8d6350`) | 铜线5.6/电子1.4 | 钛0.3 | 钛8000/铜线6000/复合5000/玻璃3000 | 1.24 | 380 | 280 | 180 | 10 |
| OrbitalBuildingMaterialsWorks | `91dcce6626f54f46b212e145f15b2aae` | OrbitalEngineering(`b3e1a7c94d2f6081ab35c7e9d4f26018`) | 玻璃4.8/陶瓷1.6/混凝土8 | 钛0.4 | 混凝土15000/钛10000/复合6000 | 1.24 | 420 | 480 | 260 | 10 |

老厂侧唯一改动（`upgradeTo: {fileID: 0}` → 指向新 guid，仅此一行）：MachineFactory→`2385…faba`、WireMill→`7c77…efa2`、BuildingMaterialsComplex→`91dc…2aae`。成本/维护草案值取自章程 §2 表。

### 与章程草案的偏离（全部有据，记录防审计误报）

1. **OWW 维护去掉铜线 0.8/s、OBMW 去掉混凝土 1.5/s**（章程草案含）：`ValidateResourceFlowDefinitions`（Building.cs:149-174，抛点 :168-172）禁止同一资源同时出现在 raw gen 与 cons，而修订条款 2b 要求这两资源进 gen。校验器强制，非取舍。
2. **costGrowth 1.16→1.24**：同代加工同类实况（OrbitalCarbonizationComplex/OrbitalCryogenicPropellantArray/Shipyard/OrbitalAgroecologyArray/PhantomMaterialsFabricator 均 1.24，PMSA 1.17 为例外；老厂 1.20）。1.24 使升级链递增更陡，与用户节奏约束同向。
3. **spaceCost 440/380/420**：章程 300-500 带内，锚定同代 DSO 440 / OCC 380 / OCPA 420。
4. **productivityConsumption 320/280/480 = 老厂 80/70/120 ×4**（章程未给；保持单位产出的生产力消耗不变，升级价值来自成本/空间/维护，不白送效率）。
5. **logistics 18/10/10、foodConsumption 0**（章程沉默 → 继承老厂；同代物流范围 8-96）。
6. PhantomAlloy 建造费 guid = **`f718293a4b5c6d7e8f901a2b3c4d5e6f`**（已按 .meta 实测纠正——OCC 资产里 `a7f1…` 是 PhaseMaterial，易误判）。

### 静态回本提示（供运行时校准对照，本轮不调数值）

按 batch2 §0.2 折价静态估算，三厂草案成本首件回本约 **2.4-3.7h**，高于 TL4 加工暂定带 15-40min 约 4-15x（batch2 折价对 Spacer 深链失真，仅为方向估计）。方向为「更慢」，与用户节奏约束同向；最终按修订条款 3b 用运行时净流校准。若实测过慢，调节旋钮是**建造成本/维护**，不得砍继承产出（会重开 CopperWire 断链）。

### 验证执行

- `content-closure-check.ps1`：修改前 exit 0（Spacer Building **16/16**，Industrial 81/81、37/37、50/50，Spacer 47/47、46/46，Ultra 1/1，资源 40，Unreachable 全空）；修改后 exit 0，**Spacer Building 19/19**，其余与基线完全一致。
- `python tools/codex/audit-upgrade-continuity.py D:\GitHub\Kingdom`：升级边 **37→40**；唯一净产出减少边仍为既有 `CharcoalKiln→IndustrialCarbonizationRetort`（Coal，与本方案无关）；**三条新边零损失**；**CopperWire 不再单源**（WireMill + OrbitalWireWorks 双源）。
- 独立子代理复核（从源文件重算，不采信文档）：3 新资产逐字段、3 老厂 git diff 仅一行、gen∩cons=∅、全部资源/科研引用可解析、新 guid 全库唯一、三解锁科研 TechLevel 4 —— **全部 PASS**。
- NewEconomySimulator 维护自测：见下补记。
- **未执行真实 Unity 编译。** 未跑 EditMode/PlayMode/Console；新 .asset/.meta 未经 Unity 导入验证；六态回归（合同 §6 第 5 条）未跑。

### 风险与限制

- 静态回本偏高提示（见上节）。
- 电力：三链满配 6-8 座合计新增约 1080-2080 电/s，需相应扩 OrbitalSolarArray（360 电/s，growth 1.22）——额外拖慢，节奏同向；物流 18/10/10 压力小。
- D06 的 12 项定向加成仍绑定老厂对象：既有老厂继续享受；新厂按基值补偿（4b 已核算全升）。未来若给新厂配定向加成，`Rebuild` 重放无 TechLevel 门控，加成会正常生效。
- ~~`PhantomAlloy.asset.meta` 截断~~ **已追加修复（用户授权"处理你发现的问题"）**：全库扫描发现截断不止一处——**21 个** `.asset.meta` 同类截断（60 字节、缺 NativeFormatImporter 块；Building 7、Research 3、Resource 2、Sector 2、Workshop 7，均为既有状态）。已按标准块批量修复（脚本 `tmp/repair-truncated-metas.ps1`，自发现式：从各文件现场读 guid 重写，杜绝 guid 漂移；`mainObjectFileID: 11400000` 与其余 336 个健康定义 meta 一致；唯一例外 `Story/StoryArchive.asset.meta` 的 `mainObjectFileID: 0` 系另一资产类型，未动）。**全部 guid 原样保留**；复查截断数 0，`content-closure-check.ps1` 重跑 exit 0 且全部计数与修复前一致。日志 `tmp/meta-repair.log`。
- 座数 6-8 是需求下限，未计效率下降、巨构建造瞬时消耗与其他消费端。

### 下一具体行动

1. **交互式 Unity**（勿批处理）：导入三资产无报错 → 编译 → EditMode → 相关非零 PlayMode → Console → 六态回归（解锁前/解锁后未升级/部分升级/全升级/拆最后老厂/存读档）。
2. 运行净流校准回本与电力/物流约束（修订条款 3b），节奏定档留待实测。
3. ~~维持未收口：D04/D11~~ **D04/D11 已于 2026-09-19 第二批收口**（`WorkshopManager.TryPurchase` 补 `rollbackState`；`ResourceManager.ApplyAtomicChanges` 校验阶段一致拒绝同稳定 ID 别名实例），详见 `.codex/handoffs/2026-09-10-readonly-refactor-scan-handoff.md`「续写：2026-09-19 第二批」。仍需 Unity 侧运行验证；Unity 批处理根因仍未解。
