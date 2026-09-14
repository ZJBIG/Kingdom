---
name: kingdom-project-dev
description: Kingdom 项目开发总入口。用于本仓库扫描、接手任务、玩法 bug 修复、Runtime/Manager/Save/Simulation 开发、C# 重构、Prefab/UI、内容配置、测试验证与子代理协作；按领域路由到内容/经济统一技能和 UI 技能。仅适用于 Kingdom。
agent_created: true
---

# Kingdom 开发入口

确认当前工作区是 Kingdom，读取根 `AGENTS.md`，然后读取工作区根目录下 `.agents/skills/kingdom-project-dev/SKILL.md` 并按其流程执行。这个文件只作 WorkBuddy 项目级发现入口；完整规则和参考资料仅维护在 `.agents/skills/kingdom-project-dev/`，不要复制形成第二套规则。

若主技能缺失，报告缺失路径，不下载、不猜测，也不把入口文件当作完整技能。只读任务不修改项目；领域子代理默认只读；所有实施、构建和保存仍以当前用户授权为前提。
