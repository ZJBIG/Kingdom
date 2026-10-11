# 07 · `.codex/` 归档与交接 审查报告

只读盘点，未修改任何文件。基准：当前磁盘工作树（`main` @ `ee6e173`）。

## 1. 范围

实际审查的路径与真实数量：

| 路径 | 内容 | 数量 / 体积 |
|---|---|---|
| `.codex/handoffs/` | 交接文档 | 8 份（非 7；见 §3 不确定项 1） |
| `.codex/archive/` | 归档根文件 + 6 个子目录 + 3 个 zip | 14 个顶层条目，约 124M |
| `.codex/prompts/CODEX_ECONOMY_PROMPT.md` | 经济入口 | 1 份（535B） |
| `.codex/config.toml` | 本机 Codex 设置 | 1 份（135B） |
| `.codex/package-recovery/` | 空目录 | 0 文件 |
| `.codex` 合计 | — | 127M |

`.codex/archive/` 子目录体积（`du -sh`）：`2026-10-09-repo-cleanup-204150` 49M、`document-cleanup-20261008-retired` 64M、`document-cleanup-20261008-post-G` 12K、`recovered-20260915` 344K、`pdf-intermediates-20260914-100336` 276K。

背景核对（未误判）：
- `.gitignore:22-23` 忽略 `/.codex/archive/`、仅保留 `README.md`；`git ls-files .codex/` 只跟踪 6 份文件（5 份 handoff + 1 份 prompt）——即 archive 内容确实未纳入 git，属本地可恢复归档。
- `AGENTS.md:7` 明确 `.codex/` 保存续接入口、交接和可恢复归档；`AGENTS.md:41` 要求"修改前读当前同主题 `.codex/handoffs/`"；`AGENTS.md:35` 明确 `.codex/archive/` 只作背景。故 handoffs 是活的工作流载体，archive 是背景。
- `package-recovery/` 与 `config.toml` 均未被 `git ls-files` 跟踪。

## 2. 结论清单

### 2.1 建议归档（已闭环且被更新交接覆盖）

- [置信度 High] `.codex/handoffs/2026-09-18-doc-consistency-and-regression-handoff.md` — 维度 D3；证据：正文记录 A/C1/F1–F3 全部完成并上传，末行"F1–F3已完成，没有新增未验收游戏代码；E插画/G遗迹继续按用户要求暂缓"；其中 E、G 此后均已落地（见下条证据）；建议：归档；理由：任务已闭环，且被 2026-10-08 relic / 2026-10-09 story 两份更新交接完全覆盖。
- [置信度 High] `.codex/handoffs/2026-10-08-document-cleanup-handoff.md` — 维度 D3；证据：末行"下一动作：当前授权批次无实施待办；保持E暂缓"；`ToDoList.txt:13-14` 亦将 E 列为唯一未完成项，而 E 已由 10-09 交接完成；建议：归档；理由：文档清洁任务已闭环，无遗留动作。
- [置信度 High] `.codex/handoffs/2026-10-08-relic-handoff.md` — 维度 D3；证据：末段"最终状态：G1/G2/G3完成"，提交 `86d03c8`/`99d45a1` 经 `git merge-base --is-ancestor` 确认已在 `main`；`Assets/Resources/Script/Manager/RelicManager.cs`、`Assets/Resources/Script/Runtime/RelicState.cs`、`Assets/Editor/RelicUIAuthoring.cs` 均存在于当前源码；建议：归档；理由：G 遗迹实现闭环。

> 说明：`document-cleanup-20261008-retired/.codex/handoffs/` 已存在 16 份 2026-09 旧交接的归档先例，将上述 3 份闭环交接移入同一目录与既有实践一致。

### 2.2 建议删除（无价值中间产物）

