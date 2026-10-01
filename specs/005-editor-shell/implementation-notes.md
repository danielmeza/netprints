# Implementation Notes: P3a — Editor shell

## Decisions

Decisions taken during implementation go here as "Decision: …", one line each, with the task id. Batch S2's
decisions are listed in its entry below.

## Batch S2 (specification: tasks, analyze, constitution 1.2.4)

### Task counts

`tasks.md` has 114 tasks: 30 implementation batches, 8 sub-phase reviews, 8 reserved fix batches and the merge
preparation.

| Sub-phase | Tasks | Batches |
|---|---|---|
| A — naming, CI and test infrastructure | 16 (T001–T016) | A1 haiku (T001–T002), A2 (T003–T006), A3 (T007–T009), A4 (T010–T014), A-R, A-F |
| B — registry and commands core | 13 (T017–T029) | B1 (T017–T019), B2 (T020–T022), B3 (T023–T025), B4 (T026–T027), B-R, B-F |
| C — shell | 18 (T030–T047) | C1 (T030–T032), C2 (T033–T035), C3 (T036–T038), C4 (T039–T041), C5 (T042–T045), C-R, C-F |
| D — lifecycle and feedback | 13 (T048–T060) | D1 (T048–T050), D2 (T051–T053), D3 (T054–T058), D-R, D-F |
| E — start page and persistence | 13 (T061–T073) | E1 (T061–T063), E2 (T064–T065), E3 (T066–T068), E4 (T069–T071), E-R, E-F |
| F — navigation and event inspector | 16 (T074–T089) | F1 (T074–T076), F2 (T077–T080), F3 (T081–T083), F4 (T084–T087), F-R, F-F |
| G — look, search and hygiene | 12 (T090–T101) | G1 (T090–T092), G2 (T093–T096), G3 (T097–T099), G-R, G-F |
| H — docs and polish | 13 (T102–T114) | H1 (T102–T104), H2 (T105–T107), H3 (T108–T111), H-R, H-F, H-M |

Implementation batches are sonnet except A1 (haiku, the mechanical rename); reviews are opus.

### Analyze findings and resolutions

speckit-analyze found 24 findings: 1 critical, 2 high, 11 medium, 10 low. Coverage was 100% of the functional
requirements with buildable work and of the success criteria. All were fixed in this batch.

| ID | Severity | Finding | Resolution |
|---|---|---|---|
| D1 | critical | `cli-windows.yml` contradicts the constitution's "single" Windows exception | Constitution 1.2.4 (PATCH); plan.md marks the proposal accepted; ADR-0019 notes it |
| I4 | high | contracts/ci.md: "rethrown unchanged" conflicts with "message prefixed" | The test fails with `E2EStepFailureException` (prefixed message, original as `InnerException`) |
| E1 | high | FR-102: no E2E scenario for US9 and US10 | `ThemeSwitchTests` and `TypeScopedSearchTests` (R13, plan, T096) |
| I1 | medium | 24 `*VM` types stated; the code has 25 | 25 in spec, plan, research and roadmap; T001 lists them |
| I2 | medium | state-files.md session example used graph keys that contradict shell.md §2 | Example uses `#method:<id>` and `#event:<id>` |
| I3 | medium | FR-021 title order differs from contracts/shell.md §4 | FR-021 points to the contract's format |
| I6 | medium | quickstart §10 names `CatalogFixtureLib` as the annotated library | `CatalogAnnotatedLib` (the embedded-catalog fixture) |
| I11 | medium | Backup pruning per backup (FR-024) vs per folder (contract) | Per backup by `writtenUtc`; empty or orphaned folders removed (contract, R6) |
| I13 | medium | `NETPRINTS_E2E_FORCE_TIMEOUT` as a process-wide switch would hang parallel workers | Per-test harness hook; the variable only for manual runs (ci.md, R3, quickstart) |
| E2 | medium | Context-menu targets do not say what happens to items that do not apply | Items whose command cannot execute for the target are hidden (contributions.md §3) |
| E3 | medium | The Windows path filter missed projects the CLI tests build | Filter = transitive `ProjectReference` closure, listed in ci.md §2 and checked by `CiWorkflowTests` |
| B1 | medium | SC-006 "longest Linux job … on the same commit" is ambiguous and unmeasurable | Longest of the jobs that replace "Build and test (Linux)", against CI run 36819424717 (`3a6eafc`) |
| B2 | medium | FR-080 "every view uses the tokens" had no check | New enforced rule E8 (no `FontSize` literal outside `EditorStyles.axaml`; R11, R12, T092) |
| C1 | medium | B's handlers need the open project before C builds the shell | `ProjectSessionViewModel` extracted in B (T021; plan) |
| I5 | low | ci.md said `e2e` uploads results only on failure | It uploads on every run (`if: always()`) |
| I7 | low | Plan: implementation-notes created by the first implementation task | Created by S2 |
| I8 | low | Plan: Dock-specific tasks rewritten "in the C fix batch" (after C) | Rewritten before batch C2 (T032) |
| I9 | low | data-model `IShell` lacked `DockDocument` | Added |
| I10 | low | Plan did not state the task count | 114 tasks |
| I12 | low | commands.md listed Alt+F4 as `exit`'s shortcut | The OS closes the window; no registered gesture |
| I14 | low | Types the tasks need were not in the plan or data model | `RunStateTracker`, `EditorDataPaths`, `AtomicFileWriter`, `ProjectSessionViewModel` added |
| I15 | low | Batch sizing differed between the plan and the coordinator's rule | One rule: 3–6 units, 2–4 tasks when design-heavy, up to 6 light or mechanical |
| B3 | low | FR-035 "a few seconds" vs 4 s in the contract | FR-035 says 4 seconds |
| B4 | low | SC-002's E2E would wait the full 30 s backup interval | Test-only `NETPRINTS_BACKUP_DELAY` (R6, state-files.md) |

