# Kingdom 长期项目约定

- 智能体开发入口：根 `AGENTS.md` → `.agents/skills/kingdom-project-dev/SKILL.md` 唯一任务路由 → 按需读取内容/经济、UI或运行时/测试专题。同轮已读且未变的入口不循环加载，不创建同义技能或重复规则。
- 规范技能在 `.agents/skills/`，`.workbuddy-ai/skills/kingdom-project-dev/` 仅客户端适配；不能以文件存在声称自动注册已验证。
- 指导体系维护方法与权威归属集中主技能 `references/guidance-maintenance.md`。页面/详情规则归 `docs/ui/page-responsibilities.md`，产品默认归 `docs/decisions/conservative-defaults.md`；旧兼容文档不新增正文。
- 保留当前工作树和并行改动；历史审计/旧计划不覆盖当前源码事实和硬约束。存量完整probe测试有fixture写入/清理副作用，受保护阻断后停跑；纯文档修改选只读契约/控制流测试，不能据此声称真实链接、Unity或全量工具回归通过。
- Unity 批处理取证在本机不可用：`-batchmode` 启动会在 `Application.AssetDatabase Initial Refresh Start` 后零进展（已排除 `-nographics`、缺标准环境变量、资源库损坏、陈旧 DB 锁文件四假设；空库重建与 `-quit` 只编译同样卡住；`Assets` 侧无重复 GUID/非 ASCII 名/符号链接，`Packages/manifest.json` 无网络引用，D: 为本地 NTFS）。交互式 Editor 可正常编译导入（可查 `%LOCALAPPDATA%\Unity\Editor\Editor.log`）。取得编译/测试证据请走交互式 Test Runner，不要重复批处理尝试；诊断只终止自有进程，改名的 Library 缓存/锁文件必须改回原名。
- `CONTENTADVISE/` 目录已在 HEAD `a85f0e7` 删除但多处文档仍引用（ToDoList P0-02、`docs/balance/balance-model.md`、`docs/repository-map.md`）；原文可用 `git show a85f0e7^:CONTENTADVISE/<文件名>` 只读恢复，已归档到 `.codex/archive/recovered-20260915/`，不要把该目录重新纳入活动目录。
