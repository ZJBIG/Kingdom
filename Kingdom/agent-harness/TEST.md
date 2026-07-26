# Kingdom CLI Harness Test Plan

## Unit/static checks

- `python -m pip install -e .`
- `cli-anything-kingdom --help`
- `cli-anything-kingdom --json project info`
- `cli-anything-kingdom --json assets validate`

## Real backend checks

- `cli-anything-kingdom unity compile`
- Verify the generated log contains no `error CS` or `Script Compilation Error`.

The Unity backend test is intentionally not marked as passed until the real Unity
Editor returns successfully.
