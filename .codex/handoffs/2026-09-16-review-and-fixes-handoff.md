# 2026-09-16 已完成任务复核与修正交接

## 任务与状态

用户要求「重新审查，务必审查我目前已经完成的任务，再给出具体建议」。本批为**只读复核 + 已核实的 3 类修正**，产物为 `outputs/Kingdom-已完成任务复核与建议-2026-09-16.md`。

- 已完成：P0-04 存档 v9 语义复核、D12/D09 测试可信度复核、E08 数字独立复算、乱码修复、2 处文档数字更正、2 条错误建议的驳回记录。
- **未完成**：任何运行验证（未执行真实 Unity 编译、未跑 EditMode/PlayMode/Console）。
- 不关闭项：P0-01（D09/D12 待运行）、P0-02（待用户拍板甲/乙案）、P0-04 缺口 A（待补回归）。

## 起始基线与保留

- 基线 HEAD `a85f0e7` + 大量未提交工作树，**全部保留，未回滚任何既有改动**。
- 用户前序成果（P0-04 v9、D12、D09、音效/插画/剧情产出、指导体系）均未触碰。

## 本批实际变更

1. `Assets/Tests/Editor/ProgressionMilestoneRecorderTests.cs` — 修 `RepeatedSampling_IsReadOnlyAndDoesNotReplaceFirstObservation`：
   - 原缺陷：基线快照在 `SampleNow()` **之前**采集，若被测方法写坏 state 基线已含坏值，「只读」断言**恒真**（误报通过）。
   - 已改为：先采样 → 取基线 → 再采样 → 断言第二次采样未改 state；并补「首次采样必须记录到进度」前置断言，防止"不记录也能过"。
2. `Assets/Resources/Script/Manager/ResearchManager.cs` — 第 934/962/1119 行 3 处 mojibake（`瀛樻。涓\ue160殑鐮旂┒鐘舵€佺储寮曠己灏戯細`）改为 `存档中的研究状态索引缺少：`。**仅异常文本**，无逻辑变化。该乱码在 HEAD 同样存在，属历史遗留，非 P0-04 引入；文件为合法 UTF-8、无 PUA，不影响编译。
3. `outputs/Kingdom-P0-02-E08升级链合同审定-2026-09-15.md` — Ceramic 总供给 3.4/s → **3.8/s**；§9.2「12 项单源资源」表删除 Ceramic 行（该行使其成 13 行且与本节"共 12 项"矛盾），并加注 Ceramic 属"升级后降为单源"的容量回退项。
4. `outputs/Kingdom-outputs审查与修缮开工-2026-09-15.md` — 同处数字 3.4 → **3.8**/s。

## 复核发现的缺口（**未实施修复**，均有源码证据）

- **P0-04 缺口 A**：pause 时 `SaveNow(true)` 失败 → 主档保留旧时间戳、内存 `applicationPausedAtUnixSeconds`（SaveManager.cs:200）随进程消失 → 下次启动从旧 `LastSaveUnixSeconds` 起算，**重复结算**已暂停区间（多给）。**无测试覆盖**。需补有界回归。
- **P0-04 缺口 B**：未知/旧 ID 抛 `KeyNotFoundException`（DataBase.cs:35-42）后经 `TryLoadSave` catch（SaveManager.cs:300）转**新游戏**，主档既不删除也不标记，新档会**写回覆盖坏档**。与交接文档"直接使主档失效"表述有差距。当前行为与 v9 取向自洽，仅标注。
- **P0-04 缺口 C**：「段存在但为空」被静默接受（`ValidateRequiredSections` 只查非 null）。**收紧会打破 32 处既有测试**（`CreateRepresentativeSaveData` KingdomLogicTests.cs:2430 用空 `States`，32 处经真实加载断言成功，如 :334/:439）。若需收紧必须先改 fixture。

## 本轮**驳回**的两条错误建议（防回归，务必保留此记录）

1. **「暂停保存失败时清掉内存暂停标记」——有害**。该标记必须存活到 resume，否则恢复分支 `pausedAt > 0L` 判定失败，**整个暂停区间不再结算**（漏算），比原缺口更糟。已实测并回滚。
2. **「把空 `States` 纳入拒绝条件」——有害**。直接使 32 处依赖空账本 fixture 的测试全红。已实测并回滚。

## 验证执行

- `dotnet build Kingdom.Editor.Developer.csproj --no-restore`：**0 错误、0 警告**（多轮，含 ResearchManager 与测试改动后）。
- 独立数字复算：`Ceramic` 净产出经脚本从 `.asset` 原文重算 = AdvancedCeramicsPlant 2.4 + BMC 0.4 + CeramicKiln 1.0 = **3.8/s**（含 6 个消费端正确扣除）。
- 乱码核实：HEAD 与工作树各 3 处；文件 50859 字节、UTF-8 合法、PUA 计数 0；全仓扫描 `Assets/Resources/Script`、`Assets/Tests` 无其它同类残留。
- **未执行真实 Unity 编译。** 未运行任何 EditMode/PlayMode/Console；`TestResults/` 最新 XML 为 **2026-09-12**（661/661），**不覆盖本批**；`EditMode-D12-20260915-run1..5.log` 全是编辑器启动日志（run1 记录 UPM 启动失败），不含测试结果或 `error CS`。

## 风险与证据限制

- 本批所有"成立"均为**源码/静态复算层面**，不构成运行验收。
- 未复算三座新建筑资产（OrbitalWireWorks 等**当前不在** `Datas` 中），§9.3 只能核到章程口径。
- 环境勘误：本机 Python 3.13.12 的 `re` 对「多字符字面量后接 `\s`」（如 `guid:\s`）会静默失配；现有 `audit-upgrade-continuity.py` 用空格写法不受影响，后续改脚本需避开。
- PlayMode 跨用例静态缓存（`Singleton.instance`、`ResearchManager.cachedResourceManager`）静态分析**未发现确定的泄漏**（`Singleton.OnDestroy` 会清、getter 有 `FindObjectOfType` 兜底），但**未经运行验证**，列运行后第一优先观察项。

## 下一具体行动

1. **交互式** Unity Test Runner（**不要再试批处理**，已四次证实卡在 `Application.AssetDatabase Initial Refresh Start`）：`ProgressionMilestoneRecorderTests` → 4 项 PlayMode 用例 → 全量 EditMode → 相关非零 PlayMode → Console。重点看跨用例残留。
2. 补 P0-04 缺口 A 的「暂停保存失败不重复结算」有界回归（仅测试）。
3. P0-02 拍板甲/乙案；乙案前置已按审定文档 §9.4 补全。
4. 运行可用后做 D04/D11 有界复现与最小修复。
