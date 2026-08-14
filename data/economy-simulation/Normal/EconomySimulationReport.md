# Economy Simulation Report - Normal

Offline runtime-aligned simulation; no Unity runtime was launched.
Strict snapshot: 40 resources, 71 buildings, 113 research, 65 Workshop upgrades.

## Rules

- Adaptive ticks: one second in Animal/Neolithic, ten seconds in Medieval, ten minutes in Industrial, and thirty minutes in Spacer/Ultra/Archotech; rates remain per-second and snapshots remain ten-minute aligned.
- Each route runs for a 30-day observation horizon so late-era construction, workshops and supply chains are visible; reports sample every 10 minutes.
- Research resource costs are paid atomically before progress begins, matching ResearchManager.
- Workshop unlocks, prerequisite chains, costs and effects are included.
- Strategy decisions are emitted to DecisionTrace.csv with deduplicated reasons.
- Research speed uses ResearchPower and runtime era effects.
- Buildings use geometric cost growth and commit immediately after payment.
- Food starts at +5/s; population consumes 0.8 food/s per person and grows toward housing capacity.
- Population growth uses a logistic occupancy factor; over-capacity departure accelerates with relative excess and remains productivity-gated.
- Total productivity equals population x2 plus fixed research and owned-building grants; construction checks pre-build available productivity.
- Sector occupation/campaigns are not simulated; territory totals therefore include research and Workshop effects only, not Sector territory rewards.
- Productivity-blocked building decision time: total 187380 seconds; longest continuous 184365 seconds.
- Final population growth: x1.6, 51.296/s; productivity utilization: 105.8%; territory: 666/2475.

## Animal Age

到达时间: 0 分钟
完成研究: 11

## Neolithic Age

到达时间: 75.43 分钟
完成研究: 23

## Medieval Age

到达时间: 237.73 分钟
完成研究: 3

## Industrial Age

到达时间: 528.08 分钟
完成研究: 17

## Spacer Age

到达时间: 不可达
完成研究: 0

原始时代最长无研究目标: 0 分钟；新石器时代: 0 分钟。

## Bottlenecks

- Aluminum: single producer
- CopperWire: single producer

## Warnings

None

## Validation boundary

未执行真实 Unity 编译。

未执行 Huawei P40 Pro 真机验收。
