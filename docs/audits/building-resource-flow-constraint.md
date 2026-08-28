# Building resource-flow constraint

## Rule

Every `Building`, including `SectorBuilding`, must express a resource as either
a positive production rate or a positive consumption rate. A definition must
not serialize the same resource in both lists. Authors must merge opposing
rates into one net flow before saving the asset.

## Enforcement

`Building.ValidateResourceFlowDefinitions()` examines the raw serialized
generation and consumption lists, before `Building` creates its runtime net
flow cache. `BuildingManager.ValidateBuildingChains()` invokes that validation
for every loaded building. `GameBootstrap` invokes the same definition
validation before runtime state is initialized.

The check intentionally compares resource identity and rate direction only; it
does not assert any resource amount. Balance changes therefore remain data
changes, while malformed opposing flows remain definition errors.

The raw-asset audit is reproducible with:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\codex\building-resource-flow-check.ps1
```

The building-content regression tests follow the same rule: they assert
resource identity, direction, and required relationships instead of fixed
resource-rate constants.

This boundary also applies to resource-facing acceptance assertions: changing
content balance must not require rewriting tests that merely prove a resource
exists, is positive, or changes in the expected direction. Exact values are
reserved for isolated arithmetic tests with synthetic inputs.

## Coverage

- `AllBuildings_DoNotSerializeTheSameResourceAsProductionAndConsumption` runs
  against `DataBase<Building>.All`, so subclasses are included.
- `BuildingDefinition_RejectsUnmergedOpposingResourceFlows` proves that an
  invalid raw definition is rejected even though runtime flow access would
  otherwise net the two sides.

## Current asset audit

The raw-YAML audit covers all 66 building assets. It found and normalized the
three opposing flows in `OrbitalResourceExtractionArray` into their equivalent
net production rates. The post-change audit reports zero building assets with
an overlapping production and consumption resource.

## Current validation evidence

The current static audit reports `BuildingAssets=66` and
`OpposingRawResourceFlows=0`. The latest available repository test report is
`TestResults/Latest-Test-Errors.txt` from 2026-08-28 00:20:51; it records 32
tests, 31 passed and 1 failed in the research-tree overflow assertion. It is
retained as historical runtime evidence and is not silently converted into a
pass claim because the subsequent source fixes have not yet been exercised by
a newer Unity Test Runner log.
