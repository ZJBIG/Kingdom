# Economy Simulation Report - Conservative

Offline runtime-aligned simulation; no Unity runtime was launched.
Strict snapshot: 40 resources, 70 buildings, 106 research, 58 Workshop upgrades.

## Rules

- Adaptive ticks: one second in Animal/Neolithic, ten seconds in Medieval, sixty seconds in Industrial, one-hundred-twenty seconds in Spacer, one-hundred-eighty seconds in Ultra, and three-hundred seconds in Archotech; snapshots remain ten-minute aligned.
- Each route runs for a 30-day observation horizon so late-era construction, workshops and supply chains are visible; reports sample every 10 minutes.
- Research resource costs are paid atomically before progress begins, matching ResearchManager.
- Workshop unlocks, prerequisite chains, costs and effects are included.
- Strategy decisions are emitted to DecisionTrace.csv with deduplicated reasons.
- Research speed uses ResearchPower and runtime era effects.
- Buildings use geometric cost growth and commit immediately after payment.
- Food starts at +5/s; population consumes 0.8 food/s per person and grows toward housing capacity.
- Population growth uses a logistic occupancy factor; over-capacity departure accelerates with relative excess and remains productivity-gated.
- Total productivity equals population x2 plus fixed research and owned-building grants; construction checks pre-build available productivity.
- Productivity-blocked building decision time: total 0 seconds; longest continuous 0 seconds.
- Final population growth: x1.6, 0/s; productivity utilization: 45.2%; territory: 296/2325.

## Animal Age

到达时间: 0 分钟
完成研究: 11

## Neolithic Age

到达时间: 79.07 分钟
完成研究: 23

## Medieval Age

到达时间: 475.9 分钟
完成研究: 3

## Industrial Age

到达时间: 591.92 分钟
完成研究: 10

原始时代最长无研究目标: 1 分钟；新石器时代: 1 分钟。

## Bottlenecks

None

## Warnings

- **High Building wait exceeds ten minutes** OrbitalAgroecologyArray: Construction inputs 47550 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** OrbitalCarbonizationComplex: Construction inputs 27710 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** OrbitalCryogenicPropellantArray: Construction inputs 46200 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** OrbitalForestryHarvestingArray: Construction inputs 21870 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** OrbitalHabitatMegastructure: Construction inputs 31300 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** OrbitalLogisticsHub: Construction inputs 18200 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** OrbitalResourceExtractionArray: Construction inputs 37140 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** OrbitalStation: Construction inputs 23520 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** OrbitalTextileFabricationArray: Construction inputs 38200 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** OrbitalVacuumMetallurgyArray: Construction inputs 46980 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** Shipyard: Construction inputs 28800 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.

## Validation boundary

未执行真实 Unity 编译。

未执行 Huawei P40 Pro 真机验收。
