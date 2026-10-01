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

- Follow-up (A3 step 0): `NoTypeNameEndsInVM` only checked names ending in `VM`, so 13 `*VMTests` classes and the `MainEditorVMTypeName`/`ClassEditorVMTypeName` constants survived. The test now also rejects any declared type name with `VM` followed by an uppercase letter (`MVVM` excluded). Red: 13 offenders (`ReferenceListVMTests`, `CodeViewVMTests`, `MemberVariableVMTests`, `DialogVMTests`, `NodeGraphVMTests`, ...). Renamed the 13 classes and files to `*ViewModelTests` and every identifier embedding `<Name>VM` (constants, `IssuesDialogVM...`/`ErrorDialogVM...` test methods, the allowlist paths in `SourceHygieneTests`). Green: 13/13 `SourceHygieneTests`, 344/344 `Editor.Tests`, format clean.


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

## Batch A3 (implementation: sonnet, 6dcd636–7dcc38d and the T009 commit)

### Red output

`CiWorkflowTests` against the unsplit `ci.yml` (reader parsed it fine; the contract checks failed):

```
TestMatrixCoversEveryTestProjectExactlyOnce  Expected: [tests/NetPrints.Catalog.Tests, ... Editor.UITests]  Actual: []
TestMatrixHasTheContractLegsWithUniqueNames  Expected: [Core, Catalog, CLI, Editor, Editor UI (headless)]   Actual: []
TestMatrixDoesNotFailFast                    Expected: "false"  Actual: ""
EveryLegUploadsItsOwnResultsAndCoverage      Not found: "test-results-${{ matrix.leg }}"
BuildAndTestAggregatesEveryTestRunningJob    Not found: "always()"
Total: 8, Failed: 6, Skipped: 1 (the cli-windows check)
```

### Green evidence (T007-T009)

- Core.Tests `CiWorkflowTests` 7 passed, 1 skipped; full solution run (Release): 1754 tests, 1742 passed, 12 skipped, 0 failed (5m 09s); format clean; build 0 warnings
- The `cli-windows.yml` check was exercised against a throwaway draft of the file (all paths of contracts/ci.md §2: green; without `tests/Fixtures/**`: red, naming the three fixture projects), then the draft was removed
- First PR run: `Test (Editor UI (headless))` failed with 2 tests (`RunCompilesStartsAndPrints`, `EditCompileAndRun`: "Build failed with 1 error(s)"), because building only `tests/NetPrints.Editor.UITests` did not build `NetPrints.Generator`, whose output folder the compile scenarios use. Fixed in 7dcc38d with a `ReferenceOutputAssembly="false"` project reference (as `Cli.Tests` has). Second run all green, both required checks `success`
- Commits: 6dcd636 (step 0 naming), a22a281 (T007, T008), 7dcc38d (generator reference), then the T009 commit

### T009 timings (SC-006)

Baseline: "Build and test (Linux)" in run 36819424717 (master 3a6eafc): 995 s. This PR's run 36863268200 (job durations, runner start to end):

| Job | Seconds | % of baseline |
|---|---|---|
| Test (Editor UI (headless)) | 530 | 53% |
| Repository checks (Linux) | 224 | 23% |
| Test (Editor) | 218 | 22% |
| Test (Core) | 182 | 18% |
| Test (Catalog) | 145 | 15% |
| Test (CLI) | 139 | 14% |
| Build and test (Linux) (aggregate) | 3 | 0% |

The longest new job (530 s) is 53% of the baseline, under the 60% limit (597 s), so no fix was needed. `e2e` (510 s, was 456 s) is unchanged by this batch and stays with the P8 sharding.

Skips: the 12 skipped in the full run are 3 headless UI tests that need a real cursor, OS drag and drop or a window manager (`HeadlessSmokeTests.PanCursor`, `DragFromLists`, `MinimizeAndRestoreClassWindow`), 8 Desktop E2E tests that skip without `NETPRINTS_E2E` (7 scenarios plus `E2EDiagnosticsTests`, which is what took the count from 10 to 11 in A2), and 1 new one: `CiWorkflowTests.CliWindowsPathFilterCoversTheCliTestsBuildInputs`, which skips until T011 creates `cli-windows.yml`.

### Decisions

- Decision: the YAML reader inside `CiWorkflowTests` supports block mappings and sequences, flow sequences, literal and folded scalars, quotes and comments only; an unsupported construct throws, so a future workflow edit cannot pass silently. Correction (Review A R6): as first written the reader stopped silently at an unexpected indent; since A-F3 `Parse` throws a `FormatException` naming the line when any line is left unread, and `TheReaderRejectsAContinuationLineItDoesNotUnderstand` pins it.
- Decision: the `cli-windows.yml` check skips with a reason while the file is missing (T011 creates it) instead of failing, so every commit stays green; the check was verified against a throwaway draft.
- Decision: "test-running job" means a job with a `dotnet test` step other than `e2e`, `packages` and `build-test`; today that is `checks` (E2E discovery) and `test`. `desktop-publish` and `jsonschema` run no tests and are not aggregated.
- Decision: matrix legs carry `name`, `leg` (artifact slug) and `project`; artifacts are `test-results-<leg>`, `coverage-<leg>` and, for `editor-ui` only, `ui-headless`. `NETPRINTS_UI_ARTIFACTS` is a job-level variable (it is inert in the other legs).
- Decision: the E2E zero-test discovery step in `checks` drops the coverage arguments (nothing uploads them any more); each leg builds only its own project (`dotnet build tests/<project>`), the solution is built once in `checks`.
- Decision: the aggregate keeps `timeout-minutes: 30` like every other job; the required check names `Build and test (Linux)` and `Desktop E2E (Linux, Xvfb)` are asserted by `RequiredCheckNamesAreUnchanged`.

## Batch A4 (implementation: sonnet, 9c07c2f–81c3b7f and the notes commit)

### Red output

T010 on Linux passed at once (P2 set UTF-8 output), so the defect was injected: `Console.OutputEncoding = Encoding.Latin1` in `src/NetPrints.Cli/Program.cs` (reverted afterwards, `git diff` empty):

```
ShowTextconvEncodingTests.ShowTextconvWritesNonAsciiNamesAsUtf8WithoutABom
  System.Text.DecoderFallbackException : Unable to translate bytes [FC] at index 19 from specified code page to Unicode.
NetPrints.Cli.Tests  Total: 1, Errors: 0, Failed: 1
```

Reverted: `Total: 1, Failed: 0`. A BOM cannot be injected through `Console.OutputEncoding` (the console stream never writes the preamble), so the no-BOM assertion is covered by the byte check only; the invalid-UTF-8 path is the one proven red.

T012: `CLI (Windows)` first run (36866524963, after 9c07c2f): 269 tests, 17 failed, 3 skipped, `ShowTextconvEncodingTests` passed. T011's path-filter check was written after the workflow file (the check already existed from A3 and was proven against a draft there); with the skip removed it is `Total: 8, Failed: 0, Skipped: 0`.

### Windows fixes and skips (T012)

Fixes (all in tests or `.gitattributes`; no defect in `src/`):

| Failures | Cause | Fix |
|---|---|---|
| 8 `CatalogCommandTests` | The committed catalog snapshots were checked out with CRLF (`* text=auto`) while the CLI writes LF | `.gitattributes`: `*.npcat.json text eol=lf` |
| 4 `ProjectLocatorTests` | `Touch` built the expected path with `/` | build it with `Path.DirectorySeparatorChar` |
| 4 `GitDriversEndToEndTests` | the driver command held `D:\a\...\NetPrints.Cli.dll`; git runs it through `sh`, which eats the backslashes ("Could not execute because the specified command or file was not found") | `CliUnderTest` uses forward slashes |
| all 5 `GitDriversEndToEndTests` (the 4 above also hit this; `PlainGitConflictsOnTheSameBranches` only this) | git marks object files read-only and `Directory.Delete` then throws `UnauthorizedAccessException` | `TempGitRepository.Dispose` clears the attributes first |

