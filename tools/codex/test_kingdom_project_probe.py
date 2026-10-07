#!/usr/bin/env python3
"""Probe tests with explicit verified cleanup in the project's excluded archive area."""
from __future__ import annotations

import contextlib
import hashlib
import io
import json
import os
from pathlib import Path
import re
import stat
import sys
import tempfile
import unittest
from unittest import mock

sys.dont_write_bytecode = True
import kingdom_project_probe as probe

REPOSITORY = Path(__file__).resolve().parents[2]


def remove_owned_fixture(root: Path, parent: Path) -> None:
    """Remove only one freshly allocated test tree; fail visibly on any residue."""
    probe.validate_local_directory(parent)
    if root.parent != parent or not root.name.startswith("probe-test-"):
        raise ValueError("Refusing to clean a directory not owned by this test")
    probe.validate_local_directory(root)
    files, directories = [], [root]
    pending = [root]
    while pending:
        for path in pending.pop().iterdir():
            if probe.is_link(path):
                raise ValueError("Refusing to traverse a fixture link/reparse point")
            if path.is_dir():
                pending.append(path)
                directories.append(path)
            else:
                files.append(path)
    for path in files:
        path.unlink()
    for path in sorted(directories, key=lambda p: len(p.parts), reverse=True):
        path.rmdir()
    if root.exists():
        raise RuntimeError(f"Fixture cleanup left a directory behind: {root}")


