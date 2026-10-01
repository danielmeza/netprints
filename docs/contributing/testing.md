# Testing

## Test projects

| Project | Covers |
|---|---|
| `NetPrints.Core.Tests` | The model, serialization, translation and the repository rules (`SourceHygieneTests`, golden fixtures). |
| `NetPrints.Catalog.Tests` | The type catalog and its annotations. |
| `NetPrints.Cli.Tests` | The `netprints` command line, against a fake host. |
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

They need Linux with Xvfb, openbox, xdotool, ImageMagick and GTK 3, and run only with `NETPRINTS_E2E=1`; without it every test skips (exit code 8).
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
| `run-state.json` | The last launched program: `phase` (`notStarted`, `building`, `running`, `exited`), `exitCode`, and the last 200 lines of `stdout` and `stderr`. It tells a slow run from lost output. |
| `capture-errors.txt` | Only when a part could not be captured: which one and why. |

Each part has 10 seconds and the whole capture 30, so a hung editor cannot block it.

### Forcing a timeout

`E2EDiagnosticsTests` proves the capture by holding the `open project` step until its 20 s budget runs out, through a
hook of its own class (`ForcedTimeoutStep`); it never sets an environment variable, so parallel workers are unaffected.
To see the diagnostics of any scenario locally, name the step in `NETPRINTS_E2E_FORCE_TIMEOUT` for a run of one class:

```bash
NETPRINTS_E2E=1 NETPRINTS_E2E_FORCE_TIMEOUT='run' tests/NetPrints.Desktop.E2ETests/bin/Release/net10.0/NetPrints.Desktop.E2ETests -class '*EditCompileAndRunTests'
```

The step names are the `using (Step("..."))` blocks of `SmokeScenarios`. The step is held at its next checkpoint, so name
one that records a checkpoint (`start`, `open project`, `edit graph`, `run`). The variable is for manual runs only: set for a whole
run, it would hold that step in every test.