The full report is kept outside the repository (`.agent-archive/netprints-p3a/analyze-s2.md`).

### Decisions

- Decision: the view model rename covers 25 types (survey 2026-10-01), not 24; historical specs 001–004 are not rewritten.
- Decision: `ProjectSessionViewModel` is extracted from `MainEditorViewModel` in sub-phase B (T021), before the shell.
- Decision: a shrink-only `PendingCommandIds` list lets the contract-table test (T025) pass while commands land across sub-phases; it is empty after T093.
- Decision: E2E failures are reported as `E2EStepFailureException` wrapping the original; the forced timeout is a per-test hook, and `NETPRINTS_E2E_FORCE_TIMEOUT` is only for manual runs.
- Decision: `CiWorkflowTests` reads the workflows with a small reader inside the test; no YAML package is added.
- Decision: the Windows CLI workflow's path filter is the transitive `ProjectReference` closure of `NetPrints.Cli.Tests`, checked by `CiWorkflowTests`.
- Decision: SC-006's baseline is CI run 36819424717 (master `3a6eafc`), compared with the longest of `checks` and the `test` legs.
- Decision: the Dock spike runs behind `NETPRINTS_DOCK_SPIKE=1` and a throwaway E2E class, both removed by T033.
- Decision: `NETPRINTS_BACKUP_DELAY` (test-only) shortens the backup wait for `CrashRecoveryTests`.
- Decision: backups are pruned per file by `writtenUtc`; a backup folder goes when it is empty or its project no longer exists.
- Decision: a context-menu item whose command cannot execute for its target is hidden.
- Decision: a new enforced rule E8 (no `FontSize` literal in views outside `EditorStyles.axaml`) makes FR-080's type ramp checkable; it joins E7 in ADR-0007's amendment (T097), so no new ADR.
- Decision: `ThemeSwitchTests` and `TypeScopedSearchTests` give US9 and US10 their E2E main flows (FR-102).
- Decision: every checkpoint adds its user-visible changes to `.github/release-notes.md` "Unreleased"; `WhatsNew.md` is written from it in T107.
- Decision: only the rename batch (A1) runs on haiku; if it cannot get green, the coordinator re-runs it on sonnet.
- Decision: `.github/release-notes.md` was reset to an empty "Unreleased" section (the v0.2.0 notes shipped with the tag); the "Downloads" template stays.

## Batch A1 (implementation: haiku, 70b533a–48cc09d)

### Red output (T001 test failure before rename)

```
ClassEditorVM
CodeViewVM
ConnectionVM
DeclaredReferenceVM
DiagnosticRowVM
DialogVM
ErrorDialogVM
ErrorListVM
EventGraphVM
GetSetChooserVM
IssuesDialogVM
LocalVariableVM
MainEditorVM
MemberVariableVM
MethodVM
NodeGraphVM
NodePinVM
NodeVM
PinRowVM
ReferenceListVM
SelectMethodDialogVM
SelectTypeDialogVM
SuggestionListVM
TrustDialogVM
VariablesPanelVM
```

### Green evidence (T001–T002)

- Build: 40 projects, 0 errors, 0 warnings (Release)
- Full suite: 1727 tests, 1717 passed, 10 skipped, 0 failed (5m 12s)
- E2E suite: 9 tests, 9 passed, 0 skipped, 0 failed (2m 31s)
- Docs sweep: the first grep missed the UITests `ClassVM`/`GraphVM` members and two skill sketches (`ItemVM`, `ListVM`, `XDesignVM`); found and fixed in the follow-up commit. git grep now finds only MVVM and `NoTypeNameEndsInVM`
- Commits: T001 (70b533a, 112 files), T002 (48cc09d, 16 files)

## Batch A2 (implementation: sonnet, f87275a–eda3181 and the T006 commit)

