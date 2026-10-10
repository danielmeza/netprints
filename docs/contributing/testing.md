# Testing

## Test projects

| Project | Covers |
|---|---|
| `NetPrints.Core.Tests` | The model, serialization, translation and the repository rules (`SourceHygieneTests`, golden fixtures). |
| `NetPrints.Catalog.Tests` | The type catalog and its annotations. |
| `NetPrints.Cli.Tests` | The `netprints` command line, in process (`CliTestHost`); some tests run the real tool or the real project system as child processes. |
| `NetPrints.Editor.Tests` | Editor view models and hosting services, over fakes (no window). |
| `NetPrints.Editor.UITests` | The editor's views, headless (Avalonia headless platform), through page objects and `AutomationIds`. |
| `NetPrints.Desktop.E2ETests` | The real desktop editor on a private X11 display (Linux only). |
| `NetPrints.Testing`, `NetPrints.Testing.Ui` | Shared fixtures, the driver abstraction, page objects and the Screenplay layer; not tests themselves. |

## Running them

Build quietly, then run without building again (the solution selects the Microsoft.Testing.Platform runner):

```bash
dotnet build -v q -tl:off --nologo
dotnet test --no-build --no-progress --no-ansi
```

Run one class with the test assembly itself (`--treenode-filter` is not accepted):

```bash
tests/NetPrints.Editor.Tests/bin/Debug/net10.0/NetPrints.Editor.Tests -class '*RunStateTrackerTests'
```

The full suite is what CI's `test` job runs, and the format check is part of the gate:

```bash
dotnet test --solution NetPrints.slnx -c Release --no-build --no-progress --no-ansi -- --ignore-exit-code 8
dotnet format NetPrints.slnx --verify-no-changes
```

Never run the whole `NetPrints.Editor.UITests` project under Xvfb (it runs out of memory); the headless tests need no display.

## Desktop E2E tests

They need Linux with Xvfb, openbox, xdotool, ImageMagick and GTK 3, and run only with `NETPRINTS_E2E=1`; without it the scenarios skip and only the always-on tests (failure capture, entry point, worker pool) run; the exit code is 0.
Each test rents a worker from a pool (a private Xvfb display with openbox) and starts a fresh editor on it
([ADR-0006](../adr/0006-parallel-desktop-e2e.md)). The pool size is `NETPRINTS_E2E_WORKERS` (default `min(cores / 2, 4)`).
If `:100` is taken, set `NETPRINTS_E2E_DISPLAY_START`. Never use `DISPLAY=:1`.

```bash
dotnet build tests/NetPrints.Desktop.E2ETests -c Release
NETPRINTS_E2E=1 dotnet test --project tests/NetPrints.Desktop.E2ETests -c Release --no-build --no-progress --no-ansi -- --fail-skips on
```

### Diagnostics of a failed scenario

When a scenario fails (an exception, an assertion, the test's own time budget running out, or the editor exiting
while the test holds it), the harness captures the state before it reports. The test then fails with an
`E2EStepFailureException`: its message starts with `[step '<name>' running for <n> s]` and its `InnerException` is the
original failure, unchanged.

The files are written to `TestResults/e2e-diagnostics/<TestClass>/` and, on CI, uploaded with the `e2e-results` artifact:

| File | Content |
|---|---|
| `summary.md` | The failure kind, the test, the step that was running and for how long, the worker display, the editor PID and the UTC time. |
| `timings.md` | Every step with its duration; the step that was open is marked `(running)`. |
| `ui-tree.json` | For each editor window, every control with an automation id: `automationId`, `name`, `type`, `bounds`, `isVisible`, `isEnabled`, `hasFocus`; the top-level `focused` names the focused control. If the editor is gone it holds its exit code instead. |
| `display.png` | The whole display (root window), including platform dialogs. |
| `editor.log` | The last 400 lines the editor logged, then the tail of its stderr. |
| `process.txt` | `running`, or `exited <code>`. |
| `run-state.json` | The last launched program: `phase` (`notStarted`, `building`, `running`, `exited`), `exitCode` (present only once the program has exited), and the last 200 lines of `stdout` and `stderr`. It tells a slow run from lost output. |
| `capture-errors.txt` | Only when a part could not be captured: which one and why. |

Each part has 10 seconds and the whole capture 30, so a hung editor cannot block it.

### Forcing a timeout

`E2EDiagnosticsTests` proves the capture by holding the `open project` step until its 20 s budget runs out, through a
hook of its own class (`ForcedTimeoutStep`); it never sets an environment variable, so parallel workers are unaffected.
To see the diagnostics of any scenario locally, name the step in `NETPRINTS_E2E_FORCE_TIMEOUT` for a run of one class:

```bash
NETPRINTS_E2E=1 NETPRINTS_E2E_FORCE_TIMEOUT='run' tests/NetPrints.Desktop.E2ETests/bin/Release/net10.0/NetPrints.Desktop.E2ETests -class '*EditCompileAndRunTests'
```

