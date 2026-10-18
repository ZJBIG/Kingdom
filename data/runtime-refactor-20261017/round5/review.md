# Runtime refactor round5 — 2026-10-18

## Scope and changes
Preserve prior uncommitted work. baseline.json, byte backups and pre-existing-changes.patch distinguish this round from HEAD; round5-only.patch is the isolated change.
- PopulationState.RestorePopulationChangeProgress now restores validated progress even at full housing. Starvation departure progress can accumulate at full capacity and is non-derivable saved state.
- RestoreCapacityExact no longer erases full-housing progress and preserves the legal saved endpoint 1. Other values retain the previous normalization. Building transaction rollback uses this shared path; global NormalizeProgress is unchanged.
- AdvancePopulation validates all parameters then returns for zero seconds. This prevents derived refresh from clearing full/over-capacity progress or spending restored progress=1. Positive-time mode changes still clear incompatible progress; normal full-capacity growth remains stopped.
- Runtime code: 319 -> 316 lines. No content/economy values, save fields, stable IDs, assets or GUIDs changed. No new dependencies or reflection.
- Existing offline settlement test now uses the existing explicit-time Editor API for its second call, with the saved timestamp as both endpoints. All assertions remain. Its prior public wall-clock call could legitimately cross a second and settle one extra second.

## Reproduction and intermediate evidence
- before.xml: old runtime fails all 3 restoration cases (direct restore, capacity rollback, full JSON save roundtrip), progress ~0.5 becoming zero.
- verified.xml: restoration-only fix passes 235/235 related EditMode cases.
- zero-before.xml: restoration-only runtime fails all 3 zero-second cases (full capacity, over capacity, below capacity with saved progress=1).
- final.xml: intermediate zero-second fix passes 243/243.
- one-before.xml: exact rollback still fails the legal progress=1 endpoint, becoming zero.
- complete.xml: final production code passes all 7 new cases; 243/244 overall because the existing wall-clock offline test crossed a second. complete.log records the extra one-second settlement; this is not final acceptance evidence.

## Final validation
- accepted.xml / accepted.log: real Unity compile + EditMode passes 244/244, 2.1331908 seconds. Scope: KingdomLogicTests, EconomyIntegrationRegressionTests, SimulationDeterminismTests, OfflineSimulationBoundaryTests, WorldEventTests, SaveManagerUltraProjectEraValidationTests, ResearchPaymentSaveTests, InvestmentChoiceTests.
- accepted-status.txt: runner exit0, normal Unity process exit. Earlier batches produced XML then hung during shutdown; their wrappers stopped only the recorded task PID at 60 seconds (runner1). Do not conflate intermediate shutdown timeouts with the final runner result.
- compile-accepted.log: developer assembly build exit0, 0 errors / 6 existing TextureImporter warnings. This compiles PlayMode sources but does not run PlayMode.
- Targeted git diff --check passed; source-inputs.json records final hashes. Independent read-only review identified the progress=1 endpoint, now covered and fixed.

## Boundaries and next action
No PlayMode, full-suite run, whole-game profiling, real player-save operation, commit or push. No FPS or global performance claim. Keep the continuous goal active; next review population/food accounting at tick boundaries with a concrete failing case before further edits.
