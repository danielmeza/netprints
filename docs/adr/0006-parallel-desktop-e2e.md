# 0006: Parallel desktop E2E via a worker pool, one class per scenario

## Status

Accepted (2026-09-28).

## Context

`NetPrints.Desktop.E2ETests` ran its 7 X11/GTK smoke scenarios serially, sharing one Xvfb display
and one editor process through a `[CollectionDefinition(DisableParallelization = true)]` collection
fixture. A full run took 5 m 15 s, almost all of it wall-clock spent one scenario at a time even
though the machine has spare cores. Batch D2 parallelizes the run.

xUnit's unit of parallelism is the test *collection*, not the test method: every `[Fact]` in one
class shares that class's (default) collection and always runs serially within it, regardless of
`xunit.runner.json`. The old `X11SmokeTests` had all 6 shared-scenario facts in one class, so
splitting the work required splitting the class.

## Decision

- **`DesktopWorkerPool`** (`tests/NetPrints.Desktop.E2ETests/Hosting/DesktopWorkerPool.cs`), an
  assembly fixture, owns N workers, each a private `XServer` (Xvfb + openbox) started once and
  reused for the life of the run — a test never pays for X server startup. N defaults to
  `min(ProcessorCount / 2, 4)`, overridable with `NETPRINTS_E2E_WORKERS`. `RentAsync` hands out a
  `DesktopLease` (a worker plus a *freshly started* editor process) and returns the worker to the
  pool when the lease is disposed.
- **The editor is started fresh on every `RentAsync`**, not pre-started as a spare on the returned
  worker. A pre-started spare was tried and reverted: GTK's Open/Save dialog's location-bar
  completion resolves a typed absolute path against the dialog's current folder, which for a GTK
  file chooser is fixed at the editor process's working directory. A spare started ahead of knowing
  which test will rent it — or which temp directory that test will use — can never have the right
  working directory, so the first `Return` keystroke can land on an unresolved path completion
  instead of submitting; a second `Return` does not reliably recover it either (measured). Chasing
  that with more keystrokes would hide the real cause instead of fixing it, so only the display's
  own startup cost (Xvfb, openbox) is amortized; the editor keeps starting with the correct working
  directory from the start, as it did before the pool. `GtkFileDialogs` still sends a second
  `Return` when the dialog is still open after the first, since a temp directory two levels below
  the editor's own working directory (any editor, spare or not, once other tests are running
  concurrently and no scenario's temp dir is a live ancestor of another's) can hit the same
  completion race even with a fresh editor; that keystroke is conditional on the dialog still being
  open, so it costs nothing when the first `Return` already submitted.
- **One sealed class per scenario.** The shared flows live in `SmokeScenarios` (already shared with
  the headless driver); `X11SmokeTestBase` implements the desktop-specific `StartAsync`/
  `CheckpointAsync` and rents its own lease, and each scenario gets its own sealed subclass with a
  single `[Fact]` (`EditCompileAndRunTests`, `CreateProjectTests`, `AddReferencesTests`,
  `MinimizeAndRestoreClassWindowTests`, `PanCursorTests`, `DragFromListsTests`). `ShutdownTests`
  rents its own lease directly. `AssemblyInfo.cs` no longer disables parallelization; the six
  scenario classes plus `ShutdownTests` are each their own (default) collection, and
  `xunit.runner.json` (`parallelizeTestCollections: true`, `maxParallelThreads: 8`) lets xUnit
  schedule all seven onto the pool at once.
- **No test needs a serial `[Collection]`.** Audit (owner-approved): every resource a test touches —
  the X11 clipboard, window focus, the GTK file-picker dialog, the automation pipe — lives either on
  the worker's own private display or behind that editor's own Unix socket with a random name. Two
  tests never share a display or an editor process, so none of the seven need to be pinned to a
  serial collection; the only shared state is the pool itself, which already serializes access to
  each display via its rent/return channel.
- **Per-step timing.** `StepTimer` (`Hosting/StepTimer.cs`) writes each `using (Step("name")) { ... }`
  block's duration to the test's own output and to a per-run `TestResults/e2e-timings-<run>.md`, so
  hot spots and before/after regressions can be read off after a run without re-instrumenting.
  `SmokeScenarios.Step` is a no-op hook on the headless driver and a real `StepTimer` on the desktop
  driver.

### The serial-collection-per-shared-resource rule

If a future E2E test genuinely shares something across tests that a lease does not isolate —
clipboard content asserted across two tests, a fixed TCP port, anything global to the machine rather
than to a display or an editor — give it its own `[CollectionDefinition(DisableParallelization =
true)]` collection, scoped to just that resource, and put only the tests that touch it in that
collection. Do not reach for a single serial collection for convenience: that silently serializes
every future test added to it, defeating this batch's work.

### How to add a new E2E test

1. If it is one of the shared smoke flows, add the flow to `SmokeScenarios` (so the headless driver
   gets it too) and a one-`[Fact]` sealed subclass of `X11SmokeTestBase` in `Scenarios/`, following
   the existing six.
2. If it is desktop-only (like `ShutdownTests`), write a sealed class that takes `DesktopWorkerPool`
   in its constructor, calls `pool.RentAsync(token, workDirectory)` for an `await using` lease, and
   skips with `Assert.Skip` when `DesktopWorkerPool.IsEnabled` is false.
3. Never put more than one `[Fact]` needing the pool in the same class unless those facts must run
   serially with each other — that class becomes one collection, and xUnit will not parallelize
   within it.
4. Only reach for a serial `[Collection]` if the new test shares a resource no lease isolates (see
   above); otherwise it needs none.

## Consequences

- Full E2E run: 5 m 15 s serial → ~2 m 24–39 s parallel on this machine (`NETPRINTS_E2E_WORKERS`
  default), verified green 5 times in a row; see `specs/003-core-refactor/implementation-notes.md`,
  Batch D2, for the exact runs.
- A new shared scenario costs one small sealed class, not a new collection or fixture; forgetting
  the split (adding a second `[Fact]` to an existing scenario class) silently loses parallelism for
  that pair rather than failing, so reviewers should watch for it.
- The pool's worker count is a shared ceiling: `NETPRINTS_E2E_WORKERS` above the number of scenario
  classes wastes displays, and a value of 1 degrades to the old serial behavior (still correct, just
  slow) — useful for isolating a flake.
- `LocalSdkLayout` (previously three near-identical copies in `Core.Tests`, `Testing.Ui` and a third
  helper in `Editor.Tests`) is now one implementation in `tests/NetPrints.Testing/LocalSdkLayout.cs`,
  referenced by every test assembly that builds a temp copy of a sample against the repository's own
  SDK; a future change to that local-SDK layout is now one edit, not three.