Skips already in place (3, reasons name what is missing): `MigrateCommandTests.AnUnreadableSubdirectoryIsReportedAsAFailureNotACrash` and `FormatCommandTests.AnUnreadableDirectoryIsReportedInTheSameShapeAsAnUnreadableFile` ("Needs POSIX permissions and a non-root user"), `MigrateCommandTests.ADirectorySymlinkIsNotFollowed` ("Creating a symbolic link needs a privilege on Windows"). No new skip was added.

Observation: the Windows log shows `Restore failed for ...\np-sample-*\HelloWorld.csproj (exit code 1)` as a warning in the error output of a passing test; the test does not depend on the restore, so it was left (not investigated further).

### Green evidence (T010-T014)

- Windows run 36867229184 (head 81c3b7f): 269 tests, 266 passed, 3 skipped, 0 failed; `ShowTextconvEncodingTests.ShowTextconvWritesNonAsciiNamesAsUtf8WithoutABom` Passed
- CI run 36867229176 (same head): every check success, including `Build and test (Linux)` and `Desktop E2E (Linux, Xvfb)`
- Local full suite (Release, solution-wide): 1755 tests, 1744 passed, 11 skipped, 0 failed (5m 10s); format clean; build 0 warnings
- Local E2E: 18 tests, 18 passed, 0 skipped (2m 24s)
- Commits: 9c07c2f (T010, T011), 738de5b (T012), 81c3b7f (T013)

### Decisions

- Decision: `ShowTextconvEncodingTests` builds its graph from the Core.Tests `HelloWorld` fixture with the class renamed `Grüße`, the method `Größe` and the string literal `日本語` (the "node title" of the task is a pin value, the only node text the summary prints), and runs `dotnet NetPrints.Cli.dll show --textconv` as a child process reading stdout as bytes.
- Decision: the catalog snapshots are pinned to LF in `.gitattributes` (like `*.netpc.json`) instead of normalizing line endings in the tests: the CLI's output is LF on every OS, so the committed expectation must be too.
- Decision: the `cli-windows.yml` check now fails when the file is missing (the A3 skip-when-missing is gone).
- Decision: `cli-windows.yml` has a concurrency group of its own (`cli-windows-<ref>`) and no coverage arguments (the static-coverage settings belong to the Linux legs).

## Checkpoint reports

Each checkpoint task (T014, T027, T045, T058, T071, T087, T099, T111) adds its report here, with evidence for every
success criterion its sub-phase covers and a "Docs updated:" line.

### Checkpoint A (T014)

| Item | Evidence |
|---|---|
| FR-105 | `SourceHygieneTests.NoTypeNameEndsInVM` passes in the full suite (A1 renamed 24 view model classes; A3 step 0 tightened the rule to reject any declared type name with `VM` followed by an uppercase letter, `MVVM` excluded, after finding 13 `*VMTests` classes and two constants; red output in A1 and A3 above). |
| SC-006, diagnostics | CI run 36867229176: `E2EDiagnosticsTests.AForcedTimeoutLeavesTheDiagnosticFiles` Passed (18 of 18 E2E tests passed in the `e2e-results` `.trx`). The artifact `e2e-results` (1.3 MB) was downloaded with `gh run download` and holds `e2e-diagnostics/E2EDiagnosticsTests/` with the seven files `summary.md`, `timings.md`, `ui-tree.json`, `display.png`, `editor.log`, `process.txt`, `run-state.json` (`summary.md`: failure timeout, step `open project (20 s)`). No fix to the upload path was needed. |
| SC-006, timings | The T009 table in A3: longest new job 530 s (Editor UI (headless)) against the 995 s baseline, 53%, under the 60% limit. |
| Windows | `CLI (Windows)` run 36867229184 green: 269 tests, 266 passed, 3 skipped (POSIX permissions twice, symlink privilege once), `ShowTextconvEncodingTests` Passed. |
| Whole suite | Local: 1755 tests, 1744 passed, 11 skipped, 0 failed; E2E: 18 of 18 passed, 0 skipped. CI head 81c3b7f: all checks success. |

Skip count, corrected: without `NETPRINTS_E2E`, 8 of the 18 Desktop E2E tests skip (the 7 scenario classes `CreateProject`, `EditCompileAndRun`, `DragFromLists`, `Shutdown`, `AddReferences`, `MinimizeAndRestoreClassWindow`, `PanCursor` plus `E2EDiagnosticsTests`); the other 10 (`EntryPoint` 1, `DesktopWorkerPool` 1, `FailureCapture` 8) always run. With `NETPRINTS_E2E=1` all 18 run. A2's "9 scenarios" was the 7 scenarios plus `EntryPoint` and `DesktopWorkerPool`, the 9 tests that existed before A2; A3's "7 scenarios plus `E2EDiagnosticsTests`" is the right skip list. The 11 skips of the full run are these 8 plus the 3 headless UI tests.

After Review A: all of R1-R21 fixed or decided; solution suite 1772 tests, 1759 passed, 13 skipped, 0 failed; Desktop E2E 31 of 31 passed (`NETPRINTS_E2E=1 --fail-skips on`); CI run 36891689161 (head e1e9a6c, all checks success).

Docs updated: `docs/contributing/testing.md` (CI section), `AGENTS.md` (the Windows workflow, the `test`/`e2e` split), the Avalonia skills and ADR-0007 (the `ViewModel` naming, A1 commits 48cc09d and bf000fc); no further change was needed in them.

### Checkpoint B (T027)

| Item | Evidence |
|---|---|
| Registry tests | `ContributionRegistryTests` 39, `CommandGestureTests` 25, `ContributionsAreUiFreeTests` 7, `BuiltInCommandTableTests` 33: all pass (Editor tests project). |
| Handler tests | `EditCommandsTests` 22, `ProjectCommandsTests` 38, `BuildCommandsTests` 11, `MainEditorProjectActionsTests` 13, `ProjectSessionViewModelTests` 8: all pass. |
| SC-003, registered built-ins | `BuiltInCommandTableTests` compares every registered built-in with its row of contracts/commands.md (id, label, menu path, group and order, gestures, scope, bar order, handler type, icon): `ARegisteredCommandMatchesItsRow` per id, `EveryRegisteredBuiltInIsARow`, `MenuEntriesFollowTheTableOrderWithinTheirGroup`, `MenuPathsAreTheSixMenus`, `EveryIconIsAMaterialIcon`. |
| SC-003, conflicts | `TheBuiltInsReportNoIssues`: the registry's `Issues` is empty after `BuiltInContributions.Register` (0 duplicate ids, 0 gesture conflicts, 0 invalid descriptors). `ExitsAltF4BelongsToTheOsAndIsNotARegisteredGesture` pins the one table gesture that is not registered. |
| SC-003, pending | Rows without a handler yet, each with its task, pinned by `ThePendingListIsShrinkOnlyAndEachEntryIsStillPending`: `showPanel.<panel>`, `floatDocument`, `dockDocument`, `resetLayout`, `nextTab`, `previousTab`, `closeTab` (T039); `theme.dark`, `theme.light`, `theme.system` (T093); `commandPalette` (T075); `goToAnything` (T076); `navigateBack`, `navigateForward` (T074); `goToSource`, `goToTarget` (T077); `keyboardShortcuts`, `about` (T055); `startPage` (T066). |
| Existing windows' shortcuts | The class editor window has no hand-written key bindings left: `CommandKeyBindingsBehavior` (window) and `ScopedCommandKeysBehavior` (the canvas panel) take them from the registry through `CommandInvoker`. `ClassEditorShortcutTests` (real window and sample project): Delete from the canvas removes the selected node, Delete with focus in the override combo box does nothing, Ctrl+Z and Ctrl+Y from outside the canvas act on the history the editor records to. Ctrl+Space (`nodeSearch`) is covered by `CanvasPopupPositioningTests.OpeningSearchByKeyboardFallsBackToTheSelectedNodeOrTheCanvasCenter` and `CanvasInteractionTests.CtrlSpaceInAPinValueTextBoxDoesNotOpenSearch`; `GraphViewRequestTests` 3/3; `LocalVariablePanelTests` (real Ctrl+Z). |
| Key binding rules | `CommandKeyBindingTests` 9/9 (headless): Global gestures run from a text box, a node text box and the canvas; Graph gestures only with the canvas focused; Ctrl+A, Delete and F2 stay with a text box inside a node; a scope runs only its own commands; a disabled command does not run, and enabling it makes the same key run it (state read at invocation). |
| Whole suite | Solution suite (Debug, no `NETPRINTS_E2E`): 2020 tests, 2007 passed, 13 skipped (the usual E2E and headless skips), 0 failed. Desktop E2E (Release, `NETPRINTS_E2E=1 --fail-skips on`, own Xvfb): 31 of 31 passed, 0 skipped. `dotnet format --verify-no-changes` clean; Release build 0 warnings. |

