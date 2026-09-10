# New economy simulator

This directory contains the replacement simulator foundation and its maintenance validation layer. It does not make pacing or Unity runtime acceptance claims.

## Snapshot contract

The current strongly typed snapshot format is version `3`. The Unity editor
exporter resolves each definition through its Unity `.meta` GUID, orders stable
IDs with ordinal comparison, and writes every economy value as an
`ExpantaNum` string produced by `ToString` after a successful `TryParse`.
Effects carry independent `BuildingId` and `ResourceId` fields because a
runtime effect can target both. The loader rejects unsupported versions,
missing or duplicate IDs/GUIDs, unresolved references, duplicate resource
pairs, invalid numbers, and incomplete initial/save/runtime state coverage.

## Validation coverage

`CoreValidationAdapter` uses only public, strongly typed APIs from the current core. It does not use reflection.

The executable validation suite checks:

- deterministic traces and terminal state for the same seed;
- ordinary resources have no capacity and can grow past the Food-cap probe;
- Food has one positive capacity and clamps attempted overfill;
- Research payment is atomic: an unaffordable multi-resource payment changes nothing, while an affordable payment deducts every cost before activation;
- save/restore preserves tick, elapsed time, resources, Food, Food capacity, and Research state.

The deterministic, ordinary-resource, and Food checks run through `SimulationCore`, `SimulationState`, `ISimulationRules`, and `SimulationEventLog`. Research and save/restore are validation adapters around the current public state API because the core does not expose dedicated Research or persistence services.

## Run

The project intentionally remains a library. `Program.cs` is a .NET 10 file-based entry point, so no project-file change is required.

```powershell
dotnet run --project .\tools\NewEconomySimulator\NewEconomySimulator.csproj
dotnet run --project .\tools\NewEconomySimulator\NewEconomySimulator.csproj -- --json
```

The process exits with code `0` only when all five checks execute and pass. Markdown is the default output; pass `--json` for machine-readable output.

## Boundaries

- Historical standalone simulator files and pacing reports were retired. New
  parity facts may be persisted under `data/economy-parity/`; this tool does not
  make pacing or balance acceptance claims.
- These self-tests are maintenance evidence only; they do not replace Unity compilation, EditMode/PlayMode tests, Console inspection, or player playtests.
- Food is the only capped stockpile.

## Era step schedule

`SimulationCore.FromSnapshot` defaults to Unity-parity fixed steps: `0.1 s`
realtime and at most `60 s` per offline chunk. Era-adaptive stepping is explicit
through `SimulationCore.FromSnapshotWithEraSteps`; those runs choose the step
from the era at the start of each tick.
An era completed during research changes the step on the following tick.
Era-adaptive output is deterministic long-horizon diagnostic evidence, not
Unity parity or pacing acceptance evidence.

| Era | Realtime step | Offline step |
|---|---:|---:|
| Animal | 0.1 s | 1 s |
| StoneAge | 0.5 s | 2 s |
| Medieval | 1 s | 5 s |
| Industrial | 5 s | 10 s |
| Spacer | 15 s | 20 s |
| Ultra | 30 s | 30 s |
| Archotech | 60 s | 60 s |

Early-era steps preserve short research, Food and production boundaries. Later
steps reduce long-horizon work while remaining no larger than Unity's 60-second
offline settlement step. Tick events record era, requested seconds and
effective seconds for first-difference diagnosis.
