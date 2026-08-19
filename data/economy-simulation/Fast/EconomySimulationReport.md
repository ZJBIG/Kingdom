# Economy Simulation Report - Fast

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
- Productivity-blocked building decision time: total 37160 seconds; longest continuous 20430 seconds.
- Final population growth: x2.4, 1126.123/s; productivity utilization: 136.0%; territory: 2723/2725.

## Animal Age

到达时间: 0 分钟
完成研究: 11

## Neolithic Age

到达时间: 79.12 分钟
完成研究: 23

## Medieval Age

到达时间: 399.73 分钟
完成研究: 3

## Industrial Age

到达时间: 679.75 分钟
完成研究: 33

## Spacer Age

到达时间: 不可达
完成研究: 0

原始时代最长无研究目标: 0 分钟；新石器时代: 0 分钟。

## Bottlenecks

- Aluminum: single producer
- CopperWire: single producer
- Electronics: single producer
- Engine: single producer
- Glass: single producer
- Machinery: single producer

## Warnings

- **Info Legacy metal surplus** Bronze: Active production is 20.6/s versus 2/s consumption; net flow remains 18.6/s. Suggestion: Review durable Industrial/Spacer sinks or active building mix; do not raise Phantom material source rates.
- **Info Legacy metal surplus** Tin: Active production is 20/s versus 2/s consumption; net flow remains 18/s. Suggestion: Review durable Industrial/Spacer sinks or active building mix; do not raise Phantom material source rates.
- **High Building wait exceeds ten minutes** IndustrialHabitationComplex: First-copy Concrete input takes 11.67 minutes at active production. Suggestion: Adjust that resource cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** IndustrialStoneworks: First-copy StoneBrick input takes 12.5 minutes at active production. Suggestion: Adjust that resource cost or unlock a producer earlier.

## Validation boundary

未执行真实 Unity 编译。

未执行 Huawei P40 Pro 真机验收。
