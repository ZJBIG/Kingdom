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

Start through root `AGENTS.md` and the project-dev router. The canonical command,
prerequisites and side effects are documented in
`.agents/skills/kingdom-project-dev/references/validation.md`; do not keep another
command recipe here.

The current csproj is a `net9.0` executable, not a library or a .NET 10 file-based
app. `Program.cs` calls `ValidationSuite.RunCore()` and exits with `0` only when
`report.Passed` is true. Output is Markdown by default; `--json` selects JSON and
`--csv` takes precedence when both flags are present. Check current suite coverage
rather than treating the historical five-check list as the total test count.

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
