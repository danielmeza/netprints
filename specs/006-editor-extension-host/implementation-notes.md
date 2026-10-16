# Implementation notes: Editor Extension Host (P3)

Decisions, measurements and checkpoint reports of the P3 batches, in the order they happen. Each batch adds its own
entries; a decision is written as "Decision: …" with its reason. Tasks are in [tasks.md](tasks.md).

## Batch P3-S2c (tasks and analysis)

- Decision: batches are labelled by sub-phase (A1–A5, B1–B7, C1a–C1f, C2a–C2f, D1a–D1g, D2a–D2d, E1–E5, F1–F2,
  G1–G5), with letters inside C1, C2, D1 and D2 so a batch label never reads as a sub-phase name.
- Decision: a test-first task commits its red test together with the change that makes it green, so every commit
  builds and passes (bisect); both runs are recorded here.
- Decision: checkpoint tasks carry no units of their own, except T133, which also applies the scope rule.
- Decision: the NPS rules (T004) land inactive in the repository build and are switched on together with the sweep
  (T005), so no commit fails the build.
- Decision: the guide and ADR text of each feature lands in the sub-phase that builds it; sub-phase G keeps only the
  release notes, the guide images, the quickstart run and the docs site check.

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

## R5-0 baselines

(T006)

## M5 spike

(T007)

## Pack spike

(T026)

## Publish gate

(T073)

## Checkpoint reports

(T016, T037, T057, T076, T098, T110, T126, T133, T152)

## Reviews

(T017, T038, T058, T077, T099, T111, T127, T134, T153, T155)
