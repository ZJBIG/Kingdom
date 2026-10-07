#!/usr/bin/env python
"""Compile gate for the Unity test assemblies, without launching Unity.

Python twin of tools/codex/compile-developer-tests.ps1. Same filtering, same
outputs under Temp/, same pass/fail semantics -- but it also works in
environments where the PowerShell tool cannot spawn native processes.

Why a twin exists
-----------------
The .ps1 gate drives Unity's Roslyn compiler via dotnet.exe. In a restricted
shell that silently swallows native processes, `$LASTEXITCODE` comes back empty
and `exit $null` yields 0, so the .ps1 gate can report success while compiling
nothing (fixed to fail loudly, but it still cannot compile there). This script
invokes the same compiler from Python, which does get to run native processes,
so the gate becomes usable again.

It answers exactly one question: "does the test code still build against
today's Runtime?" It compiles; it does not run. Unity Test Runner is still the
only thing that can execute the tests.

Usage
-----
    python tools/codex/compile-developer-tests.py
    python tools/codex/compile-developer-tests.py --target Editor
    python tools/codex/compile-developer-tests.py --project-root D:/GitHub/Kingdom

Exit code is 0 only when every selected assembly actually compiled and produced
its output file.
"""
import argparse
import os
import re
import subprocess
import sys

SRC_RE = re.compile(r'^"(?P<path>Assets/.+\.cs)"$')

TARGETS = {
    "Editor": {
        "rsp": "Assembly-CSharp-Editor.rsp",
        "assembly": "Kingdom.EditorTests",
        "extra_sources": os.path.join("Assets", "Tests", "Editor"),
    },
    "PlayMode": {
        "rsp": "Kingdom.PlayModeTests.rsp",
        "assembly": "Kingdom.PlayModeTests",
        "extra_sources": os.path.join("Assets", "Tests", "PlayMode"),
    },
}


def read_lines(path):
    with open(path, encoding="utf-8", errors="replace") as handle:
        return [line.rstrip("\r\n") for line in handle if line.strip()]


def find_unity_editor(project_root):
    """Locate the Unity editor directory from ProjectVersion.txt."""
    version_file = os.path.join(project_root, "ProjectSettings", "ProjectVersion.txt")
    if not os.path.exists(version_file):
        raise SystemExit("ProjectSettings/ProjectVersion.txt not found.")
    version = None
    for line in read_lines(version_file):
        if line.startswith("m_EditorVersion:"):
            version = line.split(":", 1)[1].strip()
            break
    if not version:
        raise SystemExit("m_EditorVersion missing from ProjectVersion.txt.")

    candidates = [
        os.path.join("D:\\", "Unity", "Hub", "Editor", version, "Editor"),
        os.path.join("C:\\", "Program Files", "Unity", "Hub", "Editor", version, "Editor"),
    ]
    program_files = os.environ.get("ProgramFiles")
    if program_files:
        candidates.append(os.path.join(program_files, "Unity", "Hub", "Editor", version, "Editor"))
    for candidate in candidates:
        compiler = os.path.join(candidate, "Data", "DotNetSdkRoslyn", "csc.dll")
        if os.path.exists(compiler):
            return candidate
    raise SystemExit("Unity %s compiler was not found next to any known editor path." % version)


def find_rsp(artifact_root, name, require_define=None):
    """Newest matching Bee response file; optionally one containing a define."""
    hits = []
    for dirpath, _dirnames, filenames in os.walk(artifact_root):
        for filename in filenames:
            if filename != name:
                continue
            path = os.path.join(dirpath, filename)
            hits.append((os.path.getmtime(path), path))
    hits.sort(reverse=True)
    for _mtime, path in hits:
        if require_define is None:
            return path
        if require_define in open(path, encoding="utf-8", errors="replace").read():
            return path
    return None


def filter_runtime(project_root, source_rsp):
    """Drop test-only bits, keep Bee's reference set, add current sources."""
    out = []
    for line in read_lines(source_rsp):
        if line.startswith("-out:") or line.startswith("-refout:"):
            continue
        if line == "-define:UNITY_INCLUDE_TESTS":
            continue
        match = SRC_RE.match(line)
        if match and not os.path.exists(os.path.join(project_root, match.group("path"))):
            continue
        out.append(line)
    return out, add_missing_sources(
        project_root, out, os.path.join("Assets", "Resources", "Script"))


def filter_test_assembly(project_root, source_rsp, runtime_dll, extra_sources):
    """Swap the stale Runtime reference for the freshly built one."""
    out = []
    for line in read_lines(source_rsp):
        if line.startswith("-out:") or line.startswith("-refout:"):
            continue
        if "Kingdom.Runtime.ref.dll" in line:
            out.append('-r:"%s"' % runtime_dll.replace("\\", "/"))
            continue
        out.append(line)
    return out, add_missing_sources(project_root, out, extra_sources)


def add_missing_sources(project_root, lines, relative_root):
    """Append sources that exist now but were not in Bee's snapshot."""
    known = {match.group("path") for match in (SRC_RE.match(line) for line in lines) if match}
    added = []
    root = os.path.join(project_root, relative_root)
    if not os.path.isdir(root):
        return added
    for dirpath, _dirnames, filenames in os.walk(root):
        for filename in filenames:
            if not filename.endswith(".cs"):
                continue
            relative = os.path.relpath(os.path.join(dirpath, filename), project_root)
            relative = relative.replace("\\", "/")
            if relative not in known:
                known.add(relative)
                lines.append('"%s"' % relative)
                added.append(relative)
    return added