Docs updated: ADR-0020 (status line, the two key-binding behaviors, the scope paragraph, a "Changes made in implementation" section) and contracts/contributions.md (`Freeze`, `CommandScope` flags and `Global` overlap, `CommandGesture`, the function-key exemption, the extra `InvalidDescriptor` cases, `CommandContext` members, `IProjectActions` and `UnloadingCommandHandler`, no `CanExecuteChanged` and the Run and Stop refresh through `PropertyChanged(IsRunning)`, the key binding surface row). contracts/commands.md already carries the four add-member rows (B3). Each statement was checked against the code (`ContributionRegistry`, `CommandScope`, `CommandGesture`, `CommandContext`, `IProjectActions`, `UnloadingCommandHandler`, `ProjectSessionViewModel`, `CommandInvoker`, the behaviors).

## Review A (T016, part 1 of 4)

Review of sub-phase A (`86196b7..7d94130`), report in `.agent-archive/netprints-p3a/review-A.md`: no blockers, 2 majors,
21 findings. The coordinator accepted every recommendation. Fix batches: F1 R1-R4; F2 R5, R9, R10, R11, R20, R21;
F3 R6, R7, R8, R16, R17, R18; F4 R12, R13, R14, R15, R19.

| Id | Severity | Summary | Batch | Status |
|---|---|---|---|---|
| R1 | major | A cancelled automation request leaves its reply in the pipe; the failure capture reads stale replies | F1 | fixed, c519244 |
| R2 | major | The "editor exited" trigger and every exited-editor branch of `DiagnosticParts` never ran in a test | F1 | fixed, a8fa3ef |
| R3 | minor | The per-class diagnostics folder is never cleared, so stale files pass or fail the proof | F1 | fixed, a8c1aae |
| R4 | minor | A capture failure outside the narrow filter (or building the parts) replaces the original failure | F1 | fixed, a8c1aae |
| R5 | minor | The `start` step includes the wait for a worker, so timings mislead | F2 | fixed, 2f20afe |
| R6 | minor | The YAML reader in `CiWorkflowTests` ignores everything after an unexpected indent | F3 | fixed, c56cdab |
| R7 | minor | The aggregate's "fails unless every needed job succeeded" is checked only as text | F3 | fixed, f4f9ca8 |
| R8 | minor | The `cli-windows.yml` path filter misses inputs the CLI tests read | F3 | fixed, 071c8de |
| R9 | minor | `ProcessLauncher`: the exit can be raised before the start, and the drain can block forever | F2 | fixed, 680e850 |
| R10 | minor | `RunStateTracker` gives one program's exit to another | F2 | fixed, 6dcade6 |
| R11 | minor | The Shutdown scenario has no failure capture, and that is not recorded | F2 | fixed (Decision, no code change) |
| R12 | minor | Deviations are recorded as Decisions while "Deviations" says "None yet" | F4 | fixed, e87db94 |
| R13 | minor | Windows defects in `src/` hidden by test-side fixes, with no follow-up recorded | F4 | fixed, e87db94 (follow-ups recorded) |
| R14 | minor | Docs and workflow text that no longer match behaviour | F4 | fixed, e32deae |
| R15 | minor | The docs sweep broke ADR-0007's amendment and went past T002's scope without a Decision | F4 | fixed, 10675f6 (Decision under Deviations) |
| R16 | nit | Locals still named after the old types; the rule's doc comment is stale | F3 | fixed, 00f04ca |
| R17 | nit | Repeated identifiers are not named constants | F3 | fixed, 6cdf2e4 |
| R18 | nit | `ci.yml:1` header still names the superseded contract | F3 | fixed, 0c292dd |
| R19 | nit | Two red commits in the history | F4 | fixed (Decision, no code change) |
| R20 | nit | A skip after start would be reported as a failure | F2 | fixed, 2f20afe |
| R21 | nit | Forcing a step that never reaches a checkpoint does nothing, silently | F2 | fixed, 2f20afe |

### Accepted decisions (implemented in F2-F4)

- Decision (R8): widen the `cli-windows.yml` path filter to the inputs the CLI tests read, as the review recommends.
- Decision (R10): `ProcessStarted` carries a per-start id, and `RunStateTracker` matches exits by it.
- Decision (R11): `ShutdownTests` is exempt from failure capture (it closes the editor itself, so there is no leased editor to dump); the reason is recorded here and in the test's doc comment.
- Decision (R12): "node title as string pin value" is listed under Deviations, and contracts/ci.md section 2 is amended.
- Decision (R13): both Windows `src/` issues are recorded as follow-ups with the P3 code carry-overs.
- Decision (R15): restore ADR-0007's amendment example; record a Decision for the research/ADR rewrite the docs sweep made.
- Decision (R19): no history rewrite; both red commits are named as bisect-skip.

### Batch A-F4 (R12-R15, R19: e32deae, 10675f6, e87db94)

