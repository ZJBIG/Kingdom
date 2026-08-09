# Economy Simulation Report - Fast

Offline runtime-aligned simulation; no Unity runtime was launched.
Strict snapshot: 39 resources, 61 buildings, 86 research, 40 Workshop upgrades.

## Rules

- Fixed one-second ticks with integer minute snapshots.
- Research resource costs are paid atomically before progress begins, matching ResearchManager.
- Workshop unlocks, prerequisite chains, costs and effects are included.
- Strategy decisions are emitted to DecisionTrace.csv with deduplicated reasons.
- Research speed uses ResearchPower and runtime era effects.
- Buildings use geometric cost growth and commit immediately after payment.
- Food starts at +5/s; population consumes 0.8 food/s per person and grows toward housing capacity.
- Population growth uses a logistic occupancy factor; over-capacity departure accelerates with relative excess and remains productivity-gated.
- Total productivity equals population x2 plus fixed research and owned-building grants; construction checks pre-build available productivity.
- Productivity-blocked building decision time: total 530 seconds; longest continuous 110 seconds.
- Final population growth: x1.6, 0/min; productivity utilization: 50.5%; territory: 716/2325.

## Animal Age

到达时间: 0 分钟
完成研究: 11

## Neolithic Age

到达时间: 97.15 分钟
完成研究: 12

## Medieval Age

到达时间: 187.58 分钟
完成研究: 15

## Industrial Age

到达时间: 425.42 分钟
完成研究: 4

原始时代最长无研究目标: 0 分钟；新石器时代: 0 分钟。

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
