# 建筑角色分类与成本增长判断

更新：2026-10-07。保留字段派生、多角色与断言边界，删除旧69座分类统计及过时growth均值表。

| 角色 | 字段判据 |
|---|---|
| Sector | sector引用存在 |
| Housing | populationCapacityGranted为正 |
| FoodProducer | foodProductionRate为正 |
| FoodBuffer | foodCapacityGranted为正，且不产粮 |
| Research | researchPowerGranted为正 |
| Productivity | productivityGranted为正，记录直接提供生产力的能力 |
| Infrastructure | 电力或物流产出为正 |
| Military | 舰队/攻击/防御/军力授予存在 |
| Converter | 有资源产出和持续消耗 |
| Extraction | 有产出、无持续资源消耗 |
| Consumer | 有持续消耗、无资源产出 |

保留多重角色。附带防御不能把科研/住房/生态全部误标为军事；耗粮不等于产粮。Productivity是补充能力标签，不改变下述主角色优先级，也不能把提供生产力与消耗生产力混为一项。
主角色可按Sector→Housing→FoodProducer→FoodBuffer→Research→Infrastructure→Military→Converter→Extraction→Consumer选取，但全部角色仍记录；主角色优先级只是分析工具，不是运行规则。

costGrowth分带用于比较角色/时代设计，不用目录或名称写死固定区间；几何成本与闭式批量计算有数学回归，可调资产数值不应精确锁死。
发现高增长或复合角色不自动判bug；先看实际成本、供給、用途与运行收益。
未执行真实 Unity 编译。
