# 2026-10-08 全仓文档与历史证据清洁

用户要求暂停下一阶段，清理不必要、过时文档和测试结果，明确包括所有技能文档；此前已授权阶段上传及合并main。

## 首次清洁（G 实施前）的范围与结果

- 核对根/scoped AGENTS、README、docs、outputs、项目技能正文及references、客户端适配、经济prompt、交接、临时导出和测试日志。第三方许可证/署名、个人客户端记忆保留；后者不是规则或当前证据来源，也未覆盖其既有修改。
- 11份过时审计/重复入口、30份outputs报告或导出、16份历史交接退出活动目录；outputs保留18份独有专题或当前状态文档，docs保留22份权威规则/独有历史分析。
- 157份旧测试结果/日志、126份临时报告退出活动目录。TestResults保留19份、Logs保留11份，包括最近有效结果及解释修复过程所需失败证据。
- 旧待办从995行收敛为当前事项；阶段交付区分F之前完整EditMode758项与F之后分组/完整PlayMode证据。Latest-Test-Errors明确引用历史最终41/41，不伪装本轮运行。
- 独有规则迁入权威位置：瓶颈方法归数值模型，时代门槛/推荐归页面职责，互斥原始资源流归验收清单，12项中世纪研究职责归内容路线图；删除重复规则入口并同步工具断言。
- 技能移除固定旧统计、旧tmp依赖和失效说明，修正最高解锁档建筑可建、FoodNetRate边界等事实；保留单入口和客户端指针。
- 四份仍有独有候选问题的只读扫描保留历史快照性质，不能作为当前缺陷或待办。E插画/G遗迹继续暂缓，未开始下一功能阶段。

## 恢复与边界

- 修改前快照：`.codex/archive/document-cleanup-20261008-001808.zip`（303项）；补充快照：`.codex/archive/document-cleanup-20261008-supplement.zip`（135项）。已按清单逐项校验SHA256与ZIP完整性。
- 移出的outputs、交接、临时报告及157项旧测试日志可在`.codex/archive/document-cleanup-20261008-retired/`恢复；被合并的11份docs原文在主快照中。归档仅供恢复，不是活动开发依赖。
- 初次整体测试目录清理被自动审核拒绝；后续使用审核通过的固定绝对路径分批操作，备份内容已恢复到归档并核对原始哈希。
- 本次不改游戏代码、资产、存档或数学层；保留已有字体、客户端记忆及孤立Android.meta等未授权上传项。
- Python开发测试编译入口未聚合编译器返回码的既有观察只记待办，未在文档任务中扩展修复。

## 验证

- `python -B test_kingdom_project_probe.py -v`（工作目录tools/codex）：48项，47通过，1项因系统缺少创建符号链接权限跳过，exit0；夹具目录已清理。首次从仓库根按模块运行因工具采用同目录导入而失败，改用现有脚本入口成功。
- `tools/codex/validate-guidance.ps1 -ProjectPath D:/GitHub/Kingdom`：当前规则与工具路径齐全，exit0。
- 57份活动Markdown中的91个本地链接有效；两份快照共438项逐项SHA256及ZIP完整性通过；329份归档文件哈希匹配且原活动路径已不存在。
- 文本`git diff --check`通过；outputs、技能发现目录及归档均无本轮probe测试夹具残留。提交范围仅文档清洁与两项必要工具引用/断言同步。
- 未执行真实 Unity 编译。本轮没有新增Unity测试结果。

## G 完成后的文档清洁续接（2026-10-08）

用户继续暂缓 E，要求仅剩 E 时清洁文档，明确不启用子代理。本轮单代理核对当前 ToDo、阶段记录与真实 XML：约定批次 A–D/F/G 全部完成，E 是唯一未完成项；后续候选和未核实观察不升级为实施任务。

- 两份阶段报告合并为 `outputs/阶段交付.md`：保留 A–D/F/G 行为、G 前置/费用/永久路线/保存合同、实际分支提交和最新真实证据；详细失败修复继续归原主题 handoff。旧阶段路径退出活动目录，所有活动引用统一更新。
- `ToDoList.txt` 区分唯一暂缓 E 与未授权后续候选，移除重复通过数；outputs README 只保留一个交付入口。科幻候选移除已完成遗迹的施工建议，文明工程专题修正“遗迹仍未授权”的旧状态；其他独有设计与 E 制作资料保留。
- `TestResults/Latest-Test-Errors.txt` 从历史 F 的41项摘要更新为 G 最终47项PlayMode，并关联833项EditMode；保留XML完成时间和隔离检出范围，明确只是重列已有结果，未生成新测试结果。该本地忽略文件不强制上传。
- 修改前快照 `.codex/archive/document-cleanup-20261008-post-G.zip`：23份原文及SHA256清单，逐项读回校验通过。两份旧阶段原文保存在 `.codex/archive/document-cleanup-20261008-post-G/outputs/`，原活动路径已确认不存在。归档仅供恢复，不作为日常开发依赖。
- `validate-guidance.ps1 -ProjectPath D:/GitHub/Kingdom` exit0；`python -B tools/codex/kingdom_project_probe.py --project-root D:/GitHub/Kingdom --check-skill --format text` exit0，Errors0、4项可选范围跳过。
- 54份活动Markdown中的95个本地链接目标有效；未验证外部网址和Markdown锚点。活动outputs/docs/技能/工具不再引用两份旧阶段路径。文本diff空白校验通过。
- 未启用子代理，未修改源码、资产、技能规则或工具实现；既有Story/字体/客户端记忆/临时文件保留。未执行真实 Unity 编译。

下一动作：当前授权批次无实施待办；保持E暂缓。G交付在codex分支，不能将上传表述为已合并main。