- [置信度 High] `.codex/archive/shadercompiler-UnityShaderCompiler.exe0.log` — 维度 D4；证据：165B，仅含"Base path/Cmd: initializeCompiler"噪声；`.workbuddy-ai/memory/2026-09-14.md:12` 已把此类文件按"仅真临时产物"删除；当前 `Logs/` 下存在同名 `shadercompiler-UnityShaderCompiler.exe0..8+.log` 会持续再生；建议：删除；理由：Unity 噪声日志的陈旧副本，零信息价值。
- [置信度 High] `.codex/archive/detail-ui-migration-current.log` — 维度 D3/D4；证据：3.2M（归档最大单文件），内容为 2026-08-29 一次性 UI 迁移的 Unity batchmode 日志（头尾为 Licensing/Input System 噪声）；全仓除 archive 外无任何引用（`grep -rl` 为空）；建议：删除；理由：一次性迁移中间日志，无恢复价值。
- [置信度 High] `.codex/archive/pdf-intermediates-20260914-100336/` — 维度 D3；证据：仅 2 文件——`Kingdom_kittens_pdf_contact_sheet.png`(260K) + `build_kittens_followup_pdf.py`(17K)；`.workbuddy-ai/memory/2026-09-14.md:14` 记载其为并行任务误扫入的"PDF中间产物"，正式 PDF 交付物在别处未受影响；建议：删除；理由：PDF 构建中间物，成品已另有归属。
- [置信度 Med] `.codex/package-recovery/` — 维度 D9；证据：`du -sh` = 0、`find -mindepth 1` 无输出、全仓无引用、git 未跟踪；建议：删除；理由：空目录，无内容无引用。
- [置信度 Med] `.codex/archive/codex-compile.log`(20K) / `codex-repository-audit.txt`(44K) / `codex-ui-audit.txt`(40K) — 维度 D3；证据：三者是 2026-08-29/30 的旧副本；对应工具 `tools/codex/compile-unity.ps1:4`、`inspect-kingdom.ps1:3`、`audit-ui.ps1:3` 的默认输出路径均为**活动** `Logs/`（非 archive），即工具会重新生成；建议：删除；理由：被活动 `Logs/` 输出取代的陈旧审计副本（若担心历史，可降级为"保留观察"）。

### 2.3 建议更新

- [置信度 High] `.codex/handoffs/2026-10-09-repo-cleanup-handoff.md` — 维度 D6；证据：line 10 记录"`D:/GitHub/Kingdom-source-audit` 的未提交 BuildingManager/KingdomLogicTests 两处保留……尚无真实测试证据……后续先验证……然后再交付"；实测该 worktree 仍存在（`git worktree list` 显示 `D:/GitHub/Kingdom-source-audit da8d6a5 [codex/source-audit-fixes]`），`git -C "D:/GitHub/Kingdom-source-audit" status --short` 仍输出 ` M Assets/Resources/Script/Manager/BuildingManager.cs`、` M Assets/Tests/Editor/KingdomLogicTests.cs`；建议：更新（追加闭环或明确搁置）；理由：该未闭环项真实存在且悬置，属活交接。
- [置信度 Med] `.codex/handoffs/2026-10-09-story-illustrations-handoff.md` — 维度 D6；证据：line 26/41 反复记录"EditMode 断言完成但原生退出问题尚未解决，不能把该轮称为正常完成"，且该限制被 `2026-10-09-progressive-design-review-handoff.md:26` 交叉引用（"EditMode原生退出停滞不能算正常exit0"）；建议：更新（记录是否已定位或长期豁免）；理由：跨多轮的悬置限制，需显式收口。

### 2.4 建议合并

- [置信度 Low] 三份 2026-10-09 只读审查交接（`information-recovery-and-choice-review`、`progressive-design-review`、`ui-controls-and-feature-audit`）—— 维度 D8；证据：三者同属"10-09 只读策划审查"系列，各自正文均声明保留前序文档（info 交接 line 3"保留前两轮四份文档"、progressive line 7"原有Story、Relic和文档交接仅读取"、ui-controls line 26"工作树保留上一轮两份文档"）；主题不同（信息可信度 / 阶段策划 / 功能按钮），**无内容重复**；建议：保留观察，不强制合并；理由：仅格式同构、非内容重复，合并会损失各报告与交接的一一对应。

### 2.5 保留观察（在用 / 可恢复归档，仅报不删）

