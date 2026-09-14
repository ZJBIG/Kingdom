# Kingdom 长期项目约定

- 智能体开发入口：根 `AGENTS.md` → `.agents/skills/kingdom-project-dev/SKILL.md` 唯一任务路由 → 按需读取内容/经济、UI或运行时/测试专题。同轮已读且未变的入口不循环加载，不创建同义技能或重复规则。
- 规范技能在 `.agents/skills/`，`.workbuddy-ai/skills/kingdom-project-dev/` 仅客户端适配；不能以文件存在声称自动注册已验证。
- 指导体系维护方法与权威归属集中主技能 `references/guidance-maintenance.md`。页面/详情规则归 `docs/ui/page-responsibilities.md`，产品默认归 `docs/decisions/conservative-defaults.md`；旧兼容文档不新增正文。
- 保留当前工作树和并行改动；历史审计/旧计划不覆盖当前源码事实和硬约束。存量完整probe测试有fixture写入/清理副作用，受保护阻断后停跑；纯文档修改选只读契约/控制流测试，不能据此声称真实链接、Unity或全量工具回归通过。
