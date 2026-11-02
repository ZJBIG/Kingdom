---
name: evidence-driven-change-delivery
description: Investigate and deliver scoped code changes in an existing repository, with reproducible defects, preserved local work, and verification claims tied to actual results. Use for bug fixes, small features, and behavior-preserving refactors; review-only requests remain read-only.
metadata:
  version: "0.1.1"
---

# Evidence-Driven Change Delivery

Turn coding requests into focused, reviewable changes with clear verification. Adapt to the repository's language, framework, tools, and conventions. Follow the project's architecture and the user's authorized scope throughout the workflow.

## Establish the task boundary

Read applicable repository instructions and inspect the relevant source and tests before deciding what is wrong. Discover the project root and available commands from current files; never require a particular directory layout, operating system, framework, or companion skill.

Identify whether the request permits investigation only or implementation. A review-only task permits reading and reporting, not edits, generated reports, dependency installation, or commands with unexamined write effects. Prefer stdout or a confirmed disposable workspace when verification would otherwise write into the reviewed repository.

Inspect current changes when version control is available. Preserve unrelated edits and untracked files; a dirty worktree alone is not a reason to stop. Inspect overlaps and continue where ownership is clear. Pause only affected work if conflicting edits make the intended result ambiguous. Do not discard work, terminate user processes, migrate real data, install dependencies, publish, or commit unless the user's request and environment authorize that action. Reuse authorization already given.

For a small task, proceed directly. For a task with material ambiguity, establish the intended behavior and success criteria before dependent changes. Discoverable facts are investigation work, not questions for the user.

## Find a demonstrable cause

Trace the relevant entry point, callers, state owner, and affected consumers. Distinguish a documented requirement, current implementation, observed failure, and historical report. A discrepancy alone does not establish which side needs changing.

For a defect, seek a minimal reproduction or a focused failing check before editing. Record the input, expected behavior, actual result, and environment needed to reproduce. If execution is unavailable, identify the source-supported issue and the missing runtime evidence without inventing a reproduction. Do not create a test that merely repeats the implementation.

For a feature or refactor, identify the behavior being added or preserved and the important callers or serialized interfaces it touches. Follow existing ownership and transaction boundaries rather than introducing a parallel architecture.

## Make the smallest complete change

Reuse existing capabilities. Change the shared cause when appropriate, then update affected callers, tests, fixtures, and documentation needed for the requested behavior. Avoid unrelated cleanup or speculative configuration.

Preserve public interfaces, persistent data, identifiers, and asset references unless changing them is part of the task. For operations that can partially alter state, inspect validation and failure paths, not only the successful result. Project-specific rules remain project-specific; do not impose a particular resource model, save version, UI technology, or test style.

Use the tools available in the host. Delegation is optional when independent tasks justify it and the host permits it; do not require subagents or a named API. The coordinating agent checks the final diff and evidence.

## Verify the result proportionally

Choose checks that can detect the failure or protect the changed behavior. Read [verification.md](references/verification.md) when choosing checks, interpreting failures, or dealing with unavailable tools.

Run focused checks first. Expand only when the dependency surface, a failure, or repository requirements justify it. For a trivial reversible edit, inspection may suffice. Do not install a missing tool, weaken assertions, or claim execution merely to complete a checklist.

Inspect the final diff for unintended changes and review actual outputs. If a check fails, determine whether it concerns the change, the environment, or a pre-existing issue. Stop dependent work when a failure invalidates the next step. Do not repeatedly rerun an unchanged failing command without a new hypothesis.

If you create a temporary workspace, keep it separate from product sources and existing user files. Preserve necessary evidence and authorized results, then remove only what you created and verify removal. Do not leave test fixtures inside the distributed skill or claim cleanup succeeded without checking.

## Deliver with evidence

Explain the outcome, relevant files, verification, and unresolved matters concisely. Use [delivery.md](references/delivery.md) when the change needs a structured handoff. Follow an existing project handoff convention; do not require creating a new documentation system.

Tie claims to the actual input and result. Static inspection, successful compilation, unit tests, integration tests, and user interaction establish different things. A test definition is not an executed test; a passing result file is not proof that its runner exited normally; an old report is not current verification. Report partial progress accurately when a blocker prevents completion.
