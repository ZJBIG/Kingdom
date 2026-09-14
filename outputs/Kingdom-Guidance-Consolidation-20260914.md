# Kingdom 指导体系合并报告

日期：2026-09-14。结论：单主入口与按需分流已落盘，文档合并专项检查通过；完整文件系统工具回归仍未通过，不等于游戏验收通过。

## 1. 以后怎么进入

```text
README / 客户端入口 / 旧经济prompt
  → AGENTS.md（全局硬约束）
    → .agents/skills/kingdom-project-dev/SKILL.md（唯一任务路由）
      ├─ 内容、经济、玩法 → kingdom-economy-simulation
      ├─ 布局、交互、Prefab → kingdom-ui-redesign
      ├─ Runtime / State / 存档 → scoped AGENTS + 对应架构专题
      ├─ 测试、构建 → 验证手册 + 测试scope
      └─ Skill / AGENTS / 文档维护 → guidance-maintenance参考
```

同一轮已经读取且未变化的入口不回读；先选一个主分支，实际跨边界时才加读第二个。既有3个规范技能保留，不再新增同义总入口。WorkBuddy项目适配文件继续只引用规范技能。

## 2. 审查口径和原有工作

审查了当前根/scoped AGENTS、3个规范技能及参考资料、适配入口、经济prompt、工程/产品/UI/架构/测试文档、工具说明与指导校验源码；只读巡检覆盖1,736个白名单文件的元数据/指定文本指纹，不代表逐行审查整个游戏。

接手时已有大量未提交的上一轮整理，旧content-expansion六文件已处于删除状态。本轮沿用并完善，不重复归功、不回滚用户改动。未审查Unity缓存、第三方实现和二进制素材的语义；保留日期审计、专题内容设计和历史交接，不将不同主题机械合成巨型文件。

修改前快照对比为29个既有文件变化，另新增1份维护参考；其中handoff同时保留并行清理任务的追加记录，该清理不是本次执行。完整路径、前后哈希和行数见Verification.json。不能把整个git diff都归为本轮成果。

## 3. 实际合并与纠正

| 原重复/冲突 | 当前唯一维护位置与处理 |
|---|---|
| 根规则与主技能两张任务路由 | 路由只在project-dev；根AGENTS保留硬约束并指向主入口 |
| README、经济prompt、领域默认提示各自指挥 | 均先经过根和主入口；领域元数据不复制完整流程 |
| engineering rules与产品默认重复 | 工程规则旧页改兼容指针；产品默认集中conservative-defaults |
| 产品默认重复Current UI contract、桌面与手机基线冲突 | 删除重复与冲突要求；移动端技术基线只留UI技能 |
| UI技能、page-responsibilities、detail-action-policy三份操作规则 | 合入page-responsibilities；旧详情页改指针，UI技能按需引用 |
| Kittens参考边界与经济设计参考重复 | 合入经济content-design，原链接和设计边界保留；旧页改指针 |
| scoped AGENTS重复全局硬约束 | 改为继承，只补目录规则；没有撤销反射/事务/State等门槛 |
| 旧UI-ready计划看似当前待办 | 顶部标明历史，保留原文，禁止据此重建已存在系统 |
| tools说明指向不存在的content-dependency | 改指向现有codex工具及唯一验证手册 |
| 模拟器README错误写成.NET 10/library和固定5检查 | 按csproj/Program纠正为net9.0 Exe，CLI事实与真实代码一致 |
| 活动校验依赖忽略的archive/README | 移除归档强依赖；新增维护参考、UI/产品专题及scope必要项 |

入口与高重复文档样本8份：224行降到135行，减少约40%。该数字只表示这8份热路径文档的行数，不是全仓缩减、Token测量或效率基准；详细知识与维护检查保留在按需参考中。

## 4. 独有信息保留核对