### Red output

Both test-first steps were red as compile errors (the types did not exist yet):

```
FailureCaptureTests.cs(16,20): error CS0246: The type or namespace name 'CapturePart' could not be found
E2EDiagnosticsTests.cs(16,32): error CS0115: 'E2EDiagnosticsTests.ForcedTimeoutStep': no suitable method found to override
E2EDiagnosticsTests.cs(18,33): error CS0115: 'E2EDiagnosticsTests.Budget': no suitable method found to override
RunStateTrackerTests.cs(102,22): error CS0103: The name 'RunPhase' does not exist in the current context
RunStateTrackerTests.cs(110,33): error CS0246: The type or namespace name 'RunStateTracker' could not be found
RunStateTrackerTests.cs(143,52): error CS0117: 'AutomationResponse' does not contain a definition for 'RunState'
```

The E2E test went red a second time on a real defect, after the code compiled: the open step was already closed by its
`using` when the failure reached the scenario base (`[step '(none)' running for 0 s]`), and the first run also hit a
static-initializer order bug in `StepTimer`. Both fixed (see decisions).

### Green evidence (T003–T006)

- Build: 40 projects, 0 errors, 0 warnings (Release); `dotnet format --verify-no-changes` clean
- Full suite (Release, solution-wide): 1746 tests, 1735 passed, 11 skipped, 0 failed (5m 13s); the first run had one failure, `SourceHygieneTests.NoNullForgivingOperator`, fixed as listed below
- `FailureCaptureTests` 8/8, `RunStateTrackerTests` 10/10 (including the `runState` serialization contract), `MainEditorVMTests` 20/20
- E2E suite: 18 tests, 18 passed, 0 skipped, 0 failed (2m 24s) = 9 scenarios + 8 `FailureCaptureTests` + `E2EDiagnosticsTests`
- `E2EDiagnosticsTests` output folder: `summary.md`, `timings.md` (`open project (running)`), `ui-tree.json`, `display.png`, `editor.log`, `process.txt` (`running`), `run-state.json`; no `capture-errors.txt`
- Commits: bf000fc (A1 naming follow-up), f87275a (T003, T004), eda3181 (T005), then T006 with these notes

### Decisions

- Decision: the failing step is taken from `AppDomain.FirstChanceException` (step open at the first throw, keyed by exception instance), because the scenarios' `using (Step(...))` blocks have closed it by the time the failure reaches the base class.
- Decision: `IProcessLauncher` gains `ProcessStarted`, `LineReceived(stream, line)` and `ProcessExited(code)` beside `OutputReceived`; the real launcher calls `WaitForExit()` in its exit handler so every line precedes the exit.
- Decision: `EditorContext` gains a required `RunState` member and the compile flow calls `BuildStarted`/`BuildFinished`; `AutomationAgent` takes the snapshot provider as an optional trailing argument so the existing agent tests are unchanged.
- Decision: a stale exit of an earlier program is ignored unless the tracker is `running`; two programs running at once are not tracked separately.
- Decision: the capture runs its parts concurrently, each with its own 10 s limit and a 30 s guard over all of them, so a hung part costs 10 s, not the sum.
- Decision: the editor exiting while a test holds its lease cancels the test's token (`kind: editor exited`) instead of waiting out the 180 s budget; it is covered by the shared code path but has no dedicated test.
- Decision: `ui-tree.json` comes from a new read-only `tree` automation request (every window and every control with an automation id, hidden ones included); `dump` stays text.
- Decision: the `e2e` job's artifact paths list `TestResults/e2e-diagnostics/**` (the job uploads named globs, not all of `TestResults/`).
- Decision: three allowlisted null-forgiving operators (`AutomationClient.StatusAsync`, `EditorProcess.DesktopAssembly`, `X11SmokeTestBase.CheckpointAsync`) were replaced by explicit guards and their entries removed from `SourceHygieneTests`, instead of moving the line numbers.
- Decision: issue #11 (`EditCompileAndRun`): no speculative fix. The scenario is rewritten on the shell in T043; if it fails again, `run-state.json`, `timings.md` and `editor.log` say whether the editor was slow to start, the build hung or the output was lost.

## Checkpoint reports

Each checkpoint task (T014, T027, T045, T058, T071, T087, T099, T111) adds its report here, with evidence for every
success criterion its sub-phase covers and a "Docs updated:" line.

## Deviations

None yet.

## Governance proposals

- Constitution PATCH 1.2.3 → 1.2.4 (Development Workflow: path-filtered Windows or macOS legs that extend `CI`) was
  applied in batch S2 on the coordinator's instruction; the owner confirms it on PR #12.
- Roadmap: the phases table now records P2 as released (`v0.2.0`) and P3a in progress on PR #12 (batch S2, on the
  coordinator's instruction); the P3a view model bullet now says 25 types.
