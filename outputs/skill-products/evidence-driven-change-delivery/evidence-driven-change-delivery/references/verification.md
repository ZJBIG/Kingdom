# Select checks that support the claim

Use the repository's documented commands after inspecting their current configuration and side effects. Prefer available tools and existing tests. These are examples to adapt, not mandatory commands:

| Change | Useful evidence | What it does not establish |
|---|---|---|
| Pure function or parser | Focused input/output checks, including the failure case | Integration with every caller |
| Service or CLI behavior | Reproduction through the entry point and relevant tests | Production deployment or real account behavior |
| UI interaction | Component checks plus actual interaction in an available runtime | Interaction from markup inspection alone |
| Serialized data or game assets | References, identifiers, load path, relevant engine checks | Compatibility from successful compilation alone |
| Documentation or skill instructions | References, consistency, realistic independent use where feasible | Client discovery or behavior from file existence alone |

## A result must identify its input

For nontrivial verification, retain enough detail to reproduce the claim: command, relevant source state, environment, exit status, and the meaningful output. Include executed/failed/skipped counts when the runner supplies them. Do not invent counts or require a new logging framework.

Check that result files belong to this run and input. A process may write passing XML and later hang; report both observations. Zero executed tests or a skipped relevant case does not establish the behavior. A compiler can succeed while an interaction remains broken.

If the runtime is unavailable, perform useful static checks and state exactly which execution is missing. Offer a concrete command or interaction for later verification without claiming it ran. For Unity, obtain the version from ProjectSettings/ProjectVersion.txt when present; preserve .meta/GUID references, inspect Scene/Prefab consumers, and separate compilation, EditMode, PlayMode, and interaction evidence. Do not launch or close someone else's Editor as an incidental verification step.

## Failure handling

Keep the raw failure relevant to the claim. A failing check seen after an edit is not automatically caused by that edit. Compare a narrow baseline in a safe workspace if needed and feasible; otherwise report the attribution as unresolved.

When a tool is absent, authentication is missing, or a command would alter real data, continue checks that do not depend on that condition. Name the blocker and the next specific action. Never convert an unavailable check into a passing check, hide a relevant failure, or change the test simply to get green output.
