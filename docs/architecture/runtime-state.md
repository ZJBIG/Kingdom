# Runtime-state and simulation architecture

## Evidence boundary

This document owns State, clock and transaction invariants. Follow
`.agents/skills/kingdom-economy-simulation/SKILL.md` for deterministic diagnostics
and runtime evidence gates; simulator fixtures are not Unity acceptance.

## Current ownership

- Resource, Building and Research ScriptableObjects are definitions.
- ResourceState, BuildingState, ResearchState, StoryProgressState and GameState are mutable runtime authority.

- `BuildingState.Amount` is also the authoritative count for `SectorBuilding`.
  Sector building construction and deconstruction are gated by sector
  occupation and `MaxAmount`, but do not read, submit, or refund homeland
  territory `spaceCost`.
- ResourceManager, BuildingManager and ResearchManager own State collections.
- SimulationManager is the only gameplay clock.
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

Save only non-derivable values using stable definition IDs and parseable ExpantaNum `ToString()` values, never localized display labels. Rates, efficiency, UI state caches and indexes are rebuilt after load. Save format v9 is the only accepted format. `General`, `Resources`, `Buildings`, `Researches`, `Workshop`, `Sectors`, `Tutorial` and `Story` are required top-level sections; if any is missing or corrupt, or any section is semantically invalid, reset all Manager State and start a new game without retaining partially applied data. `UltraProject` is an optional v9 extension: if that section is absent, initialize a fresh locked Ultra project while restoring the rest of the valid save; an explicitly null or malformed present section invalidates the save. Older and future save versions remain unsupported.

`KingdomSave.json` is the only managed save. Writes use a temporary file followed by atomic replacement; there is no backup, recovery candidate, migration or retired-ID mapping. Existing `.bak` files are ignored. Story completion is serialized in the required `Story.CompletedChapterIds` segment.
