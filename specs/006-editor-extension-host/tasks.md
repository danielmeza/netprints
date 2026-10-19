---
description: "Task list for P3 — Editor extension host"
---

# Tasks: Editor Extension Host

**Input**: `specs/006-editor-extension-host/`: plan.md (the sub-phase table, gates and ADR schedule), spec.md, research.md
(R1–R35), data-model.md, contracts/, quickstart.md.

**Start condition**: met when batch P3-S2c (this file, the speckit-analyze fixes and `implementation-notes.md`) is on
draft PR #20 and CI is green. Branch `006-editor-extension-host` from `master` after the P3a merge (PR #12, `bf3496e9`).
ADR-0040 is committed with the spec. User Stories 13 and 14 (FR-094–FR-106, SC-013, SC-014) moved to P3c and have no
task here. Batch P3-S2d moved User Stories 16 and 17 to P3b; batch P3-S2e (2026-10-16, the owner's decision in the
spec's Clarifications) brought them back with User Story 12. The tail is sub-phase G (T136–T146 and its gate T170;
T153 and T154 are the E+F+G checkpoint's review and fixes) and the NuGet packages are sub-phase H (T158–T169). No ID
was renumbered. Counts after S2e: 15 active stories, 105 active FRs, 15 active SCs; 160 active tasks of 170 IDs (10
merged); 50 implementation batches with 190 units, plus 5 units in PR-P (195); 62 batches in all. Review C1+C2 added
T172–T175 (164 active of 175 IDs, 194 units plus 5 in PR-P = 199). P3's release is
0.4.0 (P3a ships as v0.3.0; spec "Version").

**Tests**: REQUIRED (constitution V). A behaviour task names its test class first. "Test first" means: write the test,
run it, see it fail for the expected reason (red), then change the code until it passes (green). The red test is
committed together with the change that makes it green, in the task's own commit, so every commit builds and passes
(bisect); both runs are recorded in implementation-notes.md. Never push a red test. A test written after the code is
reported as such. Every test is xUnit v3 on Microsoft.Testing.Platform and passes
`TestContext.Current.CancellationToken`. UI tests go through page objects and `AutomationIds`, with no sleeps. Desktop
E2E scenarios are one class each (ADR-0006), set `NETPRINTS_STATE_DIR` per lease, and set a temporary
`XDG_CONFIG_HOME` when they touch settings or key bindings. Snapshots and goldens change only with
`NETPRINTS_UPDATE_SNAPSHOTS=1` or `NETPRINTS_UPDATE_GOLDENS=1` in a task that names the change, reviewed by hand, and
the commit says why. Red/green also guards every risky refactor (plan "Standing constraints"): the S0 pin-layout golden
before the renames, the search goldens before SUG1, the M1/M2 baselines before any composition change, each hygiene test
until its analyzer is red on the test's planted violation, and every carry-over defect.

**Rules for the implementer** (plan.md "Standing constraints" and AGENTS.md "Batch rules"): one batch per agent; read
only the task text, the contract sections it names and the code you touch; load `avalonia-xaml` and `avalonia-styling`
(and `avalonia-behaviors` for interaction) before touching any `.axaml`, view, style or converter code; new views use
theme tokens only. Build quietly (`dotnet build -v q -tl:off --nologo`), test after building
(`dotnet test --no-build --no-progress --no-ansi`), filter while iterating, run the whole suite once at the end of the
batch (`dotnet test --solution NetPrints.slnx -c Release --no-build --no-progress --no-ansi -- --ignore-exit-code 8`)
plus the Desktop E2E pass (`NETPRINTS_E2E=1 … --fail-skips on`, AGENTS.md), then `dotnet build -c Release` (0
warnings) and `dotnet format NetPrints.slnx --verify-no-changes`. No `!`, no suppressions, XML docs on every public
member, `[LoggerMessage]` logging, injected `TimeProvider`, source-generated JSON only, async all the way, sealed types,
a named constant for every identifier used in more than one place. Every public API change of a tracked library goes
through its `PublicAPI.*.txt`; the 0.4.0 breaks use `*REMOVED*` entries and release notes, never `[Obsolete]`; every
settling surface carries its NPXE id (research R16) and a row in the extension guide's "API stability" table. Contracts
in Abstractions are UI-free. New UI text comes from resources once L1 lands (T044), and the XAML literal ratchet never
grows. Code comments stay short; rationale goes in the commit message and in
`specs/006-editor-extension-host/implementation-notes.md` as "Decision: …". **Start of every batch**: check CI at the
previous head (`gh run list` once, or ask the coordinator) and fix any red first, in a separate first commit, before
your own tasks. Commit per task (pathspec of your own files), tick the task here, push, and return: do not wait for
CI; the coordinator watches PR #20's CI in the background (plan "Process: CI and checkpoints"). Never leave changes under `samples/`; no machine paths in
repository files (grep the diff); edit repository files only with the Edit and Write tools; never use `DISPLAY=:1`. No
publishing (tags, NuGet pushes, releases, upstream issues). `.specify/memory/*` and the roadmap are not edited by
implementers; proposals go in implementation-notes.md. Commits, PR text and review comments carry no tool or model
attribution. Every user-visible change gets an entry under "Unreleased" in `.github/release-notes.md`. The
`ShellMainFlow` E2E test is watched in every batch: a new failure is fixed in the batch that sees it (FR-088).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel with the other tasks of the same batch (different files, no dependency). Batches still
  run one after another.
- **[Story]**: US1–US12 and US15–US17 from spec.md. Checkpoint, review, fix and merge tasks have no story label.
- **Batch header**: `Batch <id> — model: <sonnet|opus> — T<first>–T<last> — <n> units`; one agent per batch, batches run
  in order. The model tag is the owner-approved process role (AGENTS.md "Model selection"): implementation and fix
  batches are sonnet, reviews are opus.
- **Batch sizing** (plan.md "Process"): an implementation batch carries 2–4 related tasks, or up to 6 when they are
  mechanical, for 3–6 units; a unit is about half a day, a task is 1 unit, and a design-heavy task is 2 (3 for the
  typed contexts). A batch must end under about 150K context with everything pushed (the coordinator confirms CI). A
  gate task carries no units of its own.
- **"Default pending owner"**: the task applies the plan's default for a product question the owner has not answered
  (research R3, R13, R23, R31). Implement the default, record it in implementation-notes under "Owner questions taken
  by default", and keep the change easy to reverse.
- **Sub-phase gates** ("Checkpoint X" tasks): the last task of each sub-phase's implementation batches is its gate:
  the whole suite plus the E2E pass, CI green (including `CLI (Windows)` when it runs), and a report in implementation-notes with evidence (test
  names, CI run ids, artifact names, timings) for the sub-phase's gate in plan.md, the longest CI job (SC-016), the
  Dock check (has a Dock release that includes wieslawsoltes/Dock#1191 shipped? if so, the fix batch does T085's bump),
  and a "Docs updated:" line. A sub-phase is not done until its docs are updated. The gate task's "CI green" is checked
  by the coordinator, and the CI run ids it names are filled in by the next batch's start-of-batch check.
- **Reviews** (owner rule, five checkpoints since 2026-10-16): A+B, C1+C2, D1+D2, E+F+G and the final review. Each is
  one adversarial opus review batch of the group's whole diff (`B-R`, `C2-R`, `D2-R`, `G-R`, `PR-R`) by an agent that
  did not implement it, then one reserved sonnet fix batch (`B-F`, `C2-F`, `D2-F`, `G-F`, `PR-F`) that must be green on
  CI before the next checkpoint's work starts. The reviews of the earlier sub-phases of a group (A, C1, D1, E and F)
  are "merged into" the last one's task; the IDs stay. The reviewer first enumerates the decision corpus (constitution,
  AGENTS.md, the ADRs the group touches, spec.md, plan.md, the contracts) and checks each applicable document against
  the diff. The final review covers sub-phase H in full, the release preparation (PR-P), and the whole PR lightly for
  integration; the merge preparation (PR-M) follows. The owner merges; any release needs the owner's OK.

---

## Phase 1: Foundation (sub-phase A, User Stories 1 and 11, Priority P1) 🎯 MVP

**Goal**: NetPrints' own analyzer and generator projects run in every build; the NPS layout rules hold in `src/` and
`tests/`; services, smart enums and settings validation are declared with attributes; unknown document members survive
a save; the R5-0 baselines and the M5 spike decide the composition path; the P2 code carry-overs are closed.
Contracts: contracts/analyzer-diagnostics.md §1, §2, §5, §7, §8. **Independent test**: US1's independent test; the
US11 scenarios that run in CI. **Gate (plan)**: 0 NPS violations; every A rule and generator tested; FR-007 round
trip; Windows CLI tests green; SC-015; R5-0 numbers and the M5 outcome recorded.

### Batch A1 — model: sonnet — T001–T003 — 4 units

- [x] T001 [US1] Roslyn projects and wiring (FR-001, FR-008, research R2, ADR-0024; 2 units). Test first: create
  `tests/NetPrints.Analyzers.Tests` (xunit.v3, MTP) with `Infrastructure/AnalyzerAssemblyTests.cs`: each new analyzer
  assembly loads into a `CompilationWithAnalyzers` host and reports its supported diagnostics (red: the projects do not
  exist). Move `tests/NetPrints.Catalog.Tests/Generator/GeneratorTestHost.cs` to
  `tests/NetPrints.Testing/Roslyn/GeneratorTestHost.cs` (Catalog.Tests stays green). Then add `src/Shared/Roslyn/`
  (`EquatableArray<T>`, diagnostic helpers; linked into `NetPrints.Annotations` the way it links Catalog sources),
  `src/NetPrints.Authoring`, `src/NetPrints.Authoring.CodeFixes`, `src/NetPrints.RepoAnalyzers` and
  `src/NetPrints.RepoAnalyzers.CodeFixes`: `netstandard2.0`, `Microsoft.CodeAnalysis.CSharp` 5.0.0 through
  `VersionOverride` as `NetPrints.Annotations` does, `AnalyzerReleases.Shipped.md`/`Unshipped.md`, RS1038 clean, the
  RepoAnalyzers pair `IsPackable=false`; all four in `NetPrints.slnx`. Wiring: `src/Directory.Build.props` and
  `tests/Directory.Build.props` reference the RepoAnalyzers pair with `OutputItemType="Analyzer"
  ReferenceOutputAssembly="false"`; `src/NetPrints.Core/NetPrints.Core.csproj` references the Authoring pair the same
  way and packs both under `analyzers/dotnet/cs`; `tests/Directory.Build.targets` adds Authoring to every test project
  that references Core. A user's NetPrints project builds as before (the CI sample compile step stays green). Measure
  the build with `-p:ReportAnalyzer=true` before and after; record it in implementation-notes (target: under 2%).
- [x] T002 [US1] The Analyzers CI leg (FR-001, FR-118, research R28; 1 unit). Test first:
  `tests/NetPrints.Core.Tests/Core/CiWorkflowTests.cs` already requires every test project in the `test` matrix; run it
  and see it name `NetPrints.Analyzers.Tests` (red). Add the `Analyzers` row to the matrix in `.github/workflows/ci.yml`
  (artifacts `test-results-analyzers`, `coverage-analyzers`) under the unchanged "Build and test (Linux)" aggregate.
  Update the project table of `docs/contributing/testing.md`. Green.
- [x] T003 [P] [US1] ADR-0024 and the diagnostics guide (FR-008, ADR-0024, contracts/analyzer-diagnostics.md §1; 1
  unit). Write `docs/adr/0024-analyzer-and-generator-infrastructure.md`: the four projects, shipped versus internal
  rules, the families NPS, NPA, NPH and NPI, the test style (snapshot, compile and incrementality tests; exact-span rule
  tests; `AdhocWorkspace` fix tests; no `Microsoft.CodeAnalysis.Testing`), the build-time budget; it amends ADR-0003's
  StyleCop note (add the amendment line to ADR-0003) and is listed in `docs/adr/README.md`. Test first:
  `tests/NetPrints.Editor.Tests/Docs/DiagnosticsGuideTests.cs` compares `docs/guide/diagnostics.md` with every
  `*DiagnosticCodes` class and every `AnalyzerReleases.*.md` (red: the page does not exist). Then write the page with
  every existing prefix (NPC, NPD, NPE, NPT, NPW, NPX, NPXE) and code, its severity and a one-line cause. Every later
  task that adds a code adds its row; this test keeps them honest. Green.

### Batch A2 — model: sonnet — T004–T005 — 3 units

- [x] T004 [US1] NPS001–NPS003 (FR-002, research R3, contracts/analyzer-diagnostics.md §2; 2 units; **default pending
  owner**: non-public fields and consts, `internal` and `protected` ones included, may stay grouped — AOP follow-up
  question 8, "the blank-line rule"). Test first: `tests/NetPrints.Analyzers.Tests/Style/Nps001Tests.cs`,
  `Nps002Tests.cs` and `Nps003Tests.cs` with a failing and a passing case at exact spans for every row of the contract
  (an attribute on its member's line; two attributes on one line; each NPS002 member category; grouped private fields
  pass; enum members and record parameters are out of scope; NPS003 reports at suggestion and never fails a build), and
  before/after tests of each fix through an `AdhocWorkspace`. Then `src/NetPrints.RepoAnalyzers/Style/` (the rules and
  `NpsDiagnosticCodes`) and `src/NetPrints.RepoAnalyzers.CodeFixes/Style/` (split attributes onto their own lines,
  insert a blank line). This commit must build: the rules stay inactive in the repository build until T005 switches
  them on (a `CompilerVisibleProperty` switch, or wiring added in T005; record the choice). Rows in
  `docs/guide/diagnostics.md` and the release files.
- [x] T005 [US1] The NPS sweep (FR-002, SC-001, research R3; 1 unit). One mechanical commit: switch the rules on, then
  `dotnet format analyzers NetPrints.slnx --diagnostics NPS001 NPS002 --severity error` over `src/` and `tests/` (about
  274 and 177 sites). The diff holds only attribute line splits and blank lines (check `git diff --stat` and samples);
  goldens and generated files are unchanged. The build reports 0 NPS001 and NPS002; the whole suite is green.

### Batch A3 — model: sonnet — T006–T007 — 3 units

- [x] T006 [US1] R5-0 baselines (FR-004, SC-005, research R4 step 1; 1 unit). Add
  `tests/NetPrints.Editor.Tests/Hosting/ShellStartupPerformanceTests.cs` (M1: composition, the shell host and a ready
  shell view model, headless) and `tests/NetPrints.Editor.Tests/Graph/GraphScopePerformanceTests.cs` (M2: a generated
  graph of 1000 nodes and about 4000 pins; time and allocated bytes; counting fakes that record per-pin resolutions),
  both in the filter of the "Performance budgets" job of `.github/workflows/ci.yml`, each writing its numbers to the
  test output. They pin today's behaviour and carry no budget yet (C1 adds the +10% gates). Record the baselines (the
  median of the perf job's runs, with run ids) and the current M4 trim count under "R5-0 baselines" in
  implementation-notes.
- [x] T007 [US1] The M5 spike (FR-004, research R4 step 1, ADR-0031; 2 units; gate; not merged). In a local branch or a
  scratch copy that is never pushed to this PR, compose a copy of the editor's composition
  (`src/NetPrints.Editor/Hosting/EditorComposition.cs`) three ways: hand-wired (today), MS.DI with factory registrations
  in the shape the T008 generator will emit, and Pure.DI; measure with BenchmarkDotNet the composition time, the first
  resolves, the allocations and the generated file size. Record the numbers, the environment and the verdict on
  ADR-0031's terms (generated file under about 600 lines; every needed shape expressible) under "M5 spike" in
  implementation-notes. If MS.DI with the generator fails, stop the batch and report: the coordinator rewrites T008 and
  the C1 composition tasks (T053–T055) before they start (plan "Gate outcomes"). Only the notes reach the PR.

### Batch A4 — model: sonnet — T008–T010 — 4 units

- [x] T008 [US1] DI attributes and the registration generator (FR-003, research R4 step 2, contracts/analyzer-
  diagnostics.md §5; 2 units). Test first: `tests/NetPrints.Analyzers.Tests/Composition/RegistrationGeneratorTests.cs`
  (a snapshot of `AddEditorServices()` for a sample compilation, a compile test, an incrementality test) and
  `NpiCompositionRuleTests.cs` (NPI001–NPI004 failing and passing at exact spans). Then
  `src/NetPrints.RepoAnalyzers/Composition/`: `[Singleton<TService>]`, `[SessionScoped<TService>]` and `[ScopeFactory]`
  emitted as internal post-initialization sources; the generator emits factory-lambda registrations (constructor calls;
  no reflection activation, no assembly scanning) and implements the `Create` methods of `partial` `[ScopeFactory]`
  classes; NPI001–NPI004 with `NpiDiagnosticCodes`. No editor service is annotated yet (C1 does it, one step at a
  time). Guide rows.
- [x] T009 [US1] `[SmartEnum]` (FR-005, research R5, ADR-0034; 1 unit). Test first:
  `tests/NetPrints.Analyzers.Tests/SmartEnums/SmartEnumGeneratorTests.cs` (snapshot: only the companion id enum and the
  `All` list; compile; incrementality) and NPI010/NPI011 cases. Then the generator in
  `src/NetPrints.RepoAnalyzers/SmartEnums/`. Guide rows.
- [x] T010 [US1] `CommandKey`, `CommandKeyId` and gesture parsing (FR-005, research R5, ADR-0034; 1 unit). Test first:
  `tests/NetPrints.Editor.Tests/Contributions/CommandKeyTests.cs` (the 1:1 mapping of `CommandKey.All` to
  `CommandKeyId`; per-member behaviour with no switch over keys; a test that touches the static constructor, so a
  `TypeInitializationException` fails it — US1 scenario 4) and `CommandGestureTests.cs` (the `ISpanParsable<T>` and
  `ISpanFormattable` round trip; the `FrozenDictionary` alternate-span lookup; the Avalonia key map covers every key a
  gesture uses). Then `src/NetPrints.Editor/Contributions/CommandKey.cs` (abstract, private nested sealed members),
  span parsing and formatting in `CommandGesture.cs`, and `CommandDescriptor.DefaultGestures` as
  `IReadOnlyList<CommandGesture>?` with every built-in descriptor updated. Gesture strings are parsed only at the
  boundaries (key bindings, manifests, display).

### Batch A5 — model: sonnet — T011–T016 — 5 units

- [x] T011 [US1] Settings validators at start (FR-006, research R6, ADR-0040; 1 unit). Test first:
  `tests/NetPrints.Editor.Tests/Hosting/SettingsValidationTests.cs`: for each existing settings section (`netprints`,
  `extensions.netprints.editor`), an invalid value makes composition use that section's defaults, log one message
  naming the key and the reason, and still start (US1 scenario 5; quickstart US1 step 4). Then each section becomes a
  sealed options class with `init` members in a source-generated JSON context, validated by an `[OptionsValidator]`
  validator over DataAnnotations (add `Microsoft.Extensions.Options` 10.0.12 to `Directory.Packages.props`); the
  composition calls the validators itself (no generic host). Extension sections and NPX014 come in T088.
- [x] T012 [US1] Unknown document members (FR-007, research R7, data-model §7; 1 unit). Test first:
  `tests/NetPrints.Core.Tests/Serialization/UnknownMembersTests.cs`: class, graph and node documents with unknown
  members at several levels load and save with the members after the known ones, in ordinal key order (US1 scenario
  6); `tests/NetPrints.Cli.Tests/` cases: `format` and `show` keep them as opaque values, and the merge driver takes one
  side's change and reports a conflict when both sides changed the same unknown member. Then a `[JsonExtensionData]`
  member (`IReadOnlyDictionary<string, JsonElement>?`, init-only, last) on every document record in
  `src/NetPrints.Serialization/Documents/`, the canonical writer, `format`, `show` and the merge driver. Every golden is
  unchanged. `docs/guide/graph-format.md` says unknown members survive a save. This lands before any new document field
  (T035, T067).
- [x] T013 [US11] Extension and reference carry-overs (FR-082, FR-083, research R29; 1 unit). Test first: in
  `tests/NetPrints.Core.Tests/Extensibility/ExtensionLoaderTests.cs`, an `Fx` fixture under `tests/Fixtures/Extensions/`
  whose private dependency conflicts with a host-provided or a transitive reference produces NPX008 (US11 scenario 1);
  in `tests/NetPrints.Editor.Tests/Reflection/`, a project with an unreadable reference loads the other references and
  shows NPW009 once as a warning (US11 scenario 2). Then `src/NetPrints.Extensibility/Loading/` and the live reflection
  provider; the NPW009 constant and guide row.
- [x] T014 [US11] No generation on load; the two Windows CLI defects (FR-085, FR-086, FR-087, SC-015; 1 unit). Test
  first: `tests/NetPrints.Editor.Tests/Hosting/ProjectLoaderTests.cs` and a Desktop `--check-project` test: loading
  writes no generated file (compare the tree before and after; US11 scenario 5); in `tests/NetPrints.Cli.Tests/`, a
  catalog checked out with CRLF line endings is not stale for `netprints catalog --check` (US11 scenario 3), and
  `GitInstallCommandTests` proves the `--command` path survives `sh` on Windows (US11 scenario 4). The Windows tests run
  on the `CLI (Windows)` leg and are red there first. Then the fixes, `.gitattributes` `*.npcat.json text eol=lf`, and
  the `cli-windows.yml` path filter if it misses a file. Both Windows tests green.
- [x] T015 [US11] FU-5 triage (FR-084, SC-015; 1 unit). Run `dotnet format analyzers NetPrints.slnx --severity info
  --verify-no-changes --no-restore` over `src/` (210 hits at P3a's final run). For each rule: fix the code (preferred)
  or record the rule in ADR-0003 with its reason. No suppression in code and no severity change. End state: 0
  unrecorded info-level hits in `src/`; the counts per rule before and after in implementation-notes.
- [x] T016 **Checkpoint A**. Whole suite plus the E2E pass; CI green, including the Analyzers leg and `CLI (Windows)`.
  Report the plan's gate A in implementation-notes: 0 NPS violations (the build); every A rule and generator with its
  tests (list); the FR-007 round trip; SC-015 (the Windows run id with the CRLF and `git-install` tests; 0 unrecorded
  info-level hits); the R5-0 numbers and the M5 verdict; the longest CI job; the Dock check. Docs updated: ADR-0024,
  ADR-0003, `docs/guide/diagnostics.md`, `docs/guide/graph-format.md`, `docs/contributing/testing.md`.

### Batch A-R — no batch (T017 merged into T038)

- [x] T017 Merged into T038: the review of sub-phase A runs with sub-phase B's, at the A+B checkpoint (batch B-R).

### Batch A-F — no batch (T018 merged into T039)

- [x] T018 Merged into T039: the fixes for sub-phase A's findings run in batch B-F.

---

## Phase 2: Node model and search, Core 0.4.0 (sub-phase B, User Story 2, Priority P1) 🎯 MVP

**Goal**: node search is built from providers owned by node kinds, with one creation service and one ranker; pins are
typed properties declared by convention and tracked by key; the 8 renames (first counted as 13) ship as a hard break; capabilities are role
interfaces; pins carry roles, `out` and `ref` parameters get output pins, and purity follows the roles; `sbyte` and
`decimal` are full primitives. Saved graphs do not change. **Independent test**: US2's independent test. **Gate
(plan)**: SC-002; the FR-017 budget; the trim ratchet down by at least 2; the pack spike's outcome recorded.

### Batch B1 — model: sonnet — T019–T020 — 3 units

- [x] T019 [US2] Pin the search behaviour first (FR-016, FR-017, research R10; 1 unit). Before any search change, list
  the scenarios of `tests/NetPrints.Editor.Tests/Search/NodeSearchGoldenTests.cs` in implementation-notes and add any
  P3a characterization scenario it lacks (exec output, typed input, typed output, canvas, type-scoped search); add
  `NodeSearchInvariantTests.cs`: opening the search creates no node and leaves the graph and the undo stack unchanged
  (US2 scenario 6). All of these pass on today's code; they are the invariants for SUG1–SUG3.
- [x] T020 [US2] SUG1: providers by node kind (FR-016, research R10, ADR-0028; 2 units). Test first:
  `NodeSearchProviderTests.cs` (each provider is owned by its node kind; the fixed provider order) and a counting
  provider that proves providers are built once per registry, never per open (research R10 "M3"). Then extract
  `SuggestionRows.Build`; about 12 internal providers in `src/NetPrints.Editor/NodeSearch/Providers/`, grouped by the
  node kind that owns them (CallMethod → methods, Variable get/set → variables, Constructor → constructors, Type →
  types, …); `NodeSearchEntry` records replace `object Value`; rename `SuggestionListViewModel` → `NodeSearchViewModel`
  and `SuggestionItem` → `SuggestionRowViewModel` (types, files with `git mv`, `.axaml`, page objects, tests; the
  `src/NetPrints.Editor/Search/` search files move to `NodeSearch/`); the catalog-metadata part (titles and keywords from `CatalogNodeHint`) moved to T051 (AB-10). The
  goldens of T019 stay green.

### Batch B2 — model: sonnet — T021–T023 — 4 units

- [x] T021 [US2] SUG2: one node creation service (FR-016, FR-117, research R10; 2 units). Test first:
  `tests/NetPrints.Editor.Tests/NodeSearch/NodeCreationServiceTests.cs` (clamp, connect and one undo step; prompts
  through a fake adapter; every entry creates its node through a `Func<NodeGraph, Node>` factory). Then
  `INodeCreationService`, a prompts adapter over the dialogs, and factories in place of `Activator` and
  `GetConstructor`; lower `eng/trim-warnings.txt` by the two IL warnings this removes (named in the commit).
- [x] T022 [US2] SUG3: one ranker (FR-016, FR-017, SC-002, research R10; 1 unit). Test first:
  `NodeSearchRankerTests.cs`: the default `ISuggestionRanker` reproduces the goldens with the fixed category order.
  Then the ranker. The one recorded order change (This Variables and This Methods) updates its golden in this commit,
  listed with the reason in implementation-notes. `SearchPerformanceTests.cs` stays within P3a's budget: 30,000 rows,
  open under 2 s, keystroke under 300 ms (FR-017).
- [x] T023 [US2] ADR-0028 (research R10, plan ADR schedule; 1 unit). Write
  `docs/adr/0028-node-search-providers-and-creation.md`: providers grouped by kind, the creation service and factories,
  the ranker, providers built once per registry, the recorded order change, and SUG4's public move in C1. Add it to
  `docs/adr/README.md`.

### Batch B3 — model: sonnet — T024–T026 — 4 units

- [x] T024 [US2] Typed pins and pin attributes (FR-009, FR-010, research R8 S2a; 1 unit). Test first:
  `tests/NetPrints.Core.Tests/Graph/TypedPinTests.cs`: `NodeInputDataPin<T>` and `NodeOutputDataPin<T>` default to
  `default(T)`, with no generic-math constraint; untyped pins behave as before. Then the typed pins, `[NodeKind]`,
  `[NodeBase]`, `[Pin("key")]` (with `Role`), `[PinType(nameof(member))]` and `[NodePinLayout]` in
  `src/NetPrints.Core/Graph/`, with `PublicAPI.Unshipped.txt`.
- [x] T025 [US2] The pin convention generator (FR-010, research R8 S2a, ADR-0025; 2 units). Test first:
  `tests/NetPrints.Analyzers.Tests/Pins/PinConventionGeneratorTests.cs`: snapshots for a node kind with typed pins, a
  `[Pin("…")]` override, a `[NodeBase]` base with pin offsets and a BCL `[DefaultValue]`; a compile test; an
  incrementality test (US2 scenario 1). Then `src/NetPrints.Authoring/Pins/`: the generator implements `partial` pin
  properties and `DeclarePins()`; a key is the property name without `Pin` unless `[Pin("…")]` overrides it.
- [x] T026 [US2] The pack spike (research R12, plan "Gate outcomes"; 1 unit). Pack `NetPrints.Core` into
  `local-packages/` with `scripts/pack-local.sh` and build a scratch consumer outside the repository that references
  only the packed Core: the Authoring generator and analyzers run exactly once (`-p:ReportAnalyzer=true`; generated
  sources present; no duplicate hint names). Repeat through a scratch package that depends on Core with
  `PrivateAssets="none"`, standing in for Abstractions. Record the outcome under "Pack spike" in implementation-notes.
  If the analyzers are missing or duplicated, the coordinator adjusts T106 (the template references Core directly).

### Batch B4 — model: sonnet — T027–T028 — 3 units

- [x] T027 [US2] NPA001–NPA009 (FR-010, SC-001, contracts/analyzer-diagnostics.md §3; 2 units). Test first:
  `tests/NetPrints.Analyzers.Tests/Pins/` one class per rule, failing and passing at exact spans. Then the analyzers in
  `src/NetPrints.Authoring/Pins/` with `NpaDiagnosticCodes`, the release files and the guide rows.
- [x] T028 [US2] NPA code fixes (FR-010; 1 unit). Test first: before/after tests for "make partial" (NPA001), "add
  `[Pin]`" (NPA008) and "add the marker" (NPA009). Then `src/NetPrints.Authoring.CodeFixes/Pins/`.

### Batch B5 — model: sonnet — T029–T030 — 4 units

- [x] T029 [US2] Built-in node kinds declare their pins (FR-010, SC-002; 2 units). Red/green: the S0 golden
  (`tests/NetPrints.Core.Tests/Fixtures/Golden/NodePinLayout.golden.txt`, `NodePinLayoutTests`) is the invariant,
  green before and after. Convert every built-in node kind in `src/NetPrints.Core/Graph/` to typed pin properties
  declared by convention, with `[NodeKind]` or `[NodeBase]`, and `[Pin("…")]` only where the property name does not
  give the stored key (no rename yet). List those overrides in implementation-notes: they are the renames and the
  overrides of T031 (research R8; B6 found 9: 8 renamed, 1 kept). Every fixture and sample graph loads and saves byte-identically; the generated C#
  goldens are unchanged.
- [x] T030 [US2] Pin-key tracking (FR-011, research R8 S4; 2 units). Test first:
  `tests/NetPrints.Analyzers.Tests/Pins/PinKeyTrackingTests.cs`: NPA010 for a declared key not recorded; NPA011 naming
  the old and the new key when a shipped key disappears (US2 scenario 2); the "keep key" and "remove on purpose" fixes.
  Then `NodePins.Shipped.txt` (seeded from the S0 golden, one line per `<kind> <direction>.<exec|data>.<key>`) and
  `NodePins.Unshipped.txt` as `AdditionalFiles` of `NetPrints.Core`, the two analyzers and their fixes; the release step
  (move Unshipped to Shipped) in `docs/contributing/releasing.md`.

### Batch B6 — model: sonnet — T031–T033 — 3 units

- [x] T031 [US2] The 0.4.0 pin renames (FR-012, research R8 S3; 1 unit). Of the `[Pin("…")]` overrides T029 listed,
  rename the properties to `<key>Pin` and drop their override (done: 8 of the 9; first counted as 13 and 2); the one whose new name would clash with an existing
  member keeps its override (the AOP follow-up §2 counts, research R8). Ship it as a hard break: `*REMOVED*` lines in
  `src/NetPrints.Core/PublicAPI.Unshipped.txt`, no `[Obsolete]`, and a breaking entry under "Unreleased" in
  `.github/release-notes.md` listing each old and new name. `NodePins.Shipped.txt`, every
  fixture and every sample graph stay byte-identical (the S0 golden changes in the property-name column only, 12 lines) (US2 scenario 3; NPA011 proves no stored key moved).
- [x] T032 [P] [US2] ADR-0025 and "Declaring pins" (FR-010–FR-012, FR-116, research R8; 1 unit). Write
  `docs/adr/0025-declarative-node-pins.md` (typed pins, the convention, pin-key tracking, the 0.4.0 rename policy) and
  the "Declaring pins" section of `docs/guide/extensions.md` (typed properties, the convention, `[Pin]`, `[PinType]`,
  `[NodeBase]`, the `NodePins` files, the NPA rules). ADR index.
- [x] T033 [P] [US2] `PrimitiveKinds` (FR-018, research R11, ADR-0034; 1 unit). Test first:
  `tests/NetPrints.Core.Tests/Graph/PrimitiveKindsTests.cs`: a golden of the table; unconnected `sbyte` and `decimal`
  inputs get defaults and the numeric editor, and their values emit valid C# (US2 scenario 7). Then
  `src/NetPrints.Core/Core/PrimitiveKinds.cs` (one `INumberBase<T>` row per primitive) replacing the five scattered
  lists, including the one behind the IL2057 warning in `NodePinViewModel` (the one in `TypedValueConverter.ParseEnum` stays: enums are not in the table);
  `eng/trim-warnings.txt` lowered if the count dropped.

### Batch B7 — model: sonnet — T034–T037 — 5 units

- [x] T034 [US2] Node capabilities as role interfaces (FR-015, research R11, ADR-0029; 2 units). Test first:
  `tests/NetPrints.Editor.Tests/Graph/NodeCapabilityTests.cs`: node search, navigation and the inspector find nodes by
  role (`IPinListEditor`, `IOverloadableNode`, editable pin names, `IMemberReferencingNode`), not by concrete node type.
  Then the interfaces in Core, their implementations on the node kinds, and the callers in
  `src/NetPrints.Editor/NodeSearch/`, `Navigation/` and `Inspectors/`; the header role falls back by convention.
- [x] T035 [US2] Pin roles, `out` and `ref` output pins, purity (FR-013, FR-014, SC-002, research R9; 2 units). Test
  first: `tests/NetPrints.Core.Tests/Graph/PinRoleTests.cs` (roles set by the generator and by `CallMethodNode`;
  `CanSetPure` is true exactly when an output data pin has the `Result` or `OutParam` role; the Exception pin never
  counts — US2 scenario 5), `OutRefPinTests.cs` (every `out` and `ref` parameter gets the output data pin
  `out.data.<parameterName>`, carrying its value after the call; an `out` or `ref` input not connected to a variable is
  a validation error — US2 scenario 4) and translator tests for a pure call with `out` parameters. Then `PinRole` in
  Core, roles from the generator (`[Pin(Role = …)]` where the convention cannot tell), `CallMethodNode`'s dynamic pins,
  `Node.CanSetPure`, validation and the translator. Changed goldens (graphs with `out`/`ref` calls; the generated C# of
  a pure call with `out` parameters) are updated with `NETPRINTS_UPDATE_GOLDENS=1` and listed with the reason in
  implementation-notes (SC-002's exception). Schema v1 stays additive (`docs/guide/graph-format.md`).
- [x] T036 [US2] ADR-0029 (FR-013–FR-015, research R9, R11; 1 unit). Write
  `docs/adr/0029-node-capabilities-and-pin-roles.md`: role interfaces, pin roles and the purity rule (owner decisions
  11–13). ADR index.
- [x] T037 **Checkpoint B**. Whole suite plus the E2E pass; CI green. Report the plan's gate B: SC-002 (fixture and
  sample graphs byte-identical across the renames; the golden changes limited to the `out`/`ref` list; the search
  goldens apart from the recorded order change); the FR-017 budget (the `SearchPerformanceTests` numbers); the trim
  count against the start of the phase (down by at least 2); the pack spike's outcome; the longest CI job; the Dock
  check. Docs updated: ADR-0025, ADR-0028, ADR-0029, the extension guide's "Declaring pins", `releasing.md`,
  `graph-format.md`, the release notes.

### Batch B-R — model: opus — T038 (checkpoint A+B review)

- [x] T038 Review sub-phases A and B (the A+B checkpoint): an opus reviewer who did not implement them reviews the
  whole diff of batches A1–B7 (from the commit before T001 to HEAD) against the decision corpus (constitution, AGENTS.md,
  ADR-0003, ADR-0024, ADR-0025, ADR-0028, ADR-0029, ADR-0031, ADR-0034, ADR-0040, spec US1, US2 and US11, research
  R8–R11, plan "Standing constraints", contracts/analyzer-diagnostics.md §1–§3, §5, §7, §8, data-model §7–§8). Check every
  golden change against its recorded reason and every `*REMOVED*` line against the release notes; run US1's and US2's
  independent tests. Findings (severity, file:line, fix) go to the PR as review comments and to implementation-notes
  under "Review A+B". No code changes in this task.

### Batch B-F — model: sonnet — T039 (checkpoint A+B fixes)

- [x] T039 Fix every finding of T038 (test first for behaviour findings), reply on each review thread with the fixing
  commit or the reason for deferral, whole suite plus the E2E pass, push; if the review had no findings, tick this task
  with "no findings". Sub-phase C1 starts only after this batch is pushed and green on CI (the coordinator checks).

---

## Phase 3: Public contracts (sub-phase C1, User Stories 3 and 9, Priority P1)

**Goal**: the contracts live in `NetPrints.Extensibility.Abstractions`; labels are localization keys with default
text; the C# target types are stable and every writer generates for the same language version; contributions get typed
contexts; the editor composes through MS.DI in three gated steps. **Precondition**: T007's verdict. If the M5 spike
failed, the coordinator has rewritten T053–T055 before this phase starts. **Independent test**: US9 scenarios 1–2 and
SC-010; US3 scenarios 5–6. **Gate (plan)**: SC-005; SC-010 (the three writers); the API files reviewed for shape.

### Batch C1a — model: sonnet — T040–T042 — 4 units

- [x] T040 [US3] The Abstractions split (FR-019, research R12, ADR-0030; 2 units). Test first:
  `tests/NetPrints.Core.Tests/Architecture/AbstractionsReferenceTests.cs`: `NetPrints.Extensibility.Abstractions`
  references only Core, Serialization, Reflection, Catalog, `Microsoft.Extensions.Logging.Abstractions` and
  `Microsoft.Extensions.DependencyInjection.Abstractions` (10.0.12, added to `Directory.Packages.props`) and holds no
  `System.Runtime.Loader`; `tests/NetPrints.TestExtension` and the `Fx` fixtures compile against Abstractions only.
  Then create `src/NetPrints.Extensibility.Abstractions/` and move the contracts there with the SOLID report's type
  split (`INetPrintsExtension`, `IExtensionBuilder`, the manifest model, `ExtensionApi`, the node library contracts,
  the settings descriptors, the host channel contracts), keeping namespaces and adding no type forwards;
  `NetPrints.Extensibility` keeps the host (loader, load contexts, `ExtensionHost`, `ExtensionRegistry`,
  `RegistryBuilder`, `BuiltInExtension`, `JsonFileSettingsStore`). Split the `PublicAPI.*.txt` files; add the project to
  `NetPrints.slnx`.
- [x] T041 [US3] `GraphPoint` to Core (research R14, R31; 1 unit). Move `GraphPoint` to `src/NetPrints.Core/Graph/`
  (namespace `NetPrints.Graph`), public and stable from 0.4.0; update its callers and the API files.
- [x] T042 [P] [US3] ADR-0030, the split (FR-019, FR-116, research R12; 1 unit). Write
  `docs/adr/0030-extensibility-split-and-editor-facet.md`: the package split, the allowed references, analyzer delivery
  through `PrivateAssets="none"` on the Core reference, and a "Facet" section that T072 fills. ADR index.

### Batch C1b — model: sonnet — T043–T046 — 4 units

- [x] T043 [US3] `LocalizedText` (FR-027, research R13, data-model §11; 1 unit; **default pending owner**: pin labels
  fall back to the raw key — AOP follow-up question 4, "pin labels"). Test first:
  `tests/NetPrints.Core.Tests/Localization/LocalizedTextTests.cs`: a key with no translation shows its default text
  (US3 scenario 6); the fallback chain (UI culture, its parent, the neutral resx, the default text); a pin label with no
  resource shows the raw key. Then the public `LocalizedText` record struct and its resolver in Core; contribution
  labels become `LocalizedText`; menu ids are separate from labels (`MenuPlacement`).
- [x] T044 [US3] Resource files, convention keys and per-pin automation ids (FR-027, research R13; 1 unit; **default
  pending owner**: the built-in node category stays the single "NetPrints" category — AOP follow-up question 9, "node
  categories"). Test first: every convention key a declaration implies (`Node.<kind>.Title`,
  `Node.<kind>.Pin.<in|out>.<exec|data>.<key>`, `Category.<id>`, `Command.<id>.Label`, `Menu.<menuId>`,
  `Panel.<panelId>.Title`) exists in the neutral resx; a headless page-object test finds a pin by its per-pin automation
  id. Then `src/NetPrints.Core/Graph/NodeStrings.resx` and `src/NetPrints.Editor/Strings.resx` (MSBuild strongly typed
  resources, `PublicClass=true`), `{x:Static}` for new XAML text, per-pin ids in `AutomationIds`, and
  `IExtensionBuilder.AddStringResources` (NPXE0011) for extension resources. The XAML literal ratchet does not grow.
- [x] T045 [US3] NPA040–NPA042 (FR-027, contracts/analyzer-diagnostics.md §3; 1 unit). Test first: one class per rule
  in `tests/NetPrints.Analyzers.Tests/Localization/`. Then the analyzers in `src/NetPrints.Authoring/Localization/`
  (NPA040 is a warning for authors and an error in this repository, set in `.editorconfig` as the contract says); guide
  rows; 0 hits in `src/`.
- [x] T046 [US3] The language setting, Semi and Ursa locale, ADR-0027 (FR-027, FR-044, research R13, ADR-0023
  amendment; 1 unit; **default pending owner**: a language change applies after a restart, with no live switch — AOP
  follow-up question 5, "live language switch"). Test first: `netprints.language` (a string; empty means the OS
  culture) sets the UI culture at start, and an invalid value is refused by the section's validator; a headless test
  shows Semi and Ursa following the app culture. Then the key in the `netprints` section, the locale wiring in
  `src/NetPrints.Editor/EditorApp.axaml.cs`, `docs/adr/0027-localization-keys-and-display-text.md`, and the ADR-0023
  amendment.

### Batch C1c — model: sonnet — T047–T050 — 4 units

- [x] T047 [US9] `CSharpLanguageVersion` and `CSharpTarget` (FR-028, FR-070, FR-071, research R19, data-model §6; 1
  unit). Test first: `tests/NetPrints.Core.Tests/Projects/CSharpTargetTests.cs`: parsing `latest`, `latestMajor`,
  `preview`, `default`, `major.minor` and unknown values; `latest`, `preview` and `default` resolve to the smaller of
  the framework's maximum and `MaxKnown` (US9 scenario 1); a multi-targeted project uses the minimum version and the
  common capabilities (scenario 2); a version newer than the framework's default is kept; an unknown version or one
  newer than `MaxKnown` resolves to `MaxKnown`. Then the stable types in `src/NetPrints.Core/Projects/`, the `MaxKnown`
  constant, `Unconstrained`, and the target on `ProjectSnapshot` (a non-positional `init` member) reached through
  `Project`.
- [x] T048 [US9] Evaluation capture and NPW006–NPW008 (FR-070, FR-071; 1 unit). Test first: six workspace fixtures in
  `tests/Fixtures/CSharpTarget/` (no `LangVersion`, `latest`, `preview`, `10.0`, `16.0`, multi-target) with workspace
  tests asserting each target and info message (the `16.0` edge case says the editor's maximum is used). Then capture
  `LangVersion`, `MaxSupportedLangVersion` and `TargetFramework(s)` in `src/NetPrints.Workspace/MsBuildProjectSystem.cs`;
  NPW006–NPW008 constants and guide rows.
- [x] T049 [US9] The generate request and the three writers (FR-072, SC-010; 1 unit). Test first:
  `tests/NetPrints.Cli.Tests/Generation/CSharpTargetWriterIdentityTests.cs`: for each of the six fixtures, the editor's
  save path, `netprints regen` and the SDK's generate target write byte-identical files. Then the language version
  travels in the generate request (`src/NetPrints.Generator/`, the target in `src/NetPrints.Sdk/`, `netprints regen`, the
  editor's save).
- [x] T050 [US9] The translator's `Target` and literal formatting (FR-075; 1 unit). Test first: translator tests in
  `tests/NetPrints.Core.Tests/Translator/`: `Target` is reachable from the execution translation context through a
  default-implemented member (existing translators compile unchanged), and literals are formatted for the target's
  language version. Then the member and the formatting in `src/NetPrints.Core/Translator/`. Goldens of the default
  fixtures unchanged.

### Batch C1d — model: sonnet — T051–T052 — 5 units

- [x] T051 [US3] SUG4: public node search providers (FR-021, FR-028, FR-030, research R10, R16; 2 units). Test first:
  an `Fx` extension that registers a provider with `AddSuggestionProvider` gets its rows in node search; a provider id
  without the manifest prefix is NPX013 and a duplicate id NPX004; an entry's `Requires` carries a `CSharpFeature`.
  Then move the provider contracts (`INodeSuggestionProvider`, `NodeSearchContext`, `NodeSearchEntry`) to Abstractions
  under NPXE0006, add `AddSuggestionProvider`, and add `CSharpFeature`, `CSharpFeatureId`, `Requires` and `Availability`
  under NPXE0007; both ids in `src/NetPrints.Core/ExperimentalApiIds.cs`. The conformance check for providers lands in
  T103 (NPK013, NPK015). Also fill `NodeSearchEntry.Keywords` and the row titles from the catalog node hints
  (`CatalogNodeHint` `DisplayName`, `Category`, `Keywords`) once the providers can reach them (moved from T020, AB-10).
- [x] T052 [US3] Typed contexts (FR-022, research R14, contracts/extension-api.md §3; 3 units). Test first:
  `tests/NetPrints.Editor.Tests/Contributions/TypedContextTests.cs`: every member of `ICommandContext`,
  `IPanelContext`, `IStartPageContext`, `IStatusBarContext`, `ISettingsPageContext` and `IDialogContext` is non-null in
  the editor host; a context-menu item whose `AppliesTo` is false is hidden, not disabled; handlers are stateless and
  receive `ICommandContext`. Then the contexts and the types they expose (`IEditorShell`, `IDialogService`,
  `IEditorOutput`, `CommandSelection`, `CommandScope`) in Abstractions, namespace `NetPrints.Extensibility.Editor`,
  under NPXE0005; `IProjectSession` (project, `CSharpTarget`, reflection, active graph, output, the extension's session
  services) and `IGraphEditor` (shared with `INodeCreationContext`: `Place`, selection, `IEditorUndo`);
  `ICommandHandler` with `CanExecute`, `ExecuteAsync(ICommandContext, CancellationToken)` and `DynamicLabel`. Every
  built-in handler and descriptor factory moves to typed contexts; `NoServices`, `StartPageServices` and the
  `?? new Default()` fallbacks go.

### Batch C1e — model: sonnet — T053–T054 — 4 units

- [x] T053 [US1] Composition step 1: app and shell (FR-003, FR-004, SC-005, research R4 step 3, gate M1; 2 units).
  Red/green: T006's M1 test is the invariant. Annotate the app and shell services with `[Singleton<T>]`; the
  composition root (`src/NetPrints.Editor/Hosting/EditorComposition.cs`) builds a `ServiceCollection` from the
  generated `AddEditorServices()` plus the host instances; `EditorContext` stays a facade;
  `tests/NetPrints.Editor.UITests/Hosting/TestComposition.cs` swaps doubles with `services.Replace`; the options
  validators of T011 run at composition (US1 scenario 3). Gate M1: composition at most 150 ms including the provider
  build and the first resolves, and M1 at most 10% over its R5-0 baseline in the perf job. If the gate fails, stop
  here and report (FR-004): T054 and T055 wait for the owner.
- [x] T054 [US1] Composition step 2: the project session (FR-003, FR-004, research R4 step 4; 2 units). Test first:
  `tests/NetPrints.Editor.Tests/Hosting/CompositionResolveAllTests.cs` resolves every registration in the root and in a
  project session scope (the gate). Then extract `ProjectSessionScope` from `ProjectLoader`; backups, recovery and
  templates become `[SessionScoped<T>]`; one container per project session, disposed with it.

### Batch C1f — model: sonnet — T055–T057, T171 — 5 units

- [x] T171 [US3] Display of node titles, pin labels, category names and extension resources through the resx (FR-027,
  research R13, ADR-0027; 2 units; added after C1d: C1b and C1c left the display on the C# strings, and no task owned
  it). Test first: with the UI culture set to a culture that has a translation of `Node.<kind>.Title`, a built-in
  node's header, a shipped pin's label and the `Category.netprints` search header show the translation; an extension
  that adds a `ResourceManager` with `AddStringResources` has its node title, pin label and category resolved through it;
  a key with no translation shows the default text. Then switch `NodeViewModel` titles, `NodePinViewModel` labels, the
  node search categories and `NodeSuggestion.DisplayName` to `LocalizedText.Resolve` over `CoreStrings.Resources` and
  `ExtensionRegistry.StringResources`; the XAML literal ratchet does not grow and the goldens of T019 stay green (the
  default text is the current text). Remove the "not switched yet" deviation from ADR-0027.

- [x] T055 [US1] Composition step 3: classes and graphs (FR-003, FR-004, SC-005, research R4 step 5, gate M2; 2 units).
  Red/green: T006's M2 test with counting fakes. The generated `ClassContextFactory` and `GraphScopeFactory`
  (`[ScopeFactory]`) replace the hand-written ones; nothing is resolved per row, node or pin. Gate M2: zero per-pin
  resolutions, allocations per node at most the baseline plus 10%, and M2 time at most 10% over its R5-0 baseline. A
  failed gate stops here and is reported.
- [x] T056 [US3] The API shape review and the stability table (FR-030, FR-116, research R16; 1 unit). Review the API
  files of Core, Serialization, Abstractions and Extensibility against contracts/extension-api.md; every settling
  surface so far carries its NPXE id (NPXE0005, NPXE0006, NPXE0007, NPXE0011). Write the "API stability" section of
  `docs/guide/extensions.md` (research R16's table, the opt-in `<NetPrintsExperimentalOptIn Include="NPXE00nn" />`).
  Test: a compile test in `tests/NetPrints.Analyzers.Tests/` shows an `Fx` source using an NPXE API without the opt-in
  fails with that id (US3 scenario 5). Shape findings in implementation-notes.
- [x] T057 **Checkpoint C1**. Whole suite plus the E2E pass; CI green. Report the plan's gate C1: SC-005 (M1 and M2
  against the R5-0 baselines, perf job run ids; the M1 composition time); SC-010 (`CSharpTargetWriterIdentityTests`
  run); the API shape review of T056; the longest CI job; the Dock check. Docs updated: ADR-0027, ADR-0030 (split),
  ADR-0023, the extension guide's "API stability", the diagnostics guide.

### Batch C1-R — no batch (T058 merged into T077)

- [x] T058 Merged into T077: the review of sub-phase C1 runs with sub-phase C2's, at the C1+C2 checkpoint (batch C2-R).
  Its extra checks (the composition gates' evidence; Abstractions stays UI-free; ADR-0017, ADR-0023, ADR-0027, ADR-0038,
  research R4, R10, R13, R19, contracts/extension-api.md §1–§6) are listed in T077.

### Batch C1-F — no batch (T059 merged into T078)

- [x] T059 Merged into T078: the fixes for sub-phase C1's findings run in batch C2-F.

---

## Phase 4: Facets and the publish gate (sub-phase C2, User Stories 3 and 4, Priority P1)

**Goal**: editor contributions register through an editor facet that hosts without an editor ignore; the built-in
editor is an extension like any other; extensions get services, dialogs, status-bar items, settings pages, templates
with files and node-data migrations; the packages pack and validate; after the publish gate the hygiene tests become
analyzers. **Independent test**: US3's independent test. **Gate (plan)**: SC-001 (NPH); package validation and the API
checks pass; 100% XML docs on the published packages; `NetPrints.TestExtension` builds against the packed packages;
SC-005 rechecked (research R4).

### Batch C2a — model: sonnet — T060–T062 — 4 units

- [x] T060 [US3] The editor facet and its Null Object (FR-023, research R14, contracts/extension-api.md §2; 1 unit).
  Test first: in `tests/NetPrints.Cli.Tests/`, `netprints build` and the SDK generator load an `Fx` extension that
  registers editor contributions and ignore them without error (US3 scenario 3). Then `IExtensionBuilder.Editor`
  returning `IEditorContributions` (NPXE0005) in Abstractions, the descriptor types moved there from
  `src/NetPrints.Editor/Contributions/`, and a Null Object handed out by the CLI, the generator and the build.
- [x] T061 [US4] The built-in editor as an extension (FR-021, research R14; 2 units; load `avalonia-xaml` and
  `avalonia-styling`). Test first: `tests/NetPrints.Editor.Tests/Contributions/BuiltInEditorExtensionTests.cs`: every
  built-in contribution is registered through the facet by `netprints.editor`, before any other extension, and the
  built-in registry content equals P3a's (a snapshot of ids, order and gestures). Then
  `src/NetPrints.Editor/Contributions/BuiltIn/` registers through the facet; the DataTemplates move to
  `src/NetPrints.Editor/BuiltInViews.axaml`, added with `AddViewResources`; the internal registry API goes.
- [x] T062 [US4] View resources and icon ids (FR-021, research R14 N8, ADR-0021; 1 unit). Test first: an extension icon
  id `<extension id>.<name>` resolves through `Icon.<extension id>.<name>` in its `AddViewResources` dictionary; an
  unknown id draws the fallback glyph and records NPX010. Then the resolver in `src/NetPrints.Editor/Icons/`.

### Batch C2b — model: sonnet — T063–T065 — 4 units

- [x] T063 [US3] Extension services and host exports (FR-024, research R15, contracts/extension-api.md §8; 2 units).
  Test first: `tests/NetPrints.Core.Tests/Extensibility/ExtensionServicesTests.cs`: an `Fx` extension resolves its own
  registrations and only the allow-list (`ILoggerFactory`, `ILogger<T>` for its own types, `TimeProvider`,
  `IOptionsMonitor<T>` for its own sections — US3 scenario 4); closed generics over host types and
  `IConfigureOptions`, `IPostConfigureOptions` or `IValidateOptions` for host options are rejected; the allow-list is
  pinned; disposal runs sessions, then the provider, and one throwing `Dispose` does not stop the rest. Then
  `IExtensionBuilder.Services` (NPXE0008), `HostExports`, one provider per extension owned by `ExtensionRegistry`, and
  the per-extension accessor behind `IProjectSession.Services`.
- [x] T064 [US4] Dialogs (FR-021, research R14 R4; 1 unit). Test first: a dialog registered with `AddDialog` is shown by
  id through `IDialogService` with its `IDialogContext`. Then `DialogDescriptor` and `AddDialog`.
- [x] T065 [US4] The status-bar kind and the built-in segments (FR-021; 1 unit; load `avalonia-xaml` and
  `avalonia-styling`). Test first: `tests/NetPrints.Editor.Tests/Shell/StatusBarViewModelTests.cs`: the built-in
  segments (error and warning counts, build state, run state, zoom, selection count) come from status-bar items, left
  or right by order; an item whose `AppliesTo` is false is hidden. Then `StatusBarItemDescriptor`, `AddStatusBarItem`,
  and `src/NetPrints.Editor/Shell/StatusBarViewModel.cs` built from items. The status bar looks the same (snapshot
  baselines unchanged).

### Batch C2c — model: sonnet — T066–T067 — 4 units

- [x] T066 [US3] Templates with files (FR-025, research R17, NPXE0012; 2 units). Test first:
  `tests/NetPrints.Editor.Tests/StartPage/ProjectTemplateFilesTests.cs`: `NetPrintsProject` is replaced in paths and
  text, and the `NetPrintsRootNamespace` and `NetPrintsProfileId` tokens by their symbols; an optional
  `Properties/launchSettings.json` is written like any file; the built-in Console and Library templates produce the same
  tree as P3a's New project (a byte comparison with a recorded tree). Then `TemplateFile` and
  `ProjectTemplateDescriptor.Files`, the console and library trees embedded in `src/NetPrints.Workspace/Templates/`,
  `src/NetPrints.Editor/StartPage/ProjectTemplateService.cs` writing through them, and `AddProjectTemplate`
  (NPXE0012) on the builder.
- [x] T067 [US3] Node-data migrations (FR-026, research R18, NPXE0013; 2 units). Test first:
  `tests/NetPrints.Core.Tests/Serialization/NodeDataMigrationTests.cs`: a fixture graph whose `Fx` node has no
  `kindVersion` (version 0) is migrated on open and loads (US3 scenario 7); migrations run in `FromVersion` order up to
  the kind's `DataVersion`; `kindVersion` is written only when above 0, so every built-in node and every existing file
  stays byte-identical; a migration whose kind lacks the `<manifest id>/` prefix is rejected with NPX013. Then
  `NodeKindDescriptor.DataVersion`, `kindVersion` in the serializer, `INodeDataMigration` and `AddDocumentMigration`;
  `docs/guide/graph-format.md` documents `kindVersion`.

### Batch C2d — model: sonnet — T068–T070 — 3 units

- [x] T068 [US5] The settings page kind and the settings metadata attributes (FR-021, FR-040, research R6, R16; 1
  unit). Test first: an `Fx` page registered with `AddSettingsPage` reaches the registry; the schema transform reads
  `[RequiresRestart]`, `[SettingPath(PathKind.File|Folder)]`, `[Description]` and a choices provider id into
  `x-netprints-restart`, `x-netprints-path`, `description` and `x-netprints-choices`. Then `SettingsPageDescriptor`
  (NPXE0005, `CreateViewModel` included; ADR-0040 amendment 1), the attributes (NPXE0009) and `AddSettingsPage`. The Settings document is T089.
- [x] T069 [US3] The manifest's `themes` field (FR-029, contracts/extension-api.md §9, data-model §1; 1 unit; T145
  consumes it, FR-115). Test first: manifest tests: `themes` is accepted from `netprintsApi` 0.4; with an older `netprintsApi` it is NPX001;
  unknown manifest fields are ignored with an info message. Then the manifest model and its validation.
- [x] T070 [US3] The `ExtensionApi.Version` tie (FR-020, research R12; 1 unit). Test first:
  `tests/NetPrints.Core.Tests/Extensibility/ExtensionApiVersionTests.cs`: a recorded hash of the Abstractions and
  Extensibility `PublicAPI.*.txt` files changes only together with `ExtensionApi.Version`. Then set the version to 0.4
  (P3's release is 0.4.0) and record the hash.

### Batch C2e — model: sonnet — T071–T073 — 4 units

- [x] T071 [US3] Packaging and validation (FR-020, SC-003, FR-116, research R12; 2 units). Test first: extend
  `scripts/verify-packages.sh` (run by the `packages` job): every packable library is in `local-packages/` at the local
  version; a scratch consumer that references only `NetPrints.Extensibility.Abstractions` gets the Authoring analyzers
  exactly once; `tests/NetPrints.TestExtension` builds against the packed packages only. Then `EnablePackageValidation`
  on every packable library; `PackageValidationBaselineVersion` for the packages already on nuget.org, with a
  `CompatibilitySuppressions.xml` holding exactly the 0.4.0 breaks, cross-checked by a test against the `*REMOVED*`
  lines; READMEs for the new packages (`eng/PackageReadme.targets`); `scripts/pack-local.sh`; the `packages` job in
  `.github/workflows/ci.yml` validates and packs; `docs/contributing/releasing.md` gains the baseline step for a first
  publish; `docs/api/docfx.json` lists the new packages.
- [x] T072 [US3] The extensions guide on the packages and the ADR amendments (FR-030, FR-038, FR-116; 1 unit). Test
  first: a CLI test pins that a project-declared extension loads in `netprints build` with no trust check (FR-038).
  Rewrite `docs/guide/extensions.md` on the packages: package references instead of project references, the facet, the
  typed contexts, every contribution kind, the NPXE table, and the trust note (project-declared extensions load in the
  CLI and the build without a trust check, as analyzers and build tasks do). Fill ADR-0030's "Facet" section with the
  same trust note; amend ADR-0020 (typed contexts, groups, `AppliesTo`, the new kinds, label keys, menu ids, factories
  through DI) and ADR-0021 (extension icon ids).
- [x] T073 [US3] **The publish gate** (FR-020, FR-030; 1 unit). Check the frozen shape: the published packages have XML
  docs on every public member (CS1591 at error proves it; record the member count), the API files hold every member,
  every settling surface carries an NPXE id, package validation passes, and `NetPrints.TestExtension` builds from
  `local-packages/` (CI run id). From here on, a public shape changes only under an NPXE id or with a recorded
  `*REMOVED*` entry. Record it under "Publish gate" in implementation-notes.

### Batch C2f — model: sonnet — T074–T076 — 3 units

- [x] T074 [US1] NPH001–NPH006 (FR-031, SC-001, research S5, contracts/analyzer-diagnostics.md §4; 2 units). For each
  rule: a test first in `tests/NetPrints.Analyzers.Tests/Hygiene/`, red on the replaced test's planted violation and
  green on the repository; then the analyzer in `src/NetPrints.RepoAnalyzers/Hygiene/` at `error` in `src/`; then the
  replaced test leaves `tests/NetPrints.Core.Tests/Core/SourceHygieneTests*.cs` (NPH001–NPH005). One commit per rule,
  both runs recorded. NPH006 is new (literals passed to known UI sinks). VSTHRD002 and VSTHRD103 go to `error` in
  `tests/`. XAML rule E7 stays a test. Amend ADR-0003 (hygiene rules as analyzers; the suppression ledger as an
  AdditionalFile) and ADR-0007 (E7 stays a test); update the AGENTS.md lines that name the removed tests.
- [x] T075 [US3] NPA020–NPA024 (FR-031, research S6; 1 unit). Test first: one class per rule in
  `tests/NetPrints.Analyzers.Tests/Contributions/`. Then the analyzers in `src/NetPrints.Authoring/Contributions/`;
  guide rows; 0 hits in the repository.
- [x] T076 **Checkpoint C2 (publish gate)**. Whole suite plus the E2E pass; CI green, including the `packages` job.
  Report the plan's gate C2: SC-001 for NPH (0 hits; each replaced test's red/green); package validation and the API
  checks; the XML docs count; `NetPrints.TestExtension` built from the packed packages; SC-005 rechecked in the perf
  job; the longest CI job; the Dock check. Docs updated: the extensions guide, ADR-0030, ADR-0020, ADR-0021, ADR-0003,
  ADR-0007, `releasing.md`, `graph-format.md`, `docs/api/docfx.json`, AGENTS.md.

### Batch C2-R — model: opus — T077 (checkpoint C1+C2 review)

- [x] T077 Review sub-phases C1 and C2 (the C1+C2 checkpoint) against their decision corpus (constitution II, III, IV and
  V, ADR-0003, ADR-0007, ADR-0010, ADR-0017, ADR-0020, ADR-0021, ADR-0023, ADR-0027, ADR-0030, ADR-0031, ADR-0038,
  ADR-0040, spec US3 and US9, FR-019–FR-031, FR-038, FR-070–FR-072, FR-075, research R4, R10, R12–R19,
  contracts/extension-api.md, data-model §1–§2): the whole diff of batches C1a–C2f. Check the composition gates'
  evidence and that Abstractions stays UI-free. The publish gate gets extra weight: every public shape, every NPXE id,
  every `*REMOVED*` line and every allow-list entry. Findings to the PR and to implementation-notes under "Review
  C1+C2". No code changes.

### Batch C2-F — model: sonnet — T078 (checkpoint C1+C2 fixes)

- [x] T078 Fix every finding of T077 (test first for behaviour findings), reply on each thread, whole suite plus the
  E2E pass, push; "no findings" if there were none. Sub-phase D1 starts only after this batch is pushed and green on CI
  (the coordinator checks).

---

## Phase 5: The extension host in the editor (sub-phase D1, User Stories 4, 5, 6, 8 and 11, Priority P1–P2)

**Goal**: extension contributions appear on every surface and leave with their project; failures stay contained;
`--profile` preselects a profile; a Settings document shows every setting on generated or built-in pages; users rebind
any shortcut; the theme presets apply at once; the P3a open items close. Contracts:
contracts/settings-and-keybindings.md, contracts/cli.md §4. **Independent test**: US4, US5, US6 and US8 independent
tests. **Gate (plan)**: SC-004, SC-006, SC-007, SC-009.

### Batch D1a — model: sonnet — T079–T080 — 4 units

- [x] T079 [US4] Contribution groups (FR-032, research R14, data-model §2; 2 units). Test first:
  `tests/NetPrints.Editor.Tests/Contributions/ContributionRegistryTests.cs`: an app group (built-ins first, then
  per-user extensions in load order) and one session group per open project; a session group is removed as a whole
  when its project unloads; every change raises `Changed` with a new immutable snapshot; within a kind the first
  registration wins and built-ins register first. Then groups and snapshots in `src/NetPrints.Editor/Contributions/`;
  the registry is no longer frozen.
- [x] T080 [US4] Surfaces rebuild from snapshots; owners shown (FR-033, US4 scenarios 1–2; 2 units; load
  `avalonia-xaml` and `avalonia-styling`). Test first: headless tests in `tests/NetPrints.Editor.UITests/Shell/`: adding
  and removing a session group updates the menus, the command bar, key bindings, the palette, the shortcuts sheet,
  context menus, the start page, templates and the status bar; the palette and the shortcuts sheet name each item's
  owner (the extension's `name`). Then each surface subscribes to `Changed` and rebuilds from the snapshot.

### Batch D1b — model: sonnet — T081–T083, T173 — 5 units

- [x] T081 [US4] Failure isolation (FR-034, FR-035, research R27; 2 units; load `avalonia-xaml` and `avalonia-styling`).
  Test first: failure fixtures in `tests/Fixtures/Extensions/` (a throwing `Register`, handler, provider and view
  factory; a duplicate command id; a gesture conflict; an unknown referenced id): each leaves the editor usable and
  names the extension (US4 scenarios 3–5); NPX005, NPX004, NPX009 and NPX010 appear in Errors and the log; NPX011 is
  logged once per provider and session; a throwing view shows `ContributionErrorView` naming the extension while the
  rest of the surface renders (edge case 1). Then one boundary guard in `src/NetPrints.Editor/Contributions/` used by
  every call into extension code, and `ContributionErrorView` (resource text, theme tokens).
- [x] T082 [US4] Per-user extensions in the groups (FR-032, Clarifications answer 2, NPX012; 1 unit). Test first:
  extensions in the configured directories (`netprints.extensionPaths`, `NETPRINTS_EXTENSION_PATH`) load once at start,
  with app-level kinds in the app group and project-level kinds added to each session group; a project that declares
  the same id serves its session and NPX012 names both versions; a saved layout naming a panel of an extension that is
  not loaded drops it, and the panel opens at its default place once the extension loads again (edge case 2). Then the
  loading in `src/NetPrints.Editor/Hosting/`.
- [x] T083 [US4] Every kind on its surface, none after unload (SC-004; 1 unit). Extend `tests/NetPrints.TestExtension/`
  to contribute one item of every public kind so far (command, panel, tile, template, context-menu item, tooltip
  provider, go-to provider, status-bar item, settings page, dialog, icon, node search provider, document migration). A
  headless test opens the fixture project that declares it, finds each item on its surface, unloads the project and
  finds none (US4 independent test, SC-004).
- [x] T173 [US4] View resources of an extension, by URI (FR-032, FR-033; 1 unit; load `avalonia-xaml` and
  `avalonia-styling`). Added by Review C1+C2 (CD-19): `ViewResourceLoader` throws `NotSupportedException` for a URI that
  is not a compiled built-in resource, "until D1", and no task owned the replacement. Test first:
  `tests/NetPrints.Editor.Tests/Hosting/ViewResourceLoaderTests.cs`: a fixture extension declares a view resource
  (`DataTemplates`, `ResourceDictionary` or `Styles`) by the URI of its own assembly; it loads when the extension's
  group is added and is removed with the group, and a URI that resolves to nothing, or to another kind of document, is
  reported and ignored instead of throwing. Then the loader resolves the URI against the extension's assembly through the
  group lifecycle of T079.

### Batch D1c — model: sonnet — T084–T085, T175 — 4 units

- [x] T084 [US4] `--profile` (FR-036, contracts/cli.md §4; 2 units). Test first: headless start-page tests (a known id
  preselects the profile in New project and filters the template list, with "Show all"; an unknown id leaves it
  unfiltered with a message naming the id and the known ids; with a project path the project opens and an info message
  says the profile was ignored) and the E2E `tests/NetPrints.Desktop.E2ETests/Scenarios/StartWithProfileTests.cs`. Then
  the argument in `src/NetPrints.Desktop/Program.cs`, the start page filter and the messages from resources. The profile
  is not remembered.
- [x] T085 [US11] The P3a open items (FR-088; 1 unit). Test first: the startup warnings test asserts no Dock `IsPinned`
  binding warning and no capability "Value is null" warning; a headless test hovers the exact middle of a cable at the
  fit zoom and gets its tooltip. Then the fixes. Check the Dock releases for wieslawsoltes/Dock#1191: if one has
  shipped, bump Dock from 12.1.0.6 in `Directory.Packages.props` and remove the workaround that fills empty capability
  objects, with the warnings test still green; otherwise record "not shipped" with the date. Do not file the upstream
  `IsPinned` issue (it waits for the owner's OK). Record the `ShellMainFlow` status.
- [x] T175 [US4] The extension's own services in panel and start-page contexts (FR-021, FR-034, ADR-0020; 1 unit).
  Added by Review C1+C2 (CD-19): `ShellHost` gives every panel and start-page factory `EmptyServiceProvider.Instance`,
  while ADR-0020 has a factory resolve its view model from the extension's own provider. Test first:
  `tests/NetPrints.Editor.Tests/Hosting/FactoryServiceProviderTests.cs`: a fixture extension registers a service with
  `IExtensionBuilder.Services`; its panel factory and its start-page factory resolve it from their context, and a
  factory of another extension does not see it. Then the panel and start-page contexts carry the contributing
  extension's provider (the built-ins get the editor's).

### Batch D1d — model: sonnet — T086–T088 — 4 units

- [x] T086 [US5] The settings store (FR-043, SC-006, contracts/settings-and-keybindings.md §7; 1 unit). Test first:
  `tests/NetPrints.Core.Tests/Extensibility/JsonFileSettingsStoreTests.cs`: unknown sections and keys round-trip
  byte-identically (US5 scenario 6); invalid JSON starts with defaults, says so once and copies the file to
  `settings.json.unreadable-<yyyyMMddHHmmss>` once before the next write (edge case); a newer `schemaVersion` is
  preserved with one warning; a failed write reverts the in-memory value; a section change reaches its
  `IOptionsMonitor<T>`. Then `src/NetPrints.Extensibility/Settings/JsonFileSettingsStore.cs` and the monitor bridge.
- [x] T087 [US5] The settings schema (FR-040, research R6, S10; 2 units). Test first: a golden test of
  `schemas/settings.v1.schema.json` that fails when a built-in section changes without the file; the DataAnnotations
  ranges and each `x-netprints-*` keyword appear in it; the existing `jsonschema` CI job validates it; a staleness test
  for the generated settings table in `docs/guide/editor/settings.md`. Then the exporter
  (`JsonSchemaExporter.GetJsonSchemaAsNode` with `TransformSchemaNode`), the canonical file and the table generator.
- [x] T088 [US5] The new built-in sections and NPX014 (FR-006, FR-044, contracts/settings-and-keybindings.md §5; 1
  unit). Test first: the validators reject out-of-range values of `run.stopTimeoutSeconds` (1–600) and
  `run.linuxTerminal` and unknown preset ids in `darkPreset` and `lightPreset` with a message naming the key; an
  extension section that fails validation uses its defaults and shows NPX014. Then the `run` section, the preset keys
  in `extensions.netprints.editor` (strings with a choices provider), and NPX014.

### Batch D1e — model: sonnet — T089–T091 — 4 units

- [x] T089 [US5] The Settings document and generated pages (FR-039–FR-042, research R20; 2 units; load `avalonia-xaml`,
  `avalonia-behaviors` and `avalonia-styling`). Test first: headless
  `tests/NetPrints.Editor.UITests/Settings/SettingsDocumentTests.cs`: `openSettings` (Ctrl+,, File › Settings, the
  palette) opens one document `netprints.document.settings`, and reopening activates it (US5 scenario 1); the page
  order; every row of the schema-to-field table (data-model §3); search filters by label and description across pages
  and hides pages with no match (scenario 3); an invalid value shows the validator's message and is not saved, a valid
  one is saved at once (scenario 4); a restart field shows "Requires restart" and the bar with "Restart now" (scenario
  5). Then `src/NetPrints.Editor/Settings/` (document, generated page and field view models, views with theme tokens and
  resource strings).
- [x] T090 [US5] Built-in and extension pages (FR-039, FR-044, SC-006; 1 unit). Test first: every key of the settings
  schema is reachable from a page (SC-006); the `NetPrints.TestExtension` section gets a generated page under Extensions
  (US5 scenario 2); changed values survive a restart in an automated test (SC-006). Then the General, Run and
  Extensions pages (extension paths, trusted projects, the loaded extensions with version and source); Appearance's
  preset fields come in T096.
- [x] T091 [P] [US5] The settings guide (FR-116; 1 unit). Write `docs/guide/editor/settings.md`: the document, the pages,
  search, validation, restart, the generated table of T087, the unreadable-file behaviour and extension pages. Guide
  index.

### Batch D1f — model: sonnet — T092–T094 — 3 units

- [x] T092 [US6] Effective gestures and the `keyboard` section (FR-046, FR-048–FR-050, research R21; 1 unit). Test
  first: `tests/NetPrints.Editor.Tests/Settings/KeyBindingResolverTests.cs`: the effective set is (defaults − remove) ∪
  add; precedence is user override, then built-in default, then extension default in load order; an extension default
  that a user binding takes is marked "overridden" (edge case); a single-key or Shift-only gesture in Global is refused
  with the reason, function keys exempt (US6 scenario 5); overrides of commands that are not loaded are kept (scenario
  4); an unparseable gesture is reported and ignored (edge case); the set is computed once per registry snapshot. Then
  the resolver and the `keyboard` section; every surface that shows gestures reads the effective set.
- [x] T093 [US6] The Keyboard shortcuts page (FR-045–FR-047; 1 unit; load `avalonia-xaml`, `avalonia-behaviors` and
  `avalonia-styling`). Test first: headless page tests: one row per command with label, owner, scope, default and
  current gestures; filter by text and by a recorded gesture (US6 scenario 1); add, remove, reset one and reset all
  (scenario 6); a conflict names the other command first, accepting removes the gesture from it and cancelling changes
  nothing (scenario 3); "Not loaded" lists kept overrides. Then the page; `keyboardShortcuts` opens it, and the
  shortcuts sheet keeps its printable view with "Edit shortcuts".
- [x] T094 [US6] The `RebindShortcuts` E2E and the shortcuts guide (SC-006, SC-007, FR-116; 1 unit).
  `tests/NetPrints.Desktop.E2ETests/Scenarios/RebindShortcutsTests.cs`, keyboard only, with its own `XDG_CONFIG_HOME`:
  rebind a built-in command and an extension command, resolve a conflict, restart, and run both through their new
  gestures. Update `docs/guide/editor/commands-and-shortcuts.md`.

### Batch D1g — model: sonnet — T095–T098 — 4 units

- [x] T095 [US8] The token catalogue and the contrast math (FR-066, research R24; 1 unit; load `avalonia-styling`).
  Test first: every visual token the theme dictionaries and `EditorStyles.axaml` use is in the catalogue, including
  wire opacity and thickness per variant and the inspector tokens ADR-0036 needs; unit tests of the contrast math. Then
  `src/NetPrints.Editor/Theming/` (the catalogue and the contrast math move into product code; the existing theming
  UITests call it).
- [x] T096 [US8] Presets, the overlay and the Appearance page (FR-067–FR-069, SC-009; 2 units; load `avalonia-xaml` and
  `avalonia-styling`). Test first: each of the four presets passes every gated contrast pair (US8 scenario 2); the
  overlay test shows a Dark preset changes 0 Light token values (FR-067, SC-009); switching presets needs no restart
  (scenario 1); Classic Dark gives the `#000` canvas, triangle pins and the `#AA202020` card; High contrast uses Semi's
  high-contrast variants (Aquatic for Dark, Desert for Light) plus its tuned tokens (scenario 4); an unknown
  `darkPreset` falls back to the variant's default with a log entry and keeps the other appearance keys (scenario 3).
  Then the embedded `*.nptheme.json` presets in `src/NetPrints.Editor/Theming/Presets/`, the loader that B1 (T144) reuses,
  the overlay writer for `ThemeDictionaries[Dark|Light]`, and the Appearance page fields with the preview strip.
- [x] T097 [P] [US8] The theming guide (FR-066, FR-116; 1 unit). Write `docs/guide/editor/themes.md`: modes, presets,
  the contrast policy and the token list generated from the catalogue (a staleness test). Guide index.
- [x] T098 **Checkpoint D1**. Whole suite plus the E2E pass; CI green; if a UI shard passes 12 minutes, add a fifth
  shard here (research R28). Report the plan's gate D1: SC-004 (T083's run, the failure fixtures), SC-006 (T090, T092
  and T086 runs), SC-007 (`RebindShortcutsTests`), SC-009 (contrast and overlay tests); the longest CI job; the Dock
  check. Docs updated: the settings, themes and shortcuts guides, the diagnostics guide, the release notes.

### Batch D1-R — no batch (T099 merged into T111)

- [x] T099 Merged into T111: the review of sub-phase D1 runs with sub-phase D2's, at the D1+D2 checkpoint (batch D2-R).
  Its extra checks (constitution I and VII, ADR-0006, ADR-0007, ADR-0020, ADR-0033, ADR-0035, ADR-0036, spec US4–US6,
  US8 and FR-088, research R20, R21, R24, R27, contracts/settings-and-keybindings.md, contracts/cli.md §4, data-model
  §2–§3, §5, the avalonia skills) are listed in T111.

### Batch D1-F — no batch (T100 merged into T112)

- [x] T100 Merged into T112: the fixes for sub-phase D1's findings run in batch D2-F.

---

## Phase 6: The author kit and the Godot sample (sub-phase D2, User Stories 10 and 4, Priority P2)

**Goal**: authors test extensions with the conformance kit, create them from a template, check a package with
`netprints extension verify`, and reuse a CI workflow; a Godot sample extension built only from the packed packages
proves the whole path. Contracts: contracts/extension-api.md §10, contracts/analyzer-diagnostics.md §6,
contracts/cli.md §2–§3. **Independent test**: US10's independent test. **Gate (plan)**: SC-003, SC-011.

### Batch D2a — model: sonnet — T101–T102 — 4 units

- [x] T101 [US10] The kit packages (FR-076, research R25, contracts/extension-api.md §10; 2 units). Test first: a new
  project `tests/NetPrints.Extensibility.Testing.Tests` (run in the Core leg of the CI matrix; `CiWorkflowTests`
  updated): `ExtensionTest<T>` with a declarative `TestState` and `RunAsync` drives a minimal extension;
  `ExtensionHarness` loads a real and a folder-based extension. Then `src/NetPrints.Extensibility.Testing/`
  (`ExtensionTest<TExtension>`, `TestState`, `ExtensionHarness` moved from
  `tests/NetPrints.Testing/Extensions/ExtensionHarness.cs`, `InMemoryHostChannel`, `ConformanceSuite`,
  `ConformanceOptions`, `ConformanceReport`, `NpkDiagnosticCodes`) and `src/NetPrints.Extensibility.Testing.Xunit/`
  (`ConformanceTests<TExtension>`, one test per check, xunit.v3); both packable with API files, XML docs and READMEs.
- [x] T102 [US10] Checks NPK001–NPK008 (FR-076, FR-078, SC-011; 2 units). For each check: test first with its failing
  fixture `tests/Fixtures/Extensions/Conformance/Fx.Fail.<Check>/`, whose run fails with a message naming the code, the
  rule and the offending item (US10 scenario 2), and a passing run on a good fixture; then the check. Guide rows.

### Batch D2b — model: sonnet — T103–T105 — 4 units

- [x] T103 [US10] Checks NPK009–NPK013, NPK015 and NPK016 (FR-076–FR-078, SC-011; 2 units). Same pattern as T102.
  NPK016 scans the extension's `.axaml` sources with the repository's E2/E8 XAML rules, from
  `ConformanceOptions.SourceDirectory` or the folder holding `netprints-extension.json`. NPK014 (graph rules) lands
  with N2 in T138.
- [x] T104 [US10] `NetPrints.TestExtension` through the kit only (FR-078, US10 scenario 4; 1 unit). Replace its direct
  tests with a `ConformanceTests<TestExtension>` class and kit-based tests; amend ADR-0010 (conformance checks for
  service providers, graph rules and editor contributions; NPK013–NPK016, with NPK014 landing in T138).
- [x] T105 [P] [US10] The extension testing guide (FR-116; 1 unit). Write `docs/guide/extension-testing.md`: the kit,
  the xUnit adapter, and each NPK check with its rule and its fix. Guide index.

### Batch D2c — model: sonnet — T106–T107 — 4 units

- [ ] T106 [US10] The extension package format and the `netprints-extension` template (FR-079, FR-080, SC-011, research
  R25, R17; 2 units; T026's outcome applies: if the analyzers do not flow through Abstractions, the template references
  `NetPrints.Core` directly and the guide says why). Test first, run by the `packages` job: install the packed
  `NetPrints.Templates` into a custom hive (`--debug:custom-hive`); `dotnet new netprints-extension` with no edits
  builds, its kit tests pass, and `dotnet pack` produces a `.nupkg` of package type `NetPrintsExtension` holding the
  loader's folder under `extension/` (US10 scenario 1). Then `src/NetPrints.Templates/` (the template package project)
  with the `netprints-extension` template: the extension project with the packing set up inline, and its test project on
  `ConformanceTests<T>`.
- [ ] T107 [US10] `netprints extension verify` and ADR-0041 (FR-080, FR-116, contracts/cli.md §3; 2 units). Test first:
  `tests/NetPrints.Cli.Tests/Commands/ExtensionVerifyCommandTests.cs`: a passing fixture package exits 0; one fixture
  per failing check (NPK001, NPK002, NPK003, NPK017) exits 1 with its message, and the NPK017 fixture lists each missing
  member (US10 scenario 3); a missing or wrong path exits 2; the JSON output matches the contract. Then
  `src/NetPrints.Cli/Commands/ExtensionVerifyCommand.cs` (reads metadata with `System.Reflection.Metadata`, loads
  nothing; `--host`; `--format text|json`), its section in `docs/guide/cli.md`, and
  `docs/adr/0041-extension-packages-and-verify.md` (the package format, `verify`, the author workflow; P3c may change
  the format after its feed spike). ADR index.

### Batch D2d — model: sonnet — T108–T110 — 3 units

- [ ] T108 [US10] The reusable author workflow (FR-081, research R25; 1 unit). Test first: `CiWorkflowTests` checks that
  `.github/workflows/netprints-extension.yml` is a `workflow_call` with inputs for the extension folder and an optional
  packages artifact, and runs restore, build, kit tests, pack and `verify`. Then the workflow.
- [ ] T109 [US4] The Godot sample extension (FR-037, SC-003, research R26; 2 units). Create `extensions/NetPrints.Godot/`
  outside `NetPrints.slnx` (`src/`, `tests/`, its own `Directory.Build.props`, `Directory.Packages.props` and
  `NuGet.config` on `local-packages/`). It contributes the `godot` project profile; a Godot template whose
  `Properties/launchSettings.json` holds `Executable` profiles for the game and the Godot editor (`%GODOT4%`,
  `--path .`), labelled with the stale-assembly note for Release builds; a command; a settings section with its
  generated page; and a status-bar item. Its tests run `ConformanceTests<T>` and need no Godot installation. CI: a
  Godot extension job after `packages` calls `netprints-extension.yml` (record whether `CiWorkflowTests` makes it part
  of the "Build and test (Linux)" aggregate), and a smoke step shows the editor host loads the built extension from an
  extension directory with no extension diagnostic (SC-003; record how). Its own sample under `samples/` is P3c's.
- [ ] T110 **Checkpoint D2**. Whole suite plus the E2E pass; CI green, including the `packages` and Godot jobs. Report
  the plan's gate D2: SC-003 (the Godot extension built from the packed packages, loaded in the editor, every kit check
  passing; validation and API checks; XML docs) and SC-011 (the template with 0 edits; each check's failing fixture);
  quickstart §4 step 5 with the Godot extension; the longest CI job; the Dock check. Docs updated: the extension
  testing guide, `cli.md`, ADR-0041, ADR-0010.

### Batch D2-R — model: opus — T111 (checkpoint D1+D2 review)

- [ ] T111 Review sub-phases D1 and D2 (the D1+D2 checkpoint) against their decision corpus (constitution I, III, V and
  VII, ADR-0006, ADR-0007, ADR-0010, ADR-0015, ADR-0017, ADR-0020, ADR-0033, ADR-0035, ADR-0036, ADR-0037, ADR-0040,
  ADR-0041, spec US4–US6, US8, US10, FR-037 and FR-088, research R20, R21, R24–R28, contracts/settings-and-keybindings.md,
  contracts/extension-api.md §10, contracts/analyzer-diagnostics.md §6, contracts/cli.md §2–§4, data-model §2–§3, §5, the
  avalonia skills): the whole diff of batches D1a–D2d. Findings to the PR and to implementation-notes under "Review
  D1+D2". No code changes.

### Batch D2-F — model: sonnet — T112 (checkpoint D1+D2 fixes)

- [ ] T112 Fix every finding of T111 (test first for behaviour findings), reply on each thread, whole suite plus the
  E2E pass, push; "no findings" if there were none. Sub-phase E starts only after this batch is pushed and green on CI
  (the coordinator checks).

---

## Phase 7: Run and build (sub-phase E, User Story 7, Priority P2)

**Goal**: projects run through `Project` and `Executable` launch profiles, the same way in the editor and in
`netprints run`; a configuration selector, console input and an external terminal complete it. Contract:
contracts/run-profiles.md. **Independent test**: US7's independent test. **Gate (plan)**: SC-008.

### Batch E1 — model: sonnet — T113–T115 — 4 units

- [ ] T113 [US7] The launch settings reader (FR-051, FR-052, contracts/run-profiles.md §1, data-model §4; 1 unit). Test
  first: `tests/NetPrints.Core.Tests/Run/LaunchSettingsReaderTests.cs` over fixtures in `tests/Fixtures/RunProfiles/`:
  comments and trailing commas allowed; file order kept; `commandName` case-sensitive; names compared case-insensitively,
  with NPR002 for names that differ only by case; `Project` and `Executable` run, other kinds are listed disabled with
  the reason (NPR003, NPR011); NPR001 for invalid JSON; the default is the first runnable profile; `<name>.run.json` and
  `My Project/` are ignored. Then `src/NetPrints.Workspace/Run/` (the reader, the internal kinds table,
  `NprDiagnosticCodes`) and guide rows.
- [ ] T114 [US7] The splitter and the joiner (FR-054, SC-008; 1 unit). Test first: a round trip of at least 30 cases
  (quotes, backslashes before quotes, doubled quotes, spaces, empty arguments, trailing backslashes, Unicode) with the
  `CommandLineToArgvW` rules on every OS. Then `src/NetPrints.Workspace/Run/CommandLineSplitter.cs`.
- [ ] T115 [US7] The resolver (FR-054–FR-056, contracts/run-profiles.md §2; 2 units). Test first: one fixture per row
  of the resolution table and per code NPR004–NPR010: `%NAME%` from the environment, `$(Name)` from the captured
  properties and the `AddProjectProperty` names; the working directory rules; the executable rules (a separator, `PATH`
  with `.exe` and `.com` on Windows, a macOS `.app`); an unset variable stops before the build in a path field and
  warns in arguments and environment (US7 scenario 3); a `.cmd` or `.bat` target on Windows is refused with the reason
  (edge case). Then `src/NetPrints.Workspace/Run/RunProfileResolver.cs`, producing the resolved request.

### Batch E2 — model: sonnet — T116–T118 — 3 units

- [ ] T116 [US7] `ProcessStartRequest` additions (FR-051, FR-057, research R22; 1 unit). Test first: launcher tests:
  the argument list reaches the program exactly, with no shell; environment additions and removals; input redirection;
  output capture off for "Don't capture output" (the GUI program edge case); the external-terminal flag. Then
  `src/NetPrints.Core/Projects/ProcessStartRequest.cs`, the editor's `IProcessLauncher` implementation and
  `src/NetPrints.Cli/Infrastructure/ProgramRunner.cs`.
- [ ] T117 [US7] The MSBuild variable scrub (FR-057; 1 unit). Test first: a variable `MsBuildRegistration` set is
  removed from an `Executable` program's environment, and the same variable set by the user before NetPrints started
  is kept (edge case). Then `src/NetPrints.Workspace/MsBuildRegistration.cs` records the names it set and whether each
  existed before; the resolver removes only those that did not.
- [ ] T118 [US7] `netprints run` with profiles (FR-062, SC-008, contracts/run-profiles.md §4; 1 unit). Test first:
  `tests/NetPrints.Cli.Tests/Commands/RunCommandTests.cs`: the default profile; `-lp|--launch-profile` and
  `--no-launch-profile`; `-- <args>` replace the profile's arguments while its environment and working directory apply;
  exit 2 for NPR003, NPR004 and usage errors (US7 scenario 4), exit 1 for NPR001, NPR002, NPR005, NPR006 (path) and
  NPR008–NPR010; for every resolver fixture the CLI and the editor's run path build the identical request (program,
  argument list, working directory, environment) — SC-008. Then `src/NetPrints.Cli/Commands/RunCommand.cs` and its
  `--help` text.

### Batch E3 — model: sonnet — T119–T120 — 4 units

- [ ] T119 [US7] The run profile button and Run enablement (FR-053, FR-058, FR-061; 2 units; load `avalonia-xaml` and
  `avalonia-styling`). Test first: headless tests: the split button sits next to Run and Stop when the project has
  profiles or is an executable; profiles in file order, disabled ones with their reason, a check on the selected one,
  and "Edit run profiles…" (US7 scenario 1); the per-profile options "Run in external terminal" and "Don't capture
  output"; `selectRunProfile` and `editRunProfiles`; Run is enabled for a library with an `Executable` profile; the
  selection and the options are remembered per user and per project in the session state's `run` block, never in
  `launchSettings.json`; the first run of each `Executable` profile prints the resolved command line in Output
  (scenario 2); a file with NPR001 or NPR002 shows the error on the button and disables Run for profiles (edge case).
  Then `src/NetPrints.Editor/Run/` (view models; commands through the facet) and the `run` block in
  `src/NetPrints.Editor/State/SessionState.cs`.
- [ ] T120 [US7] Stop, the process tree and the unload prompt (FR-059; 2 units). Test first: the echo tool in
  `tests/Fixtures/RunProfiles/` set to ignore SIGTERM is killed with its child after `run.stopTimeoutSeconds`; the
  Windows branch asks the main window to close first (a unit test of the platform branch); unloading a project with a
  live `Executable` run asks Leave, Stop or Cancel (US7 scenario 5). Then `CloseMainWindow` on Windows, SIGTERM through a
  `libc` `kill` import on Linux and macOS, `Kill(entireProcessTree: true)` after the timeout, and the prompt in the
  unload path.

### Batch E4 — model: sonnet — T121–T122 — 3 units

- [ ] T121 [US7] Project settings › Run (FR-060, research R22; 2 units; load `avalonia-xaml` and `avalonia-styling`).
  Test first: edits go through `JsonNode` and keep unknown members and property order (a byte comparison); a file with
  comments is read-only with "Open file", as are kinds NetPrints cannot run; the argument preview shows the split list;
  the warning that `dotnet run` resolves a relative `workingDirectory` from `Properties/`. Then the Run section of
  `src/NetPrints.Editor/Shell/ProjectSettingsDocumentViewModel.cs` and its view.
- [ ] T122 [US7] The configuration selector (FR-063; 1 unit). Test first: Debug or Release next to the profile button;
  build and run use it; it is remembered per user and per project (US7 scenario 7); the CLI keeps `-c`. Then the
  selector.

### Batch E5 — model: sonnet — T123–T126 — 3 units

- [ ] T123 [US7] Console input and the external terminal (FR-064, research R23; 1 unit; **default pending owner**:
  "external terminal" — the Linux detection order behind `run.linuxTerminal`, the Windows console window closing when
  the program exits, and macOS through `open -a Terminal`). Test first: with output captured, Output shows an input
  line that writes a line to standard input and "Close input" sends end of file (US7 scenario 6); there is no input
  line without capture or in an external terminal; the generated POSIX `sh` script single-quotes every argument and
  exports the environment (run through a fake terminal command); the Windows start options are unit-tested. Then the
  input line in `src/NetPrints.Editor/Output/` and the terminal launcher, one path per OS behind one service. The
  Windows and macOS behaviour goes on the owner's manual list.
- [ ] T124 [US7] The `RunExecutableProfile` E2E (SC-008, FR-118; 1 unit).
  `tests/NetPrints.Desktop.E2ETests/Scenarios/RunExecutableProfileTests.cs`: a fixture `Executable` profile starts the
  echo tool, which prints its argv, working directory and one environment variable and reads a line from standard
  input; the test types a line in Output and sees it echoed.
- [ ] T125 [US4] The Godot profiles, the run profiles guide and the CLI reference (FR-065, US4 scenario 7, FR-116; 1
  unit). Test first: the Godot template's profiles (`extensions/NetPrints.Godot`, run in the Godot job) resolve with
  `GODOT4` set to a stub and fail with NPR005 when it is unset; a headless test opens a project created from a copy of
  the template's files in `tests/Fixtures/RunProfiles/`: it builds, and the run profile button lists the game and the
  Godot editor profiles (US4 scenario 7); a test over `src/` finds no Godot-specific code. Then
  `docs/guide/run-profiles.md` (the kinds, resolution, the NPR table, the per-profile options, the `workingDirectory`
  difference from `dotnet run`) and the `run` section of `docs/guide/cli.md`.
- [ ] T126 **Checkpoint E**. Whole suite plus the E2E pass; CI green, including `CLI (Windows)`. Report the plan's gate
  E: SC-008 (the round-trip case count, the resolver fixtures with identical editor and CLI requests,
  `RunExecutableProfileTests`); the owner's manual list (external terminals on Windows and macOS, Stop on Windows); the
  longest CI job; the Dock check. Docs updated: the run profiles guide, `cli.md`, the diagnostics guide, the release
  notes.

### Batch E-R — no batch (T127 merged into T153)

- [x] T127 Merged into T153 (by way of T134): the review of sub-phase E runs with sub-phases F and G, at the E+F+G
  checkpoint (batch G-R). Its extra checks (constitution I and VII, ADR-0033, ADR-0037, spec US7 and FR-051–FR-065,
  research R22, R23, contracts/run-profiles.md, data-model §4; the argument handling for shell re-parsing and
  injection, the environment scrub, and the process-tree kill) are listed in T153.

### Batch E-F — no batch (T128 merged into T154)

- [x] T128 Merged into T154 (by way of T135): the fixes for sub-phase E's findings run in batch G-F.

---

## Phase 8: The command line and the C# target in the editor (sub-phase F, User Stories 15 and 9, Priority P2–P3)

**Goal**: `netprints new` and `dotnet new` create the same projects as the editor; node search hides what the
project's C# version cannot use and offers fixes. Contract: contracts/cli.md §1–§2. **Independent test**: US15's
independent test; US9 scenarios 3–4. **Gate (plan)**: the US15 tests and SC-010 (editor); the ADR-0035 amendment.

### Batch F1 — model: sonnet — T129–T130 — 3 units

- [ ] T129 [US15] `netprints new` (FR-107, FR-116, contracts/cli.md §1; 1 unit). Test first:
  `tests/NetPrints.Cli.Tests/Commands/NewCommandTests.cs`: `--list` as text and JSON (built-ins first, then the
  templates of per-user extensions, with their profile — US15 scenario 1); creating `console` and `library` and
  building each (scenario 2); an unknown template exits 2; a non-empty folder exits 1 without `--force`; an `Fx`
  extension template from a user extension directory is offered. Then `src/NetPrints.Cli/Commands/NewCommand.cs` on
  the Workspace template trees and its section in `docs/guide/cli.md`.
- [ ] T130 [US15] The `netprints-console` and `netprints-library` templates and the parity test (FR-108; 2 units). Test
  first: `tests/NetPrints.Cli.Tests/Templates/TemplateParityTests.cs`: `dotnet new` from the packed
  `NetPrints.Templates` in a custom hive, `netprints new` and the editor's New project produce byte-identical trees for
  Console and Library (US15 independent test). Then the template package copies the Workspace trees and adds
  `.template.config/template.json` with `sourceName` `NetPrintsProject` and the `RootNamespace` and `ProfileId`
  symbols.

### Batch F2 — model: sonnet — T131–T133, T172 — 4 units

- [ ] T131 [US9] Availability in node search (FR-073; 1 unit; load `avalonia-xaml` and `avalonia-styling`). Test
  first: in the `10.0` fixture, a node that needs C# 11 is hidden, the list says how many results the project's C#
  version hides, and "Show unavailable" shows them disabled with the reason; short lists show unavailable items
  disabled with the reason and the quick fixes (US9 scenario 3). Then the filter in the node search view model, from
  `Requires` and `CSharpTarget.Check`.
- [ ] T132 [US9] Quick fixes (FR-074; 1 unit). Test first: "Set LangVersion" raises `<LangVersion>` in the project file
  at most to the framework's maximum (US9 scenario 4), edits the project body even when an imported props file sets
  it, and says so; a PolySharp hint appears when only types are missing; a missing type is a warning, not an error.
  Then the fixes.
- [ ] T172 [US9] Common capabilities of a multi-targeted project (FR-071; 1 unit). Added by Review C1+C2 (CD-19):
  `CSharpTarget` takes the minimum version of the targets, and implementation-notes C1c defers the capability half to
  here. Test first: `tests/NetPrints.Core.Tests/Projects/CSharpTargetTests.cs` gains a project that targets `net10.0`
  and `net8.0`: the version is the minimum of the two, and a type or feature that only one target supplies is
  unavailable (`Check` reports it) while one both supply stays available (US9 scenario 2), with the node search of T131
  hiding what the common set lacks. Then the intersection of the targets' capabilities in `CSharpTarget`.
- [ ] T133 **Checkpoint F** (plan "Gate outcomes"; 1 unit). Whole suite plus the E2E pass; CI green. Report the plan's
  gate F: the US15 tests (`NewCommandTests`, `TemplateParityTests`) and SC-010 for the editor path
  (`CSharpTargetWriterIdentityTests`). There is no scope rule: the owner kept the tail and the NuGet packages in P3 on
  2026-10-16. Write the ADR-0035 amendment: B1 (user theme files) and C (the themes of loaded extensions) land in P3's
  sub-phase G, and distributing extension themes through the extension manager is P3c's. Docs updated: `cli.md`, the
  projects guide, ADR-0035.

### Batch F-R — no batch (T134 merged into T153)

- [x] T134 Merged into T153: the review of sub-phases E and F runs with sub-phase G's, at the E+F+G checkpoint (batch
  G-R). Its checks (constitution I and VII, ADR-0015, ADR-0033, ADR-0035, ADR-0037, ADR-0038, spec US7, US9 and US15,
  FR-051–FR-065, FR-073, FR-074, FR-107, FR-108, research R17, R19, R22, R23, R31, contracts/run-profiles.md,
  contracts/cli.md §1–§2, data-model §4) are listed in T153.

### Batch F-F — no batch (T135 merged into T154)

- [x] T135 Merged into T154: the fixes for sub-phases E's and F's findings run in batch G-F.

---

## Phase 9: The tail (sub-phase G, User Stories 16 and 17, Priority P4)

**Goal**: extensions contribute inspector sections, value editors and graph rules; node translators add helper members;
the built-in fixed node kinds are declared by convention; users duplicate, edit and reload theme files, and loaded
extensions offer their themes. Restored from `51e0f06d` in batch P3-S2e (owner, 2026-10-16) with no scope rule; the
model-driven grid stays in P3b. Contracts: contracts/extension-api.md §2, §6, contracts/analyzer-diagnostics.md §3, §6.
**Independent test**: US16 and US17 independent tests; quickstart §13. **Gate (plan)**: the US16 and US17 tests;
SC-002 rechecked; SC-001 for NPA030–NPA033; NPK014's failing fixture.

### Batch G1 — model: sonnet — T136–T137 — 4 units

- [ ] T136 [US16] The value-editor contract and inspector sections (FR-109, FR-021, ADR-0036, research R14, R16; 2
  units; load `avalonia-xaml` and `avalonia-styling`). Test first: the built-in inspectors and the pin and default
  editors register through the contract; an extension inspector section for variables appears below the built-in ones
  and edits the model with undo (US16 scenario 1). Then `AddInspectorSection` (NPXE0014) on the facet and the
  value-editor contract shared by pins, variable and parameter defaults and grid rows; the built-ins register the same
  way. The model-driven grid stays in P3b (FR-112).
- [ ] T137 [US16] Value editors (FR-109, research R16; 2 units; load `avalonia-xaml` and `avalonia-styling`). Test
  first: an extension value editor for its type edits an unconnected pin and a default of that type, and without the
  extension the generic editor does (US16 scenario 2); `NetPrints.TestExtension` contributes one, and NPK015 covers it.
  Then `AddValueEditor` (NPXE0014).

### Batch G2 — model: sonnet — T138–T140 — 4 units

- [ ] T138 [US16] Graph rules and NPK014 (FR-110, FR-077, FR-078, SC-011, research R16; 2 units). Test first: an `Fx`
  graph rule's diagnostic appears in Errors and in `netprints build` and `generate` with the same code (US16 scenario
  3); NPK014 (deterministic, graph unchanged, cancellation honoured, isolated when throwing) with its failing fixture
  `Fx.Fail.GraphRules`. Then `AddGraphRule` (NPXE0015) on the builder and the rule runner shared by the editor and the
  CLI; the extension testing guide and the diagnostics guide gain NPK014 (the guide's staleness test needs the row in
  the same commit).
