# 0019: CI runs one job per test project, and a Windows job checks the CLI

## Status

Accepted (2026-10-01, P3a spec `specs/005-editor-shell/`, research R3). Amends the job layout of
`specs/001-modernize-build/contracts/ci-workflow.md`. The new layout is
`specs/005-editor-shell/contracts/ci.md`.

## Context

P2's final review left three CI follow-ups (FU-3, FU-4 and FU-7 in `specs/004-catalog-cli/implementation-notes.md`).
P3a takes all three because it adds many Desktop E2E scenarios:

- The "Build and test (Linux)" job takes about 13 minutes. It restores and builds the whole solution, runs the repo
  checks, then runs six test projects one after another. Every implementation batch waits on it.
- `netprints show --textconv` is the git diff driver, and its output must be UTF-8. P2 set the console encoding
  explicitly (F-R10), but no test pins it. On Linux the default is already UTF-8, so the test can only fail on
  Windows.
- Constitution I requires every project to build, test and run on Windows. The development workflow keeps the
  merge gate (`CI`) Linux-only, and its only Windows exception is the future VS extension workflow (P4).

## Decision

- **Matrix.** The `build-test` job becomes three parts, all in `ci.yml`:
  - `checks`: restore, solution build, graph checks, format check, CLI smoke, sample compile and run, generated
    files unchanged, and the Desktop E2E zero-test discovery step.
  - `test`: a matrix with one leg per test project: Core, Catalog, CLI, Editor, and Editor UI (headless).
    Each leg restores and builds only its own test project's closure in Release. It runs that project with the same
    flags as today (`--report-xunit-trx`, `--coverage` with the static settings of ADR-0008, and
    `NETPRINTS_UI_ARTIFACTS` for the UI leg). It uploads `test-results-<leg>` and `coverage-<leg>`. The matrix sets
    `fail-fast: false`, so one red leg does not hide another.
  - `build-test`: keeps the name **"Build and test (Linux)"** as an aggregate gate (`needs: [checks, test]`,
    `if: always()`). It fails unless every needed job succeeded. Branch protection and any check filters that name
    it keep working.
  Each leg shares the existing NuGet cache key. No build output is passed between jobs: a per-leg build is simpler
  and still shorter than the serial run. The `e2e` and `packages` jobs are unchanged. Adding a test project means
  adding one matrix entry, and a test checks that every `tests/*.Tests` project has one.
- **Windows CLI job.** A separate workflow, `cli-windows.yml` ("CLI (Windows)", `windows-latest`), runs on pull
  requests and pushes to `master` that touch the CLI or a library it uses (`src/NetPrints.{Cli,Core,Serialization,Workspace,Generation,Catalog,Reflection,Extensibility}/**`,
  `tests/NetPrints.Cli.Tests/**`, the root build files, `global.json` and the workflow itself). It builds and runs
  `NetPrints.Cli.Tests` in full. A test that cannot run on Windows skips with a stated reason, and is not filtered
  out silently.
- **UTF-8 test.** `ShowTextconvEncodingTests` starts the built tool as a child process on a graph with non-ASCII
  class, member and node names. It asserts that standard output is valid UTF-8, has no byte order mark, and
  contains the names. It runs on every OS.
- **Constitution.** The Windows job is outside `CI`, does not replace it, and is not a merge gate by itself. That
  was a deviation from the "single exception" wording, so `plan.md` justified it under Complexity Tracking and
  proposed a PATCH amendment: other workflows may add Windows or macOS legs that extend `CI` but do not replace it.
  The amendment was applied as constitution 1.2.4 (2026-10-01).

## Consequences

- PR feedback time is set by the slowest single leg instead of the sum of all legs, at the cost of more runner
  minutes, because each leg restores and builds its own closure. The target is at most 60% of the old job's wall
  time (spec SC-006).
- Required-check names are unchanged. A future leg that should be required must also be listed under the
  aggregate's `needs`. The matrix-coverage test guards that.
- Windows regressions in the CLI (encoding, paths, process launching) become visible on the PR that causes them.
  The editor itself still runs only on Linux CI.
- The E2E diagnostics (FU-4) need no workflow change beyond uploading the diagnostics folder with the existing
  `e2e-results` artifact. They are specified in `contracts/ci.md` §3.
