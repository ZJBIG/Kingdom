# Economy Simulation Report - Conservative

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
- Productivity-blocked building decision time: total 322380 seconds; longest continuous 287820 seconds.
- Final population growth: x2.65, 0/s; productivity utilization: 98.8%; territory: 2724/2725.

## Animal Age

到达时间: 0 分钟
完成研究: 11

## Neolithic Age

到达时间: 64.58 分钟
完成研究: 23

## Medieval Age

到达时间: 442.82 分钟
完成研究: 3

## Industrial Age

到达时间: 1131 分钟
完成研究: 44

## Spacer Age

到达时间: 8941.17 分钟
完成研究: 8

原始时代最长无研究目标: 1 分钟；新石器时代: 2 分钟。

## Bottlenecks

- Aluminum: single producer
- CopperWire: single producer
- Electronics: single producer
- Engine: single producer
- Glass: single producer
- Machinery: single producer

## Warnings

- **Info Legacy metal surplus** Bronze: Active production is 70.6/s versus 1/s consumption; net flow remains 69.6/s. Suggestion: Review durable Industrial/Spacer sinks or active building mix; do not raise Phantom material source rates.
- **Info Legacy metal surplus** Tin: Active production is 68/s versus 1/s consumption; net flow remains 67/s. Suggestion: Review durable Industrial/Spacer sinks or active building mix; do not raise Phantom material source rates.
- **High Building wait exceeds ten minutes** IndustrialHabitationComplex: First-copy Concrete input takes 11.67 minutes at active production. Suggestion: Adjust that resource cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** IntegratedPetrochemicalComplex: First-copy Electronics input takes 11.9 minutes at active production. Suggestion: Adjust that resource cost or unlock a producer earlier.
- **Info Building wait exceeds ten minutes** PhantomMaterialsFabricator: First-copy TitaniumAlloy input takes 22.22 minutes at active production. Suggestion: Treat this as an intentional late-era material gate; do not raise its source rate without a progression review.

## Validation boundary

未执行真实 Unity 编译。

未执行 Huawei P40 Pro 真机验收。
