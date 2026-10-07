---
name: kingdom-project-dev
description: Kingdom 项目开发总入口。适用于仓库扫描、接手、玩法bug、C#重构、Runtime/Manager/Save/Simulation开发、Prefab/UI、内容配置、测试验证及子代理协作。负责定位、任务分流、权限和交付；内容/经济统一交给kingdom-economy-simulation，视觉交互交给kingdom-ui-redesign。仅适用于Kingdom。
agent_created: true
---

# Kingdom 项目开发流程

按“确认范围 → 定位与分流 → 复现 → 最小修改 → 分层验证 → 交接”推进。硬约束只在根/scoped AGENTS维护，领域规则只在对应技能维护；本文件不再逐条复制。

## 1. 进入正确项目

1. 确认根目录有 `AGENTS.md`、`ProjectSettings/ProjectVersion.txt` 和 `Assets/Resources/Script/Manager/GameBootstrap.cs`。目录不匹配时停止并询问正确位置，不假定盘符。
2. 读取根AGENTS、就近scope和当前同主题handoff；检查工作树与相关diff，区分既有/并行变更。同一轮已读且未变化的入口不回读，导航引用不是递归加载指令。
3. 明确只读审计、实施或构建相关任务。只读任务不启动写报告、缓存或存档的检查，不“顺便修复”。
4. 从 `docs/repository-map.md` 定位文件/API。审查自有代码、定义、Scene/Prefab、测试和工具；不把缓存、第三方和二进制素材说成逐行审计范围。
5. 可选运行 `tools/codex/kingdom_project_probe.py`，只读stdout盘点、指定文本指纹和入口/引用校验。若未安装辅助工具，使用文件读取继续，不自动下载。

## 2. 按任务读取，不全量堆叠

| 任务 | 必读入口 |
|---|---|
| 玩法、进度、Research/Resource/Building/TechLevel/Workshop、人口、领土、星区、生产、可达性、数值 | `kingdom-economy-simulation`，需要设计时读取其content-design参考 |
| UI、研究树、Prefab、滚动/拖动、安全区 | `kingdom-ui-redesign`；改变玩法/费用则同时读经济技能 |
| Runtime/State/Manager/保存/离线结算 | 运行时scope和 `docs/architecture/runtime-state.md`、`serialized-pairs.md`、`ui-boundaries.md` |
| 纯ExpantaNum内部实现 | 运行时scope、数学调用点和数值测试，不重建BigNumber |
| 测试、脚本、编译 | 测试scope和 [验证手册](references/validation.md)；外部运行体验与交付形态由用户自行验收，代理不执行或跟踪 |
| 音乐、剧情、教程 | 同主题handoff、Manager与测试；剧情创作再读 `docs/story/story-data-authoring.md`；触及经济/UI再加领域技能 |
| 产品默认、是否允许改变既有玩法 | `docs/decisions/conservative-defaults.md`；修改仍需本轮授权 |
| Skill/AGENTS/开发文档整理与规则冲突 | [指导体系维护](references/guidance-maintenance.md)；不启动游戏构建 |

此表是唯一任务路由。先选一个主分支，只有实际跨边界时再增加第二分支；硬约束继承，不抄写到领域文件。定位只读 `docs/repository-map.md` 相关节，不把地图、历史审计或同主题旧交接全部堆入上下文。

从 `.agents/skills/` 读取规范文件；客户端未登记时不要调用未知技能名，不声称自动触发已生效。WorkBuddy薄入口只转发到本文件。

## 3. 复现与最小实施

- 先说明输入、预期/实际、状态所有者、受影响文件与验收方法；复杂任务使用 [子代理协议](references/subagents.md)，默认只读，主代理独立核对关键结论。
- 修改前理解实际字段/消费端；保留根AGENTS的强类型、事务、序列化与权限约束。只修授权范围，记录字段前后值；不把文档漂移当成重建系统的理由。
- 对照 `docs/repository-map.md` 中的资产金额与运行时Pair边界，不能照旧模板强改序列化或复制同名类型。
- 主代理检查实际diff与运行输出，排除无关格式化、ID/GUID变化和他人工作。子代理失败就缩小范围或接管，不伪造完成状态。

## 4. 验证与交付

1. 按 [验证手册](references/validation.md) 选择最小充分检查；玩法行为标准参考 `docs/testing/acceptance-checklist.md`，不把工具退出成功等同游戏通过。
2. 用 [证据规则](references/evidence.md) 区分本轮执行、静态事实、历史和未验证。缺真实Unity编译时明确写“未执行真实 Unity 编译。”
3. 实施任务续写原handoff，使用 [交接栏目](assets/handoff-template.md)；只读任务仅回复，除非获准落盘。
4. 汇报具体文件、行为、验证结果/限制和下一动作，提供真实产物，不只给内部计划。
5. 新规则或流程变更对照 [验收场景](references/acceptance-cases.md)，同时验证路径依赖和不产生残留。测试输出不得与正式技能混在一起。
6. 合并文档/技能时执行 [指导体系维护](references/guidance-maintenance.md)，完成独有约束迁移、兼容导航、引用检查与原handoff续接；不改写历史事实或擅改游戏实现。
