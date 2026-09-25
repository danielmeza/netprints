# Contributing to NetPrints

This project (and any AI coding session working in it) follows the rules in
[`AGENTS.md`](AGENTS.md), which sit on top of
[`.specify/memory/constitution.md`](.specify/memory/constitution.md) and
[`.specify/memory/roadmap.md`](.specify/memory/roadmap.md); where they conflict, the constitution
wins. This document is the human-facing summary; `AGENTS.md` is authoritative.

## Spec Kit flow

Work is planned and tracked with [Spec Kit](https://github.com/github/spec-kit) under `specs/`.
Each roadmap phase (`.specify/memory/roadmap.md`) becomes one feature, worked through five
commands in order:

1. `speckit-specify` — write the feature spec (`specs/NNN-short-name/spec.md`): user stories,
   functional requirements, success criteria.
2. `speckit-clarify` — resolve open questions before planning.
3. `speckit-plan` — the implementation plan and `research.md` (design decisions, alternatives
   rejected, and why).
4. `speckit-tasks` — break the plan into concrete, independently testable tasks
   (`tasks.md`).
5. `speckit-analyze` — cross-check spec, plan and tasks for gaps before implementing.
6. `speckit-implement` — write the code and tests, one task at a time.

One phase = one spec = one branch (`NNN-short-name`) = one pull request. Governance files
(`.specify/memory/*`) are changed only by the coordinating session with the owner's approval;
propose changes in a PR description or report instead of editing them directly.

## Opening a pull request

- PRs target this fork, `danielmeza/netprints`, against `master`:
  `gh pr create --repo danielmeza/netprints --base master`. `master` is protected; merges go
  through a PR with required checks **Build and test (Linux)** and **Desktop E2E (Linux,
  Xvfb)** (the CI workflow's job names — do not rename them).
- Never open a PR against an upstream repository without the owner's explicit approval.
- CI (the `CI` workflow, Linux-only) must be green before merge.
- Every PR is reviewed by someone who did not implement it. The implementer addresses each
  finding, replies on the review thread with how it was resolved (commit SHA) or why it's
  deferred, keeps CI green, and does not merge their own PR — the owner or the coordinating
  session does.

## Code style

- An [`.editorconfig`](.editorconfig) defines formatting and style; CI runs
  `dotnet format NetPrints.slnx --verify-no-changes` as part of the build-test job. Run
  `dotnet format NetPrints.slnx` locally before pushing if it fails.
- Match the surrounding code's comment density: no long explanatory blocks between lines of
  code, at most a short one-line comment where something is genuinely non-obvious. Put
  rationale, background and design discussion in commit messages, PR descriptions or `specs/`
  docs instead — this matters most because parts of this repo are read by contributors upstream.
  XML doc comments (`///`) are the exception: they can be as detailed as needed.

## Tests

- Reproduce a bug with a test that fails before fixing it.
- Tests use xUnit v3 on Microsoft.Testing.Platform and always pass
  `TestContext.Current.CancellationToken` for cancellable async calls (`xUnit1051` is an error).
- UI tests go through page objects and `AutomationIds`, never raw control lookups, and never use
  sleeps — wait on real signals (rendered frames, dispatcher idle, the automation agent's
  `status`/`find`/`settle` calls for E2E).
- `dotnet test --solution NetPrints.slnx` covers `tests/NetPrints.Core.Tests`,
  `tests/NetPrints.Editor.Tests` and the headless `tests/NetPrints.Editor.UITests`, none of
  which need a display server. `tests/NetPrints.Desktop.E2ETests` drives the real desktop app
  over X11 and needs `NETPRINTS_E2E=1` plus the X11 tools listed in the README; it's skipped
  (not failed) otherwise.
- See [`specs/001-modernize-build/quickstart.md`](specs/001-modernize-build/quickstart.md) for
  individual test-project commands, snapshot baseline regeneration and CI artifacts.

## Working alongside other sessions

If you're running an AI coding session here, read `AGENTS.md` in full — it covers model
selection per kind of work, one-agent-per-working-tree-and-branch, `git worktree` usage for
parallel work, and cleaning up processes and displays (Xvfb, Unreal Editor) you started.

## Commits

End commit messages with the attribution line(s) your session is configured with; PR bodies end
with the "Generated with Claude Code" footer when produced by Claude Code.
