# Contributing to NetPrints

This is a fork of [RobinKa/netprints](https://github.com/RobinKa/netprints) under active
modernization (Avalonia editor, .NET 10, Linux-first). It follows a phased roadmap driven by
[Spec Kit](https://github.com/github/spec-kit) and the rules in [`AGENTS.md`](AGENTS.md), which
every contributor — human or AI agent — follows.

## Before you start

- Read [`AGENTS.md`](AGENTS.md), the [constitution](.specify/memory/constitution.md) and the
  [roadmap](.specify/memory/roadmap.md). The constitution's principles (Linux-first, UI-agnostic
  core, extension points instead of forks, `net10.0` only, tests gate every change) are
  non-negotiable; the roadmap says what phase is next.
- For anything beyond a small fix, open an issue or check the roadmap first — phases are
  sequenced on purpose, and work belonging to a later phase is deferred rather than folded in.

## Spec Kit workflow

Every roadmap phase is one Spec Kit feature, one branch, one PR:

```
speckit-specify → speckit-clarify → speckit-plan (+ research.md) → speckit-tasks → speckit-analyze → speckit-implement
```

- Branches follow `NNN-short-name`; the spec, plan, research and tasks live in
  `specs/NNN-short-name/`.
- `speckit-implement` is where code is written, against the acceptance criteria `speckit-tasks`
  produced.
- Governance files (`.specify/memory/constitution.md`, `.specify/memory/roadmap.md`) are edited
  only by the coordinating session with the owner's approval; everyone else proposes changes to
  them in a PR description instead of editing directly.

## Pull requests and review

- PRs target `danielmeza/netprints:master` — never the upstream `RobinKa/netprints` repository,
  and never without the owner's explicit approval for anything upstream-facing.
- Every PR is reviewed by someone other than its author (or, for AI agents, a separate review
  session that did not implement it). The author addresses each finding in code and replies on
  the thread with the resolving commit SHA, or a reasoned "deferred" with a follow-up.
- CI (the `CI` workflow, Linux-only, `ubuntu-latest`) must be green before merge: build, every
  test suite headless, and the `Desktop E2E (Linux, Xvfb)` job. Neither job is skipped or made
  non-required to get a PR through.
- The PR author does not merge their own PR; the owner or coordinating session does, once review
  and CI are both clear.

## Building, testing and running

See the [README](README.md#build-and-test) for the day-to-day commands
(`dotnet build`/`test`/`run`) and the [P0 quickstart](specs/001-modernize-build/quickstart.md)
for the full walkthrough, including headless UI tests and the desktop E2E suite.

Before opening a PR:

```bash
dotnet build NetPrints.slnx -c Release
dotnet test --solution NetPrints.slnx -c Release --no-build
dotnet format NetPrints.slnx --verify-no-changes
```

`dotnet format` fixes itself: run `dotnet format NetPrints.slnx` (no `--verify-no-changes`) to
apply whatever it would otherwise flag in CI.

## Test conventions

- Reproduce a bug with a test that fails before fixing it.
- Tests use xUnit v3 on Microsoft.Testing.Platform. Every async test method takes and passes
  `TestContext.Current.CancellationToken` (enforced as an error, `xUnit1051`).
- UI tests (`NetPrints.Editor.UITests`, `NetPrints.Desktop.E2ETests`) drive the app through page
  objects and `AutomationIds`, never raw coordinates, and never `Thread.Sleep`/fixed delays —
  wait on a condition instead.
- A behavior-preserving refactor keeps existing tests green; where generated code or a
  serialized document could silently drift, add a characterization or snapshot test.
- New code that isn't UI or a thin host shim needs coverage; the `build-test` CI job publishes a
  Cobertura report as the `coverage` artifact.

## Code style

- `.editorconfig` plus `dotnet format` is the source of truth for formatting; don't hand-tune
  whitespace the formatter would change.
- Comment density matches the surrounding code: a short one-line comment only where something is
  genuinely non-obvious, no explanatory blocks between lines. Rationale and design discussion
  belong in the commit message, the PR description or `specs/`, not in code comments — this
  matters most here, since this repo is contributed back upstream. XML doc comments (`///`) are
  the exception and may be as detailed as needed.
- Commit messages end with the attribution line(s) your session is configured with; PR
  descriptions end with the "Generated with Claude Code" footer when written by Claude Code.

## Getting help

Open an issue for bugs or feature suggestions. For anything else, see the
[credits](README.md#credits-and-license) in the README.