def compile_assembly(dotnet, compiler, project_root, arguments, output_dll, label):
    os.makedirs(os.path.dirname(output_dll), exist_ok=True)
    for stale in (output_dll, output_dll[:-4] + ".ref.dll"):
        if os.path.exists(stale):
            os.remove(stale)

    # A response file is mandatory: the raw command line exceeds the Windows
    # limit once Bee's reference set is included (WinError 206).
    response = os.path.join(os.path.dirname(output_dll),
                            os.path.basename(output_dll)[:-4] + ".gate.rsp")
    header = [
        "-nologo",
        "-out:%s" % output_dll.replace("\\", "/"),
        "-refout:%s" % (output_dll[:-4] + ".ref.dll").replace("\\", "/"),
    ]
    with open(response, "w", encoding="utf-8") as handle:
        handle.write("\n".join(header + arguments))

    source_count = sum(1 for argument in arguments if SRC_RE.match(argument))
    print("=== %s ===" % label)
    print("  source_files=%d" % source_count)
    print("  response_file=%s" % response)

    completed = subprocess.run(
        [dotnet, compiler, "@%s" % response],
        cwd=project_root, capture_output=True, text=True, errors="replace")

    produced = os.path.exists(output_dll)
    print("  compiler_exit=%d  output_produced=%s" % (completed.returncode, produced))
    for stream_name, stream in (("stdout", completed.stdout), ("stderr", completed.stderr)):
        text = (stream or "").strip()
        if not text:
            continue
        lines = text.splitlines()
        print("  --- %s (%d lines, first 80 shown) ---" % (stream_name, len(lines)))
        for line in lines[:80]:
            print("   ", line)
    if produced:
        print("  dll=%s (%d bytes)" % (output_dll, os.path.getsize(output_dll)))
    print()
    return completed.returncode, produced


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--target", choices=["All", "Editor", "PlayMode"], default="All")
    parser.add_argument("--project-root",
                        default=os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))
    args = parser.parse_args()

    project_root = os.path.abspath(args.project_root)
    artifact_root = os.path.join(project_root, "Library", "Bee", "artifacts")
    output_directory = os.path.join(project_root, "Temp", "DeveloperTests")
    runtime_output = os.path.join(project_root, "Temp", "DeveloperBuild")

    if not os.path.isdir(artifact_root):
        raise SystemExit("Unity compile metadata is missing. Open the project in Unity once.")

    editor_directory = find_unity_editor(project_root)
    dotnet = os.path.join(editor_directory, "Data", "NetCoreRuntime", "dotnet.exe")
    compiler = os.path.join(editor_directory, "Data", "DotNetSdkRoslyn", "csc.dll")
    for required in (dotnet, compiler):
        if not os.path.exists(required):
            raise SystemExit("Missing Unity toolchain file: %s" % required)

    # 1) Runtime (Editor flavour) -- required by the test assemblies.
    runtime_rsp = find_rsp(artifact_root, "Kingdom.Runtime.rsp", require_define="-define:UNITY_EDITOR")
    if not runtime_rsp:
        raise SystemExit("Kingdom.Runtime.rsp with -define:UNITY_EDITOR was not found.")
    print("runtime rsp: %s" % runtime_rsp)
    runtime_arguments, added = filter_runtime(project_root, runtime_rsp)
    print("  sources added beyond Bee snapshot: %d" % len(added))
    runtime_dll = os.path.join(runtime_output, "Kingdom.Runtime.Editor.dll")
    runtime_exit, runtime_ok = compile_assembly(
        dotnet, compiler, project_root, runtime_arguments, runtime_dll,
        "Kingdom.Runtime.Editor")
    if not runtime_ok:
        print("Runtime assembly failed to compile; skipping the test assemblies.")
        print("SUMMARY: runtime_exit=%d runtime_ok=False" % runtime_exit)
        return 1

    # 2) Test assemblies.
    selected = list(TARGETS) if args.target == "All" else [args.target]
    results = []
    for name in selected:
        spec = TARGETS[name]
        source_rsp = find_rsp(artifact_root, spec["rsp"])
        if not source_rsp:
            print("%s not found under %s; skipped." % (spec["rsp"], artifact_root))
            results.append((name, 0, False))
            continue
        print("%s rsp: %s" % (name, source_rsp))
        arguments, added = filter_test_assembly(
            project_root, source_rsp, runtime_dll, spec["extra_sources"])
        print("  test sources added beyond Bee snapshot: %d" % len(added))
        for path in added:
            print("    + %s" % path)
        output_dll = os.path.join(output_directory, spec["assembly"] + ".dll")
        exit_code, ok = compile_assembly(
            dotnet, compiler, project_root, arguments, output_dll, spec["assembly"])
        results.append((name, exit_code, ok))

    print("SUMMARY: runtime_exit=%d runtime_ok=%s%s" % (
        runtime_exit, runtime_ok,
        "".join(" | %s_exit=%d %s_ok=%s" % (n, c, n.lower(), o) for n, c, o in results)))

    all_ok = runtime_ok and all(ok for _n, _c, ok in results)
    if all_ok:
        print("Test assemblies compiled. This proves compilation only; "
              "running the tests still requires the Unity Test Runner.")
    else:
        print("Compilation FAILED for at least one assembly.")
    return 0 if all_ok else 1


if __name__ == "__main__":
    sys.exit(main())
