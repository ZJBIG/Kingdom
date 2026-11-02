# A compact, reviewable delivery

Use the portions that help the reader. A small change does not need a long report.

- **Outcome:** What now happens, the trigger or input, and why the change was needed.
- **Scope:** Relevant files and behavior; mention an important compatibility decision when applicable.
- **Evidence:** Checks actually executed, their result, and the claim each supports. Identify stale or unavailable evidence explicitly.
- **Remaining work:** A real failure, blocker, or unverified behavior, with the next concrete action. Omit hypothetical risks unrelated to the change.

Example of an honest partial delivery:

> Updated the selection handler to clear its previous subscription before rebinding. The focused lifecycle test passes. I inspected the scene reference, but did not run the game engine; drag and click behavior still needs an interaction check in the scene.

For a review-only task, report actionable findings with source locations, a failure path, and the relevant uncertainty. Do not call it a fix or create a handoff file unless requested.

For longer work, update an existing same-topic handoff if the repository uses one. Record the relevant input state, changed behavior, verification results, and next action. Keep historical results dated; do not overwrite history to make old evidence appear current. Do not include credentials, personal files, real saves, or unrelated logs.