The step names are the `using (Step("..."))` blocks of the scenarios. The step is held at its next checkpoint, so name
one that records a checkpoint (`start`, `open project`, `edit graph`, `run`, `create project`, `add references`). `compile` and the nested
`wait for worker` (timed inside `start`) have none, and forcing a step that was never held fails the scenario loudly. The variable is for manual runs only: set for a whole
run, it would hold that step in every test.

## Continuous integration

Two workflows run tests ([ADR-0019](../adr/0019-ci-test-matrix-and-windows-cli-leg.md)).

### `CI` (`.github/workflows/ci.yml`)

Runs on every pull request to `master`, every push to `master` and on demand.

| Job | Check name | What it does |
|---|---|---|
| `checks` | Repository checks (Linux) | Builds `NetPrints.slnx` in Release, runs the graph checks (`regen --check`, `format --check`), `dotnet format --verify-no-changes`, the Desktop E2E smoke step (no display, always-on tests only), the CLI smoke and the sample compile and run, and fails on any changed generated file. |
| `test` | Test (Core), Test (Catalog), Test (CLI), Test (Editor), Test (Editor UI (headless)) | One matrix leg per test project, run in parallel (`fail-fast: false`). Each leg builds only its own project in Release and runs it with `--report-xunit-trx` and static code coverage ([ADR-0008](../adr/0008-static-only-code-coverage.md)). |
| `build-test` | **Build and test (Linux)** | The aggregate. It runs even when a job it needs failed and fails unless `checks` and every `test` leg succeeded. |
| `e2e` | **Desktop E2E (Linux, Xvfb)** | The Desktop E2E project on Xvfb with `NETPRINTS_E2E=1` and `--fail-skips on`. |
| `packages`, `desktop-publish`, `jsonschema` | Packages (local feed), Self-contained editor smoke (linux-x64), JSON Schema (metaschema, lint, validate) | Packaging and schema checks; they run no tests. |

Branch protection requires two checks, `Build and test (Linux)` and `Desktop E2E (Linux, Xvfb)`. Do not rename them.
A new test leg is covered by the aggregate without touching branch protection.

Artifacts, uploaded even when a job fails:

| Artifact | From | Content |
|---|---|---|
| `test-results-<leg>` | each `test` leg (`core`, `catalog`, `cli`, `editor`, `editor-ui`) | The `.trx` results of that project. |
| `coverage-<leg>` | each `test` leg | Cobertura coverage of that project. |
| `ui-headless` | the `editor-ui` leg | Snapshot actual and diff images, flow screenshots and per-test diagnostics. |
| `e2e-results` | `e2e` | The `.trx` results, `ui/`, `e2e-timings-*.md` and `e2e-diagnostics/` (see [Diagnostics of a failed scenario](#diagnostics-of-a-failed-scenario)). |

### `Performance budgets (non-blocking)` (the `perf` job in `ci.yml`)

Runs the tests with `[Trait("Category", "Performance")]` of `tests/NetPrints.Editor.Tests` with
`NETPRINTS_PERF_STRICT=1`, which switches `ProjectOpenPerformanceTests` from its sanity bound to the SC-005 strict gate
(3x the spec target, best of three restored runs). It is not a required check and is not in the `build-test` aggregate.
The blocking `Editor` leg runs the same tests with the sanity bound only, so a slow shared runner cannot fail a pull
request, while an order-of-magnitude regression still does.

### `CLI (Windows)` (`.github/workflows/cli-windows.yml`)

Builds `tests/NetPrints.Cli.Tests` and runs it on `windows-latest`, and uploads `test-results-cli-windows`. It runs on
pull requests and pushes to `master` that touch the CLI, a project in its transitive `ProjectReference` closure, the
test fixtures or the build files (the path filter), and on demand. It is not a required check.
`CiWorkflowTests.CliWindowsPathFilterCoversTheCliTestsBuildInputs` fails when a project joins that closure without a
path entry.

A CLI test that cannot run on Windows calls `Assert.Skip("<what is missing>")` behind an OS check (for example a POSIX
file mode). `ShowTextconvEncodingTests` never skips: it pins `show --textconv` to UTF-8 without a byte order mark,
which the git diff driver needs.

### Adding a test project

1. Create `tests/<Name>.Tests` (or `.UITests`) and add it to `NetPrints.slnx`.
2. Add a row to the `test` matrix in `ci.yml` with the `name` (the check name), the `leg` (the artifact slug) and the
   `project`. `CiWorkflowTests` fails while a project under `tests/` has no row, has two, or a row names a missing
   project. The Desktop E2E project is the one exception: it runs in `e2e`.
3. If the project joins the CLI tests' closure, add its folder to the `paths` of both triggers in `cli-windows.yml`.
4. Add the project to the table at the top of this page.
