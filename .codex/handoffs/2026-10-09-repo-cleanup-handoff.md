# 剩余改动分类与清理

用户授权检查交付后的剩余改动，清理无用文件。基线 main `b7bf4c08e6c8891c466cebe72547e9fd3740b730`，保留真实功能修复，不删除字体资产本体。

- 预览白名单 75 个目标、271 个文件、18,318,361 字节：仅未跟踪 tmp 诊断脚本/截图/沙盒、Python 缓存、3 份历史客户端笔记、旧 Android Addressables 构建状态及文件夹 meta。仓库已跟踪 tmp 文件保留。
- 所有目标先核验仓库内绝对路径、无 reparse point、无跟踪源码，随后移到 `.codex/archive/2026-10-09-repo-cleanup-204150/removed/`。`manifest.json` 记录逐项原路径/归档位置；75 项均核验原位置不存在、归档位置存在，未例行永久删除历史证据。
- `.workbuddy-ai/memory/MEMORY.md` 的既有改动及 `SIMSUN SDF.asset` 先复制到该归档 `modified/` 并核对内容一致，再还原 HEAD。字体差异仅新增18个动态字形/字符和atlas排布/像素，源TTC、GUID、动态配置均未变；个人旧笔记不作为当前规则交付。
- `.codex/config.toml` 是本机 Codex 设置，内容未改，仅在 `.git/info/exclude` 精确加入该路径，保持本地可用且不上传；未修改全局忽略或全局 Git 配置。
- 还原后 Git 内容 diff 为空；一次精确 `git add` 更新两个文件的行尾/索引状态，确认 staged diff 仍为空。主工作树全部原剩余 dirty/untracked 项已退出活动变更清单。
- `D:/GitHub/Kingdom-source-audit` 的未提交 BuildingManager/KingdomLogicTests 两处保留：属于把失败建造的状态注册推迟到提交阶段的真实修复，不是垃圾。尚无真实测试证据，不随本次清理上传；后续先验证无效数量、领土/生产力/资源不足不新增状态/事件，以及成功建造登记、扣费和事件，然后再交付。

本轮仅文件分类、归档与恢复，没有改游戏行为，没有运行 Unity。未执行真实 Unity 编译。交付只包含本交接，归档和本机设置不上传。

## 2026-10-10 补充：清除已确认的一次性审计文件

按用户明确授权，从工作树移除以下 10 个已跟踪且干净的文件：`tmp/verify_e08.py`、`tmp/verify_e08b.py` 至 `tmp/verify_e08g.py`、`tmp/verify_p003c.py`、`tmp/audit_upgrade_continuity.py`、`code-audit-refactor-candidates.csv`。

- 8 个 `verify_*.py` 是 2026-09-15 的单次资产核验脚本；仓库内无当前引用。升级连续性临时审计脚本由 `tools/codex/audit-upgrade-continuity.py` 覆盖，且无当前引用。旧 CSV 仅在 `docs/audits/2026-09-10-full-readonly-refactor-scan.md` 的历史关系说明中提及；该报告确认旧条目已逐项复核并更新行号。
- 修改前逐项确认路径在仓库内、不是重解析点、Git 跟踪且无本地改动；修改后确认 10 个原路径均不存在，Git 仅显示这 10 个预期删除。
- 保留 2026-09-10/11 历史审计报告：`ToDoList.txt` 仍将其标记为尚未逐项核清的调查；保留 `tmp` 中插画报告生成脚本和 `outputs/` 专题文档，因为仍有创作/历史用途或被正式索引。
- 本轮未运行脚本、Unity 编译或测试。删除的跟踪文件可从 Git 历史恢复；未删本机 `TestResults/` 和 `Logs/`。

## 2026-10-10 补充：临时验证项目收尾与约束

用户授权删除目前无用的临时项目，并为代理创建此类项目增加收尾约束。主目录既有暂存/未提交内容保留；不迁入游戏修复、不提交或推送。