- 全局：禁止新增反射、强类型API/DTO、State唯一可变权威、原子交易、稳定ID/GUID和数值容差。
- Runtime：显式deltaSeconds、禁止BigNumber兼容别名、Pair资产/运行时区别、ExpantaNum ToString可解析持久化。
- 产品：住房降低不追溯踢人；Food/s与粮食不足人口离开条件；不顺便扩展退款、离线进度或建筑优先级。
- UI：资源详情无主按钮；单按钮左右各48内边距；金色Outline仅当前研究，排除其他项及连线；切换清理选择；未占领星区不创建建筑菜单。
- 设计灵感：独立鼠族文明、人口生产力、领土能源物流、星区探索与外星经营式战争；禁止照抄容量墙、名称、费用和重置等；原4个外部参考链接保留，但未宣称本轮访问。
- 权限：禁止自动提交、推送、PR、安装升级、发布或操作真实存档；工具副作用不得由技能文字自动授权。

## 5. 可复用维护能力

新增 `.agents/skills/kingdom-project-dev/references/guidance-maintenance.md`，从主技能路由按需加载。包含主题权威归属、独有约束迁移、修改前快照、旧路径兼容、历史隔离、客户端元数据边界、安全与验证流程。优先维护已有项目技能，不另建全局平行技能。

领域openai.yaml设为 `allow_implicit_invocation: false`，仅对支持该字段的客户端有意义；项目文件存在不等于WorkBuddy已自动注册。当前会话没有做客户端自动发现端到端测试。

## 6. 验证结果

- `GuidanceContractTests ProbeHarnessTests`：23项通过，0失败、0错误、0跳过。14项新增只读真实仓库/内存反例契约，9项既有控制流mock检查。
- `validate-guidance.ps1`：退出0；必要路径还由只读测试独立核对。
- `kingdom_project_probe.py --check-skill`：1,736文件，Errors=0；仅静态引用/指纹口径。
- 在58份指导文件上核对检查前后SHA-256一致，验证过程未修改这些文件；没有创建fixture或运行真实删除。
- 两个Python文件AST通过；`git diff --check`通过。Git提示LF/CRLF规范化不是测试失败，未为消除提示全仓格式化。
- **未执行真实 Unity 编译。** 未运行游戏EditMode/PlayMode、经济闭包、模拟器、Android或设备验证。

未解决：旧完整文件系统/真实链接回归仍未通过。已有记录显示链接创建得到普通目录、fixture清理受保护阻断；本次没有重跑该套件，没有绕过保护，没有以23项mock/只读通过替代它。

## 7. 安全与复核限制

专项安全审查能力本会话不可用，采用人工静态审查，并非安全认证。核对了技能正文、全部references/assets及相关巡检/测试源码；本次未新增联网、下载执行、凭据访问、提权、全局安装或游戏数据修改。存量完整工具测试包含显式fixture创建/删除副作用，已与本次只读检查隔离并继续停用。

辅助审查Explore-2因提供方400参数拒绝失败，Explore-1因500服务不可用失败；未取得独立复审结果，主代理接管阅读和复核。需要独立复审时可在服务恢复后另行重试，不应把失败代理算作通过证据。

## 8. 交付与恢复

- 本报告：`outputs/Kingdom-Guidance-Consolidation-20260914.md`。
- 验证明细：`outputs/Kingdom-Guidance-Consolidation-20260914-Verification.json`，包含本轮命令输出、变更前后哈希及限制。
- 项目指导快照包：`outputs/Kingdom-Guidance-Integration-20260914.zip`。含47份指导/工具文件及哈希清单，CRC和47份内容哈希全部核验。保留仓库相对路径，排除审计报告、用户日志、归档和缓存；不是通用全局Skill安装包，不自动覆盖应用。
- 修改前56文件备份：`.codex/archive/guidance-before-20260914-124959.zip`。
- 模拟器README补充备份：`.codex/archive/guidance-extra-before-20260914.zip`。

两份备份已校验CRC及原内容。没有删除原指导文件；兼容路径继续有效。恢复或应用快照前，先比较工作树和并行任务的最新改动，按文件选择，不整包覆盖未提交工作。

后续直接从根AGENTS开始即可；完整工具回归和客户端自动发现仍需分别在合适环境验证。
