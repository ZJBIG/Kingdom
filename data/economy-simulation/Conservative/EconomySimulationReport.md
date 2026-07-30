# Economy Simulation Report - Conservative

Offline runtime-aligned simulation; no Unity runtime was launched.

## Rules

- Fixed one-second ticks with integer minute snapshots.
- Research is selected first; resource costs are paid progressively before progress begins.
- Research speed uses ResearchPower and runtime era effects.
- Buildings use geometric cost growth and commit immediately after payment.
- Food starts at +5/s; population consumes 1 food/s per person and grows toward housing capacity.
- Population growth is base 1/min x completed-research multiplier x food satisfaction; departure remains 1/min.
- Total productivity equals population plus fixed research and owned-building grants; construction checks pre-build available productivity.
- Productivity-blocked building decision time: total 0 seconds; longest continuous 0 seconds.
- Final population growth: x1.725, 1.489/min; productivity utilization: 58.9%; territory: 594/2375.

## Animal Age

到达时间: 0 分钟
完成研究: 8

## Neolithic Age

到达时间: 98.4 分钟
完成研究: 44

## Medieval Age

到达时间: 770.83 分钟
完成研究: 9

## Industrial Age

到达时间: 1247.7 分钟
完成研究: 8

原始时代最长无研究目标: 4 分钟；新石器时代: 4 分钟。

## Bottlenecks

- Clay: single producer
- Copper: single producer
- CopperOre: single producer
- Iron: single producer
- IronOre: single producer
- PlantFiber: single producer
- Pottery: single producer
- Silica: single producer
- Steel: single producer
- StoneChunk: single producer
- Tin: single producer
- TinOre: single producer
- WoodLog: single producer

## Warnings

None

## Validation boundary

未执行真实 Unity 编译。

未执行 Huawei P40 Pro 真机验收。
