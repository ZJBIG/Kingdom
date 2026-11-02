# Validation record: version 0.1.0

Date: 2026-10-24. Status: locally checked release candidate; not submitted to Agensi.

## What was tested

Two independent agents received identical synthetic JavaScript, Python, and Unity-style inputs and the same three user requests. One read this skill and its verification reference; the baseline did not read this package. Each had its own isolated copy. A third agent reviewed the package text without modifying it. The coordinating agent subsequently checked final files and reran the executable checks.

This is one small paired trial, not a benchmark across real repositories. The agents were directly instructed to read the skill; automatic activation and marketplace connector delivery were not tested. Both agents also received the same explicit scope restrictions, so adherence cannot be attributed to the skill alone.

Synthetic input manifest SHA-256: f5dd187eb75d6b336491be2f024507ef3a394438cfbe6f782e03182d6aad80cd.
Runtime: Python 3.13.5; Node v24.22.1. No package installation or real repository copy.

| Request | Without skill | With skill | Coordinator check |
|---|---|---|---|
| Fix Python CLI's explicit zero limit while retaining missing/null defaults | Reproduced failure, made a narrow fix, added caller regression | Reproduced failure at function and caller, made a narrow fix, added caller regression | Both: `python -B -m unittest -v`, exit 0, 5 tests passed |
| Review JavaScript duplicate notifications after detach/reattach; no changes | Identified callback identity mismatch and reproduced it | Identified callback identity mismatch and reproduced it | Both: `node --test`, exit 1, 1 failing test, actual 2 notifications vs expected 1; this is intentional defect evidence, not a passing suite |
| Review proposed Unity serialized field rename; no engine available | Identified serialized-name compatibility, stable GUID, stale XML and runtime gap | Identified serialized-name compatibility, stable GUID, stale XML and runtime gap | Original source, prefab, metadata and historical XML remained byte-identical |

Each Python input initially had four tests; both agents reported the zero-limit test failing before modification. Both produced a five-test passing final suite. The added caller tests differ: the assisted trial additionally preserves the existing negative-slice behavior. This is an observed coverage difference in one trial, not proof of systematic improvement.

The coordinator compared every final fixture file with the original input. Each variant changed exactly python/options.py and python/test_options.py. User notes stayed byte-identical; all Web/Unity files stayed byte-identical; no files were added or removed. Git overlap protection was not tested because the fixtures had no Git history.

The Unity fixture intentionally contained partial source/YAML and an unresolved local target reference, not a runnable game. No engine compile, EditMode, PlayMode, deserialization or interaction ran. 未执行真实 Unity 编译。

## Structural and textual checks

- `python -X utf8 C:/Users/19603/.codex/skills/.system/skill-creator/scripts/quick_validate.py <skill-folder>` returned `Skill is valid!`, exit 0.
- The three-file allowlist matched the source directory exactly. Both relative reference links resolved inside the package; UTF-8 reads succeeded. Static scans found no Kingdom name, personal absolute path, or mandatory companion skill in the distributed text.
- Manual review inspected the whole distributable. It ships no executable, dependencies, credentials, assets or third-party skill text. This is not a security certification or legal rights audit.
- Independent textual review found a premature completed-trial statement in the draft listing. It was first changed to in-progress, then replaced by the actual outcomes above after execution. No other actionable textual defects were reported.

## Evidence files

- verification-results.json: coordinator commands, exact exit codes, and captured output for both variants.
- trial-changes.diff: the two variants' actual Python changes. These are evidence only, outside the product ZIP.
- package-manifest.json: final ZIP inventory and input correspondence, generated when packaging.

Temporary trial root: C:/Users/19603/AppData/Local/Temp/agensi-skill-eval-20261024. Cleanup is blocked: automatic approval review rejected both recursive cleanup and a narrower exact-allowlist, individual-file cleanup with `blocked by policy`; neither deletion command executed. A final physical scan confirms 29 files, 7,785 bytes remain in that temporary root. This is an unresolved cleanup item, recorded in the handoff. No runnable evaluation fixtures are distributed or retained in the product workspace.

## Interpretation and remaining limits

The release candidate is self-contained and its workflow was usable in these three small tasks. The baseline also succeeded. There is no demonstrated increase in success rate, speed, or commercial value. US$9 is a trial price proposal only.

Real-project adoption, repeated trials, client-specific skill loading, relative-reference access through Agensi, platform ZIP acceptance, payout eligibility, and actual buyer demand have not been verified. Do not advertise universal support, certified safety, or measured productivity gains.

User chose local delivery first and upload later. The browser reached Agensi's login screen; no account was created, no credentials or KYC sent, no payout method connected, no listing draft saved, and no submission published.

## Packaging revision: version 0.1.1, 2026-10-25

The customer-facing listing was rewritten to focus on product capabilities, examples and setup. SKILL.md changed only its version metadata and introductory product wording; the detailed workflow and both references stayed unchanged. The v0.1.0 ZIP and a verified before-edit snapshot are preserved. The behavioral results above describe v0.1.0; no behavioral trials were rerun for this wording revision.

The v0.1.1 ZIP is 4,871 bytes and contains the same three relative paths. The skill validator returned `Skill is valid!` with exit 0. Exact inventory, reference containment/existence, UTF-8 reads, project/path scan, ZIP CRC and source byte equality checks passed. package-manifest.json now describes v0.1.1; the previous manifest is in .codex/archive/agensi-packaging-20261025-v011/ at the repository root. This internal record is outside the ZIP.
