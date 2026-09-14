# Test scope

These instructions apply to `Assets/Tests/**` and supplement root `AGENTS.md`.
Start through the root entry and project-dev router; inherit global reflection,
strongly typed API/DTO, numeric assertion and evidence constraints without redefining them.

- Use EditMode tests for pure rules, State, transactions, simulation and save data.
- Use PlayMode tests for Unity lifecycle, page visibility/input gating, Scene wiring, music independence and visual refresh; follow the current UI lifecycle rather than requiring all pages to deactivate.
- Each regression test must fail against the known defect.
- Prefer `SimulationManager.ManualTick` over wall-clock waits.
- Do not depend on dictionary order, localized text or hardcoded transform positions. Layout tests should measure geometry and assert valid bounds, relative placement and non-overlap.
- Destroy created GameObjects and temporary assets in teardown.
- Separate correctness and performance tests.
- Record Unity result XML and log paths.
- Check project case counts and actual coverage under `docs/testing/acceptance-checklist.md` before reporting acceptance.
