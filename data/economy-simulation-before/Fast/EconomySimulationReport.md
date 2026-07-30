# Economy Simulation Report - Fast

Offline runtime-aligned simulation; no Unity runtime was launched.

## Rules

- Fixed one-second ticks with integer minute snapshots.
- Research is selected first; resource costs are paid progressively before progress begins.
- Research speed uses ResearchPower and runtime era effects.
- Buildings use geometric cost growth and commit immediately after payment.
- Food starts at +5/s; population consumes 0.8 food/s per person and grows toward housing capacity.
- Population growth uses a logistic occupancy factor; over-capacity departure accelerates with relative excess and remains productivity-gated.
- Total productivity equals population x2 plus fixed research and owned-building grants; construction checks pre-build available productivity.
- Productivity-blocked building decision time: total 60 seconds; longest continuous 60 seconds.
- Final population growth: x2.415, 0/min; productivity utilization: 47.6%; territory: 990/2375.

## Animal Age

到达时间: 0 分钟
完成研究: 9

## Neolithic Age

到达时间: 158.33 分钟
完成研究: 43

## Medieval Age

到达时间: 693.35 分钟
完成研究: 9

## Industrial Age

到达时间: 957.08 分钟
完成研究: 16

原始时代最长无研究目标: 0 分钟；新石器时代: 0 分钟。

## Bottlenecks

- Clay: single producer
- CopperOre: single producer
- Iron: single producer
- IronOre: single producer
- PlantFiber: single producer
- Pottery: single producer
- Silica: single producer
- StoneChunk: single producer
- TinOre: single producer
- WoodLog: single producer

## Warnings

None

## Validation boundary

未执行真实 Unity 编译。

未执行 Huawei P40 Pro 真机验收。
