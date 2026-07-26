import json
import re
import subprocess
from pathlib import Path

import click


def _project_root() -> Path:
    return Path(__file__).resolve().parents[3]


def _json_or_text(payload, as_json: bool):
    if as_json:
        click.echo(json.dumps(payload, ensure_ascii=False, indent=2))
    else:
        for key, value in payload.items():
            click.echo(f"{key}: {value}")


def _resource_assets(root: Path):
    return sorted((root / "Assets" / "Resources" / "Datas" / "Resource").rglob("*.asset"))


def _research_assets(root: Path):
    return sorted((root / "Assets" / "Resources" / "Datas" / "Research").rglob("*.asset"))


def _definition_record(asset: Path, kind: str):
    text = asset.read_text(encoding="utf-8", errors="replace")
    identifier = re.search(r"^\s*id:\s*(\S+)", text, re.MULTILINE)
    label = re.search(r"^\s*Label:\s*(.+)$", text, re.MULTILINE)
    meta = asset.with_name(asset.name + ".meta")
    guid_match = re.search(r"^guid:\s*([0-9a-f]{32})$", meta.read_text(encoding="utf-8") if meta.exists() else "", re.MULTILINE)
    return {
        "id": identifier.group(1) if identifier else None,
        "kind": kind,
        "label": label.group(1).strip() if label else None,
        "path": str(asset.relative_to(asset.parents[4])).replace("\\", "/"),
        "guidValid": bool(guid_match),
    }


@click.group(invoke_without_command=True)
@click.option("--json", "as_json", is_flag=True, help="Emit machine-readable JSON.")
@click.pass_context
def main(ctx, as_json):
    """Agent-native CLI for the Kingdom Unity project."""
    ctx.ensure_object(dict)
    ctx.obj["json"] = as_json
    if ctx.invoked_subcommand is None:
        click.echo(ctx.get_help())


@main.group()
def project():
    """Inspect the Unity project."""


@project.command("info")
@click.pass_context
def project_info(ctx):
    root = _project_root()
    payload = {
        "project": root.name,
        "projectRoot": str(root),
        "unityVersion": "2022.3.62f2c1",
        "primaryScene": "Assets/Scenes/SampleScene.unity",
        "resourceDefinitions": len(_resource_assets(root)),
        "researchDefinitions": len(_research_assets(root)),
        "buildingDefinitions": len(list((root / "Assets/Resources/Datas/Building").rglob("*.asset"))),
    }
    _json_or_text(payload, ctx.obj["json"])


@main.group()
def assets():
    """Inspect and validate ScriptableObject definitions."""


@assets.command("list")
@click.argument("kind", type=click.Choice(["resources", "research"]))
@click.pass_context
def assets_list(ctx, kind):
    root = _project_root()
    files = _resource_assets(root) if kind == "resources" else _research_assets(root)
    records = [_definition_record(asset, kind) for asset in files]
    if ctx.obj["json"]:
        click.echo(json.dumps(records, ensure_ascii=False, indent=2))
    else:
        for record in records:
            click.echo(f"{record['id']}\t{record['label']}\t{record['path']}")


@assets.command("validate")
@click.pass_context
def assets_validate(ctx):
    root = _project_root()
    resources = [_definition_record(asset, "resource") for asset in _resource_assets(root)]
    researches = [_definition_record(asset, "research") for asset in _research_assets(root)]
    records = resources + researches
    duplicate_ids = []
    for kind_records in (resources, researches):
        ids = [record["id"] for record in kind_records]
        duplicate_ids.extend(
            f"{kind_records[0]['kind']}:{item}"
            for item in sorted({item for item in ids if item and ids.count(item) > 1})
        )
    invalid = [record["path"] for record in records if not record["id"] or not record["guidValid"]]
    payload = {
        "valid": not duplicate_ids and not invalid,
        "definitionCount": len(records),
        "duplicateIds": duplicate_ids,
        "invalidDefinitions": invalid,
    }
    _json_or_text(payload, ctx.obj["json"])
    if not payload["valid"]:
        raise click.ClickException("Definition validation failed")


@main.group()
def unity():
    """Invoke the real Unity Editor backend."""


@unity.command("compile")
@click.option("--log-file", type=click.Path(path_type=Path), default=None)
@click.pass_context
def unity_compile(ctx, log_file):
    root = _project_root()
    log_path = log_file or (root / "TestResults" / "cli-anything-unity-compile.log")
    log_path.parent.mkdir(parents=True, exist_ok=True)
    unity = Path(r"D:\Unity\Hub\Editor\2022.3.62f2c1\Editor\Unity.exe")
    if not unity.exists():
        raise click.ClickException(f"Unity Editor not found: {unity}")
    command = [str(unity), "-batchmode", "-nographics", "-quit", "-projectPath", str(root), "-logFile", str(log_path)]
    result = subprocess.run(command, cwd=root, text=True, timeout=180)
    payload = {"exitCode": result.returncode, "logFile": str(log_path), "success": result.returncode == 0}
    _json_or_text(payload, ctx.obj["json"])
    if result.returncode != 0:
        raise click.ClickException("Unity compilation failed")