- [ ] T139 [P] [US16] ADR-0032 (FR-110, FR-116; 1 unit). Write `docs/adr/0032-graph-rules.md` and the graph rules
  section of `docs/guide/extensions.md`. ADR index.
- [ ] T140 [US16] Translation context additions (FR-111, N5, research R16; 1 unit). Test first: a node translator adds
  a using and a helper member; the output is deterministic and each addition is written once. Then the context API
  (NPXE0016).

### Batch G3 — model: sonnet — T141–T143 — 5 units

- [ ] T141 [US16] Node kinds by convention (FR-112, S7, SC-001, contracts/analyzer-diagnostics.md §3; 2 units). Test
  first: generator snapshots (the inferred document, the translator and `On<Pin>` dispatch, the generated
  registration), compile and incrementality tests, and NPA030–NPA033 cases. Then the generator in
  `src/NetPrints.Authoring/NodeKinds/` (NPXE0017) and guide rows.
- [ ] T142 [US16] Built-in fixed kinds by convention and SUG5 (FR-112, SC-002; 2 units). Red/green: the goldens, the S0
  golden and the search goldens stay unchanged. Convert the built-in fixed node kinds; their search entries become
  generated metadata (SUG5).
- [ ] T143 [P] [US16] ADR-0026 (FR-112, FR-116; 1 unit). Write `docs/adr/0026-node-kinds-by-convention.md`, including
  the amendment content the plan names (`PrepareFromSearchAsync`, offers from typed pins). ADR index.

