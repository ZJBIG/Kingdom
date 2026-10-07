# Kingdom tools

Non-runtime tooling. Start with root `AGENTS.md` and the project-dev router before
selecting a command. Exact parameters, write effects, isolation and validation
choices live only in `.agents/skills/kingdom-project-dev/references/validation.md`.

- `codex/`: repository validation wrappers, content-closure entry and read-only project probe.
- `NewEconomySimulator/`: deterministic diagnostic harness. The legacy standalone
  simulator it replaced (and its historical report directory) are retired; this
  harness is frozen to deterministic regression use only — no extension, strategy
  search, scoring or auto-tuning. Its README documents implementation architecture,
  not gameplay acceptance.
- `content-dependency/`: retired and empty; `ContentDependencyAnalyzer.ps1` no longer
  exists. Content dependency analysis now lives in Unity editor code
  (`Assets/Editor/Content/ContentDependencyAnalyzer.cs`).
- `Assets/Editor/`: Unity-discovered editor menus, migration and validation code (outside this directory).

Do not assume a tool is read-only from its name. Check its current implementation;
fixture/self-tests do not prove real Unity parity or pacing acceptance.
