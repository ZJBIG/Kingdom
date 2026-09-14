# 内容与数值验收入口

内容设计、执行顺序和模拟器证据边界统一维护在
`.agents/skills/kingdom-economy-simulation/SKILL.md`；本文件保留兼容入口，
不再维护第二份执行流程或“当前缺少哪些测试”的静态清单。

- 定义、source/sink、事务与可达性验收：`docs/testing/acceptance-checklist.md`。
- 新档、后台恢复、剧情与页面行为：`docs/testing/playmode-test-plan.md`。
- 当前时代里程碑：`docs/content/progression-roadmap.md`。
- 命令、输入覆盖与存档隔离：
  `.agents/skills/kingdom-project-dev/references/validation.md`。

确定性诊断可按规范技能运行；禁止扩展策略搜索或将fixture结果当作真实Unity、
节奏或平衡验收。现有测试必须核对本轮实际执行日志，不能仅因文件存在就算通过，
也不按旧文档重复新增已有用例。
