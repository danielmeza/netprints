# Contract: CI layout and E2E diagnostics

ADR-0019 and FR-001 to FR-006. This contract supersedes the job layout in
`specs/001-modernize-build/contracts/ci-workflow.md`. The workflow name `CI`, its triggers, its permissions and its
concurrency group are unchanged.

## 1. `ci.yml` jobs

| Job id | Name | Needs | Content |
|---|---|---|---|
| `checks` | Repository checks (Linux) | — | restore and build `NetPrints.slnx` (Release); graph checks (`regen --check`, `format --check`); `dotnet format --verify-no-changes`; the Desktop E2E smoke step (no display, always-on tests only; `--ignore-exit-code 8`); CLI smoke; sample compile and run; generated files unchanged |
| `test` | Test (`${{ matrix.name }}`) | — | matrix: `Core`, `Catalog`, `CLI`, `Editor`, `Editor UI (headless)`; each leg restores and builds `tests/<project>` in Release, then runs it with `--report-xunit-trx`, `--coverage`, the ADR-0008 settings and the leg's environment (`NETPRINTS_UI_ARTIFACTS` for the UI leg); uploads `test-results-<leg>` (and `ui-headless` for the UI leg) and `coverage-<leg>`; `fail-fast: false` |
| `build-test` | **Build and test (Linux)** | `checks`, `test` | `if: always()`; fails unless every needed job succeeded; no other steps |
| `e2e` | Desktop E2E (Linux, Xvfb) | — | unchanged, plus the diagnostics folder (§3) inside the uploaded `e2e-results` |
| `packages` | Packages (local feed) | — | unchanged |

- Every job uses the existing NuGet cache key and `actions/*` pins, and keeps `timeout-minutes: 30` (`packages`:
  20).
- **Matrix coverage test.** `CiWorkflowTests` in `NetPrints.Core.Tests` (next to the other repository hygiene tests)
  parses `ci.yml`. It asserts that every test project under `tests/` whose name ends in `.Tests` or `.UITests`
  appears in the matrix exactly once, and that `build-test` needs every other test-running job except `e2e`,
  `packages` and itself. `NetPrints.Desktop.E2ETests` is covered by the `e2e` job.

## 2. `cli-windows.yml`

- `name: CLI (Windows)`. It runs on `windows-latest` with `timeout-minutes: 30`.
- Triggers: `pull_request` and `push` on `master`, path-filtered to the closure plus the data and build
  files the CLI tests read: every project in the transitive `ProjectReference` closure of `tests/NetPrints.Cli.Tests`
  (`CiWorkflowTests` checks this), which on 2026-10-01 is:
  - the CLI and its libraries: `src/NetPrints.Cli/**`, `src/NetPrints.Core/**`, `src/NetPrints.Serialization/**`,
    `src/NetPrints.Workspace/**`, `src/NetPrints.Generation/**`, `src/NetPrints.Catalog/**`,
    `src/NetPrints.Reflection/**`, `src/NetPrints.Extensibility/**`, `src/NetPrints.Generator/**`;
  - `tests/NetPrints.Cli.Tests/**`, `tests/NetPrints.Testing/**`, `tests/NetPrints.TestExtension/**` and
    `tests/Fixtures/**`;
  - the data and build files the CLI tests read: `samples/**`, `schemas/**`, `eng/schemastore/**`,
    `src/NetPrints.Sdk/**`, `.gitattributes`;
  - the build files: `Directory.*`, `src/Directory.Build.props`, `src/BannedSymbols*.txt`,
    `tests/Directory.Build.props`, `global.json`, `NuGet.config`;
  - `.github/workflows/cli-windows.yml`.
- `workflow_dispatch` is also allowed.
- Steps: checkout (`fetch-depth: 0`), setup-dotnet from `global.json`, NuGet cache, then
  `dotnet build tests/NetPrints.Cli.Tests -c Release`, then
  `dotnet test --project tests/NetPrints.Cli.Tests -c Release --no-build --report-xunit-trx --results-directory TestResults`.
  The results are uploaded as `test-results-cli-windows`.
- A test that cannot run on Windows calls `Assert.Skip("<reason>")` behind an OS check. The reason must name what
  is missing. Example: a POSIX file mode.
- **`ShowTextconvEncodingTests`:**
  - It writes a temporary graph whose class is `Grüße`, with a method `Größe` and a node whose string pin value contains `日本語`.
  - It runs the built `netprints` as a child process, `show --textconv <file>`, with stdout redirected as bytes.
  - It asserts that the bytes are valid UTF-8, do not start with `EF BB BF`, and decode to text that contains the
    three names.
  - The exit code must be 0.

## 3. Desktop E2E diagnostics

**Triggers.** An exception or assertion failure in a scenario, the per-test `CancelAfter` timeout (ADR-0006), or
the editor process exiting while the test still holds its lease.

**Output folder.** `TestResults/e2e-diagnostics/<TestClass>/`:

| File | Content |
|---|---|
| `summary.md` | the failure kind, the test, the running step and its elapsed time, the worker display, the editor PID, and the UTC time |
| `timings.md` | the `StepTimer` entries for this test, in order, with durations; the open step is marked `(running)` |
| `ui-tree.json` | for each editor window, its automation tree: `automationId`, `name`, `type`, `bounds`, `isVisible`, `isEnabled`, `hasFocus`; the top-level `focused` names the focused element |
| `display.png` | a capture of the whole Xvfb display (root window) |
| `editor.log` | the last 400 lines of the editor's log, then its stderr tail |
| `process.txt` | `running`, or `exited <code>` |
| `run-state.json` | asked from the editor through the automation pipe: the last launched program's state (`notStarted`, `building`, `running`, `exited`), exit code, and stdout and stderr tails (last 200 lines each), so a slow run can be told apart from lost output (issue #11) |
| `capture-errors.txt` | only when a part could not be captured: which part failed, and why |

**Rules.**

- Each part runs with its own 10 s limit, so a hung editor cannot block the capture.
- The total capture time is at most 30 s.
- After the capture, the test fails with an `E2EStepFailureException` whose message starts with
  `[step '<name>' running for <n> s]` and whose `InnerException` is the original failure, unchanged. If the capture
  itself fails, the original failure is still the one reported.
- A per-test harness hook makes a named step wait until it is cancelled. `E2EDiagnosticsTests` uses it, for its own
  test only, to prove all seven files are produced; it never sets a process-wide variable, so parallel workers are
  unaffected. For a manual run of one class, `NETPRINTS_E2E_FORCE_TIMEOUT=<step name>` does the same. Both run only
  with `NETPRINTS_E2E=1`, like every other E2E test.
- The `e2e` job already uploads `TestResults/` on every run (`if: always()`), so the diagnostics need no workflow
  change. The artifact name stays `e2e-results`.