def remove_owned_symlink(link: Path, root: Path) -> None:
    """Remove only this test's real link, never traverse its target."""
    probe.validate_local_directory(root)
    if link != root / "Assets/linked-scope":
        raise ValueError("Refusing to clean an unowned link")
    probe.validate_local_directory(link.parent)
    info = link.lstat()
    if not stat.S_ISLNK(info.st_mode):
        raise ValueError("Fixture link is not a real symlink; preserve for inspection")
    if os.name == "nt" and getattr(info, "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_DIRECTORY:
        link.rmdir()
    else:
        link.unlink()
    try:
        link.lstat()
    except FileNotFoundError:
        return
    raise RuntimeError("Fixture link cleanup left an entry behind")


def run_tests(argv=None):
    return unittest.main(argv=argv, verbosity=2, failfast=True)


class ProbeHarnessTests(unittest.TestCase):
    """In-memory control-flow tests; no fixture creation or real deletion."""

    def test_runner_stops_on_first_failure(self):
        with mock.patch.object(unittest, "main") as main:
            run_tests(["probe-tests"])
        main.assert_called_once_with(argv=["probe-tests"], verbosity=2, failfast=True)

    def test_cleanup_error_prevents_next_test_setup(self):
        calls = []

        class BlockedCase(unittest.TestCase):
            def runTest(self):
                self.addCleanup(self.block_cleanup)

            def block_cleanup(self):
                raise SystemExit(1)

        class LaterCase(unittest.TestCase):
            def setUp(self):
                calls.append("later setup")

            def runTest(self):
                calls.append("later run")

        result = unittest.TextTestRunner(stream=io.StringIO(), failfast=True).run(
            unittest.TestSuite([BlockedCase(), LaterCase()]))
        self.assertEqual(result.testsRun, 1)
        self.assertEqual(len(result.errors), 1)
        self.assertFalse(result.wasSuccessful())
        self.assertEqual(calls, [])

    def test_unowned_link_is_not_inspected_or_removed(self):
        root = Path("probe-harness-root")
        with mock.patch.object(probe, "validate_local_directory"), \
                mock.patch.object(Path, "lstat") as lstat, \
                mock.patch.object(Path, "rmdir") as rmdir, \
                mock.patch.object(Path, "unlink") as unlink:
            with self.assertRaises(ValueError):
                remove_owned_symlink(root / "Assets/unowned", root)
        lstat.assert_not_called()
        rmdir.assert_not_called()
        unlink.assert_not_called()

    def check_link_cleanup(self, platform, directory):
        root = Path("probe-harness-root")
        link = root / "Assets/linked-scope"
        info = mock.Mock(st_mode=stat.S_IFLNK,
                         st_file_attributes=stat.FILE_ATTRIBUTE_DIRECTORY if directory else 0)
        with mock.patch.object(probe, "validate_local_directory"), \
                mock.patch.object(os, "name", platform), \
                mock.patch.object(Path, "lstat", side_effect=[info, FileNotFoundError()]), \
                mock.patch.object(Path, "rmdir") as rmdir, \
                mock.patch.object(Path, "unlink") as unlink:
            remove_owned_symlink(link, root)
        if platform == "nt" and directory:
            rmdir.assert_called_once_with()
            unlink.assert_not_called()
        else:
            unlink.assert_called_once_with()
            rmdir.assert_not_called()

    def test_windows_directory_symlink_uses_rmdir(self):
        self.check_link_cleanup("nt", True)

    def test_file_symlink_uses_unlink(self):
        self.check_link_cleanup("nt", False)

    def test_posix_symlink_uses_unlink(self):
        self.check_link_cleanup("posix", True)

    def test_non_link_is_preserved(self):
        root = Path("probe-harness-root")
        info = mock.Mock(st_mode=stat.S_IFDIR)
        with mock.patch.object(probe, "validate_local_directory"), \
                mock.patch.object(Path, "lstat", return_value=info), \
                mock.patch.object(Path, "rmdir") as rmdir, \
                mock.patch.object(Path, "unlink") as unlink:
            with self.assertRaises(ValueError):
                remove_owned_symlink(root / "Assets/linked-scope", root)
        rmdir.assert_not_called()
        unlink.assert_not_called()

    def test_link_cleanup_residue_is_an_error(self):
        root = Path("probe-harness-root")
        info = mock.Mock(st_mode=stat.S_IFLNK, st_file_attributes=0)
        with mock.patch.object(probe, "validate_local_directory"), \
                mock.patch.object(Path, "lstat", return_value=info), \
                mock.patch.object(Path, "unlink"):
            with self.assertRaises(RuntimeError):
                remove_owned_symlink(root / "Assets/linked-scope", root)

    def test_link_cleanup_does_not_swallow_protection_block(self):
        root = Path("probe-harness-root")
        info = mock.Mock(st_mode=stat.S_IFLNK, st_file_attributes=0)
        with mock.patch.object(probe, "validate_local_directory"), \
                mock.patch.object(Path, "lstat", return_value=info), \
                mock.patch.object(Path, "unlink", side_effect=SystemExit(1)):
            with self.assertRaises(SystemExit):
                remove_owned_symlink(root / "Assets/linked-scope", root)


class GuidanceContractTests(unittest.TestCase):
    """Read-only real-repository contracts and in-memory negative cases.

    This suite does not exercise fixture cleanup or prove client auto-discovery.
    """

    @classmethod
    def setUpClass(cls):
        cls.root = probe.validate_root(REPOSITORY)

    def text(self, relative):
        return probe.read_bounded(probe.safe_path(self.root, relative)).decode("utf-8-sig")

    def test_real_skill_checks_without_write_operations(self):
        with contextlib.ExitStack() as stack:
            for method in ("write_text", "write_bytes", "mkdir", "unlink", "rmdir", "rename"):
                stack.enter_context(mock.patch.object(Path, method,
                                    side_effect=AssertionError("read-only guidance check")))
            self.assertEqual(probe.check_skill(self.root), [])

    def test_required_project_contracts_exist(self):
        for relative in probe.REQUIRED_PROJECT_FILES:
            with self.subTest(path=relative):
                self.assertTrue(probe.safe_path(self.root, relative).is_file())

    def test_only_main_skill_has_task_router_table(self):
        main = self.text(f"{probe.SKILL_ROOT}/SKILL.md")
        self.assertEqual(main.count("| 任务 |"), 1)
        for relative in ("AGENTS.md", "README.md", ".codex/prompts/CODEX_ECONOMY_PROMPT.md"):
            with self.subTest(path=relative):
                text = self.text(relative)
                self.assertNotIn("| 任务 |", text)
                self.assertIn(f"{probe.SKILL_ROOT}/SKILL.md", text)

    def test_domain_entries_return_through_main(self):
        for name in ("kingdom-economy-simulation", "kingdom-ui-redesign"):
            with self.subTest(skill=name):
                text = self.text(f".agents/skills/{name}/SKILL.md")
                self.assertIn(f"{probe.SKILL_ROOT}/SKILL.md", text)
                self.assertIn("不回读", text)

    def test_client_adapter_is_thin_and_points_to_main(self):
        text = self.text(".workbuddy-ai/skills/kingdom-project-dev/SKILL.md")
        self.assertIn(f"{probe.SKILL_ROOT}/SKILL.md", text)
        self.assertLessEqual(len(text.splitlines()), 16)

    def test_consolidated_owners_preserve_product_and_design_boundaries(self):
        owners = {
            "docs/decisions/conservative-defaults.md": ("不追溯移除", "退款"),
            "docs/ui/page-responsibilities.md": ("Resource | 无主操作按钮", "EraGoalEvaluation"),
            ".agents/skills/kingdom-economy-simulation/references/content-design.md":
                ("设计灵感边界", "不照抄普通资源容量墙"),
        }
        for relative, markers in owners.items():
            with self.subTest(path=relative):
                text = self.text(relative)
                for marker in markers:
                    self.assertIn(marker, text)

    def test_unique_ui_semantics_were_preserved(self):
        text = self.text("docs/ui/page-responsibilities.md")
        for marker in ("Resource | 无主操作按钮", "左右各48", "金色Outline", "连接线",
                       "SectorBuilding", "TryBuild/TryDeconstruct", "重复订阅"):
            with self.subTest(marker=marker):
                self.assertIn(marker, text)
        ui = self.text(".agents/skills/kingdom-ui-redesign/SKILL.md")
        self.assertIn("docs/ui/page-responsibilities.md", ui)
        self.assertNotIn("左右各48", ui)

    def test_product_defaults_preserved_without_stale_resolution(self):
        text = self.text("docs/decisions/conservative-defaults.md")
        for marker in ("不追溯移除", "Food/s", "退款", "建筑优先级", "MusicManager"):
            self.assertIn(marker, text)
        self.assertNotIn("1920x1080", text)
        self.assertNotIn("\\n##", text)

    def test_archives_are_not_required_dependencies(self):
        paths = list(probe.REQUIRED_PROJECT_FILES) + list(probe.REQUIRED_SKILL_FILES)
        ps = self.text("tools/codex/validate-guidance.ps1")
        paths += re.findall(r'^\s+"([^"]+)",?$', ps, re.M)
        self.assertTrue(paths)
        self.assertFalse(any("archive/" in path for path in paths))

    def test_powershell_required_paths_exist(self):
        paths = re.findall(r'^\s+"([^"]+)",?$',
                           self.text("tools/codex/validate-guidance.ps1"), re.M)
        self.assertGreater(len(paths), 20)
        for relative in paths:
            with self.subTest(path=relative):
                self.assertTrue(probe.safe_path(self.root, relative).is_file())

    def test_missing_maintenance_reference_fails_in_memory(self):
        real_read = probe.read_bounded
        def missing(path):
            if path.name == "guidance-maintenance.md":
                raise FileNotFoundError("synthetic missing maintenance reference")
            return real_read(path)
        with mock.patch.object(probe, "read_bounded", side_effect=missing):
            errors = probe.check_skill(self.root)
        self.assertTrue(any("synthetic missing" in error for error in errors))

    def test_invalid_domain_frontmatter_fails_in_memory(self):
        target = probe.safe_path(self.root, ".agents/skills/kingdom-ui-redesign/SKILL.md")
        real_read = probe.read_bounded
        with mock.patch.object(probe, "read_bounded",
                               side_effect=lambda path: b"# invalid" if path == target else real_read(path)):
            self.assertIn("Skill frontmatter is missing", probe.check_skill(self.root))

    def test_current_ui_boundaries_do_not_restore_legacy_refresh_architecture(self):
        text = self.text("docs/architecture/ui-boundaries.md")
        self.assertIn("KingdomUIRoot.LiveRefresh.cs", text)
        self.assertIn("SetResearchPageVisible", text)
        self.assertIn("Hidden does not mean interactive", text)
        self.assertIn("Do not rebuild", text)

    def test_leaf_metadata_does_not_enable_implicit_invocation(self):
        for name in ("kingdom-economy-simulation", "kingdom-ui-redesign"):
            text = self.text(f".agents/skills/{name}/agents/openai.yaml")
            self.assertIn("allow_implicit_invocation: false", text)
            self.assertIn(f"{probe.SKILL_ROOT}/SKILL.md", text)


class ProjectProbeTests(unittest.TestCase):
    def setUp(self):
        self.scratch = probe.validate_local_directory(REPOSITORY / ".codex/archive")
        if not self.scratch.is_relative_to(REPOSITORY):
            raise RuntimeError("Test scratch area must stay inside the repository")
        self.root = Path(tempfile.mkdtemp(prefix="probe-test-", dir=self.scratch))
        self.addCleanup(remove_owned_fixture, self.root, self.scratch)
        for relative in probe.REQUIRED_PROJECT_FILES:
            self.put(relative, "# fixture\n")
        self.put("ProjectSettings/ProjectVersion.txt", "m_EditorVersion: 2022.3.62f3c1\n")
        self.put("Assets/Resources/Script/Manager/GameBootstrap.cs", "class GameBootstrap {}\n")
        self.put("Assets/Resources/Datas/Resource/Test.asset", "name: 资源\n")
        for relative in probe.REQUIRED_SKILL_FILES:
            self.put(f"{probe.SKILL_ROOT}/{relative}", "# fixture\n")
        self.put(f"{probe.SKILL_ROOT}/SKILL.md",
                 "---\nname: kingdom-project-dev\ndescription: Kingdom project fixture\n---\n"
                 "[validation](references/validation.md)\n")
        for name in ("kingdom-economy-simulation", "kingdom-ui-redesign"):
            self.put(f".agents/skills/{name}/SKILL.md",
                     f"---\nname: {name}\ndescription: Kingdom domain fixture\n---\n")
        self.put(".workbuddy-ai/skills/kingdom-project-dev/SKILL.md",
                 "---\nname: kingdom-project-dev\ndescription: Kingdom adapter fixture\n---\n")

    def put(self, relative, text):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
        return path

    def snapshot(self):
        return {p.relative_to(self.root).as_posix():
                (p.stat().st_mtime_ns, hashlib.sha256(p.read_bytes()).hexdigest())
                for p in self.root.rglob("*") if p.is_file() and not p.is_symlink()}

    def test_valid_root(self):
        self.assertEqual(probe.validate_root(self.root), self.root.resolve())

    def test_wrong_root_is_rejected(self):
        wrong = self.root / "unrelated"
        wrong.mkdir()
        with self.assertRaises(ValueError):
            probe.validate_root(wrong)

    def test_paths_cannot_escape(self):
        for relative in ("../other", "/tmp", "C:/Users", "Assets/../../outside", "Assets\\file"):
            with self.subTest(relative=relative), self.assertRaises(ValueError):
                probe.safe_path(self.root, relative)

    def test_inventory_does_not_write(self):
        before = self.snapshot()
        result = probe.inventory(probe.validate_root(self.root), True)
        self.assertEqual(result["errors"], [])
        self.assertEqual(before, self.snapshot())

    def test_fingerprint_stays_stable(self):
        first = probe.inventory(self.root)
        second = probe.inventory(self.root)
        self.assertEqual(first["source_fingerprint"], second["source_fingerprint"])

    def test_fingerprint_changes_with_source(self):
        before = probe.inventory(self.root)["source_fingerprint"]
        self.put("Assets/Resources/Script/Manager/GameBootstrap.cs", "class GameBootstrap { int changed; }")
        self.assertNotEqual(before, probe.inventory(self.root)["source_fingerprint"])

    def test_generated_directories_are_excluded(self):
        before = probe.inventory(self.root)["source_fingerprint"]
        self.put("Library/cache.cs", "never read")
        self.put("tools/NewEconomySimulator/bin/cache.cs", "never read")
        result = probe.inventory(self.root)
        self.assertEqual(before, result["source_fingerprint"])
        self.assertFalse(any("cache.cs" in x["path"] for x in result["files"]))

    def test_utf8_and_json_cli(self):
        buffer = io.StringIO()
        with contextlib.redirect_stdout(buffer):
            code = probe.main(["--project-root", str(self.root), "--check-skill", "--format", "json"])
        result = json.loads(buffer.getvalue())
        self.assertEqual(code, 0)
        self.assertEqual(result["definition_counts"]["Resource"], 1)
        self.assertTrue(result["read_only"])

    def test_missing_skill_reference_fails(self):
        (self.root / probe.SKILL_ROOT / "references/validation.md").unlink()
        self.assertTrue(probe.check_skill(self.root))

    def test_malformed_frontmatter_fails(self):
        self.put(f"{probe.SKILL_ROOT}/SKILL.md", "# no frontmatter\n")
        self.assertIn("Skill frontmatter is missing", probe.check_skill(self.root))

    def test_missing_project_contract_fails(self):
        (self.root / "Assets/Tests/AGENTS.md").unlink()
        self.assertTrue(any("Missing required" in error for error in probe.inventory(self.root)["errors"]))

    def test_oversized_file_is_not_read(self):
        self.put("Assets/Resources/Script/Oversized.cs", "x" * 120)
        with mock.patch.object(probe, "MAX_BYTES", 100):
            result = probe.inventory(self.root)
        self.assertTrue(any(x["reason"] == "oversized; metadata only" for x in result["skipped"]))
        record = next(x for x in result["files"] if x["path"].endswith("Oversized.cs"))
        self.assertNotIn("sha256", record)

    def test_read_failure_is_visible(self):
        real_read = probe.read_bounded
        def fail_one(path):
            if path.name == "Test.asset":
                raise PermissionError("fixture denied")
            return real_read(path)
        with mock.patch.object(probe, "read_bounded", side_effect=fail_one):
            result = probe.inventory(self.root)
        self.assertTrue(any("fixture denied" in x for x in result["errors"]))

    def test_missing_evidence_is_not_passing(self):
        result = probe.inventory(self.root)
        self.assertTrue(all(not x["exists"] for x in result["evidence"]))
        self.assertTrue(all("not evaluated" in x["acceptance"] for x in result["evidence"]))

    def test_link_policy_without_os_privilege(self):
        link = self.put("Assets/Resources/Script/linked.cs", "not read")
        original = probe.is_link
        with mock.patch.object(probe, "is_link", side_effect=lambda p: p == link or original(p)):
            result = probe.inventory(self.root)
        self.assertTrue(any(x["path"].endswith("linked.cs") for x in result["skipped"]))
        self.assertFalse(any(x["path"].endswith("linked.cs") for x in result["files"]))

    def test_unc_root_is_rejected_before_filesystem_access(self):
        with mock.patch.object(probe, "is_link", side_effect=AssertionError("must reject before lstat")):
            for value in ("//server/share", "\\\\server\\share"):
                with self.subTest(value=value), self.assertRaises(ValueError):
                    probe.validate_local_directory(value)

    def test_linked_output_is_rejected(self):
        output = self.root / "outputs"
        output.mkdir()
        original = probe.is_link
        with mock.patch.object(probe, "is_link", side_effect=lambda p: p == output or original(p)):
            with self.assertRaises(ValueError):
                probe.validate_local_directory(output)

    def test_link_check_precedes_target_existence(self):
        link = self.root / "Assets/link"
        original = probe.is_link
        with mock.patch.object(probe, "is_link", side_effect=lambda p: p == link or original(p)):
            with mock.patch.object(Path, "exists", side_effect=AssertionError("must not dereference")):
                with self.assertRaises(ValueError):
                    probe.safe_path(self.root, "Assets/link/secret")

    def test_fixture_cleanup_really_removes_tree(self):
        parent = self.root / "isolated"
        parent.mkdir()
        owned = parent / "probe-test-owned"
        (owned / "child").mkdir(parents=True)
        (owned / "child/item.txt").write_text("owned", encoding="utf-8")
        remove_owned_fixture(owned, parent)
        self.assertFalse(owned.exists())

    def test_cleanup_residue_fails_instead_of_passing(self):
        parent = self.root / "isolated"
        parent.mkdir()
        owned = parent / "probe-test-owned"
        owned.mkdir()
        with mock.patch.object(Path, "rmdir", return_value=None):
            with self.assertRaises(RuntimeError):
                remove_owned_fixture(owned, parent)

    def test_cleanup_cannot_remove_unowned_directory(self):
        with self.assertRaises(ValueError):
            remove_owned_fixture(self.root, self.root)

    def test_fixture_leak_is_reported(self):
        self.put("outputs/probe-test-leaked/AGENTS.md", "# fixture\n")
        self.assertTrue(any("leaked" in error for error in probe.check_skill(self.root)))

    def test_retired_skill_is_rejected(self):
        self.put(".agents/skills/retired-extra/SKILL.md", "# not canonical\n")
        self.assertTrue(any("Unexpected canonical" in error for error in probe.check_skill(self.root)))

    def test_domain_skill_frontmatter_is_checked(self):
        self.put(".agents/skills/kingdom-economy-simulation/SKILL.md", "# missing metadata\n")
        self.assertIn("Skill frontmatter is missing", probe.check_skill(self.root))

    def test_actual_symlink_if_os_permits(self):
        link = self.root / "Assets/linked-scope"
        try:
            link.symlink_to(self.root / "ProjectSettings", target_is_directory=True)
        except OSError as exc:
            self.skipTest(f"OS does not permit creating a fixture symlink: {exc}")
        # Check creation independently of the production predicate. A wrapper
        # returning without creating a real link is a failure, not a passed audit.
        self.assertTrue(stat.S_ISLNK(link.lstat().st_mode),
                        "symlink_to did not create a real symlink; inspect the test environment")
        self.addCleanup(remove_owned_symlink, link, self.root)
        result = probe.inventory(self.root)
        self.assertTrue(any(x["path"] == "Assets/linked-scope" for x in result["skipped"]))


if __name__ == "__main__":
    run_tests()
