# Medieval Research Effect Role Review

审查范围：当前 `Assets/Resources/Datas/Research/Medieval` 下的 12 项
Research 资产，以及 `ResearchEffectType` 的现有运行时处理。

## 结论

Todolist 中“Fortification / GuildSystem / Gunpowder / StandingArmy
都使用全局生产效率”的旧描述已经不符合当前资产。当前四项分别承担建设、
研究、物流和人口生产力角色，不需要为了制造差异而任意替换效果或新增
`ResearchEffectType`。

| Research | 当前决策角色 | 现有效果角色 | 与邻近节点的区别 | 数值处理 |
| --- | --- | --- | --- | --- |
| Bookmaking | 扩大知识复制与研究投入 | 全局研究 | 研究基础设施与知识传播 | 暂不调整 |
| FeudalAdministration | 扩大治理范围 | 领土 | 领土与行政承载，而非生产效率 | 暂不调整 |
| Fortification | 降低建设阻力 | 全局建设 | 公共设施与长期建设 | 暂不调整 |
| GuildSystem | 提高知识制度化效率 | 全局研究 | 行会传承与研究体系，区别于 Bookmaking 的复制传播 | 暂不调整 |
| Gunpowder | 维持区域运输与补给 | 全局物流 | 军需标准化带来的物流能力 | 暂不调整 |
| MechanicalEngineering | 提升机械化生产与建设能力 | 全局食物生产、全局建设 | 动力机械同时影响基础供给与建设 | 暂不调整 |
| PublicHealth | 支撑人口持续增长 | 建设、人口增长、人口生产力、幸福度 | 人口与公共卫生复合节点 | 暂不调整 |
| ScholasticInstitutions | 建立持续研究机构 | 建设、全局研究 | 学院建设与研究能力的联动 | 暂不调整 |
| StandingArmy | 形成稳定的标准化劳动流程 | 人口生产力 | 生产力组织，不是军事倍率 | 暂不调整 |
| Steelmaking | 建立钢铁材料路线 | 资源生产、领土、人口生产力、建设 | 材料与工业基础的复合节点 | 暂不调整 |
| TradeRoutes | 降低道路与贸易网络建设阻力 | 全局建设 | 物流网络的建设前置 | 暂不调整 |
| UrbanHousing | 提高城市承载并支持拆建循环 | 全局建设、拆除返还 | 城市住房与建设回收 | 暂不调整 |

## 重叠与后续边界

建设类效果在 Medieval 仍然较多，但它们对应不同的决策入口：

- Fortification：是否先建设公共设施；
- ScholasticInstitutions：是否投资学院与研究基础；
- TradeRoutes：是否建设贸易和运输网络；
- UrbanHousing：是否扩张住房并保留拆建回收；
- Steelmaking：是否先完成材料工业基础；
- PublicHealth：是否优先人口与幸福度基础设施；
- MechanicalEngineering：是否优先机械化供给与建设。

这些差异可以由现有效果类型表达，当前没有“完全不能表达”的角色，
因此不新增 Modifier 类型。由于本仓库不把冻结模拟器输出作为当前节奏证据，
也没有当前 Unity 实测数据证明需要改变倍率；数值调节留到真实 Medieval
流程实测后，按实际瓶颈来源单独处理。

## 验证边界

- 研究定义的静态可达性由当前 content closure 检查覆盖。
- 本审查不改变 Research ID、前置、成本、效果值、存档格式或运行时规则。
- 研究时长与效果强度仍需用户在 Unity Medieval 流程中实测后决定。
