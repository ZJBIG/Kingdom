# Kingdom 数值设计基线

## 1. 建筑价格

推荐公式：

`unitCost(k) = baseCost × growth^k`

`batchCost = baseCost.GeometricSeriesCost(growth, owned, count)`

建议 growth：

- 原料建筑：1.12~1.14
- 加工建筑：1.15~1.17
- 研究/基础设施：1.18~1.20
- 住房：1.18~1.22
- 奇观：只允许1座

普通资源没有库存上限，因此建筑价格增长是长期经济的主要刹车之一。

## 2. 生产

`actualRate = baseRate × amount × efficiency × researchModifiers`

不要让每个科技都只增加 5%。关键科技建议：

- 小升级：×1.15~1.30
- 时代关键升级：×1.5~2.5
- 新生产方式：改变配方或解锁新建筑

## 3. 研究

`ResearchSpeed = (1 + buildingResearchPower) × globalMultiplier`

研究成本必须按目标耗时倒推：

`cost = targetSeconds × expectedResearchPower`

建议目标：

- 首项：30~90秒
- 原始普通节点：2~8分钟
- 原始跃迁：10~20分钟
- 新石器普通节点：5~20分钟
- 新石器跃迁：20~60分钟
- 中世纪完整阶段：4~12小时

## 4. 回本时间

第一座建筑的目标回本：

- 原料：45~120秒
- 加工：90~240秒
- 研究：5~15分钟
- 基础设施：通过新增能力衡量

## 5. 食物

Food 是唯一有容量的库存。

在人口系统前，粮食满足率应影响人工建筑效率。

人口系统完成后：

- 每个人消耗粮食
- 食物不足停止增长
- 长期严重不足导致人口下降
- 自动化建筑主要受能源而不是粮食影响

## 6. 领土和劳动力

它们不是普通资源容量。

- `TerritoryTotal - TerritoryUsed`
- `TotalWorkforce - AssignedWorkforce`

初始值必须足够小，形成实际选择。

## 7. 大数

不要为了使用 ExpantaNum 而提前进入 `ee`、`^^`、`J`。

数值符号应跟随内容增长：

- 原始/新石器：1~1e6
- 中世纪：1e4~1e10
- 工业：1e8~1e20
- 太空：1e18~1e50
- Ultra/Archotech：再逐步进入更高层级

这些只是量级方向，最终以模拟结果为准。
