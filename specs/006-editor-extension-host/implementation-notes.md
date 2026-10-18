# Implementation notes: Editor Extension Host (P3)

Decisions, measurements and checkpoint reports of the P3 batches, in the order they happen. Each batch adds its own
entries; a decision is written as "Decision: …" with its reason. Tasks are in [tasks.md](tasks.md).

## Batch P3-S2c (tasks and analysis)

- Decision: batches are labelled by sub-phase (A1–A5, B1–B7, C1a–C1f, C2a–C2f, D1a–D1g, D2a–D2d, E1–E5, F1–F2,
  G1–G5; G moved to P3b in S2d), with letters inside C1, C2, D1 and D2 so a batch label never reads as a sub-phase
  name.
- Decision: a test-first task commits its red test together with the change that makes it green, so every commit
  builds and passes (bisect); both runs are recorded here.
- Decision: checkpoint tasks carry no units of their own (T133 also applied the scope rule until S2d removed it).
- Decision: the NPS rules (T004) land inactive in the repository build and are switched on together with the sweep
  (T005), so no commit fails the build.
- Decision: the guide and ADR text of each feature lands in the sub-phase that builds it; sub-phase G keeps only the
  release notes, the guide images, the quickstart run and the docs site check.

## Batch P3-S2d (tail to P3b, five checkpoints, process)

Owner-approved 2026-10-16. Docs only; no code.

- Decision: sub-phase G (US16, US17, FR-109–FR-115, tasks T136–T154) moved to roadmap phase P3b. The spec keeps "moved
  to P3b" stubs for the stories and FRs, as for P3c; tasks.md keeps T136–T154 reserved as one stub section; nothing is
  renumbered. FR-116 stays in P3. No success criterion was tied only to the moved items (SC-001, SC-002, SC-016 and
  SC-017 were only partly served by G tasks and stay).
- Decision: the manifest `themes` field (FR-029, T069) stays in P3: it must exist before the packages publish. T069 is
  relabelled US3. P3b adds the Appearance page offering and its validation.
- Decision: NPK014 (graph rules) and the S7 analyzers NPA030–NPA033 move with the tail. The kit ships NPK001–NPK013,
  NPK015 and NPK016 in P3; NPK014 stays reserved.
- Decision: the release preparation of G (T147–T151: guide images, the 0.4.0 release notes, the quickstart run, the CI
  budget and ratchet checks, the docs site, and the SC table of T152) stays in P3. With T147–T154 reserved for P3b it
  folds into T157 (the merge batch, 5 units), after the final review and its fixes. Ambiguity for the coordinator: the
  folded work is not itself reviewed; a separate batch before T155 is the alternative.
- Decision: reviews run at five checkpoints, each one opus review batch plus one sonnet fix batch: A+B (T038, T039),
  C1+C2 (T077, T078), D1+D2 (T111, T112), E+F (T134, T135) and the final review (T155, T156). T017/T018, T058/T059,
  T099/T100 and T127/T128 are "merged into" those and keep their IDs. The sub-phase gate reports ("Checkpoint X" tasks)
  stay. The M5 spike gate in A3 is unchanged.
- Decision: the scope rule is removed (T133 only records the gate and the ADR-0035 amendment). The Clarifications
  answer 1 cut lines for R6, N2, N5, S7, SUG5, B1 and C no longer apply to P3.
- Decision: process. Implementers push and return without waiting for CI; the coordinator watches PR #20's CI in the
  background; each batch first checks CI at the previous head and fixes any red before its own tasks.
- Decision: P3 is about 4 weeks in agent time. Plan units are human half-days and overstate calendar time by about 5
  times, so the roadmap states estimates in agent time. P3b gains the tail; P3c is unchanged.
- New counts: 12 active stories, 93 active FRs, 14 active SCs; 8 sub-phases; 130 active tasks of 157 IDs (19 stub, 8
  merged); 42 implementation batches with 155 units, plus PR-M with 5 units of release preparation (160); 53 batches in
  all (42 implementation, 4 review, 4 fix, PR-R, PR-F, PR-M).

## Batch A1 (T001-T003)

- Decision: `GeneratorTestHost` moved to `tests/NetPrints.Testing/Roslyn` (namespace `NetPrints.Testing.Roslyn`, types
  made public). `NetPrints.Testing` references `NetPrints.Annotations` with `Aliases="Annotations"` and
  `PrivateAssets="all"`, so the alias does not leak to the projects that reference Testing. `NetPrints.Catalog.Tests`
  now references Testing and keeps its own aliased Annotations reference.
- Decision: `EquatableArray<T>` moved from `NetPrints.Annotations` to `src/Shared/Roslyn` (namespace `NetPrints.Roslyn`)
  and is linked into Annotations and the four new projects. A small `DiagnosticHelpLinks` helper (guide URL with the
  lower-case code as anchor) sits beside it. No descriptor factory: RS2002 tracking needs the `new DiagnosticDescriptor`
  call sites visible to the release analyzer.
- Decision: the four projects carry no rules yet, so `AnalyzerAssemblyTests` loads each assembly, checks that every
  analyzer found declares supported diagnostics tracked in the release files, runs the analyzers in a
  `CompilationWithAnalyzers` host, and checks every code fix targets a diagnostic of its sibling analyzer assembly.
  The checks are vacuous until T004 lands NPS001; the test needs no change then.
- Decision: `tests/Directory.Build.targets` imports `../Directory.Build.targets` first. MSBuild picks the nearest file,
  and without the import the experimental opt-in target is lost and the UI tests fail with NPXE errors.
- Decision: the Core package packs the Authoring pair from `..\NetPrints.Authoring*\bin\$(Configuration)\netstandard2.0`;
  `dotnet pack -c Release` on Core was checked and holds both under `analyzers/dotnet/cs`.
- Deviation: T001 and T002 are one commit. The new test project makes `CiWorkflowTests` red until the matrix row
  exists, so the row travels with the project to keep every commit green. `CiWorkflowTests` also needed three edits that
  the task text does not name: the expected leg list gains "Analyzers" (after "Core"), and `cli-windows.yml` gains the
  path filters for the new `src/` projects (Annotations became part of the Cli.Tests closure through Testing),
  `src/Shared/**` and `tests/Directory.Build.targets`.
- Red/green: `AnalyzerAssemblyTests` was written first and failed 10 of 10 (the four projects did not exist); green 10
  of 10 after the projects, 13 of 13 with `SharedRoslynTests` (written after the code). `CiWorkflowTests` was red on
  `TestMatrixCoversEveryTestProjectExactlyOnce` (named `NetPrints.Analyzers.Tests`) and
  `CliWindowsPathFilterCoversTheCliTestsBuildInputs`, then green. `DiagnosticsGuideTests` failed 6 of 6 (no page), then
  green 6 of 6.
- Measurement (build time, `dotnet build NetPrints.slnx --no-incremental`, warm compile server, 3 runs each): 9.0 to
  9.5 s before, 9.0 to 9.9 s after (about +1%, inside run-to-run noise). The new assemblies hold no analyzers yet, so
  `ReportAnalyzer` shows nothing for them; repeat at T004 and in S2a.
- Decision: `docs/guide/diagnostics.md` lists the seven existing prefixes (NPC, NPD, NPE, NPT, NPW, NPX, NPXE).
  `DiagnosticsGuideTests` requires a row for every code in a `*DiagnosticCodes` class or `AnalyzerReleases.*.md`, a
  section per prefix, a severity and cause per row, and that each listed code appears quoted in some `src` file or in a
  release file. NPD, NPW, NPE and NPXE codes live outside `*DiagnosticCodes` classes, so only the last check covers
  them; consolidating them is a separate cleanup. Severities of NPD, NPW, NPT and NPX were read from the code that
  reports them.
- Decision: ADR-0024 written and listed in `docs/adr/README.md`; ADR-0003 gets the amendment line.

## Batch A2 (T004-T005)

- Decision (process): the release preparation (T147-T152) moved out of T157 into its own batch PR-P, just before the
  final review, so that T155 reviews it; T157 keeps only the merge preparation. This supersedes the P3-S2d note that
  folded it into T157.

- Deviation (process): the coordinator later said to skip that tasks.md move because the owner is putting sub-phase G
  back into P3 and a later docs batch restores the section. The edit was already made in A2 and is left as it is.
- Decision (T004, default pending owner): non-public fields and consts, `internal` and `protected` included, may stay
  grouped (NPS003 points at them). The owner has not answered yet.
- Decision (T004): `[A, B]` in one list is not reported by NPS001; only separate lists sharing a line are. NPS001
  reports at the attribute list, NPS002 and NPS003 at the first token of the second member. A field with braces
  (a lambda or initializer) counts as a member with braces and needs a blank line.
- Decision (T004): the rules are off through `isEnabledByDefault: false` (release files list them as Disabled),
  because `src/Directory.Build.props` and `tests/Directory.Build.props` already reference the analyzer projects since A1.
  T005 switches them on with `dotnet_diagnostic.NPSnnn.severity` in `.editorconfig`. Tests enable each rule through
  compilation options at its contract severity.
- Decision (T004): `RepoAnalyzers.CodeFixes` needs a direct `Microsoft.CodeAnalysis.CSharp` 5.0.0 reference: with the
  Workspaces override alone, central transitive pinning (5.9.0) dropped CSharp and Common from the compile references.
- Measurement (T004, `dotnet build NetPrints.slnx --no-incremental`, 3 runs): 9.4 to 11.5 s against A1's 9.0 to 9.9 s;
  the rules are disabled, so this is mostly noise. Re-measured with the rules on in T005: 9.1 to 10.0 s (no visible cost).
- T005: the rules are on through a `[{src,tests}/**.cs]` section of `.editorconfig` (NPS001 and NPS002 error, NPS003
  suggestion). `dotnet format analyzers` changed 59 files (+477 lines, 5 attribute lines moved), fewer than the plan's
  estimate of about 450 sites; the diff holds only attribute splits and blank lines; goldens, samples and generated
  files are unchanged. Build: 0 NPS hits and 0 warnings; `dotnet format --verify-no-changes` clean.
- Red/green: the 36 style tests failed before the analyzers existed (42 after adding the harness, 13 old ones green),
  then 52 of 52 in Analyzers.Tests. `DiagnosticsGuideTests` was red (NPS section and rows missing), then 6 of 6. Suite
  (Debug, no E2E pass): 4167 total, 4134 passed, 33 skipped, 0 failed.

## Batch P3-S2e (US12, US16 and US17 back in P3; version 0.4.0)

Owner decision 2026-10-16, verbatim: "Lo de los paquetes nuget debe quedar, ademas las secciones del inspector, editores
de valores y reglas del grafo y temas de usuario o extensiones debe quedar tambien lo que puedes sacar es el gestor de
extensiones y los samples remotos." Docs only; no code.

- Decision: User Story 12 (FR-089–FR-093, SC-012) and User Stories 16 and 17 (FR-109–FR-115) are active again; only
  User Stories 13 and 14 (FR-094–FR-106, SC-013, SC-014) stay in P3c. The spec records it as the last Clarification of
  2026-10-16 and narrows the earlier answers; there is no scope rule and no cut line.
- Decision: the model-driven grid (ADR-0036 c) stays in P3b; FR-112 says so. FR-112 is now a MUST (it was a SHOULD
  while the tail could be cut).
- Decision: FR-090 gains the read-only rule (a version from an MSBuild property, or several conditioned items for one
  id) and a matching edge case, so the edits never guess; NPW013 reports it.
- Decision: the tail is sub-phase G, restored from `51e0f06d` as T136–T146 (batches G1–G4) with a new gate T170
  (Checkpoint G); T153 and T154 become the E+F+G checkpoint's review and fixes, and T134 and T135 merge into them.
  The ADR-0035 amendment stays in T133 (Checkpoint F) and now records B1 and C in G, with C's distribution in P3c.
- Decision: the NuGet packages are sub-phase H, new: T158–T169 in batches H1–H4 (18 units), with research R32–R35,
  contracts/nuget-references.md, data-model §12, quickstart §14 and ADR-0042 (planned like ADR-0041: the package
  service and package edits, written in T160). The NuGet libraries live in a new `src/NetPrints.Packages/`; their
  transitive `Newtonsoft.Json` is banned in `src/`.
- Decision: the package `ProjectEdit` records are experimental under NPXE0018, because they add public Core API after
  the publish gate.
- Decision: checkpoints stay at five. G folds into the fourth (E+F+G, about 40 units; the others are 41–46). H folds
  into the final checkpoint, whose review covers H in full plus the release preparation and the whole PR lightly
  (about 23 units of new work). No sixth checkpoint was needed. H runs after G although US12's priority is higher:
  there is no dependency either way, and the sizes balance.
- Decision: P3 is about 5 weeks in agent time (195 implementation units plus 5 checkpoints; it was about 4 weeks for
  160). P3c shrinks to the extension manager and the remote samples, about 12 human days, about 1 week in agent time,
  on P3's package service. P3b loses the tail. The roadmap carries both.