### Batch G4 — model: sonnet — T144–T146, T174, T170 — 5 units

- [ ] T144 [US17] User theme files (FR-113, FR-114, research R24 B1; 2 units; load `avalonia-xaml` and
  `avalonia-styling`). Test first:
  "Duplicate theme" writes `<config>/NetPrints/themes/<id>.nptheme.json` with the active preset's values and selects
  it (US17 scenario 1); "Reload themes" applies edits; an unknown token is ignored with a warning, a bad value keeps the
  base value with a warning, a newer schema is skipped with a message, and a contrast failure applies with a warning
  naming the pairs (scenario 2); the size cap; no URLs, fonts or images; a Semi key is checked against the resources
  loaded at runtime (unknown: warning) and its value parsed by the type of the resource it replaces, with no list of
  Semi keys in NetPrints. Then the commands and the validation on T096's loader.
- [ ] T145 [US17] Extension themes (FR-115, FR-029, research R24 C; 1 unit; load `avalonia-xaml` and
  `avalonia-styling` before touching the Appearance page view). Test first: a loaded per-user `Fx`
  extension whose manifest lists themes (T069's field) offers them on the Appearance page under its name, with the same
  validation (US17 scenario 3); a theme never runs code or loads XAML. Then the wiring.
- [ ] T146 [P] [US17] The theming guide for user and extension themes (FR-114, FR-116; 1 unit). Extend
  `docs/guide/editor/themes.md`: the file format, the commands, the warnings, and a link to Semi's own documentation.
- [ ] T174 VSTHRD002 and VSTHRD103 at error in `tests/` (FR-031, ADR-0003; 1 unit). Added by Review C1+C2 (CD-19): T074
  planned it and ADR-0003 says "a follow-up batch fixes first". Red/green: set both rules to `error` for `tests/` in
  `.editorconfig` (the build fails on every blocking wait and every sync call that has an async twin), then fix each
  site for real (async tests, `await` instead of `.Result`/`.Wait()`); no suppression and no ledger entry. Record the
  count in the ADR-0003 amendment and in the AGENTS.md async rules.
- [ ] T170 **Checkpoint G** (plan "Gate outcomes"). Whole suite plus the E2E pass; CI green. Report the plan's gate G:
  the US16 tests (the inspector section, value editor and graph rule tests of T136–T138) and the US17 tests (T144,
  T145); SC-002 rechecked after T142 (goldens, S0 golden and search goldens unchanged); SC-001 for NPA030–NPA033;
  NPK014's failing fixture (SC-011); quickstart §13; the longest CI job; the Dock check. Docs updated: the extensions
  guide, the extension testing guide, the themes guide, ADR-0026, ADR-0032.

### Batch G-R — model: opus — T153 (checkpoint E+F+G review)

- [ ] T153 Review sub-phases E, F and G (the E+F+G checkpoint) against their decision corpus (constitution I, V and
  VII, ADR-0015, ADR-0026, ADR-0032, ADR-0033, ADR-0035, ADR-0036, ADR-0037, ADR-0038, spec US7, US9, US15, US16 and
  US17, FR-051–FR-065, FR-073, FR-074, FR-107–FR-115, research R14, R16, R17, R19, R22–R24, R31,
  contracts/run-profiles.md, contracts/cli.md §1–§2, contracts/extension-api.md §2, §6, contracts/analyzer-diagnostics.md
  §3, §6, data-model §2, §4, §5): the whole diff of batches E1–G4. Check the argument handling for shell re-parsing and
  injection, the environment scrub and the process-tree kill; every new NPXE surface; the goldens around T142; that a
  theme never runs code or loads XAML. Findings to the PR and to implementation-notes under "Review E+F+G". No code
  changes.

### Batch G-F — model: sonnet — T154 (checkpoint E+F+G fixes)

- [ ] T154 Fix every finding of T153 (test first for behaviour findings), reply on each thread, whole suite plus the
  E2E pass, push; "no findings" if there were none. Sub-phase H starts only after this batch is pushed and green on CI
  (the coordinator checks).

---

## Phase 10: NuGet packages in the References dialog (sub-phase H, User Story 12, Priority P3)

**Goal**: a developer browses, adds, updates and removes NuGet packages from a Packages tab, with the sources and
credentials `dotnet restore` uses; a failed or cancelled change leaves the files byte-identical; the package service is
ready for P3c to reuse. New in batch P3-S2e (owner, 2026-10-16). Contract: contracts/nuget-references.md; data-model
§12; research R32–R35; ADR-0042. **Independent test**: US12's independent test; quickstart §14. **Gate (plan)**:
SC-012; US12 scenarios 1–5; no secret in NetPrints state or logs; the trim ratchet not up.

### Batch H1 — model: sonnet — T158–T160 — 4 units

- [ ] T158 [US12] The NuGet spike (FR-089, FR-117, research R32; 1 unit; gate). On a scratch branch that is never
  pushed, reference `NuGet.Protocol`, `NuGet.Configuration`, `NuGet.Credentials`, `NuGet.Versioning`,
  `NuGet.Frameworks` and `NuGet.ProjectModel` from a probe library referenced by the editor, at the version the SDK
  pinned by `global.json` ships (`dotnet nuget --version`). Check, and record under "NuGet spike" in
  implementation-notes: the version; that `DefaultCredentialServiceUtility` in non-interactive mode runs in-process in
  the editor (with no credential plugin installed, and with the Azure Artifacts credential provider if it is present);
  that a local folder feed answers search, versions and metadata; the trim ratchet count (`eng/trim-warnings.sh`) with
  the probe referenced; and, for P3c, whether a local folder feed honours the `packageType` filter. If the ratchet
  rises, or the plugins cannot run non-interactively, apply the plan's gate outcome and report it. Only the notes and
  the `Directory.Packages.props` pins reach the PR, with T159.
- [ ] T159 [US12] `NetPrints.Packages`: sources and credentials (FR-089, research R32, contracts/nuget-references.md
  §1; 2 units). Test first: `tests/NetPrints.Editor.Tests/Packages/PackageSourcesTests.cs`: a fixture `nuget.config`
  hierarchy (the project folder and its parent, with the user and machine files kept out through the service's
  settings seam) gives the enabled sources in file order; a `packageSourceCredentials` entry never appears in a captured log line or in any file
  NetPrints writes; logged URLs lose user info, query and fragment; a source needing credentials with no provider
  reports NPW012 with the `dotnet restore --interactive` hint. Then `src/NetPrints.Packages/` (in `NetPrints.slnx`,
  not packable), `IPackageService` with `Sources`, the `[Singleton<T>]` registration, the NPW010–NPW013 constants with
  their rows in `docs/guide/diagnostics.md` (same commit, for the guide's staleness test), the `[LoggerMessage]` log
  class, the `BannedSymbols.txt` entry for `Newtonsoft.Json`, and the pins from T158.
- [ ] T160 [US12] ADR-0042 (FR-116, research R32–R35; 1 unit; after T158). Write
  `docs/adr/0042-nuget-package-service-and-package-edits.md`: the NuGet client libraries in `NetPrints.Packages` with
  `Newtonsoft.Json` transitive and banned in `src/`; sources and credentials from `nuget.config` and the credential
  plugins only, no secrets in NetPrints state, redacted URLs; CPM-aware edits through the MSBuild construction model;
  the byte-identical rollback; the removal impact; reuse by P3c; the spike's outcome. ADR index.

### Batch H2 — model: sonnet — T161–T163 — 5 units

- [ ] T161 [US12] Search, versions, metadata, cache and offline (FR-089, FR-093, research R32, contract §1; 2 units).
  Test first: `tests/NetPrints.Editor.Tests/Packages/PackageSearchTests.cs`: against the local folder feed packed once
  per run from `tests/Fixtures/Packages/Fx.Packages.Sample/` (1.0.0, 1.1.0, 2.0.0-beta), search and versions honour
  the prerelease switch (US12 scenario 1); compatibility is computed against every target framework of the project;
  deprecation and vulnerabilities map from NuGet's own metadata types; a second call within 5 minutes hits the cache
  and a later one does not (fake `TimeProvider`), and Refresh bypasses it; an unreachable source gives NPW011 and
  offline results from a fixture global packages folder (scenario 5). Then the operations on `IPackageService`.
- [ ] T162 [US12] Installed packages, transitive packages and updates (FR-089, FR-093, research R32, R35; 2 units).
  Test first: from a fixture `obj/project.assets.json`, direct references list with declared and resolved versions,
  transitive ones read-only; a `$(…)` version and several conditioned items for one id are read-only with NPW013 and
  the reason; Updates lists direct references with a newer version in the selected sources; no network is needed for
  Installed (scenario 5). Then `Installed` and the update query.
- [ ] T163 [US12] The removal impact (FR-092, research R34, contract §5; 1 unit). Test first: a fixture graph whose node
  calls a method of `Fx.Packages.Sample` is listed (graph, node, member) for a removal (US12 scenario 4); a package
  whose assemblies another direct reference also brings lists nothing. Then the impact query over the assets file's
  dependency graph and `IMemberReferencingNode`.

### Batch H3 — model: sonnet — T164–T165 — 5 units

- [ ] T164 [US12] Package reference edits (FR-090, SC-012, research R33, contract §2; 3 units, design-heavy). Test
  first: `tests/NetPrints.Editor.Tests/Hosting/PackageEditTests.cs`: each row of contract §2 on the plain, CPM and
  `VersionOverride` fixtures of `tests/Fixtures/Packages/`, with comments and order preserved (US12 scenario 2); the
  plain and CPM add, update and remove compared byte for byte with `dotnet package add --no-restore` and `dotnet
  package remove` run on a second copy (SC-012, first half); the `VersionOverride` golden; a range kept as written; a
  property version and several conditioned items refused with NPW013; `NetPrints.Sdk` updated but not removed; an
  invalid id or version refused before any write. Then `AddPackageReference`, `SetPackageVersion` and
  `RemovePackageReference` in `NetPrints.Core/Projects/ProjectEdit.cs` under NPXE0018 (the public shape is frozen
  after T073, research R16), their `PublicAPI` entries and the extension guide's "API stability" row, and their
  application in `MsBuildProjectSystem` (both files under CPM).
- [ ] T165 [US12] Apply, restore and roll back (FR-091, SC-012, research R34, contract §3; 2 units). Test first:
  `tests/NetPrints.Editor.Tests/Packages/PackageChangeTests.cs`: a successful add refreshes the reflection provider and
  re-validates the open graphs; a forced restore failure (a version the local feed lacks) and a cancellation during
  the restore each leave the project and props files byte-identical, report NPW010, and keep the session's snapshot
  and reflection provider (US12 scenario 3, the spec's edge case, SC-012 second half); a second change while one runs
  is refused. Then the change runner in `NetPrints.Packages` on `IProjectSystem` and `ProjectFiles.WriteAtomicAsync`.

### Batch H4 — model: sonnet — T166–T169 — 4 units

- [ ] T166 [US12] The Packages tab (FR-093, FR-092, research R35, contract §4; 2 units; load `avalonia-xaml`,
  `avalonia-styling` and `avalonia-behaviors`). Test first: view-model tests in
  `tests/NetPrints.Editor.Tests/Packages/PackagesTabViewModelTests.cs` on a fake `IPackageService` (the three sub-tabs,
  the source filter, prerelease, the compatibility filter with "Show incompatible", the badges, the offline banner,
  read-only rows with their reason, "Use VersionOverride" only under CPM, the remove confirmation listing the impact —
  US12 scenarios 1, 4, 5), and a headless UI test of the tab's controls and automation ids
  (`References.Packages.*`) through a page object in `NetPrints.Testing.Ui`. Then the view models, the tab in the
  References dialog, and its text in resources.
- [ ] T167 [US12] The `ManagePackages` E2E (SC-012, FR-118; 1 unit). One class (ADR-0006) on a copy of the CPM fixture
  with a temporary local folder feed: add `Fx.Packages.Sample` 1.0.0, update to 1.1.0, remove it with the impact list
  shown, and a forced restore failure that leaves both files byte-identical. `NETPRINTS_STATE_DIR` per lease.
- [ ] T168 [P] [US12] The NuGet packages guide (FR-116; 1 unit). Write `docs/guide/editor/packages.md`: sources and
  credentials (what NetPrints reads and never stores), the three sub-tabs, Central Package Management and
  `VersionOverride`, read-only references, rollback, removal, offline; links to the NPW010–NPW013 rows T159 added to
  the diagnostics guide. Guide index.
- [ ] T169 **Checkpoint H** (plan "Gate outcomes"). Whole suite plus the E2E pass; CI green. Report the plan's gate H:
  SC-012 (`PackageEditTests`, `PackageChangeTests`, `ManagePackagesTests`); US12 scenarios 1–5 with their test names;
  no secret in NetPrints state or logs (`PackageSourcesTests`); the trim count at most its value at the start of the
  phase; quickstart §14 on a local folder feed; the longest CI job; the Dock check. Docs updated: the packages guide,
  the diagnostics guide, ADR-0042, the release notes.

---

## Phase 11: Final review and merge preparation (the fifth checkpoint)

### Batch PR-P — model: sonnet — T147–T152 (release preparation, before the final review; 5 units)

Moved out of the merge batch so that the final review (T155) sees this work. The IDs are the old sub-phase G ones; the
batch runs after sub-phase H (Checkpoint H, T169).

- [ ] T147 Guide images (FR-088, FR-116; 1 unit). Regenerate the guide images that carry the build version with
  `scripts/guide-screenshots.sh` on your own Xvfb display; inspect each changed PNG with the Read tool.
- [ ] T148 The 0.4.0 release notes (FR-012, FR-116; 1 unit). Consolidate the "Unreleased" entries of
  `.github/release-notes.md` into the 0.4.0 section: the hard break (the pin renames and every `*REMOVED*` line), the
  new packages, the experimental ids, and the user-visible features. Check FR-116's list of guides and ADRs against
  `docs/`.
- [ ] T149 The quickstart run (1 unit). Run every section of `specs/006-editor-extension-host/quickstart.md` (§14 on a
  temporary local folder feed, no network) on your own display with an isolated `NETPRINTS_STATE_DIR` and
  `XDG_CONFIG_HOME`; record each step's result in implementation-notes; fix a deviation here (test first).
- [ ] T150 SC-016, the ratchets and SC-001 (FR-117, FR-118; 1 unit). From the last CI run: every job's duration (none
  over 15 minutes; a fifth UI shard if a shard passes 12 minutes); the trim count at most its value at the start of the
  phase; the XAML literal ratchet not grown; 0 NPS, NPA and NPH hits in `src/`, and a test that every analyzer
  descriptor NetPrints ships has at least one passing and one failing test (SC-001).
- [ ] T151 The docs site (SC-017, FR-116; 1 unit). `scripts/build-docs.sh` builds with 0 broken links; every guide of
  FR-116 and every ADR of the plan's schedule is present; `docs/adr/README.md` lists them all.
- [ ] T152 The SC table: SC-001 to SC-017 with evidence for each (SC-013 and SC-014 marked "moved to P3c"); the
  quickstart results; the owner's manual list (Windows and macOS editor behaviour, two-DPI check, Cmd mapping, external
  terminals, an authenticated private feed in the Packages tab); the Dock check. Docs updated: the release notes, the
  guide images, the themes guide, the extensions guide, the packages guide.

### Batch PR-R — model: opus — T155 (final review, checkpoint 5)

- [ ] T155 The final review (the fifth checkpoint) by a reviewer who did not implement the work, in two parts.
  1. **Sub-phase H in full** (its own review folds into this one): the whole diff of batches H1–H4 against its decision
     corpus (constitution I, V and VII, AGENTS.md, ADR-0003, ADR-0015, ADR-0031, ADR-0033, ADR-0042, spec US12,
     FR-089–FR-093, SC-012, research R32–R35, contracts/nuget-references.md, contracts/analyzer-diagnostics.md §8,
     data-model §12). Extra weight: no credential or secret in NetPrints state, settings or logs; every logged URL
     redacted; the rollback byte-identical on every failure path, cancellation included; no `Newtonsoft.Json` use in
     `src/`; the `dotnet package add` comparisons; the trim count.
  2. **The release preparation (PR-P) and the whole PR, lightly**: integration across sub-phases, not each sub-phase
     again: the analyzers ↔ the sweep ↔ the hygiene analyzers that replaced tests; the composition gates ↔ the perf
     numbers; the typed pins ↔ the renames ↔ the S0 golden ↔ `NodePins` files ↔ the S7 conversion ↔ the release notes;
     the Abstractions split ↔ the facet ↔ the Null Object in the CLI; the published packages ↔ validation ↔ the kit ↔
     the template ↔ `verify` ↔ the Godot sample; groups ↔ every surface ↔ failure isolation; settings ↔ the schema ↔ the
     pages ↔ key bindings ↔ presets ↔ user and extension themes; inspector sections ↔ value editors ↔ graph rules ↔
     NPK014; run profiles in the editor ↔ the CLI; the package service ↔ the project edits ↔ the reflection refresh;
     docs ↔ behaviour; and the SC table (SC-001 to SC-015 from the sub-phase gate reports; SC-016 and SC-017 from PR-P,
     T150 and T151).
  Findings to the PR and to implementation-notes under "Final review" (H's under "Review H").

### Batch PR-F — model: sonnet — T156 (reserved: fix final review findings)

- [ ] T156 Fix every finding of T155 (test first for behaviour findings), reply on each thread, whole suite plus the
  E2E pass, push; "no findings" if there were none.

### Batch PR-M — model: sonnet — T157 (merge preparation; 1 unit)

- [ ] T157 Merge preparation (the release preparation, old T147–T152, is batch PR-P before the final review).
  1. Merge `origin/master` into `006-editor-extension-host` (a merge commit, no rebase; resolve conflicts keeping both
     sides' intent), whole suite plus the E2E pass, and push; do not wait for CI. Update the PR description (summary,
     decisions, deferrals, the owner's manual list, a link to the SC table). The coordinator watches `CI` and `CLI
     (Windows)` and marks PR #20 ready (`gh pr ready 20 --repo danielmeza/netprints`) once both are green. The owner
     merges. The v0.4.0 release, tags and NuGet pushes need the owner's OK; the roadmap and constitution edits after the
     merge belong to the coordinator.

---

## Dependencies and execution order

- Batches run strictly in order: A1 → … → A5 → B1 → … → B7 → B-R → B-F → C1a → … → C1f → C2a → … → C2f → C2-R → C2-F
  → D1a → … → D1g → D2a → … → D2d → D2-R → D2-F → E1 → … → E5 → F1 → F2 → G1 → … → G4 → G-R → G-F → H1 → … → H4 → PR-P
  → PR-R → PR-F → PR-M (one agent at a time, AGENTS.md). A review batch reads the finished group of sub-phases; its fix
  batch closes the checkpoint. A-R, A-F, C1-R, C1-F, D1-R, D1-F, E-R, E-F, F-R and F-F do not exist (merged); H's
  review is part of PR-R.
- Every batch starts by checking CI at the previous head and fixing any red first (plan "Process: CI and checkpoints").
- Gates stop the chain: T007 (M5) decides T008 and T053–T055; T053's M1 and T055's M2 stop the migration at the failed
  step (FR-004); T026 (pack spike) decides T106's reference; T073 (publish gate) freezes the public shape for every
  later batch (G and H add surfaces only under NPXE0014–NPXE0018); T158 (NuGet spike) pins the libraries for T159–T166
  and applies the plan's gate outcome for credential plugins and the trim ratchet.
- Within A: the NPS rules (T004) and the sweep (T005) come before every other code change, so later batches follow the
  layout from the start. T012 (unknown members) lands before any new document field: the `out`/`ref` pins (T035) and
  `kindVersion` (T067). T008 (registration generator) and T011 (validators) feed the composition steps T053–T055.
- Within B: the search invariants (T019) come before SUG1 (T020); declared built-in pins (T029) before pin-key
  tracking (T030), which comes before the renames (T031, FR-011); the role interfaces and pin roles (T034, T035) after
  the renames (research R11).
- C1 and C2: the split (T040) comes before SUG4 (T051, research R31: R7 before SUG4) and the facet (T060); the typed
  contexts (T052) before the facet's descriptors (T060–T065); the C# target types (T047) before `Requires` (T051) and
  the availability filter (T131); the templates with files (T066) before the template package (T106, T130) and
  `netprints new` (T129); the settings page kind (T068) before the Settings document (T089); the manifest `themes`
  field (T069) before the extension themes (T145).
- D1: groups (T079) before surfaces (T080) and isolation (T081); the store and schema (T086–T088) before the pages
  (T089, T090); the resolver of gestures (T092) before the page (T093) and the E2E (T094); the catalogue (T095) before
  the presets (T096), whose loader B1 (T144) reuses.
- D2 needs C2's packages (T071) and D1's settings pages (the Godot sample's page). The kit (T101) comes before every
  check; `NetPrints.Templates` is created in T106 and gains the project templates in T130. The Godot sample (T109) ships
  the template whose profiles E tests (T125).
- E needs no D work except the Godot sample (T125). F's availability filter needs C1's target and SUG4.
- G: the value-editor contract (T136) before the value editors (T137); graph rules (T138) after D2's kit, which NPK014
  joins; the convention generator (T141) before the built-in conversion (T142), whose goldens stay unchanged; the user
  theme files (T144) before the extension themes (T145), which need T069's manifest field.
- H: the spike (T158) before every other H task; the sources (T159) before search (T161) and installed packages
  (T162); T162 before the removal impact (T163) and the tab (T166); the edits (T164) before the change runner (T165);
  T163 and T165 before the tab (T166), which comes before the E2E (T167). H needs B's `IMemberReferencingNode` (T034)
  and nothing from E to G.
- Within a batch, [P] tasks touch different files; tasks without [P] run in order.

## Parallel opportunities (inside a batch, for a single agent)

- A1: T003 ∥ T001–T002. B6: T032 ∥ T033 (and both ∥ T031). C1a: T042 ∥ T040–T041. D1e: T091 ∥ T089–T090. D1g: T097 ∥
  T095–T096. D2b: T105 ∥ T103–T104. G2: T139 ∥ T138, T140. G3: T143 ∥ T141–T142. G4: T146 ∥ T144–T145. H4: T168 ∥
  T166–T167.

## Implementation strategy

1. MVP = Checkpoint C2 plus D1a–D1b (US1–US4): the analyzers and generators, the reshaped node model, the published
   packages with the stable API, and extensions on every editor surface (plan "Order rationale").
2. Then the features on the frozen API, in story-priority order: settings, shortcuts, presets and `--profile` (D1);
   the author kit and the Godot sample (D2); run profiles (E); `netprints new` and the C# target in the editor (F);
   the tail (G); the NuGet packages (H, after G although US12's priority is higher: plan "Order rationale").
3. P3 ends with the release preparation (T147–T152), the final review (T155), its fixes (T156) and the merge
   preparation (T157).
4. Defects are caught at five checkpoints: A+B, C1+C2, D1+D2, E+F+G (each a review batch and a fix batch) and the
   final review, which reviews H in full and the rest only for integration.

## Task counts

| Sub-phase | Active tasks | Implementation batches (units) | Review, fix, merge |
|---|---|---|---|
| A — foundation | 16 (T001–T016); T017, T018 merged | A1 4, A2 3, A3 3, A4 4, A5 5 = 19 | into B-R, B-F (A+B) |
| B — node model and search | 21 (T019–T039) | B1 3, B2 4, B3 4, B4 3, B5 4, B6 3, B7 5 = 26 | B-R (T038), B-F (T039) |
| C1 — public contracts | 18 (T040–T057); T058, T059 merged | C1a 4, C1b 4, C1c 4, C1d 5, C1e 4, C1f 5 = 26 | into C2-R, C2-F (C1+C2) |
| C2 — facets and the publish gate | 19 (T060–T078) | C2a 4, C2b 4, C2c 4, C2d 3, C2e 4, C2f 3 = 22 | C2-R (T077), C2-F (T078) |
| D1 — extension host in the editor | 22 (T079–T098, T173, T175); T099, T100 merged | D1a 4, D1b 5, D1c 4, D1d 4, D1e 4, D1f 3, D1g 4 = 28 | into D2-R, D2-F (D1+D2) |
| D2 — author kit and the Godot sample | 12 (T101–T112) | D2a 4, D2b 4, D2c 4, D2d 3 = 15 | D2-R (T111), D2-F (T112) |
| E — run and build | 14 (T113–T126); T127, T128 merged | E1 4, E2 3, E3 4, E4 3, E5 3 = 17 | into G-R, G-F (E+F+G) |
| F — command line and the C# target in the editor | 6 (T129–T133, T172); T134, T135 merged | F1 3, F2 4 = 7 | into G-R, G-F (E+F+G) |
| G — tail | 15 (T136–T146, T170, T174, T153, T154) | G1 4, G2 4, G3 5, G4 5 = 18 | G-R (T153), G-F (T154) |
| H — NuGet packages | 12 (T158–T169) | H1 4, H2 5, H3 5, H4 4 = 18 | into PR-R, PR-F (the final checkpoint) |
| Release preparation, final review and merge | 9 (T147–T152, T155–T157) | PR-P carries 5 units of release preparation | PR-P, PR-R, PR-F, PR-M |
| **Total** | **164 active of 175 IDs** (10 merged; T172–T175 added by Review C1+C2) | **50 batches, 194 units** (+5 units in PR-P = 199) | **12 batches** (4 review, 4 fix, PR-P, PR-R, PR-F, PR-M) |

62 batches in all (54 after S2d and A2, 68 before S2d). The units refine plan.md's first estimate per sub-phase, and its
table carries them: A 19 (was 16: the M5 spike and the carry-overs are sized separately), B 26 (25), D1 26 (25), D2 15
(14); C1, C2, E, F and G are unchanged; H (18) is new in S2e. The total, 195 units, is the plan's "about 195". Five
review checkpoints replace the nine per-sub-phase reviews; T170 (Checkpoint G) has a higher ID than H's tasks because
IDs are never renumbered.

## Requirement coverage

Every active FR (105) and SC (15) maps to at least one task. US13, US14, FR-094–FR-106, SC-013 and SC-014 moved to
P3c and have no task (T152 marks them as moved).

| Requirement | Tasks |
|---|---|
| FR-001 | T001, T002 |
| FR-002 | T004, T005 |
| FR-003 | T008, T053, T054, T055 |
| FR-004 | T006, T007, T053, T054, T055 |
| FR-005 | T009, T010 |
| FR-006 | T011, T088 |
| FR-007 | T012 |
| FR-008 | T001, T003, and every rule and generator task (T004, T008, T009, T025, T027, T030, T045, T074, T075) |
| FR-009 | T024 |
| FR-010 | T024, T025, T027, T028, T029, T032 |
| FR-011 | T030 |
| FR-012 | T031, T148 |
| FR-013, FR-014 | T035 |
| FR-015 | T034 |
| FR-016 | T019, T020, T021, T022 |
| FR-017 | T019, T022 |
| FR-018 | T033 |
| FR-019 | T040, T042 |
| FR-020 | T070, T071, T073 |
| FR-021 | T051, T060, T061, T062, T064, T065, T068, T136, T137, T138, T175 |
| FR-022 | T052 |
| FR-023 | T060 |
| FR-024 | T063 |
| FR-025 | T066 |
| FR-026 | T067 |
| FR-027 | T043, T044, T045, T046, T171 |
| FR-028 | T047, T051 |
| FR-029 | T069, T145 |
| FR-030 | T051, T056, T072, T073 |
| FR-031 | T074, T075, T174 |
| FR-032 | T079, T082, T173 |
| FR-033 | T080, T173 |
| FR-034, FR-035 | T081, T175 |
| FR-036 | T084 |
| FR-037 | T109, T125 |
| FR-038 | T072 |
| FR-039 | T089, T090 |
| FR-040 | T068, T087, T089 |
| FR-041, FR-042 | T089 |
| FR-043 | T086 |
| FR-044 | T046, T088, T090 |
| FR-045 | T093 |
| FR-046 | T092, T093 |
| FR-047 | T093 |
| FR-048–FR-050 | T092 |
| FR-051 | T113, T116 |
| FR-052 | T113 |
| FR-053 | T119 |
| FR-054 | T114, T115 |
| FR-055, FR-056 | T115 |
| FR-057 | T116, T117 |
| FR-058 | T119 |
| FR-059 | T120 |
| FR-060 | T121 |
| FR-061 | T119 |
| FR-062 | T118 |
| FR-063 | T122 |
| FR-064 | T123 |
| FR-065 | T125 |
| FR-066 | T095, T097 |
| FR-067–FR-069 | T096 |
| FR-070, FR-071 | T047, T048, T172 |
| FR-072 | T049 |
| FR-073 | T131 |
| FR-074 | T132 |
| FR-075 | T050 |
| FR-076 | T101, T102, T103 |
| FR-077 | T103, T138 |
| FR-078 | T102, T103, T104, T138 |
| FR-079 | T106 |
| FR-080 | T106, T107 |
| FR-081 | T108, T109 |
| FR-082, FR-083 | T013 |
| FR-084 | T015 |
| FR-085–FR-087 | T014 |
| FR-088 | T085, T147, every sub-phase gate (Dock check, `ShellMainFlow`) |
| FR-089 | T158, T159, T161, T162 |
| FR-090 | T164 |
| FR-091 | T165 |
| FR-092 | T163, T166 |
| FR-093 | T161, T162, T166 |
| FR-107 | T129 |
| FR-108 | T106, T130 |
| FR-109 | T136, T137 |
| FR-110 | T138, T139 |
| FR-111 | T140 |
| FR-112 | T141, T142, T143 |
| FR-113, FR-114 | T144, T146 |
| FR-115 | T145 |
| FR-116 | T003, T023, T032, T036, T042, T046, T056, T071, T072, T091, T094, T097, T105, T107, T125, T129, T139, T143, T146, T148, T151, T160, T168 |
| FR-117 | T021, T033, T150, T158 |
| FR-118 | T002, T084, T094, T101, T124, T150, T167 |
| SC-001 | T004, T005, T027, T074, T075, T076, T141, T150 |
| SC-002 | T019, T022, T029, T031, T035, T037, T142, T170 |
| SC-003 | T071, T073, T109, T110 |
| SC-004 | T081, T083, T098 |
| SC-005 | T006, T053, T055, T057, T076 |
| SC-006 | T086, T090, T092, T094, T098 |
| SC-007 | T094, T098 |
| SC-008 | T114, T115, T118, T124, T126 |
| SC-009 | T096, T098 |
| SC-010 | T049, T057, T133 |
| SC-011 | T102, T103, T106, T110, T138 |
| SC-012 | T164, T165, T167, T169 |
| SC-015 | T014, T015, T016 |
| SC-016 | every sub-phase gate, T150 |
| SC-017 | T151 |

## ADR schedule coverage

| ADR (plan.md) | Task |
|---|---|
| 0024 | T003 |
| 0025, 0028, 0029 | T032, T023, T036 |
| 0027 | T046 |
| 0030 | T042 (split), T072 (facet, CLI trust) |
| 0041 | T107 |
| 0032 | T139 |
| 0026 (with its amendment content) | T143 |
| 0042 (new) | T160 |
| Amendments | ADR-0003: T003 (StyleCop note), T015 (FU-5 records), T074; ADR-0007: T074; ADR-0023: T046; ADR-0020, ADR-0021: T072; ADR-0010: T104; ADR-0035: T133 (B1 and C in G, C's distribution in P3c) |

## Analysis

`speckit-analyze` ran over spec.md, plan.md and this file on 2026-10-16 (with the constitution, research.md and the
contracts as references). Result: 0 CRITICAL, 1 HIGH, 10 MEDIUM and 8 LOW findings. No constitution conflict. Every
active FR (100) and SC (14) has a task (FR-049 and FR-068 through T092's and T096's ranges); no task serves a P3c
item; the checkpoint gates of plan.md and this file match after the fixes.

Fixed in the same commit:

| Id | Severity | Finding | Fix |
|---|---|---|---|
| I1 | HIGH | SC-003 and SC-011 counted "12 conformance checks", but FR-077 adds checks and FR-078, the contract (§6) and the quickstart want a failing fixture for every check of the kit | spec SC-003, SC-011: every check of the kit |
| C1 | MEDIUM | FR-038 was in no row of plan.md's sub-phase table; FR-116 and FR-118 apply everywhere but were unlisted | plan: FR-038 in C2; a note for FR-116 and FR-118 |
| I2 | MEDIUM | SC-015 was in no checkpoint gate | plan and T016: gate A |
| I3 | MEDIUM | research R4 rechecks SC-005 at the publish gate; plan's gate C2 omitted it | plan and T076: "SC-005 rechecked" |
| I4 | MEDIUM | gate labels "C (publish gate)" and "D" did not name the sub-phases C2 and D2 | plan labels |
| I5 | MEDIUM | gate B said "trim ratchet −2", but `PrimitiveKinds` may also remove IL2057 (FR-018) | plan and T037: "down by at least 2" |
| I6 | MEDIUM | the spec's Sizing said the plan updates the roadmap's phases table; the plan only proposes it for the owner | spec Sizing wording |
| I7 | MEDIUM | the units and batches of this file differ from the plan's first estimate | plan table carries the refined numbers, first estimate noted |
| I8 | MEDIUM | plan's G row kept the API reference and "the remaining guides" for the end; the guides land in their own sub-phases | plan G row |
| U1 | MEDIUM | T031's 13 renames were listed only in a record outside the repository | T029 records the overrides; T031 derives the renames from them |
| U2 | MEDIUM | no task tested the launch-settings error edge case on the button, US4 scenario 7, or the Godot extension loading in the editor (SC-003) | T119, T125, T109 |

LOW, fixed: a path placeholder in T033; the spec's sub-phase outline now says plan.md refines it into nine
sub-phases.

LOW, recorded and left as they are:

- The spec's Clarifications answer 1 still names B17's v1, the gallery cut lines and Checkpoint E; the 2026-10-16
  session narrows it in place, and the clarifications stay a dated record.
- plan.md lists ADR-0026 both as a new ADR and among the amendments. ADR-0026 is not written yet; P3b writes it once,
  with the amendment content (`PrepareFromSearchAsync`, offers from typed pins).
- research R4 says "Checkpoints C1 and C"; C is C2. Research is the decision record and is not edited here.
- Batch A5 holds five 1-unit tasks (the plan's rule is 2–4 related tasks, up to 6 when mechanical); three are small
  carry-over fixes and one is a mechanical triage. The coordinator may move T015 to a batch of its own if A5's context
  grows.
- The model-driven grid (FR-112) has no task: it moved to P3b with the tail.
- The roadmap's P3 estimate (about 2.5 weeks against about 20 weeks of batches) waited for the owner; batch P3-S2d
  states it as about 4 weeks in agent time.

Batch P3-S2d (2026-10-16) later moved the tail (T136–T154) to P3b, merged the per-sub-phase reviews into five
checkpoints and changed the CI process. The analysis above is the record of S2c; its counts are superseded by "Task
counts".

### Analysis of batch P3-S2e (2026-10-16)

`speckit-analyze` ran again over spec.md, plan.md and this file (with the constitution, research.md, data-model.md,
quickstart.md and the contracts as references) after US12, US16 and US17 came back and the version moved to 0.4.0.
Result: 0 CRITICAL, 0 HIGH, 6 MEDIUM and 8 LOW findings. No constitution conflict. Every active FR (105) and SC (15)
has a task; every task ID T001–T170 exists once; each batch header's units equal its tasks' units (50 implementation
batches, 190 units); no task serves a P3c item.

Fixed in the same commit:

| Id | Severity | Finding | Fix |
|---|---|---|---|
| M1 | MEDIUM | NPW010–NPW013 (T159) and NPK014 (T138) add diagnostic codes, but their rows in the diagnostics guide, whose staleness test compares it with the code constants, were left to later tasks, so those commits would fail CI | T159 and T138 add the guide rows in the same commit; T168 links to them |
| M2 | MEDIUM | T137 (value editors) and T145 (extension themes on the Appearance page) touch views without the avalonia skills, and T144 loaded only `avalonia-styling` | the three tasks load `avalonia-xaml` and `avalonia-styling` |
| M3 | MEDIUM | the requirement coverage mapped FR-012, FR-088, FR-116, FR-117, FR-118, SC-001, SC-016 and SC-017 to T157, whose release work moved to PR-P in batch A2 | mapped to T147, T148, T150 and T151 |
| M4 | MEDIUM | the package `ProjectEdit` records add public Core API after the publish gate, where plan.md allows new shapes only under an NPXE id | NPXE0018 (research R16, R33, T164, contracts/nuget-references.md §2) |
| M5 | MEDIUM | the spec's User Scenarios intro kept a broken line from S2d ("in the Clarifications may move out of P3.") | rewritten |
| M6 | MEDIUM | plan.md listed ADR-0026 both as a new ADR and among the amendments (an S2c LOW that G's return makes real again) | written once, with its amendment content (plan Scale/Scope and ADR schedule, T143) |

LOW, recorded and left as they are:

- US16 and US17 have no success criterion of their own (as in the original spec); gate G uses their acceptance tests
  and SC-001, SC-002 and SC-011.
- FR-089 names no latency budget for package search, only the 5-minute cache; performance work is P8's.
- Research R33 states that `dotnet package remove` leaves the `PackageVersion` item under Central Package Management;
  T164's byte comparison with the CLI is the check, and the contract changes with the test if the CLI differs.
- Sub-phase H runs after G although US12's priority (P3) is above the tail's (P4); plan.md's "Order rationale" gives
  the reason (checkpoint sizes, no dependency either way).
- T170 (Checkpoint G) has a higher ID than H's tasks because IDs are never renumbered.
- The deprecated and vulnerable badges are tested on NuGet's metadata types and on a fake service only; a local
  folder feed carries no such metadata, so `ManagePackages` does not show them.
- The package edit tests run on Linux only (Editor.Tests); the Windows CLI leg does not cover Windows paths in
  `Directory.Packages.props`. An authenticated private feed is on the owner's manual list (T152).
- The Clarifications keep the superseded answers of 2026-10-16 (the P3c split, the tail to P3b) as a dated record; the
  last answer of that session narrows them.
