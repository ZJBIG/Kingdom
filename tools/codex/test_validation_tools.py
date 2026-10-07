"""Regression tests for validation wrapper failure reporting.

These tests invoke PowerShell with an isolated fake Unity process. They never
open the Unity project or touch TestResults, Logs, or user save data.
"""

from __future__ import annotations

import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
PS = shutil.which("pwsh") or shutil.which("powershell")


def run_ps(script: Path, *args: str, env: dict[str, str] | None = None) -> subprocess.CompletedProcess[str]:
    if PS is None:
        raise unittest.SkipTest("PowerShell is unavailable")
    return subprocess.run(
        [PS, "-NoProfile", "-File", str(script), *args],
        cwd=ROOT,
        env=env,
        text=True,
        encoding="utf-8",
        errors="replace",
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        check=False,
    )


FAKE_UNITY = r'''param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Args)
$results = $null; $log = $null
for ($i = 0; $i -lt $Args.Count; $i++) {
    if ($Args[$i] -eq '-testResults') { $results = $Args[$i + 1] }
    if ($Args[$i] -eq '-logFile') { $log = $Args[$i + 1] }
}
if ($env:FAKE_COMPILE) {
    if ($env:FAKE_COMPILE -eq 'good') { Set-Content $log 'Unity compile completed' }
    elseif ($env:FAKE_COMPILE -eq 'empty') { Set-Content $log '' }
    elseif ($env:FAKE_COMPILE -eq 'nonzero') { Set-Content $log 'Unity compile completed'; exit 9 }
    exit 0
}
if ($env:FAKE_TEST -eq 'no-xml') { exit 0 }
if ($env:FAKE_TEST -eq 'bad-xml') { Set-Content $results '<broken>'; exit 0 }
if ($env:FAKE_TEST -eq 'missing-counts') { Set-Content $results '<test-run total="1" passed="1" failed="0" />'; exit 0 }
if ($env:FAKE_TEST -eq 'negative-counts') { Set-Content $results '<test-run total="1" passed="1" failed="0" skipped="-1" />'; exit 0 }
if ($env:FAKE_TEST -eq 'non-numeric-counts') { Set-Content $results '<test-run total="one" passed="1" failed="0" skipped="0" />'; exit 0 }
if ($env:FAKE_TEST -eq 'zero') { Set-Content $results '<test-run total="0" passed="0" failed="0" skipped="0" />'; exit 0 }
if ($env:FAKE_TEST -eq 'nonzero-success') { Set-Content $results '<test-run total="1" passed="1" failed="0" skipped="0" />'; exit 7 }
Set-Content $results '<test-run total="1" passed="1" failed="0" skipped="0" />'
exit 0
'''


class ValidationToolTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory(prefix="kingdom-validation-")
        self.root = Path(self.temp.name)
        fake_ps = self.root / "fake-unity.ps1"
        fake_ps.write_text(FAKE_UNITY, encoding="utf-8")
        fake_cmd = self.root / "fake-unity.cmd"
        fake_cmd.write_text(
            '@echo off\npowershell -NoProfile -File "%~dp0fake-unity.ps1" %*\nexit /b %ERRORLEVEL%\n',
            encoding="ascii",
        )
        self.unity = fake_cmd

    def tearDown(self) -> None:
        self.temp.cleanup()
        self.assertFalse(self.root.exists(), "Validation fixture directory was not removed")

    def invoke_tests(self, mode: str) -> tuple[int, str]:
        out = self.root / mode
        out.mkdir()
        env = os.environ.copy()
        env["FAKE_TEST"] = mode
        result = run_ps(
            ROOT / "tools/codex/run-unity-tests.ps1",
            "-Platform", "EditMode", "-ProjectPath", str(ROOT), "-UnityPath", str(self.unity),
            "-ResultsPath", str(out / "result.xml"), "-LogPath", str(out / "run.log"),
            "-LatestErrorsPath", str(out / "latest.txt"), "-TimeoutSeconds", "10", env=env,
        )
        return result.returncode, (out / "latest.txt").read_text(encoding="utf-8")

    def test_runner_rejects_false_success_cases(self) -> None:
        for mode in ("no-xml", "bad-xml", "missing-counts", "negative-counts",
                     "non-numeric-counts", "zero", "nonzero-success"):
            code, report = self.invoke_tests(mode)
            self.assertNotEqual(code, 0, mode)
            self.assertNotIn("Result: Passed", report, mode)
        code, report = self.invoke_tests("success")
        self.assertEqual(code, 0)
        self.assertIn("Result: Passed", report)

    def test_compile_requires_nonempty_log(self) -> None:
        for mode in ("missing", "empty", "nonzero"):
            out = self.root / f"compile-{mode}"
            out.mkdir()
            env = os.environ.copy()
            env["FAKE_COMPILE"] = "empty" if mode == "empty" else ("nonzero" if mode == "nonzero" else "missing")
            result = run_ps(
                ROOT / "tools/codex/compile-unity.ps1",
                "-ProjectPath", str(ROOT), "-UnityPath", str(self.unity),
                "-LogPath", str(out / "compile.log"), "-TimeoutSeconds", "10", env=env,
            )
            self.assertNotEqual(result.returncode, 0, mode)
        out = self.root / "compile-good"
        out.mkdir()
        env = os.environ.copy(); env["FAKE_COMPILE"] = "good"
        result = run_ps(
            ROOT / "tools/codex/compile-unity.ps1",
            "-ProjectPath", str(ROOT), "-UnityPath", str(self.unity),
            "-LogPath", str(out / "compile.log"), "-TimeoutSeconds", "10", env=env,
        )
        self.assertEqual(result.returncode, 0)

    def test_aggregate_forwards_custom_root_and_rejects_failed_gate(self) -> None:
        tools = self.root / "tools/codex"
        tools.mkdir(parents=True)
        gates = (
            "validate-guidance.ps1", "validate-ui-contract.ps1",
            "validate-android-settings.ps1", "verify-yaml-references.ps1",
            "content-closure-check.ps1", "building-resource-flow-check.ps1",
        )
        fake_gate = r'''param([string]$ProjectPath, [string]$ProjectRoot)
$name = Split-Path -Leaf $PSCommandPath
$root = if ($name -eq 'content-closure-check.ps1') { $ProjectRoot } else { $ProjectPath }
Add-Content -LiteralPath $env:FAKE_GATE_LOG "$name|$root"
if ($name -eq $env:FAKE_GATE_FAILURE) { exit 7 }
exit 0
'''
        for name in gates:
            (tools / name).write_text(fake_gate, encoding="utf-8")
        for name in ("run-unity-tests.ps1", "build-android.ps1", "compile-developer-tests.ps1"):
            (tools / name).write_text("exit 0\n", encoding="utf-8")
        log = self.root / "gates.txt"
        env = os.environ.copy()
        env["FAKE_GATE_LOG"] = str(log)
        env["FAKE_GATE_FAILURE"] = ""
        script = ROOT / "tools/codex/validate-todolist-gates.ps1"
        result = run_ps(script, "-ProjectPath", str(self.root), env=env)
        self.assertEqual(result.returncode, 0, result.stdout)
        self.assertEqual(log.read_text(encoding="utf-8-sig").splitlines(),
                         [f"{name}|{self.root}" for name in gates])
        log.unlink()
        env["FAKE_GATE_FAILURE"] = gates[1]
        result = run_ps(script, "-ProjectPath", str(self.root), env=env)
        self.assertNotEqual(result.returncode, 0, result.stdout)
        self.assertIn("Gate failed with exit code 7", result.stdout)
        self.assertEqual(log.read_text(encoding="utf-8-sig").splitlines(),
                         [f"{name}|{self.root}" for name in gates[:2]])


if __name__ == "__main__":
    unittest.main()
