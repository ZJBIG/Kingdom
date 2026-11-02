# Agensi independent skill product

## Authorization and scope

2026-10-24: User requested review of Agensi, preparation and upload where needed, with a product useful beyond Kingdom. User accepted the recommended cross-stack core and independent source package, using Agensi as one channel. This work does not change gameplay, existing installed skills, or existing user changes.

## Product

Source workspace: outputs/skill-products/evidence-driven-change-delivery/.
Distributable source: evidence-driven-change-delivery/ subfolder; three Markdown files, version 0.1.0. Newly written workflow; no Kingdom-specific tools/assets or third-party skill redistribution.

Listing draft proposes US$9 one-time; no actual price, account, payout setup, or publication has been changed.

## Validation plan and temporary work

Use independent agents on synthetic JavaScript, Python, and Unity-style repositories. Compare a no-skill baseline with the skill-assisted run. These are behavioral fixture trials, not production-project acceptance or client compatibility certification.

Temporary evaluation root: C:/Users/19603/AppData/Local/Temp/agensi-skill-eval-20261024, created fresh with two identical synthetic sets (baseline/assisted). No real Unity project or user data copied. Preserve compact results here, then remove agent-created fixtures and verify absence.

## External state

Initial in-app browser request to https://www.agensi.io/dashboard/submit failed with net::ERR_CONNECTION_CLOSED. Public pages were readable through web search in the preceding turn. No account access or upload yet.

Browser access later recovered and showed https://www.agensi.io/auth with email/password and Google sign-in; no authenticated session. User explicitly selected "先交付技能包，稍后上传". Stop external upload work for this delivery; finish the local package and validation. No account credentials, KYC, payout method, listing, or price changed.

## Completed local delivery

- Source product version 0.1.0: three Markdown files, 1,404 words. No Kingdom-specific code/tools/assets or third-party skill text shipped.
- `python -X utf8 C:/Users/19603/.codex/skills/.system/skill-creator/scripts/quick_validate.py <source-folder>`: exit 0, Skill is valid.
- Exact source allowlist and relative reference containment/existence checks: passed. All source reads UTF-8; no Kingdom name or personal absolute path in the distributable text.
- Independent no-skill baseline and skill-assisted trials both completed the Python/Web/Unity-style requests. Coordinator reran `python -B -m unittest -v`: exit 0, 5/5 per variant. `node --test`: exit 1, 1 failed per variant, intentionally reproducing duplicate notifications (2 vs 1) during review-only tasks. Web and Unity unchanged; Python changed exactly options.py and test_options.py; user notes unchanged; no added files.
- Unity results are static-only on a partial fixture. 未执行真实 Unity 编译。No EditMode/PlayMode/interaction executed. Historical XML was correctly rejected as current proof.
- Baseline also succeeded: no measured effectiveness improvement established. LISTING.md explicitly states this limitation. US$9 one-time is a trial price proposal only.
- Text reviewer found one premature completed-trial claim in draft listing; corrected during drafting and replaced with actual results after completion. No other actionable textual defects reported.
- Final ZIP: outputs/skill-products/evidence-driven-change-delivery/evidence-driven-change-delivery-v0.1.0.zip, 4,903 bytes. SHA-256 ac192c5af6574f90083a2eb4c1f414fb8e0d59c4fc1dc6c2ec94e5aa470c1726. Root SKILL.md plus references/delivery.md and references/verification.md only. ZIP CRC and byte equality with source: passed.
- Supporting evidence: VALIDATION.md, verification-results.json, trial-changes.diff, package-manifest.json. Creator materials: LISTING.md, SOURCE-NOTES.md, UPLOAD-GUIDE.zh-CN.md. None are inside the ZIP.

## Cleanup blocker

After preserving evidence, automatic approval review rejected the combined package/recursive cleanup command (`blocked by policy`). No part of that command executed. Packaging succeeded separately. A narrower cleanup then explicitly checked resolved root, reparse points, 29-file exact manifest allowlist, and removed only individual files/empty directories without recursive deletion; it too was rejected with `blocked by policy`, with no specific reason. Do not bypass the policy or claim cleanup success.

Final read-only physical scan: C:/Users/19603/AppData/Local/Temp/agensi-skill-eval-20261024 still exists with 29 files, 7,785 bytes. Agent-created synthetic data only; no real project/user data. All necessary results and changes are preserved in the product workspace. Next cleanup action: remove this exact temporary root when permitted by the host, then verify absence and update this handoff. Do not delete any sibling directory.

## Next action

User requested local delivery first. Deliver ZIP, listing, upload guide and honest validation boundaries. Later, if user resumes upload, sign in to Agensi and inspect its actual form; verify ZIP acceptance and referenced Markdown delivery, set agreed price and permitted payout method, and submit only within the user's authorization. Account creation/terms acceptance, KYC and financial onboarding may require user-operated steps. Current client loading, payout eligibility and marketplace acceptance remain untested.

## 2026-10-25: presentation and ZIP revision, v0.1.1

User requested positive public presentation and a rebuilt ZIP. Rewrote LISTING.md as customer-facing product copy focused on capabilities, example requests, included files, and tool prerequisites. Removed internal evaluation/comparison and seller-administration discussion from that public copy; did not add superiority, universal compatibility, or certification claims. Internal evidence remains preserved outside the distribution.

SKILL.md metadata 0.1.0 -> 0.1.1; opening paragraph changed to positive product wording with equivalent architecture/scope semantics. Detailed workflow and both references unchanged. Updated upload guide to point at v0.1.1. Pre-edit copies of LISTING.md, SKILL.md, package-manifest.json and upload guide saved and byte-verified under .codex/archive/agensi-packaging-20261025-v011/ with snapshot.json. Previous v0.1.0 ZIP preserved.

New ZIP: outputs/skill-products/evidence-driven-change-delivery/evidence-driven-change-delivery-v0.1.1.zip, 4,871 bytes. Exactly SKILL.md and the two references; no marketing copy, logs, internal reports, user data or history included. Updated package-manifest.json identifies this release. quick_validate.py exit 0; three-file source inventory, reference containment/existence, project/path scan, ZIP CRC and source byte equality all passed. This was a presentation/packaging change; behavioral trials were not repeated. No external upload performed. Existing cleanup blocker remains as recorded above; this task did not retry it.
