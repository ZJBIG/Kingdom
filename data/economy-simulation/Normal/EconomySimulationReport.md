# Economy Simulation Report - Normal

Offline runtime-aligned simulation; no Unity runtime was launched.

## Rules

- Fixed one-second ticks with integer minute snapshots.
- Research is selected first; resource costs are paid progressively before progress begins.
- Research speed uses ResearchPower and runtime era effects.
- Buildings use geometric cost growth and commit immediately after payment.
- Food starts at +5/s; population consumes 1 food/s per person and grows toward housing capacity.
- Population growth is base 1/min x completed-research multiplier x food satisfaction; departure remains 1/min.
- Total productivity equals population plus fixed research and owned-building grants; construction checks pre-build available productivity.
- Productivity-blocked building decision time: total 3465 seconds; longest continuous 540 seconds.
- Final population growth: x2.415, 0.882/min; productivity utilization: 60.6%; territory: 976/2375.

## Animal Age

到达时间: 0 分钟
完成研究: 8

## Neolithic Age

到达时间: 120.78 分钟
完成研究: 44

## Medieval Age

到达时间: 541.02 分钟
完成研究: 9

## Industrial Age

到达时间: 916.72 分钟
完成研究: 16

原始时代最长无研究目标: 1 分钟；新石器时代: 2 分钟。

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
