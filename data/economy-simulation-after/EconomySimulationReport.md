# Economy Simulation Report - Normal

Offline runtime-aligned simulation; no Unity runtime was launched.

## Rules

- Fixed one-second ticks with integer minute snapshots.
- Research is selected first; resource costs are paid progressively before progress begins.
- Research speed uses ResearchPower and runtime era effects.
- Buildings use geometric cost growth and commit immediately after payment.
- Food starts at +5/s; population consumes 0.8 food/s per person and grows toward housing capacity.
- Population growth uses a logistic occupancy factor; over-capacity departure accelerates with relative excess and remains productivity-gated.
- Total productivity equals population x2 plus fixed research and owned-building grants; construction checks pre-build available productivity.
- Productivity-blocked building decision time: total 0 seconds; longest continuous 0 seconds.
- Final population growth: x1.725, 0/min; productivity utilization: 48.7%; territory: 645/2325.

## Animal Age

到达时间: 0 分钟
完成研究: 9

## Neolithic Age

到达时间: 165.72 分钟
完成研究: 42

## Medieval Age

到达时间: 672.67 分钟
完成研究: 9

## Industrial Age

到达时间: 788.08 分钟
完成研究: 7

原始时代最长无研究目标: 2 分钟；新石器时代: 2 分钟。

## Bottlenecks

- Clay: single producer
- CopperOre: single producer
- Iron: single producer
- IronOre: single producer
- PlantFiber: single producer
- StoneChunk: single producer
- TinOre: single producer
- WoodLog: single producer

## Warnings

None

## Validation boundary

未执行真实 Unity 编译。

未执行 Huawei P40 Pro 真机验收。
