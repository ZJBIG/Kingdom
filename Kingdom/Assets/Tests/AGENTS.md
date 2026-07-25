# Test scope

These instructions apply to `Assets/Tests/**`.

- Use EditMode tests for pure rules, State, transactions, simulation and save data.
- Use PlayMode tests for Unity lifecycle, Viewer enable/disable, Scene wiring, music independence and visual refresh.
- Each regression test must fail against the known defect.
- Prefer `SimulationManager.ManualTick` over wall-clock waits.
- Do not depend on dictionary order, localized text or transform positions.
- Destroy created GameObjects and temporary assets in teardown.
- Separate correctness and performance tests.
- Record Unity result XML and log paths.
- A successful PlayMode runner with zero project test cases is not acceptance evidence.
