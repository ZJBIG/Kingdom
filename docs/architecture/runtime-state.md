# Runtime-state and simulation architecture

## Current ownership

- Resource, Building and Research ScriptableObjects are definitions.
- ResourceState, BuildingState, ResearchState and GameState are mutable runtime authority.
- ResourceManager, BuildingManager and ResearchManager own State collections.
- SimulationManager is the only gameplay clock.
- Viewer/Displayer code is UI only.
- SaveManager captures non-derivable State using stable definition IDs.

## Target deterministic tick

The current Kingdom3 tick foundation must be completed with a two-phase resource calculation:

1. integrate food/time-independent global values;
2. collect potential resource generation/consumption for the tick;
3. calculate per-resource satisfaction using inventory plus potential generation;
4. calculate each building's actual efficiency;
5. aggregate actual resource generation/consumption;
6. commit inventory changes without going below zero;
7. advance research;
8. advance auto-build;
9. mark State versions.

Commands such as manual build/deconstruct may execute synchronously through Manager transactions. If command queuing is introduced later, it must have a documented stable order.

## Resource satisfaction invariant

For each resource and tick:

```text
available = current inventory + potential production * deltaSeconds
demand = potential consumption * deltaSeconds
satisfaction = demand <= 0 ? 1 : Clamp01(available / demand)
```

A building's resource-limited efficiency is the minimum satisfaction of its required input resources. Actual aggregate consumption must not exceed available amount.

The existing rule “inventory > 0 means full efficiency” is not acceptable.

## Food/calendar invariant

Food integrates every simulation tick. Calendar days advance from a separate accumulator. Building changes affect only future elapsed time.

## Transaction invariant

Build, deconstruct and research cost payment:

1. normalize/clamp amount;
2. calculate and validate all requirements;
3. mutate all affected State only after all checks pass.

No partial mutation on failure.

## Save invariant

Save only non-derivable values. Rates, efficiency, UI state caches and indexes are rebuilt after load. Candidate save loading must be transactional so a failed main save can fall back to backup without leaving partial State.
