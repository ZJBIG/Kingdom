# Economy Simulation Report - Normal

Offline runtime-aligned simulation; no Unity runtime was launched.

## Rules

- Fixed 0.1 second ticks, minute aggregation, stockpiles clamp at zero.
- Research cost is paid progressively; research speed uses ResearchPower and runtime era effect.
- Buildings can repeat and use geometric cost growth; construction is immediate after payment.


# Animal Age

预计时间: 0分钟

研究完成数: 13; 资源归零时间: 0 分钟。

# Neolithic Age

预计时间: 152分钟

研究完成数: 39; 资源归零时间: 0 分钟。

# Medieval Age

预计时间: 1385分钟

研究完成数: 3; 资源归零时间: 0 分钟。

# Industrial Age

预计时间: 不可达

研究完成数: 0; 资源归零时间: 0 分钟。

## Bottlenecks

- Copper: single producer
- Tin: single producer
- WoodLog: single producer
- CopperOre: single producer
- IronOre: single producer
- Clay: single producer
- StoneChunk: single producer
- TinOre: single producer
- PlantFiber: single producer

## Warnings

- **Medium Single point bottleneck** Copper: Only one defined producer serves BronzeFoundry. Suggestion: Add an alternative producer or move the dependency later.
- **Medium Single point bottleneck** Tin: Only one defined producer serves BronzeFoundry. Suggestion: Add an alternative producer or move the dependency later.
- **Medium Single point bottleneck** WoodLog: Only one defined producer serves CharcoalKiln. Suggestion: Add an alternative producer or move the dependency later.
- **Medium Single point bottleneck** CopperOre: Only one defined producer serves CopperSmelter. Suggestion: Add an alternative producer or move the dependency later.
- **Medium Single point bottleneck** IronOre: Only one defined producer serves IronSmelter. Suggestion: Add an alternative producer or move the dependency later.
- **Medium Single point bottleneck** Clay: Only one defined producer serves PotteryKiln. Suggestion: Add an alternative producer or move the dependency later.
- **Medium Single point bottleneck** StoneChunk: Only one defined producer serves StoneCuttingWorkshop. Suggestion: Add an alternative producer or move the dependency later.
- **Medium Single point bottleneck** TinOre: Only one defined producer serves TinSmelter. Suggestion: Add an alternative producer or move the dependency later.
- **Medium Single point bottleneck** PlantFiber: Only one defined producer serves WeavingWorkshop. Suggestion: Add an alternative producer or move the dependency later.

## Validation boundary

未执行真实 Unity 编译。

未执行 Huawei P40 Pro 真机验收。
