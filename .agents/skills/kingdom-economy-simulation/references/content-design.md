# 内容设计与质量门槛

作为内容/经济设计唯一详细清单，与根AGENTS硬约束一起使用。此文件保留原内容技能、经济续接prompt及根规则中的独特设计要求；不提供需要重新定义现有类型的旧C#模板。

## 1. 先补连接，再加内容

- 查找已有Research、Workshop、Building、Resource是否可承担目标角色，优先补齐现有解锁/来源/用途而非平行新增。
- 先修可复现玩法bug，完成并验证当前时代生产、研究、工坊和进度闭环，再扩展后时代；不要批量生成未来资产。
- 容量边界继承根AGENTS，完整禁止项及可替代的进度门槛查 `docs/balance/no-resource-caps.md`，不在此维护第二份清单。
- 保留人口与生产力的玩家可见语义。任何人口容量增加，都估算 `PopulationCapacity * FoodConsumptionPerPerson` 满载需求，并验证合理数量的同代Food生产建筑可以支持。

## 2. 新内容必须满足

| 定义 | 必须具备 |
|---|---|
| Resource | 稳定ID、可达来源、至少两个持久用途或一个战略用途、可见且可达解锁路径、UI分类/说明、校验测试，以及跨时代价值 |
| Research | 可达前置与成本、至少一个被实际消费的真实效果；不能只有名字或空效果 |
| Building | 可达建造材料、明确角色、首件回本目标、几何成本增长、来源/用途作用、移动端安全展示；核对生产/消耗和解锁链 |
| Workshop | 解锁条件、研究和工坊前置、完整资源成本、原子购买、实际效果与测试；高级倍率必须先付费，不能绕开已有进度 |

新增资源不能只用于一次建造费/研究费后永久闲置；设计时核对跨时代sink。若只设一个用途，必须说明其持续战略作用，而非用“战略”给孤立资源贴标签。

## 3. 保留旧工业与高级生产价值

- 先进资源用于高级Research、Workshop、Building及晚期战役；生产同时复用高级结构材料和选定的旧材料。
- TitaniumAlloy用于太空结构、科研、高级工坊和星际物流；Nickel及其他低使用率工业材料进入后续合金/相态链。保留铜线、玻璃、陶瓷等既有副产物与用途，不因替代升级而断链。
- PhantomAlloy/PhantomWeave只作允许的设计示例：必须有真实生产建筑、研究门槛、既有工业/高科技投入和多项太空/战争用途，不表示必须新增它们。
- Spacer以后不为每个工业资源增加一个轨道替代厂。通过升级链、工坊、研究和效率延续旧产业；必要的新角色须解释为何已有内容不能承担。
- Sector主要提供领土、原料、一次性战利品和有限战略流量，不能成为高级加工材料的无限主来源或替代玩家建设的先进产业。

## 4. 战役与供给

- 星际战役在物料与时间上保持实质投入，持续消耗Food、燃料、物流相关资源和先进材料。
- 可靠进度须满足军事/物流及供给条件；收益规模匹配晚期投入，不能仅给象征性的几百/几千资源。
- 用生产链、补给、物流、领土、研究和战略消耗解决战役门槛，不恢复普通资源容量墙。
- 对大幅调整同时核对殖民、占领长期产出、维修成本和实际运行节奏，不只看静态奖励总数。

## 5. 数值与已有实现

- 建筑成本采用几何增长；批量费用使用 `ExpantaNumExtensions.GeometricSeriesCost`，最大可买数量使用 `MaxAffordableGeometricSeries` 等当前公开闭式API，不逐座循环。
- 拆除返还按现行交易规则计算最后N座的历史成本及返还率，保留升级/星区例外；先查BuildingManager和测试，不复制孤立公式。
- 研究费用由目标时长和预期ResearchPower推导；首件回本、成本增长带与时代折算以 `docs/balance/balance-model.md` 为方法，静态估算必须由真实运行校准。
- 不提前使用与内容阶段不符的极端记数法；保留旧资源价值。记录字段前后、理论估计和实测节奏；fixture模拟不能替代试玩。
- 直接复用 `Assets/Resources/Script/Data/ResearchEffect.cs`、`Assets/Tests/Editor/ContentProgressionValidatorTests.cs` 及公开强类型API。不复制旧模板重定义ResearchEffectType或修改其序列化编号。

## 6. 设计灵感边界

可借鉴Kittens Game的逐步揭示产业链、旧资源持久价值、研究开启系统、乘数联动、聚落到太空进度。自动化若以后获准，应随进度逐步解锁，而非开局全开放；长期可选自动化仅是设计方向，不授权本轮新增策略AI。

不照抄普通资源容量墙、猫/宗教术语与叙事、具体科技/名称/费用/声望或重置系统。主题以当前剧情与 `docs/content/progression-roadmap.md` 的鼠族文明、人口与生产力、领土、能源物流、星区探索和外星经营式战争为准，不用旧模板改写世界观。

灵感来源（可选阅读，不自动下载，不代表本轮已访问）：
- https://kittensgame.com/
- https://wiki.kittensgame.com/en/general-information/resources
- https://wiki.kittensgame.com/en/game-tabs/science
- https://wiki.kittensgame.com/en/game-tabs/space
