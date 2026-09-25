# 0001: Repository layout

## Status

Accepted (2026-09-25).

## Context

The repository grew from a single WPF app (`NetPrints`, `NetPrintsEditor`, `NetPrintsVSIX`) into a
multi-project solution (P0: `NetPrints` (core), `NetPrints.Reflection`, the Avalonia
`NetPrints.Editor`/`NetPrints.Desktop`, `NetPrintsCLI`, and four test projects) with everything
flat at the repository root next to `samples/`, `docs/`, `specs/` and the governance files. That
made it hard to tell library code from test code from the abandoned Visual Studio extension at a
glance, gave every project the same nesting depth regardless of role, and put `git status`/IDE
project pickers in one long undifferentiated list.

## Decision

Group projects by role under three top-level directories, leave `samples/`, `docs/`, `specs/` and
the governance/config files (`.specify/`, `AGENTS.md`, `.github/`) at the root:

- `src/` — shipping code:
  - `NetPrints/` → `src/NetPrints.Core/` (csproj renamed `NetPrints.csproj` → `NetPrints.Core.csproj`)
  - `NetPrints.Reflection/` → `src/NetPrints.Reflection/`
  - `NetPrints.Editor/` → `src/NetPrints.Editor/`
  - `NetPrints.Desktop/` → `src/NetPrints.Desktop/`
  - `NetPrintsCLI/` → `src/NetPrints.Cli/` (csproj renamed `NetPrintsCLI.csproj` → `NetPrints.Cli.csproj`)
- `tests/` — everything that only exists to test `src/`:
  - `NetPrintsUnitTests/` → `tests/NetPrints.Core.Tests/` (csproj renamed `NetPrintsUnitTests.csproj` → `NetPrints.Core.Tests.csproj`)
  - `NetPrints.Editor.Tests/` → `tests/NetPrints.Editor.Tests/`
  - `NetPrints.Editor.UITests/` → `tests/NetPrints.Editor.UITests/`
  - `NetPrints.Desktop.E2ETests/` → `tests/NetPrints.Desktop.E2ETests/`
  - `NetPrints.Testing.Ui/` → `tests/NetPrints.Testing.Ui/` (shared test infrastructure, not a test
    project itself, but it only exists to serve the ones above)
- `legacy/` — code kept for reference that is not built or tested by CI:
  - `NetPrintsVSIX/` → `legacy/NetPrintsVSIX/` (stays out of the solution)

Assembly names follow the renamed projects (`NetPrints.Core`, `NetPrints.Cli`,
`NetPrints.Core.Tests`); **namespaces are unchanged** (`NetPrints`, `NetPrints.Core`,
`NetPrintsCLI`, etc. as they were) to keep this a pure layout/build change with no source-level
churn. `NetPrints.Reflection`, `NetPrints.Editor`, `NetPrints.Desktop` and the other test projects
keep their existing project/assembly names — only the three renamed above collided with the
`NetPrints.*` project-naming convention used by everything added since (Editor, Desktop, the test
projects) closely enough to justify the rename.

The solution moved from `NetPrints.sln` to `NetPrints.slnx` with `src`/`tests` solution folders
mirroring the disk layout, and `legacy/NetPrintsVSIX` stays out of it, as it was in `NetPrints.sln`
before this change.

## Consequences

- Every `ProjectReference`, `InternalsVisibleTo`, CI path, and relative path a test project uses to
  find `samples/`, snapshot baselines or the desktop app needed updating in the same change (done
  as a separate, reference-only commit so the move commit is pure `git mv` and preserves
  `git log --follow` / blame).
- `dotnet build`/`dotnet test` at the root now point at `NetPrints.slnx`, not `NetPrints.sln`.
- IDEs and scripts that hard-coded the old top-level project paths (`NetPrints/`, `NetPrintsCLI/`,
  `NetPrintsUnitTests/`, ...) need the new `src/`/`tests/` paths; every in-repo doc, spec and
  script reference was updated alongside this ADR.
- `legacy/NetPrintsVSIX` remains buildable only outside CI (if at all); it already referenced a
  deleted `NetPrintsEditor` project before this move and was not part of `NetPrints.sln`, so it
  stays a pure reference snapshot.
