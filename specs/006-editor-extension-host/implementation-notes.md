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

## Pack spike

(T026)

## Publish gate

(T073)

## NuGet spike

(T158)

## Checkpoint reports

(T016, T037, T057, T076, T098, T110, T126, T133, T170, T169; the SC table is T152 in PR-P)

## Reviews

(Review A+B: T038; Review C1+C2: T077; Review D1+D2: T111; Review E+F+G: T153; Final review with Review H: T155)
