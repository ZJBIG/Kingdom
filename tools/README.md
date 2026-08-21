# Kingdom tools

This directory contains non-runtime tools. Normal gameplay development usually does not require these files.

- `EconomySimulator/`: standalone offline economy pacing simulator.
- `content-dependency/`: offline Resource/Building/Research closure analyzer.

Unity-integrated editor scripts remain under `Assets/Editor` so Unity can discover their menus. Project-specific Unity validation scripts remain under `tools/codex` because the repository guidance invokes those paths directly.
