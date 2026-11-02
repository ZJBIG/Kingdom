# Evidence-Driven Code Changes

## Short description

Turn coding requests into focused, reviewable changes. Guide your AI agent through repository discovery, root-cause analysis, implementation, verification, and clear delivery.

## Description

Bring a consistent engineering workflow to AI-assisted development.

Evidence-Driven Code Changes helps your coding agent understand an existing repository, trace the behavior that matters, make a targeted change, and explain the result with clear verification. Use it for bug fixes, small features, behavior-preserving refactors, and focused code reviews.

The workflow adapts to your project's language, framework, directory layout, tools, and conventions. It keeps attention on the requested outcome while preserving existing work and important interfaces.

## Key capabilities

- **Repository-aware investigation:** Discover relevant instructions, source files, tests, entry points, and callers.
- **Root-cause analysis:** Connect the reported issue to a reproducible failure or source-supported explanation.
- **Focused implementation:** Reuse existing capabilities and update the necessary callers, tests, and documentation.
- **Proportional verification:** Choose checks suited to the changed behavior and inspect their actual results.
- **Clear delivery:** Explain what changed, why it changed, and how it was checked.
- **Review-only support:** Produce actionable findings while preserving the reviewed files.

## Example requests

- "Fix the CLI bug where a configured limit of zero is ignored. Preserve my existing edits and verify the result."
- "Review this component's selection handler and explain why notifications duplicate after reattaching."
- "Refactor this function while preserving its public behavior and updating the relevant tests."
- "Update this serialized field while preserving existing asset references."

## What's included

- `SKILL.md`: the complete investigation-to-delivery workflow.
- `references/verification.md`: guidance for selecting and interpreting verification checks.
- `references/delivery.md`: a concise structure for reviewable results and handoffs.

## Setup

Use with a coding agent that can read the skill and its references and access your repository through file and command tools. The workflow uses your project's existing toolchain and conventions.