- 根 `AGENTS.md` 的工作树条款新增：优先复用、必要时才创建隔离项目、登记目的/源版本/退出条件、不复制缓存；创建者负责同轮迁回成果、核对.meta/GUID、保留必要证据与独有补丁、注销worktree并核验无残留。阻塞须记录，不默认长期保留第二主项目。
- 归档：`.codex/archive/2026-10-10-temporary-project-cleanup/`。保留规则与本交接修改前快照；三个临时项目的独有改动、二进制patch（worktree）、日志/XML及review-evidence均已复制并逐项SHA256核验，清单在 `manifest.json`。ShaderGraph设置因首次Git差异枚举漏列，另存 `relic-verification/files/ProjectSettings/ShaderGraphSettings.asset` 并单独核验。
- 已完成：`C:/Users/19603/.codex/worktrees/relic-verification/Kingdom` 的功能代码与最终G3提交一致；归档差异后恢复其已保存文件并以 `git worktree remove` 正常移除，不使用force。确认目录不存在、注册项不存在，清理约2.70GB。
- 未完成：后续移除 `D:/GitHub/Kingdom-source-audit` 与 `D:/GitHub/Kingdom-review1009` 的命令被执行策略拒绝（blocked by policy，未给出更具体原因），按规则停止、不改方式绕过。两目录仍存在；source-audit两处未提交修复仍原样保留，且已存patch和源码副本。下一动作是在执行策略允许时核验归档后恢复已保存的两文件、正常注销source-audit，再移除已归档差异的review1009；不需再调查其用途。
- 验证：只读project probe `--check-skill` exit0，errors为空；ProbeHarnessTests 9/9通过；GuidanceContractTests failfast在第10项 `test_powershell_required_paths_exist` 失败，正则未处理当前CRLF行尾，抽取路径0项（要求>20），本轮未修改该测试或validate-guidance.ps1，不弱化断言。根规则diff检查通过。
- 未执行真实 Unity 编译。外部临时项目删除被阻断不是游戏验证失败；本轮清理未全部完成。

## 2026-10-10 补充：项目清理建议执行

按 `tmp/cleanup-audit-20261010/清理建议.md` 完成低风险缓存与失效产物整理；保留工作树内既有并行改动，不提交。

- 未跟踪 `Logs/`、过期 `TestResults/`、`UserSettings/`、`.vs/`、Android Burst 临时文件、模拟器 `bin/obj` 和 Unity 测试残留场景移至仓库外 `D:/Kingdom-cleanup-backup-20261010/`。TestResults 仅恢复建议中保留的批次，并恢复 `Latest-Test-Errors.txt` 当前证据入口；旧批次保存在外部备份。
- `output/fb89034f-45d8-4194-856b-175b429cc8d2/`、两个失效 story-art 生成脚本、无引用 contact sheet、四个无活动流程引用的失效/一次性工具脚本移至 `.codex/archive/2026-10-10-project-cleanup/`。归档包含 `manifest.json`（路径、长度、SHA256）。`output/` 空目录已移除。
- 移除空 `Kingdom/` 与三个空 ultra-r2 临时目录。建议中九个已删未落实索引状态的 `tmp/verify_*` 与 `tmp/audit_upgrade_continuity.py` 仍显示为预期删除；不执行暂存操作。原 handoff 已记录的 `code-audit-refactor-candidates.csv` 删除也保留为既有变更。
- 代码复核建议暂不删 C# 候选：Singleton 的公开 Save/Load 虚方法及多个 override 有外部绑定风险；若干 Editor/测试入口需进一步确认。未修改代码，也未运行离线编译。
- 历史审计/闭环 handoff 未归档：活动文档仍引用至少两份 handoff，且 ToDoList 仍把旧审计列为未核清；alien-war-first-version 涉及玩法语义，继续保留。孤儿 PNG、运行时 UI 构建、测试反射和定义字段未处理。
- 验证：核对目标缓存目录不再位于活动项目路径；TestResults 保留批次仍在，`Latest-Test-Errors.txt` 已恢复；跟踪 `output/fb...` 文件只显示预期删除。未执行真实 Unity 编译；无游戏行为修改。