- [置信度 High] `.codex/prompts/CODEX_ECONOMY_PROMPT.md` — 维度：无（在用）；证据：被 4 处引用——`.agents/skills/kingdom-economy-simulation/SKILL.md:19`（"只是到本技能的入口"）、`tools/codex/kingdom_project_probe.py:44`（`REQUIRED_PROJECT_FILES`）、`tools/codex/test_kingdom_project_probe.py:203`、`tools/codex/validate-guidance.ps1:8`；其内容（line 3"兼容旧经济任务入口，不维护独立流程"、line 5 指向 `kingdom-project-dev/SKILL.md` 路由）是**薄入口**，非重复经济规则；建议：保留；理由：活跃入口，非被取代的旧版经济 prompt，删除会破坏工具校验与技能路由。
- [置信度 High] `.codex/archive/recovered-20260915/`（344K，18 文件，含 `CONTENTADVISE/` 16 文件）— 维度：无（被引用的恢复件）；证据：`docs/repository-map.md:57` 与 `.workbuddy-ai/memory/MEMORY.md:8` 明确指向该归档路径与 `git show a85f0e7^:` 复核命令；建议：保留；理由：被当前文档引用的恢复归档。
- [置信度 High] `.codex/archive/document-cleanup-20261008-post-G/`（12K）— 维度：无；证据：`2026-10-08-document-cleanup-handoff.md:38` 指明两份旧阶段原文存于此；建议：保留。
- [置信度 Med] `.codex/archive/document-cleanup-20261008-retired/`（64M，329 文件：Logs 67 / TestResults 90 / tmp 123 / outputs 30 / .codex 16 等）— 维度：无（有清单的恢复归档）；证据：`2026-10-08-document-cleanup-handoff.md:18` 声明"移出的outputs、交接、临时报告及157项旧测试日志可在此恢复"；建议：保留；理由：文档化的可恢复归档，含解释修复过程的失败证据。
- [置信度 Med] `.codex/archive/2026-10-09-repo-cleanup-204150/`（49M，274 文件：`manifest.json` + `removed/` 75 项 + `modified/`）— 维度：无；证据：`2026-10-09-repo-cleanup-handoff.md:6` 声明"`manifest.json` 记录逐项原路径/归档位置；75 项均核验原位置不存在、归档位置存在"；建议：保留。
- [置信度 Med] 3 个 zip：`document-cleanup-20261008-001808.zip`(5.6M/304 项)、`-supplement.zip`(5.1M/136 项)、`-post-G.zip`(116K/24 项) — 维度：无（文档化快照）；证据：`2026-10-08-document-cleanup-handoff.md:17` 声明为修改前/补充快照并"逐项校验SHA256与ZIP完整性"；建议：保留观察；理由：有清单的恢复点，但与 retired 目录内容部分重叠，若要回收空间可优先评估。
- [置信度 Med] `.codex/archive/kingdom-master-game-audit-pre-todolist-2026-08-30.md`（176K）— 维度 D3；证据：2026-08-30 的"ToDoList 前"主审计快照，全仓无引用；建议：保留观察；理由：历史快照，已被后续 ToDoList/docs 取代，但属有界归档。
- [置信度 High] `.codex/config.toml` — 维度：无（本机设置）；证据：内容为 `approval_policy="never"`/`sandbox_mode="workspace-write"` 等本机 Codex 设置；未被 `git ls-files` 跟踪，经 `.git/info/exclude:9`（`/.codex/config.toml`）精确排除；`2026-10-09-repo-cleanup-handoff.md:8` 说明其为本地可用、不上传；建议：保留；理由：本机 Codex CLI 运行所需，且已正确排除出仓库。

## 3. 不确定项

1. **数量口径**：team-lead 指派为"7 份交接（10-09 ×4）"，实测 `.codex/handoffs/` 为 **8 份**、其中 2026-10-09 前缀 **5 份**（information-recovery、progressive-design、repo-cleanup、story-illustrations、ui-controls-and-feature-audit）。本报告按磁盘实际 8 份审查。
2. **归档回收策略**：retired(64M)+repo-cleanup(49M) 合计 113M 为全 `.codex` 体积主体，二者均为文档化可恢复归档。是否回收空间需用户决策（倾向保留，符合项目"可恢复归档"约定）。
3. **`information-recovery-and-choice-review-handoff.md` 归属**：其自身无实施下一动作（仅"实际节奏…未验证"），但属 10-09 只读审查系列且报告在活动目录。本报告归入"保留观察"，是否随后续只读系列一并归档待用户定。

## 4. 未覆盖项

- `.codex/archive/document-cleanup-20261008-retired/` 与 `2026-10-09-repo-cleanup-204150/` 的**逐文件内容**未全量展开（仅采样目录与数量，未逐一读 603 个归档文件正文），其可恢复性以既有 `manifest.json`/SHA256 清单为准。
- 3 个 zip 未解压逐项核对（仅 `unzip -l` 读清单头尾与项数）。
- `.codex/config.toml` 之外的全局 Codex 配置、`.git/info/exclude` 其他行未审。
- 跨域交叉项（超出本次 `.codex` 范围，仅记录不判定）：`tools/codex/kingdom_project_probe.py:17` 的 `SCAN_ROOTS` 仍含已删除目录 `CONTENTADVISE`（D1 悬空引用候选）；`ToDoList.txt:2,13-14` 仍写"E 插画继续暂缓"，与 10-09 已完成 E 的实际状态不一致（D3 陈旧候选）。二者应由对应区块代理处理。

## 5. 小结

- `.codex/handoffs/` 8 份中 **3 份已闭环**（09-18、10-08 ×2）建议归档；**2 份含真实未闭环项**（repo-cleanup、story-illustrations）建议更新后保留；**3 份为 10-09 只读审查**，无重复、保留。
- `.codex/prompts/CODEX_ECONOMY_PROMPT.md` **在用**（工具与技能双引用），非旧版经济 prompt，不删。
- archive 中 **2 个可恢复归档目录 + 1 份被引用恢复件 + 3 个快照 zip** 建议保留；**4 类无价值中间产物**（shader 噪声日志、detail-ui 迁移日志、pdf 中间物、空 `package-recovery/`）建议删除，合计约 3.5M（另 3 个旧审计副本 ~104K 可选删）。
- 未发现 `.codex/` 内相互矛盾或内容重复的交接。
