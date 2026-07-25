# 外星战争第一版设计

## 不做即时战斗

第一版使用确定性经营战役，以便复用现有 State/Manager/Simulation 架构。

## 核心数据

- `SectorDefinition`
- `SectorState`
- `MilitaryState`
- `CampaignState`

## 核心数值

- AttackPower
- DefensePower
- FleetPower
- MilitaryManpower
- SupplySatisfaction
- EnemyPower
- CampaignProgress
- Casualties

## 进度

`ratio = effectivePlayerPower / enemyPower`

- ratio < 0.7：不推进且损耗
- 0.7~1.0：慢速推进
- 1.0~2.0：正常提升
- >2.0：软上限，避免瞬间跳过

## 奖励

- Territory
- 星区
- 矿藏
- 外星科技样本
- 剧情事件

## 边界

- 战斗逻辑不在 UI。
- 失败不随机清空存档。
- 不依赖普通资源仓储上限。
- 先完成工业、能源、物流和人口后再实现。
