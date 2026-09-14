#!/usr/bin/env python3
"""Read-only Kingdom inventory and skill reference checks; stdout only, stdlib only."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import sys
from collections import Counter
from datetime import datetime, timezone

SCAN_ROOTS = ("Assets", "Packages", "ProjectSettings", "docs", "tools",
              ".agents/skills", ".codex/handoffs", "CONTENTADVISE")
EXCLUDED_NAMES = frozenset({"library", ".git", "temp", "tmp", "bin", "obj",
                           "logs", ".vs", "node_modules", "__pycache__", "archive"})
TEXT_SUFFIXES = frozenset({".cs", ".md", ".json", ".asmdef", ".asset", ".prefab",
                           ".unity", ".meta", ".ps1", ".py", ".csproj", ".props",
                           ".sln", ".txt", ".yaml"})
FINGERPRINT_PREFIXES = ("Assets/Resources/Script/", "Assets/Resources/Datas/",
                        "Assets/Resources/UI/Kingdom/", "Assets/Scenes/",
                        "Assets/Tests/", "Assets/Editor/", "Packages/",
                        "ProjectSettings/", "docs/", "tools/", ".agents/skills/")
MAX_BYTES = 8 * 1024 * 1024
MAX_FILES = 20000
SKILL_ROOT = ".agents/skills/kingdom-project-dev"
REQUIRED_SKILL_FILES = ("SKILL.md", "references/validation.md",
                        "references/guidance-maintenance.md",
                        "references/subagents.md", "references/evidence.md",
                        "references/acceptance-cases.md", "assets/handoff-template.md")
REQUIRED_PROJECT_FILES = (
    "AGENTS.md", "ProjectSettings/ProjectVersion.txt",
    "Assets/Resources/Script/Manager/GameBootstrap.cs",
    "Assets/Resources/Script/AGENTS.md", "Assets/Tests/AGENTS.md",
    "Assets/Scenes/SampleScene.unity",
    "docs/architecture/runtime-state.md", "docs/architecture/serialized-pairs.md",
    "docs/architecture/ui-boundaries.md", "docs/repository-map.md",
    "docs/architecture/research-queue-payment.md",
    "docs/ui/page-responsibilities.md", "docs/decisions/conservative-defaults.md",
    "docs/testing/acceptance-checklist.md", "docs/testing/playmode-test-plan.md",
    ".codex/prompts/CODEX_ECONOMY_PROMPT.md",
    ".agents/skills/kingdom-economy-simulation/references/content-design.md",
    ".agents/skills/kingdom-economy-simulation/SKILL.md",
    ".agents/skills/kingdom-ui-redesign/SKILL.md",
)
EVIDENCE_PATHS = ("data/content-closure-static.md", "data/economy-parity",
                  "TestResults/Latest-Test-Errors.txt")


def is_link(path: Path) -> bool:
    """Reject symlinks, junctions and other Windows reparse points."""
    info = path.lstat()
    return (stat.S_ISLNK(info.st_mode)
            or bool(getattr(info, "st_file_attributes", 0)
                    & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0)))


def validate_local_directory(value: str | Path) -> Path:
    raw = os.fspath(value)
    if raw.startswith(("//", "\\\\")):
        raise ValueError("Remote/UNC directories are not allowed")
    candidate = Path(raw).absolute()
    if candidate.anchor.startswith(("//", "\\\\")):
        raise ValueError("Remote/UNC directories are not allowed")
    # Inspect each component with lstat before any is_dir/resolve/open operation.
    for component in reversed((candidate, *candidate.parents)):
        if is_link(component):
            raise ValueError(f"Linked directory is excluded: {component}")
    if not candidate.is_dir():
        raise ValueError("Directory does not exist")
    return candidate


def safe_path(root: Path, relative: str) -> Path:
    part = Path(relative)
    if part.is_absolute() or ".." in part.parts or ":" in relative or "\\" in relative:
        raise ValueError(f"Unsafe relative path: {relative}")
    current = root
    for name in part.parts:
        current = current / name
        try:
            if is_link(current):
                raise ValueError(f"Linked path is excluded: {relative}")
        except FileNotFoundError:
            # Missing paths are returned for the caller to report, never followed.
            break
    current = root / part
    if not current.is_relative_to(root):
        raise ValueError(f"Path escapes project: {relative}")
    return current


def validate_root(value: str | Path) -> Path:
    root = validate_local_directory(value)
    for marker in REQUIRED_PROJECT_FILES[:3]:
        if not safe_path(root, marker).is_file():
            raise ValueError(f"Not a Kingdom project: missing {marker}")
    return root


def read_bounded(path: Path) -> bytes:
    with path.open("rb") as stream:
        payload = stream.read(MAX_BYTES + 1)
    if len(payload) > MAX_BYTES:
        raise ValueError(f"Text file exceeds {MAX_BYTES} byte read limit")
    return payload


def walk_scope(root: Path, relative: str, skipped: list[dict], errors: list[str]):
    try:
        start = safe_path(root, relative)
    except (OSError, ValueError) as exc:
        skipped.append({"path": relative, "reason": str(exc)})
        return
    if not start.exists():
        skipped.append({"path": relative, "reason": "missing optional scope"})
        return
    pending = [start]
    while pending:
        directory = pending.pop()
        try:
            with os.scandir(directory) as scan:
                entries = sorted(scan, key=lambda item: item.name.casefold())
            for entry in entries:
                path = Path(entry.path)
                rel = path.relative_to(root).as_posix()
                if is_link(path):
                    skipped.append({"path": rel, "reason": "link/reparse point"})
                elif entry.is_dir(follow_symlinks=False):
                    if entry.name.casefold() in EXCLUDED_NAMES:
                        skipped.append({"path": rel, "reason": "generated/cache/archive"})
                    else:
                        pending.append(path)
                elif entry.is_file(follow_symlinks=False):
                    yield rel, path
        except OSError as exc:
            errors.append(f"Cannot enumerate {directory.relative_to(root).as_posix()}: {exc}")


def check_skill(root: Path) -> list[str]:
    errors: list[str] = []
    entries = [f"{SKILL_ROOT}/{relative}" for relative in REQUIRED_SKILL_FILES]
    entries += [".agents/skills/kingdom-economy-simulation/SKILL.md",
                ".agents/skills/kingdom-economy-simulation/references/content-design.md",
                ".agents/skills/kingdom-ui-redesign/SKILL.md",
                ".workbuddy-ai/skills/kingdom-project-dev/SKILL.md"]
    for rel in entries:
        try:
            path = safe_path(root, rel)
            text = read_bounded(path).decode("utf-8-sig")
            if path.name == "SKILL.md":
                header = re.match(r"\A---\r?\n(.*?)\r?\n---(?:\r?\n|$)", text, re.S)
                if not header:
                    errors.append("Skill frontmatter is missing")
                else:
                    metadata = header.group(1)
                    if not re.search(r"^name: " + re.escape(path.parent.name) + r"\s*$", metadata, re.M):
                        errors.append("Skill name does not match its directory")
                    if not re.search(r"^description: \S.+$", metadata, re.M):
                        errors.append("Skill description is missing")
            if "[TODO:" in text or "This is a placeholder" in text:
                errors.append(f"Unfinished template: {rel}")
            for target in re.findall(r"\[[^\]]*\]\(([^)]+)\)", text):
                if target.startswith(("https://", "http://", "#")):
                    continue
                target = target.split("#", 1)[0]
                linked = path.parent.relative_to(root).as_posix() + "/" + target
                if not safe_path(root, linked).is_file():
                    errors.append(f"Missing skill link: {rel} -> {target}")
        except (OSError, ValueError, UnicodeError) as exc:
            errors.append(f"Skill file check failed {rel}: {exc}")
    # Test fixtures or retired skills must not become extra discovery entries.
    expected = {"kingdom-project-dev", "kingdom-economy-simulation", "kingdom-ui-redesign"}
    try:
        skills = safe_path(root, ".agents/skills")
        actual = {p.name for p in skills.iterdir() if not is_link(p) and p.is_dir()
                  and safe_path(root, p.relative_to(root).as_posix() + "/SKILL.md").is_file()}
        if actual != expected:
            errors.append(f"Unexpected canonical skill set: {sorted(actual)}")
        output = safe_path(root, "outputs")
        if output.is_dir():
            leftovers = [p.name for p in output.iterdir() if p.name.startswith("probe-test-")]
            if leftovers:
                errors.append(f"Test fixtures leaked into outputs: {len(leftovers)} directories")
    except (OSError, ValueError) as exc:
        errors.append(f"Cannot check guidance topology: {exc}")
    return errors


def inventory(root: Path, with_skill: bool = False) -> dict:
    errors: list[str] = []
    skipped: list[dict] = []
    files: list[dict] = []
    digest = hashlib.sha256()
    candidates: list[tuple[str, Path]] = []
    for scope in SCAN_ROOTS:
        for item in walk_scope(root, scope, skipped, errors):
            candidates.append(item)
            if len(candidates) > MAX_FILES:
                raise ValueError(f"Scope exceeded {MAX_FILES} files; narrow the configured scope")
    candidates.append(("AGENTS.md", safe_path(root, "AGENTS.md")))
    for rel, path in sorted(candidates):
        try:
            # Recheck before reading in case a file was replaced during enumeration.
            path = safe_path(root, rel)
            info = path.stat()
            record = {"path": rel, "bytes": info.st_size,
                      "modified_utc": datetime.fromtimestamp(info.st_mtime, timezone.utc).isoformat()}
            selected = rel == "AGENTS.md" or rel.startswith(FINGERPRINT_PREFIXES)
            if selected and path.suffix.casefold() in TEXT_SUFFIXES:
                if info.st_size > MAX_BYTES:
                    skipped.append({"path": rel, "reason": "oversized; metadata only"})
                else:
                    payload = read_bounded(path)
                    after = path.stat()
                    if (info.st_size, info.st_mtime_ns) != (after.st_size, after.st_mtime_ns):
                        errors.append(f"File changed while reading: {rel}")
                    record["sha256"] = hashlib.sha256(payload).hexdigest()
                    digest.update(rel.encode("utf-8") + b"\0" + record["sha256"].encode() + b"\n")
            files.append(record)
        except (OSError, ValueError) as exc:
            errors.append(f"Cannot inspect {rel}: {exc}")
    for rel in REQUIRED_PROJECT_FILES:
        try:
            if not safe_path(root, rel).is_file():
                errors.append(f"Missing required project file: {rel}")
        except (OSError, ValueError) as exc:
            errors.append(str(exc))
    evidence = []
    for rel in EVIDENCE_PATHS:
        try:
            path = safe_path(root, rel)
            evidence.append({"path": rel, "exists": path.exists(),
                             "acceptance": "not evaluated; existence is not freshness or passing"})
        except (OSError, ValueError) as exc:
            evidence.append({"path": rel, "exists": False, "error": str(exc)})
    if with_skill:
        errors.extend(check_skill(root))
    paths = [item["path"] for item in files]
    source_prefixes = ("Assets/Resources/Script/", "Assets/Tests/Editor/",
                       "Assets/Tests/PlayMode/", "Assets/Editor/", "tools/NewEconomySimulator/")
    version = read_bounded(safe_path(root, "ProjectSettings/ProjectVersion.txt")).decode("utf-8-sig").strip()
    return {
        "schema_version": 1, "generated_utc": datetime.now(timezone.utc).isoformat(),
        "project_root": str(root), "unity_version_file": version,
        "read_only": True, "scope": list(SCAN_ROOTS),
        "excluded_top_level": ["Library", ".git", "Temp", "bin", "obj", "Logs",
                               "UserSettings", "outputs", "output", "tmp", ".workbuddy-ai", ".codex/archive"],
        "file_count": len(files), "fingerprinted_files": sum("sha256" in item for item in files),
        "source_fingerprint": digest.hexdigest(),
        "csharp_counts": {prefix: sum(p.startswith(prefix) and p.endswith(".cs") for p in paths)
                          for prefix in source_prefixes},
        "definition_counts": dict(sorted(Counter(p.split("/")[3] for p in paths
                                   if p.startswith("Assets/Resources/Datas/") and p.endswith(".asset")).items())),
        "evidence": evidence, "skill_checked": with_skill,
        "errors": errors, "skipped": skipped, "files": files,
        "limits": ["Inventory and static reference checks only; no Unity compilation or tests",
                   "Binary contents and excluded/cache/archive paths are not semantically audited",
                   "Fingerprint covers selected text paths only, not the full project",
                   "Concurrent filesystem changes cannot be made atomic without filesystem snapshots"],
    }


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project-root", required=True)
    parser.add_argument("--check-skill", action="store_true")
    parser.add_argument("--format", choices=("text", "json"), default="text")
    args = parser.parse_args(argv)
    try:
        result = inventory(validate_root(args.project_root), args.check_skill)
    except (OSError, ValueError, UnicodeError) as exc:
        print(json.dumps({"error": str(exc), "read_only": True}, ensure_ascii=False))
        return 2
    if args.format == "json":
        print(json.dumps(result, ensure_ascii=False, indent=2))
    else:
        print(f"Kingdom read-only inventory: {result['file_count']} files")
        print(f"Selected-text SHA-256: {result['source_fingerprint']}")
        print(f"C# counts: {result['csharp_counts']}")
        print(f"Definitions: {result['definition_counts']}")
        print(f"Errors: {len(result['errors'])}; skipped: {len(result['skipped'])}")
        for error in result["errors"]:
            print(f"ERROR: {error}")
        print("Unity compilation/tests: NOT RUN")
    return 1 if result["errors"] else 0


if __name__ == "__main__":
    sys.exit(main())
