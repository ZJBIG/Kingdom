# Economy Simulation Report - Conservative

Offline runtime-aligned simulation; no Unity runtime was launched.
Strict snapshot: 40 resources, 69 buildings, 106 research, 58 Workshop upgrades.

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
- Productivity-blocked building decision time: total 0 seconds; longest continuous 0 seconds.
- Final population growth: x1.6, 0/s; productivity utilization: 15.9%; territory: 206/2325.

## Animal Age

到达时间: 0 分钟
完成研究: 11

## Neolithic Age

到达时间: 84.62 分钟
完成研究: 23

## Medieval Age

到达时间: 647.17 分钟
完成研究: 3

## Industrial Age

到达时间: 771.88 分钟
完成研究: 4

原始时代最长无研究目标: 4 分钟；新石器时代: 4 分钟。

## Bottlenecks

None

## Warnings

- **High Building wait exceeds ten minutes** DeepSpaceRelay: Construction inputs 15560 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** OrbitalCarbonizationComplex: Construction inputs 27710 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** OrbitalCryogenicPropellantArray: Construction inputs 77000 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** OrbitalForestryHarvestingArray: Construction inputs 21870 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** OrbitalHabitatMegastructure: Construction inputs 31300 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** OrbitalLogisticsHub: Construction inputs 18200 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** OrbitalResourceExtractionArray: Construction inputs 61900 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** OrbitalStation: Construction inputs 23520 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** OrbitalTextileFabricationArray: Construction inputs 38200 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** OrbitalVacuumMetallurgyArray: Construction inputs 78300 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** Shipyard: Construction inputs 28800 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.

## Validation boundary

未执行真实 Unity 编译。

未执行 Huawei P40 Pro 真机验收。