- Step 1, the E2E exit code. Run on 6906a0f, Debug, no `NETPRINTS_E2E`: the E2E project alone exits 0 (31 tests, 21 passed, 10 skipped); the whole solution (`dotnet test --solution NetPrints.slnx --no-build --no-progress --no-ansi`) exits 0 too (1772 tests, 1759 passed, 13 skipped, 0 failed; the E2E project line reads `passed (856ms)`). The "zero tests" failure A-F3 and A-F2 reported did not reproduce, so no code change: Review A R14 is right, a run without `NETPRINTS_E2E` runs the always-on tests (`FailureCapture`, `DiagnosticParts`, `EntryPoint`, `DesktopWorkerPool`, step and process tests, 21 now) and exits 0. The earlier reports are not explained by the code at this head (most likely a run made before the always-on classes existed or against a stale build); the command that works is the one above. `--ignore-exit-code 8` is kept in CI's smoke step only as a guard for an all-skipped run.
- Skip count 13 = 10 Desktop E2E scenario tests (`AddReferences`, `CreateProject`, `DragFromLists`, `E2EDiagnosticsTests`, `E2EEditorExitDiagnosticsTests`, `E2EScenarioRulesTests`, `EditCompileAndRun`, `MinimizeAndRestoreClassWindow`, `PanCursor`, `Shutdown`: one each) + 3 headless UI tests (`MinimizeAndRestoreClassWindow`, `PanCursor`, `DragFromLists`).
- R14 (e32deae): `testing.md` (exit code 0 with the always-on tests running; Cli.Tests in process, some as child processes; `exitCode` present only once exited, checked against `RunStateSnapshot` and the `WhenWritingNull` JSON context; holdable steps `start`, `open project`, `edit graph`, `run`, `create project`, `add references`, with `compile` and the nested `wait for worker` not holdable and a forced never-held step failing loudly, checked against `StepTimer` and `X11SmokeTests`), `ci.yml` (step renamed "Desktop E2E (no display, always-on tests only)", comment corrected, the never-uploaded `--report-xunit-trx` dropped), contracts/ci.md section 1, README and AGENTS.md (the "zero tests" claims). `CiWorkflowTests` 9/9 after the rename.
- R12, R13, R15, R19: the Deviations, Follow-ups and History note sections above; contracts/ci.md section 2 amended (e87db94), roadmap P3 "Carried over" line extended, `docs/guide/cli.md` says to use forward slashes in `git-install --command` paths, ADR-0007's amendment example restored (10675f6; `SourceHygieneTests` 13/13).
- Flake found by CI at 8d8fa11 (not a finding): `DiagnosticPartsTests.ARunningEditorIsReadThroughConnectionsOfTheirOwn` failed once with `IOException: Connection reset by peer` (the test's fake pipe server closed a connection while the next client was connecting). Red: 1 failure in 60 runs of the class locally. Fix: the fake server pre-creates its instances, answers each client and closes them only after the test has read both replies; 0 failures in 300 runs.
- Totals at e87db94: solution suite (Debug, no `NETPRINTS_E2E`) 1772 tests, 1759 passed, 13 skipped, 0 failed (code unchanged since 6906a0f); Desktop E2E with `NETPRINTS_E2E=1 --fail-skips on`: 31 of 31 passed, 0 skipped. Format check clean.

### Batch A-F1 (R1-R4: c519244, a8c1aae, a8fa3ef)

R1 red (test first: a fake pipe server answers request 1 late, request 1 is cancelled after the write, request 2 is sent):

```
Assert.IsType() Failure: Value is null
Expected: typeof(System.IO.IOException)
Actual:   null          (the second call returned "reply 1")
total: 1, failed: 1
```

R1 fix: `AutomationClient` marks itself out of step when a request is cancelled between write and read, and later calls throw an `IOException` naming the cause; `EditorProcess` keeps `PipeName` and `ConnectAsync`, and the `ui tree` and `run state` parts and `tree.txt` open their own connection. Green: `AutomationClientTests` 1/1.

R3 and R4 red (three new `FailureCaptureTests` facts, after adding the `Func<IReadOnlyList<CapturePart>>` constructor): `StaleFilesFromAnEarlierRunAreGoneAfterACleanCapture` (two `Assert.False() Failure`), `APartFactoryThatThrowsNeverReplacesTheOriginal` (`InvalidOperationException` escaped `FailAsync`), `AnUnexpectedCaptureExceptionNeverReplacesTheOriginal` (`ArgumentException` from an empty folder name escaped); total 11, failed 3. Fix: `CaptureAsync` deletes the folder's files first, `FailAsync` catches every exception, and `X11SmokeTestBase` passes a factory so building the parts is inside the guard. Green: `FailureCaptureTests` 11/11.

R2: the behaviour was already there, so these tests were written after the code and mutation-checked. `DiagnosticParts` now reads the editor through `ICapturedEditor` (`EditorProcess` implements it; `Create` takes the display name and a screenshot function), so `DiagnosticPartsTests` runs the exited and running branches over a fake: `process.txt` `exited 137`, `ui-tree.json` and `run-state.json` with `editorExitCode`, no connection opened for an exited editor, and two connections of their own for a running one. Mutation: with the `HasExited` branches disabled, `AnExitedEditorLeavesItsExitCodeInsteadOfAUiTree` failed (1 of 2 failed); restored, 2/2. `E2EEditorExitDiagnosticsTests` (new class, `EditorProcess.Kill` as the test hook) kills the leased editor mid-step: the failure arrives in about 7 s against a 90 s budget, `summary.md` says `failure: editor exited`, `process.txt` matches `exited <code>`, both JSON files carry that code, `ui-tree.json` has an empty `windows`, no `capture-errors.txt`. Mutation: with the kind forced away from `editor exited`, it failed on the `summary.md` assertion; restored, green.

Totals at a8fa3ef: solution suite 1762 tests, 1750 passed, 12 skipped, 0 failed; Desktop E2E with `NETPRINTS_E2E=1 --fail-skips on`: 25 of 25 passed, 0 skipped.

## Deviations

- Node title as a string pin value (Review A R12): FR-006, ADR-0019 and contracts/ci.md section 2 ask for non-ASCII node names or a node title. `show --textconv` prints no node title, so `ShowTextconvEncodingTests` uses a node whose string pin value contains `日本語`; contracts/ci.md section 2 is amended to that wording.
- Research notes and ADRs outside T002's stated scope (Review A R15): T002's grep criterion spans `docs/`, so the rename also rewrote `docs/research/2026-09-25-*` and ADRs 0003, 0004, 0005 and 0009 (they stay navigable). The one sanctioned old name left in `docs/` is the `DialogVM<TResult>` example in ADR-0007's amendment.
- Review A R11: `ShutdownTests` is exempt from failure capture (see the Decision above).

## Follow-ups (out of P3a scope, constitution VIII)

- `catalog --check` (`src/NetPrints.Cli/Commands/CatalogCommand.cs`) compares bytes: in a user repo with `core.autocrlf=true` and no `.gitattributes` rule, a checked-out `.npcat.json` has CRLF endings and always reads as stale. This repo is covered by its own `.gitattributes` line.
- `git-install --command "dotnet C:\...\NetPrints.Cli.dll"` (`GitInstallSettings.cs`) is written verbatim and git runs the driver through `sh`, which drops the backslashes. Docs now say to use forward slashes (`docs/guide/cli.md`). Both are listed in the roadmap's P3 "Carried over" line.

## History note

`eda3181` (fails `SourceHygieneTests.NoNullForgivingOperator`; fixed in `a1719f4`, whose message says "docs:" though it carries the code fix) and `a22a281` (the UI leg fails; fixed by `7dcc38d`) are red commits. The branch is not rewritten (the repo merges with merge commits); use `git bisect skip eda3181 a22a281`.

## Governance proposals

- Constitution PATCH 1.2.3 → 1.2.4 (Development Workflow: path-filtered Windows or macOS legs that extend `CI`) was
  applied in batch S2 on the coordinator's instruction; the owner confirms it on PR #12.
- Roadmap: the phases table now records P2 as released (`v0.2.0`) and P3a in progress on PR #12 (batch S2, on the
  coordinator's instruction); the P3a view model bullet now says 25 types.

### Batch A-F2 (R5, R9, R10, R11, R20, R21, R1 follow-up: 6dcade6, 680e850, 2f20afe, b32d2af)

- R10 (6dcade6). Red: `RunStateTrackerTests.AnExitOfAnEarlierProgramDoesNotEndTheCurrentOne` (two starts, the first one exits) failed with `Expected: Running, Actual: Exited` (total 1, failed 1). Fix: `IProcessLauncher` events carry a per-start id (`ProcessStarted(id, request)`, `LineReceived(id, stream, line)`, `ProcessExited(id, code)`); `RunStateTracker` keeps the current id and ignores lines and exits of any other. Green: `RunStateTrackerTests` 11/11.
- R9 (680e850). Red: `ProcessLauncherTests.TheExitIsReportedEvenWhenAGrandchildKeepsThePipesOpen` (`sh -c "sleep 20 & exit 3"`) failed with `InvalidOperationException : StandardError has not been redirected` (the exit handler disposed the process before `BeginErrorReadLine`: the ordering race). `AnInstantlyExitingProgramAlwaysEndsExited` (100 starts of `true`) passed before the fix, so it is a guard, not a red; the race is timing-dependent. Fix: `EnableRaisingEvents` is set after `ProcessStarted` and `Begin*ReadLine`; the exit drains with `WaitForExitAsync` under `DrainTimeout` (5 s, internal, 500 ms in the test), then reports anyway. The test double in `TestDoubles.cs` got the same ordering. Green: `ProcessLauncherTests` 2/2, three runs in a row. The never-hangs half is covered by the grandchild test's 10 s bound; its pre-fix failure was the race above, before the drain was reached, so the drain bound itself is not mutation-checked.
- R5 (2f20afe). Red: `E2EDiagnosticsTests.AForcedTimeoutLeavesTheDiagnosticFiles` with the new assertion `timings.md` contains `| wait for worker |` failed (`Not found`). Fix: `StartAsync` times `pool.RentAsync` as its own `wait for worker` step, nested in `start`. Green: 6/6 of the E2E classes touched, under Xvfb.
- R20 (2f20afe). Red: `E2ESkipAfterStartTests` (a scenario that calls `Assert.Skip` after `StartAsync`) got an `E2EStepFailureException` wrapping `$XunitDynamicSkip$not supported here`. Fix: the catch filter excludes `SkipException`. Green: the test catches the `SkipException` itself (`Assert.ThrowsAsync` made xunit report the test as skipped, which `--fail-skips` would fail).
- R21 (2f20afe). Red: `StepTimerTests.AForcedStepThatNeverReachedACheckpointFailsLoudly` (no exception, stub) failed, 1 of 3. Fix: `StepTimer.EnsureForcedStepHeld`, called at the end of `RunScenarioAsync` (inside the capture), throws "step 'x' was never held (no checkpoint inside it)". Holdable steps are the ones with a checkpoint inside: `start`, `open project`, `edit graph`, `run`, `create project`, `add references`; `compile` is not. Green: `StepTimerTests` 3/3 and `E2EScenarioRulesTests` (real editor).
- R11. Decision: `ShutdownTests` stays outside `RunScenarioAsync`; the editor exits by design, so there is no leased editor to dump, and its assertion messages carry the stderr. Recorded in the test's doc comment.
- R1 follow-up (b32d2af). Red: `AutomationClientTests.ACallAfterAFailedExchangeNamesTheCause` (the server drops the connection after accepting) failed on `Not found: "open a new connection"`. Fix: `AutomationClient` marks itself out of step on any exception between the write and the full reply, and the later call's `IOException` names the cause. Green: `AutomationClientTests` 2/2.

### Batch A-F3 (R6, R7, R8, R16, R17, R18: c56cdab, f4f9ca8, 071c8de, 00f04ca, 6cdf2e4, 0c292dd)

- R6 (c56cdab). Mutation first, on the real `ci.yml`: `- name: Install X11 tools` split into a two-line plain scalar plus a new `extra` job with a `dotnet test` step: `CiWorkflowTests` 8/8 green (the bug). Red: the new self-test `TheReaderRejectsAContinuationLineItDoesNotUnderstand` with the check disabled: `Assert.Throws() Failure: No exception was thrown` (total 9, failed 1). Fix: `Parse` throws `FormatException` ("Unsupported YAML on line N") when `Peek()` is not null after `ParseBlock(0)`. Green with the same mutation in `ci.yml`: the workflow-reading tests fail with `System.FormatException : Unsupported YAML on line 222: tools`; `ci.yml` restored, 9/9 green. The A3 Decision text is corrected.
- R7 (f4f9ca8). Mutation: `&&` to `||` in the aggregate step. Red with the strengthened test: `Assert.DoesNotContain() Failure: Sub-string found`. The test now requires `||` and `continue-on-error` to be absent, exactly one `[` line, and one `[ "${{ needs.<id>.result }}" = "success" ]` per need joined by ` && ` (any order). Restored: 9/9.
- R8 (071c8de). Red: the test's required inputs now include `samples/**`, `schemas/**`, `eng/schemastore/**`, `src/NetPrints.Sdk/**`, `.gitattributes`; on the old filter `pull_request paths in .github/workflows/cli-windows.yml miss: samples/**, schemas/**, eng/schemastore/**, src/NetPrints.Sdk/**, .gitattributes` (total 9, failed 1). Fix: those five entries in both triggers (red and green share one commit); contracts/ci.md section 2 amended. Green: 9/9.
- R16 (00f04ca). The four locals are `isClassOrMainEditorViewModel`, `referenceListViewModel`, `eventGraphViewModel`, `localViewModel`. `NoTypeNameEndsInVM` now scans `BaseTypeDeclarationSyntax` (enums included) and `DelegateDeclarationSyntax`, and its doc comment states the `VM[A-Z]` rule. No enum or delegate offended, so a mutation: a temporary `enum ModeVM` and `delegate void DoneVM()`: the old check stayed green (1/1), the new one failed with `Collection: ["ModeVM", "DoneVM"]`; file removed, green.
- R17 (6cdf2e4). `AutomationOps` (`NetPrints.Editor.Hosting.Automation`: `Status`, `Find`, `Dump`, `RunState`, `Tree`, `Settle`) is used by the agent and the client (and two test fakes); `TestEnvironment.UiArtifactsVariable` in `NetPrints.Testing` replaces the three `NETPRINTS_UI_ARTIFACTS` literals. `RunStateTrackerTests` keeps the `"runState"` literal on purpose: it pins the wire name. Refactor, no behaviour change.
- R18 (0c292dd). `ci.yml:1` points at `specs/005-editor-shell/contracts/ci.md`.

Totals: solution suite (Debug, no `NETPRINTS_E2E`) 1772 tests, 1758 passed, 13 skipped, the one reported failure being the Desktop E2E project's own "zero tests ran" exit (it self-skips without `NETPRINTS_E2E`); Desktop E2E with `NETPRINTS_E2E=1 --fail-skips on` under its own Xvfb: 31 of 31 passed, 0 skipped.

### Batch B1 (T017-T019: 9ef7aef, 4cf4011)

Order: T019 first (9ef7aef), because `ICommandHandler` and `GoToItem` need `CommandContext` and `DocumentId`; T017 and T018 together (4cf4011).

- Red, T019 (compile-red): with `DocumentId` absent, `dotnet build tests/NetPrints.Editor.Tests` failed with `CS0246: The type or namespace name 'DocumentId' could not be found` in `CommandContext.cs` and `IShell.cs` (and in the tests). Green: `DocumentIdTests` 26/26. Mutation (`hash <= 0` to `hash < 0` in `TryParse`): `AMalformedStringIsRejected(text: "graph:#class")` failed `Expected: False, Actual: True`; restored.
- Red, T017 (behavioural): with a registry whose `Add*` only appended, `ContributionRegistryTests` failed 23 of 33 (bad ids, empty labels, single-key Global, unparseable gesture, duplicate ids, every gesture-conflict case, freeze, log count, owner). Green after the validating registry: 33/33.
- T018 architecture test `ContributionsAreUiFreeTests` (source scan for `Avalonia`/`Nodify`/`Dock` usings and qualified names, a reflection scan of signatures, and a scanner self-test). It passes on the clean namespace by design; mutation: a `Contributions/Mut.cs` mentioning `Avalonia.Point` failed it with `Collection: ["Mut.cs: Avalonia.Point"]`; removed.
- Written after the code: `CommandGestureTests` (17). Mutation (canonical modifier name `Ctrl` to `Control`) failed 5 of them; restored.
- Green: Editor tests 430/430, `SourceHygieneTests` 13/13, `dotnet format --verify-no-changes` clean. First full run had one failure, `NoNullForgivingOperator`, from a `null!` in `DocumentIdTests` (the case was dropped; the guard is `ArgumentException.ThrowIfNullOrEmpty`).

Decisions:
- `ContributionRegistry` takes only a logger; the owner of every issue is `ContributionIds.Owner` (`netprints`) until P3 adds an owner parameter to `Add*`.
- A command that loses a gesture to a conflict stays registered, with that gesture removed from the stored descriptor (the issue names the winning command); a command with an invalid gesture is ignored.
- A gesture is `CommandGesture`: modifiers Ctrl, Alt, Shift, Meta (aliases Control, Cmd, Command, Win), canonical order Ctrl+Alt+Shift+Meta then the key; the key is normalised to an initial capital and the rest lower case.
- Validation beyond the contract: a blank label or title, a missing handler or factory, an invalid `CommandId` in a context-menu item, and a blank template name or profile id are `InvalidDescriptor`.
- `CommandContext.Session` is `object?` until `ProjectSessionViewModel` exists (sub-phase C); `ActiveGraph` is `NodeGraphViewModel?`; `CommandSelection(Nodes, TreeItem)` carries the selection. `NavigationTarget` (data-model) lives in `NetPrints.Editor.Shell` with string node and pin ids.
- `TooltipTarget(Kind, Subject)` with kinds Pin and Connection; `PanelDock` is Left, Right, Bottom; `ProjectOutputType` is Console, Library.
- The registry log event is 1070 (`ContributionIssue`, warning), continuing the per-folder blocks.
- `DocumentId` is a sealed record with a private constructor, factories and `TryParse`; the class path may not contain `#`.

### Batch B2 (T020-T022: dd33ac6, bafe527, faf79ff, 6278f1f)

- Red, T020 (behavioural, with the new members stubbed to `false`/`null`/no-op): `UndoRedoStackSavedMarkerTests` failed 5 of 9 (`MarkSaved` makes the state saved: `Expected: True, Actual: False`; undo back to the mark; redo to the mark; `MarkSaved` raises `Changed`: `Expected: 1, Actual: 0`; the names). Green: 16/16 with `UndoRedoStackTests`.
- Red, kill on cancel (bafe527): `ProcessLauncherTests.CancellingTheTokenKillsTheProcessTree` with the token accepted and ignored: failed after 10 s (the exit was never reported). Green: 3/3, no stray `sleep` left.
- Red, T021 (stub session): `ProjectSessionViewModelTests` 6/6 failed (key `Expected: "5f53fafd046c50da"`, run state `Expected: Exited, Actual: NotStarted`, save `Expected: True, Actual: False`, stacks not the same instance). Green: 6/6. Mutation (the wait for a save removed from `CompileAsync`): `CompileWaitsForASaveInProgress` failed with `Expected: 1, Actual: 2` (two writes reached the store); restored.
- Red, T022 (stub handlers): `BuildCommandsTests` 6 of 11 failed (the other 5 are "disabled without a project", true for a stub). Green: 11/11. Mutations: Stop without `Stop()` and Run without the `IsRunning: false` guard each failed `RunStartsTheProgramAndStopEndsItThroughTheRunToken`; Run without the save wait failed `CompileAndRunWaitForASaveInProgress(run: True)`; all restored.
- Written after the code: none; the stubs gave the behavioural red.
- Green: Editor tests 446/446 before T022 and 11 more with it; solution suite (Debug, no `NETPRINTS_E2E`) 1882 tests, 0 failed, 13 skipped (the usual headless skips); Desktop E2E with `NETPRINTS_E2E=1 --fail-skips on` under its own Xvfb: 31 of 31, 0 skipped. `dotnet format --verify-no-changes` clean.

Decisions:
- `IProcessLauncher.Start(request, CancellationToken)`: cancelling kills the process tree (`Process.Kill(entireProcessTree: true)`); this is how Stop ends the program, since the launcher had no handle to it. xUnit1051 makes test call sites pass `TestContext.Current.CancellationToken`.
- `ProjectSessionViewModel` takes `(Project, EditorContext)`; `MainEditorViewModel` creates one per open project (`Session`) and disposes the previous. `CommandContext.Session` is typed `ProjectSessionViewModel?`.
- The static `CompileAsync` and `CompileAndRunAsync` moved to the session; the class editor windows call the statics, so a program they start has no run token and Stop does not reach it until C replaces those windows.
- `UndoStackFor(ClassGraph)` is a registry the class editors do not use yet (they keep their own stack); C wires them to it.
- `save` and `saveAll` both save every edited class: persistence has no single-class save, and the per-document unsaved files arrive in D.
- `Run` is disabled while a program runs (Stop takes the slot); `IsRunning` reads `RunStateTracker` and raises no change event yet, the command bar in C polls it.
- A new `UndoRedoStack` is not at a saved state until `MarkSaved()`, the same as after `Clear()`.
- `ProjectKey` is the first 16 hex characters of the SHA-256 of the full path, private to the session until T051 moves it to `EditorDataPaths`.
- The save-in-progress test holds a write with `GatedDocumentStore` through a `TestEditor.CreatePersistence` decorator hook; a call to `SaveAllAsync` during a save returns that save's result.

### Batch B3 (T023-T025: 9dc2de7, b6a24e7, 85d77a4, b282200, 1310668, 34d35e2)

- Red, `IsRunning` change (9dc2de7, behavioural): `IsRunningRaisesAChangeWhenTheProgramStartsAndExits` with no event: `Expected: [True, False]`, `Actual: []`. Green after `RunStateTracker.PhaseChanged` and the session re-reading `IsRunning` on the UI thread. Written after the code: `PhaseChangedFiresOnlyWhenThePhaseChanges`; mutation (`OnExited` raising unconditionally) failed it, restored.
- Red, T023 (stub handlers, `CanExecute` false, `DynamicLabel` null): `EditCommandsTests` 12 of 22 failed (`Expected: "Undo"`/`Actual: null`, `Assert.True()` on the enabled state, `Expected: ["DeleteItem", "RenameItem"]`, `Actual: []`); the other 10 are "disabled" cases, true for a stub. Green 22/22. Mutation (tree selection before the active document in `UndoCommandHandler`): `UndoPrefersTheActiveDocumentOverTheTreeSelection` failed; restored.
- Red, the canvas behavior (T023): `GraphViewRequestTests` with the behavior reading the `DataContext` of `StyledElementBehavior` (never set for a behavior inside a `UserControl`) failed 3 of 3 (`UiWaitTimeoutException: the viewport moved`, `SearchPopup: open`). Green after the behavior follows `AssociatedObject.DataContextChanged`: 3/3, with `CanvasInteractionTests` and `CanvasPopupPositioningTests` still green (Ctrl+Space now goes through the same request).
- Red, T024 (stub handlers): `ProjectCommandsTests` 29 of 38 failed (`Actual: []` calls, `Expected: True` enabled state, `Expected: ["ConfirmUnload"]`); `MainEditorProjectActionsTests` 12 of 13 failed against no-op `IProjectActions` members. Green 38/38 and 13/13. Mutation (the declined prompt not honoured in `UnloadingCommandHandler`): 9 failed; restored.
- Red, multi-scope (b282200): `AMultiScopeCommandOverlapsEveryScopeItNames` failed 2 of 4 rows (`Graph|ProjectTree` against `ProjectTree` and against `Graph` raised no `GestureConflict`). Red, function keys (1310668): `AFunctionKeyIsAllowedInGlobalScope` 2/2 and `FunctionKeysAreF1ToF24` 3 of 8 failed with `IsFunctionKey` stubbed to `false`. Green after the real members.
- Red, T025 (registrations empty): `BuiltInCommandTableTests` 26 of 32 failed. Green 33/33. Mutations: a stale `["save"] = "T999"` pending entry failed `ThePendingListIsShrinkOnly...`; `"Save all"` renamed failed the `saveAll` row; an `Alt+F4` gesture on `closeProject` failed its row and `ExitsAltF4BelongsToTheOsAndIsNotARegisteredGesture`; all restored.
- Written after the code: none beyond the `PhaseChanged` test above and `RowsWithoutAHandlerArePending`.
- Green: Editor tests 579; solution suite (Release, no `NETPRINTS_E2E`) 2007 tests, 0 failed, 13 skipped (the usual headless skips); Desktop E2E with `NETPRINTS_E2E=1 --fail-skips on` under its own Xvfb: 31 of 31, 0 skipped. Release build 0 warnings; `dotnet format --verify-no-changes` clean.

Pending commands (`PendingCommandIds`, each with the task that registers it): `showPanel.<panel>`, `floatDocument`, `dockDocument`, `resetLayout`, `nextTab`, `previousTab`, `closeTab` (T039); `theme.dark`, `theme.light`, `theme.system` (T093); `commandPalette` (T075); `goToAnything` (T076); `navigateBack`, `navigateForward` (T074); `goToSource`, `goToTarget` (T077); `keyboardShortcuts`, `about` (T055); `startPage` (T066).

Decisions:
- `IProjectActions`, reached through `IShell.ProjectActions`, is the seam for the project flows (open, new, close, exit, settings, references, add members, tree rename and delete); `MainEditorViewModel` implements it over today's flows until C replaces the main window, and `IWindowService.CloseMainWindow()` backs `exit`.
- `UnloadingCommandHandler` is the one call site for the unload prompt: `openProject`, `newProject`, `closeProject` and `exit` call `IProjectActions.ConfirmUnloadAsync` first (only when a project is open); T050 implements the prompt there, and today it answers true.
- Add-member, class-settings and rename or delete of a tree item act on the tree selection's class, else the active document's; undo and redo prefer the active document, then the tree selection (`CommandTargets`).
- The viewport and search commands raise a `GraphViewRequest` (`FrameSelection`, `FitAll`, `NodeSearch`) on `NodeGraphViewModel`; `GraphViewRequestBehavior` on the Nodify editor carries it out, and the Ctrl+Space handler in `GraphEditorView` now raises the same request. `selectAll` and `cancel` act on the view model directly (the item containers' `IsSelected` is two-way bound).
- Observability for Run and Stop: handlers are stateless (the context carries the session), so the change notification is the session's `PropertyChanged(IsRunning)`; the command adapter in C re-queries `CanExecute` on session changes. No `CanExecuteChanged` on `ICommandHandler`.
- `CommandScope` becomes a flags enum (delete and rename are Graph and ProjectTree in commands.md); `Global` stays 0 and overlaps everything.
- Function keys F1 to F24 are exempt from the single-key-in-Global rule (commands.md binds F5, F7 and Shift+F5 globally); contributions.md and data-model.md say so.
- The labels of the four add-member commands ("Add method", "Add constructor", "Add variable", "Add event graph") are now rows of the commands.md table.
- `rename` on a tree item only reveals the class settings (the Name box) until C's tree has inline rename; `delete` of a tree item handles classes, methods and event graphs, not variables (no tree item for them yet).
- `Go` and `Help` contributions register nothing yet; their files exist for the pending commands above.

### Batch B4 (T026-T027: df94fd2, b9af37c and the notes commit)

- Red, T026 (behavioural, with the two behaviors as empty stubs): `CommandKeyBindingTests` failed 4 of 9, the other 5 being "does not run" cases that a stub satisfies:

```
AGlobalGestureRunsWhereverTheFocusIs          Expected: 3        Actual: 0
AGraphGestureRunsWhileTheCanvasHasFocus       Expected: 1        Actual: 0
AScopeRunsOnlyItsOwnCommands                  Expected: (1, 2, 1) Actual: (0, 0, 0)
ADisabledCommandDoesNotRunAndTheStateIsReadAtInvocation  Expected: (1, 1) Actual: (0, 0)
total: 9, failed: 4
```

  Green after the behaviors: 9/9. Mutation (the text input test removed from `ScopedCommandKeysBehavior`): 4 failed (`AGraphGestureStaysWithATextInputInsideANode` for Ctrl+A, Delete and F2, and `ADeleteInANodeTextBoxEditsTheText`); restored.
- Written after the code: `ClassEditorShortcutTests` (3), the `CanvasPopupPositioningTests` change, and `ProjectSessionViewModelTests.AdoptedUndoStackReplacesTheOneOfTheClass`. Mutations: `Session?.UseUndoStack(...)` removed from `MainEditorViewModel.AttachCommands` failed `UndoAndRedoActOnTheHistoryTheEditorRecordsTo`; `undoStacks[cls] = stack` removed failed the session test; both restored.
- Wiring: `CommandInvoker` (Shell) lists a scope's commands and runs one against a fresh context only when `CanExecute` is true at that moment. `ClassEditorViewModel.Commands` holds it; `MainEditorViewModel` builds a frozen registry of the built-ins and attaches an invoker to each class editor it opens, with `ClassEditorCommandContextProvider` (open graph, its selected nodes, the session, the class's document id) and `LegacyWindowShell` (project actions only). `ClassEditorWindow.axaml` has the window behavior and wraps the canvas in a `Panel` carrying the scoped behavior. `GraphEditorView` lost its Ctrl+Space tunnel handler, its top level reference and its private `IsInsideValueEditor` (now `CommandKeyGestures.IsInsideValueEditor`, shared).
- Allowlists: no XAML hygiene or `SourceHygieneTests` allowlist entry named the removed `KeyBinding`s or handler, so none was freed and none was added.
- Green: Editor UI tests, the solution suite and the Desktop E2E as in the Checkpoint B table.

Decisions:
- The scoped behavior sits on a `Panel` around `GraphEditorView` (data context: the class editor view model), not on the Nodify editor, because the invoker lives on the class editor view model and the view's data context is the graph. Keys from the inspector or the error list are outside it.
- Graph gestures need focus inside the canvas, as the contract says, so `CanvasPopupPositioningTests` clicks the empty canvas before Ctrl+Space. A freshly opened graph does not take focus by itself yet; the shell (C) is the place for that.
- `ProjectSessionViewModel.UseUndoStack` makes the class editor's own stack the session's stack for that class, so the registry's undo and redo act on the history the editor records to. C replaces the per-editor stack with the session's.
- The class editor's document id uses the graph key `class` for whatever graph is open: the model has no ids for methods and event graphs yet, and the undo handler needs only the class path.
- Delete of nodes is still not undoable (`NodeGraphViewModel.DeleteSelectedNodes` was never recorded in the history); the undo test uses a probe command, not a delete.
- Key gestures not parseable into an Avalonia key are skipped; the descriptor key `Esc` maps to `Escape`.

## Review B (T028, `review-B.md`)

Verdict: approve with changes, no blockers, 6 majors. Fix batches F1 to F4 (T029). SHAs fill in as each batch lands.

| Id | Sev | Summary | Batch | Status |
|---|---|---|---|---|
| R1 | major | Stop cannot reach every run (second Run click, class editor Run, unload leaves the program) | F1 | fixed f1cf630 |
| R2 | major | Compile and Run are not single-flight during a save | F1 | fixed f1cf630 |
| R3 | major | The saved marker can be made to lie by its callers | F2 | fixed 23380a8 |
| R4 | major | Command-state refresh contract too narrow for menus and the command bar | F3 | open |
| R5 | major | Graph gestures need canvas focus and nothing gives it | F3 | open |
| R6 | major | `exit` and window close will prompt twice once D adds the prompt | F3 | open |
| R7 | minor | Kill-tree test does not pin the tree; cancel registration race | F1 | fixed c6f401d |
| R8 | minor | Gesture parser accepts unknown keys and alias spellings | F4 | open |
| R9 | minor | `Shift+<letter>` passes the Global single-key rule; F2 exemption | F4 | open |
| R10 | minor | Extra `InvalidDescriptor` cases mostly untested | F4 | open |
| R11 | minor | `showPanel.<panel>` pending row can never go stale | F4 | open |
| R12 | minor | A synchronous throw escapes `CommandInvoker.TryRun` | F2 | fixed 81d8768 |
| R13 | minor | A `#` in a class file's folder makes every shortcut throw | F4 | open |
| R14 | minor | Handlers enabled where the action is not allowed or is a no-op | F2 | fixed 81d8768 |
| R15 | minor | A save requested during a save is dropped | F2 | fixed 23380a8 |
| R16 | minor | `CommandContext.Session` binds the public surface to editor view models | F3 | open |
| R17 | nit | Esc also clears the selection while Nodify cancels a drag | F3 | open |
| R18 | nit | Stale test comment; `NewProjectAsync` mapping untested | F2 | fixed d7076d1 |
| R19 | nit | data-model.md names drift from the contract | F3 | open |
| R20 | nit | Gate tests have no timeout | F2 | fixed 23380a8 |
| R21 | nit | UI-free scan skips `Contributions/BuiltIn/` and one level of signatures | F1 | fixed 8e2015e |
| R22 | nit | `Ctrl` binds Control on macOS, not Cmd | F4 | open |

Six orphaned `NetPrints.Editor.Tests` Debug hosts left by B test runs were found hung (they ignored SIGTERM) and killed. R20 (gate tests without a timeout) is the likely cause and was fixed in F2.

### Accepted decisions

- R1: one run at a time per session, Run disabled while running; every run path (the class editor windows included) goes through the session so Stop reaches it; unloading a project (close, open, new, exit) kills the running program. T050's prompt (sub-phase D) also covers Close, Open and New.
- R2: compile and run are single-flight per session; a call made while one is in flight returns that flow's task.
- R3: add `ForgetSavedState()`; capture the save point before the save starts; chain one follow-up save (R15).
- R4: one combined "command states changed" notification the command adapter re-queries on.
- R5: focus the canvas when a graph opens (now and in C).
- R6: `exit` only closes the window; the window-close path owns the unload prompt.
- R17: `cancel` runs only while a popup is open.
- R22: map `Ctrl` to Cmd on macOS now.

### B-F1 (R1, R2, R7, R21: f1cf630, c6f401d, 8e2015e)

- R1 + R2 (f1cf630). Red (`ProjectSessionViewModelTests`, 5 of 13 failed, plus the main window test): `ASecondCompileDuringASaveJoinsTheFirstBuild` (builds `Expected: 1 Actual: 2`), `ASecondRunDuringASaveStartsOneProgram` (`Expected: 1 Actual: 2`), `RunningAgainWhileTheProgramRunsStartsNothingAndStopStillCancelsIt` (second `RunAsync` returned true), `RunningFromTheClassEditorGoesThroughTheSessionSoStopReachesIt` (token not cancelled), `DisposingTheSessionKillsTheRunningProgram` (token not cancelled); `MainEditorViewModelTests.RunButtonIsDisabledWhileTheProgramRunsAndComesBackOnExit` (`CanExecute` stayed true). Fix: the session holds one in-flight compile/run task and returns it to a second call, refuses `RunAsync` while the program runs, `Dispose` cancels the run token, the static `CompileAsync`/`CompileAndRunAsync` are private (`BuildAsync`/`BuildAndStartAsync`), a cancelled token (Stop or unload mid-build) skips the start; the class editor reaches the session through `SessionSource` (set by `MainEditorViewModel.AttachCommands`) and its Run/Compile do nothing without one; the main Run button is disabled while the session runs. The gate tests wait on explicit gates with a 10 s bound (no time-based waits). Two `ClassEditorViewModelTests` Run tests now attach a session. Green: the same classes, 0 failed.
- R7 (c6f401d). The test now uses `DrainTimeout = 30 s` and waits for the grandchild's pid line, so a surviving grandchild pushes the exit report past the 10 s bound. Mutation: `Kill(entireProcessTree: true)` replaced by `Kill()` failed it (`Assert.Same() Failure`, exit never reported), restored, green. Fix: the cancel registration is made right after `Start`, before `EnableRaisingEvents`, so an early exit always disposes the real registration. macOS and Windows stay unverified (Linux only; the test skips on Windows; the Mac mini check is not done).
- R21 (8e2015e). `SearchOption.AllDirectories` and a namespace prefix match. Mutation: a temporary `Contributions/BuiltIn/TmpOffender.cs` (`using Avalonia.Input;`) failed both scans (`TmpOffender.cs: Avalonia.Input`, `Avalonia.Input`), removed.
- Totals (Release): solution suite 2026 total, first run 2 failed (the two class editor Run tests, fixed), Editor.Tests rerun 586 passed, UITests passed (5 m 35 s), Desktop E2E (`NETPRINTS_E2E=1`, `--fail-skips on`) 31 passed, 0 skipped. Format and Release build clean.

### B-F2 (R3, R15, R20: 23380a8; R12, R14: 81d8768; R18: d7076d1)

- R3 (23380a8). Red (`UndoRedoStackSavedMarkerTests` 3 failed, `ClassEditorViewModelTests.DeletingNodesInvalidatesTheSavedMarkerEvenAfterAnUndoReturnsToIt` `Expected: False Actual: True`, `ProjectSessionViewModelTests.SaveAllMarksTheUndoStacksSaved` `Expected: True Actual: False`, `AnEditMadeDuringASaveIsNotMarkedSaved` `Expected: True Actual: False`; run against compiling no-op stubs of the new API). Fix: `UndoRedoStack.CapturePosition()` returns a `SavePoint` (depth, top, epoch), `MarkSaved(SavePoint)` is ignored when `ForgetSavedState()` or `Clear()` ran since the capture, `ForgetSavedState()` drops the marker; the session captures the point of every stack before a save starts and marks it after the save succeeds; `NodeGraphViewModel.DeleteSelectedNodes` forgets the marker when it deleted something. Node delete stays not undoable (decision: forget instead, as the review allowed). Green: same classes, 0 failed. The `ProjectPersistence.MarkClean` race after the write (an edit made during the write is cleared as clean) is still open for T048; the follow-up save of R15 does not cover it.
- R15 (23380a8). Red: `ASaveRequestedDuringASaveRunsOnceMoreAndWritesTheLaterEdit` (the edited class was never written). Fix: a save requested while one runs sets a flag and returns the running save's task; the running save runs once more when it ends (at most one pending; a failed save does not repeat). Green.
- R20 (23380a8). Gate tests (`CompileWaitsForASaveInProgress`, the two held-save tests, the three new save tests, `CompileAndRunWaitForASaveInProgress`) carry `Timeout = 30000`, every await after a gate is `WaitAsync(10 s)`, and `GatedDocumentStore` fails a blocked write after 30 s. Mutation: commenting out the release in `CompileWaitsForASaveInProgress` failed it with `TimeoutException` after 14.7 s (suite of the class), not a hang; restored. No orphaned hosts left by my runs.
- R12 (81d8768). Red: `CommandInvokerTests.ASynchronousThrowReachesTheFaultCallbackInsteadOfEscaping` (`InvalidOperationException : sync` escaped `TryRun`). Fix: the handler runs inside an async local function so both paths reach `onFaulted`. Green.
- R14 (81d8768). Red: `RenameIsDisabledForAConstructorTreeItemAndAnActiveConstructorGraph` and `DeleteIsDisabledForAVariableTreeItemNothingHandlesButEnabledForMethodsAndEventGraphs` (`Expected: False Actual: True`). Fix: rename is disabled for a `ConstructorGraph` target; delete is enabled for a tree item only when it is a class, method or event graph (C's tree tasks re-enable variables). Green.
- R18 (d7076d1). Test written after the code (the mapping already worked): `NewProjectCreatesAndLoadsTheProject`. Mutation: `NewProjectAsync` returning `Task.CompletedTask` failed it (`Expected: True Actual: False`), restored. The canvas test comment now says the canvas's scoped key behavior.
- Totals (Release): solution suite 2038 total, 2024 passed, 13 skipped (the tolerated headless skips), 1 failed on the first run: `SourceHygieneTests.NoNullForgivingOperator` (the allowlist lines of `ClassEditorViewModelTests` shifted and my new test used a `!`); the new test no longer uses one and the entries moved to 537 and 570 in the same commit; Core.Tests 671 passed and Editor.Tests 598 passed afterwards, UITests passed (5 m 34 s), Desktop E2E (`NETPRINTS_E2E=1`, `--fail-skips on`) 31 passed, 0 skipped. Format and Release build clean.
