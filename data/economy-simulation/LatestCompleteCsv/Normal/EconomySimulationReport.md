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
- Final population growth: x1.2, 0/min; productivity utilization: 25.8%; territory: 128/500.

## Animal Age

到达时间: 0 分钟
完成研究: 7

## Neolithic Age

到达时间: 不可达
完成研究: 0

## Medieval Age

到达时间: 不可达
完成研究: 0

## Industrial Age

到达时间: 不可达
完成研究: 0

原始时代最长无研究目标: 1373 分钟；新石器时代: 0 分钟。

## Bottlenecks

None

## Warnings

- **High Building wait exceeds ten minutes** Arsenal: Construction inputs 1950 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** Castle: Construction inputs 4700 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** CentralPowerStation: Construction inputs 2400 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** LaunchCenter: Construction inputs 2800 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** OrbitalStation: Construction inputs 3650 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** RoyalWorkshop: Construction inputs 2100 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Building wait exceeds ten minutes** Shipyard: Construction inputs 5300 exceed ten minutes of active aggregate production. Suggestion: Adjust first-copy cost or unlock a producer earlier.
- **High Pacing failure** Neolithic: The run did not reach Neolithic within 24 hours. Suggestion: Repair the Animal main line.
- **High Pacing failure** Medieval: The run did not reach Medieval within 24 hours. Suggestion: Repair ResearchPower and the Neolithic main line.
- **High Progress drought** Animal: Longest interval without an active research target was 1373 minutes. Suggestion: Move an affordable action or ResearchPower source earlier.

## Validation boundary

未执行真实 Unity 编译。

未执行 Huawei P40 Pro 真机验收。
