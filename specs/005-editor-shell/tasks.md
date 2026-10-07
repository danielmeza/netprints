---
description: "Task list for P3a — Editor shell"
---

# Tasks: Editor Shell

**Input**: `specs/005-editor-shell/` — plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Start condition**: met when batch S2 (this file, the speckit-analyze fixes and constitution 1.2.4) is on draft PR #12
and CI is green. Branch `005-editor-shell` from `master` at `3a6eafc` (the P2 merge, released as `v0.2.0`). ADR-0018,
ADR-0019 and ADR-0020 are committed with this spec.

**Tests**: REQUIRED (constitution V). A behaviour task names its test class first. "Test first" means: write the
test, run it, see it fail for the expected reason (red), then implement until it passes (green), in the same batch;
record both runs in implementation-notes.md; never push a red test. A test written after the code is reported as such.
Every test is xUnit v3 and passes `TestContext.Current.CancellationToken`. UI tests go through page objects and
`AutomationIds`, with no sleeps. Desktop E2E scenarios are one class each (ADR-0006) and, from batch D2 on, set
`NETPRINTS_STATE_DIR` per lease. Snapshots and goldens change only with `NETPRINTS_UPDATE_SNAPSHOTS=1`, reviewed by
hand, and the commit says why.

**Rules for the implementer** (plan.md "Standing constraints" and AGENTS.md "Batch rules"): one batch per agent;
read only the task text, the contract sections it names and the code you touch; load the `avalonia-xaml`,
`avalonia-behaviors` and `avalonia-styling` skills before touching `.axaml`/`.axaml.cs`; build quietly
(`dotnet build -v q -tl:off --nologo`), test after building (`dotnet test --no-build --no-progress --no-ansi`), filter
while iterating, whole suite once at the end of the batch
(`dotnet test --solution NetPrints.slnx -c Release --no-build --no-progress --no-ansi -- --ignore-exit-code 8`) plus the
Desktop E2E run (AGENTS.md two-run split), then `dotnet build -c Release` (0 warnings) and
`dotnet format NetPrints.slnx --verify-no-changes`. No `!`, no suppressions, XML docs on public API, and
`PublicAPI.Unshipped.txt` updated with every public API change of a tracked library (Core, Reflection, Serialization,
Extensibility, Catalog; `NetPrints.Editor` is not tracked). View model types end in `ViewModel` (FR-105). View models
hold no Avalonia or Dock types, and Dock types appear only in `NetPrints.Editor.Shell.Docking`. Every identifier used in
more than one place (command, panel, tile and template ids, document id prefixes, automation ids, state file names,
environment variable names) is a named constant. Commit per task (pathspec of your own files), tick the task here,
write decisions to `specs/005-editor-shell/implementation-notes.md` as "Decision: …", push, and wait for your own CI.
Never leave changes under `samples/`. No publishing (tags, NuGet, wiki, upstream repos). Every batch must pass Linux CI
(and `CLI (Windows)` when it runs) before the next starts.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel with other [P] tasks of the same batch (different files, no dependency).
- **[Story]**: US1–US10 from spec.md. Cross-cutting and polish tasks have no story label.
- **Batch header**: `Batch <id> — model: <haiku|sonnet|opus> — T<first>–T<last> — <n> units`; one agent per batch,
  batches run in order.
- **Batch sizing** (owner rule, plan.md "Workflow"): an implementation batch carries 2–4 related tasks, or up to 6 when
  they are mechanical, for 3–6 units; a task is 1 unit, and a design-heavy task (the spike, the shell adapter, the
  registry, the lifecycle services, a large test set) is 2. haiku runs only mechanical work; release notes and guide
  text that make factual claims go to sonnet. If a haiku batch cannot get the build or the suite green, the
  coordinator re-runs it on sonnet.
- **Checkpoints**: the last task of each sub-phase's implementation batches is its checkpoint: the whole suite plus the
  E2E run, CI green, and a report in implementation-notes.md with evidence (test names, CI run ids, artifact names,
  timings) for every success criterion the sub-phase covers, plus a "Docs updated:" line listing the pages changed. A
  sub-phase is not done until its docs are updated; each user-visible change also gets an entry under "Unreleased" in
  `.github/release-notes.md`.
- **Reviews** (owner rule): every sub-phase ends with an Opus review of its whole diff (`<X>-R`) by an agent that did
  not implement it, then a reserved sonnet fix batch (`<X>-F`) that must be green on CI before the next sub-phase
  starts. Sub-phase H ends with one final, lighter Opus review of the whole PR (H-R), its fix batch (H-F) and the merge
  preparation (H-M); the owner or the coordinator merges.

---

## Phase 1: Naming, CI and test infrastructure (sub-phase A, User Story 1 and FR-105, Priority P1) 🎯 MVP

**Goal**: every view model type ends in `ViewModel` before any new P3a code (FR-105); Desktop E2E failures explain
themselves (FR-001–FR-003); the Linux tests run as a matrix under the unchanged "Build and test (Linux)" check
(FR-004); a Windows workflow runs the CLI tests, including the UTF-8 `show --textconv` test (FR-005, FR-006).
Contract: contracts/ci.md. **Independent test**: US1's independent test; SC-006; `NoTypeNameEndsInVM` green.

### Batch A1 — model: haiku — T001–T002 — 3 units

- [x] T001 View model rename, committed on its own before any other P3a code (FR-105, research R16, ADR-0007
  amendment; 2 units). Test first: add `NoTypeNameEndsInVM` to `tests/NetPrints.Core.Tests/Core/SourceHygieneTests.cs`.
  It scans every class, record, struct and interface declaration, generic ones included (`DialogVM<TResult>`), in
  `src/**/*.cs` and `tests/**/*.cs` (not `bin/`, `obj/`), and fails on any type name ending in `VM`, listing each. Run it
  and see it list the 25 types of the 2026-10-01 survey, all in `src/NetPrints.Editor/`: `ClassEditorVM`, `CodeViewVM`,
  `ConnectionVM`, `DeclaredReferenceVM`, `DiagnosticRowVM`, `DialogVM`, `ErrorDialogVM`, `ErrorListVM`, `EventGraphVM`,
  `GetSetChooserVM`, `IssuesDialogVM`, `LocalVariableVM`, `MainEditorVM`, `MemberVariableVM`, `MethodVM`,
  `NodeGraphVM`, `NodePinVM`, `NodeVM`, `PinRowVM`, `ReferenceListVM`, `SelectMethodDialogVM`, `SelectTypeDialogVM`,
  `SuggestionListVM`, `TrustDialogVM`, `VariablesPanelVM`. Then rename each `<Name>VM` to `<Name>ViewModel`: the type,
  its file (`git mv`), and every reference in `src/` and `tests/` (`.cs`; `.axaml` `x:DataType`, bindings and casts;
  page objects; test class and method names that embed the type name; locals and members named after the type). No
  rename collides with an existing type (checked 2026-10-01); a collision found later is resolved case by case and
  recorded as a Decision. Build, the whole suite and the E2E run green; one commit.
- [x] T002 Docs sweep for the rename: `AGENTS.md` (MVVM section, one line: view model types are named
  `<Name>ViewModel`, never `<Name>VM`, enforced by `SourceHygieneTests.NoTypeNameEndsInVM`); `.claude/skills/avalonia-*/`
  (every old type name, plus the rule in `avalonia-xaml`); `docs/adr/0007-xaml-practices.md` (the old names below its
  2026-10-01 amendment, as the amendment says); `docs/guide/` and `docs/contributing/`. Historical records
  (`specs/001-*` to `specs/004-*` and their implementation notes) are not rewritten (Decision). Afterwards
  `git grep -nE '\b[A-Z][A-Za-z]+VM\b' -- src tests docs .claude AGENTS.md` finds only `MVVM`.

### Batch A2 — model: sonnet — T003–T006 — 5 units

- [x] T003 [US1] Test first (contracts/ci.md §3). `tests/NetPrints.Desktop.E2ETests/Hosting/FailureCaptureTests.cs`:
  plain facts that need no display (like `DesktopWorkerPoolTests`), over a `FailureCapture` with fake parts and a fake
  `TimeProvider`. A part that throws is named in `capture-errors.txt`, and the other parts are still written. A part
  that hangs is abandoned after 10 s, and the whole capture ends within 30 s. The reported failure is an
  `E2EStepFailureException` whose message starts with `[step '<name>' running for <n> s]` and whose `InnerException` is
  the original failure, unchanged. A failure of the capture itself never replaces the original.
  `tests/NetPrints.Desktop.E2ETests/Scenarios/E2EDiagnosticsTests.cs` (one class, ADR-0006): forces a timeout at a named
  step of a short scenario through the harness, for its own test only (never through the process environment, so
  parallel workers are unaffected), expects the scenario to fail, then asserts that
  `TestResults/e2e-diagnostics/E2EDiagnosticsTests/` holds `summary.md`, `timings.md` (the open step marked
  `(running)`), `ui-tree.json` (parses; a top-level `focused`, and per element `automationId`, `name`, `type`, `bounds`,
  `isVisible`, `isEnabled`, `hasFocus`), `display.png` (a non-empty PNG), `editor.log`, `process.txt` (`running`) and
  `run-state.json`, and no `capture-errors.txt`. Both red.
