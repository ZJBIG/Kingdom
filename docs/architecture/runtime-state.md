# Runtime-state and simulation architecture

## Evidence boundary

This document owns State, clock and transaction invariants. Follow
`.agents/skills/kingdom-economy-simulation/SKILL.md` for deterministic diagnostics
and runtime evidence gates; simulator fixtures are not Unity acceptance.

## Current ownership

- ResourceDef, BuildingDef and ResearchDef ScriptableObjects are definitions.
- ResourceState, BuildingState, ResearchState, StoryProgressState and GameState are mutable runtime authority.

- `BuildingState.Amount` is also the authoritative count for `SectorBuildingDef`.
  Sector building construction is gated by sector occupation and `MaxAmount`,
  without submitting homeland territory `spaceCost`. Sector facilities cannot
  be deconstructed. `BuildingManager.SetSectorBuildingEnabled` changes their
  `BuildingState.IsEnabled`; disabled facilities retain ownership, physical
  capacities and permanent engineering stages, but stop ongoing inputs, outputs
  and productivity supply/demand. Save entries persist this state as `Disabled`;
  absent flags default to enabled. Re-enabling does not charge construction costs.
- ResourceManager, BuildingManager and ResearchManager own State collections.
- SimulationManager is the only gameplay clock.
- SectorManager owns optional planetary exploration in SectorState: separate progress,
  active and manual-pause flags. Occupation opens ordinary facilities; exploration
  completion additionally gates configured special buildings. Exploration pays only
  the affordable advancing duration in one inventory/state transaction, uses stable
  sector order and retains progress while paused or waiting for supplies.
- `KingdomUIRoot` partials are presentation only;
  lifecycle ownership is documented in `docs/architecture/ui-boundaries.md`.
- SaveManager captures non-derivable State using stable definition IDs.
- StoryProgressState stores only the completed chapter ID prefix and a version;
  completion is permanent history and never grants economic effects.

## Deterministic tick

Use explicit `deltaSeconds` in gameplay integration; do not derive elapsed gameplay time from UI activity.
`SimulationManager.ManualTick` owns the implemented order. The ordered navigation
summary lives in `docs/repository-map.md`; read the method before changing it.
`BuildingManager.PrepareTickResourceSatisfaction` already performs bounded
satisfaction/efficiency convergence and refreshes ResearchPower before the
population and resource ticks. Do not restore the obsolete Kingdom3 target list
or add a second building scan from that list.

Manual build/deconstruct commands use synchronous Manager transactions.
Auto-build and command queues are not current runtime guarantees. Introducing
such behavior requires separate authorization and a documented stable order.

## Resource satisfaction invariant

For each resource and tick:

```text
available = current inventory + potential production * deltaSeconds
demand = potential consumption * deltaSeconds
satisfaction = demand <= 0 ? 1 : Clamp01(available / demand)
```

A building's resource-limited efficiency is the minimum satisfaction of its required input resources. Actual aggregate consumption must not exceed available amount.

Do not simplify this to “inventory > 0 means full efficiency”.

## Food/calendar invariant

Food integrates every simulation tick. Calendar days advance from a separate accumulator. Building changes affect only future elapsed time.

## Transaction invariant

Build, deconstruct and research cost payment:

1. normalize/clamp amount;
2. calculate and validate all requirements;
3. mutate all affected State only after all checks pass.

No partial mutation on failure.

## Save invariant

Save only non-derivable values using stable definition IDs and parseable ExpantaNum `ToString()` values, never localized display labels. Rates, efficiency, UI state caches and indexes are rebuilt after load. The current clean-break format is v1. `General`, `Resources`, `Buildings`, `Researches`, `Workshop`, `Sectors`, `Tutorial`, `Story`, `UltraProject` and `Relic` are required sections. Missing, corrupt or semantically invalid sections reject the whole save and initialize a clean game without retaining partially applied data. No optional historical extensions or resource-ID aliases are supported. Persisted economic numeric strings are required and finite; missing amounts, progress, efficiency or General numeric values reject the save instead of inheriting runtime values or defaulting to zero/one.

`KingdomSave-v1.json` is the only managed save. The historical `KingdomSave.json` is ignored, including its old v1 format; version numbers alone do not identify this new save generation. Writes use a temporary file followed by atomic replacement; there is no backup, recovery candidate, migration or retired-ID mapping. Existing `.bak` files are ignored. Story completion is serialized in the required `Story.CompletedChapterIds` segment.

Each serialized sector includes required `InteriorExplorationProgress` plus
`InteriorExplorationActive` and `InteriorExplorationPaused`. Restore rejects missing
or non-finite progress, out-of-range progress, active-and-paused combinations,
unfinished occupation/research prerequisites and exploration-gated construction
without completed exploration. Purchased workshop stages retain that condition
even after demolition of their body; reconstruction restores the recorded stages.
