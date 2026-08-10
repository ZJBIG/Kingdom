# Economy Simulation Report - Normal

Offline runtime-aligned simulation; no Unity runtime was launched.
Strict snapshot: 40 resources, 65 buildings, 91 research, 48 Workshop upgrades.

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
- Productivity-blocked building decision time: total 135 seconds; longest continuous 45 seconds.
- Final population growth: x2, 0/min; productivity utilization: 57.1%; territory: 706/2325.

## Animal Age

到达时间: 0 分钟
完成研究: 11

## Neolithic Age

到达时间: 96.6 分钟
完成研究: 23

## Medieval Age

到达时间: 307.25 分钟
完成研究: 4

## Industrial Age

到达时间: 609.12 分钟
完成研究: 11

原始时代最长无研究目标: 2 分钟；新石器时代: 2 分钟。

## Bottlenecks

- Clay: single producer
- PlantFiber: single producer
- StoneChunk: single producer
- WoodLog: single producer

## Warnings

None

## Validation boundary

未执行真实 Unity 编译。

未执行 Huawei P40 Pro 真机验收。
