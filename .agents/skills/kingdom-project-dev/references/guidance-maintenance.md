# 指导体系维护：单入口、单一权威、按需加载

仅在合并、添加或修订 Skill / AGENTS / 开发文档时读取。优先扩展现有主题，不因已有文件多而新增总览、同义技能或第二套路由。

## 1. 入口和归属

固定进入顺序：根 `AGENTS.md` → `kingdom-project-dev/SKILL.md` → 一个相关领域或专题。下面是维护归属清单，不是要求每次开发全部读取的列表。

| 主题 | 唯一详细维护位置 | 不再重复的位置 |
|---|---|---|
| 全局硬约束、工作树与权限 | 根 `AGENTS.md` | 领域技能只引用；scoped AGENTS 仅补目录约束 |
| 任务分流与实施流程 | 本技能 `SKILL.md` | README、经济prompt、客户端适配只指向主入口 |
| Runtime代码约束 / 测试编写约束 | 对应 `Assets/**/AGENTS.md` | 不复制根约束或命令手册 |
| 文件/API/tick顺序定位 | `docs/repository-map.md` | 不在技能中维护第二份项目地图 |
| State/事务/时钟/存档 | `docs/architecture/runtime-state.md` | 工程规则旧入口仅导航 |
| Pair资产/运行时及兼容 | `docs/architecture/serialized-pairs.md` | 不新增旧类型模板 |
| 研究队列/支付台账 | `docs/architecture/research-queue-payment.md` | UI只定义按钮语义，不复制付款算法 |
| 产品默认 | `docs/decisions/conservative-defaults.md` | `docs/rules/kingdom-rules.md` 兼容导航 |
| 内容/经济执行顺序与输入契约 | `kingdom-economy-simulation/SKILL.md` | 不恢复 content-expansion 平行技能 |
| 内容质量与设计灵感边界 | 经济技能 `references/content-design.md` | kittens-game-reference-boundary 兼容导航 |
| 容量禁止项及允许的替代门槛 | `docs/balance/no-resource-caps.md` | 根仅保留禁止退化摘要 |
| 数值方法与暂定目标带 | `docs/balance/balance-model.md` | 不把设计目标写成当前实测值 |
| 当前内容方向 / 专题内容 | `docs/content/progression-roadmap.md` 及相关专题 | 不合并不同主题的设计资料 |
| UI布局/图/手势/设备技术契约 | `kingdom-ui-redesign/SKILL.md` | 旧UI计划不构成当前实施任务 |
| 页面职责/详情按钮/选择态 | `docs/ui/page-responsibilities.md` | detail-action-policy 兼容导航；UI技能引用 |
| UI状态所有权/刷新/生命周期 | `docs/architecture/ui-boundaries.md` | 不重复页面操作表或布局数值 |
| 行为验收 / PlayMode场景 | `docs/testing/acceptance-checklist.md` / `playmode-test-plan.md` | content-balance-tests 兼容导航；场景可引用规则作断言，不另立规则 |
| 命令/副作用/存档隔离 | 本技能 `references/validation.md` | tools/README 只索引实际工具 |
| 协作 / 证据 / 交接 | 本技能 subagents、evidence参考和handoff-template | 只更新同主题当前handoff，不将历史复制为长期规则 |

## 2. 合并流程

1. 确认项目标记，读取当前文件及Git工作树；区分既有改动和本轮改动。排除Unity缓存、第三方实现、二进制素材与个人目录。
2. 按上述主题归属逐段分类：重复、独有、冲突、历史。先列迁移表，再缩减旧文件；不能以关键词相似代替语义核对。
3. 对本轮拟改文件保留精确的修改前快照和哈希，验证备份可读。不回滚用户工作，不把已有未提交删除归为本轮成果。
4. 将独有条款完整安置到权威位置。用源码/资产确认实现事实，保留未实现的硬约束；产品改变须另行授权。不要因文档过期重建系统。
5. 将旧公开路径缩为兼容指针，链接到实际权威位置；历史计划只加明显的历史状态说明，保留原文。不将历史引用全局替换成当前事实。
6. 主入口只维护一张分流表；子技能直接命中时先完成主入口范围检查，同轮已读入口不重复加载。简单任务不强制全读参考资料。
7. 同步必要路径清单、客户端适配、技能元数据、相关README与原handoff。不要默认新增全局Skill：Kingdom专属知识随仓库维护。

## 3. 安全与验收

- 审查全部改动的技能正文、references、assets与关联脚本。禁止自动下载执行、读取凭据、扩权、访问真实存档、隐藏写入或伪造结果；专项能力不可用时明确披露人工静态审查。
- 先静态核对角色和路径，再运行现有只读巡检与无副作用回归；任何会创建/清理fixture的测试先确认隔离和授权，保护阻断后停止，不绕过。
- 检查正向路由，也检查反向约束：入口不膨胀，旧文档不重新出现独立规则，安全门槛不丢失，历史不进入必需文件清单。
- 活动文档不得依赖忽略的归档文件；归档不存在不应阻断正常开发。局部相对链接与根路径说明采用各自校验方式，不谎称简单链接扫描验证了所有Markdown锚点。
- 对照 `references/acceptance-cases.md` 的实际任务场景。链接存在不等于客户端自动触发已验证；纯文档整理不启动Unity，不声称游戏回归通过。
- 客户端元数据只保留入口信息，不复制规则。领域 `agents/openai.yaml` 的 `allow_implicit_invocation: false` 仅约束支持该字段的客户端；不能据此声称WorkBuddy已自动注册或强制拦截全部入口。
- 交付只包含明确列出的项目指导文件与校验源码，保留目录层级；不夹带归档、用户数据、个人路径、缓存或旧测试日志。提供合并摘要、验证限制和可恢复备份位置。