- Decision (coordinator, owner-approved): P3a ships as v0.3.0, so P3's release is 0.4.0. Every "0.3.0" that meant P3's
  release now says 0.4.0 (the hard renames, `*REMOVED*`, the release notes, the package versions) in the feature
  folder, ADR-0034 and the roadmap. `netprintsApi` and `ExtensionApi.Version` for P3 become 0.4 (they were 0.3, tied
  to P3's package version; the code still says 1.0 until T070).
- New counts: 15 active stories, 105 active FRs, 15 active SCs; 10 sub-phases; 160 active tasks of 170 IDs (10
  merged); 50 implementation batches with 190 units plus 5 in PR-P (195); 62 batches in all.
- speckit-analyze: 0 CRITICAL, 0 HIGH, 6 MEDIUM (fixed) and 8 LOW; the table and the LOW list are in tasks.md
  "Analysis of batch P3-S2e". LOW, left as they are: US16 and US17 have no SC of their own; no latency budget for
  package search; R33's claim about `dotnet package remove` under CPM is checked by T164's comparison; H runs after G
  although US12's priority is higher; T170 has a higher ID than H's tasks; the package badges are not shown in the
  E2E (no local-feed metadata); Windows paths in `Directory.Packages.props` are not covered in CI; the superseded
  2026-10-16 Clarifications stay as a dated record.

## Owner questions taken by default

The plan left these product questions to the owner (research R3, R13, R23, R31). Each task below applies the default
and is marked "default pending owner" in tasks.md.

| Question | Default | Task |
|---|---|---|
| The blank-line rule: may `internal` and `protected` fields group? | yes, every non-public field and const may group | T004 |
| Pin labels with no resource | the raw key | T043 (applied in C1b, still pending the owner) |
| Node categories | one "NetPrints" category for the built-ins | T044 (applied in C1b, still pending the owner) |
| Live language switch | no; a language change applies after a restart | T046 (applied in C1b, still pending the owner) |
| External terminal | Linux detection order behind `run.linuxTerminal`; the Windows console closes at exit; macOS through `open -a Terminal` | T123 |
| Search order: This Methods before This Variables (the old canvas search showed variables first per base type) | This Methods first, as the default ranker's fixed order says; one ranker constant to flip | T022 |

## Batch A3 (CI fix, T006, T007)

- CI fix (cause): CI builds Release, the local build Debug. `ReferenceAssemblyPackageTests.cs` and
  `AnnotationsPackageTests.cs` chose the build configuration with `#if DEBUG / #else / #endif`; in Release the inactive
  `"Debug";` branch and its blank line sit between the field and the next member, and NPS002 reports the next member as
  missing a blank line. The A2 sweep and its local build ran Debug only. Fix: both read the configuration from
  `AssemblyConfigurationAttribute` (no preprocessor). Checked: `dotnet build NetPrints.slnx -c Release` is clean (0
  warnings, 0 errors) and was red on exactly these two sites before the change. The only other conditional compilation
  under `src/` and `tests/` is `#if NETSTANDARD2_0` in `src/NetPrints.Catalog/Engine/Polyfills.cs` (whole-file wrapper,
  built by the solution for netstandard2.0, clean). The `<Compile>` items in the projects are unconditional
  (removals of data folders, linked shared files), so none changes with the configuration.
- CI at e22e7aa2 (PR #20): red legs were Build and test (Linux), Performance budgets, Repository checks (Linux) and Test
  (Catalog), all with the two NPS002 errors; every other leg passed.

## R5-0 baselines

(T006) Both tests carry `[Trait("Category", "Performance")]` and no budget (no bound assertion; only shape checks), so
`CiWorkflowTests`' budget-file gate does not select them. Numbers go to the test output (`-showLiveOutput`).

- Deviation: M1 is `tests/NetPrints.Editor.UITests/Hosting/ShellStartupPerformanceTests.cs`, not under `Editor.Tests`.
  The shell host builds a `ShellWindow`, which needs the Avalonia headless platform that only `Editor.UITests` has;
  `Editor.Tests` has no platform. It is in the perf job's filter (the job runs `Editor.UITests` too). M2 is in
  `tests/NetPrints.Editor.Tests/Graph/GraphScopePerformanceTests.cs` as planned.
- M1 (`ShellApp.Start()`: `TestComposition` plus `CreateShellWindow`, the shell view model ready when it returns;
  headless; 6 runs, the first reported as cold): local Release, AMD Ryzen 9 7950X3D, .NET 10.0.11: cold 756 ms, warm
  runs 166, 184, 235, 218, 213 ms, median 213 ms, 16.06 MB allocated on the calling thread (stable to 0.1%). Run to run
  the time varies by about 30%; the C1 gate at +10% needs the perf job's median, not this local figure.
- M2 (5 runs; a method graph of 1000 call nodes with two parameters, chained by exec pins: 1002 nodes, 5002 pins;
  `new NodeGraphViewModel(...)`): 81, 45, 15, 13, 13 ms, median 14.8 ms; 35.15 MB, 35,080 bytes per node, 7,027 bytes per
  pin. Counting fakes (a counting `IMessenger` and a counting `ProjectClasses` accessor, both in
  `ClassEditorServices`) record 0 messenger calls and 0 project-classes calls while the graph is built: today nothing is
  resolved per pin, so the C1 "zero per-pin resolutions" gate starts at zero.
- M4 (trim count of `NetPrints.Editor`): 14 (`eng/trim-warnings.txt`, the ratchet).
- Perf-job medians with run ids: not available at this commit (the perf leg was red on the NPS002 build error at
  e22e7aa2). The first green perf run on this branch is the baseline for the +10% gates; the checkpoint report records
  it with its run id. The local numbers above are the stand-in.
- Perf-job baseline (recorded in Batch C-F2): run 38899627936 (head d7ddde50, the first green perf run), from its `test-results-perf` TRX:
  M1 cold 1341.2 ms, warm median 418.7 ms, 16,501,712 bytes; M2 median 51.9 ms, 36,037 bytes per node. The gate itself is the calibrated
  steady-state ratio in the Batch C-F2 entry: those absolute figures are a first-use sample (the median of five lands on a run still in the
  JIT's lower tier), CI shows 41 to 76 ms for M2 across heads that did not touch its path, so they cannot carry a 10% gate.
- Gate ratios (Batch C-F2b): local (Ryzen 9 7950X3D) M2 0.954, M1 4.75; CI Xeon Platinum 8573C M2 1.08, M1 5.55; CI EPYC 7763 M2 1.90, M1 3.22. The CI figures were measured at
  heads shown locally equal to R5-0 on the M2 path (within 1%, 8 interleaved runs); see the Batch C-F2b entry.
- Superseded as a gate (Batch C-F2c, ADR-0031 amendment 1): the numbers above stay as history. The M1 and M2 time gates no longer compare with a recorded figure; the perf job
  rebuilds R5-0 on the same runner (`eng/perf/prepare-r5-0.sh`, `eng/perf/r5-0.patch`) and `eng/perf/ab.sh` compares the two trees run in alternation, time without GC pauses and
  allocated bytes, each at head / base <= 1.10.

## M5 spike

(T007) Gate verdict: **PASS** for MS.DI with the registration generator on both ADR-0031 terms. The spike lives outside
the repository (nothing of it is in the PR); the code, the generated Pure.DI file and the BenchmarkDotNet reports are
kept in `.agent-archive/2026-10-16-p3-m5-spike/` beside the repository.

- What was composed: a copy of `EditorServices`' constructor and `ShellHost.Create` minus the window (28 container
  services: dispatcher, windows, dialogs, picker, clipboard, processes, file system, state store, backup options, recent
  projects, window state, project locations, project system, serializers, format registry, mapper, persistence and its
  binding, reflection host, code analysis host, run state, `EditorContext`, contribution registry, shell view model,
  command invoker, plus two session-scoped stand-ins). The Avalonia services are replaced by the `Editor.Tests` fakes,
  the project system is `NoSdkProjectSystem` (no MSBuild), `StateMigration.Run` is not called. Three compositions of the
  same graph, with the same host instances (logger factory, extension host, settings, host channel) given to all: hand
  wired (today), MS.DI 10.0.12 with factory-lambda registrations in the T008 shape, and Pure.DI 2.5.4. The first resolves
  are `EditorContext`, `ShellViewModel`, `CommandInvoker` and `PersistenceBinding`.
- Environment: AMD Ryzen 9 7950X3D (16 cores), Linux, .NET SDK 10.0.400, runtime 10.0.11, BenchmarkDotNet 0.15.8,
  Release, JIT (no ReadyToRun).
- Composition time, cold (12 launches, 1 invocation each; the host inputs are created before the timed call):
  hand wired 62.9 ms; MS.DI 75.3 ms (+12.4 ms, +20%); Pure.DI 65.6 ms (+2.6 ms, +4%); MS.DI registration plus
  `BuildServiceProvider` alone 6.3 ms. Warm (20 iterations): 540 us, 722 us (+0.18 ms), 536 us; the build alone 9.8 us.
  The M1 gate of 150 ms for the composition including the provider build and the first resolves holds with margin for
  this graph (75 ms), but this graph has no window and no MSBuild; C1 measures the real one.
- Allocations per composition: hand wired 465 KB, MS.DI 529 KB (+14%), Pure.DI 466 KB (+0%).
- Session scope (warm): hand wired `new` of two services 10 ns, 64 B; MS.DI `CreateScope` plus resolve 139 ns, 400 B;
  Pure.DI child composition 46 ns, 400 B. Per project open, so irrelevant next to the open itself.
- Generated size: the MS.DI registration file is 130 lines for 28 registrations (4.6 per registration, one line per
  constructor argument), so the 600-line bound is reached at about 130 registrations in that format, or at about 240 if
  the generator puts arguments on one line when they fit. The hand-written factory methods that a few registrations
  point to are 48 lines. Pure.DI generates 1,523 lines (1,182 without comments and blank lines) for the same graph and 5
  roots, so it fails the size term on its own; it is also the only one that cannot take the shell construction with its
  cycle without a factory lambda.
- Shapes the generator must express (all were expressible; the first four need a generator feature):
  1. a static factory method named on the attribute, for what a constructor call cannot do: the tuple returned by
     `PersistenceBinding.CreateSerializers`, `ContributionRegistry` (register, then `Freeze`), the shell cycle
     (`ShellViewModel` and `ShellProjectActions` and `DockShellAdapter` assign each other after construction),
     `BackupOptions` and `ProjectLocations` (computed from the environment);
  2. `ILogger<T>` and non-generic `ILogger` parameters resolved with `ILoggerFactory.CreateLogger` (they are not in the
     collection);
  3. instances the host provides (`ILoggerFactory`, `ExtensionHost` as `IExtensionHost`, settings, channel, time,
     scheduler, `Func<IMessenger>`), registered by hand next to `AddEditorServices()`;
  4. one concrete type registered as several services (a `ShellParts` holder whose members are services); MS.DI
     disposes a lambda-created `IDisposable` it holds, so a member service that is also owned by the holder is disposed
     twice unless the holder is the only disposable registration (the generator or the factory must not register the
     members as owned instances);
  5. service type different from the implementation (`[Singleton<IFoo>]` on the class), optional constructor
     parameters passed explicitly (the generator resolves an optional parameter only when it is registered), and
     scoped services (`AddScoped`) all worked as plain generated lambdas.
- Not measured: ReadyToRun (ADR-0031 decision 8 measured the delta at about half of the JIT one), the real `ShellWindow`
  and the Avalonia services, MSBuild, and an extension's own provider. C1 gate M1 measures the real composition.
- Conclusion for the coordinator: T008 and T053–T055 stand as written. The one addition worth putting in T008 is the
  factory-method option on the attributes (shape 1 above), which the contract text already hints at ("directly or
  through a factory").

## Batch A4 (T008-T010)

- T008 shape: `[Singleton<T>]` and `[SessionScoped<T>]` (both with `Factory = "Method"`, a static factory method on the type, for tuple
  serializers, the registry freeze, the shell cycle and `BackupOptions`/`ProjectLocations`) and `[ScopeFactory]`. `AddEditorServices()`
  is one lambda per registration, one line each; constructor parameters resolve from the provider, `ILogger<T>` and `ILogger` come from
  `ILoggerFactory`, `IEnumerable<T>` is `GetServices`, an optional parameter is passed explicitly unless its type is registered here.
  Host instances stay hand-registered next to the call. `[ScopeFactory]` partial classes need an `IServiceProvider services` member;
  their `partial Create*` methods take matching parameters by type and resolve the rest.
- Size (240 registrations in one file): 255 lines, 1.06 lines per registration, so the 600-line bound is reached near 585
  registrations (the spike's per-argument format gave 4.6 lines each).
- Decision (double dispose): a type with several service attributes is registered once and forwarded for the other services when it is
  not disposable and has one lifetime. A disposable one, or one with mixed lifetimes, is NPI005 (new code; the contract table gets the
  row). A holder whose members are services stays a factory plus hand registration, as the spike found. A runtime test pins that a
  held disposable type is disposed once.
- Decision: the NPI rules are reported by the generators (spans at the attribute), and `CompositionDiagnosticsAnalyzer` and
  `SmartEnumDiagnosticsAnalyzer` only declare the descriptors, because RS2002 needs an analyzer to list them.
- Decision: constructors count when public or internal (the generated code is in the same assembly).
- Deviation: the generator tests use their own `GeneratorHarness` (loads the generators from the RepoAnalyzers output folder), not
  `GeneratorTestHost`, which is tied to the Annotations generator; no Annotations alias is needed in `Analyzers.Tests`.
- Decision: the injected attributes carry `[Embedded]` (a third generator, `EmbeddedAttributeGenerator`, adds the definition once).
  Without it every `src` project that has `InternalsVisibleTo` access to another got CS0436 on `[SmartEnum]`. The two Catalog
  fixtures that run the Annotations generator, which adds the same definition, drop the RepoAnalyzers reference.
- T009: `[SmartEnum]` emits `{Name}Id` and a lazily built `All` property (not a field), so a lookup declared after the members cannot
  see it half-built; NPI011 reports a lookup declared before them (same file) or in another file, and members that are properties or
  not `static readonly`. NPI010: not a top-level abstract partial class.
- T010: `CommandKey` has 75 members (26 letters, D0-D9, F1-F24, 15 named), per-member nested classes, no switch (a test scans the
  file). `CommandGesture` is `record(CommandModifiers, CommandKey)`, `ISpanParsable` and `ISpanFormattable`; span parsing allocates
  only the gesture (test). `CommandDescriptor.DefaultGestures` is `IReadOnlyList<CommandGesture>?`; the 24 built-in lists are typed
  values. New `CommandGesture.Display` uses the key glyph (`Esc`), so menus and the guide keep their text. The registry's
  "unparseable gesture" case is gone (parsing is at the boundary) and a null gesture is an invalid descriptor.
- Red/green: T008 tests 16 of 75 red (no generator), then 75 green; NPI doc rows red (2 guide tests), then green. T009 11 of 90 red,
  then 90 green. T010 red as a compile failure (142 errors), then green: Editor.Tests 1917 of 1917 in Release.
- Suite (Release, no E2E pass): 4310 total, 4276 passed, 33 skipped, 1 failed (`ShortcutGuideTests`, `Escape` against `Esc`), fixed
  with `Display`; Editor.Tests re-run 1917 of 1917. UITests (16 min) passed before that change and were not re-run.

## Batch A5 (T011-T016)

- Step 0: master (release prep PR #21, 91009737) merged into the branch as d7ddde50; the only conflict was the roadmap table, resolved to
  keep this branch's P3, P3b and P3c rows and master's P3a cell (merged 2026-10-15, PR #12 and #19, released v0.3.0). PR #20 reads
  MERGEABLE, so its pull_request workflows run again.
- T011: a section's `ExtensionSettingsDescriptor<T>` carries an optional `Validator` (`Func<string?, T, ValidateOptionsResult>`), set from
  an `[OptionsValidator]` partial class over DataAnnotations (`Microsoft.Extensions.Options` 10.0.12). `JsonFileSettingsStore.Read`
  runs it after deserializing; a failure, a JSON error or a null section logs 2006 once with the section, file and reason, and the
  section's default is used, so the start goes on. The composition has no generic host, so "the composition calls the validators"
  is the store's first read of each section. `NetPrintsSettings` stays a sealed record (init members); `EditorSettings.Theme` has
  `[EnumDataType]`; a custom internal `[NonBlankItems]` checks the two path lists. Log 2006 gained a `reason` argument.
- T012: `[JsonExtensionData]` goes on a public field `UnknownMembers? ExtensionData` of every document record except the value-like refs
  (`TypeRef`, `ParameterRef`, `MethodRef`, `ConstructorRef`, `VariableRef`, `TypedValue`; they are compared and used as keys) and
  `UnknownNodeDocument`. Deviation from the research text (`IReadOnlyDictionary`, init-only): System.Text.Json cannot bind an
  extension-data `init` member in source-generated code (it is treated as a constructor parameter), a non-public setter is not
  visible to the contexts that extension assemblies generate for derived node documents, and CA2227 forbids a settable collection
  property, so it is a field of `UnknownMembers`, a sealed `SortedDictionary<string, JsonElement>` (ordinal) with JSON-equal value
  equality. The node reader drops the `$kind` the derived-type read leaves among the unknown members. `show` prints
  `unknown <key> <compact value>` at the class, graph, member, node and pin levels; the merge driver merges the unknown members
  key by key (one side's change wins, a different change of one key on both sides is a `Scalar` conflict at `...unknown.<key>`) and
  compares node properties without them. Golden fixtures are unchanged (HelloWorld round trip is byte-identical). The editor's model
  round trip (`DocumentMapper`) does not carry unknown members yet; they survive at the document layer (format, show, merge).
- T013: NPX008 now compares every reference of the extension and of the private libraries in its folder (read from PE metadata) with
  the host's copy or the dependency's copy; the message names the library that asked. New fixtures: `Fixture.HostLib.V1/V2` (the test
  host loads V1, `Fx.HostConflict` is built against V2), `Fixture.MidLib` (built against SharedLib V2) and `Fx.TransitiveConflict`
  (ships MidLib, depends on `fx.libv1`). NPW009 (`ProjectMessage.UnreadableReference`): `ReflectionHost` probes each existing reference
  as a managed assembly, skips an unreadable one, logs it once per reload (event 1012) and adds it to `LastWarnings`.
- T014: `ProjectSystemOptions(GenerateOnLoad: false)` in the editor (`EditorServices.CreateProjectSystemOptions`) and in
  `ProjectCheck.CreateProjectSystemOptions`; the build step of `--check-project` still generates, as a build does. `catalog --check`
  compares with CR before LF removed on both sides. `git-install --command` that names an existing file is written as
  `'<path with / separators>'`; any other value is kept. `.gitattributes` already had `*.npcat.json text eol=lf`; the
  `cli-windows.yml` path filter already covers the closure these tests read, so neither changed.
- T015: 932 info-level hits in `src/` before (NPS003 532, S3267 59, CA1859 48, S1135 33, S3358 25, S2325 19, CA1822 19, S2365 15,
  S1075 15, S4136 11, S1104 11, S6608 10, CA1825 8, then 57 smaller rules), 905 after: fixed S6608 10, CA1825 8, CA1865 2, CA2249 1,
  CA1826 3, CA1834 2 and CA2263 1; every remaining rule is in the new ADR-0003 table with its reason (0
  unrecorded). S1104 (11) is the `ExtensionData` field of T012. CA1865 and CA2249 stay where `netstandard2.0` lacks the overloads.
- Red/green: T011 6 of 7 red (no validator, message without reason), then 7 green. T012 Core 3 of 5 red, Cli 4 of 8 red (the `format`
  tests pass once the document layer keeps the members), then 5 of 5 and 8 of 8 green; Core.Tests 847 of 847 and Cli.Tests 277 of 277 at
  that commit. T013 loader 2 of 33 red, Editor.Tests reflection 2 of 3 red, then green (Core.Tests 849 of 849). T014 two Editor.Tests,
  the CRLF catalog test and the `git-install` path test red, then green; Windows runs of the last two wait for the `CLI (Windows)` leg of
  PR #20 (the path test runs a shell script through `git diff`; if Git for Windows' `sh` cannot run that script, the test needs a
  different probe). T015 has no test; the build and the suite are its check.
- Checkpoint A, evidence so far (Release): suite 4342 total, 4309 passed, 33 skipped, 0 failed (UITests 13 min 31 s, Editor.Tests 1 min
  38 s, Core.Tests 35 s, Cli.Tests 41 s, Catalog.Tests 46 s); `dotnet format NetPrints.slnx --verify-no-changes` clean; solution build 0
  warnings. E2E (`NETPRINTS_E2E=1`, `--fail-skips on`, display start 140): 53 total, 52 passed, 1 skipped (`GuideScreenshotTests`, which
  the run reports as not run for explicit filtering), 0 failed. Dock check: the pin stays 12.1.0.6; Dock#1191 merged 2026-10-16, after the
  latest release (v12.1.0.9, 2026-10-13), so no release contains it yet and no bump is possible. FR-007: the round-trip tests above.
  Still to read from PR #20 CI at this head: the Analyzers leg, `CLI (Windows)` run id with the CRLF and `git-install` tests, the
  longest CI job; the R5-0 numbers and the M5 verdict are in their sections above. T016 stays unticked until that CI is green.
- Checkpoint A, CI at 6aab2555 green (Analyzers leg included): CI run 38906857501, CLI (Windows) run 38906856876, Release run 38906857074, Docs run 38906856885. T016 ticked. Longest CI job of run 38906857501: Desktop E2E (Linux, Xvfb), 13.5 min.

## Batch B1 (T019-T020)

- T019 scenarios in `NodeSearchGoldenTests` (9 goldens, already complete, none added): `rows-{method,constructor,class,event}` each over
  7 pins (canvas `none`, exec in, exec out, string data in, string data out, type in, int type out; the last two are the type-scoped
  search) and `filter-{for, write-line, static, netprints-loop, zzz}`. New `NodeSearchInvariantTests` (3): opening the search for five
  pin kinds creates no node and leaves the undo stack alone (US2 scenario 6); filtering and closing change no graph; a second open
  builds the same rows. All 12 pass on the code before SUG1.
- T020: 13 internal providers in `NodeSearch/Providers/` in the fixed order makeDelegate, nodeKinds, pinVariables, pinMethods,
  thisVariables, thisMethods, staticMethods, staticVariables, localVariables, types, typePin, eventEntries, extensionNodes (each
  category's first appearance matches the old switch, so the 9 goldens are unchanged). Owners are built-in kind ids; the two that
  serve the registry's kinds use `NodeSearchProviders.AnyKind`. `NodeSearchProviderRegistry.For(ExtensionRegistry)` builds the list
  once per registry (ConditionalWeakTable); the view model takes it through an internal constructor. `NodeSearchEntry` records
  (method, variable, type, node kind, make delegate, custom event, override) replace `Value`; rows expose an internal `Entry`.
  Renames done with `git mv`: `Search/` -> `NodeSearch/`, namespace `NetPrints.Editor.NodeSearch`, `NodeSearchViewModel`,
  `SuggestionRowViewModel`, `NodeSearchViewModelTests`; the xaml-literal ratchet path follows.
- Deviation: "catalog metadata feeds titles and keywords" is only a seam (`NodeSearchEntry.Keywords`, matched by the row); the editor's
  reflection provider exposes no catalog node hints yet, so nothing fills it. The skill docs under `.claude/skills` still cite the old
  `Search/` paths.
- Red/green: NodeSearchProviderTests red as a compile failure (no provider API), then 4 of 4 green; Editor.Tests search classes 79 of 79.
  Suite (Release): 4349 total, 4316 passed, 33 skipped, 0 failed; `dotnet format --verify-no-changes` clean; build 0 warnings.

## Batch B2 (T021-T023)

- T021: `INodeCreationService`/`NodeCreationService` (clamp, connect, one undo step; the entry switch moved out of the view
  model), `INodeSearchPrompts` with `DialogNodeSearchPrompts` over the dialogs. `NodeGraphViewModel.AddNode(position, pin,
  Func<NodeGraph, Node>)` replaces `AddNodeRequest` (deleted) and `AddNode<T>`; `Drop` and the Get/Set chooser use factories.
  The tests' `AddNode<T>` call shape is kept by `NodeGraphTestExtensions` (Testing.Ui, linked into Editor.Tests) using
  `Activator`, outside the product. Trim ratchet 14 -> 12 (`Activator.CreateInstance` and `GetConstructor` in the editor).
- Deviation: the new tests sit in `tests/NetPrints.Editor.Tests/Search/` (the existing folder and namespace of the search
  tests), not `NodeSearch/`: a `...Tests.NodeSearch` namespace would shadow `NetPrints.Editor.NodeSearch`.
- Deviation: `ConfigureAwait(true)` on the awaits of `NodeCreationService` (VSTHRD111 is an error outside view models and the
  graph needs the UI context), as `EditorDialogs` does.
- T022: `ISuggestionRanker` and `DefaultSuggestionRanker` (fixed category order, extension categories after in first-appearance
  order, stable inside a category), taken by the `NodeSearchViewModel` internal constructor. Recorded order change: This Methods
  before This Variables. No golden changes: the fixture class has no instance variables, so no golden holds a This Variables
  category; `NodeSearchRankerTests.ThisMethodsComeBeforeThisVariables` pins it. Existing 9 goldens and `SearchPerformanceTests`
  (30,000 rows, 2 s, 300 ms) pass unchanged.
- T023: ADR-0028 written and indexed.
- Red/green: tests were written before the code; the red state was the compile failure of the missing types, not a separate run.

## Batch B3 (T024-T026)

- T024: `NodeInputDataPin<T>` and `NodeOutputDataPin<T>` (no constraint; type from `TypeSpecifier.FromType<T>()`, second
  constructor takes the `ObservableValue<BaseType>` that `[PinType]` pins use). The typed input pin starts with
  `UnconnectedValue = default(T)` where the pin has an editable unconnected value (a primitive that is not an enum and not
  null by default); a reference type or enum keeps null as before. Attributes `NodeKind`, `NodeBase`, `Pin` (key, `Role`),
  `PinType`, `NodePinLayout` in `NetPrints.Core/Graph/`, all in `PublicAPI.Unshipped.txt`.
- Deviation: `PinRole` (T035's type) is defined now, with the five members, because `[Pin(Role = ...)]` needs it; T035 adds
  the behaviour, not the type.
- T025: `NetPrints.Authoring/Pins/` (`PinConventionGenerator`, `PinReader`, `PinModels`). Two pipelines, `NodeKindPins` and
  `NodeBasePins`. Per class it emits `[NodePinLayout("in.exec.Execution", ...)]` (the `PinKeys` reference format, in
  declaration order), `DeclarePins()` (`private` on a sealed class, else `protected`; `new` plus `base.DeclarePins()` when a
  base class declares pins) and the `partial` properties (`InputExecPins[offset + n]`, data pins cast). Base offsets come from
  the base class's partial properties in the same compilation, or from its `[NodePinLayout]` in a referenced assembly. A BCL
  `[DefaultValue]` on a typed input pin of a special-type primitive becomes `{ UnconnectedValue = (T)literal }`. Not covered
  (NPA rules, T027): a class that is not `partial`, a name without the `Pin` suffix (the full name is the key), an untyped
  data pin without `[PinType]` (skipped).
- Test seams: `AuthoringHarness` (the repository `GeneratorHarness`, but loading `NetPrints.Authoring` and referencing the
  real `NetPrints.Core`); `NetPrints.Analyzers.Tests` now references Core. Snapshots are inline raw strings, 7 tests.
- Red/green: `TypedPinTests` red as a compile failure (no typed pins or attributes), 13 of 13 green after. The generator tests
  red as `KeyNotFoundException` (no generated source) for the four snapshots, the compile test and the incrementality
  change test; green 7 of 7 after (the compile test then failed once on a missing `using` in the sample, fixed in the test).
  Analyzers.Tests 97 of 97.
- Suite (Release): 4380 total, 4347 passed, 33 skipped, 0 failed; build 0 warnings.

## Batch B4 (T027-T028)

- T027: `PinRulesAnalyzer` in `NetPrints.Authoring/Pins/` reports NPA001-NPA009 (errors, on by default), with
  `NpaDiagnosticCodes`, `PinSymbols` (shared helpers), the release rows and the guide section (the prefix list in
  `DiagnosticsGuideTests` gains NPA). B3's gaps are NPA001 (class not partial), NPA008 (name without the suffix) and NPA007
  (untyped data pin or output type pin without `[PinType]`).
- Decision: the rules apply to `[NodeKind]` and `[NodeBase]` classes; only NPA009 looks at other classes, and only at partial
  pin-typed properties, so ordinary get-only pin accessors stay free. Partial properties with `[ObservableProperty]` are not pin
  properties: the Core pin classes (`IncomingPin`, `OutgoingPin`) hit NPA009 the first time Core built with the analyzer.
- Decision: NPA001 also covers `[Pin]` on a property that is no pin and `[PinType]` on a pin that takes no member (exec pins,
  input type pins, typed data pins). NPA003 is checked on `[NodeKind]` classes only, skips constructors that chain to `this(...)`,
  and sees pin additions by name (`AddInputExecPin` and friends, `InputExecPins.Add`), without the semantic model.
- Decision: NPA005 is a compilation-end check: an imperative addition in a `[NodeBase]` constructor is reported when the base or
  any class derived from it declares pins in that collection. Its descriptor carries the `CompilationEnd` tag (RS1037).
- Decision: the NPA008 fix adds `[Pin("Name")]` with the current name, so the key does not change; the rule is met by any
  `[Pin]`. The NPA009 fix adds `[NodeBase]` to an abstract class and `[NodeKind("ClassName")]` to a concrete one (the id is a
  placeholder for the author), fully qualified when `NetPrints.Graph` is not imported; it offers no fix-all.
- T028: `MakePartialCodeFix` (NPA001: class and containing types, or a property rewritten as partial get-only),
  `AddPinAttributeCodeFix` (NPA008), `AddNodeMarkerCodeFix` (NPA009) in `NetPrints.Authoring.CodeFixes/Pins/`. The project
  gained the `Microsoft.CodeAnalysis.CSharp` reference (it had only the Workspaces one and no code yet) and links
  `NpaDiagnosticCodes.cs`.
- Test seam: `PinAnalyzerHarness` (analyzer and fix over samples compiled against the real Core, like `AuthoringHarness`).
- Red/green: the new tests were written first and run against the empty analyzer: 33 of 52 `Pins` tests failed (all the
  new rule and fix tests; the 19 passing were B3's and the negative cases). Green after: 53 of 53 (one more test for the
  `[ObservableProperty]` case, added after the Core build showed the false positive).
- Suite (Release): 4426 total, 4393 passed, 33 skipped, 0 failed; build 0 warnings; `dotnet format` clean.
- Not covered: NPA010 and NPA011 (shipped pin key files, S4) and the NPA02x-NPA04x families (later batches).

## Batch B5 (T029-T030)

- T029: eleven kinds declare their pins by convention: `IfElseNode`, `ForLoopNode`, `ThrowNode`, `TypeReturnNode`,
  `TypeOfNode`, `DefaultNode`, `MakeArrayTypeNode`, `ClassReturnNode`, `ReturnNode`, `EventEntryNode` and the entry family
  (`ExecutionEntryNode` is `[NodeBase]`; `MethodEntryNode` and `ConstructorEntryNode` are `[NodeKind]` and inherit its pin).
  Kind ids are the `BuiltInNodeKinds` strings; `BuiltInNodeDeclarationTests` ties them to those constants.
- Decision: kinds that stay imperative, pinned by `BuiltInNodeDeclarationTests.EveryOtherBuiltInKindIsListedAsImperative`:
  `AwaitNode`, `ExplicitCastNode`, `TernaryNode`, `CallMethodNode`, `ConstructorNode` (purity toggling removes and re-adds the
  `ExecNode` exec pins, so a fixed index would be wrong), `LiteralNode`, `MakeArrayNode`, `MakeDelegateNode`, `TypeNode`,
  `VariableGetterNode`, `VariableSetterNode` (pin keys or counts come from the constructor arguments) and `RerouteNode` (all pins
  dynamic). T031's rename counts therefore come from the overrides below, not from a full conversion; these kinds move when
  their pins can be declared (R2, G).
- Decision: the converted classes are `sealed`, so the generated `DeclarePins` is private and no protected API appears; the four
  `HandleInputTypeChanged` overrides leave the API (`*REMOVED*`), as do the old untyped getters of `ConditionPin`, `InitialIndexPin`,
  `MaxIndexPin`, `IndexPin`, `ExceptionPin` and `TypePin` (`TypeOfNode`), now `NodeInputDataPin<T>`/`NodeOutputDataPin<T>`. The
  protected `ExecutionEntryNode.DeclarePins` is the only new API. The generator now documents a protected `DeclarePins` (CS1591).
- Decision: a typed primitive input pin starts at `default(T)`, which would have saved `false`/`0` for `IfElseNode.ConditionPin` and
  `ForLoopNode.MaxIndexPin`; both constructors set `UnconnectedValue = null` so the golden and every stored graph stay
  byte-identical (`InitialIndexPin` keeps its `0`).
- Decision: pins that had no property (`ThrowNode` exec) are `internal` partial properties; the golden lists public properties only.
- `[Pin("...")]` overrides T029 left (property name differs from the stored key; T031 derives its renames from this list):
  `IfElseNode.ExecutionPin`, `ForLoopNode.ExecutionPin`, `ThrowNode.ExecutionPin` (all `Exec`), `ReturnNode.ReturnPin` (`Exec`),
  `ExecutionEntryNode.InitialExecutionPin` and `EventEntryNode.InitialExecutionPin` (`Exec`), `TypeOfNode.InputTypePin` (`Type`),
  `DefaultNode.DefaultValuePin` (`Default`), `ClassReturnNode.SuperTypePin` (`BaseType`).
- Red/green T029: `BuiltInNodeDeclarationTests` red 14 of 14 (no attribute on any kind), green 14 of 14 after (the imperative list
  needed `TypeNode`). `NodePinLayoutTests` (the S0 golden) green before and after, golden untouched. The generator snapshot for a
  protected `DeclarePins` was edited in the same step: Core cannot build until the generator emits the summary, and the test
  project references Core, so that one was not run red.
- T030: `PinKeyTrackingAnalyzer` (`NetPrints.Authoring/Pins/`) reads the `NodePins.*.txt` additional files; a project without them
  is not tracked. NPA010 is a warning (contract), NPA011 an error. A shipped key that is missing is paired, in order, with a declared
  key of the same direction and collection that is not recorded; the pair is reported once, at the new property (NPA011 names both
  keys and NPA010 is not repeated), otherwise at the class. A `*REMOVED*<line>` in the unshipped file lets a key go. Shipped kinds the
  compilation does not declare are ignored. The research text swaps the NPA010/NPA011 meanings; the contract is followed.
- T030 fixes: `PinKeyTrackingCodeFix` offers "Record the pin key in NodePins.Unshipped.txt" (NPA010), "Keep the shipped pin key"
  (`[Pin("old")]` on the replacing property, replacing an existing key) and "Remove the shipped pin key on purpose" (NPA011); no fix-all.
- `NodePins.Shipped.txt` is seeded from the golden's default variants, only keys that do not depend on node contents (46 lines,
  including the imperative kinds' fixed pins); `NodePinsFileTests` checks order, uniqueness, known kinds and that each key is in the
  golden. A temporary bogus shipped line failed the Core build with NPA011, then was reverted. The release step is in
  `docs/contributing/releasing.md`, the codes in `docs/guide/diagnostics.md`.
- Red/green T030: `PinKeyTrackingTests` red 6 of 10 (the four negative cases passed against no analyzer), green 10 of 10;
  `NodePinsFileTests` 2 of 2 and one descriptor test added after the analyzer.
- Suite (Release): 4453 total, 4420 passed, 33 skipped, 0 failed; build 0 warnings; `dotnet format` clean.

## Docs build fix (CI red at 409e74c6)

- The Docs workflow failed with 20 CS9248 errors: DocFX loads Core through MSBuildWorkspace, and the Authoring analyzer
  (a `ProjectReference` with `OutputItemType="Analyzer"`) only exists once built, so the generated halves of the partial pin
  properties were missing. Reproduced locally by moving the Authoring `bin/Release` away (20 errors), fixed by building
  Core in Release in `scripts/build-docs.sh` before `docfx` (0 errors). Chosen over loading analyzers in DocFX or pointing
  it at a built assembly: one line, the same build the rest of CI does.

## Batch B6 (T031-T033)

- T031: the overrides B5 left were nine, not fifteen, so eight properties were renamed and one kept. `ExecutionPin` (IfElse, ForLoop,
  Throw), `ReturnPin`, `InitialExecutionPin` (ExecutionEntry, EventEntry) became `ExecPin`; `DefaultValuePin` became `DefaultPin`;
  `SuperTypePin` became `BaseTypePin`. `TypeOfNode.InputTypePin` keeps `[Pin("Type")]` because `TypePin` is its output pin.
  `*REMOVED*` lines and the new getters are in `PublicAPI.Unshipped.txt`; the breaking entry is under a new "Unreleased" heading in
  `.github/release-notes.md`. The 68 call sites in `src` and `tests` were edited by hand, and `NodePins.Shipped.txt` is untouched.
- Deviation: the pin layout golden is not byte-identical. It lists the property names, so 12 lines change in the name column only;
  every slot (`OutputExecPins[0]`, ...) is the same. Regenerated with `NETPRINTS_UPDATE_SNAPSHOTS=1` and the diff checked.
  The analyzer test samples keep their own `ExecutionPin` names (they are source strings, not the Core nodes).
- Red/green T031: `PinRenameTests` red 8 of 9 (new names absent, old ones present; the `TypeOfNode` case passed), green 9 of 9.
- T032: ADR-0025 (index entry added) and the "Declaring pins" section of `docs/guide/extensions.md`.
- T033: `PrimitiveKinds` (`PrimitiveKind`, `PrimitiveCategory`, `Core/Core/`): one row per primitive (14: bool, the 8 integers, float,
  double, decimal, char, string); numeric rows are `INumberBase<T>` (`T.Zero`, `T.Parse`, invariant format; `R` for float and double).
  Replaces `TypeSpecifier.IsPrimitive`, `SignatureChange.PrimitiveDefaults`, the literal suffixes in `TranslatorUtil.ObjectToLiteral`,
  `TypedValueConverter` (both directions), the default parsing in `SpecifierFactory`, `PinTypeFamily.BuiltInTypes`,
  `MethodSignatureFormatter.Keywords` and the `Type.GetType` conversion of `NodePinViewModel.UnconnectedValue`.
- Finding: `sbyte` and `decimal` were missing only from `TypeSpecifier.IsPrimitive`; that kept `UsesUnconnectedValue` false, so such a
  pin had neither a default nor the text editor. The table fixes that (US2 scenario 7).
- Deviation: the IL2057 that the table removes is the one in `NodePinViewModel` (`Type.GetType` of the pin type), not the one in
  `TypedValueConverter.ParseEnum`: that one resolves enum types by name and the table does not cover enums. `eng/trim-warnings.txt` 12 to 11.
- Decision: `DefaultOperatorSpecifiers.defaultNumericTypes` stays a list. It is a deliberate subset (no `sbyte`, `long`, `ulong`) that
  decides which operator nodes exist, and widening it would change node search results against the P3a goldens (FR-017).
- Red/green T033: `PrimitiveKindsTests` written first; red as a compile failure (no `PrimitiveKinds` type), green 7 of 7 after.
  The defect (sbyte and decimal had no default and no editor) was not pinned by an assertion on the old code: the tests use
  the new type, so they cannot compile against it. Checked later (B-F3): with the `sbyte` row removed from `PrimitiveKinds`,
  `PrimitiveKindsTests` fails 4 of 7 on assertions (`TheTableMatchesTheGolden` among them); restored, 7 of 7. Next time a
  defect fix pins the old behaviour with an assertion before the new type lands.
- Suite (Release): 4469 total, 4436 passed, 33 skipped, 0 failed; build 0 warnings; `dotnet format` clean; trim ratchet 11.

## Batch B7 (T034-T037)

- T034: `IPinListEditor`, `IOverloadableNode` and `IPinNameEditor` (Core, `NetPrints.Graph`) join `IMemberReferencingNode`. The editor asks
  for the role in `NodeViewModel` (+/- buttons, tooltips, the add and remove commands), `ModelOperations` (current overload and the
  change), `NodePinViewModel.IsNameEditable` and `GraphSelectionInspectorTarget` (any `IMemberReferencingNode` resolves to the variable or
  method it names, so a make-delegate node now selects its method too). The built-in nodes implement the interfaces explicitly, so
  their public surface does not grow; `MakeArrayNode` gained two public overload-name constants that `ModelOperations` aliases.
- Decision: node search and navigation had no node-type checks left to replace (the creation service builds nodes from factories), so
  the callers that changed are the pin buttons, overloads, pin names and the inspector. The overload list still comes from the
  reflection provider in `NodeViewModel`, and the right-hand +/- buttons (generic parameters, a second list) stay on `MethodEntryNode`.
- Red/green T034: `NodeCapabilityTests` 5 of 8 red on assertions with only the interface declarations in place (the three that passed
  were negative or in-place cases), 8 of 8 green after; NodeViewModelTests and the inspector tests stayed green.
- T035: `NodePin.Role` (default `Argument`); the generator reads `[Pin(Role = ...)]` on data pins and emits `Role = ...` in the pin
  initializer, merged with `UnconnectedValue`. `CallMethodNode` sets `Target`, `Result`, `OutParam` (input and output pin of each
  `out`/`ref` parameter) and `Exception`; the output pins `out.data.<parameterName>` follow the return values, and the translator
  assigns each from the variable passed to its parameter after the call. `Node.CanSetPure` is one rule over the output roles; the
  overrides of `CallMethodNode`, `ConstructorNode`, `TernaryNode` and `ExplicitCastNode` are gone (`*REMOVED*` lines) and those kinds
  mark their value pin `Result`. `AwaitNode` keeps its override: its result pin comes and goes with the task type, so a role rule would
  stop an await of a plain `Task` from loading pure. `ReturnValuePins` and `ReturnTypes` of a call now list the `Result` pins only.
- Deviation: the out/ref check in the translator (NPT008) reads the input pin role for calls; constructors keep the parameter pass
  type, and a constructor gets no output pins for its `out`/`ref` arguments (the spec names calls).
- Decision: no built-in convention kind needs a role, because giving `Result` to a pin of an always-pure or exec-driven kind (`Default`,
  `ForLoop`) would let the UI offer a purity toggle that does nothing. The generator's role support is covered by generator tests.
- Goldens: no C# golden and no fixture or sample graph changed (none calls a method with an `out` or `ref` parameter); SC-002 holds.
  `NotificationMap.golden.json` gained `"Role": []` for the six pin classes (the new property raises no notification);
  `CallMethodPurityTests.VoidCallWithOnlyAnOutArgumentCannotBePure` became `...CanBePure` (US2 scenario 5).
- Red/green T035: `PinRoleTests` and `OutRefPinTests` 9 of 12 red on assertions (roles all `Argument`, no output pins, no assignment)
  with `NodePin.Role` declared and nothing else; generator tests 2 of 8 red (the role in the initializer); green after: 12 of 12, 155 of
  155 in Analyzers.Tests.
- T036: ADR-0029, its index entry, a section in `docs/guide/graph-format.md`, and two entries under "Unreleased" in the release notes.
- Process slip: one `sed -i` tick of T034 in `tasks.md` (a one-line mechanical edit, diff checked); every other edit used Edit or Write.
- Checkpoint B, evidence (Release, before CI): suite 4490 total, 4457 passed, 33 skipped, 0 failed (UITests 13 min 40 s, Editor.Tests
  1 min 47 s, Cli.Tests 41 s, Catalog.Tests 48 s, Core.Tests 34 s, Analyzers.Tests 2 s). E2E (`NETPRINTS_E2E=1`, `--fail-skips on`,
  display start 180): 53 total, 52 passed, 1 skipped (`GuideScreenshotTests`, not run for explicit filtering), 0 failed. Solution
  build 0 warnings; `dotnet format --verify-no-changes` clean. Trim ratchet 11 (the R5-0 baseline of the phase is 14, so down
  by 3: the A-side drops to 12, then T033 to 11; the second drop the plan expects from the FR-117 factories is not in this batch's numbers: read it from the
  trim job). Still to read from PR #20 CI at this head: the longest CI job, `SearchPerformanceTests` numbers from the perf job, the pack
  spike (see "Pack spike": passed), the Dock check (pin stays 12.1.0.6, see Checkpoint A). T037 ticked after CI went green at 0a37ba60: CI run 38934517919, CLI (Windows) run 38934517781, Release run 38934517593, Docs run 38934517619.

## Pack spike

T026, run on `scripts/pack-local.sh` version `0.1.0-local.20261016141953`, scratch projects outside the repository (the
consumers restore from `local-packages/` plus a scratch feed and build with `-p:ReportAnalyzer=true -v:d`).

- The `NetPrints.Core` package holds `analyzers/dotnet/cs/NetPrints.Authoring.dll` and `NetPrints.Authoring.CodeFixes.dll`.
- Consumer that references only the packed Core, with a `[NodeKind]` node: the compiler command line has one
  `/analyzer:` entry for `NetPrints.Authoring.dll`, the analyzer report lists `PinConventionGenerator` once, one generated
  file `NodePins.Spike.LoopNode.g.cs`, no duplicate hint, no warnings. Pass.
- Scratch package `Fake.Abstractions` that depends on Core with `PrivateAssets="none"` (nuspec: `include="All"`), and a
  consumer that references only it: same result, one analyzer entry, one report line, one generated file. Pass.
- Control, `Fake.Control` with the default `PrivateAssets` (nuspec: `exclude="Build,Analyzers"`): the analyzers also reach
  the consumer, exactly once. So with this SDK (10.0.400) and NuGet the analyzers of a transitive package dependency are not
  held back by that exclude; the `PrivateAssets="none"` declaration is not what makes it work, and it is harmless. Decision for
  the coordinator: T106 keeps the template with the single Abstractions reference; the fallback (the template references Core
  directly) is not needed.

## Publish gate

T073, local evidence at the C2e head (Release). From here a public shape changes only under an NPXE id or with a recorded `*REMOVED*` entry.

- XML docs: CS1591 is `error` in `.editorconfig` and the Release build has 0 warnings, so every public member of the six libraries has a doc comment.
  Documented members in the packed XML files: Core 1510, Reflection 170, Catalog 429, Serialization 560, Extensibility.Abstractions 633, Extensibility 175 (3477).
- API files: `RS0016`/`RS0017` run as errors through `TreatWarningsAsErrors` on every project with `NetPrintsTrackPublicApi`, so the `PublicAPI.*.txt` files hold
  every public member: Core 1607 lines, Reflection 199, Catalog 639, Serialization 952, Abstractions 938, Extensibility 171.
  `ExtensionApiVersionTests` hash unchanged (no API file changed in C2e).
- Settling surfaces: `ExperimentalApiTests` passes (every symbol carries the id of each experimental type it mentions). Ids in use: NPXE0001 to NPXE0009, NPXE0011 to NPXE0013;
  NPXE0010 was never allocated.
- Package validation: `dotnet pack NetPrints.slnx -c Release` passes for all six libraries; Core, Reflection and Catalog compare with 0.2.0.
- `NetPrints.TestExtension` builds from the packed packages alone: `scripts/verify-packages.sh` step 5 (local run at `0.4.0-local.20261017010000`, all checks passed).
  CI at 70c75b75, including the `packages` job: CI run 38993704432, CLI (Windows) 38993704463, Release 38993704427, Docs 38993704425; all green.

## NuGet spike

(T158)

## Checkpoint reports

(T016, T037, T057, T076, T098, T110, T126, T133, T170, T169; the SC table is T152 in PR-P)

## Batch C1a (T040-T042)

- T040: `src/NetPrints.Extensibility.Abstractions/` holds `INetPrintsExtension`, `IExtensionBuilder`, `ExtensionApi`,
  `ExtensionManifest`, `ExtensionManifestException`, `ExtensionDiagnosticCodes`, the node library contracts (`INodeLibrary`,
  `NodeKindDescriptor`, `NodeSuggestion`, `GraphKinds`, `NodeGraphKinds`), `ExtensionSettingsDescriptor` and `ISettingsStore`,
  and the host channel contracts including `InMemoryHostChannel`; `git mv` kept the history, namespaces and no forwards. The
  host keeps the loader, load contexts, registry, built-ins, `NetPrintsSettings`, `JsonFileSettingsStore` and `NullHostChannel`.
  The `PublicAPI.Unshipped.txt` files were split by declaring type; the 12 fixtures and `TestExtension` reference Abstractions only.
- Decision: `ExtensionSettingsDescriptor<T>.Validator` is `Func<T, IReadOnlyList<string>>` instead of returning
  `ValidateOptionsResult`, because `Microsoft.Extensions.Options` is not in the allowed Abstractions references. `NetPrintsSettings`
  and `EditorSettings` adapt their generated validators with a private `Validate`. Experimental and unreleased, so no break.
- Decision: `InMemoryHostChannel` and `ExtensionDiagnosticCodes` moved too (the fixtures build channel pairs; the manifest
  exception throws the codes). Abstractions therefore still sees `System.Reactive` through Serialization; the test pins
  `System.Runtime.Loader`, `Microsoft.Extensions.Options` and the host assembly as absent, not Rx.
- Decision: the Core reference of Abstractions has `PrivateAssets="none"` already; the pack spike (S2a, later) verifies delivery.
- Red/green T040: `AbstractionsReferenceTests` 4 of 4 red on assertions (project file missing, assembly name still
  `NetPrints.Extensibility`, host not referencing Abstractions, fixtures not referencing it); 4 of 4 green after the split.
  `PublicApiTrackingTests`, `AssemblyReferenceGateTests` and the `SourceHygieneTests` path list gained the new project; the
  `cli-windows` workflow path filters include it.
- T041: `GraphPoint` is in `src/NetPrints.Core/Graph/` (`NetPrints.Graph`), 19 lines in Core's `PublicAPI.Unshipped.txt`; the editor
  files gained `using NetPrints.Graph;` and the two editor test projects a global `<Using>`. Red/green: `GraphPointTests` was red
  as a compile failure (the type was not in Core, so the arithmetic test could not bind); green after the move, 2 of 2.
- T042: ADR-0030 with the split, the references, the Options decision, analyzer delivery and an empty Facet section for T072; the
  index no longer reserves 0030.

## Batch C1b (T043-T046)

- T043: `LocalizedText(Key, DefaultText)` in `NetPrints.Core` (`Localization/`), `Resolve(ResourceManager | IEnumerable<ResourceManager>, culture)`;
  the framework's `ResourceManager` walks culture, parent, neutral; then default text, then raw key. `CommandDescriptor.Label` and
  `PanelDescriptor.Title` are `LocalizedText`; a literal takes `Command.<id>.Label` / `Panel.<id>.Title` (`ConventionKeys`), an explicit
  text keeps its key; `DisplayLabel`/`DisplayTitle` resolve through `Strings`. `MenuPlacement.Path` stays the menu id, `Label` is
  `Menu.<id>`; `MenuViewModel.Title` is the shown header and the shell binds it.
- T044: `NodeStrings.resx` (72 keys: 23 kind titles, 46 shipped pins, `Category.netprints`) and `Strings.resx` (6 menus, 6 panels,
  54 commands, category). Per-pin id `Graph.Pin.<In|Out>.<Exec|Data|Type>.<key>` on a wrapper panel around each pin (the existing
  `Graph.Pin` id and name stay on the pin itself); the id comes from a style setter because E4 allows only `x:Static` attributes.
  `AddStringResources` (NPXE0011) reaches `ExtensionRegistry.StringResources`.
- T045: `ResourceKeyAnalyzer` (NPA040 to NPA042) and `ResxFile` in `NetPrints.Authoring/Localization/`; the pin collection logic of the key
  tracking analyzer moved to `DeclaredPin` so both share it. NPA040 is an error in `src/**.cs` through `.editorconfig`; Core lists
  `NodeStrings.resx` as an additional file. Checked by deleting `Node.throw.Title`: `ThrowNode.cs` reports NPA040. 0 hits in `src/`.
- T046: `NetPrintsSettings.Language` with `[CultureName]` (empty or a predefined culture); `EditorApp.ApplyLanguageSetting` at start sets the UI
  culture and the Semi and Ursa locales; ADR-0027 and the ADR-0023 amendment.
- Defaults taken, each still pending the owner: pin labels with no resource show the raw key (T043); the built-in node category stays the
  single "NetPrints" one (T044); a language change applies after a restart, no live switch (T046).
- Decision: Core's `NodeStrings` is internal behind `CoreStrings.Resources`: a public generated class puts one `PublicAPI` line per key
  (and RS0041 oblivious-type errors) into Core's API files. Deviation from "PublicClass=true" for Core only; `Strings` in the editor is public.
- Decision: Semi and Ursa expose no supported-locale list (`TryGetLocaleResource` is private), so the editor sets the app culture and, when
  the theme reads back another locale, sets `en-US`. Semi has 16 locales and Ursa 7 at the pinned versions.
- Deviation: the display of node titles, pin labels, category names and extension resources is not switched to the resources yet (keys exist,
  are checked, and `AddStringResources` collects); command labels, panel titles and menu headers resolve through `Strings`. ADR-0027 says so.
- Deviation: NPA042 only looks at `Node.<kind>.*` keys of kinds with `[NodeKind]` in the project; `Command.*`, `Menu.*`, `Panel.*`, `Category.*`
  and `Settings.*` keys are declared in code the analyzer does not evaluate.
- Red/green. T043: `LocalizedTextTests` 4 of 8 red on assertions with the lookup stubbed out, 8 of 8 green. T044: `NodeStringKeyTests` 2 of 2 red
  (resx without the keys), `ConventionKeyTests` 3 of 6 red, then green after the resx were filled from the failure lists; `PinAutomationIdTests`
  red on the pin lookup without the XAML wrapper, green with it. `StringResourcesTests` (2) was written after the code. T045: 8 of 15 red on assertions
  with no analyzer, 15 of 15 green. T046: `LanguageSettingTests` 4 of 4 red with a no-op `ApplyLanguageSetting`, 5 of 5 green (one case added after a
  run showed Ursa reading back `zh-CN` for an unsupported culture); `SettingsValidationTests` 2 of 14 red (invalid language accepted), 14 of 14 green.

## Batch C1c (T047-T050)

- T047: `CSharpLanguageVersion` (readonly record struct, `MaxKnown` 14.0, `TryParse`, `IsKeyword`, ordering), `CSharpTarget` (`Version`, `Specified`,
  `TargetFrameworks`, `Check`, `Unconstrained`, `Resolve`) and `CSharpFrameworkInput` in `NetPrints.Core/Projects/`; `ProjectSnapshot.CSharpTarget` is a
  non-positional `init` member (default `Unconstrained`), `Project.CSharpTarget` reads it. NPW006 to NPW008 are `ProjectMessage` constants and
  `Resolve` returns the messages, so the project system only adds them.
- Decision: `Resolve` takes one `CSharpFrameworkInput` per target framework (`LangVersion`, `MaxSupportedLangVersion`); the writers that cannot evaluate
  MSBuild in process (the SDK target) send those same inputs and the generator calls `Resolve`, so all three writers run one function.
- Decision: an empty `LangVersion` resolves like `default` without a message; the SDK sets `LangVersion` to the framework default when the project sets
  none (`14.0` on net10.0), so NPW006 fires only for a keyword the project wrote. A missing `MaxSupportedLangVersion` reads as `MaxKnown`.
- Decision: "common capabilities" of a multi-targeted project is the minimum version only for now; `CSharpFeature` and the capability intersection
  land with SUG4 (F).
- Red/green T047: `CSharpTargetTests` red as a compile failure (types absent), 37 of 37 green. A compile failure is not an assertion-level red (CD-26; Batch C-F7).
- T048: `MsBuildProjectSystem.ReadFrameworkInputs` evaluates every target framework of a multi-targeted project (the first from the existing evaluation, the
  others in throwaway collections) and sets `ProjectSnapshot.CSharpTarget`; fixtures in `tests/Fixtures/CSharpTarget/` (`NoLangVersion`, `Latest`,
  `Preview`, `Pinned10`, `Unknown16`, `MultiTarget` = net10.0;net8.0), linked into the Core and Cli test outputs and copied to a temp folder per test
  (the repository's `Directory.Build.props` sets `LangVersion`). Guide rows NPW006 to NPW008. Red/green: `CSharpTargetWorkspaceTests` 6 of 6 red on
  assertions (target stayed `Unconstrained`), 6 of 6 green (one expectation fixed: `Specified` is `14.0` for a project with no `LangVersion`).
- T049: `GenerateRequest.Target` (non-positional `init`), a `framework=<moniker>|<LangVersion>|<MaxSupportedLangVersion>` request line parsed by
  `GenerateRequestFile` (none means `Unconstrained`), `GenerateRequestFactory` copies the snapshot's target, `GraphCodeGenerator` puts it on its
  throwaway snapshot. `NetPrints.Sdk.targets`: `_NetPrintsCollectFrameworks` calls the project once per target framework through the `MSBuild` task and
  `_NetPrintsWriteRequest` writes one line each, so every inner build of a multi-targeted project writes the same request. Contract line added to
  project-system.md section 2. `CSharpTargetWriterIdentityTests` (6 fixtures x editor path, `netprints regen`, `netprints build` with the real SDK
  target) plus two request-parse tests: red as a compile failure (`GenerateRequest.Target` absent), 8 of 8 green. The identity assertions held from
  the start because nothing yet reads the version; the request assertions are what pin the plumbing.
- Deviation: the editor's save path renders through `ClassTranslator`, not through a request; it reaches the target through the class's `Project`
  (T050). The test renders like the editor does (mapper, `ClassTranslator`, `RenderFile`) over the project the workspace loaded.
- T050: `IExecutionTranslationContext.Target` is a default-implemented member (`Class?.Project ?? Graph.Project`, else `Unconstrained`);
  `TranslatorUtil.ObjectToLiteral(obj, type, target)` is the new overload (the two-argument one is `Unconstrained`); `ExecutionGraphTranslator` passes
  the context's target. Decision: the one version-dependent rule is the escape character, written `\e` from C# 13 and `\u001b` before (Roslyn's
  `FormatLiteral` emits `\u001b` for both, valid everywhere; the rule makes the version visible in the output and is read one escape at a time so an
  escaped backslash before `u001b` is untouched). Output for a project on C# 13 or newer changes only for an ESC character; no golden holds one.
  Red/green: `TranslatorTargetTests` 7; the class-level case red on assertion while `Target` read `Graph.Project` only (the method graph had no
  project), green after reading the class's project first.
- Suite (Release, solution-wide, `--ignore-exit-code 8`): Core.Tests 983, Cli.Tests, Catalog.Tests, Editor.Tests, Editor.UITests, Analyzers.Tests and
  Desktop.E2ETests passed. One failure on the first run, `NotificationMapTests`: `Project.Snapshot` now also notifies `CSharpTarget`; the golden map gained
  that entry (intended). Core.Tests then 983 of 983.
- Not owned by C1c: the display of node titles, pin labels, category names and extension resources still does not use the resx (see C1b deviation);
  no task in T047 to T050 covers it, so it stays open for the batch that owns the editor's node views.

## Batch C1d (T051-T052, T171 added)

- T051: `INodeSuggestionProvider`, `NodeSearchContext`, `NodeSearchEntry` and `INodeCreationContext` are in Abstractions (`NetPrints.Extensibility.Nodes`,
  NPXE0006); `CSharpFeature`, `CSharpFeatureId`, `Availability` and `NodeSearchEntry.Requires` are NPXE0007 (`Check(target, typeExists)` compares the
  C# version and, when asked, the required types). `IExtensionBuilder.AddSuggestionProvider`; `RegistryBuilder` drops a provider whose id does not start
  with `<manifest id>.` or `<manifest id>/` (NPX013, new `ExtensionDiagnosticCodes.IdPrefix`) or repeats an id (NPX004); `ExtensionRegistry.SuggestionProviders`.
  The editor's own providers keep an internal `INodeSearchProvider : INodeSuggestionProvider` (owner kind); its entries derive from `EditorSearchEntry`,
  which receives the editor's `NodeCreationContext` (narrowed from the public `INodeCreationContext` in one place). The registry adds the
  extensions' providers after the editor's, built once for each registry.
- AB-10 done: `INodeHintSource`/`NodeHint` in Reflection; `CatalogTypeCatalog` implements it from `[NetPrintsNode]`/`[NetPrintsType]`, the composite and
  memoized providers forward. `NodeSearchHints.Apply` gives a method or type entry its display name as text and its keywords; the category is not
  changed (it would move rows between the goldens' groups). Only entries of cataloged members are touched.
- Decision: `Availability` is a record (`IsAvailable`, `Reason`, `Fixes` as quick-fix ids) with `Available`/`Unavailable(reason, fixes)`; no filtering of
  entries by `Requires` yet (that is sub-phase F).
- T052: contexts in Abstractions, `NetPrints.Extensibility.Editor` (NPXE0005): `ICommandContext`, `IPanelContext`, `IStartPageContext`,
  `IStatusBarContext`, `ISettingsPageContext`, `IDialogContext`, `IProjectSession`, `IGraphEditor`, `IEditorShell`, `IDialogService`, `IEditorOutput`,
  `IEditorUndo`, `ICommandHandler`, `CommandScope`, `CommandSelection`. The editor's `CommandContext` implements `ICommandContext` explicitly over its
  view models (facades in `Contributions/Facades/`, built only when an extension asks); built-in handlers implement `IEditorCommandHandler`, which maps
  the typed members to their `CommandContext` methods once. `PanelDescriptor.CreateViewModel` takes `IPanelContext`, `DashboardTileDescriptor.CreateViewModel`
  takes `IStartPageContext`; `StartPageServices` and the `NoServices` doubles are gone (`StartPageContext`, `PanelFactoryContext`). `ContextMenuItemDescriptor.AppliesTo`
  with `CommandInvoker.ContextMenuCommands(target, context)`: a rejected item is left out, not disabled.
- Deviation: `CommandSelection.Nodes` stays `IReadOnlyList<object>` (the editor's view models); the model nodes are `IGraphEditor.SelectedNodes`.
- Deviation: `IProjectSession.Services` and `ICommandContext.Services` are an empty provider until the per-extension providers of C2 (research R15).
- Deviation: the status bar, settings page and dialog contexts exist as interfaces and one host class (`EditorContexts`); their descriptors come with C2.
- Not owned by T051 or T052: the display of node titles, pin labels, category names and extension resources through the resx. Added as T171 to batch C1f (FR-027).
- Red/green. T051: `SuggestionProviderTests` (4), `CSharpFeatureTests` (4) and `ExtensionProviderTests` (4) red as compile failures (types absent), green;
  `NodeHintTests` (4) written with the code (its types did not exist either), green. These reds were compile failures, not assertion reds (CD-26; Batch C-F7). T052: `TypedContextTests` (4) red as a compile failure, green after
  one failure on an unloaded reflection provider (the test now waits for `ReloadAsync`, as a project open does). `DiagnosticsGuideTests` failed once on the
  missing NPX013 row; the row was added.
- Suite (Release, solution-wide, `--ignore-exit-code 8`): 4636 total, 4602 passed, 33 skipped, 1 failed (the guide row above, fixed and re-run green).

## Batch C1e (T053-T054)

- T053: `EditorServices(host, configure)` builds a `ServiceCollection`: `AddEditorHost(host)` (hand-registered host instances, `IScheduler`, `Func<IMessenger>`,
  `IProjectSystem`, `ProjectPersistence`) then the generated `AddEditorServices()`, then the caller's `configure`, and a provider with `ValidateScopes`. Annotated:
  `AvaloniaUiDispatcher`, `ProcessLauncher`, `WindowService` (also as `IWindowService`), clipboard, dialogs and file picker (static `Create(WindowService)`
  factories), `ReflectionHost`, `CodeAnalysisHost`, `RunStateTracker`, `PersistenceBinding` (`Bind`), `ContributionRegistry` (`Create`, frozen), `EditorContext`
  (`Create`, optional members from `GetService`), `ShellHost` (`Create`). `EditorContext` stays the facade. `EditorComposition` adds the production-only
  services (backup options, recent projects, window state, state store, locations) in `configure`; `TestComposition` uses `Replace` for the three doubles.
  The provider disposes the shell, binding, analysis host and run state. `CreateShellWindow` is once only (the shell is a singleton): a second call throws.
- Settings validators at composition: the composition reads the `netprints` and `netprints.editor` sections once, so an invalid section logs 2006 and keeps
  its defaults before the window exists (US1 scenario 5).
- Generator fix (found by T053): `[Singleton<T>]` on a `record` was silently ignored (the syntax predicate matched `ClassDeclarationSyntax` only); it now
  matches records too, with a generator test. `EditorContext` is a record.
- T054: `ProjectSessionScope` (extracted from `ProjectLoader`'s `BackupScope`) builds one container per session from the context's host services
  (`AddSessionHost`) and creates a scope in it; `SessionIdentity` (`[SessionScoped]`, set once) carries the project path and the warn callback. Session-scoped:
  `BackupStore`, `BackupService`, `RecoveryService`, `ProjectTemplateService`. `SessionBackups` stays created by the scope (it needs the session view model, which
  exists after recovery). Disposal is idempotent: backups, scope, container. The loader creates the scope when the project is loaded (recovery needs it) and
  hands it to `SetProjectAsync`; a load that fails disposes it.
- Deviation: templates are session-scoped as written, but New project runs with no project open, so that flow creates a short-lived scope with no project path;
  `BackupStore`/`BackupService` throw if resolved there.
- Deviation: `CompositionResolveAllTests` resolves the root's singletons except `ShellHost` (a `ShellWindow` needs the Avalonia platform, which `Editor.Tests`
  lacks; the UI tests build it) and checks that scoped ones are not available at the root.
- M1 (Release, local): composition alone (provider build and first resolves, no window) cold 30 ms, warm 0.2 ms; the `ComposingTheEditorStaysWithinTheBudget`
  perf test asserts the warm median at most 150 ms (ADR-0031). `ShellApp.Start` (M1 as in A3): cold 818 ms (A3: 756), warm median 183 ms (A3: 213, runs
  133 to 235 ms), 16.55 MB (A3: 16.06 MB, +3%). Run to run noise is about 30%, so the +10% gate waits for the perf job.
- Red/green: `ARecordIsRegisteredLikeAClass` red then green. `AnInvalidSettingsSectionIsReportedWhenTheEditorComposes` red (assertion, with the composition
  read removed) then green. `TheDoublesReplaceTheGeneratedRegistrations` and `CompositionResolveAllTests` were written before the code and did not compile
  until it existed; they were not run red here. Batch C-F7 ran both red on assertions by breaking the code temporarily (CD-26, below).
- Suite (Release, solution-wide, `--ignore-exit-code 8`): 4643 total, 4610 passed, 33 skipped, 0 failed. No E2E run.

## Batch C1f (T171, T055, T056, Checkpoint C1)

- T171: `NodeTexts` (Editor, `Graph/`) resolves node titles, pin labels, search category headers and node-suggestion names as `LocalizedText` over
  the extensions' string resources and then `CoreStrings.Resources`, for the UI culture. The extensions come first so that a language pack can translate a
  built-in key; the neutral Core text answers when no pack has the key. `Node.TitleIsFixed` (new, virtual, false by default; true for await, classReturn,
  forLoop, ifElse, return, reroute, throw, typeOf, typeReturn) marks a node whose text is the same for every instance: only those show `Node.<kind>.Title`;
  a title built from content (call method, default, literal, variable, entry names, explicit cast) keeps its text. The kind id comes from the registry's node
  converter for the node type (`NodeConverters.FindByNodeType`), so extension kinds work. A pin shows `Node.<kind>.Pin.<pinRef>` for its name (`PinKeys.For`)
  and keeps what `Pin.ToString()` adds after the name (a data pin's type); with no resource it shows its name (the owner default "raw key"). A category header
  is `Category.<name in lower case, spaces as hyphens>` (so `Category.netprints`; the group key stays the name); a node suggestion's display name is the key
  of its translation (the `NodeSuggestion` record is unchanged, no API change). `NPXE0011` opt-in added to Editor and Editor.Tests; ADR-0027 updated.
- Goldens: none changed. `NodeStrings.resx` neutral values of await, classReturn, throw and typeReturn changed to the text the nodes already showed
  ("Await Node", "Class Return Node", "Throw Exception", "Type Return Node"), because the default text must be the current text; `FixedNodeTitleTests` pins it.
  The resx values of kinds with content-built titles stay as they were (unused as titles).
- Deviation: built-in node kinds in the search rows are not translated (their display names are not keys); the search header and extension suggestions are.
- T055: `ClassContextFactory` and `GraphScopeFactory` (`[ScopeFactory]` partial classes, `[Singleton]`, in `Hosting/`) replace the `new ClassContext` /
  `new ClassEditorServices` calls: `GraphScopeFactory.CreateServices(undo, messenger, projectClasses)` and `ClassContextFactory.CreateContext(cls, scope)` are
  generated; `Create`/`CreateScope` compose them. `ClassContext` has a second constructor over a `ClassEditorServices` (the one the generator picks); the old one
  stays for tests and calls the factory. `ProjectSessionViewModel` takes the factory (optional; `ClassContextFactory.For(context)` for hosts with no container);
  `ProjectSessionScope.ClassContexts` resolves it from the session container and the loader passes it. Nothing is resolved per row, node or pin: the
  scope is built once per class. `ICommandContext.Services` and `IProjectSession.Services` stay the empty provider: they belong to the per-extension providers of C2
  (T062 and following, research R15), not to T055 or T056.
- M2 (gate, local Release, the T006 test with counting fakes, now through the generated factory): messenger calls 0, project-classes calls 0 (0.000 per pin);
  35,766 to 35,829 bytes per node against the baseline 35,080 (+2.0% to +2.1%, gate +10%, asserted in the test); median time 25.9 to 26.8 ms against 14.8 ms:
  the median picks run 3, which is still JIT warm-up (runs 1 to 3: 88, 44, 27 ms; runs 4 and 5: 13.7 and 13.5 ms, the baseline's steady 13 ms), and
  the factory is not on the timed path (it runs before the stopwatch). The strict time gate waits for the perf job.
- T056: API shape review of Core, Serialization, Abstractions and Extensibility against contracts/extension-api.md, by symbol: `CommandScope` and
  `CommandSelection` (NPXE0005 surface, T052) carried no `[Experimental]`; both now do (API file lines prefixed `[NPXE0005]`). Every other type of the
  Editor/Nodes/Settings/Hosting namespaces of Abstractions is either marked or part of the stable surface. Finding left as is: `CommandSelection.Nodes` is
  `IReadOnlyList<object>` (the editor's view models), the model nodes are `IGraphEditor.SelectedNodes` (C1d deviation). The "API stability" section of
  `docs/guide/extensions.md` lists NPXE0005, 0006, 0007 and 0011 and the stable-from-0.4.0 surface; ids for later surfaces are added with their API.
  `ExperimentalOptInTests` (Analyzers.Tests, references Abstractions): for NPXE0003, 0005, 0006, 0007 and 0011 a source using the API fails with that id, compiles with
  that opt-in alone, and another id does not help.
- Red/green. `FixedNodeTitleTests`: 5 of 9 red on assertions (resx text differs from the node's text; explicitCast is content-built and left the list after
  that run), 8 of 8 green; the `TitleIsFixed` case was written with the property already present at `false`, so it was not run as a separate red.
  `NodeDisplayTextTests` 5 of 9 red on assertions, 9 of 9 green. T055: the M2 gates and the factory were written together (the factory did not exist before),
  so no separate red; `CompositionResolveAllTests` resolves the new singletons in both containers. `ExperimentalOptInTests` 11 of 11 green at the first run
  (it characterises the existing attribute behaviour), not red.
- Suite (Release, solution-wide, `--ignore-exit-code 8`): 4672 total, 4639 passed, 33 skipped, 0 failed (UITests 15 min 02 s, Editor.Tests 2 min 03 s,
  Cli.Tests 47 s, Catalog.Tests 52 s, Core.Tests 40 s, Analyzers.Tests 3 s). E2E (`NETPRINTS_E2E=1`, `--fail-skips on`, display start 200): 53 total, 52 passed,
  1 skipped (`GuideScreenshotTests`, not run for explicit filtering), 0 failed, 2 min 22 s. Solution build 0 warnings; `dotnet format --verify-no-changes` clean.
- Checkpoint C1, gate items. SC-005: M1 local `ShellApp.Start` cold 816 ms (A3 baseline 756, C1e 818), warm median 182 ms (A3 213), 16.56 MB (A3 16.06 MB, +3.1%);
  composition alone 0.2 ms warm (budget 150 ms). M2 as above. The perf job (PR #20 run 38973466744, job 117217142615, green at 18824b36) prints no
  live output, but its `test-results-perf` TRX artifact carries the M1 and M2 numbers (run 38899627936 onwards; corrected in Batch C-F2, CD-1); the
  +10% comparison here rested on the local figures, and the perf job passed its strict budgets. SC-010:
  `CSharpTargetWriterIdentityTests` 8 of 8 (editor path, `netprints regen`, `netprints build`, 6 fixtures). API shape review: T056 above. Longest CI job at
  18824b36: Desktop E2E (Linux, Xvfb) 13 min 34 s; longest test leg Editor UI 3/4, 8 min 57 s. Dock check: pin stays 12.1.0.6 (no release since v12.1.0.9 holds
  Dock#1191; not rechecked in this batch). Trim ratchet: `eng/trim-warnings.txt` is 11 (B6); not re-measured locally, the trim job at 18824b36 passed. Docs:
  ADR-0027 (display switched), ADR-0030 (split, C1d), ADR-0023 (language amendment, C1b), the extension guide's "API stability"; the diagnostics guide has the
  NPX013 and NPW006 to NPW008 rows (C1c, C1d). T057 ticked: CI green at 7a426036 (CI run 38977152765, CLI (Windows) 38977152766, Release 38977152776, Docs 38977152809).

## Batch C2a (T060-T062, T057 ticked)

- T057: ticked on the coordinator's word, CI green at 7a426036 (see Batch C1f).
- T060: `IExtensionBuilder.Editor` returns `IEditorContributions` (NPXE0005; commands, panels, dashboard tiles, context-menu items, tooltip and go-to
  providers, `AddViewResources`; status bar, dialog and settings page members come with their descriptors). `ExtensionBuilder` hands out a
  `BufferedEditorContributions`: it checks and buffers each call (null guard, `InvalidOperationException` once `Register` returned) and the loader
  replays the buffer to `ExtensionLoaderOptions.EditorContributions(manifest)` only after `Register` returned normally, so a throwing extension
  contributes nothing to the editor either. With no factory (CLI, SDK generator, build) the replay target is the internal `NullEditorContributions`.
  The descriptor types (`CommandDescriptor`, `PanelDescriptor`, `DashboardTileDescriptor`, `ContextMenuItemDescriptor`, `ITooltipProvider`, `IGoToProvider`,
  `GoToItem`, `TooltipContent/Target`, `MenuPlacement`, `PanelDock`, `ContextMenuTarget`, `CommandGesture/Key/Modifiers`, `ConventionKeys`) moved to
  `NetPrints.Extensibility.Editor` in Abstractions, with `DocumentId` and `NavigationTarget` (a `GoToItem` needs them). All are NPXE0005. The
  editor projects already import that namespace, so only the `DocumentId` aliases of test files changed. `DisplayLabel` and `DisplayTitle` stay in the
  editor as C# 14 extension properties (`DescriptorDisplay`), because they read the editor's resx.
- Decision: `ICommandContext.Services` and `IProjectSession.Services` are not wired here: the per-extension providers are T063 (batch C2b), which owns
  `ExtensionRegistry` providers and the accessor behind `IProjectSession.Services`. They stay the empty provider until then.
- T061: the built-in editor registers through the facet as `netprints.editor` (`BuiltInContributions.OwnerId`). `ContributionRegistry.For(owner)` is the
  facet the registry hands out and records the owner of each id (`OwnerOf`, the owner of an issue). `IContributionRegistry` lost the six `Add*` members
  (the concrete registry keeps them as the implementation behind the facet) and gained `OwnerOf` and `ViewResources`; project templates stay a direct
  registry call (the builder-level `AddProjectTemplate` is not in this batch). The built-in files take `IEditorContributions` (parameter still named
  `registry`, to keep the diff small). The 12 application DataTemplates moved to `BuiltInViews.axaml` (root `DataTemplates`), declared with
  `AddViewResources("avares://NetPrints.Editor/BuiltInViews.axaml")` and added to the application by `ViewResourceLoader` when `ShellHost` is created
  (once for each application and URI). The loader accepts a `DataTemplates`, `ResourceDictionary` or `Styles` document, because a resource
  dictionary cannot hold templates in Avalonia.
- Deviation: `netprints.editor` is not an `INetPrintsExtension` loaded by the extension loader; the registry calls the same registration code with its
  own facet (the editor registry is built before and apart from the loader until the dynamic groups of D1).
- Deviation: the moved types keep the `NetPrints.Extensibility.Editor` namespace of the contexts, not `NetPrints.Editor.*`, so extension authors need one
  `using`; the editor projects import it globally.
- T062: `IconGlyph` carries an optional `Geometry`; `IconRegistry` takes a lookup (`IconRegistry.ForApplication` reads `Icon.<id>` from the application
  resources, where `ViewResourceLoader` merged the extension's dictionary). `IconPresenter` shows a `PathIcon` for a geometry glyph and the Material glyph
  otherwise. An unknown id draws the fallback glyph, logs 1080 once and records an `IconIssue` with the new code `NPX010`
  (`ExtensionDiagnosticCodes.UnknownReference`, row added to the diagnostics guide).
- Red/green. T060: `EditorFacetTests` (Cli.Tests, 2) and the Core.Tests `EditorFacetTests` (4) were written with the types in place and did not compile
  without `IExtensionBuilder.Editor`; no assertion-level red run. T061: the P3a snapshot (`Contributions/Goldens/builtin-registry.txt`) was generated
  from the registry before the refactor (it is unchanged after it); `OwnerOf`, `For` and `ViewResources` tests were red as compile failures.
  T062: `ExtensionIconTests` (3) red as compile failures, green. C2a had no assertion-level red for T060 to T062 (CD-26; Batch C-F7). From C2b on the runs are assertion-red.
- Suite (Release, solution-wide, `--ignore-exit-code 8`): 4685 total, 33 skipped, 13 failed at the first run: `CommandKeyTests.NothingSwitchesOverTheKeys` (the source path
  moved) and 12 UITests (`ShellAdapterTests`, `DockLayoutRoundTripTests`: `ShellRig` composes no `ShellHost`, so the built-in templates were not
  added; it now applies the registry's view resources). Both classes re-run green (101 and 34 tests); the whole suite was not run again. No E2E run.

## Batch C2b (T063-T065, plus the CI fix before it)

- CI fix (6fcb2ca8): the trim ratchet had risen to 12 because `ViewResourceLoader` called `AvaloniaXamlLoader.Load(Uri)`. `BuiltInViews` is now a compiled
  class (`x:Class`, `AvaloniaXamlLoader.Load(this)`), and the loader keeps a table of compiled resources by URI. Deviation: a URI that is not compiled
  throws `NotSupportedException`; extension view resources by URI come with the dynamic groups of D1 (no extension declared one until now).
- T063: `IExtensionBuilder.Services` (NPXE0008) is a `ServiceCollection` per extension. `RegistryBuilder.Commit` builds one `ServiceProvider` per extension
  (`ExtensionServiceProviders`) from the filtered registrations plus `HostExports`: `ILoggerFactory`, `ILogger<T>` (an `ExtensionLogger<T>` over the factory),
  `TimeProvider` and `IOptionsMonitor<T>` for the extension's own settings sections (value from `ExtensionLoaderOptions.SettingsStore`, the section default
  without one; `OnChange` never fires). The seam filter removes, with an NPX006 issue and the extension still loaded: registrations of the exports,
  `IConfigureOptions<>`, `IPostConfigureOptions<>` and `IValidateOptions<>` over a type the extension does not own, and any service type that is a
  `NetPrints.*` type (or a closed generic over one) outside the extension's own assembly. Own = declared in the assembly of the extension type.
  `ExtensionRegistry.ServicesOf(id)` gives the provider (an empty one for an unknown id) and `OpenSession(id)` a tracked scope. Disposal order: open sessions,
  the owned contributions, then the providers, each item wrapped so one throwing `Dispose` is logged and the rest run (the contributions go before the
  providers, because they may use services while disposing; research R15 says sessions then providers).
- T063 editor side: `CommandContext.Owner` (set by `CommandInvoker` from `IContributionRegistry.OwnerOf`) selects the extension whose provider is
  `ICommandContext.Services`; `ProjectSessionViewModel.ExtensionServices(id)` opens one session scope per extension on first use and disposes them with the
  session, and `IProjectSession.Services` returns it. Known gap: a registry swap (`LoadForProjectAsync`) disposes the scopes of an open session without the
  session knowing; it does occur on the rollback of a failed project switch, and Batch C-F5 (CD-11) closes it: the session forgets its cached scopes on `RegistryChanged`. `Desktop` passes its `JsonFileSettingsStore` as `SettingsStore`.
- Decision: the invoker now always copies the context (`with { Owner, Contributions }`), so `TypedContextTests` compares the session instead of the context.
- T064: `DialogDescriptor(Id, Title, CreateViewModel(IDialogContext))`, `IEditorContributions.AddDialog`, and `IDialogService.ShowDialogAsync(id, token)` which
  returns the view model it showed (it holds the dialog's result, so no editor type leaks into the contract). `EditorDialogService` creates the view model with an
  `EditorContexts` and calls the new `IEditorDialogs.ShowViewModelAsync`, implemented by `ExtensionDialog` (a window whose content is the view model, found by
  data template; `DialogCloseBehavior` closes it for an `IDialogCloseSource`). An unknown id throws `ArgumentException`.
- T065: `StatusBarItemDescriptor(Id, Alignment, Order, CreateViewModel, AppliesTo)`, `StatusBarAlignment`, `AddStatusBarItem`. `StatusBarViewModel.Attach/Refresh` build
  the items and keep `LeftItems` and `RightItems` (visible ones, by order); `ShellViewModel` attaches the registry's items with a `ShellStatusBarContext` and
  refreshes on session, document, diagnostics and command-state pulses. The built-in segments (`StatusBarContributions`): errors, warnings and selection on the
  left, build, run and zoom on the right. Errors, warnings, selection and zoom show only when non-empty (zoom when not 100%), run only while running, and
  the build segment is always present with an empty text when idle (as the old `TextBlock`), so the idle bar looks as before (snapshot baselines unchanged).
  The build segment is a `BuildStatusSegmentViewModel` so its template carries `ShellBuildState` through `x:Static` (E4 forbids a bound id). `SurfaceRig`
  now applies the view resources, as the product does.
- Trim ratchet: the new `IOptionsMonitor<T>` registration needs reflection over an unannotated `T`, so the loading chain (`ExtensionLoader.Load` and its helpers,
  `RegistryBuilder.Commit`, `ExtensionServiceProviders.Build`, `ExtensionHost` constructor) is annotated `[RequiresUnreferencedCode]` with one reason constant.
  That absorbed five existing IL2026/IL2062/IL2070 warnings: the count fell from 11 to 9 and `eng/trim-warnings.txt` says 9.
- Red/green. T063: `ExtensionServicesTests` (Core.Tests, 5) and `ExtensionServicesWiringTests` (Editor.Tests) were written against stubs (`HostExports` empty, `ServicesOf`
  an empty provider, `ExtensionServices` an empty provider): 4 of 5 and 2 of 2 failed on assertions or on a missing service, then went green. T064: stub
  `ShowDialogAsync` returning a plain object: `ADialogRegisteredWithAddDialogIsShown...` failed on `Assert.Single` of the shown dialogs and the unknown-id test
  on `Assert.Throws`, then green. T065: stub `Attach/Refresh` and no built-in registration: all 4 new `StatusBarViewModelTests` failed on assertions, then green.
  The facet replay test for status items and dialogs (`EditorFacetTests`) was added with the code (it needed the types to compile).
- Suite (Release, solution-wide, `--ignore-exit-code 8`): 4664 passed, 33 skipped, 4 failed at the first run (3 `XamlHygieneTests`: the Margin ratchet, E4 for a bound id,
  and the tabular check that pointed at the moved build text; `RegistrySurfaceTests.TheStatusBarShowsTheMessageAndTheBuildState`: `SurfaceRig` did not apply the view
  resources). After the fixes: Core.Tests 1010/1010, Editor.Tests 2002/2002, Cli.Tests 289, Catalog.Tests 350, Analyzers.Tests 188, and the UITests classes
  `SnapshotTests`, `ScaledSnapshotTests`, `ThemeSettingTests`, `ShellAdapterTests`, `ContactSheetTests` and `RegistrySurfaceTests` green; the whole UITests project
  (14 minutes) was not run again. No E2E run.

## Batch C2c (T066-T067)

- T066: `ProjectTemplateDescriptor` and `ProjectOutputType` moved to `NetPrints.Extensibility.Editor` in Abstractions (the builder cannot name an editor type); the
  descriptor gained `Files` (`TemplateFile`: path and text, with the `NetPrintsProject`, `NetPrintsRootNamespace` and `NetPrintsProfileId` literals as constants) and
  `IExtensionBuilder.AddProjectTemplate` (NPXE0012: `ExperimentalApiIds.ProjectTemplates`, opt-in added to the projects that use it). Decision: it is on the builder, not
  on the editor facet, because `ExperimentalApiTests` requires a member that mentions NPXE0012 types to carry NPXE0012, and the facet type is NPXE0005. The registry
  keeps the accepted ones (`ExtensionRegistry.ProjectTemplates`; an id without the manifest prefix is NPX013, a duplicate NPX004) and `ProjectTemplateService` lists them after
  the editor registry's.
  `ProjectTemplateService` writes the files after the project file (UTF-8 without BOM, LF); a path that is rooted or leaves the project folder throws
  `InvalidOperationException` and the cleanup of a failed creation removes what was written. `NetPrints.Workspace/Templates/console/Program.netpc.json` is the embedded
  console tree (`BuiltInTemplateTrees.Read`), and `ProgramGraphSeed` is gone.
- Deviation: the project file and `.gitattributes` still come from the project system and the profile (the SDK version and target framework are options, not template
  text), so the trees hold the files next to them: Program.netpc.json for the console template, none for the library template (its `SetOutputType` edit stays).
  The `dotnet new` package and `netprints new` (T106, T130) read `BuiltInTemplateTrees` and add the project file themselves.
- T067: `NodeKindDescriptor.DataVersion` (NPXE0013, default 0), `NodeDocument.KindVersion` (`kindVersion`, ignored when null) set by `DocumentMapper` from
  `NodeDocumentConverterRegistry.DataVersionOf` only above 0, `INodeDataMigration` and `NodeDataMigrator` (Serialization.Migrations: walks the parsed JSON, runs the
  migrations of each versioned kind in `FromVersion` order from the stored version, then stamps the current one), `JsonDocumentFormat` takes the migrator as an optional
  argument, `IExtensionBuilder.AddDocumentMigration` and `ExtensionRegistry.NodeMigrator`. A migration whose kind lacks the `<manifest id>/` prefix is dropped with NPX013.
  The editor and the generator pass the registry's migrator; `netprints format` and `show` read with no extensions, so an extension node stays an unknown node there and
  is written back unchanged. `docs/guide/graph-format.md` documents `kindVersion`. The unknown-member round trip (AB-1, `UnknownMembersEditorRoundTripTests`) is unchanged.
- Red/green. T066: the two tree pins (`TheConsoleTemplateWritesTheTreeP3aWrote`, `TheLibraryTemplateWritesTheTreeP3aWrote`, hashes recorded from the code before the
  refactor) passed before and after; the token, launch-settings and path-escape tests were red on assertions against the stubbed service (`File.Exists` false, no
  exception thrown), then green. T067: against stubs (`DataVersionOf` 0, `Upgrade` returning the document) 4 of 6 `NodeDataMigrationTests` failed on assertions and the NPX013
  test failed on `Assert.Equal` of the issue count, then green.

## Batch C2d (T068-T070)

- T068: `IEditorContributions.AddSettingsPage(SettingsPageDescriptor)` (Id, Title, SectionKey, Order, ParentPageId, CreateViewModel), buffered and replayed like the other facet
  calls; `ContributionRegistry.SettingsPages` accepts a page with a valid id and a non-blank title (duplicate: first wins, reported). Settings metadata in
  `NetPrints.Extensibility.Settings` (NPXE0009): `PathKind`, `[RequiresRestart]`, `[SettingPath]`, `[SettingChoices(providerId)]`. `SettingsSchema.Export(JsonTypeInfo)` runs
  `JsonSchemaExporter` with a transform that writes `description` (from `[Description]`), `x-netprints-restart`, `x-netprints-path` (`file` or `folder`) and
  `x-netprints-choices`; the Settings document (T089) consumes it.
- Deviation: the descriptor and `AddSettingsPage` carry NPXE0005 (the editor contributions id), not NPXE0009, and `CreateViewModel` is not under NPXE0010 (the constant
  was not added). `ExperimentalApiTests.EveryPublicSymbolThatMentionsAnExperimentalTypeIsExperimentalWithTheSameId` requires a symbol to carry the id of every experimental
  type it mentions, and the descriptor mentions `ISettingsPageContext`, `LocalizedText`-keyed facet types and the facet itself, all NPXE0005. NPXE0009 marks only the metadata attributes.
- T069: `ExtensionManifest.Themes` and `ExtensionManifest.UnknownFields` are `init` properties (the positional constructor is unchanged). `themes` with a `netprintsApi` below
  `ThemesSinceApi` (0.4), or not a string array, is NPX001; the loader logs the unknown fields once at information level (event 2013) and ignores them.
- T070: `ExtensionApi.Version` is 0.4 (P3 releases as 0.4.0). The loader accepts the same major, so every discovered manifest and fixture moved from `1.0` to `0.4`
  (13 fixtures, `TestExtension`, the test helpers, the guide). A 1.x manifest is now NPX002. `ExtensionApiVersionTests` hashes the four `PublicAPI.*.txt` files of Abstractions and Extensibility
  and compares the last row of a (version, hash) table to the current version and hash; the next batch that changes those files updates the hash in the last row (or adds a row with the new version).
- Red/green. T068: stubs (`Export` returning an empty object, facet and registry methods that ignore the page): 4 of 4 `SettingsSchemaTests`, `ASettingsPageReachesTheHostFacet` and both
  `SettingsPageRegistrationTests` failed on assertions (a missing property node, an empty call list, an empty collection), then green. T069: the new manifest tests (themes list, NPX001 for an older api or a
  non-array, unknown field list) failed on `Assert.Equal`/`Assert.Throws` against properties that were never filled, then green. T070: with the recorded row at version 0.4 and a placeholder hash, and the code
  still at 1.0, `TheVersionIsTheLastRecordedOne` and `TheApiFilesMatchTheHashRecordedForTheVersion` failed on `Assert.Equal`, then green.
- Suite (Release): Core.Tests 1035/1035, Editor.Tests 2013/2013; solution run 4737 total with the one failure above fixed. `eng/trim-warnings.sh`: 9, unchanged.

## Batch C2e (T071-T073)

- T071: `NetPrints.Serialization`, `NetPrints.Extensibility.Abstractions` and `NetPrints.Extensibility` are packable with `EnablePackageValidation` and a README each
  (`verify-packages.sh` requires `# <id>` as the first line). Core, Reflection and Catalog set `PackageValidationBaselineVersion` 0.2.0, their last published version; Reflection
  and Catalog have no break. `src/NetPrints.Core/CompatibilitySuppressions.xml` was generated (`-p:ApiCompatGenerateSuppressionFile=true`): 13 CP0002 (typed pin getters removed) and 11 CP0009
  (node classes sealed). `PackageValidationTests` cross-checks it: every CP0002 target has a `*REMOVED*` line in `PublicAPI.Unshipped.txt`, every CP0009 target is a sealed type, every entry is a baseline suppression;
  it also pins which projects are packable, the validation flag, the baseline (only the published three) and the README. The other `*REMOVED*` lines (emitter and `TranslationEnvironment` members, overrides) are
  not reported by package validation against 0.2.0, so they need no suppression.
- `scripts/verify-packages.sh` now expects 16 files (9 packages), checks the three new packages' contents and that the Abstractions dependency on Core excludes nothing, then step 5: a scratch consumer
  that references only Abstractions builds with `-v:d` and the compiler line holds `NetPrints.Authoring.dll` exactly once; `tests/NetPrints.TestExtension` builds against the packed packages (its project
  switches to one `PackageReference` when `NetPrintsPackagedExtensionVersion` is set, with `RestoreSources` set to the feed). Deviation: that build runs in the repository tree, so `obj/` of
  TestExtension holds the package-mode assets afterwards; the next normal build restores again (verified).
- `scripts/pack-local.sh` needed no change (it packs the solution, which now holds the new packages); the `packages` job in ci.yml already runs pack (validation runs inside it) and the verify script; only the step name changed.
  `docs/contributing/releasing.md` has the baseline step and the new counts; `docs/api/docfx.json` lists the three new projects.
- T072: the CLI test is `ProjectExtensionBuildTests` (real `netprints build` on a copy of HelloWorld): a project that declares `NetPrintsExtension` to `NetPrints.TestExtension` generates `System.Console.WriteLine(...)` for its
  `netprints.test/Log` node, and a project whose extension folder holds an invalid manifest fails with NPX001. No trust setting exists in the test host. It pins existing behaviour, so it was green on arrival
  (it only went red while the graph JSON was wrong: NPD008 for a string on an `object` pin, then NPT008 when the connection key was missing). The guide, ADR-0030 (Facet), ADR-0020 (Amendment 1) and ADR-0021 (Amendment 3) are written; the guide's package
  instructions replace the project-reference ones, with the contribution table, typed contexts, icons and the NPXE rows 0009, 0012 and 0013.
- T073: see "Publish gate".
- Red/green. T071: `PackageValidationTests` against the unchanged projects failed on assertions (5 of 7: the expected library list, validation flag, baseline null versus 0.2.0, README missing), then green after the project
  edits and the generated suppressions; `verify-packages.sh` against the old feed failed at step 1 (missing Extensibility package), then passed on a fresh pack.
- Suite (Release): 4746 total, 0 failed, 33 skipped; build 0 warnings; `dotnet format --verify-no-changes` clean; `eng/trim-warnings.sh` 9 (baseline 9). No `PublicAPI.*.txt` changed, so the `ExtensionApiVersionTests` hash is the same.

## Build-time budget (ADR-0024, S2a; B-F3)

- `dotnet build NetPrints.slnx -c Release --no-incremental`, warm compile server, 6 runs at the end of B: 24.2, 19.1 and 12.2 s
  while another job loaded the machine (load average 17), then 9.96, 10.35 and 9.76 s on a quiet machine. A1's baseline is
  9.0 to 9.9 s in Debug (after: 9.0 to 9.9 s), so the quiet Release runs are at most about 5% above the Debug figure and cannot
  be told apart from run-to-run noise or from the configuration change; the solution has also grown since A1. Budget (under 2%)
  is therefore met only in the sense that no cost is visible; a clean A/B of S2a alone was not possible after the fact.
- `dotnet build src/NetPrints.Core -c Release --no-incremental -p:ReportAnalyzer=true`: total analyzer time 5.8 s for the Core
  compilation, of which SonarAnalyzer 5.4 s, IDisposableAnalyzers 0.13 s, Threading analyzers 0.13 s, CommunityToolkit.Mvvm
  generators 0.04 s and `NetPrints.Authoring` (the pin generator and the NPA analyzers) 0.002 s. The repository's own
  analyzers are under 0.1% of the analyzer time; Sonar is the cost, as before S2a.

## Reviews

(Review A+B: T038; Review C1+C2: T077; Review D1+D2: T111; Review E+F+G: T153; Final review with Review H: T155)

### Review A+B

(T038, at 0a37ba60; PR #20 review 5551162363, one inline thread per finding.) 0 blocker, 2 major, 9 minor, 8 nit. Evidence:
Release build 0 warnings; Analyzers.Tests 155/155, Core.Tests 906/906, Cli.Tests 279/279, Editor.Tests 1955/1955,
Catalog.Tests 346/346; CLI (Windows) run 38934517781 ran the git-install path test and the CRLF test; scratch probes for
NPS, NPA003/NPA005/NPA011 and the STJ extension-data shapes.

- AB-1 major `docs/guide/graph-format.md:105`: the editor's open and save drop unknown members (`DocumentMapper.cs:91`, `:250`,
  `:267`; `ProjectPersistence.cs:163`, `:245`); FR-007 and US1 scenario 6 hold only for `format`, `show` and `merge`.
  Fixed in 240a07c7 (B-F1): the mapper remembers the unknown members against the model object each record became
  (`PreservedMembers`, a weak side table, no change to Core) and writes them back; `UnknownMembersEditorRoundTripTests` loads
  four fixtures with a member on every record through `ProjectPersistence`, marks the class dirty, saves and compares bytes
  (red first on the byte comparison).
- AB-2 major `src/NetPrints.Authoring/Pins/PinSymbols.cs:41`: NPA003/NPA005 miss `AddInputDataPin`/`AddOutputDataPin`;
  `PinRulesAnalyzer.cs:140` never checks a kind whose pins all come from a `[NodeBase]`.
  Fixed in e8876357 (B-F1): the two data methods are in the add list and the constructor check covers inherited layouts; one
  test per case (red first). Detection stays by name inside the constructor; a helper that adds pins is not seen (said in
  the extensions guide), the semantic-model resolution was not worth the cost.
- AB-3 minor `src/NetPrints.Authoring.CodeFixes/Pins/NpaSyntax.cs:58`: S1905 here and S3459 at `MakeArrayTypeNode.cs:18` are
  unrecorded info-level rules; no gate keeps SC-015 at zero.
  Fixed in 8d91ea34 (B-F2): the cast is gone and S3459 is a row in the ADR-0003 table (the generator supplies the getter). The
  gate for the info pass is not added; the batch rule's `dotnet format analyzers --severity info --include` run stays the check.
- AB-4 minor `docs/adr/0003-analyzer-promotion-and-string-literal-discipline.md:405`: the S1135 and S1144 (:431) reasons do
  not hold.
  Fixed in 9dc5c58e (B-F3): the two unused members are deleted (S1144 row removed; build 0 warnings), and the S1135 row now
  says the TODOs are untracked pre-P1 markers that stay until their code is rewritten. No roadmap item was added.
- AB-5 minor `.github/release-notes.md:3`: typed getters, sealed node classes and removed overrides missing; the shipped
  0.3.0 section stays in the release body (`release.yml:214`).
  Fixed in b52c75a6 (B-F3): the typed getters, the sealed classes and the removed overrides are breaking entries under
  "Unreleased"; the shipped "## 0.3.0" section is gone and "## Downloads" stays.
- AB-6 minor `src/NetPrints.Serialization/Documents/UnknownMembers.cs:14`: the reference records (`Refs.cs:18-82`) drop
  unknown members.
  Fixed in 927f836e (B-F1): the six reference records carry `ExtensionData`, so `format`, `show` and `merge` keep what is
  inside them. The editor's save still writes references from the model's own types, so it drops them there; FR-007,
  data-model section 7, ADR-0038 decision 10 and the graph-format guide now say so (the T012 note above that excluded the refs
  because they are compared as keys was wrong: `UnknownMembers` has value equality).
- AB-7 minor `specs/006-editor-extension-host/spec.md:654`: recorded deviations not amended (13/2 counts, S0 golden, IL2057,
  `default(T)` resets, NPI003 wording, NodePins line format).
  Fixed in b52c75a6 (B-F3): amended in place in spec (US2 scenario 3, FR-009, FR-011/012, FR-018), research (R8, R11),
  data-model (§6 and §7), plan, tasks (T029, T031, T033), contracts (NPI003) and ADR-0034. The roadmap (governance file)
  still says "13 pin property renames plus 2 overrides" at S3; the coordinator amends it.
- AB-8 minor `docs/adr/0029-node-capabilities-and-pin-roles.md:44`: the overload button is unreachable through
  `IOverloadableNode` (`NodeViewModel.cs:230`, `:240-243`); the role interfaces have no stability class (R16).
  Fixed in b52c75a6 (B-F3) in the ADR, not the code: ADR-0029 now says the overload button needs a listing member that
  `IOverloadableNode` lacks, and that the three role interfaces are unsettled until the C1 publish-gate review classifies
  them. No code change, so no test.
- AB-9 minor `src/NetPrints.Editor/NodeSearch/NodeCreationService.cs:91`: two switches (also `:139`) against ADR-0028:39 and
  data-model §8.
  Fixed in 979ba06f (B-F2): each entry has `CreateAsync` over a `NodeCreationContext`; the prompted built-in kinds are one
  `NodeKindCreation` subclass each; the service has no switch. Test: an entry of a test-only kind creates its own node
  (red as a compile failure, the member did not exist; the existing creation tests are the invariants).
- AB-10 minor `src/NetPrints.Editor/NodeSearch/NodeSearchEntry.cs:17`: titles and keywords from catalog metadata not filled,
  no follow-up.
  Fixed in 46820b0d (B-F2) by moving it, not filling it: the editor's reflection provider reaches no `CatalogNodeHint`, so the
  work is in T051 (SUG4) and T020, research R10 and plan B say so.
- AB-11 minor `docs/adr/0024-analyzer-and-generator-infrastructure.md:82`: the S2a build-time budget was not measured.
  Measured in b52c75a6 (B-F3): see "Build-time budget" above (Release, quiet runs 9.8 to 10.4 s against A1's 9.0 to 9.9 s in Debug).
- AB-12 nit `src/NetPrints.Authoring/Pins/PinKeyTrackingAnalyzer.cs:50`: tracking silently off without the NodePins files.
  Fixed in 5d8dfe62 (B-F2): new NPA012 (info) on a `[NodeKind]` class with declared pins when neither file exists; ADR-0025,
  the contract table and the guide say so (red: the old "nothing is tracked" test now expects NPA012).
- AB-13 nit `src/NetPrints.Editor/NodeSearch/SuggestionRanker.cs:23`: the This Methods / This Variables direction is not in
  "Owner questions taken by default".
  Fixed in b52c75a6 (B-F3): a row in that table (T022, default pending owner) and a line in ADR-0028.
- AB-14 nit `src/NetPrints.Authoring.CodeFixes/Pins/AddNodeMarkerCodeFix.cs:51`: the NPA009 fix writes the class name as
  the kind id.
  Fixed in 828f2314 (B-F2): the fix writes `[NodeKind("TODO.kindId")]` and its title says the id is a placeholder (guide row
  reworded in 5d8dfe62); red on the fixed-text assertion.
- AB-15 nit `src/Shared/Roslyn/DiagnosticHelpLinks.cs:12`: help links point to `#npaNNN` anchors the guide lacks.
  Fixed in 5d8dfe62 (B-F2): one `<span id>` per help-linked NPA, NPI and NPS code above its table; `DiagnosticsGuideTests`
  checks every help-linked code has its anchor (red on 22 missing anchors).
- AB-16 nit `specs/006-editor-extension-host/implementation-notes.md:515`: red-first for the sbyte/decimal defect was a
  compile failure (also B2, :386).
  Recorded in b52c75a6 (B-F3): with the `sbyte` row removed, `PrimitiveKindsTests` fails 4 of 7 on assertions, 7 of 7 restored;
  the old code cannot be tested because the tests use the new type. Note added at T033.
- AB-17 nit `docs/adr/README.md:51`: leftovers (reserved-number sentence, ADR-0003:393 date, trim baseline at :552, checkpoint
  A longest job).
  Fixed in 9dc5c58e (ADR-0003 date) and b52c75a6 (README sentence, trim baseline 14 to 11 = down 3, Checkpoint A longest job:
  Desktop E2E 13.5 min), B-F3.
- AB-18 nit `src/NetPrints.Core/Graph/CallMethodNode.cs:261`: an out/ref parameter named `Exception` takes the exception
  pin's stored key.
  Fixed in c11763f8 (B-F2): the exception pin is found by its role, and an out/ref pin named `Exception` is keyed
  `out.data.param:Exception`; the test failed because the exception pin was never added next to such a parameter. A
  parameter named like a return pin (`Boolean`) keeps `Boolean~2` (stored keys stay stable); said in the commit, not changed.
- AB-19 nit `src/NetPrints.Core/Graph/ForLoopNode.cs:9`: kind ids repeat the `BuiltInNodeKinds` literals (a test ties them).
  Fixed in c11763f8 (B-F2): `NodeKindIds` in Core, `[NodeKind]` uses it, `BuiltInNodeKinds` forwards it; a hygiene test rejects a
  literal (red on the assertion).

### Review C1+C2

(T077, at 9d82b72a; PR #20 review 5555816642, one inline thread per finding.) 0 blocker, 8 major, 14 minor, 5 nit. Evidence:
Release build 0 warnings; Core.Tests 1036/1036, Editor.Tests 2013/2013, the publish-gate classes 60/60, Cli.Tests facet, writer
identity and project-extension classes 12/12; Analyzers.Tests 274 with one intermittent failure (CD-2); M1 and M2 read from the
`test-results-perf` TRX artifacts of runs 38899627936 (the first green perf run), 38948068721, 38973466744, 38977152765 and
38993704432; a scan of every `PublicAPI.*.txt` for members that name an experimental type without its id. No `!` was added
to `src/` or `tests/` code in C1a to C2f.

- CD-1 major `tests/NetPrints.Editor.Tests/Graph/GraphScopePerformanceTests.cs:70`: the +10% M1/M2 time gate is asserted
  nowhere and the R5-0 perf-job baseline was never recorded; the perf TRX does carry the numbers (the C1 note says it does
  not), and at 70c75b75 they are over the gate (M1 warm +12.7%, M2 +47.2% against run 38899627936).
- CD-2 major `src/NetPrints.RepoAnalyzers/Hygiene/RawDiagnosticCodeAnalyzer.cs:78`: NPH003's duplicate check depends on
  callback order under concurrent execution; `ASecondDeclarationOfOneCodeInAProjectIsReported` failed 3 of 7 full runs.
- CD-3 major `src/NetPrints.Serialization/PublicAPI.Unshipped.txt:535`: AP-T02 (`ExperimentalApiTests.cs:95`) scans Core,
  Abstractions and Catalog only; the stable `NodeDataMigrator` constructor names the NPXE0013 interface, and
  `ExtensionLoaderOptions.SettingsStore` (NPXE0008) returns an NPXE0002 type.
- CD-4 major `src/NetPrints.Extensibility.Abstractions/Nodes/NodeKindDescriptor.cs:31`: `DataVersion` (NPXE0013) is a
  parameter of the stable positional constructor and `Deconstruct`.
- CD-5 major `src/NetPrints.Extensibility.Abstractions/Editor/SettingsPageDescriptor.cs:11`: settings pages moved to NPXE0005
  and NPXE0010 was dropped without amending ADR-0040, R16, the contract, the plan or T068.
- CD-6 major `src/NetPrints.Extensibility.Abstractions/Editor/DialogDescriptor.cs:11`: dialog and tile titles, template names
  and descriptions, and the stable `NodeSuggestion` labels are strings, against FR-027 and contract §4; suggestion text doubles
  as its resource key.
- CD-7 major `src/NetPrints.Core/Translator/TranslatorUtil.cs:97`: the two-argument `ObjectToLiteral` shipped in 0.2.0 now
  writes `\e`, which C# before 13 rejects.
- CD-8 major `tests/NetPrints.Cli.Tests/Generation/CSharpTargetWriterIdentityTests.cs:61`: no fixture holds a
  version-dependent construct, so the SC-010 byte comparison cannot fail on the language version.
- CD-9 minor `src/NetPrints.Extensibility/Loading/ExtensionServiceProviders.cs:56`: `ILogger<T>` resolves for any `T`, and
  the pinned `HostExports.AllowedServiceTypes` is not what the provider is built from.
- CD-10 minor `src/NetPrints.Extensibility/Loading/ExtensionServiceProviders.cs:68`: a deny-list seam filter, contributions
  disposed between sessions and providers, and no startup validation, against ADR-0031 decision 5 and R15, unamended.
- CD-11 minor `src/NetPrints.Editor/Shell/ProjectSessionViewModel.cs:278`: after a failed project switch the open session
  keeps extension scopes the old registry disposed (the rollback in `ProjectLoader.cs:448-457`).
- CD-12 minor `src/NetPrints.Authoring/Contributions/ContributionLiteralsAnalyzer.cs:20`: NPA022's owner prefix reads a
  property no shipped target makes compiler-visible.
- CD-13 minor `src/NetPrints.RepoAnalyzers/Hygiene/LiteralUiTextAnalyzer.cs:63`: NPH006 misses `StatusBarViewModel.Show` and
  interpolated strings, which the contract's "status-bar text" covers.
- CD-14 minor `src/NetPrints.Editor/Contributions/BuiltIn/StatusBarContributions.cs:68`: the new segments build English text
  and plurals in code (FR-027).
- CD-15 minor `src/NetPrints.Editor/Graph/NodeTexts.cs:32`: NPA040 requires an extension kind's title key, which is shown only
  when `TitleIsFixed` is overridden; the guide does not say so.
- CD-16 minor `.github/release-notes.md:1`: no C1 or C2 entry (NPX002 for `netprintsApi` 1.0, new packages, `kindVersion`,
  the escape output, `netprints.language`, the status bar, the author analyzers).
- CD-17 minor `tests/NetPrints.Core.Tests/Extensibility/PackageValidationTests.cs:88`: only CP0002 and CP0009 are
  cross-checked; the documented release step turns three tests red; the 0.2.0 baseline goes stale if 0.3.0 publishes first.
- CD-18 minor `tests/NetPrints.Core.Tests/Extensibility/ExtensionApiVersionTests.cs:27`: a shipped row can be rehashed with
  the version unchanged.
- CD-19 minor `src/NetPrints.Editor/Hosting/ViewResourceLoader.cs:44`: no task owns multi-target common capabilities,
  view resources by URI, VSTHRD002/103 in tests, or per-extension services for panel and start-page contexts.
- CD-20 minor `src/NetPrints.Core/Projects/CSharpTarget.cs:103`: ADR-0038 decision 2 and FR-071 still name the editor
  Roslyn's maximum; `CSharpTarget.Resolve`, its input and result records and `INodeHintSource`/`NodeHint` are stable and
  unclassified in R16.
- CD-21 minor `HygieneLedger.txt:37`: the 19 grandfathered test `!` sites all have a proper form (`Assert.NotNull`,
  `?? throw`, a pattern match, a nullable local); fix them and drop the NPH001 section.
- CD-22 minor `src/NetPrints.Extensibility.Abstractions/Editor/CommandSelection.cs:10`: `Nodes` hands extensions the
  editor's view models, against ADR-0020 Amendment 1.
- CD-23 nit `docs/adr/0003-analyzer-promotion-and-string-literal-discipline.md:444`: ADR-0003 (:25, :65, :301, :387) and
  ADR-0007 (:39) still name the removed hygiene tests as the enforcement.
- CD-24 nit `specs/006-editor-extension-host/contracts/extension-api.md:18`: contract §1, §4 and §6, R13, R17 and data-model §2
  do not follow recorded deviations (`InMemoryHostChannel`, opt-in form, `PublicClass`, built-in owner id, `AppliesTo`,
  library tree).
- CD-25 nit `docs/guide/extensions.md:258`: the NPXE0005 and NPXE0013 rows are incomplete; no opt-in probe for NPXE0009,
  0012 and 0013.
- CD-26 nit `specs/006-editor-extension-host/implementation-notes.md:757`: red-first was compile-only in C1c, C1d and C2a, and
  two C1e tests were never run red.
- CD-27 nit `tests/NetPrints.Editor.Tests/Hosting/CompositionResolveAllTests.cs:75`: the session-scoped template service runs
  in a scope with no project, where the backup services throw (fails fast).

All 27 findings are resolved: fix batches C-F1 to C-F7, plus C-F2b and C-F2c. The last green CI is at eada8ad1 (CI 39032644596,
CLI Windows 39032644523, Release 39032644552, Docs 39032644486); Batch C-F7 is on top of it.

## Batch C2f (T074-T076)

Red/green. Every rule's tests were written against an analyzer stub that declares its descriptor and reports nothing, and failed on the
`Assert.Equal` of the marked spans (`Actual: Expected[] []`), then went green with the analyzer. Red counts: NPH001 3 of 4, NPH002 2 of 3,
NPH003 15 of 21, NPH004 6 of 7, NPH005 1 of 5, NPH006 4 of 6, NPA020 to NPA024 26 of 40 (the others assert acceptance). The tests that
assert acceptance pass on the stub by design. Planted violations in a real build: a scratch file in `NetPrints.Extensibility` with a pragma,
a `VM` type, `null!` and an `"NPT001"` literal failed the build with NPH004, NPH002, NPH001 and NPH003 (file removed).

T074 (commits f09da9ec, f849359c, 28bc7bc3, 1b2f0a2c, 40070521, a149fc68, e878d378, af244931). One commit per rule, each removing the
`SourceHygieneTests` code it replaces.
- Decision: the allowlists and the ADR-0003 ledger are one additional file, `HygieneLedger.txt` at the repository root (`CODE | path | detail`,
  path matched by suffix), added to every src and tests project. NPH001 and NPH002 run in `tests/` as well as `src/` because the replaced
  tests did; the 19 test sites of `NullForgivingAllowlist` are NPH001 rows. NPH004 rows are `member:rule` and still require the ADR-0003
  justification. NPH003 and NPH005 are `src/` only.
- Deviation: the cross-project duplicate check of a code (`NPXE0004` in Core and Catalog) stays a test, `NoDiagnosticCodeIsDeclaredInTwoProjects`,
  because an analyzer sees one project; NPH003 reports duplicates inside a project. The other `SourceHygieneTests` stay.
- Deviation: NPH006 does not flag a literal given to a `LocalizedText` parameter (`CommandDescriptor.Label`): ADR-0027 derives the key from the
  command id and the literal is its neutral default (40 built-in labels would otherwise be hits). The sinks are the `string` parameters of
  `IDialogService`, `IEditorDialogs.Confirm` and `ShellViewModel.ShowStatus`, and `DialogDescriptor.Title`. 25 dialog titles and messages in
  `src/NetPrints.Editor` are ledger rows that move to resources with the L2 localization batch.
- Deviation, open: VSTHRD002 and VSTHRD103 to `error` in `tests/` was tried and reports at least 153 hits (synchronous `File.*` calls);
  not applied, ADR-0003 says so.
- ADR-0003 and ADR-0007 amended (E7 stays a test), AGENTS.md and ADR-0017 name the NPH rules, the guide has an NPH section.
- The roadmap-code test `NoUserVisibleStringNamesARoadmapCode` failed on an `ADR-` literal in NPH004's message; the marker is built from two parts.

T075 (1cb00da4). `ContributionLiteralsAnalyzer` (NPA020 to NPA022) and `ContributionDuplicatesAnalyzer` (NPA023, NPA024) in
`NetPrints.Authoring`, matching the extension API types by name so the analyzer assembly needs no reference to them.
- Decision: the registered built-in icon ids are the constants of `NetPrints.Editor.Icons.IconIds` when the compilation declares it; an
  extension author's project has no such type, so only the pattern is checked there. The owner prefix is read from
  `build_property.NetPrintsExtensionId` when a project makes that property compiler-visible; no shipped target does it yet.
- Gestures: `CommandGesture.Parse` and `TryParse` literals are checked against a copy of the key grammar (`GestureText`); the scope rule
  (single-key or Shift-only outside F1 to F24 in the Global scope) is checked for `new(modifiers, key)` and parsed gestures in `DefaultGestures`.
- The Authoring analyzers now also run on `NetPrints.Editor` (analyzer reference added). They found a real bug: the six built-in panels still
  passed the old Material kind names ("FileTree", "Tune") as `IconId`, so they drew the unknown glyph; they use `IconIds.Panel*` now.
  The repository has 0 other hits.

T076, Checkpoint C2, gate items measured on this head (Release).
- Whole suite: 4826 tests, 4793 passed, 33 skipped, 0 failed (`dotnet test --solution`, `--ignore-exit-code 8`). E2E (`NETPRINTS_E2E=1`,
  `--fail-skips on`): 53 tests, 52 passed, 1 skipped, 0 failed.
- SC-001 (NPH): 0 hits in `src/` and `tests/` with the ledger as listed (19 NPH001, 3 NPH004, 25 NPH006 rows); each replaced test's red/green is above.
- API checks and package validation, the XML docs (CS1591 stays an error in `src/`), `NetPrints.TestExtension` from the packed packages and the
  `packages` job: unchanged by this batch (no public API change; PublicAPI hashes untouched) and green in CI at 70c75b75 (see T073). Trim baseline stays 9.
- SC-005 in the perf job, the longest CI job (at 70c75b75: Desktop E2E 13 min 41 s, longest test leg Editor UI 2/4 and 3/4, 7 min 20 s) and the
  Dock check (pin stays 12.1.0.6): read from CI at the final head of the review batch; not re-measured locally here.
- Docs: ADR-0003 and ADR-0007 amended here; the extensions guide, ADR-0030, ADR-0020, ADR-0021, `releasing.md`, `graph-format.md` and
  `docs/api/docfx.json` were done in C2a to C2e.
- Checkpoint C2, CI at 9d82b72a green: CI run 38996910612, CLI (Windows) run 38996910613, Release run 38996910683, Docs run 38996910605. T076 ticked.

## Batch C-F1 (T078, part 1: CD-2, CD-7, CD-8, CD-20)

- CD-2: fixed in acc2f524. The analyzer collects the declarations per compilation and reports every one but the first, ordered by
  path and then position, in a compilation-end action (`CompilationEnd` tag on the descriptor, RS1037).
  Red: the new `TheDuplicateAcrossFilesIsTheOneInTheLaterPathWhateverTheCompilationOrder` (two files passed latest-path first, 25
  rounds) failed in 6 of 6 runs on the old analyzer (`Expected ... BDiagnosticCodes.cs, 42; Actual ... ADiagnosticCodes.cs, 42`), and
  `ASecondDeclarationOfOneCodeInAProjectIsReported` failed intermittently across the same runs (Expected start 81, actual 42).
  Green: the Analyzers project, 275 tests, 12 of 12 runs (Release).
- CD-7: fixed in 060305ca. v0.2.0 wrote `SymbolDisplay.FormatLiteral` output unchanged, so the escape character is `\u001b`; the
  two-argument overload keeps that and only the `CSharpTarget` overload rewrites to `\e` (C# 13 and later).
  Red: `TheLiteralOverloadWithoutATargetKeepsTheOutputOfRelease020` (Expected `"a\\u001bb"`, Actual `"a\\eb"`). Green: TranslatorTargetTests
  8 of 8, with the below-13 and from-13 cases already pinned and a new `Unconstrained` case.
- CD-8: fixed in 580bc618. The identity test copies `tests/Fixtures/CSharpTarget/HelloWorld.Program.netpc.json`, a graph whose
  string holds an escape character, and asserts the expected form per fixture (`\u001b` for Pinned10 and MultiTarget, `\e` for the
  others). Red: `GraphCodeGenerator` temporarily given `CSharpTarget.Unconstrained` instead of the request's target failed Pinned10 and
  MultiTarget (Expected `Hello,\\u001bWorld!`, Actual `Hello,\\eWorld!`); restored, 8 of 8 green.
- CD-20: fixed in c410c739. ADR-0038 decision 2 and FR-071 name `MaxKnown`; decision 9 records the escape rule (CD-7); R16 and the
  guide's "API stability" list `CSharpTarget.Resolve`, `CSharpFrameworkInput`, `CSharpTargetResolution`, `INodeHintSource` and
  `NodeHint` as stable, so no PublicAPI change.

## Batch C-F2 (T078, part 2: CD-1)

- Format: 13de56f5 fixes the whitespace `dotnet format` flagged in `HygieneHarness.cs` (the Repository checks leg at 297524b4).
- CD-1: gate added in 110f7b96; baseline recorded under "R5-0 baselines" (run 38899627936); the C1 note corrected (the perf TRX carries the numbers).
- Trend from the `test-results-perf` TRX (M1 warm median / M2 median, as the tests reported them):

  | Run | Head | M1 warm | M2 | M2 runs 1 to 5 (ms) |
  |---|---|---|---|---|
  | 38899627936 | d7ddde50 | 418.7 | 51.9 | 52.4, 59.9, 44.1, 41.6, 51.9 |
  | 38948068721 | 6fb02d16 | 339.5 | 46.4 | 65.4, 72.9, 46.4, 24.6, 22.8 |
  | 38973466744 | 18824b36 | 449.1 | 54.7 | 100.3, 162.8, 54.7, 51.0, 52.2 |
  | 38977152765 | 7a426036 | 382.6 | 41.2 | 117.6, 69.0, 24.3, 20.4, 41.2 |
  | 38993704432 | 70c75b75 | 471.7 | 76.4 | 91.6, 106.0, 76.4, 72.1, 55.0 |
  | 38999385388 | cd32d8eb | 285.1 | 53.2 | 57.6, 73.4, 51.8, 53.2, 40.5 |

- Cause: there is no commit range to blame. The newest green head (cd32d8eb, after C2e and C2f) reads M1 -32% and M2 +2.5% against the baseline
  run, and 70c75b75 was one noisy sample. The recorded method takes the median of five (M2) or of five warm runs (M1) while the process is still
  in the JIT's lower tiers: in Release at 297524b4, M2 runs are about 83, 43, 28, 15, 14 ms, and at d7ddde50 about 92, 44, 15, 14, 14 ms, so the
  median is run 3 and lands before or after the tier-up depending on timing (the bytes per node flip between 36,037 and 35,080 with it). The
  steady state is the same: M2 14.3 ms at d7ddde50 and 14.5 ms at 297524b4 (8 runs each, in the same harness), M1 70.2 ms and 64.1 ms (6 runs).
  No product change was needed, and none was made.
- Gate (110f7b96, `tests/NetPrints.Testing/PerformanceGate.cs`): warm up for at least 1 s and until three runs agree within 10%, take the median of
  nine runs, repeat up to three times while it is over the limit and keep the lowest median (noise only adds time); divide by a calibration (the
  fastest of 15 runs of a fixed allocation-and-hash workload, `AggressiveOptimization`, measured before and after, the lower kept) and compare with
  the baseline ratio at x1.10. Judged only with `NETPRINTS_PERF_STRICT=1` (the perf job); other runs print the numbers. Baseline ratios, measured
  with this harness on the baseline commit d7ddde50: M2 0.954 (8 runs 0.92 to 1.01), M1 4.75 (6 runs 4.56 to 5.23). Head: M2 0.956, M1 4.33.
  The ratios come from one local machine; the first perf run after this batch shows whether CI agrees. Until then a CI failure is a calibration
  question first. The perf job is non-blocking.
- Red: with 14 ms of busy wait added to the `NodeGraphViewModel` constructor (about double, not committed), the old assertions and a non-strict run
  pass (`M2 ... ratio 1.944 ... +103.8% over the baseline`, Total 1, Failed 0), and the strict run fails: `M2 steady-state median: 27.7 ms
  against a calibration of 14.5 ms is a ratio of 1.916; the baseline is 0.954, the gate 1.049 (+10%), +100.8% over the baseline.` Without the
  addition the strict run passes (M2 and M1). `PerformanceGateTests` 3 of 3 pin the verdict and the median.
- Full suite (Release, `dotnet test --solution NetPrints.slnx`): 4831 total, 4798 passed, 33 skipped, 0 failed; build 0 warnings; `dotnet format
  NetPrints.slnx --verify-no-changes` clean.
- Deferred: the allocation half of M2 stays as before (bytes per node, +10%).

## Batch C-F3 (T078, part 3: CD-3, CD-4, CD-5, CD-18, CD-25)

- CD-3: `ExperimentalApiTests.EveryPublicSymbolThatMentionsAnExperimentalTypeIsExperimentalWithTheSameId` now scans every library that has a `PublicAPI.Shipped.txt`
  and `<IsPackable>true</IsPackable>` (found on disk, six assemblies, not a list). Red on the wider scan (assertion):
  `NetPrints.Serialization.Migrations.NodeDataMigrator..ctor mentions NPXE0013 but carries no [Experimental]` and
  `NetPrints.Extensibility.Loading.ExtensionLoaderOptions.SettingsStore mentions NPXE0002 but carries NPXE0008`. Fixed, not allow-listed: `NodeDataMigrator` is `NPXE0013`;
  `JsonDocumentFormat` keeps the two-argument constructor stable and the three-argument one is `NPXE0013`; `SettingsStore` is `NPXE0002`. Green after.
- CD-4: new `AStablePositionalParameterNeverFeedsAnExperimentalProperty`. Red: `NodeKindDescriptor constructor parameter DataVersion feeds the NPXE0013 property of the same name`.
  `DataVersion` is now an init-only `[Experimental(NPXE0013)]` property (default 0); the six-parameter constructor and `Deconstruct` carry no experimental value.
  Existing `with { DataVersion = 1 }` callers compile unchanged.
- CD-5: settled by amendment, not by moving the code. Settings pages, `AddSettingsPage` and `CreateViewModel` stay `NPXE0005`; `NPXE0009` marks the settings metadata only;
  `NPXE0010` is retired and never reused. ADR-0040 amendment 1, research R16, contracts/extension-api.md, the plan (C2) and T068 now say so, and the guide's table has the
  `NPXE0010` row. New `TheGuideAccountsForEveryIdUpToTheLastOne`. Red (row removed, restored after): `NPXE0010 has no row in the guide's id table`.
- CD-18: `ExtensionApiVersionTests` rows carry a `Shipped` flag; new `AShippedVersionHasNoUnshippedEntries` (a shipped last row needs empty Unshipped files, an older row must be shipped).
  Red with the 0.4 row marked shipped, hash unchanged and the version unchanged: `0.4 is shipped; add a row for the next ExtensionApi.Version before changing the API (1109 unshipped entries)`;
  restored to not shipped (0.4 has not been released). Rehashing a shipped row to follow an API change now fails because the change sits in the Unshipped files.
- CD-25: the guide's `NPXE0005`, `NPXE0002` and `NPXE0013` rows list the missing symbols. `ExperimentalOptInTests` gained probes for `NPXE0009`, `NPXE0012` and two for
  `NPXE0013` (`INodeDataMigration`, `NodeKindDescriptor.DataVersion`); they pass at once because the marks already existed, so they pin the existing behaviour (no red).
- API files: the Abstractions, Serialization and Extensibility `PublicAPI.Unshipped.txt` follow the changes; the recorded 0.4 hash was refreshed to the new files (`d78d636b...a576`).
- Full suite (Debug, `dotnet test --solution NetPrints.slnx`): 4842 total, 4809 passed, 33 skipped, 0 failed; `dotnet format NetPrints.slnx --verify-no-changes` clean.

## Batch C-F4 (T078, part 4: CD-6, CD-13, CD-14, CD-15)

- CD-6: `ContributionTextTests`. Red (assertion, 15 failed): the five label properties (`DialogDescriptor.Title`, `DashboardTileDescriptor.Title`, `ProjectTemplateDescriptor.DisplayName` and
  `Description`, `NodeSuggestion.DisplayName`) are not `LocalizedText`, and the ten built-in tile and template keys are not in the neutral resx. Green after the change. `DialogDescriptor`,
  `DashboardTileDescriptor` and `ProjectTemplateDescriptor` take convention keys like panels (`Dialog.<id>.Title`, `Tile.<id>.Title`, `Template.<id>.Name`, `Template.<id>.Description`);
  `NodeSuggestion.DisplayName` keeps a plain string as its own key and takes an explicit `LocalizedText(key, text)` to separate them (`NodeDisplayTextTests`, written after the code).
  `NodeSuggestion.Category` stays a string: it groups rows and its header already resolves through `Category.<name>`. The New project dialog resolves the template texts through
  `LocalizedTextConverter`. None of these types is in a published baseline (`NetPrints.Extensibility` and `.Abstractions` have none; `PublicAPI.Shipped.txt` is empty), so package validation
  needs no suppression; the break is in the release notes and the 0.4 hash row is refreshed (`aa653180...723d`, still not shipped).
- CD-13: `Nph006Tests`. Red (assertion): `ALiteralPassedToTheStatusBarIsReported` and `AnInterpolatedStringWithTextIsReportedAtEverySink` found no diagnostic. `StatusBarViewModel.message` is a sink and an
  interpolated string with literal text is reported. `DialogDescriptor.Title` is no longer a sink (a `LocalizedText` with a convention key, like a command label). The thirteen new hits in
  `src/NetPrints.Editor` moved to the resx (`Message.*`, `Status.*`) with `TextFormat.Of`; no suppression and no ledger row.
- CD-14: `StatusSegmentTextTests`. Red (assertion): the six `Status.*` keys are not in the resx. The repository has no plural mechanism, so counts use a one key and an other key chosen by count
  (`Status.ErrorsOne`/`ErrorsOther`, `WarningsOne`/`WarningsOther`); selection and zoom have one key each. The text is pinned by `ACountUsesTheSingularFormForOneAndThePluralFormOtherwise` (written after the code). Release note: "Added".
- CD-15: `Npa040Tests`. Red (assertion): `AMissingTitleKeyIsQuietWhenTheTitleIsBuiltFromTheNode`. NPA040 requires `Node.<kind>.Title` only when the class (or a base) overrides `TitleIsFixed`, because the editor
  shows the key only then (ADR-0027). The guide, the diagnostics table, the analyzer release file and the ADR agree. No `Fx` node test was added.
- Full suite (Release): 4872 total; the only failures were the two `ContributionsAreUiFreeTests` (the converter was in the UI-free `Contributions` namespace), fixed by moving it to `Controls`; Editor.Tests 2043 and Core.Tests 1040 pass after; `dotnet format` clean.

## Batch C-F5 (T078, part 5: CD-9, CD-10, CD-11, CD-27)

- CD-9: `ExtensionServicesTests.AnExtensionGetsLoggersOnlyForItsOwnTypes`. Red (assertion): `Assert.Null() Failure: Value is not null, Actual: ExtensionLogger`1` for `ILogger<NodeGraph>`. `ExtensionServiceProviders.Build`
  now registers each entry of `HostExports.AllowedServiceTypes` (an entry without a registration throws), and `ILogger<T>` is registered closed for the types of the extension's assembly only, so the
  pinned list is what the provider serves. An extension registering `ILogger<T>` or `IOptionsMonitor<T>` itself is rejected as replacing a host export.
- CD-10: `ARegistrationOfATypeThatIsNotTheExtensionsOwnIsRejected` and `ARegistrationWithAMissingDependencyFailsTheExtensionAtLoad`. Red (assertions): no NPX006 issue for `HttpClient` (collection empty), and no `Failed` result for a
  registration whose dependency is missing (both results `Loaded`). Green: the seam filter is an allow-list (the extension's own types, and generics outside the host assemblies over allowed types); the provider is built with
  `ValidateOnBuild` before anything of the extension is committed, and a failure is an NPX005 `Failed` result naming the missing service. ADR-0031 decision 5 and R15 are amended in place: the disposal order is
  sessions, contributed objects, provider (a contribution may use provider services in `Dispose`), which is the behaviour that was already there.
- CD-11: `ProjectLoaderTests.AFailedSwitchLeavesTheOpenSessionsExtensionServicesUsable`. Red (exception): `ObjectDisposedException` from the open session's extension services after a failed switch. `ProjectSessionViewModel` drops
  its cached scopes on `RegistryChanged` (the old registry disposes them), so the next use opens one on the live registry. The C2b note is corrected.
- CD-27: `CompositionResolveAllTests.EverySessionScopedRegistrationResolvesInAProjectSessionScope` now asserts `ProjectTemplateService` is not session-scoped. Red (assertion): `Assert.DoesNotContain() Failure: Filter matched in collection`.
  The service holds no session state and New project has no project, so `ProjectLoader` creates it with `ProjectTemplateService.Create` and `ProjectSessionScope.Templates` is gone. ADR-0031 decision 4 and R4 step 4 are amended.
- API files: no public API changed (the new exception and the changed types are internal).

## Batch C-F2b (T078, part 2b: CD-1, CI baselines)

- Trim ratchet (run 39010829786, 9 to 10): C-F5's `Assembly.GetTypes()` (IL2026) in `ExtensionServiceProviders` is gone. The own types an extension can get an
  `ILogger<T>` for are now the service and implementation types it registered that belong to it (`OwnTypes(accepted, ownAssembly)`); `AnExtensionGetsLoggersOnlyForItsOwnTypes`
  stays green and `eng/trim-warnings.sh` reports 9. Narrower than before: a type the extension never registers gets no logger.
- No regression (M2, same harness and session, 8 interleaved rounds of R5-0 d7ddde50 with the gate files ported, 01eb1e51, and f50b93b2; ratio of the steady-state median to the calibration):

  | Commit | Mean | Median | Range |
  |---|---|---|---|
  | d7ddde50 (R5-0) | 0.977 | 0.970 | 0.941 to 1.021 |
  | 01eb1e51 | 0.984 | 0.975 | 0.958 to 1.031 |
  | f50b93b2 | 0.976 | 0.972 | 0.944 to 1.023 |

  The three agree within 1%, well inside the 3% bar, so C-F3, C-F4 and C-F5 did not touch the M2 path and the baseline absorbs nothing. M1 locally is bimodal run to run
  (ratios 2.8 to 3.5 in one mode, 4.6 to 5.9 in the other, in all three trees): means 4.23 (R5-0), 4.07 (01eb1e51), 4.71 (f50b93b2), medians 4.85, 4.03, 4.91. The mode, not the
  commit, explains the spread; inside a mode the commits agree. M1 therefore cannot show equality to 2% locally.
- Why 0.954 did not carry over: the runner's CPU is not one model. Run 39012644189 (head 047a252b, the same code) was on an AMD EPYC 7763: M2 calibration 29.7 ms, ratio 1.904, M1 3.218. Its rerun
  and the earlier runs (563ec6c1: 1.102; 2d79e88f: 1.080; 047a252b first attempt: 1.098) were on an Intel Xeon Platinum 8573C (CPU printed from 047a252b on; the earlier two have the Xeon's
  17.7 to 19.8 ms calibration): M2 1.041 to 1.102, M1 5.545. The verdict now names the CPU on pass and fail (test output, the TRX in `test-results-perf`; the strict job's log shows it on fail).
- Baselines, set from CI runs at heads equal to R5-0 on the M2 path (the table above), keyed by CPU model with the highest as fallback for an unknown model, and the local ones kept:

  | Metric | Local (Ryzen 9 7950X3D) | CI Xeon Platinum 8573C | CI EPYC 7763 |
  |---|---|---|---|
  | M2 | 0.954 | 1.08 (mean of 1.102, 1.080, 1.098, 1.041) | 1.90 (one run, 1.904) |
  | M1 | 4.75 | 5.55 (one run) | 3.22 (one run) |

  `PerformanceGate.Baseline` picks the CI entry on `GITHUB_ACTIONS=true`. The gate stays +10%. The EPYC figures and both M1 figures are single samples, and M1 is bimodal locally,
  so M1 can fail the strict job without a regression; if it does, collect more runs per model before touching the tolerance. The perf job runs every project and fails at the end
  (before, a failing M2 stopped the loop and hid M1).
- Red: 14 ms busy wait in the `NodeGraphViewModel` constructor (not committed), strict run locally (baseline 0.954): `M2 steady-state median: 27.8 ms against a calibration of 14.5 ms is a ratio of
  1.923; ... gate 1.049 ... +101.6% over the baseline`, failed; with `GITHUB_ACTIONS=true` (the CI path; at that commit the CI baseline was the single value 1.09): `ratio of 1.930 ... baseline is 1.090 ...
  +77.1%`, failed. The per-CPU selection is pinned by `TheBaselineComesFromTheMachineKindTheRunIsOn`. (A first attempt measured the unmodified binary because the busy wait did not compile under S108/S109; it read 1.053, +10.4%, which shows the local noise at the gate.)
- Deferred: the allocation half of M2 is unchanged; more samples per CPU model come from later perf runs.

## Batch C-F6 (T078, part 6: CD-12, CD-22, CD-21)

- CD-12: `Npa022ShippedPropertyTests` reads the props file the NetPrints.Core package ships and feeds the analyzer the option names it exposes. Red (assertion): `The package ships no
  src/NetPrints.Core/buildTransitive/NetPrints.Core.props, so MSBuild never passes the extension id to the compiler` (2 of 2 failed). Green: `buildTransitive/NetPrints.Core.props` declares
  `CompilerVisibleProperty NetPrintsExtensionId`, is packed into `buildTransitive` (reaches an author who references Abstractions only), and `verify-packages.sh` step 2 checks the file. An extension
  sets `<NetPrintsExtensionId>` in its project file. Not done: defaulting the property from the manifest.
- CD-22: `CommandSelection.Nodes` is `IReadOnlyList<Node>` (model nodes) and the provider hands `vm.Node`. Red (assertions): `TheSelectionHandsExtensionsModelNodesNotOpaqueObjects` expected
  `IReadOnlyList<Node>`, actual `IReadOnlyList<object>`; `ShellCommandContextProviderTests` expected the model node `If Else`, actual a `NodeViewModel`. `AbstractionsExposeNoEditorTypesTests` pins that no
  public member of the Abstractions assembly uses a `NetPrints.Editor` type (green before and after: the leak was an `object` holding a view model, so only the type test is red). `GoToConnectionEnd`
  maps the model node back to its view model. API files: `CommandSelection` constructor, `Deconstruct` and `Nodes.get` take `IReadOnlyList<NetPrints.Graph.Node!>`; the 0.4 hash row is updated and not shipped.
- CD-21: the NPH001 section of `HygieneLedger.txt` is deleted. Red (build errors): NPH001 reported 19 sites in 11 test files (`error NPH001: Model the null instead of silencing it`). Green: all 19 fixed,
  none added: `Assert.NotNull(x)` on its own line, then the member (this xunit `Assert.NotNull` returns void, so the flow analysis carries the non-null state); `?? throw new InvalidOperationException`
  for `Process.Start`, `TestOutputHelper`, `Application.Current`, the headless `EditorApp` and the wait result; `ArgumentException` for `Path.GetDirectoryName`; `UiElement.Number` for the
  automation numbers; `UiWait.ForAsync` keeps the accepted value in an array instead of `default!`.

## Batch C-F2c (T078, part 2c: CD-1, same-runner A/B against R5-0)

- Why: the calibrated ratio did not hold across runner CPUs (M2 for the same code: local 0.95 to 0.98, Xeon Platinum 8573C 1.04 to 1.10, EPYC 7763 1.90, EPYC 9V74 2.54 in job
  117346128293), and at 250ed2fd M1 failed at +25.4% on yet another model (EPYC 9V45, job 117351326444). Decision (ADR-0031 amendment 1): compare the head with R5-0 built and
  run on the same runner, interleaved, at head / base <= 1.10. No fallback was needed.
- Method: `eng/perf/prepare-r5-0.sh DIR` adds a worktree at d7ddde50, applies `eng/perf/r5-0.patch` (the M1 and M2 tests of R5-0 call the harness; rebuilt from the C-F2b port) and
  copies the head's `tests/NetPrints.Testing/PerformanceGate.cs`, then builds the two test projects. `eng/perf/ab.sh BASE HEAD` runs 8 rounds; each round runs M2 and M1 once per tree,
  one fresh test process per run (the test executable with `-method`), the base first in odd rounds. A run warms up (at least 1 s and three samples within 10%), then times 9 (M2) or 21
  (M1) samples and reports the fastest; a tree's figure is the median of its 8 runs. `PerformanceGate` keeps only this (the per-CPU table, the calibration and the in-test verdict
  are gone); outside the script the two tests print their first-use numbers and judge no time.
- GC (found on the first runner run): job 117354922356 (730a3ddd, EPYC 9V74, 4 cores) read M2 0.955 but M1 1.339, every round alike (head 108 to 124 ms, R5-0 85 to 93 ms), while
  locally they agreed (0.996). With a full collection forced before each sample, a sample pays zero or one collection: R5-0 allocates 16.40 MB per start, the head 17.28 MB, and a 16 MB
  gen0 budget sits between them. Locally `DOTNET_GCgen0size=0x1000000` reproduces it (R5-0 68 to 70 ms, head 89 to 91 ms; with 0x400000 the head is faster); pinning to 4 CPUs or to
  SMT pairs did not. A sample now leaves its GC pauses out of its time (`GC.GetTotalPauseDuration`) and records the bytes the process allocated; `ab.sh` gates time and bytes, each
  at 1.10 (a41b0c51). With the 16 MB budget locally: time 1.043, bytes 1.053. Choosing the statistic: the median of each run's median sample would have failed one of the first 15
  local runs (M2 1.210, R5-0 against head); each run's fastest sample did not.
- Equal trees, locally (Ryzen 9 7950X3D, 8 rounds, the driver's own verdict): head against itself, 10 runs, 0 failed: M1 time 0.953 to 1.057 (mean 0.991, sd 0.036), M2 time 0.974
  to 1.064 (mean 1.007, sd 0.027), bytes 0.995 to 1.003. Three more under a load average of 42 to 49 from other processes: 0 failed, M1 0.966 to 1.040, M2 1.003 to 1.025. R5-0 against
  head, 5 runs, 0 failed: M2 time 0.947 to 1.073, M1 time 1.008 to 1.058, M1 bytes 1.049 to 1.051, M2 bytes 1.000. So 18 runs, 0 false fails; 1.10 is about three standard
  deviations above the equal-tree mean.
- Red (local, 8 rounds against R5-0, uncommitted busy waits first in the constructor, under the load above): M2 +14 ms in `NodeGraphViewModel` 1.728 FAIL (R5-0 19.21 ms, head 33.19
  ms); M2 +2 ms 1.123 FAIL; M1 +66 ms in `ShellViewModel` 1.792 FAIL; M1 +10 ms 1.129 FAIL; M1 with 2 MB more allocated per start: bytes 1.172 FAIL, time 1.027 PASS. Each run exited 1.
- CI: job 117359521544 (a41b0c51, AMD EPYC 7763, 4 cores): M2 time 0.988, bytes 1.000; M1 time 1.066, bytes 1.051 (the head's 24 ms GC pause per start left out, R5-0 none); PASS.
  Runs on the runner agree within about 2% per tree (M1: R5-0 107.6 to 112.4 ms, head 114.5 to 118.2 ms). The job took 13.2 min: build 2.3, strict budgets 0.8, R5-0 build 0.7,
  A/B 9.0. Its timeout is 30 min.
- Guards: `CiWorkflowTests.ThePerformanceJobComparesTheHeadWithR5ZeroOnTheSameRunner` (red before the workflow change: `The perf job does not build R5-0 with
  eng/perf/prepare-r5-0.sh.`; green after); `PerformanceGateTests` pin the sample (bytes counted, a collection inside a sample left out of its time), the steady state and the
  record line the script reads.
- Docs: ADR-0031 amendment 1 (the R5-0 gates are ADR-0031's; ADR-0037 is about run profiles and has no performance rule), research R4, the R5-0 baselines section,
  `docs/contributing/testing.md` and the spec 005 CI contract row.
- Full suite (Release, `dotnet test --solution NetPrints.slnx`): 4883 total, 4850 passed, 33 skipped, 0 failed; build 0 warnings; `dotnet format NetPrints.slnx --verify-no-changes` clean;
  `eng/trim-warnings.sh` 9.

## Batch C-F7 (T078, part 7: CD-15 test, CD-16, CD-17, CD-19, CD-23, CD-24, CD-26)

- CD-17 (f671e979): `PackageValidationTests` gained `EveryRecordedSuppressionHasACrossCheck` (every suppression id of every library is in
  `CP0002`, `CP0009`, the two ids a test checks). Red (assertion, with a `CP0001` suppression added to Core's file for the run, then removed):
  `NetPrints.Core: CP0001 T:NetPrints.Graph.TypeOfNode is a suppression no test checks`. The baseline is `NetPrintsPackageBaselineVersion` in
  `Directory.Build.props`, one edit; the three published libraries use `$(NetPrintsPackageBaselineVersion)` and the test reads the property from that file
  (red before: `NetPrintsPackageBaselineVersion in Directory.Build.props is ''`). The seal and break tests, and the `*REMOVED*` cross-check, return when a
  suppression file is absent, so the release step of `releasing.md` (bump the property, delete the files) leaves them green. 0.3.0 is not on nuget.org, so the
  baseline stays 0.2.0; `releasing.md` says not to move it until the version is published.
- CD-15 (de9bbf25): `AMissingTitleKeyIsReportedWhenAFixedTitleIsInheritedFromABaseClass` and `AFixedTitleKeyThatIsPresentIsQuiet` join the direct-override
  and quiet cases C-F4 already had. Red (assertion, with `OverridesTitleIsFixed` reading the type only, then restored): `Assert.Single() Failure: The collection was empty`.
- CD-16 (14ce7335): "## Unreleased" gains Breaking entries for NPX002 on `netprintsApi` 1.0 and the `\e` escape output, and Added entries for the
  three packages with the API tracking and NPA020 to NPA024 and NPA040 to NPA042, `kindVersion`, and `netprints.language`; the status bar line was there.
- CD-19 (13fd4e43): four tasks, none implemented. T172 common capabilities of a multi-target project (F2, FR-071); T173 view resources by URI (D1b, FR-032, FR-033);
  T174 VSTHRD002 and VSTHRD103 at error in tests (G4, FR-031); T175 the extension's own services in panel and start-page contexts (D1c, FR-021, FR-034).
  The batch headers, the task counts (164 active of 175 IDs, 194 units plus 5 in PR-P) and the FR coverage rows are updated.
- CD-23 (b3474812): ADR-0003 (the two summary paragraphs, the NPH003 heading, the review-order note, the `SourceHygieneTests` mentions that were left) and ADR-0007
  (the VM note) name NPH001 to NPH004. `NoUnlistedBuildWarningSuppressions` is still a test and stays named.
- CD-24 (f605766e): `extension-api.md` §1 (`InMemoryHostChannel` in Abstractions), §4 (`AppliesTo` positional on the context-menu and status-bar descriptors) and §6
  (`NoWarn`, the item is this repository's); research R13 (`PublicClass` for the editor resx only) and R17 (the console tree holds one file, the library tree is empty,
  the project file comes from the project system); `data-model.md` §2 (built-in owner `netprints.editor`).
- CD-26: the red-first notes of C1c (T047), C1d (T051, T052) and C2a (T060 to T062) say the reds were compile failures. The two C1e tests, run red on assertions now by breaking the
  code with Edit and restoring it:
  - `CompositionResolveAllTests.EverySessionScopedRegistrationResolvesInAProjectSessionScope`, with `AddSessionHost` registering `ProjectTemplateService` scoped:
    `Assert.DoesNotContain() Failure: Filter matched in collection` (1 of 3 failed); green again after.
  - `CompositionTests.TheDoublesReplaceTheGeneratedRegistrations`, with `EditorServices` calling `configure` before `AddEditorHost` (so the generated registrations win):
    `Assert.Same() Failure: Values are not the same instance, Expected: RecordingDialogs, Actual: EditorDialogs` (1 of 2 failed); green again after.
  No source file differs from eada8ad1 (`git diff eada8ad1 -- src` shows only the three `PackageValidationBaselineVersion` lines).
- Full suite (Release, `dotnet test --solution NetPrints.slnx --ignore-exit-code 8`): 4886 total, 4853 passed, 33 skipped, 0 failed (22 min, Editor.UITests 22 min 35 s);
  build 0 warnings; `dotnet format NetPrints.slnx --verify-no-changes` clean; `eng/trim-warnings.sh` 9. The `NETPRINTS_E2E=1` Desktop run was not repeated: nothing it covers changed.

## Batch D1a (T079-T080)

- **T079 red (assertions)**: `AddSessionGroup`, `RemoveSessionGroup`, `Changed`, `Snapshot` and `OwnerNameOf` were first stubs that did nothing; the 13 new
  `ContributionRegistryTests` ran 14 failed of 73 (`Assert.True() Failure` on remove, `Assert.Throws() Failure: No exception was thrown` on a reused key and on a
  throwing fill, `Assert.Equal() Failure: Collections differ` on the app + session order, `Values differ` on the Changed count). Green after the registry rewrite
  (the 5 left were test faults: `Fill` added a panel, so the Show-panel command appeared; fixed in the test) and the whole `Editor.Tests` project: 2059 passed.
- **T079 design**: `ContributionRegistry` keeps `ContributionGroup`s (the app group, then one per session key). `ContributionSnapshot` (immutable, `IContributionSnapshot`) is
  built lazily from the groups and published by every change; the registry's own lists read the current snapshot, so no consumer changed. `AddSessionGroup(key, populate)`
  fills a group through `IContributionGroupBuilder` (`For(owner, ownerName)` and `AddProjectTemplate`), publishes it once, and drops it when the fill throws.
  Ids and gestures are checked across the groups, so the first registration wins and the built-ins (app group) are always first. Session panels get their Show-panel
  command as the group is published.
- **Decision**: `Freeze()` stays as "ends the initial registration" (adds the Show-panel commands of the app group and publishes the first snapshot) and `IsFrozen` is gone:
  an `Add*` after it is accepted and raises `Changed`. About 30 call sites keep their `registry.Freeze()`, and the "frozen" tests became `FreezeDoesNotStopLaterAdds`.
  A late app panel also gets its Show-panel command.
- **Decision**: the extension's `name` reaches the registry as `For(owner, ownerName)`; `OwnerNameOf(id)` returns it, the id when no name was given, and null for the
  built-in owners (`netprints`, `netprints.editor`), so built-in rows show no owner. The loader that calls `For` with the manifest name comes with T082.
- **Decision**: `Changed` is raised on the thread that adds or removes the group (the UI thread when a project session opens or closes); the surfaces do not marshal.
- **T080 red (assertions)**: with the surface classes (menu bar, command bar, status bar, shell, start page, invoker and the key-binding behavior) restored to their
  pre-T080 form by `git stash`, the 4 `SessionGroupSurfaceTests` ran 4 failed of 4 (`Assert.True() Failure` for the menu item, `Assert.Contains() Failure: Filter not matched in
  collection` for the context menu, `Assert.Equal() Failure: Values differ` for the start page tiles, `Collections differ` for the owner texts); green after the stash pop.
- **T080 design**: `MenuBarViewModel.Menus` and `CommandBarViewModel.Slots` are `ObservableCollection`s rebuilt from the snapshot on `Changed`; `CommandInvoker.CommandsChanged`
  forwards the registry event (subscribed only while someone listens) and `CommandKeyBindingsBehavior` rebuilds its key bindings on it; `ShellViewModel` re-attaches the status bar
  items (`StatusBarViewModel.Attach` keeps the view model of a descriptor it already has); `StartPageViewModel.Tiles` is rebuilt keeping the tile view models that stay;
  the context menus and the templates read the registry on every use. `PaletteItem.Owner` and `ShortcutRow.Owner` are shown beside the label (`AutomationIds.PaletteOwner`,
  `ShortcutsOwner`) and appended to the palette row's accessible description.
- **Deferred**: the palette and the shortcuts sheet read the snapshot when they open; a modal dialog is not rebuilt while it is open. Panels are not in the T080 list, so the
  shell's panel list and the dock layout do not follow a session group yet (a session panel is registered and has its Show-panel command, but only panels present when the
  shell starts are created). Settings pages are a D1 task of their own (T090 onward).
- Full suite (Release, `dotnet test --solution NetPrints.slnx --ignore-exit-code 8`): 4902 total, 4869 passed, 33 skipped, 0 failed (22 min 31 s); build 0 warnings;
  `dotnet format` clean; `eng/trim-warnings.sh` 9.

## Batch D1b (T081-T083, T173)

- **T082 red (assertions)**: `AProjectCopyShadowsAUserCopyWithNpx012NamingBothVersions` (Core.Tests) failed with `Assert.Single() Failure` (the user copy was an NPX004 duplicate);
  `ProjectExtensionGroupTests` (2) failed 2 of 2 on `Assert.Contains() Failure` / `Assert.Single() Failure` before `ProjectLoader` added and removed the session group;
  `AProjectCopyThatShadowsAUserCopyIsReportedAsInfoNpx012NamingBothVersions` failed with `Assert.Equal() Failure: Expected: Info, Actual: Error`. The two
  `ExtensionContributionRecorderTests` were written after `ExtensionContributionRecorder` (no red run).
- **T082 design**: `ExtensionContributionRecorder` (Hosting) is the `EditorContributions` factory of the `ExtensionHost`: it records each extension's facet calls (the latest load of an id
  wins). `ApplyToAppGroup` replays the application-level kinds of the extensions loaded at start once, from `ContributionRegistry.Create`; `ProjectLoader` adds one session group per
  open project (key = project file path, replaced on reopen, removed on close) filled by `PopulateSessionGroup`: project-level kinds of every loaded extension, and every kind of an
  extension loaded from one of the project's folders. `EditorHostServices.Contributions` and `EditorContext.Contributions` carry the recorder; Desktop creates it.
- **Decision**: application-level kinds are tiles, settings pages, dialogs, view resources and `Global`-scope commands; project-level kinds are panels, status bar items, context-menu
  items, tooltip and go-to providers and commands of any other scope (Clarifications answer 2 names the kinds loosely; the scope is the only signal a command carries).
  Project templates, node search providers and document migrations are not in the groups: they already follow `IExtensionHost.Current`.
- **Decision**: closing a project now reloads the extensions without the project's folders (`CloseProjectAsync`), so a project's node kinds, templates and providers go with it too (SC-004).
- **Decision**: a project extension whose id also loads from a user directory: the loader reports the user copy as `NPX012` (new `ExtensionDiagnosticCodes.Shadowed`, shown with Info severity,
  naming both versions) instead of NPX004; the project's copy puts every kind in the session group, so a duplicate id with the user copy's app-level item is an NPX004 issue (first wins).
- **T173 red (assertions)**: with `ViewResourceLoader.Sync` a stub returning nothing, the 3 `ViewResourceLoaderTests` failed 3 of 3 (`Assert.Equal() Failure: Expected: 1, Actual: 0`, `Collection: []`).
  `Sync` keeps one entry per URI per application: it loads new URIs (`AvaloniaXamlLoader.Load` for an `avares://` URI of an extension's assembly, the compiled table for the built-in views),
  removes the ones no longer declared, and reports (`ViewResourceFailure`, logged as 1042) a URI that resolves to nothing or to a document that is none of `DataTemplates`, `ResourceDictionary`
  and `Styles`, once per URI. `ShellHost` syncs on every registry change. `Apply` is gone.
- **Deviation**: the T173 tests are in `tests/NetPrints.Editor.UITests/Hosting/` (they need the headless application), with a new fixture `Fx.Views` (compiled `Templates`, `Icons`, `Styles`
  and a `UserControl` view). The compiled XAML of an extension assembly loads through the extension's load context without extra work.
- **T081 red (assertions)**: `FailureIsolationTests` 5 failed of 6 before the guard (the exception escaped as `InvalidOperationException`, the unknown reference was not an issue, the Errors
  dialog was empty); `ContributionErrorViewTests` failed with `Assert.NotEmpty() Failure: Collection was empty` with the placeholder template removed.
- **T081 design**: `ContributionGuard` (Contributions) wraps, when an extension (not a built-in owner) registers: command handlers (`ExecuteAsync` rethrows `ExtensionFailureException` naming the
  extension and the command, which the invoker's fault callback shows in the error dialog; `CanExecute`, `DynamicLabel`, `IsChecked` log and answer false/null), tooltip and go-to providers
  (empty result), view model factories of panels, tiles, status bar items, dialogs and settings pages (a `ContributionErrorViewModel`, drawn by `ContributionErrorView` in `Controls/` through a
  `BuiltInViews` template; the Contributions namespace stays UI-free) and the `AppliesTo` predicates (false). Provider, factory and predicate failures log NPX011 once per contribution and registry (event 1071); handler failures
  log 1072 each time. Built-in command handlers (`IEditorCommandHandler`) are never wrapped, so `ShowPanelCommandHandler` detection keeps working.
- **Decision**: a view template that throws while it builds (the compiled view of an extension, not its view model factory) is not wrapped: Avalonia builds templates inside its own layout
  pass. Only the factories the registry calls are guarded.
- **Decision**: registry issues reach the Errors dialog (`ReportExtensionFailuresAsync`, once each) as warnings: `DuplicateId` is NPX004, `GestureConflict` NPX009 (new constant),
  new `ContributionIssueKind.UnknownReference` NPX010 (a context-menu item naming a command nothing registered, a settings page with an unknown parent; computed per snapshot for extension-owned
  items only, and logged once),
  anything else NPX006. NPX005 was already shown there. NPX011 is added to `ExtensionDiagnosticCodes` and the diagnostics guide; the 0.4 hash row in `ExtensionApiVersionTests` is updated (not shipped).
- **T083**: `NetPrints.TestExtension` contributes a command (with an icon and a File menu entry), panel, tile, project template, context-menu item, tooltip provider, go-to provider, status bar item,
  settings page, dialog, icon dictionary (`TestIcons.axaml`, compiled with the `Avalonia` package reference, omitted from the packaged-verification build), node search provider and a document migration
  (of a new node kind `netprints.test/Legacy`, data version 1, so the document goldens of `netprints.test/Log` stay as they were; the node search provider answers only in constructor graphs for
  the same reason; three Core.Tests assertions that counted the extension's node kinds now name them). Red (assertions): `ExtensionKindsTests` failed with `Expected: ["command", "panel", ...] Actual: []` with the registrations removed; green with them.
  The icon is checked headless in `ExtensionIconSurfaceTests`. `SessionPanelTests` (2) failed 2 of 2 before the panel gap was closed: `ShellViewModel` now adds and removes `PanelViewModel`s on registry
  changes (`PanelsChanged`), and `DockShellAdapter` docks an added panel at its default place and removes a removed one from the layout and the hidden list. A saved layout that names a panel of an
  extension that is not loaded drops it (existing mapper behaviour) and the panel docks at its default place when it loads (T082 edge case 2).
- **Decision (dialog gap)**: the palette and the shortcuts sheet still read the snapshot when they open and are not rebuilt while open; with the editor modal dialogs that only matters if a project
  loads while one is open, which the unsaved-changes flow does not allow. Left as is.
- **Fix for the trim ratchet**: `AvaloniaXamlLoader.Load` is the one new IL2026 in the Editor; two `JsonArray.Add<T>` warnings in `NetPrintsJsonSchema` were removed with a non-generic overload
  and the baseline in `eng/trim-warnings.txt` is now 8.

## Batch D1c (T084, T085, T175)

- **T084 red (assertions)**: `StartupProfileTests` ran 5 failed of 6 before the code (`Assert.True() Failure` on `IsFiltered`, `Collections differ` on the template ids, no start page message, the project not opened
  because the three arguments were not a single path). Green after `StartupArguments` (parses `--profile <id>` and `--profile=<id>`; the one positional argument left is the project), `ProjectLoader.StartupProfileId`
  and the filter in `NewProjectDialogViewModel` (`IsFiltered`, `ShowAll`, `ProfileNote`; the dialog shows a note and a "Show all templates" check box). The E2E scenario `StartWithProfileTests` was written with the
  code (no red run) and passes on the editor under Xvfb.
- **Decision**: an unknown id (or `--profile` with no id) sets `StartPageError` (the start page's message line) naming the id and the known ids; the dialog stays unfiltered. With a project path the project opens and the
  status bar says the profile was ignored, shown after the load so the "Loaded" status does not replace it. The profile is held only by the loader of the session; nothing is written to the state files.
  A known profile without templates leaves the list empty until Show all.
- **T085 Dock**: Dock#1191 is in 12.1.0.10 (merged 2026-10-16 04:29 UTC, release 2026-10-16 19:28 UTC; 12.1.0.12 is the latest). Dock 12.1.0.10 needs Avalonia 12.1.4, so Avalonia, Desktop, Themes.Fluent, Fonts.Inter,
  Skia and Headless went from 12.1.3 to 12.1.4 in `Directory.Packages.props`; Headless.XUnit stays 12.1.3. The workaround is gone: `ShellDocument`/`ShellTool` constructors and the empty `DockCapabilityPolicy` /
  `DockCapabilityOverrides` of every dock in `ShellDockFactory`. `AnEmptyCapabilityObjectStillInheritsSoDragDropAndCloseStayAllowed` keeps the drag, drop and close assertions with null capability objects.
  The binding-warning tests (`ShellAdapterTests`, an empty list of warnings) stay green without the workaround. With 12.1.4 a floated host window is no longer the active window after the main window hides in
  the headless platform, so `DialogsAreOwnedByTheActiveDockHostWindow` activates the host window explicitly. `.specify/memory/roadmap.md` (line about the bump) is governance; not edited.
- **T085 tests (no red)**: `StartupWarningsTests` (E2E: no `IsPinned`, no "Value is null", no binding warning in the editor output after start and showing a panel) and
  `HoveringTheExactMiddleOfACableAtTheFitZoomHitsItAndGivesItsTooltip` (headless, after `fitAll`) both passed on the first run, before any fix, with Dock 12.1.0.6; they pin the behaviour. The `IsPinned`
  warning did not show in this flow with either Dock version. The upstream `IsPinned` issue was not filed. `ShellMainFlow` E2E: 1 of 1 passed (83 s) at this head.
- **T175 red (assertions)**: `FactoryServiceProviderTests` failed 2 of 3 (`Assert.NotNull() Failure: Value is null` for the panel and the tile service) before the change. `ShellViewModel.ExtensionServicesOf(id, snapshot)` gives the
  provider of the extension that owns a contribution (null for built-ins and without a host); panel factories get a `ServicesContext` over the editor's context with it, tile factories a `StartPageContext.WithServices` copy.
  Another extension's factory resolves nothing of the first's services; built-ins keep the editor's context (`EmptyServiceProvider`).

## Batch D1d (T086, T087, T088)

- **D1c follow-ups (18d9934c)**: `THIRD-PARTY-NOTICES.md` now lists Avalonia.Fonts.Inter 12.1.4 and names Irihi.Ursa and Irihi.Ursa.Themes.Semi one by one. `BrandAssetTests.TheThirdPartyNoticesListThePinnedPackageVersions`
  checks the five packages against `Directory.Packages.props`; red 2 of 5 (`Assert.Matches() Failure` for `Irihi.Ursa` and `Avalonia.Fonts.Inter`), green after the edit. The roadmap line about Dock#1191 now says it shipped in 12.1.0.10.
- **T086 red (assertions)**: four of the six new `JsonFileSettingsStoreTests` failed against stubs (`OnSectionChanged` did nothing, no copy, `schemaVersion` always written as 1): the unreadable copy, the newer `schemaVersion`, the
  subscriber notification and the `IOptionsMonitor<T>.OnChange` bridge. Two passed at once and pin existing behaviour (a valid file is never copied; a failed write leaves the old value because the state is replaced only after the
  write). Green: an unparseable or non-object file is copied to `settings.json.unreadable-<yyyyMMddHHmmss>` (UTC, from an injected `TimeProvider`) before the first write, once; a `schemaVersion` above 1 logs 2014 once and is written
  back unchanged; `ISettingsChangeSource.OnSectionChanged` (implemented by `JsonFileSettingsStore`) notifies after a persisted write, never for a failed one, outside the write gate; the options monitor of an extension section subscribes
  through it. New log events 2014 to 2016. The API hash row of 0.4 changed (not shipped).
- **Decision (copy only for unparseable content)**: a file that cannot be read for IO or permission reasons is not copied (the copy would fail too); only invalid JSON and a non-object root are.
- **T088 red (assertions)**: 9 of the new `SettingsValidationTests` failed and `AnInvalidExtensionSectionUsesItsDefaultsAndShowsNpx014Once` failed against stubs with no ranges and no store support. Green after: `RunSettings`
  (`run.stopTimeoutSeconds` 1 to 600, `run.linuxTerminal`), `EditorSettings.DarkPreset`/`LightPreset`, `JsonFileSettingsStore.Issues` and `ExtensionDiagnosticCodes.SettingsInvalid` (NPX014).
- **Decision (run is a top-level section)**: `run` is stored at the top level of the file, as the contract shows, so the store knows two top-level sections (`netprints`, `run`); `keyboard` joins them in D2. `RunSettings` is not registered
  through `AddSettings` because the built-in extension has one descriptor; the composition reads it so an invalid value logs before the window exists.
- **Decision (linuxTerminal range)**: the spec gives no range, so a value is valid when it has no control character (including a newline) and at most 512 characters. Detection of the terminal binary is the run batch's concern.
- **Decision (preset ids)**: the validator rejects an id that is empty or not lowercase letters, digits and `.`, `_`, `-` (`^[a-z0-9]+([._-][a-z0-9]+)*$`), naming the key. A well-formed id that no theme provides is NOT a validation failure: the
  contract (FR-069) wants the variant's default with a log entry while the other appearance keys stay, which T096 does because only it knows the loaded themes. The keys carry `[SettingChoices("netprints.presets.dark|light")]`.
- **Decision (NPX014 surface)**: a section other than `netprints`, `run` and ids starting with `netprints.` (the editor's own) that fails validation gets an entry in `JsonFileSettingsStore.Issues` with code NPX014 and the log message
  starts with the code; built-in sections get an issue without a code. The Settings document (T089/T090) shows `Issues`.
- **T087 red (assertions)**: `SettingsSchemaTests` (Core) `RangeWritesMinimumAndMaximum` and `LengthAndPatternAttributesWriteTheirKeywords` failed (`Actual: null`); in Editor.Tests 6 of 9 `Docs.SettingsSchemaTests` failed (missing golden,
  stale table, no restart/path keywords, no descriptions). Green after `SettingsSchema` wrote `minimum`/`maximum`, `minLength`/`maxLength` and `pattern` from the DataAnnotations and used `TreatNullObliviousAsNonNullable`, the built-in
  properties got `[Description]`, `[RequiresRestart]` and `[SettingPath]`, and the golden and table were written with `NETPRINTS_UPDATE_SNAPSHOTS=1` (same switch as the other goldens).
- **T087 layout**: the generator is test code (`tests/NetPrints.Editor.Tests/Docs/SettingsSchemaFile.cs`, like `ShortcutGuide`); `schemas/settings.v1.schema.json` is checked by `eng/validate-schemas.sh` (metaschema, lint, and the tracked
  `tests/Fixtures/Settings/example.settings.json`; run locally, 53 instances pass) and copied by `scripts/build-docs.sh`. `docs/guide/editor/settings.md` holds only the generated table between markers; T091 writes the rest.
  Descriptions end without a period because the schema lint requires it. The `x-netprints-*` keywords appear in the file through restart (language, extensionPaths), path (extensionPaths, trustedProjects) and choices (presets).

## Batch D1-CP (T098, Checkpoint D1)

- Suite (Release, solution-wide, `--ignore-exit-code 8`, at 9e8aacfa): 5231 total, 5195 passed, 36 skipped, 0 failed (UITests 16 min 07 s, Editor.Tests 2 min 19 s,
  Cli.Tests 53 s, Catalog.Tests 62 s, Core.Tests 45 s, Analyzers.Tests 7 s). E2E (`NETPRINTS_E2E=1`, `--fail-skips on`, display start 300, own `NETPRINTS_STATE_DIR` and
  `XDG_CONFIG_HOME`): 56 total, 55 passed, 1 skipped (`GuideScreenshotTests`, explicit filtering, as at C1), 0 failed, 9 min 40 s. `dotnet format --verify-no-changes` clean; trim ratchet 8 of 8.
- CI (run 39173174355, green at e2c0a31d): UI shards 1/4 7 min 41 s, 2/4 5 min 44 s, 3/4 8 min 57 s, 4/4 6 min 11 s; longest job Desktop E2E (Linux, Xvfb) 12 min 17 s, then perf 11 min 1 s.
  No UI shard is near 12 minutes, so no fifth shard (R28). The CI run for 9e8aacfa was still running when this was written.
- Gate D1. SC-004: `ExtensionKindsTests` (T083, every public kind found on its surface, none after the project unloads) plus the failure fixtures of D1b: pass. SC-006: the settings store
  tests (T086), the page-reachability and restart-survival tests (T090) and the effective-gesture tests (T092): pass. SC-007: `RebindShortcutsTests` E2E (rebind a built-in and an extension
  command, resolve a conflict, restart, run both): pass in the E2E run above. SC-009: `ContrastMathTests`, `ThemePresetsTests`/`ThemePresetTests` (every preset passes every gated pair, a Dark preset
  changes 0 Light tokens, no restart): pass. Dock check: the pin is 12.1.0.10; 12.1.0.11 and 12.1.0.12 are released (12.1.0.12 on 2026-10-17), not evaluated or adopted here.
- Open for the D1+D2 review: `ContrastCheck.Passed` rounds the ratio to 2 decimals, so the mandated Classic card (`#AA202020` over `#000`, ratio 1.1497) passes the 1.15 gate. Flagged, not changed.
- Docs: the settings, themes and shortcuts guides and the diagnostics guide (NPX014) were already current; the release notes (`.github/release-notes.md`, Unreleased) gained the Settings document,
  rebindable shortcuts and theme presets entries.
