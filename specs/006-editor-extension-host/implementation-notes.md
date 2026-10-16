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
| Pin labels with no resource | the raw key | T043 |
| Node categories | one "NetPrints" category for the built-ins | T044 |
| Live language switch | no; a language change applies after a restart | T046 |
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

(T073)

## NuGet spike

(T158)

## Checkpoint reports

(T016, T037, T057, T076, T098, T110, T126, T133, T170, T169; the SC table is T152 in PR-P)

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
- AB-5 minor `.github/release-notes.md:3`: typed getters, sealed node classes and removed overrides missing; the shipped
  0.3.0 section stays in the release body (`release.yml:214`).
- AB-6 minor `src/NetPrints.Serialization/Documents/UnknownMembers.cs:14`: the reference records (`Refs.cs:18-82`) drop
  unknown members.
  Fixed in 927f836e (B-F1): the six reference records carry `ExtensionData`, so `format`, `show` and `merge` keep what is
  inside them. The editor's save still writes references from the model's own types, so it drops them there; FR-007,
  data-model section 7, ADR-0038 decision 10 and the graph-format guide now say so (the T012 note above that excluded the refs
  because they are compared as keys was wrong: `UnknownMembers` has value equality).
- AB-7 minor `specs/006-editor-extension-host/spec.md:654`: recorded deviations not amended (13/2 counts, S0 golden, IL2057,
  `default(T)` resets, NPI003 wording, NodePins line format).
- AB-8 minor `docs/adr/0029-node-capabilities-and-pin-roles.md:44`: the overload button is unreachable through
  `IOverloadableNode` (`NodeViewModel.cs:230`, `:240-243`); the role interfaces have no stability class (R16).
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
- AB-12 nit `src/NetPrints.Authoring/Pins/PinKeyTrackingAnalyzer.cs:50`: tracking silently off without the NodePins files.
  Fixed in 5d8dfe62 (B-F2): new NPA012 (info) on a `[NodeKind]` class with declared pins when neither file exists; ADR-0025,
  the contract table and the guide say so (red: the old "nothing is tracked" test now expects NPA012).
- AB-13 nit `src/NetPrints.Editor/NodeSearch/SuggestionRanker.cs:23`: the This Methods / This Variables direction is not in
  "Owner questions taken by default".
- AB-14 nit `src/NetPrints.Authoring.CodeFixes/Pins/AddNodeMarkerCodeFix.cs:51`: the NPA009 fix writes the class name as
  the kind id.
  Fixed in 828f2314 (B-F2): the fix writes `[NodeKind("TODO.kindId")]` and its title says the id is a placeholder (guide row
  reworded in 5d8dfe62); red on the fixed-text assertion.
- AB-15 nit `src/Shared/Roslyn/DiagnosticHelpLinks.cs:12`: help links point to `#npaNNN` anchors the guide lacks.
  Fixed in 5d8dfe62 (B-F2): one `<span id>` per help-linked NPA, NPI and NPS code above its table; `DiagnosticsGuideTests`
  checks every help-linked code has its anchor (red on 22 missing anchors).
- AB-16 nit `specs/006-editor-extension-host/implementation-notes.md:515`: red-first for the sbyte/decimal defect was a
  compile failure (also B2, :386).
- AB-17 nit `docs/adr/README.md:51`: leftovers (reserved-number sentence, ADR-0003:393 date, trim baseline at :552, checkpoint
  A longest job).
- AB-18 nit `src/NetPrints.Core/Graph/CallMethodNode.cs:261`: an out/ref parameter named `Exception` takes the exception
  pin's stored key.
  Fixed in c11763f8 (B-F2): the exception pin is found by its role, and an out/ref pin named `Exception` is keyed
  `out.data.param:Exception`; the test failed because the exception pin was never added next to such a parameter. A
  parameter named like a return pin (`Boolean`) keeps `Boolean~2` (stored keys stay stable); said in the commit, not changed.
- AB-19 nit `src/NetPrints.Core/Graph/ForLoopNode.cs:9`: kind ids repeat the `BuiltInNodeKinds` literals (a test ties them).
  Fixed in c11763f8 (B-F2): `NodeKindIds` in Core, `[NodeKind]` uses it, `BuiltInNodeKinds` forwards it; a hygiene test rejects a
  literal (red on the assertion).
