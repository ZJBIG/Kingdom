# 本星系探索奖励约束

本星系（`SectorDefinition.IsHomeSystem`）探索/殖民行动只消耗 Food 与配置的行动资源。

成功后只授予星区状态和 `TerritoryReward`；`ResourceRewards` 必须为空。星区已占领后的
`OccupiedResourceRatesPerSecond` 是持续战略产出，不属于探索成功的一次性资源奖励，仍可保留。
本星系探索不使用敌方强度、攻击力或防御力作为门槛；探索能否推进只由已解锁状态、Food 与该星区配置的持续行动资源决定。探索技术效果用于提高推进速度，不改变资源奖励规则。

运行时 `SectorValidator` 与 `SectorManager` 都会拒绝带有本星系资源奖励的定义；
`SectorManagerTests.C705_HomeSystemExplorationGrantsOnlyTerritory` 覆盖所有本星系资产。
本星系探索不使用敌方强度、攻击力或防御力作为门槛；这些字段只对远星战役有意义。探索能否推进只由已解锁状态、Food 与该星区配置的持续行动资源决定。
