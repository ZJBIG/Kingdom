# cli-anything-kingdom

Agent-native CLI harness for the Kingdom Unity project.

## Install

```powershell
python -m pip install -e .
cli-anything-kingdom --help
```

## Commands

```powershell
cli-anything-kingdom --json project info
cli-anything-kingdom --json assets validate
cli-anything-kingdom --json assets list resources
cli-anything-kingdom unity compile
```

The Unity command invokes the real Unity Editor in batch mode. It does not emulate
Unity or silently fall back to a Python implementation.
