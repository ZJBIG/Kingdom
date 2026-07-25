# Kingdom5 内容与数值静态审查

基线：`Kingdom5.7z`  
日期：2026-07-25  
Unity：2022.3.62f2c1

## 项目规模

- Resource：35
- Building：15
- Research：28
- 可从新游戏静态到达的 Research：13
- 当前静态最高可达 TechLevel：Animal/原始
- 当前 EditMode：33/33 通过
- 当前 PlayMode：0 个用例

## 时代覆盖

- Building：Animal 6、Neolithic 9、Medieval 及以后 0。
- Research：Animal 13、Neolithic 14、Medieval 1。
- 当前没有完整中世纪内容。

## 当前主线阻断

1. `Clay` 无生产建筑，`Pottery` 无法支付。
2. `PlantFiber` 无生产建筑，`TextileCraft` 无法支付。
3. `CopperOre`、`TinOre`、`IronOre` 无生产矿井。
4. `NeolithicSettlement` 因 Pottery/TextileCraft 不可达，无法进入新石器。
5. `SmithingRevolution.AdvancesTechLevel=false`，无法推进到中世纪。
6. `Mining_Quartz` 没有对应 Quartz 资源、产出和用途。
7. `PotteryKiln`、`WeavingWorkshop` 当前没有加工产物。
8. `StoneTool` 存在但没有来源和实际循环。
9. `Smithing_Bronze` 没有使用锡锭。
10. `GameBootstrap` 直接添加 Gold，属于调试残留。

## 资源利用

当前实际参与任意生产、消耗、建筑成本或科研成本的资源约 13 个：

Bronze, Clay, Coal, Copper, CopperOre, Iron, IronOre, PlantFiber, StoneBrick_Marble, StoneChunk_Marble, Tin, TinOre, WoodLog

当前未参与实际循环的资源约 22 个：

Citrine, Diamond, EbonyLog, ElvenWoodLog, Emerald, Gold, GoldOre, Jade, Mithril, MithrilOre, RoseWoodLog, Ruby, Sapphire, ScentedWoodLog, Silver, SilverOre, Steel, StoneTool, Titanium, TitaniumOre, Uranium, WhiteBirchLog

原则：不要立刻为这些闲置资源批量补内容。应按时代逐个启用，每个资源先满足“至少一个来源、至少两个用途或一个战略用途”。

## 数值风险

- 新游戏仅 `WoodLog +1/s`。
- 多数早期研究耗时 20~167 分钟，首小时决策密度过低。
- `SmithingRevolution=1e8` 在 1/s 下约 3.17 年。
- 没有研究力建筑。
- 建筑成本线性，与“普通资源无限库存”组合后长期滚雪球失控。
- 初始 Food 10000/10000，使 Granary/Pottery food capacity 早期没有价值。
- 初始 Space 100000，早期占地几乎无意义。
- Food 为 0 时没有明确的人口/劳动力后果。
- `GameState.AdvanceFood` 无条件增加 Version，可能导致持续 dirty/刷新。
- `SimulationManager` 为 50 Tick/s、单帧最多 1000 Tick，对移动端偏高。
- `Application.runInBackground=true` 不适合作为移动端默认。

## 项目分辨率检查

- `ProjectSettings.asset`：默认 1920×1080。
- `SampleScene` 主 Canvas：`Constant Pixel Size`。
- Canvas Reference Resolution：800×600，但在 Constant Pixel Size 下并不真正驱动自适应。
- 项目仍允许四方向自动旋转。
- 这与“内容扩充”不是同一阶段，但在 Huawei P40 Pro 发布前仍需真实确认。

## 核心建议

当前不要直接添加工业、太空和外星战争资产。

正确顺序：

1. 建立可达性/来源用途测试；
2. 修复原始和新石器硬阻断；
3. 引入等比建筑成本；
4. 引入研究效果和研究力；
5. 完成原始→新石器→中世纪纵向切片；
6. 再加入人口、领土；
7. 填充中世纪；
8. 工业、电力、太空、外星战争依次推进。
