# Economy Simulation Report - Conservative

> **STATUS: FROZEN DIAGNOSTIC ONLY.** Do not use this output as a current gameplay, pacing, balance, progression, or Unity acceptance report. It is not authoritative compared with real Unity runtime evidence or player playtests.

Offline runtime-aligned simulation; no Unity runtime was launched.
Strict snapshot: 40 resources, 69 buildings, 121 research, 73 Workshop upgrades.

## Rules

- Adaptive ticks: one second in Animal/StoneAge, ten seconds in Medieval, ten minutes in Industrial, and thirty minutes in Spacer/Ultra/Archotech; rates remain per-second and snapshots remain ten-minute aligned.
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
- Productivity-blocked building decision time: total 0 seconds; longest continuous 0 seconds.
- Final population growth: x2, 131.804/s; productivity utilization: 85.1%; territory: 462/2325.

## Animal Age

到达时间: 0 分钟
完成研究: 11

## Stone Age

到达时间: 64.58 分钟
完成研究: 23

## Medieval Age

到达时间: 426.32 分钟
完成研究: 3

## Industrial Age

到达时间: 1142.83 分钟
完成研究: 21

## Spacer Age

到达时间: 不可达
完成研究: 0

原始时代最长无研究目标: 1 分钟；石器时代: 2 分钟。

## Bottlenecks

None

## Warnings

None

## Validation boundary

未执行真实 Unity 编译。
