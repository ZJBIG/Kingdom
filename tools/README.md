# Kingdom tools

This directory contains non-runtime tools. Normal gameplay development usually does not require these files.

- `NewEconomySimulator/`: deterministic, Unity-aligned economy parity harness.
- `content-dependency/`: offline Resource/Building/Research closure analyzer.

Unity-integrated editor scripts remain under `Assets/Editor` so Unity can discover their menus. Project-specific Unity validation scripts remain under `tools/codex` because the repository guidance invokes those paths directly.
The repository also includes `content-dependency/ContentDependencyAnalyzer.ps1` for static content dependency checks. New simulator validation is diagnostic only; Unity runtime and PlayMode evidence remain authoritative. Current parity reports, when persisted, belong under `data/economy-parity/`.
