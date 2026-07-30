# Economy Simulation Report - Conservative

Offline runtime-aligned simulation; no Unity runtime was launched.

## Rules

- Fixed one-second ticks with integer minute snapshots.
- Research is selected first; resource costs are paid progressively before progress begins.
- Research speed uses ResearchPower and runtime era effects.
- Buildings use geometric cost growth and commit immediately after payment.
- Food starts at +5/s; population consumes 1 food/s per person and grows toward housing capacity.
- Population growth is base 1/min x completed-research multiplier x food satisfaction; departure remains 1/min.
- Total productivity equals population plus fixed research and owned-building grants; construction checks pre-build available productivity.
- Productivity-blocked building decision time: total 0 seconds; longest continuous 0 seconds.
- Final population growth: x2.415, 2.415/min; productivity utilization: 21.2%; territory: 360/2375.

## Animal Age

到达时间: 0 分钟
完成研究: 1

## Neolithic Age

到达时间: 46.72 分钟
完成研究: 1

## Medieval Age

到达时间: 132.82 分钟
完成研究: 1

## Industrial Age

到达时间: 191.97 分钟
完成研究: 99

原始时代最长无研究目标: 0 分钟；新石器时代: 3 分钟。

## Bottlenecks

None

## Warnings

None

## Validation boundary

未执行真实 Unity 编译。

未执行 Huawei P40 Pro 真机验收。
