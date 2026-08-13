# Economy Simulation Report - Normal

Offline runtime-aligned simulation; no Unity runtime was launched.
Strict snapshot: 40 resources, 70 buildings, 106 research, 58 Workshop upgrades.

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
- Productivity-blocked building decision time: total 58095 seconds; longest continuous 56520 seconds.
- Final population growth: x2.65, 0/s; productivity utilization: 99.9%; territory: 2540/2725.

## Animal Age

到达时间: 0 分钟
完成研究: 11

## Neolithic Age

到达时间: 75.17 分钟
完成研究: 23

## Medieval Age

到达时间: 333.4 分钟
完成研究: 3

## Industrial Age

到达时间: 506.42 分钟
完成研究: 44

## Spacer Age

到达时间: 2516.58 分钟
完成研究: 8

原始时代最长无研究目标: 0 分钟；新石器时代: 0 分钟。

## Bottlenecks

- Aluminum: single producer
- Composite: single producer
- CopperWire: single producer
- Electronics: single producer
- Engine: single producer
- Glass: single producer
- Machinery: single producer

## Warnings

- **Info Legacy metal surplus** Bronze: Active production is 11.8/s versus 0.5/s consumption; net flow remains 11.3/s. Suggestion: Review durable Industrial/Spacer sinks or active building mix; do not raise Phantom material source rates.
- **High Building wait exceeds ten minutes** IndustrialStoneworks: First-copy StoneBrick input takes 16.67 minutes at active production. Suggestion: Adjust that resource cost or unlock a producer earlier.
- **Info Building wait exceeds ten minutes** LaunchCenter: First-copy Composite input takes 18.52 minutes at active production. Suggestion: Treat this as an intentional late-era material gate; do not raise its source rate without a progression review.
- **Info Building wait exceeds ten minutes** OrbitalCarbonizationComplex: First-copy PhantomAlloy input takes 66.67 minutes at active production. Suggestion: Treat this as an intentional late-era material gate; do not raise its source rate without a progression review.
- **Info Building wait exceeds ten minutes** OrbitalForestryHarvestingArray: First-copy PhantomWeave input takes 100 minutes at active production. Suggestion: Treat this as an intentional late-era material gate; do not raise its source rate without a progression review.
- **Info Building wait exceeds ten minutes** OrbitalSolarArray: First-copy PhantomWeave input takes 100 minutes at active production. Suggestion: Treat this as an intentional late-era material gate; do not raise its source rate without a progression review.
- **Info Building wait exceeds ten minutes** OrbitalStation: First-copy PhantomAlloy input takes 111.11 minutes at active production. Suggestion: Treat this as an intentional late-era material gate; do not raise its source rate without a progression review.
- **Info Building wait exceeds ten minutes** PhantomMaterialsFabricator: First-copy Composite input takes 18.52 minutes at active production. Suggestion: Treat this as an intentional late-era material gate; do not raise its source rate without a progression review.
- **Info Building wait exceeds ten minutes** PhaseMaterialSynthesisArray: First-copy PhantomWeave input takes 100 minutes at active production. Suggestion: Treat this as an intentional late-era material gate; do not raise its source rate without a progression review.

## Validation boundary

未执行真实 Unity 编译。

未执行 Huawei P40 Pro 真机验收。
