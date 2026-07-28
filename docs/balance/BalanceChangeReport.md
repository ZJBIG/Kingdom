# Kingdom Balance Change Report

## Scope

This batch adds a standalone, runtime-aligned simulator and applies one dependency correction. It does not claim Industrial balance acceptance: the current content still has an unreachable Medieval transition.

## Simulator implementation

- `tools/EconomySimulator/` now reads YAML definitions through `.meta` GUID resolution.
- Internal time is a fixed `0.1` second tick; reports aggregate at one-minute boundaries.
- Resource inventory clamps at zero; production and consumption are scaled by resource, food, power and logistics satisfaction.
- Research costs are paid incrementally and research speed uses `ResearchPower` plus the runtime era effect.
- Repeated buildings use geometric cost growth. Normal, Fast and Conservative routes are exported separately.
- Outputs include `SimulationTimeline.csv`, `ResourceFlowByEra.csv`, `ResearchCompletionTimeline.csv`, `BuildingConstructionTimeline.csv` and `BalanceWarnings.csv`.

## Data change

| File | Field | Before | After | Reason |
|---|---|---|---|---|
| `Kingdom/Assets/Resources/Datas/Building/Animal/Quarry.asset` | `requiredResearch` | `Quarry`, `Mining` | `Quarry` | Removes the research/building cycle: `Mining -> Quarry building -> Mining`. `Mining` remains in the research chain and the core quarry is available after its own research. |

No ID, GUID, coordinate, label, description, icon, save field or runtime script was changed by this batch.

### Added Animal research

Added `Arithmetic`, `Geometry`, `Surveying`, `Stargazing`, `NaturalPhilosophy`, `OrePurification` and `RecordKeeping`. They use existing EffectTypes only and target specific buildings or the global research multiplier. Their effects are present in the simulator timeline and do not advance TechLevel.

## Evidence

Normal route reaches Neolithic at about 152 minutes. With a 24-hour horizon it still does not finish the Medieval transition. With a 48-hour horizon it completes `SmithingRevolution` at about 1385 minutes, enters Medieval, and reaches Industrial at about 1525 minutes. This proves the chain is not permanently unreachable, but the current research/economy pacing is far slower than the intended 5–9 hour Medieval and 10–15 hour Industrial targets.

The current result is therefore classified as a pacing failure, not a time-limit-only failure and not a permanent deadlock. The next balancing pass should reduce research/building waiting time and improve ResearchPower availability before changing core resource production.

The snapshots are in:

- `data/balance/BeforeBalance/`
- `data/balance/AfterResourceFlow/`
- `data/balance/AfterResearchPacing/`
- `data/balance/FinalBalance/`

`AfterResearchExpansion` and `FinalBalance` contain the current post-expansion simulation. Resource-flow and research-cost tuning remains intentionally unapplied; the 48-hour result now provides the evidence needed for that next pass.

## Validation boundary

Standalone simulator: compiled successfully with `dotnet build` and executed successfully.

未执行真实 Unity 编译。

未执行 Huawei P40 Pro 真机验收。
