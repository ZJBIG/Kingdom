# Kingdom repository map

The current Unity project is rooted at this directory and uses
`ProjectSettings/ProjectVersion.txt` as the authoritative editor-version
source.

- `Assets/Resources/Script/`: runtime managers, data models, UI and validation.
- `Assets/Resources/Datas/`: ScriptableObject content definitions.
- `Assets/Scenes/`: Unity scenes; `SampleScene.unity` is the primary scene.
- `Assets/Tests/`: EditMode and PlayMode tests.
- `docs/`: current architecture, balance, content, testing and UI guidance.
- `tools/codex/`: project validation, closure and Unity test helpers.
- `tools/NewEconomySimulator/`: deterministic economy parity harness and validation suite.
- `data/economy-parity/`: current simulator-vs-Unity facts when reports are persisted.
- `.codex/`: prompts and recoverable Codex metadata.

For gameplay/content changes, the current repository and the canonical
economy/UI guidance under `.codex/prompts/`, `.agents/skills/` and `docs/`
are authoritative. Historical material must not be used as current evidence.
