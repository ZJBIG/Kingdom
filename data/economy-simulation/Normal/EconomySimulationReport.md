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
- Productivity-blocked building decision time: total 177120 seconds; longest continuous 1800 seconds.
- Final population growth: x2, 224.602/s; productivity utilization: 188.2%; territory: 1002/2325.

## Animal Age

到达时间: 0 分钟
完成研究: 11

## Neolithic Age

到达时间: 76.32 分钟
完成研究: 23

## Medieval Age

到达时间: 317.63 分钟
完成研究: 3

## Industrial Age

到达时间: 826.32 分钟
完成研究: 20

## Spacer Age

到达时间: 不可达
完成研究: 0

原始时代最长无研究目标: 0 分钟；新石器时代: 0 分钟。

## Bottlenecks

- Aluminum: single producer
- CopperWire: single producer

## Warnings

- **Info Legacy metal surplus** Bronze: Active production is 17.5/s versus 1.5/s consumption; net flow remains 16/s. Suggestion: Review durable Industrial/Spacer sinks or active building mix; do not raise Phantom material source rates.
- **Info Legacy metal surplus** Tin: Active production is 16.8/s versus 1.5/s consumption; net flow remains 15.3/s. Suggestion: Review durable Industrial/Spacer sinks or active building mix; do not raise Phantom material source rates.

## Validation boundary

未执行真实 Unity 编译。

未执行 Huawei P40 Pro 真机验收。