- [x] T004 [US1] `tests/NetPrints.Desktop.E2ETests/Hosting/FailureCapture.cs` (2 units). It runs from the scenario base's
  failure path on an exception, an assertion failure, the per-test `CancelAfter` timeout (ADR-0006), and the editor
  process exiting while the test holds its lease. It writes the files of contracts/ci.md §3: `ui-tree.json` through
  the existing automation pipe for every editor window (the editor's exit code instead when it is gone); `display.png`
  of the root window through the existing screenshot path; `editor.log` with the last 400 log lines, then the stderr
  tail; `process.txt` with `running` or `exited <code>`. Each part has its own 10 s limit, 30 s in total. `StepTimer`
  exposes the open step and its elapsed time and honours the per-test forced timeout; `NETPRINTS_E2E_FORCE_TIMEOUT=<step>`
  forces it for a manual run of a chosen class. T003's rule tests green.
- [x] T005 [US1] Launched-program state (FR-001, issue #11). Test first:
  `tests/NetPrints.Editor.Tests/Hosting/RunStateTrackerTests.cs` (`notStarted` → `building` → `running` → `exited` with
  the exit code; stdout and stderr tails capped at 200 lines; a new compile or run resets it) and a contract test that
  the automation pipe's `runState` reply serializes through `AutomationJsonContext`. Then
  `src/NetPrints.Editor/Hosting/RunStateTracker.cs`, fed by the existing compile and run flow through
  `IProcessLauncher`; the `runState` request in `Hosting/Automation/AutomationContracts.cs` and `AutomationAgent.cs`; and
  the `run-state.json` part of `FailureCapture`. T003's E2E test green.
- [x] T006 [US1] `docs/contributing/testing.md` (new): the test projects and what each covers, how to run them (the
  AGENTS.md commands), and the Desktop E2E diagnostics (the `e2e-results` artifact, what each file holds, how to force
  a timeout locally). Decision in implementation-notes: no speculative fix for issue #11 (`EditCompileAndRun`); the
  scenario is rewritten on the shell in T043, and the diagnostics explain any recurrence there.

### Batch A3 — model: sonnet — T007–T009 — 4 units

- [x] T007 [US1] Test first: `tests/NetPrints.Core.Tests/Core/CiWorkflowTests.cs` (contracts/ci.md §1–§2), reading the
  workflow files with a small indentation-aware reader inside the test (no new package; Decision). Checks: every test
  project under `tests/` whose name ends in `.Tests` or `.UITests`, except `NetPrints.Desktop.E2ETests`, appears in the
  `test` matrix exactly once; the matrix has `fail-fast: false`; each leg uploads `test-results-<leg>` and
  `coverage-<leg>`; `build-test` is named `Build and test (Linux)`, runs `if: always()`, needs every other
  test-running job except `e2e` and `packages`, and fails unless each needed result is `success`;
  `cli-windows.yml`'s path filter covers every project in the transitive `ProjectReference` closure of
  `tests/NetPrints.Cli.Tests` (fixtures included), the build files of contracts/ci.md §2 and the workflow itself. Red.
- [x] T008 [US1] `.github/workflows/ci.yml` (2 units): split `build-test` into `checks` ("Repository checks (Linux)"),
  the `test` matrix (`Core`, `Catalog`, `CLI`, `Editor`, `Editor UI (headless)`) and the aggregate `build-test`, per
  contracts/ci.md §1. Keep the triggers, permissions, concurrency group, action pins, NuGet cache key,
  `timeout-minutes`, the ADR-0008 coverage settings and the UI leg's `NETPRINTS_UI_ARTIFACTS`; `e2e` and `packages`
  stay as they are. Add one line at the top of `specs/001-modernize-build/contracts/ci-workflow.md` saying its job
  layout is superseded by `specs/005-editor-shell/contracts/ci.md`. T007's `ci.yml` checks green.
- [x] T009 [US1] SC-006 timing. Record in implementation-notes the duration of "Build and test (Linux)" in the last
  pre-split run (CI run 36819424717 on master `3a6eafc`) and of every new Linux job in this batch's PR run. If the
  longest exceeds 60% of the baseline, find the cause in the step timings and fix it in this batch (Decision with the
  evidence; E2E sharding stays in P8).

### Batch A4 — model: sonnet — T010–T014 — 5 units

- [x] T010 [US1] Test first (contracts/ci.md §2): `tests/NetPrints.Cli.Tests/ShowTextconvEncodingTests.cs`. It writes a
  temporary graph with class `Grüße`, method `Größe` and a node title containing `日本語`, runs the built `netprints`
  as a child process with `show --textconv <file>`, and reads stdout as bytes. It asserts exit code 0, valid UTF-8, no
  `EF BB BF` prefix, and that the decoded text contains the three names. If it passes at once on Linux (P2 set UTF-8
  output), prove it catches the defect: make the tool write a BOM or a non-UTF-8 encoding, see the test fail and name
  itself, revert, and record both runs (US1 scenario 5).
- [x] T011 [US1] `.github/workflows/cli-windows.yml` per contracts/ci.md §2: `name: CLI (Windows)`, `windows-latest`,
  the path filter, `workflow_dispatch`, build then test with `--report-xunit-trx`, artifact `test-results-cli-windows`.
  T007's path-filter check green.
- [x] T012 [US1] Get `CLI (Windows)` green. Fix each CLI test that fails on Windows because of a real defect (test first
  when the defect is in `src/`). A test that genuinely cannot run there calls `Assert.Skip("<what is missing>")` behind
  an OS check. `ShowTextconvEncodingTests` never skips. List every fix and skip in implementation-notes.
- [x] T013 [US1] `docs/contributing/testing.md`, CI section: the jobs, the aggregate check, the per-leg artifacts, the
  Windows workflow and when it runs, and how to add a test project (the matrix row `CiWorkflowTests` requires).
- [x] T014 **Checkpoint A**: whole suite plus the E2E run; CI green, including `CLI (Windows)`. Report in
  implementation-notes: FR-105 (`NoTypeNameEndsInVM`), SC-006 (the run where `E2EDiagnosticsTests` passed and the
  `e2e-results` artifact holding its seven files; the T009 timings; the Windows job with `ShowTextconvEncodingTests`
  passed). Docs updated: `docs/contributing/testing.md`, `AGENTS.md`, the skills, ADR-0007.

### Batch A-R — model: opus — T015 (sub-phase review)

- [x] T015 [US1] Review sub-phase A: an Opus reviewer who did not implement it reviews the whole diff of batches A1–A4
  (from the commit before the first batch to HEAD): US1 and FR-105 end to end against spec.md, contracts/ci.md, the
  constitution (1.2.4) and plan.md's standing constraints. It runs the independent test of the phase. Findings
  (severity, file:line, fix) go to the PR as review comments and to `specs/005-editor-shell/implementation-notes.md`
  under "Review A". No code changes in this task.

### Batch A-F — model: sonnet — T016 (reserved: fix review findings)

- [x] T016 [US1] Fix every finding of T015 (test first for behaviour findings), reply on each review thread with the
  fixing commit or the reason for deferral, whole suite plus the E2E run, commit; if the review had no findings, tick
  this task with "no findings". The next sub-phase starts only after this batch is green on CI.

---

## Phase 2: User Story 4 (core) — registry and commands (sub-phase B, Priority P1) 🎯 MVP

**Goal**: one contribution registry with seven kinds (ADR-0020, contracts/contributions.md); a command and handler for
every existing action, plus the new Stop; the saved marker on the undo stack; key bindings generated from the
registry (FR-030, FR-033, FR-034 for the existing actions, FR-037). **Independent test**: the registry and handler
tests; the existing windows' shortcuts run through the registry.

### Batch B1 — model: sonnet — T017–T019 — 4 units

- [x] T017 [US4] Test first: `tests/NetPrints.Editor.Tests/Contributions/ContributionRegistryTests.cs`
  (contracts/contributions.md §1, §4). Ids must match `^[a-z0-9]+(\.[a-zA-Z0-9]+)+$`. An empty label, a bad id, or a
  single-key gesture in `Global` scope is an `InvalidDescriptor` issue and is ignored. A duplicate id within one kind
  is a `DuplicateId` issue and the first wins; the same id in two kinds is allowed. Overlapping gestures in one scope,
  or in `Global` and any scope, are a `GestureConflict`, and only the first keeps the gesture; `Graph` and
  `ProjectTree` never conflict with each other. Gestures compare canonically (`Ctrl+Shift+B` equals `Shift+Ctrl+B`).
  Lists keep registration order. After `Freeze()`, every `Add*` throws `InvalidOperationException`. Each issue is
  logged once at warning level (fake logger). Red.
- [x] T018 [US4] `src/NetPrints.Editor/Contributions/` (2 units): `IContributionRegistry`, `ContributionRegistry`, the
  seven descriptor kinds and their enums (`CommandScope`, `MenuPlacement`, `PanelDock`, `ContextMenuTarget`,
  `ProjectOutputType`), `ICommandHandler` (default `DynamicLabel`), `ITooltipProvider`, `TooltipTarget`,
  `TooltipContent`, `IGoToProvider`, `GoToItem`, `ContributionIssue`, a UI-free `CommandGesture` parser and normaliser
  for the descriptor strings, `ContributionIds` (the built-in prefixes and the owner `netprints`), and `Log.cs`
  (`[LoggerMessage]`). Test first: `tests/NetPrints.Editor.Tests/Architecture/` asserts the namespace uses no Avalonia
  or Dock type. T017 green.
- [x] T019 [US4] Test first: `tests/NetPrints.Editor.Tests/Shell/DocumentIdTests.cs` (contracts/shell.md §2,
  data-model "Shell"): `graph:<classPath>#<graphKey>` with `method:<id>`, `ctor:<id>`, `event:<id>` and `class`, plus
  `start` and `project-settings`, round-trip through `ToString` and `TryParse`; malformed strings return false; value
  equality. Then `src/NetPrints.Editor/Shell/DocumentId.cs`, `IShell.cs` (the data-model members), `CommandContext`
  (shell, session or null, active document, active graph, selection, parameter; built per invocation by an
  `ICommandContextProvider`), and a `FakeShell` test double in `tests/NetPrints.Editor.Tests/Shell/FakeShell.cs` for
  the handler tests.

### Batch B2 — model: sonnet — T020–T022 — 4 units

- [x] T020 [US4] Test first: `tests/NetPrints.Editor.Tests/UndoRedo/UndoRedoStackSavedMarkerTests.cs` (research R6,
  data-model "Project session and lifecycle"). After `MarkSaved()`, `IsAtSavedState` is true; an edit makes it false;
  undoing back makes it true again; undoing, then a new edit at the same depth (a different top command) is not the
  saved state; `Clear()` resets to "no saved state". `UndoName` and `RedoName` come from `IUndoableCommand.Name`, or
  null when nothing can be undone or redone. Then `src/NetPrints.Editor/UndoRedo/UndoRedoStack.cs`.
- [x] T021 [US4] Test first: `tests/NetPrints.Editor.Tests/Shell/ProjectSessionViewModelTests.cs` (2 units). Extract the
  open project's state and operations from `MainEditorViewModel` into
  `src/NetPrints.Editor/Shell/ProjectSessionViewModel.cs` (data-model: `Project`, `ProjectFilePath`, `ProjectKey`, the
  per-class undo stacks, save, save all, compile, run). `ProjectKey` comes from `EditorDataPaths` in T051; until then it
  is a private helper with the same rule, moved there by T051. `MainEditorViewModel` delegates to it until C removes it.
  The existing editor tests stay green unchanged, or change only in the type they construct.
- [x] T022 [US4] Build and save handlers, test first in `tests/NetPrints.Editor.Tests/Commands/BuildCommandsTests.cs`
  with `FakeShell` and a fake `IProcessLauncher`: `save`, `saveAll`, `compile`, `run`, and the new `stop` (FR-033).
  Stop cancels the run's token and kills the process tree; Stop is enabled only while the program runs; Run and Stop
  report their state through `RunStateTracker`; compile and run wait for a save in progress. Handlers in
  `src/NetPrints.Editor/Commands/`.

### Batch B3 — model: sonnet — T023–T025 — 4 units

- [x] T023 [US4] Edit and viewport handlers (2 units), test first in
  `tests/NetPrints.Editor.Tests/Commands/EditCommandsTests.cs`: `undo` and `redo` (acting on the active document's
  class, or the tree selection's class; `DynamicLabel` gives `Undo <action>` or plain `Undo` and disabled), `delete`,
  `rename`, `selectAll`, `cancel`, `nodeSearch`, `frameSelection` and `fitAll`. The handlers stay UI-free: the
  viewport and selection commands raise a request on the graph's view model, and a behavior on the Nodify editor
  carries it out (selection, `BringIntoView`, fit), so menu and keyboard behave the same (edge case "shortcut Nodify
  handles itself").
- [x] T024 [US4] Project handlers, test first in `tests/NetPrints.Editor.Tests/Commands/ProjectCommandsTests.cs`:
  `openProject`, `newProject`, `closeProject`, `projectSettings`, `references`, `classSettings`, `addMethod`,
  `addConstructor`, `addVariable`, `addEventGraph` and `exit`, wrapping today's flows. The unload prompt is added in D
  (T050) in `UnloadingCommandHandler` (open, new, close); `exit` only closes the window and the window-close path
  prompts (Review B R6).
- [x] T025 [US4] Built-in registration: `src/NetPrints.Editor/Contributions/BuiltIn/{File,Edit,View,Go,Build,Help}Contributions.cs`.
  Test first: `tests/NetPrints.Editor.Tests/Contributions/BuiltInCommandTableTests.cs` holds contracts/commands.md §1
  as data. Every registered built-in command matches its row (id, label, menu path and group, gestures, scope,
  command-bar order); `exit`'s Alt+F4 belongs to the OS and is not a registered gesture. A shrink-only
  `PendingCommandIds` list names the rows whose feature lands later, each with its task id; a stale entry fails, like
  the XAML allowlists. The built-ins report 0 issues (SC-003: 0 conflicts).

### Batch B4 — model: sonnet — T026–T027 — 3 units

- [x] T026 [US4] Key bindings from the registry (2 units). Test first:
  `tests/NetPrints.Editor.UITests/Commands/CommandKeyBindingTests.cs` (headless, on a test host window with a text box
  and a graph canvas). `Global` gestures run their command anywhere. `Graph` gestures run only while the canvas has
  focus, and never while a text input inside a node or the inspector has focus (Ctrl+A, Delete and F2 stay with the
  text field). A disabled command does not run. Then `src/NetPrints.Editor/Behaviors/CommandKeyBindingsBehavior.cs`
  (window `KeyBindings` from `Global` commands) and `ScopedCommandKeysBehavior.cs` (a tunnel handler on the canvas for
  `Graph`, on the tree for `ProjectTree`), parsing the descriptor strings into `KeyGesture` in the view layer.
  `GraphEditorView`'s global Ctrl+Space tunnel handler is replaced by `nodeSearch`. Attach both to
  `ClassEditorWindow`, replacing its hand-written Delete, Ctrl+Z and Ctrl+Y bindings; C moves them to the shell.
- [x] T027 **Checkpoint B**: report the registry tests, the handler tests, SC-003 so far (registered built-ins match
  the contract, 0 conflicts, the pending list), and the existing windows' shortcuts running through the registry.
  Docs updated: ADR-0020 and contracts/contributions.md name any member that changed in implementation (the contract
  allows that only together with its tests).

### Batch B-R — model: opus — T028 (sub-phase review)

- [x] T028 [US4] Review sub-phase B: an Opus reviewer who did not implement it reviews the whole diff of batches B1–B4
  (from the commit before the first batch to HEAD): the registry and the handlers against spec.md US4,
  contracts/contributions.md, contracts/commands.md, ADR-0020, the constitution and plan.md's standing constraints
  (UI-free descriptors, first-wins rules, frozen registry, async rules for handlers). It runs the independent test of
  the phase. Findings go to the PR and to implementation-notes under "Review B". No code changes in this task.

### Batch B-F — model: sonnet — T029 (reserved: fix review findings)

- [x] T029 [US4] Fix every finding of T028 (test first for behaviour findings), reply on each review thread with the
  fixing commit or the reason for deferral, whole suite plus the E2E run, commit; if the review had no findings, tick
  this task with "no findings". The next sub-phase starts only after this batch is green on CI.

---

## Phase 3: User Story 2 — work in one window (sub-phase C, Priority P1) 🎯 MVP

**Goal**: one `ShellWindow` with the project tree, tabbed graph documents, the inspector, the Errors, Output and C#
panels, the menu bar, the command bar and the status bar, docked through `IShell` (ADR-0018); the launcher and the
per-class windows removed (FR-010–FR-018, FR-031, FR-032). Contracts: shell.md, commands.md. **Independent test**:
US2's independent test; SC-001.

### Batch C1 — model: sonnet — T030–T032 — 5 units

- [x] T030 [US2] Test first: `tests/NetPrints.Editor.Tests/Architecture/DockConfinementTests.cs`. No `Dock.*` namespace or
  Dock XAML namespace is used outside `src/NetPrints.Editor/Shell/Docking/`. `NetPrints.Editor`'s
  `obj/project.assets.json` lists no `ReactiveUI*` and no `Newtonsoft.Json` library. `Directory.Packages.props` pins
  `Dock.Avalonia`, `Dock.Model.Mvvm`, `Dock.Avalonia.Themes.Fluent` and `Dock.Serializer.SystemTextJson` exactly at
  `[12.1.0.6]`. Then add the pins, and the `PackageReference`s to `src/NetPrints.Editor/NetPrints.Editor.csproj` only.
- [x] T031 [US2] Dock spike, checks 1–3 of ADR-0018 (2 units), headless in
  `tests/NetPrints.Editor.UITests/Shell/DockSpikeTests.cs`: (1) a document `DataTemplate` with `x:DataType` and
  compiled bindings renders a NetPrints view model inside a `DocumentDock`; (2) a layout saved with
  `Dock.Serializer.SystemTextJson` (source-generated context, assembly attribute) and loaded again re-creates its
  documents through the app by stable id (`DockState` re-attach); (3) automation ids on content in a docked, a tabbed
  and a floating pane are found by a headless test and by the automation pipe's tree builder
  (`src/NetPrints.Editor/Hosting/Automation/AutomationTree.cs`). Record each result (pass or fail, evidence, any
  workaround) in implementation-notes under "Spike".
- [x] T032 [US2] Dock spike, checks 4–5 and the gate (2 units). (4) Float and re-dock a pane under Xvfb and openbox in a
  throwaway E2E class `tests/NetPrints.Desktop.E2ETests/Scenarios/DockSpikeTests.cs`, against a spike window that the
  desktop host opens only with `NETPRINTS_DOCK_SPIKE=1`: native floating first, then managed floating on Linux if
  native fails (ADR-0018 "Floating"). (5) Light and Dark through `DynamicResource` tokens in `ThemeDictionaries`
  (headless, both variants). **Gate**: all five pass → Dock. Check 1, 2 or 3 fails and the spike cannot fix it → the
  fallback adapter (plain `Grid`, `GridSplitter` and `TabControl`; "open in new window" through `IWindowService`;
  envelope `engine: "grid"`); remove the Dock pins and T030's pin check, and the coordinator rewrites the Dock-specific
  text of T033 and T064 before batch C2 starts. Check 4 or 5 fails alone → Dock, with the recorded mitigation. Record
  the decision in implementation-notes and update ADR-0018's Status with the outcome.

### Batch C2 — model: sonnet — T033–T035 — 6 units

- [x] T033 [US2] Shell adapter (2 units). Test first: `tests/NetPrints.Editor.UITests/Shell/ShellAdapterTests.cs`, through
  `IShell` only. Opening a document twice activates its one tab; activate; close; show and hide a panel; float and
  dock a document; reset to the default layout of contracts/shell.md §1 (tree left 20%, inspector right 22%, bottom
  25% with Errors active, every panel visible); a floated pane closed with the OS close button docks back to its
  default place, and a floated graph tab closes as a tab does. Then `src/NetPrints.Editor/Shell/Docking/DockShellAdapter.cs`
  and `ShellDockFactory.cs`; views resolve by `DataTemplate x:DataType` (ADR-0007 D6), never by Dock's locator. Fold the
  reusable spike tests in here and delete the spike code (window, `NETPRINTS_DOCK_SPIKE`, `DockSpikeTests`).
- [x] T034 [US2] Shell view models (2 units). Test first: `tests/NetPrints.Editor.Tests/Shell/ShellViewModelTests.cs` and
  `TitleFormatterTests.cs` (contracts/shell.md §4). `ShellViewModel` (session, documents, active document, panels from
  the registry, status message, title), `DocumentViewModel`, `GraphDocumentViewModel` (wraps `NodeGraphViewModel`; adds
  the viewport location and zoom), `PanelViewModel`, `StatusBarViewModel` (a message with expiry through
  `TimeProvider`, the build state) and `TitleFormatter`, in `src/NetPrints.Editor/Shell/`. The five built-in panels
  (`netprints.panel.projectTree`, `inspector`, `errors`, `output`, `csharp`) are registered through the registry.
  Opening or activating a graph document puts focus in its canvas (Review B R5; `GraphEditorView` already does so when
  its graph changes through `FocusOnDataContextBehavior`; a tab switch that keeps the view needs the same).
- [x] T035 [US2] `src/NetPrints.Editor/Shell/ShellWindow.axaml` with the menu bar, the command bar and the status bar
  generated from the registry (2 units). Test first: `tests/NetPrints.Editor.UITests/Shell/RegistrySurfaceTests.cs`
  (contracts/contributions.md §3–§4, commands.md §1). Menus come in the order File, Edit, View, Go, Build, Help, with
  dividers between groups and items in order, showing shortcut text, disabled state and the dynamic `Undo <action>`
  labels. The command bar is at most 40 px high; each button shows an icon and a label with the tooltip
  `"<Label> (<first shortcut>)"`; Compile shows an error badge after a compile with errors; Run and Stop share one
  slot. Every icon-only element has an accessible name, and every interactive element is reachable with the keyboard
  (FR-101). Automation ids derive from command ids (`Menu.<commandId>`, `CommandBar.<commandId>`), plus new `Shell.*`,
  `Menu.*` and `CommandBar.*` constants in `src/NetPrints.Editor/AutomationIds.cs`. No code-behind beyond
  `InitializeComponent`. The surfaces re-query on `ICommandContextProvider.CommandStatesChanged` (Review B R4):
  add to `RegistrySurfaceTests` that Run and Compile are disabled during a compile and re-enabled after, and that the
  Undo label and enabled state change after an edit.

### Batch C3 — model: sonnet — T036–T038 — 5 units

- [x] T036 [P] [US2] Project tree panel (2 units). Test first:
  `tests/NetPrints.Editor.Tests/ProjectTree/ProjectTreePanelViewModelTests.cs`. The tree shows project › classes ›
  Methods, Constructors, Variables, Event graphs, and follows adds, removes and renames. Double-click or Enter opens a
  graph through `IShell.OpenDocument`. The selection feeds the inspector. Context menus come from the registry's
  context-menu items (contracts/commands.md §2); an item whose command cannot run for its target is hidden. Then the
  view model and view in `src/NetPrints.Editor/ProjectTree/` (content moved from `ClassEditorWindow`'s lists), with
  automation ids `Tree.<kind>.<name>`, and a headless wiring test.
- [x] T037 [P] [US2] Inspector panel. Test first in `tests/NetPrints.Editor.Tests/Inspectors/InspectorPanelViewModelTests.cs`:
  it hosts the existing class, method and variable inspectors for the current selection of the active tree or graph
  (reusing `SelectInspectorMessage`), and shows an empty state when nothing is selected or no project is open. Then
  `src/NetPrints.Editor/Inspectors/InspectorPanelViewModel.cs` and its view.
- [x] T038 [P] [US2] Bottom panels (2 units), test first in `tests/NetPrints.Editor.Tests/Output/` and
  `tests/NetPrints.Editor.Tests/ErrorList/`. Errors: the existing `ErrorListViewModel`; activating a row opens its
  graph's tab and selects the node. Output: `src/NetPrints.Editor/Output/OutputPanelViewModel.cs` and its view show
  the build output, then the program's stdout and stderr from `RunStateTracker`, cleared at each compile or run. C#:
  the existing `CodeViewViewModel`, following the active document's class.

### Batch C4 — model: sonnet — T039–T041 — 5 units

- [x] T039 [US2] Documents and layout commands (2 units). Test first: handler tests in
  `tests/NetPrints.Editor.Tests/Shell/DocumentCommandsTests.cs` with `FakeShell`, and headless tab tests. Graph
  documents open from the tree or an error (an open one is activated, and its canvas takes focus: Ctrl+Space right
  after a tab switch opens the search with no click, SC-004); tabs reorder and close by their button, a
  middle-click and `closeTab` (Ctrl+W); `nextTab` and `previousTab` (Ctrl+Tab, Ctrl+Shift+Tab); `floatDocument`;
  `dockDocument`; `showPanel.<panel>` (closing a pane hides it, and the View menu shows it again); `resetLayout`. The
  Project settings document (`project-settings`) replaces the launcher's settings pane. A floated graph keeps
  editing, undo and save (US2 scenario 7).
- [x] T040 [US2] Composition (2 units): `src/NetPrints.Editor/Hosting/EditorComposition.cs` registers the registry (frozen
  at startup), `IShell`, `ShellViewModel` and the built-in contributions. `src/NetPrints.Desktop/Program.cs` opens
  `ShellWindow` (a project argument opens the project; without one the document area stays empty until the start page
  lands in T066). `IWindowService.OpenClassEditor` is removed. `ShutdownCoordinator` hooks the shell window's close.
  The T026 key-binding behaviors move to `ShellWindow`, the canvas and the tree. (Done in C4b: `IWindowService.OpenClassEditor`
  stays until T044, which deletes the legacy windows and their tests.)
- [x] T041 [US2] Test first, then make green: `tests/NetPrints.Editor.UITests/Shell/FormerActionsReachableTests.cs`
  (FR-017, US2 scenario 8). A table of every action the launcher and the class window offered (class settings; add
  and remove methods, constructors, variables and event graphs; references; project settings; create and open
  project; save; compile; run) maps each to a command id, a tree context-menu item or an inspector element, and the
  test finds each one in the shell.
  The table also lists the actions T044 left without a surface (C5f1): override a base method (`overrideMethod`)
  and open a variable's getter, setter or type graph (variable inspector).
  C5f2 adds the Variables panel (`showPanel.variables`, add and list of member variables, add and list of the active
  method's local variables, rename in the variable inspector); a mutation check removed each surface once and the
  matching row failed.

### Batch C5 — model: sonnet — T042–T045 — 6 units

- [x] T042 [US2] Page objects (2 units): `tests/NetPrints.Testing.Ui/Shell/` gains `ShellPage`, `ProjectTreePage`,
  `DocumentTabsPage`, `InspectorPage`, `BottomPanelPage`, `MenuBar` and `CommandBar`. They find panels by panel id,
  tabs by document id and commands by command id, never by Dock's element names. Rewrite the
  `Scenarios/SmokeScenarios.cs` flows (create project, edit-compile-run, add references, pan, drag from lists) against
  them; the headless smoke tests in `NetPrints.Editor.UITests` pass.
- [x] T043 [US2] Desktop E2E, one class each (2 units). `ShellMainFlowTests` (SC-001): open a copy of the HelloWorld
  sample, open two graphs, compile with an error, activate it, fix it, run, and read `Hello, World!` in Output; the
  automation pipe lists exactly one editor window apart from dialogs. `FloatAndRedockGraphTests` replaces
  `MinimizeAndRestoreClassWindowTests`: float a graph, edit, undo, save, dock it back; a floated pane closed with the
  OS close button docks back. `ResetLayoutTests`. `EditCompileAndRunTests` and `ShutdownTests` run on the shell. If
  `EditCompileAndRun` fails, its T004 diagnostics go into implementation-notes and the cause is fixed here (issue #11).
- [x] T044 [US2] Remove the old windows, only after T043 is green: `src/NetPrints.Editor/Main/MainWindow.*` and
  `MainEditorViewModel` (their remaining duties moved to `ShellViewModel`, `ProjectSessionViewModel` and the handlers),
  `src/NetPrints.Editor/ClassEditor/ClassEditorWindow.*` and `ClassEditorViewModel` (lists to the tree, inspectors to
  the inspector panel), the old page objects (`tests/NetPrints.Testing.Ui/Main/MainWindowPage.cs`,
  `ClassEditor/ClassEditorPage.cs`), and every `AutomationIds` constant nothing uses. The suite stays green.
- [x] T045a [US2] Stabilize the flaky `SnapshotTests.Inspectors` (issue #13, batch C5g, B7 of the flaky-test plan).
  The code view's TextMate highlighting runs on a thread-pool thread, so a capture right after the code text
  appears can catch the last lines uncoloured. Add `SnapshotStore.MatchStableAsync` (capture until two consecutive
  frames are identical within a frame budget; records the frames taken; fails with the frames taken and the last
  diff), unit-test it with a fake frame source, and move `Inspectors` and the other snapshot tests onto it.
  The code view also reports `HighlightingSettled` (TextMate lines all tokenized), which the inspector snapshots wait
  for before the stable capture. Before: 1/8 combined runs failed under CPU contention; after: 0/16. Closes #13.
- [x] T045 **Checkpoint C**: report SC-001 (`ShellMainFlowTests` and its window count), FR-010–FR-018 with the test
  for each, the spike outcome and `RegistrySurfaceTests`. Docs updated: `docs/guide/projects.md` and `README.md` no
  longer describe the launcher or the per-class windows (the full guides come in H); release notes: the
  single-window editor.

### Batch C-R — model: opus — T046 (sub-phase review)

- [x] T046 [US2] Review sub-phase C: an Opus reviewer who did not implement it reviews the whole diff of batches C1–C5
  (from the commit before the first batch to HEAD): US2 end to end against spec.md, contracts/shell.md,
  contracts/commands.md, ADR-0018 (Dock confined to `Shell/Docking`, the spike evidence, the gate decision), ADR-0007,
  the constitution and plan.md's standing constraints. It runs the independent test of the phase. Findings go to the
  PR and to implementation-notes under "Review C". No code changes in this task.

### Batch C-F — model: sonnet — T047 (reserved: fix review findings)

- [x] T047 [US2] Fix every finding of T046 (test first for behaviour findings), reply on each review thread with the
  fixing commit or the reason for deferral, whole suite plus the E2E run, commit; if the review had no findings, tick
  this task with "no findings". The next sub-phase starts only after this batch is green on CI.

---

## Phase 4: User Stories 3 and 4 (rest) — never lose work; feedback and shortcuts (sub-phase D, Priority P1) 🎯 MVP

**Goal**: unsaved state per file with markers, one unload prompt for the five unload paths, backups and crash
recovery (FR-020–FR-026); undo feedback, the shortcuts sheet and keyboard-only use (FR-034–FR-036). Contracts:
state-files.md §1–§3 (backups), shell.md §4–§5, commands.md §3. **Independent test**: US3's and US4's; SC-002, SC-003,
SC-004 (all but navigation).

### Batch D1 — model: sonnet — T048–T050 — 5 units

- [x] T048 [US3] Unsaved tracking (2 units). First check whether the references and project-settings flows write the
  project file immediately (research R6) and record the Decision. Test first:
  `tests/NetPrints.Editor.Tests/Lifecycle/UnsavedChangesTrackerTests.cs`. A class file is unsaved after any change, and
  saved after a successful save or when undo returns to the saved marker; a non-undoable inspector edit marks it
  unsaved until it is saved; the project file is unsaved only while a project-level change is pending; `UnsavedFiles`
  lists `UnsavedFile` (path, kind, display name). Then `src/NetPrints.Editor/Lifecycle/UnsavedChangesTracker.cs` on
  top of `ClassGraph.IsDirty` and the T020 marker.
- [x] T049 [US3] Markers and saving (FR-021, FR-023, FR-083). Test first in `tests/NetPrints.Editor.Tests/Lifecycle/`
  plus a headless marker test: `*` on the tabs of an unsaved class file's graphs, on its tree class node, and after
  the project name in the title (contracts/shell.md §4). Save saves the active graph's file and Save all every unsaved
  file, with the status `Saved <n> file(s)`.
- [x] T050 [US3] Unload prompt (2 units). Test first: `tests/NetPrints.Editor.Tests/Lifecycle/ConfirmUnloadTests.cs`.
  `ShellViewModel.ConfirmUnloadAsync` runs before every unload (window close through `ShutdownCoordinator`, Exit, Close
  project, Open project, New project) and prompts only when files are unsaved. `UnsavedChangesDialog`
  (`DialogViewModel<UnloadChoice>`; Save all is the default, Don't save, Cancel on Esc; lists the files; ids
  `Dialogs.Unsaved.*`): Cancel keeps the project and its changes; Save all saves, then unloads; a failed save shows the
  error and keeps the project open; Don't save unloads. Exit with unsaved changes and Don't save: exactly one prompt. Closing a tab never prompts. Exit while a program runs asks
  "Stop running program?" (Stop and exit, Cancel), and exit while compiling waits for the build (contracts/shell.md §5).

### Batch D2 — model: sonnet — T051–T053 — 5 units

- [x] T051 [US3] Test first: `tests/NetPrints.Editor.Tests/State/EditorDataPathsTests.cs` and `AtomicFileWriterTests.cs`
  (contracts/state-files.md §1). The root is `<ApplicationData>/NetPrints`, replaced by `NETPRINTS_STATE_DIR`.
  `ProjectKey` is the first 16 lowercase hex characters of SHA-256 over the project file's full path in UTF-8,
  case-folded first on Windows. A write goes to `<file>.tmp` and is renamed over the target. On Linux and macOS,
  folders are created `0700` and files `0600` (real file system, behind an OS check). Then
  `src/NetPrints.Editor/State/EditorDataPaths.cs` and `AtomicFileWriter.cs`, behind an I/O abstraction that E reuses;
  `ProjectSessionViewModel` takes its key from here.
- [x] T052 [US3] Backups (2 units). Test first: `tests/NetPrints.Editor.Tests/Lifecycle/BackupServiceTests.cs`, with a
  fake `TimeProvider` and in-memory I/O (contracts/state-files.md §1–§3). A backup is written 30 s after a file's last
  change, and every change restarts the wait. It goes to `backups/<project-key>/<relative-path>.bak.json` with the
  canonical JSON a save would write, listed in `manifest.json` (`schemaVersion` 1, original path, `writtenUtc`,
  `sha256`). Save, Don't save and Discard delete that file's backup, and a project folder with none left is removed.
  At startup, backups whose `writtenUtc` is older than 30 days are deleted, and so are folders whose project path no
  longer exists. A write failure never interrupts editing: it is logged and shows one status warning per session
  (`Backups are failing; see the log`, FR-026). Nothing is ever written inside the project directory. Then
  `src/NetPrints.Editor/Lifecycle/BackupService.cs`, wired to the tracker. `NETPRINTS_BACKUP_DELAY` (milliseconds,
  test-only, for E2E) shortens the wait (Decision).
- [x] T053 [US3] Recovery (2 units). Test first: `tests/NetPrints.Editor.Tests/Lifecycle/RecoveryServiceTests.cs`. Opening
  a project whose backup folder is not empty shows the `RecoverDialog` (`Dialogs.Recover.*`), listing each backed-up
  file, with "older than the file on disk" when the original's last write time is later than its `writtenUtc`.
  Restore (the default, unless a backup is older than its file, when Discard is) loads the content as unsaved changes
  and keeps the backup until save or discard. Discard deletes the backups. Then
  `src/NetPrints.Editor/Lifecycle/RecoveryService.cs` and the dialog.

### Batch D3 — model: sonnet — T054–T058 — 6 units

- [x] T054 [US4] Undo feedback and status messages (FR-035, contracts/commands.md §3). Test first in
  `tests/NetPrints.Editor.Tests/Shell/StatusMessagesTests.cs` plus a headless menu-label test: `Undo <action>` and
  `Redo <action>` in the Edit menu and in the command bar tooltips, disabled when there is nothing to undo or redo;
  `Undid: <action>` and `Redid: <action>` for 4 s (`TimeProvider`); `Build succeeded`, `Build failed: <n> error(s)`,
  `Running…` and `Exited with code <n>`.
- [x] T055 [US4] Help menu (FR-036). Test first, headless: the `keyboardShortcuts` sheet
  (`src/NetPrints.Editor/Commands/KeyboardShortcuts/`) lists every registered command with its shortcuts, grouped as in
  the menu, and its rows equal the registry's commands. `about` shows the version and the project links.
- [x] T056 [US3] Desktop E2E (2 units). `UnsavedChangesPromptTests`: edit, close the window, Cancel keeps it open with
  the change; close again, Save all saves and closes; Close project also shows the prompt. `CrashRecoveryTests`: edit
  with a short `NETPRINTS_BACKUP_DELAY`, wait for the backup file, kill the editor process, restart on the same
  project and `NETPRINTS_STATE_DIR`, Restore, then Save; the saved file is byte-identical to the canonical form of the
  pre-kill content (SC-002).
- [x] T057 [US4] `tests/NetPrints.Editor.UITests/Commands/DefaultShortcutTests.cs`: each FR-034 gesture of a registered
  command runs it where it applies, and single-key gestures never act while a text field has focus. Desktop E2E
  `KeyboardOnlyTests`: save, compile, run, stop, undo, redo, Ctrl+Tab and Ctrl+W without the mouse (T086 adds "find a
  node and go back"). SC-003 (shortcuts), SC-004 (so far).
- [x] T058 **Checkpoint D**: report SC-002 (the five unload paths with their tests; the recovery byte comparison),
  SC-003 so far (the registry against the contract table with the pending list, 0 built-in conflicts, the
  shortcuts) and SC-004 so far. Docs updated: release notes (unsaved markers, the unload prompt, backups and recovery,
  the menus, the command bar, shortcuts, Stop).

### Batch D-R — model: opus — T059 (sub-phase review)

- [x] T059 [US3] Review sub-phase D: an Opus reviewer who did not implement it reviews the whole diff of batches D1–D3
  (from the commit before the first batch to HEAD): US3 and the rest of US4 end to end against spec.md,
  contracts/state-files.md, contracts/shell.md §4–§5, contracts/commands.md §3, the constitution and plan.md's
  standing constraints, looking hardest for any path that loses work or writes into the project directory. It runs
  the independent test of the phase. Findings go to the PR and to implementation-notes under "Review D". No code
  changes in this task.

### Batch D-F — model: sonnet — T060 (reserved: fix review findings)

- [x] T060 [US3] Fix every finding of T059 (test first for behaviour findings), reply on each review thread with the
  fixing commit or the reason for deferral, whole suite plus the E2E run, commit; if the review had no findings, tick
  this task with "no findings". The next sub-phase starts only after this batch is green on CI.

---

## Phase 5: User Stories 5 and 6 — start dashboard and persistence (sub-phase E, Priority P2)

**Goal**: the start page with recent projects, templates, samples and what's new (FR-040–FR-044); per-user state for
the window, the layout and each project's session, with safe fallbacks (FR-050–FR-052). Contract: state-files.md.
**Independent test**: US5's and US6's; SC-005 (the theme part closes in T093).

### Batch E1 — model: sonnet — T061–T063 — 4 units

- [x] T061 [US6] State store (2 units). Test first: `tests/NetPrints.Editor.Tests/State/JsonEditorStateStoreTests.cs`
  (contracts/state-files.md §2). `window.json`, `layout.json`, `recent.json` and `sessions/<project-key>.json` carry
  `schemaVersion` 1 and are UTF-8 without a BOM with LF line endings, written through a source-generated
  `StateJsonContext`. A missing, unreadable or newer file gives the defaults with one warning log, and is not
  rewritten until that state changes. Writes go through `AtomicFileWriter`. Then
  `src/NetPrints.Editor/State/IEditorStateStore.cs`, `JsonEditorStateStore.cs`, `StateJsonContext.cs` and the state
  records.
- [x] T062 [P] [US5] Recent projects. Test first: `tests/NetPrints.Editor.Tests/State/RecentProjectsTests.cs` (FR-041,
  state-files.md §3): pinned first, then the most recent; at most 20 unpinned, the oldest dropped; pinned never
  dropped; search by name or path (case-insensitive substring); missing paths flagged unavailable; remove never touches
  files; paths compared case-insensitively on Windows only; opening or creating a project records it. Then
  `src/NetPrints.Editor/State/RecentProjects.cs`.
- [x] T063 [P] [US6] Window placement. Test first: `tests/NetPrints.Editor.Tests/State/WindowPlacementTests.cs`: saved
  bounds that intersect no current screen give a window centred on the primary screen at the saved size, clamped to
  it; the maximized state is restored. Then `src/NetPrints.Editor/State/WindowStateService.cs` (the placement rule is a
  pure function; the window reads and writes through a behavior).

### Batch E2 — model: sonnet — T064–T065 — 4 units

- [x] T064 [US6] Layout persistence (2 units). Test first: `tests/NetPrints.Editor.UITests/Shell/DockLayoutRoundTripTests.cs`.
  Save and restore through the ADR-0018 envelope (`schemaVersion`, `engine`, `dockLayout`), where `dockLayout` is the
  NetPrints-owned DTO tree (dock kind, id, proportion, active and visible flags, children; floating windows with their
  bounds; hidden panel ids) written through a source-generated `JsonSerializerContext` inside `Shell/Docking/`, never
  through Dock's serializers (ADR-0018 Status); the adapter maps the DTO to and from Dock's model, building the
  dockables from the registered panels and the documents the session can open. Panels match by panel id and documents
  by `DocumentId`; an unknown panel (an extension no longer installed) and a graph that no longer exists are dropped
  and the rest is restored; a layout that fails to load is logged and replaced by the default; layout changes are saved
  debounced (`TimeProvider`). Then `src/NetPrints.Editor/Shell/Docking/LayoutSerializer.cs` and its DTO and context.
- [x] T065 [US6] Sessions (2 units). Test first: `tests/NetPrints.Editor.Tests/State/SessionStateTests.cs`. Per project,
  the open documents in order, the active one, and each graph's viewport (location, zoom) are saved on unload and on
  exit, and restored on open; unresolvable documents and non-finite viewports are skipped; the active document falls
  back to the first restored one; the last instance to unload writes. Wire the window, layout and session restore
  into startup and project open.

### Batch E3 — model: sonnet — T066–T068 — 6 units

- [x] T066 [US5] Start page (2 units). Test first: `tests/NetPrints.Editor.Tests/StartPage/StartPageViewModelTests.cs`.
  The `start` document shows whenever no project is open (startup without a project argument, Close project, the
  `startPage` command), built from the registered dashboard tiles (`netprints.tile.recent`, `open`, `new`, `samples`,
  `whatsNew`) in order. A start on a path that does not exist or is not a project shows the start page with an error
  naming the path. Then the view models and views in `src/NetPrints.Editor/StartPage/`, ids `StartPage.*`, with the
  recent list's open, pin, unpin, remove and search bound to commands.
- [x] T067 [US5] New project (2 units). Test first: `tests/NetPrints.Editor.Tests/StartPage/ProjectTemplateServiceTests.cs`.
  The built-in templates `netprints.template.console` (`Exe`) and `netprints.template.library` (`Library`) use the
  `netprints.default` profile. A test template registered through the registry appears in the dialog and creates its
  project (the point U1's UnrealSharp template will use, research R8). An invalid name (not a C# identifier for the
  namespace, or not a valid file name) or a folder that is not empty is rejected before anything is written. A
  failing template removes the partly created folder and shows the error. Success opens the project and records it in
  Recent. A project created from the Executable template seeds `Program.netpc.json` (the `Program` class graph with an
  empty `public static void Main()`, like `samples/HelloWorld/HelloWorld.Program.netpc.json`); a Library project seeds
  no graph. Test first: a created Executable project builds with no CS5001 and runs. Then `ProjectTemplateService` and
  `NewProjectDialog` (`DialogViewModel<string?>`, Create enabled only when the input is valid).
- [x] T068 [US5] Samples and What's new (2 units). Bundle `samples/HelloWorld` (the `.csproj`, the graphs and the
  generated files; not `bin/`, `obj/` or `Compiled_HelloWorld/`) as content of
  `src/NetPrints.Desktop/NetPrints.Desktop.csproj`. Opening a sample copies it to a folder the user picks and opens the
  copy; a test checks that the bundled files' hashes do not change. `src/NetPrints.Editor/StartPage/WhatsNew.md` is
  rendered as headings, bullets and links without a Markdown library, with a link to the GitHub releases page (test
  first for the renderer).

### Batch E4 — model: sonnet — T069–T071 — 4 units

- [x] T069 [US5] Desktop E2E `StartPageNewProjectTests`: start with no arguments, create a Console app in an empty
  temporary folder; it opens and appears in Recent; close it, reopen it from Recent, pin it, search for it, remove it.
- [x] T070 [US6] Desktop E2E `RestoreSessionTests` (2 units): move a pane, open three graphs, zoom and pan one, move and
  resize the window, restart; the layout, tabs, active tab, viewport and window bounds equal the saved values (read
  through the automation pipe). Write `{` into `state/layout.json` and restart: the default layout, and a warning in
  the log. SC-005 (the theme part closes in T093).
- [x] T071 **Checkpoint E**: report SC-005 (all but the theme), FR-040–FR-044 and FR-050–FR-052 with their tests. Docs
  updated: `docs/guide/projects.md` (create and open from the start page, templates, samples); release notes (start
  page, templates, restored layout and sessions).

### Batch E5a — model: sonnet — T071a–T071f — 8 units (start page redesign, layout and recent list; research R18)

- [x] T071a [US5] Tool panels hidden with no project (2 units). Test first:
  `tests/NetPrints.Editor.UITests/Shell/NoProjectPanelsTests.cs` and `tests/NetPrints.Editor.Tests/Shell/` (layout saver):
  with no project open no tool panel is visible and the start page document is shown; opening a project brings the
  panels back to their docks (the main snapshot is unchanged); the layout file is not written while they are hidden,
  not by a layout change and not on close. Then `DockShellAdapter` hides and restores the panels and `LayoutSaver`
  skips the hidden state; `ShellHost` drives it from the session.
- [x] T071b [US5] Responsive layout (2 units). Test first: `tests/NetPrints.Editor.UITests/Shell/StartPageLayoutTests.cs`:
  at 1600 and at 900 DIP the page has two and one columns (the "Get started" column right of the recent list at the
  wide size, above it at the narrow size); the content is centred and at most about 1200 DIP; the existing automation
  ids all still resolve. Then `StartPageView` (container query, one product title row, left-aligned headers).
- [x] T071c [US5] Get started cards and What's new (2 units). Test first: `StartPageViewModelTests` and
  `StartPageLayoutTests`: each card (New project, Open folder or project, Samples, Learn) is one button with its
  automation id; What's new starts collapsed when the version's notes were already seen and expanded when not (the version
  is marked as seen in `start.json` when the page is shown). Then the card styles, the samples list as cards and the collapsible section.
- [x] T071d [US5] Recent rows (1 unit). Test first: `RecentProjectsTileViewModelTests`: grouping by Pinned, Today, This
  week, This month and Older with a fake `TimeProvider`, the relative date text, the unavailable row ("Not found",
  cannot open, can remove), the context-menu commands (Open containing folder and Copy path through services). Then
  the row template with hover and focus actions and the context menu.
- [x] T071e [US5] Recent keyboard (1 unit). Test first: headless `StartPageKeyboardTests`: the search box has the focus
  when the page opens, Down moves into the list, the arrows move, Enter opens, Delete removes and Ctrl+P pins and
  unpins. Then the behaviors and commands (no key handlers in code-behind).
- [x] T071f [US5] Snapshots, E2E and notes. New baselines `start-page-wide`, `start-page-narrow`, each empty and with five
  recent entries (one pinned, one unavailable), looked at before they are accepted; the `StartPageNewProjectTests`
  page objects follow any flow change and the class is run three times; the E5a block in implementation-notes.

### Batch E5b — model: sonnet — T071g–T071k — 8 units (start page redesign, functional; research R18)

- [x] T071g [US5] New project with location and name (2 units). Test first: `NewProjectDialogViewModelTests`: the
  project folder is location/name and the preview shows it, a folder that exists and is not empty is rejected, the last
  location is remembered in the state store and defaults to the documents folder's `NetPrints` folder. Then the dialog
  and the state.
- [x] T071h [US5] One-click samples (2 units). Test first: `SamplesAndWhatsNewTests`: a sample copies to
  `<location>/<SampleName>` with a numeric suffix when it exists, after one confirmation that names the target and
  offers Change; then it opens. Then the confirmation and the flow.
- [x] T071i [US5] Startup behaviour (1 unit). Test first: `StartupBehaviorTests`: "Reopen the last project" opens the
  last project, "Show the start page" does not, a project argument wins, a failed reopen shows the start page with
  `StartPageError`. Then the setting in the state store and the checkbox on the start page.
- [x] T071j [US5] Drop to open (2 units). Test first: headless `ProjectDropTests`: a dropped `.csproj` or folder opens
  after the unsaved-changes confirm; anything else shows `StartPageError`, or a status message while a project is
  open. Then a drop behavior on the main window.
- [x] T071k [US5] Learn card (1 unit). Test first: `StartPageViewModelTests`: the guide, the keyboard shortcuts sheet
  command, the documentation and the release notes go through `IUrlLauncher` with constant URLs. Then the card and
  the E2E update, docs (`docs/guide/projects.md`) and release notes.

### Batch E-R — model: opus — T072 (sub-phase review)

- [x] T072 [US5] [US6] Review sub-phase E: an Opus reviewer who did not implement it reviews the whole diff of batches
  E1–E4 (from the commit before the first batch to HEAD): US5 and US6 end to end against spec.md,
  contracts/state-files.md, contracts/shell.md, ADR-0018 (layout envelope), the constitution and plan.md's standing
  constraints. It runs the independent test of the phase. Findings go to the PR and to implementation-notes under
  "Review E". No code changes in this task.

### Batch E-F — model: sonnet — T073 (reserved: fix review findings)

- [ ] T073 [US5] [US6] Fix every finding of T072 (test first for behaviour findings), reply on each review thread with
  the fixing commit or the reason for deferral, whole suite plus the E2E run, commit; if the review had no findings,
  tick this task with "no findings". The next sub-phase starts only after this batch is green on CI.

---

## Phase 6: User Stories 7 and 8 — navigation; event graphs and entries (sub-phase F, Priority P2)

**Goal**: the command palette, go to anything, connection jumps with a history, connection tooltips and breadcrumbs
(FR-060–FR-065); event graph renames and the event-entry inspector with custom-event arguments (FR-070–FR-074).
Contracts: contributions.md, commands.md, shell.md §7. **Independent test**: US7's and US8's; SC-009; SC-004 (complete).

### Batch F1 — model: sonnet — T074–T076 — 5 units

- [ ] T074 [US7] Navigation history. Test first: `tests/NetPrints.Editor.Tests/Navigation/NavigationHistoryTests.cs`. An
  entry (document, viewport location, zoom, selected node ids) is recorded before each navigation by go-to, an error, a
  connection jump or a tab switch; at most 50 entries each way; a new navigation clears Forward; entries whose document
  no longer exists are skipped; `navigateBack` and `navigateForward` (Alt+Left, Alt+Right, the Go menu) restore the
  graph, the viewport and the selection. Then `src/NetPrints.Editor/Navigation/NavigationHistory.cs`, with recording in
  the shell and the Errors panel.
- [ ] T075 [US7] Command palette (2 units). Test first: `tests/NetPrints.Editor.Tests/Commands/CommandPaletteViewModelTests.cs`.
  Ctrl+Shift+P lists every registered command, filtered with the research R9 ranking (prefix, then word start, then
  substring, then name), with its shortcuts and menu path; a disabled command is listed but Enter does not run it;
  Enter runs the selected command; Esc closes. Then `src/NetPrints.Editor/Commands/CommandPalette/`, ids `Palette.*`.
- [ ] T076 [US7] Go to anything (2 units). Test first: `tests/NetPrints.Editor.Tests/Navigation/GoToAnythingViewModelTests.cs`.
  Ctrl+P queries every registered `IGoToProvider`: the built-in providers for graphs, nodes, variables and methods of
  the open project, and commands after a leading `>`. Results are grouped by `Kind` in the R9 ranking. Enter resolves
  the item's `NavigationTarget`: it opens the graph, centres and selects the item, and records history. Then
  `src/NetPrints.Editor/Navigation/`, ids `GoTo.*`.

### Batch F2 — model: sonnet — T077–T080 — 5 units

- [ ] T077 [US7] Connection navigation. Test first: the end farther from the click point is a pure function with its own
  test; Ctrl+click on a connection moves the view there, selects that node and records history; the connection
  context menu offers `goToSource` and `goToTarget`, labelled with the node and the pin. The gesture lives in
  code-behind only if no behavior fits, and is then listed for E7 (T097).
- [ ] T078 [P] [US7] Tooltip providers. Test first: `tests/NetPrints.Editor.Tests/Navigation/ConnectionTooltipProviderTests.cs`:
  `Node.pin → Node.pin`, the data type or "execution", and the documentation of the members on both ends when
  available (`DocumentationUtil`); providers are asked in `Order`, and the first non-null content wins.
- [ ] T079 [P] [US7] Breadcrumbs. Test first: `tests/NetPrints.Editor.Tests/Navigation/BreadcrumbsViewModelTests.cs`:
  Project › Class › Graph above each graph document, updated on renames; choosing a segment reveals it in the project
  tree. Ids `Breadcrumbs.*`.
- [ ] T080 [US4] `tests/NetPrints.Editor.UITests/Contributions/TestContributionSurfaceTests.cs` (2 units; US4 scenario 6,
  contracts/contributions.md §4): one test contribution of each of the seven kinds appears in its surface exactly as a
  built-in one does (menu, palette and shortcuts sheet; View menu and layout; start page; New project dialog; context
  menu; connection tooltip; go-to results). The T025 pending list now holds only the theme commands (T093). SC-003:
  every built-in command is in a menu, unless it is menu-less by design, and in the palette.

### Batch F3 — model: sonnet — T081–T083 — 5 units

- [ ] T081 [US8] Test first: `tests/NetPrints.Core.Tests/Core/EventGraphRenameTests.cs`. Renaming an event graph is one
  undoable change; a name used by another event graph of the class is refused with
  "An event graph named '<name>' already exists"; the generated C# does not change (goldens byte-identical). Then
  `src/NetPrints.Core/Core/EventGraph.cs` and the editor's undoable rename command.
- [ ] T082 [US8] Custom event entries (2 units). Renaming an entry reuses the rename of Review E R3 (`MemberRename`
  in Core, one undo step through `EditorCommands`): add an event kind there instead of a second rewrite. Test first in `tests/NetPrints.Core.Tests/`: an entry's name is unique
  among the class's methods and entries (P1 FR-027), and a clash is refused with
  "'<name>' is already used by <kind> '<name>'"; `Arguments` (name, `TypeSpecifier`) map to the entry's output pins in
  order, with unique, valid C# identifiers; the translator emits the method with those parameters in that order (a new
  golden fixture, added on purpose and named in the commit). Then `src/NetPrints.Core/Graph/EventEntryNode.cs`, the
  translator, and Core's `PublicAPI.Unshipped.txt`.
- [ ] T083 [US8] Serialization (2 units). Test first in `tests/NetPrints.Core.Tests/Serialization/`: the arguments are an
  optional property of the entry in graph schema v1 (`schemas/netpc.v1.schema.json`), written in canonical order and
  omitted when empty, so graphs without arguments stay byte-identical; a reader without it works; a fixture with
  arguments validates with the `jsonschema` CLI (ADR-0011). Then the `NetPrints.Serialization` mapping and its
  `PublicAPI.Unshipped.txt`.

### Batch F4 — model: sonnet — T084–T087 — 6 units

- [ ] T084 [US8] Event graph inspector and inline rename. Test first: renaming in the inspector, or inline in the tree
  with F2, updates the tree, the tab and the breadcrumbs; a duplicate is refused with the T081 message.
- [ ] T085 [US8] Event entry inspector (2 units). The rename goes through the same rename as T082 (Review E R3). Test first:
  `tests/NetPrints.Editor.Tests/Events/EventEntryInspectorViewModelTests.cs` (contracts/shell.md §7). It shows when an
  entry is selected on the canvas or its graph is opened: name, kind and arguments. For a custom entry: rename (a
  refused clash is shown in the inspector), and add, remove, move up, move down, rename and retype arguments (with the
  existing type picker), each change one undo step labelled "Rename event", "Add argument", "Change argument type"
  and so on. For an override: the name and the base signature, read-only, with "Signature comes from
  <BaseType>.<Method>". Then `src/NetPrints.Editor/Events/EventEntryInspectorViewModel.cs`,
  `EventArgumentViewModel.cs` and the view, ids `Inspector.EventEntry.*`.
- [ ] T086 [US7] [US8] Desktop E2E (2 units). `CommandPaletteTests`: Ctrl+Shift+P, type "comp", Enter compiles.
  `GoToAnythingTests`: Ctrl+P and a node title opens the graph with the node selected; Ctrl+click a connection reaches
  its other end; Alt+Left restores the same view; hovering a connection shows the tooltip text.
  `EventEntryInspectorTests` (SC-009): rename a custom event, add two arguments, compile; the C# panel shows the method
  with both parameters; each change undoes in one step. `KeyboardOnlyTests` gains "find a node and go back" (SC-004).
- [ ] T087 **Checkpoint F**: report SC-009, SC-004 (complete) and SC-003 (complete except the theme commands). Docs
  updated: `docs/guide/graph-format.md` (custom event arguments, optional in v1); release notes (navigation; event
  graph renames and entry arguments; "an older editor drops custom event arguments when it saves the graph").

### Batch F-R — model: opus — T088 (sub-phase review)

- [ ] T088 [US7] [US8] Review sub-phase F: an Opus reviewer who did not implement it reviews the whole diff of batches
  F1–F4 (from the commit before the first batch to HEAD): US7 and US8 end to end against spec.md,
  contracts/contributions.md, contracts/commands.md, contracts/shell.md §7, the schema change (constitution VI,
  canonical output, goldens), the tracked public API, and plan.md's standing constraints. It runs the independent test
  of the phase. Findings go to the PR and to implementation-notes under "Review F". No code changes in this task.

### Batch F-F — model: sonnet — T089 (reserved: fix review findings)

- [ ] T089 [US7] [US8] Fix every finding of T088 (test first for behaviour findings), reply on each review thread with
  the fixing commit or the reason for deferral, whole suite plus the E2E run, commit; if the review had no findings,
  tick this task with "no findings". The next sub-phase starts only after this batch is green on CI.

---

## Phase 7: User Stories 9 and 10, FR-100 — look, scoped search and XAML hygiene (sub-phase G, Priority P3)

**Goal**: type, spacing and colour tokens in Dark and Light, the canvas included, and the theme command (FR-080–FR-082);
icon ids on one vector family, the product mark, the node-header palette with pin and selection tokens, focus,
density and motion tokens, one empty-state control and one dialog shell, checked at high DPI and on a contact sheet
(FR-084–FR-089, ADR-0021); type-scoped search that follows catalogs (FR-090–FR-092); code-behind held to justified
gestures (FR-100).
**Independent test**: US9's and US10's; SC-007, SC-008, SC-011; SC-005's theme part.

**Task ids**: the visual-polish tasks (gap research 2026-10-06) take a letter suffix after the existing task they
follow in execution order (`T090a` runs after `T090`). Existing ids are unchanged, sub-phase G keeps the range
T090–T101, and T112–T114 stay the last tasks of the phase.

### Batch G1 — model: sonnet — T090–T090b — 5 units

- [ ] T090 [US9] Tokens (2 units). The UI polish (ADR-0023) already added hand-made spacing, inspector and node styles
  to `EditorStyles.axaml` and moved the title, section and secondary text classes there; the `Font.*`, `Space.*` and
  `Inspector.LabelColumnWidth` token names and `ThemeTokenTests` do not exist yet, and the Fluent palette that this task
  moves is gone (Semi supplies Light and Dark). Test first: `tests/NetPrints.Editor.UITests/Theming/ThemeTokenTests.cs` (research R11):
  `Font.Caption` 12, `Font.Body` 14, `Font.Subtitle` 16, `Font.Title` 20; `Space.XS` to `Space.XL` as 4, 8, 12, 16, 24
  in `Thickness` and `double` forms; `Inspector.LabelColumnWidth`; every colour token resolves in both
  `ThemeVariant.Dark` and `ThemeVariant.Light`. Then `src/NetPrints.Editor/EditorStyles.axaml` (colour tokens as
  `Area.Role` in `ThemeDictionaries`; a panel header class that is left-aligned and semibold at `Font.Subtitle`,
  replacing the 24 px centred header) and `src/NetPrints.Editor/EditorApp.axaml` (the Fluent palette moves into theme
  dictionaries, and the E2 allowlist entry is removed).
- [ ] T090a [US9] Icon ids on one family (2 units, FR-084, ADR-0021, research R17). Test first:
  `tests/NetPrints.Editor.Tests/Icons/IconRegistryTests.cs`: every `IconIds` constant matches
  `^[a-z0-9]+(\.[a-zA-Z0-9]+)+$` and resolves to a glyph; an unknown id resolves to `IconIds.Unknown` and logs one
  warning (a second lookup of the same id logs nothing); a command, panel or template descriptor with an unknown id
  still registers with no `ContributionIssue`; every built-in descriptor's `IconId` resolves without the fallback
  (`BuiltInCommandTableTests.EveryIconIsAMaterialIcon` becomes `EveryIconIdResolves`). Headless
  `tests/NetPrints.Editor.UITests/Icons/IconPresenterTests.cs`: `IconPresenter` draws a vector glyph for an id at
  `Icon.Small` (16) and `Icon.Medium` (20), filled when `IsActive`, in the inherited foreground in Dark and Light.
  New enforced rule **E9** in `tests/NetPrints.Core.Tests/Core/XamlHygieneTests.cs`: no `MaterialIcon`,
  `SymbolIcon` or `FluentIcon` element and no bitmap `Image` source in `src/**/*.axaml` outside
  `src/NetPrints.Editor/Icons/`, with a shrink-only allowlist seeded with the node-category bitmaps that T092b
  removes (red on today's nine files). Then `src/NetPrints.Editor/Icons/` (`IconIds`, `IconRegistry`,
  `IconPresenter`, its `ControlTheme`, `Log`); `FluentIcons.Avalonia` in `Directory.Packages.props` and
  `src/NetPrints.Editor/NetPrints.Editor.csproj`; the built-in ids cover today's Material kinds, the 16 node
  categories (each PNG's id recorded in `IconIds`' XML docs for T092b), the 13 `NodeVisualKind` glyphs (T091a), the
  three pin kinds, the empty states and the dialogs; `IconKind` becomes `IconId` on `CommandDescriptor`,
  `PanelDescriptor`, `ProjectTemplateDescriptor` and `PanelViewModel` and in
  `src/NetPrints.Editor/Contributions/BuiltIn/`; the nine XAML files use `IconPresenter`; `Material.Icons.Avalonia`
  leaves both files. E5 and E9 go into `.claude/skills/avalonia-xaml/SKILL.md`, the icon rule into
  `.claude/skills/avalonia-styling/SKILL.md`. Snapshots that show icons are re-baselined.
- [ ] T090b [US9] Product mark and third-party notices (1 unit, FR-085). Test first:
  `tests/NetPrints.Core.Tests/Core/BrandAssetTests.cs`: `assets/brand/netprints-mark.svg` has a square `viewBox`;
  `assets/brand/netprints-mark-<n>.png` exists for n = 16, 24, 32, 48, 64, 128 and 256 with that pixel size (read
  from the PNG header); `src/NetPrints.Desktop/NetPrintsLogo.ico` holds 16, 32, 48 and 256 px frames and
  `src/NetPrints.Editor/Assets/NetPrintsLogo.ico` is byte-identical; `assets/icons/netprints-icon.png` (the NuGet
  icon) equals the 128 px export; `THIRD-PARTY-NOTICES.md` at the repository root names Fluent UI System Icons (MIT),
  FluentIcons.Avalonia (MIT), Cascadia Mono (SIL OFL 1.1) and Inter (SIL OFL 1.1), each with its copyright line.
  Then the mark: an SVG of a simple geometric shape (two nodes joined by a wire, in the accent colour) that reads at
  16 px; `eng/brand/export-mark.sh` writes the exports and the `.ico` (run by hand, documented in
  `docs/contributing/`, not in CI); an `App.Mark` `DrawingImage` in `src/NetPrints.Editor/Icons/AppMark.axaml`
  transcribed from the SVG, shown on the start page header and in the About dialog; the docs site's logo and favicon
  (`website/docusaurus.config.ts`, `website/static/img/`) use the SVG and the 32 px export;
  `src/NetPrints.Desktop/NetPrints.Desktop.csproj` copies the notices file to its output and publish directories, and
  About links to it. The owner approves the mark on the G contact sheet (T098b, T100).

### Batch G2 — model: sonnet — T091–T091a — 4 units

- [ ] T091 [US9] Canvas theming (2 units). The UI polish restyled the node pin rows in `NodeView.axaml` with classes
  (`pinLabel`, `nodeTitle`) but not the Nodify `ControlTheme`s or the converters. Nodify `ControlTheme`s `BasedOn` the defaults for `NodifyEditor`, `Node`,
  `Connection`, `Connector` and `ItemContainer`, using `DynamicResource` tokens. `NodeKindBrushConverter`,
  `PinKindBrushConverter` and `GraphBrushes` (`src/NetPrints.Editor/Graph/GraphConverters.cs`) are replaced by style
  classes per node kind and pin kind, and the "Known debt" entry of `.claude/skills/avalonia-styling/SKILL.md` is
  removed. `ThemeTokenTests` covers the canvas tokens; a headless render check runs in both variants.
- [ ] T091a [US9] Node-header roles, kind glyphs, pin and selection tokens (2 units, FR-086). Test first:
  `tests/NetPrints.Editor.UITests/Theming/CanvasPaletteTests.cs`: every `NodeVisualKind`, pure and impure, gets one
  role class (Entry: `Entry`, `Return`; Call: impure `CallMethod`, `CallStatic`; Async: a call whose method returns
  `Task`, `ValueTask` or a generic form; Pure: any other pure node, including `Ternary`, `MakeArray`,
  `MakeDelegate`, `Type` and pure `Default` nodes; Flow: `Default` with execution pins; Variable: `VariableGetter`,
  `VariableSetter`; Constructor; Throw); each `Node.Header.<Role>` token and `Node.HeaderForeground` resolve in Dark
  and Light, and each pair's contrast ratio (WCAG 2.x relative luminance, computed in the test) is at least 4.5:1;
  each node header shows its kind's `IconIds` glyph; `Pin.Exec`, `Pin.Data`, `Pin.Type`, `Pin.Bool`, `Pin.Integer`,
  `Pin.Float`, `Pin.String`, `Pin.Object`, `Pin.ValueType`, `Pin.Delegate` and `Pin.Generic` resolve in both
  variants, and the three pin kinds use the first three; `Canvas.SelectionBorder`, `Canvas.MarqueeFill`,
  `Canvas.MarqueeBorder` and `Canvas.WireSelected` resolve in both variants, and a selected node's border and the
  marquee use them (red: today the border is `#009900` in both). Then the role classes and tokens in
  `src/NetPrints.Editor/EditorStyles.axaml`, the header glyph in `src/NetPrints.Editor/Graph/Nodes/NodeView.axaml`,
  and the role from the node view model (`src/NetPrints.Editor/Graph/Nodes/`). Re-baseline
  `canvas-every-node-kind.png`, `node-call-method.png` and `node-method-entry-parameters.png`. Colouring pins and
  variable headers by data type is P6.

### Batch G3 — model: haiku — T092–T092b — 3 units

- [ ] T092 [US9] Apply the type ramp, the spacing scale and the label column to every view. The UI polish applied the
  label column (`inspectorLabel`, `propertyGrid`) and moved the polish `FontSize` literals into classes; the
  pre-existing literals (`NewProjectDialog`, `ReferencesDialog`, `AboutDialog`, `GraphEditorView` watermark) and rule E8 remain. Test first: a new enforced
  rule **E8** in `tests/NetPrints.Core.Tests/Core/XamlHygieneTests.cs`: no `FontSize` literal in `src/**/*.axaml`
  outside `EditorStyles.axaml`, with a shrink-only allowlist that ends empty (FR-080, research R11).
- [ ] T092a [US9] `Font.Mono` (FR-080). Test first: E8 also rejects a `FontFamily` literal in `src/**/*.axaml` outside
  `EditorStyles.axaml` and `EditorApp.axaml` (red on `CodeView`, `OutputPanelView`, `ErrorDialog`, `TrustDialog`,
  `IssuesDialog` and `KeyboardShortcutsDialog`), and `ThemeTokenTests` asserts that `Font.Mono` is the bundled
  Cascadia Mono (`avares://NetPrints.Editor/Assets/Fonts#Cascadia Mono`) and that a `tabular` style class sets
  `FontFeatures` to `tnum`. Then those six views use `Font.Mono`, and the line and column numbers in Errors and the
  status-bar counts take the `tabular` class.
- [ ] T092b [US9] Replace the raster icons (FR-085). Test first: `NoRasterIconsInSource` in
  `tests/NetPrints.Core.Tests/Core/SourceHygieneTests.cs`: no `*.png` under `src/` (red: the 16 files in
  `src/NetPrints.Editor/Assets/`). Then each of the 21 uses (for example the node categories in
  `BuiltInNodeLibrary.cs`) names the `IconIds` constant that T090a recorded for its PNG; the 16 `*_16x.png` files and
  their `AvaloniaResource` items are deleted; E9's allowlist ends empty. Re-baseline `search-popup.png`.

### Batch G4 — model: sonnet — T092c–T092f — 5 units

- [ ] T092c [US9] Focus, hover and pressed (FR-087). Test first:
  `tests/NetPrints.Editor.UITests/Theming/InteractionStateTests.cs` (headless): keyboard focus on a project-tree
  item, a document tab, a command-bar button, a menu item, an Errors row and the palette's text box shows a ring of
  `Focus.RingThickness` (2) in the `Focus.Ring` brush whose corner radius equals the control's (`Radius.Control`);
  pointer-over and pressed backgrounds equal `State.Hover` and `State.Pressed` in both variants. Then the tokens and
  a focus-adorner template in `src/NetPrints.Editor/EditorStyles.axaml`, and the tab states in
  `src/NetPrints.Editor/Shell/Docking/DockStyles.axaml`. Canvas focus is P6 (M18).
- [ ] T092d [US9] Density and motion (FR-087). Test first: `ThemeTokenTests` asserts `Density.RowHeight` 24,
  `Density.RowPadding` (8, 2), `Density.CommandBarHeight` 36 (at most 40, FR-032), `Motion.Fast` 100 ms,
  `Motion.Normal` 150 ms and `Motion.Easing` (`CubicEaseOut`); `InteractionStateTests` asserts that tree, Errors,
  Output and palette rows have `MinHeight` = `Density.RowHeight`, that buttons, tree and list rows and tabs declare a
  `BrushTransition` on `Background` over `Motion.Fast`, and that the palette and node-search popups fade in with a
  `DoubleTransition` on `Opacity` over `Motion.Normal`. Then the tokens and transitions in `EditorStyles.axaml` and
  `DockStyles.axaml`, and the rows and the command bar in their views. The Compact/Comfortable switch is P6.
  `netprints.enableAnimations` (`NetPrintsSettings.EnableAnimations`, extension-points.md section 7) MUST keep
  working with the `Motion.*` transitions: a test sets it to `false` in a test `ISettings` and asserts that no
  `Transitions` remain on the themed controls, and that with `true` they do.
  Already done by the UI polish (ADR-0023): the setting exists and `EditorApp` calls `DisableTransitions()` when it
  is `false`; the `Motion.*` tokens and the `BrushTransition` rows do not exist yet.
- [ ] T092e [US9] Empty-state control (FR-088). Test first:
  `tests/NetPrints.Editor.UITests/Controls/EmptyStateTests.cs`: Errors with no diagnostics, Output with no lines, the project tree with no project, node search, the palette and
  go-to-anything with no results, and the inspector with no selection each show one `EmptyState` (icon id, one
  sentence, an optional action, an automation id from `AutomationIds`), and the tree's "Open project…" action runs
  `netprints.command.openProject`. Then `src/NetPrints.Editor/Controls/EmptyState.cs` (`IconId`, `Message`,
  `ActionText`, `ActionCommand`) with its `ControlTheme` in `EditorStyles.axaml`; the inspector's own empty text
  (`InspectorPanelView.axaml`) moves to it; T095 uses it.
- [ ] T092f [US9] Dialog shell (2 units, FR-088). The UI polish added the shared dialog styles (`dialogRoot`,
  `dialogTitle`, `dialogAction`, `codeBlock`) but no `DialogShell` control; this task wraps them. Test first:
  `tests/NetPrints.Editor.UITests/Dialogs/DialogShellTests.cs`, one row per P3a dialog (`UnsavedChangesDialog`, `ConfirmDialog`, `KeyboardShortcutsDialog`, `TrustDialog`,
  `IssuesDialog`, `RecoverDialog`, `AboutDialog`, `ErrorDialog`): it is hosted in `DialogShell` with a title and an
  icon id; its buttons follow the platform order (Windows: default first; macOS and Linux: cancel first; the
  platform is injected); Enter runs the default and Esc the cancel (`DialogCloseBehavior`); its width stays between
  `Dialog.MinWidth` (360) and `Dialog.MaxWidth` (640). Then `src/NetPrints.Editor/Dialogs/DialogShell.cs` with its
  `ControlTheme`, and the eight dialogs adopt it; `DialogTests`, `UnsavedChangesDialogTests`, `HelpDialogsTests`,
  `RecoverDialogTests` and `ExtensionDialogTests` stay green; `dialog-error.png` is re-baselined. The older dialogs
  (select method, select type, references) adopt the shell in P6 (M13).

### Batch G5 — model: sonnet — T093–T095 — 4 units

- [ ] T093 [US9] Theme (FR-082). Test first in `tests/NetPrints.Editor.Tests/State/EditorSettingsTests.cs` plus a headless
  test: `theme.dark`, `theme.light` and `theme.system` set `RequestedThemeVariant`; the choice is stored as
  `netprints.editor` → `theme` in `settings.json` through the P1 settings API (`EditorSettings`) and applied at startup
  (Dark by default). This closes SC-005's theme part and empties the T025 pending list.
- [ ] T094 [US10] Type-scoped catalog routing (2 units). Test first in
  `tests/NetPrints.Editor.Tests/Reflection/CompositeReflectionProviderTests.cs` (research R10, SC-007), with
  `tests/Fixtures/Catalog/CatalogAnnotatedLib` (an embedded catalog) and `tests/Fixtures/Extensions/Fx.Catalog` (an
  extension catalog). A type-scoped
  query for a cataloged type returns only the catalog's members (0 unannotated, 0 `[NetPrintsIgnore]`). A type from an
  uncovered assembly returns the same members as before (captured before the change). A covered assembly whose
  catalog does not list the type returns none, with the reason "hidden by catalog <id>". Inherited members come from
  the source that covers the base type's assembly. Project types are always live. `GetTypeFromSpecifier` and the
  binding of existing nodes are unchanged, and a graph that uses a hidden member builds and runs with unchanged
  output. Then `src/NetPrints.Reflection/Catalogs/CompositeReflectionProvider.cs`, and Reflection's
  `PublicAPI.Unshipped.txt` if its public API changes.
- [ ] T095 [US10] The scoped search's empty state names the hiding catalog (FR-091), through the `EmptyState` control
  (T092e, FR-088): `src/NetPrints.Editor/Search/SuggestionListViewModel.cs` and its view; headless test first.

### Batch G6 — model: sonnet — T096–T097 — 4 units

- [ ] T096 [US9] [US10] Desktop E2E (3 units, FR-102). `ThemeSwitchTests`: View › Theme › Light changes the canvas and a
  pane to their Light token values, and the choice survives a restart; in Light, a Call node's header has the Light
  `Node.Header.Call` value, the command bar's icons are drawn by `IconPresenter` with no fallback glyph, and the empty
  Errors panel shows its `EmptyState`. `DialogShellKeysTests`: with an unsaved change, File › Close project opens the
  Unsaved changes dialog in the shell with its buttons in the Linux order; Esc cancels and the project stays open;
  Enter on the reopened dialog saves and closes. `TypeScopedSearchTests`: in a project that references
  `CatalogAnnotatedLib`, search from a pin of a cataloged type lists only the catalog's members.
- [ ] T097 Test first: rule **E7** in `tests/NetPrints.Core.Tests/Core/XamlHygieneTests.cs` (research R12, FR-100). Every
  method in `src/**/*.axaml.cs` with an `(object? sender, <…>EventArgs e)` signature, or overriding an `On*` member,
  must be listed in a shrink-only `CodeBehindAllowlist` with its file, its method, a reason category (gesture,
  viewport math, editor interop or focus plumbing) and one line of reason; stale entries fail. Seed the allowlist with
  today's handlers and their reasons. Add an amendment note for E7, E8 and E9 to `docs/adr/0007-xaml-practices.md`,
  and document them in `.claude/skills/avalonia-xaml/SKILL.md`.

### Batch G7 — model: sonnet — T098–T099 — 5 units

- [ ] T098 Migrate code-behind (2 units): every handler a prebuilt behavior covers (the catalog in
  `.claude/skills/avalonia-behaviors/`), and the rest that a custom behavior can express (ADR-0007 D11), across
  `GraphEditorView`, `CodeView`, `LocalVariableView`, `MemberVariableView`, `EditorApp.axaml.cs` and every new P3a
  view; remove their E7 entries. What stays is a gesture, viewport math, editor interop or focus plumbing. Each
  migration keeps its tests green, and each new behavior gets a headless test.
- [ ] T098a [US9] High-DPI snapshots (FR-089). Test first:
  `tests/NetPrints.Editor.UITests/Snapshots/ScaledSnapshotTests.cs`: `editor-shell-main`, `canvas-every-node-kind`,
  `inspector-method` and the Unsaved changes dialog at render scaling 1.0, 1.5 and 2.0 in Dark and Light (24
  baselines named `<name>-<dark|light>-<scale>.png`, using the scaling hook of `grid-background-150`); red because no
  baseline exists, then written once with `NETPRINTS_UPDATE_SNAPSHOTS=1` and reviewed by hand. At 2.0 a visual-tree
  scan finds no `Image` backed by a `Bitmap`.
- [ ] T098b [US9] Contact sheet (FR-089). Test first:
  `tests/NetPrints.Editor.UITests/Snapshots/ContactSheetTests.cs` (red: the composer is a stub that throws): every
  panel (project tree, inspector, variables, Errors, Output, C#), the start page, a graph document and the eight P3a
  dialogs, in Dark and Light at 1.0 and 2.0, render into one labelled grid, `contact-sheet.png`, in the test results
  directory; the test asserts the tile count (16 × 2 × 2) and that no tile is one flat colour. It compares no
  baseline. Then `.github/workflows/ci.yml` uploads the file as the `contact-sheet` artifact of the UI tests job
  (`CiWorkflowTests` updated).
- [ ] T099 **Checkpoint G**: report SC-007, SC-008 (tokens in both variants, no colour literal, no type name ending in
  `VM`, the E1–E6, E8 and E9 allowlists empty, every E7 entry justified), SC-011 (with the contact sheet's CI run id)
  and SC-005's theme part. Docs updated: ADR-0007's amendment, ADR-0021, `THIRD-PARTY-NOTICES.md`, the brand export
  page in `docs/contributing/`, the `avalonia-*` skills, release notes (Light and System themes; vector icons and the
  new product mark; focus rings, empty states and one dialog layout; type-scoped search follows catalogs).

### Batch G-R — model: opus — T100 (sub-phase review)

- [ ] T100 [US9] [US10] Review sub-phase G: an Opus reviewer who did not implement it reviews the whole diff of batches
  G1–G7 (from the commit before the first batch to HEAD): US9, US10 and FR-100 end to end against spec.md
  (FR-080–FR-092, FR-100), research R10–R12 and R17, ADR-0007, ADR-0021 and the `avalonia-*` skills, the constitution
  and plan.md's standing constraints. It runs the independent test of the phase. Visual review: it downloads the
  `contact-sheet` artifact of the last G CI run, attaches it to the review, and checks icon consistency, contrast,
  alignment, density, focus rings, both themes and 200 %; the owner approves the product mark there. Findings go to
  the PR and to implementation-notes under "Review G", a visual finding with the tile it refers to. No code changes
  in this task.

### Batch G-F — model: sonnet — T101 (reserved: fix review findings)

- [ ] T101 [US9] [US10] Fix every finding of T100, visual findings included (test first for behaviour findings), reply
  on each review thread with the fixing commit or the reason for deferral, and attach a new contact sheet to the reply
  when a fix changes what it shows; whole suite plus the E2E run, commit; if the review had no findings, tick this
  task with "no findings". The next sub-phase starts only after this batch is green on CI.

---

## Phase 8: Docs and polish (sub-phase H)

**Goal**: the six screenshot editor guides from a scripted run, the docs site green, the quickstart run, the full
suite, the final review and merge preparation (FR-103, SC-010, every SC).

### Batch H1 — model: sonnet — T102–T104 — 4 units

- [ ] T102 Guide screenshots (2 units): `tests/NetPrints.Desktop.E2ETests/Scenarios/GuideScreenshotTests.cs` (opt-in with
  `NETPRINTS_GUIDE_SHOTS=1`, skipped with a stated reason otherwise) drives a copy of the sample through each guide's
  states and writes PNGs to `website/static/img/guide/editor/`. `scripts/guide-screenshots.sh` builds, starts its own
  Xvfb display (never `:1`), runs the class and lists the files. Run it and commit the images.
- [ ] T103 [P] `docs/guide/editor/_category_.json`, `shell.md` (layout, panels, docking, floating, Reset layout) and
  `start-page.md` (recent projects, templates, samples, what's new), each with T102 screenshots and every claim checked
  against the running editor.
- [ ] T104 [P] `docs/guide/editor/saving-and-recovery.md` (markers, the prompt, backups and where they live, recovery,
  nothing written into the project) and `commands-and-shortcuts.md` (menus, the command bar, the palette, and the
  shortcut table between marker comments), each with screenshots.

### Batch H2 — model: sonnet — T105–T107 — 5 units

- [ ] T105 `docs/guide/editor/navigation.md` (go to anything, the palette, connection jumps, history, tooltips,
  breadcrumbs) and `event-graphs.md` (renames, the entry inspector, arguments, overrides), each with screenshots.
- [ ] T106 Docs tests (2 units). `tests/NetPrints.Editor.Tests/Docs/ShortcutGuideTests.cs`: the table between the markers
  of `docs/guide/editor/commands-and-shortcuts.md` equals the table generated from the registry, and
  `NETPRINTS_UPDATE_SNAPSHOTS=1` rewrites it (red first against a deliberately stale row). `GuidePagesTests`: each of
  the six pages exists and references at least one PNG that exists under `website/static/img/guide/editor/` (SC-010).
- [ ] T107 Docs pass (2 units): `docs/guide/projects.md`; `docs/contributing/testing.md` (final); `README.md` (editor
  features and a screenshot); the Status of ADR-0018 (with the spike outcome), ADR-0019 and ADR-0020, and the
  `docs/adr/README.md` index; the `avalonia-*` skills; `src/NetPrints.Editor/StartPage/WhatsNew.md` and
  `.github/release-notes.md` "Unreleased" (every user-visible P3a change, the schema note, links to the new guides);
  the API reference (`docs/api`, docfx) builds with the Core, Serialization and Reflection changes. The guide also
  documents a hand-written `Properties/launchSettings.json` (`commandLineArgs`, `environmentVariables`) for F5 and
  `netprints run`. Test first: an integration test proves that the run command (`dotnet run --project ... --no-build`,
  from `MsBuildProjectSystem.GetRunCommand`) applies a profile's `commandLineArgs` and `environmentVariables` to an echo
  fixture.

### Batch H3 — model: sonnet — T108–T111 — 5 units

- [ ] T108 `scripts/build-docs.sh` with 0 broken links; the site shows the six guides with their screenshots and
  ADR-0018 to ADR-0020 (SC-010).
- [ ] T109 Run `specs/005-editor-shell/quickstart.md` end to end on your own Xvfb display with `NETPRINTS_STATE_DIR` in a
  temporary folder (2 units); record the outcome of each section in implementation-notes; `git status samples/` is
  clean.
- [ ] T110 Whole suite in Release plus the Desktop E2E run (AGENTS.md two-run split); `dotnet build -c Release` with 0
  warnings; `dotnet format NetPrints.slnx --verify-no-changes`; `dotnet format analyzers --severity info
  --verify-no-changes` over the changed files; SC-006 checked again on the latest CI run.
- [ ] T111 **Checkpoint H**: the SC-001…SC-010 table with evidence in implementation-notes; the "Roadmap P3a coverage"
  table below checked row by row; the governance proposals; the PR description updated; the PR is ready for the final
  review (T112).

### Batch H-R — model: opus — T112 (final PR review)

- [ ] T112 Final, lighter Opus review of the whole PR before merge, by a reviewer who did not implement it. It covers
  integration across sub-phases, not each sub-phase again: the registry ↔ every surface (menus, bar, keys, palette,
  sheet, context menus, tooltips, go-to, start page, templates); `IShell` ↔ the Dock adapter ↔ layout persistence ↔
  sessions; the lifecycle ↔ the five unload paths ↔ backups ↔ the state directory; the schema's optional property ↔
  older readers ↔ the release notes; the type-scoped search ↔ the P2 catalogs; the CI matrix and the Windows workflow;
  docs ↔ behaviour; and the SC table of the Checkpoint H report. Findings go to the PR and to implementation-notes
  under "Final review".

### Batch H-F — model: sonnet — T113 (reserved: fix final review findings)

- [ ] T113 Fix every finding of T112 (test first for behaviour findings), reply on each thread, whole suite plus the
  Desktop E2E run, commit; tick with "no findings" if there were none.

### Batch H-M — model: sonnet — T114 (merge preparation)

- [ ] T114 Merge `origin/master` into `005-editor-shell` (a merge commit, no rebase; resolve conflicts keeping both
  sides' intent), whole suite plus the Desktop E2E run, push, and wait until `CI` and `CLI (Windows)` are green. Update
  the PR description (summary, decisions, deferrals, a link to the SC table), then mark the PR ready
  (`gh pr ready 12 --repo danielmeza/netprints`). The owner or the coordinator merges; the roadmap and constitution
  edits after the merge belong to the coordinator.

---

## Dependencies and execution order

- Batches run strictly in order: A1 → A2 → A3 → A4 → A-R → A-F → B1 → … → H3 → H-R → H-F → H-M (one agent at a time,
  AGENTS.md). A review batch reads the finished sub-phase, and its fix batch closes it.
- A1 comes first, so every new P3a type follows the naming rule and nothing is renamed twice. A2 comes before any E2E
  rewrite, so every later E2E failure has diagnostics.
- B precedes C, so the shell generates its menus, command bar and key bindings from the registry from the start.
- C1's gate decides the adapter used by T033 and T064. C removes the old windows (T044) only after every replaced
  scenario is green on the shell (T043).
- D, E, F and G each need C and are otherwise independent, apart from these links: D's T051 (paths, atomic writes)
  serves E's state store (T061); D's `KeyboardOnlyTests` (T057) grows in F (T086); the T025 pending list empties in F
  (T080) and G (T093); SC-005's theme part closes in G (T093).
- Inside G, the icon ids (T090a) come before every task that draws an icon or a glyph (T090b, T091a, T092b, T092e,
  T092f), and the empty-state control (T092e) comes before the catalog message that uses it (T095). The product mark
  and the empty states use E's start page and F's palette and go-to-anything. The contact sheet (T098b) runs once the
  visual tasks are done, and the G review (T100) attaches it.
- H comes last because the screenshots need a stable UI.
- Within a batch, [P] tasks touch different files; tasks without [P] run in order.

## Parallel opportunities (inside a batch, for a single agent)

- C3: T036 ∥ T037 ∥ T038. E1: T062 ∥ T063. F2: T078 ∥ T079. H1: T103 ∥ T104.

## Implementation strategy

1. MVP = Checkpoint D (US1–US4): diagnosable CI, the single window, no lost work, and commands with shortcuts.
2. Then the start page and persistence (E), navigation and the event inspector (F), and the look, scoped search and
   XAML hygiene (G), each an increment on top of the MVP, in story-priority order.
3. H closes the phase: guides with scripted screenshots, the docs site, the quickstart, then the final review (T112),
   its fixes (T113) and the merge preparation (T114).
4. Every sub-phase X is closed by its review (X-R) and fix batch (X-F), so defects are caught per story, and the final
   review checks only integration.

## Task counts

| Sub-phase | Tasks | Implementation batches | Review, fix, merge |
|---|---|---|---|
| A — naming, CI and test infrastructure | 16 (T001–T016) | A1 haiku, A2–A4 sonnet | A-R, A-F |
| B — registry and commands core | 13 (T017–T029) | B1–B4 sonnet | B-R, B-F |
| C — shell | 18 (T030–T047) | C1–C5 sonnet | C-R, C-F |
| D — lifecycle and feedback | 13 (T048–T060) | D1–D3 sonnet | D-R, D-F |
| E — start page and persistence | 13 (T061–T073) | E1–E4 sonnet | E-R, E-F |
| F — navigation and event inspector | 16 (T074–T089) | F1–F4 sonnet | F-R, F-F |
| G — look, search and hygiene | 23 (T090–T101, with T090a–b, T091a, T092a–f, T098a–b) | G1, G2, G4–G7 sonnet; G3 haiku | G-R, G-F |
| H — docs and polish | 13 (T102–T114) | H1–H3 sonnet | H-R, H-F, H-M |
| **Total** | **125** | 34 | 17 |

Sub-phase G is 30 units in seven implementation batches (G1 5, G2 4, G3 3, G4 5, G5 4, G6 4, G7 5), up from 15 units
in three: the visual-polish tasks add 14 units and T096 grows by one. The roadmap's estimate for the growth is about
5–7 days.

## Requirement coverage

| Requirement | Tasks |
|---|---|
| FR-001–FR-003 | T003–T006 |
| FR-004 | T007–T009 |
| FR-005, FR-006 | T007, T010–T012 |
| FR-010 | T033–T035, T040, T044 |
| FR-011 | T036 |
| FR-012 | T039 |
| FR-013 | T037 |
| FR-014 | T038 |
| FR-015, FR-016 | T033, T039, T043 |
| FR-017 | T041 |
| FR-018 | T040, T050 |
| FR-020 | T020, T048 |
| FR-021, FR-083 | T034, T049 |
| FR-022 | T050, T056 |
| FR-023 | T049 |
| FR-024 | T051, T052 |
| FR-025 | T053, T056 |
| FR-026 | T052 |
| FR-030 | T017–T018, T021–T025 |
| FR-031, FR-032 | T035 |
| FR-033 | T022 |
| FR-034 | T026, T057 |
| FR-035 | T020, T054 |
| FR-036 | T055 |
| FR-037 | T017, T018, T025, T080 |
| FR-040 | T066 |
| FR-041 | T062, T066 |
| FR-042 | T067 |
| FR-043, FR-044 | T068, T107 |
| FR-045, FR-046 | T071a–T071c, T071f |
| FR-047 | T071d, T071e, T071f |
| FR-048 | T071g–T071i |
| FR-049 | T071j, T071k |
| FR-050, FR-051 | T061, T063–T065, T070, T093 |
| FR-052 | T051, T061 |
| FR-060 | T075 |
| FR-061 | T076 |
| FR-062 | T077 |
| FR-063 | T074 |
| FR-064 | T078 |
| FR-065 | T079 |
| FR-070 | T081, T084 |
| FR-071, FR-074 | T085 |
| FR-072 | T082, T085 |
| FR-073 | T082, T083, T085 |
| FR-080 | T090, T092, T092a |
| FR-081 | T091, T091a |
| FR-082 | T093 |
| FR-084 | T090a, T096 |
| FR-085 | T090b, T092b |
| FR-086 | T091a, T096 |
| FR-087 | T092c, T092d |
| FR-088 | T092e, T092f, T095, T096 |
| FR-089 | T098a, T098b, T100 |
| FR-090, FR-092 | T094 |
| FR-091 | T094, T095 |
| FR-100 | T097, T098 |
| FR-101 | T035 (accessible names, keyboard reach); ids in every view task; checked by every review |
| FR-102 | T006, T043, T056, T057, T069, T070, T086, T096 |
| FR-103 | T102–T108 |
| FR-104 | constraint: no performance task; every review checks it |
| FR-105 | T001, T002 |
| SC-001 | T043 |
| SC-002 | T050, T056 |
| SC-003 | T025, T035, T057, T080, T093 |
| SC-004 | T057, T086 |
| SC-005 | T064, T065, T070, T093 |
| SC-006 | T006, T009, T012, T014, T110 |
| SC-007 | T094 |
| SC-008 | T001, T090, T091, T092, T092a, T097, T098 |
| SC-009 | T086 |
| SC-010 | T102–T108 |
| SC-011 | T090a, T091a, T092b, T098a, T098b, T099, T100 |

## Roadmap P3a coverage

Every bullet of the roadmap's P3a section (`.specify/memory/roadmap.md`) maps to tasks or to a recorded deferral.

| Roadmap P3a item | Tasks | Recorded deferral |
|---|---|---|
| CI and test infrastructure (FU-3, FU-4, FU-7) | T003–T014 | — |
| View model naming (owner decision 2026-10-01) | T001, T002 | — |
| Layout: one window, tree, tabs, inspector, bottom panel (H2) | T030–T045 | — |
| Commands: registry, command bar, menu, shortcuts (H3, H4); the P3 contribution point | T017–T026, T035, T054–T057 | user-defined shortcuts → P3 settings pages (R5) |
| Document lifecycle (H1): dirty flag, `*`, prompt on close, autosave or backup | T020, T048–T056 | autosaving the original files is not offered in P3a (R6, spec Clarifications) |
| Visual system (M3, L1): tokens, Nodify theme overrides | T090–T093 | L1 custom title bar → P6 (R11, R14); P3a sets the title text (T034) |
| Visual polish in sub-phase G (gap research 2026-10-06, C-1 to C-16 as approved) | T090a–T090b, T091a, T092a–T092f, T098a–T098b, T100 | behaviour halves (type-coloured pins and headers, wire highlight, Compact switch, reduce motion, older dialogs) → P6; C-5 title bar and C-9 wires → P6; C-15 status-bar contribution kind → P3 |
| Persistence (L2): layout, tabs, zoom, window position | T061–T065, T070 | — |
| Undo feedback (M16, shared with P6) | T020, T054 | undo history list → P6 (R5, R14) |
| Docking on Dock.Avalonia, with the compatibility and testability checks | T030–T033, T064 | — |
| Start dashboard: recent, open, new from templates, samples, what's new | T062, T066–T069 | UnrealSharp template → NetPrintsUnreal U1 through the template contribution point that T067 proves (R8); Unity → later; Velopack update notice → later (R14) |
| Internal contribution points (seven kinds, used by the built-in editor) | T017, T018, T025, T034–T036, T066, T067, T076–T078, T080 | public contribution API → P3 (R14) |
| Type-scoped search and embedded catalogs (E-R15) | T094–T096 | "show all members" toggle and hidden-member warnings → P6 (R10, R14) |
| Adopt Xaml.Behaviors; allowlists empty or only justified gestures | T090, T092, T097, T098 | — |
| Navigation basics: palette, go to anything, connection jumps, history, tooltips, breadcrumbs | T074–T079, T086 | mouse back and forward buttons → P6 (R9) |
| Event graph and entry inspector | T081–T086 | argument modifiers → P3b (roadmap) |
| Editor guides with screenshots | T102–T108 | — |
| Done when: docs updated (guides, API reference, ADRs) | each checkpoint (T014, T027, T045, T058, T071, T087, T099, T111), T107 | — |
| P2 code carry-overs (FU-1, FU-2, FU-5, FU-6) | — | stay in P3 (spec Clarifications, R3) |
