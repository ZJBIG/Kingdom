# Kingdom 内容进度路线图

## 核心体验

玩家带领鼠族文明从原始聚落发展到跨星际文明，逐步理解：

`资源 → 建筑 → 人口与生产力 → 研究 → 加工链 → 时代目标`

Food 是唯一有库存上限的资源；普通资源不引入容量或仓库系统。

## 当前里程碑

### Milestone A：0~10 分钟开局可玩

- 新档提供有限的 WoodLog 起始库存。
- 教程精确指向真实资源、住房、研究和生产链对象。
- Resources 步骤必须查看 Food 或 WoodLog 详情后完成。
- 记录首个资源详情、建筑、人口、研究、生产链和时代里程碑。

### Milestone B：Animal → StoneAge

- 通过 Agriculture、AnimalHusbandry、ClayExtraction 和真实资源条件完成 StoneAgeSettlement。
- Era 页面区分时代推进硬条件与建议准备度。

### Milestone C：StoneAge → MiddleAge

- 通过青铜、铁器、文字记录和 FeudalAdministration 的真实资源条件推进。
- 中古时代研究节点按建设、生产、研究和人口等不同决策角色组织。

### Milestone D：MiddleAge → Industrial

- 通过 MechanicalEngineering、Steelmaking 和 Industrialization 进入工业时代。
- 工业主线以“当前 / 下一步 / 之后”三步路线呈现，不新增第二套任务系统。

## 中古研究的决策角色

以下保留内容设计上的决策区别，实际效果以定义与运行时消费端为准。建设类节点较多不等于同质化；现有效果类型足以表达这些角色，不为制造差异新增Modifier或任意换倍率。强度与等待时间只在真实流程证据支持下按主要瓶颈单独调整。

| Research | 决策角色 | 效果与邻近节点的区别 |
|---|---|---|
| Bookmaking | 扩大知识复制与研究投入 | 全局研究，侧重知识传播 |
| FeudalAdministration | 扩大治理范围 | 领土与行政承载 |
| Fortification | 先建设公共设施 | 全局建设，降低公共设施与长期建设阻力 |
| GuildSystem | 提高知识制度化效率 | 全局研究，侧重行会传承与研究体系 |
| Gunpowder | 维持区域运输与补给 | 全局物流，侧重军需标准化 |
| MechanicalEngineering | 优先机械化供给与建设 | 全局食物生产与全局建设 |
| PublicHealth | 优先人口与幸福度设施 | 建设、人口增长、人口生产力与幸福度 |
| ScholasticInstitutions | 投资学院与持续研究 | 建设与全局研究联动 |
| StandingArmy | 建立标准化劳动流程 | 人口生产力，而非军事倍率 |
| Steelmaking | 完成材料工业基础 | 资源生产、领土、人口生产力与建设 |
| TradeRoutes | 先建设贸易与运输网络 | 全局建设，降低网络建设阻力 |
| UrbanHousing | 扩张住房并保留拆建回收 | 全局建设与拆除返还 |

## 后续方向

- 进入 Spacer 后优先完善已有远征补给、舰队维修和星区反馈。
- 本星系引导复用`TutorialManager`现有目标：完成`HomeSystemSurvey`后先续接正在进行的殖民，否则按稳定ID提示首个真实可达的未占领本星系目标；`LaunchCenter`与其他准入条件不被推荐绕过。已有首个据点后返回`DeepSpaceFleet`/`InterstellarNavigation`远航研究路线，不新增任务状态。
- Research Tree 提供当前目标定位和未满足前置路径高亮；不额外提供无实际收益的当前时代跳转。
- Story 继续作为由真实玩法状态按顺序永久完成的文明记忆，不提供经济奖励；当前唯一支持的存档 v9 必须包含剧情完成段。
- 后期材料生产采用
  “少数综合工厂 + 多种基础设施机制”的方向：Spacer 由 `OrbitalResourceExtractionArray`
  承担绝大部分材料产出，并以巨构级空间、生产力、能源、物流与原料消耗作为后期门槛；生态农业阵列保留 Food/生物质/木材角色，Ultra 由
  `AutonomousMatterFabricator` 承担主要高级材料、物流与舰队支援，并采用更高一档的巨构维护成本；Ultra 工坊分别扩展生产、研究和物流能力。
- 幽影材料保留`PhantomMaterialsFabricator`专门供给；能源与科研基础设施保留独立角色。综合材料厂方向不意味着将这些不同能力合并为同一建筑，也不为每种材料再建独立轨道替代厂。
- Ultra研究把既有运行时能力延伸到远征推进、战役连续性、补给适应、舰队维修、伤亡控制、占领治理、能源、生态和人口生产力，仍不增加第二座材料工厂。
- Ultra文明工程已经具备阶段推进、持续供给、暂停/恢复、最终提交与姿态能力；验收继续使用现有`UltraProjectManager`与v9可选扩展，不将旧R1之外的规划措辞误当作该系统尚未实现。回声铸造环在比邻星前哨发现，保留 Ultra、研究、巨构与首阶段认证准入，为后续鲸鱼座与天狼星两场既有战役提供支援；已实现调查、互斥路线与远征支援，当前合同和证据见`outputs/阶段交付.md`；Archotech及更多独立机制仍需另行授权。

## 验收顺序

1. 静态闭包、资源流和 C# / Unity 编译。
2. EditMode 与 PlayMode 测试隔离及全绿。
3. Animal → StoneAge → MiddleAge → Industrial 的无作弊流程。
4. 外部运行体验由用户自行验收，不作为代理交付门槛。

离线模拟器按经济技能用于确定性回归诊断，不用于策略搜索、替代 Unity 运行时证据或调节当前经济数值。
