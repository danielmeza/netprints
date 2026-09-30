---
description: "Task list for P2 — Catalog tooling and Spectre CLI"
---

# Tasks: Catalog Tooling and Spectre CLI

**Input**: `specs/004-catalog-cli/` — plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Start condition**: met. Branch `004-catalog-cli` from `master` at `7fba032` (= `v0.1.1`). ADR-0010 (accepted) and
ADR-0012–ADR-0016 are committed with this spec.

**Tests**: REQUIRED (constitution V). A task that names a test id (`CL-`, `CT-`, `AN-`, `GI-`, `MX-`, `AP-Txx`)
implements exactly the case in that contract's obligation table. "Test first" means: write the test, run it, see it
fail for the expected reason, then implement in the same batch; never push a red test. Every test is xUnit v3 and
passes `TestContext.Current.CancellationToken`. Snapshots and goldens change only with
`NETPRINTS_UPDATE_SNAPSHOTS=1`, reviewed by hand, and the commit says why.

**Rules for the implementer** (plan.md "Standing constraints" and AGENTS.md "Batch rules"): one batch per agent;
read only the task text, the contract sections it names and the code you touch; build quietly
(`dotnet build -v q -tl:off --nologo`), test after building (`dotnet test --no-build --no-progress --no-ansi`),
filter while iterating, whole suite once at the end of the batch
(`dotnet test --solution NetPrints.slnx -c Release --no-build --no-progress --no-ansi -- --ignore-exit-code 8`), then
`dotnet build -c Release` (0 warnings) and `dotnet format NetPrints.slnx --verify-no-changes`. No `!`, no
suppressions, XML docs on public API, `PublicAPI.Unshipped.txt` updated with every public API change of a tracked
library (from batch B1 on). Commit per task (pathspec of your own files), tick the task here, write decisions to
`specs/004-catalog-cli/implementation-notes.md` as "Decision: …", push. Never leave changes under `samples/`. No
publishing (tags, NuGet, wiki, SchemaStore, upstream repos). Every batch must pass Linux CI before the next starts.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel with other [P] tasks of the same batch (different files, no dependency).
- **[Story]**: US1–US6 from spec.md. Setup and polish tasks have no story label.
- **Batch header**: `Batch <id> — model: <haiku|sonnet> — T<first>–T<last>`; one agent per batch, batches run in
  order.

---

## Phase 1: Setup (sub-phase A)

**Purpose**: projects, packages and CI wiring that every later batch needs.

### Batch A1 — model: haiku — T001–T005

- [ ] T001 Add central versions to `Directory.Packages.props`: `Spectre.Console.Cli` 0.57.2, `Spectre.Console.Testing`
  0.57.2, `Microsoft.Extensions.DependencyInjection` 10.0.12, `Microsoft.CodeAnalysis.PublicApiAnalyzers` 5.6.0 and
  `Microsoft.CodeAnalysis.CSharp` 5.9.0 (the version the Workspaces 5.9.0 dependency already pins; the generator
  overrides it). Keep `CommandLineParser` until T018; confirm `SkiaSharp.NativeAssets.Linux.NoDependencies` is listed.
- [ ] T002 Create `src/NetPrints.Catalog/NetPrints.Catalog.csproj` (net10.0, `IsPackable`, `EnablePackageValidation`,
  `PackageId`, `Description`, `ProjectReference` to `NetPrints.Reflection`) and
  `src/NetPrints.Annotations/NetPrints.Annotations.csproj` (netstandard2.0, `IsRoslynComponent`,
  `EnforceExtendedAnalyzerRules`, `IncludeBuildOutput=false`, `DevelopmentDependency=true`, `IsPackable`,
  `Microsoft.CodeAnalysis.CSharp` with `VersionOverride="5.0.0"` and `PrivateAssets="all"`, pack item for
  `$(TargetPath)` → `analyzers/dotnet/cs`); add both to `NetPrints.slnx`.
- [ ] T003 Create `tests/NetPrints.Catalog.Tests/NetPrints.Catalog.Tests.csproj` and
  `tests/NetPrints.Cli.Tests/NetPrints.Cli.Tests.csproj` (copy the shape of
  `tests/NetPrints.Core.Tests/NetPrints.Core.Tests.csproj`: xUnit v3, Exe, MTP; Catalog.Tests references
  `NetPrints.Catalog`, Cli.Tests references `NetPrints.Cli`), each with one passing smoke `[Fact]`; add
  `<InternalsVisibleTo Include="NetPrints.Cli.Tests" />` to `src/NetPrints.Cli/NetPrints.Cli.csproj`; add both to
  `NetPrints.slnx`.
- [ ] T004 In `.github/workflows/ci.yml` add steps "Test (Catalog)" and "Test (CLI)" after "Test (Core)" with the same
  flags; create `specs/004-catalog-cli/implementation-notes.md` with the headings "Decisions", "Checkpoint reports",
  "Deviations", "Governance proposals".
- [ ] T005 Build and run the whole suite; write the Checkpoint A report in
  `specs/004-catalog-cli/implementation-notes.md`; commit. **Checkpoint A**: solution builds with 0 warnings, suite
  green, CI green on the draft PR.

---

## Phase 2: User Story 6 — API tracking (sub-phase B, Priority P3, ordered first)

**Goal**: every later public API change is a reviewed diff; unstable API is opt-in (FR-044, FR-045).
**Independent test**: AP-T01–AP-T03 pass; SC-011. Ordered before the P1 stories because every later batch adds
tracked API.

### Batch B1 — model: sonnet — T006–T009

- [ ] T006 [US6] Test first (AP-T01): `tests/NetPrints.Core.Tests/Architecture/PublicApiTrackingTests.cs` — each project
  with `NetPrintsTrackPublicApi=true` (Extensibility, Core, Reflection, Serialization, Catalog) has
  `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` whose first line is `#nullable enable`; an in-test Roslyn
  compilation running the PublicApiAnalyzers analyzer (path from `$(PkgMicrosoft_CodeAnalysis_PublicApiAnalyzers)`,
  `GeneratePathProperty="true"` on a test-only `PackageReference`) reports RS0016 for an undeclared public type.
- [ ] T007 [US6] In `src/Directory.Build.props`, when `'$(NetPrintsTrackPublicApi)' == 'true'`, reference
  `Microsoft.CodeAnalysis.PublicApiAnalyzers` (`PrivateAssets="all"`) and add both files as `AdditionalFiles`; set the
  property in `src/NetPrints.{Extensibility,Core,Reflection,Serialization,Catalog}/*.csproj`; create the ten files.
- [ ] T008 [US6] Fill the files with the analyzer's code fix (`dotnet format analyzers src/<P>/<P>.csproj --diagnostics
  RS0016 RS0037 --severity info`; if the fixer does not apply, use the IDE's fix-all or list the API of the built
  assembly with a throwaway script, and say which in implementation-notes.md); move every Core and Reflection entry to `PublicAPI.Shipped.txt` (their API equals
  `v0.1.1`; confirm `git diff v0.1.1 -- src/NetPrints.Core src/NetPrints.Reflection` shows no public API change);
  Extensibility, Serialization and Catalog keep everything in Unshipped.
- [ ] T009 [US6] Fix every other new analyzer diagnostic (RS0026, RS0027, RS0041, …) with a real API change, never a
  suppression; note each in implementation-notes.md; whole suite; commit.

### Batch B2 — model: sonnet — T010–T014

- [ ] T010 [US6] Test first (AP-T02, AP-T03): `tests/NetPrints.Core.Tests/Architecture/ExperimentalApiTests.cs` (for
  `NPXE0001`–`NPXE0003`: an in-test external compilation referencing the built assemblies and using one marked API
  reports that id as an error, and compiles with the id suppressed; `NPXE0004` is added in T037); update
  `tests/NetPrints.Core.Tests/Core/SourceHygieneTests.cs` so the `<NoWarn>` gate allows exactly the
  `Directory.Build.targets` opt-in line and asserts `NetPrintsExperimentalOptIn` holds only `NPXE\d{4}` ids.
- [ ] T011 [US6] `Directory.Build.props`: `<NetPrintsExperimentalOptIn>NPXE0001;NPXE0002;NPXE0003;NPXE0004</NetPrintsExperimentalOptIn>`;
  `Directory.Build.targets`: `<NoWarn>$(NoWarn);$(NetPrintsExperimentalOptIn)</NoWarn>`; add a row to the suppression
  ledger in `docs/adr/0003-analyzer-promotion-and-string-literal-discipline.md` pointing to ADR-0010 §3.
- [ ] T012 [US6] Add `src/NetPrints.Extensibility/ExperimentalApis.cs` (internal constants `HostChannel = "NPXE0001"`,
  `Settings = "NPXE0002"`, `Emitters = "NPXE0003"`, `CatalogProfiles = "NPXE0004"`, `UrlFormat`) and
  `src/NetPrints.Core/Translator/Extensibility/ExperimentalApis.cs` (`Emitters`, `UrlFormat`); mark with
  `[Experimental]`: `IHostChannel`, `IHostChannelFactory`, `HostMessage`, `IExtensionBuilder.AddHostChannel` (0001);
  `ExtensionSettingsDescriptor`, `ISettingsStore`, `IExtensionBuilder.AddSettings` (0002); `IClassEmitter`,
  `IMemberEmitter` in `src/NetPrints.Core/Translator/Extensibility/Emitters.cs`, `IExtensionBuilder.AddClassEmitter`,
  `AddMemberEmitter` (0003).
- [ ] T013 [US6] `docs/guide/extensions.md`: new section "API stability" (anchor `api-stability`): the four ids, what
  each covers, how an extension opts in (`<NoWarn>$(NoWarn);NPXE0001</NoWarn>`), and the tracked API files.
- [ ] T014 [US6] Whole suite, release build, format check; Checkpoint B report in
  `specs/004-catalog-cli/implementation-notes.md`; commit. **Checkpoint B** (SC-011).

---

## Phase 3: User Story 1 — one CLI for every project workflow (sub-phase C, Priority P1) 🎯 MVP

**Goal**: Spectre-based `netprints` with `build`, `run`, `generate`/`regen --check`, `migrate`, exit-code contract
(FR-001–FR-011, contracts/cli.md). **Independent test**: CL-T01–CL-T14; SC-001; SC-002 (run and regen parts).

### Batch C1 — model: sonnet — T015–T018

- [ ] T015 [US1] Test first: `tests/NetPrints.Cli.Tests/CliExitCodeTests.cs` (CL-T01, CL-T02, CL-T07, CL-T10, CL-T12)
  against `CliApplication.RunAsync` with Spectre's `TestConsole`.
- [ ] T016 [US1] Rewrite `src/NetPrints.Cli/Program.cs`; add `src/NetPrints.Cli/CliApplication.cs`, `CliServices.cs`,
  `ExitCodes.cs`, `Infrastructure/TypeRegistrar.cs`, `Infrastructure/TypeResolver.cs` (namespace `NetPrints.Cli`;
  P1-flag pre-check; `SetApplicationVersion("NetPrints.Cli " + informational version)`; exception handler: parse and
  validation → 2, other → 4; stderr logging with `--verbose`); in `src/NetPrints.Cli/NetPrints.Cli.csproj` replace
  `CommandLineParser` with `Spectre.Console.Cli` and `Microsoft.Extensions.DependencyInjection`.
- [ ] T017 [US1] Test first `tests/NetPrints.Cli.Tests/Infrastructure/ProjectLocatorTests.cs` (CL-T03); add
  `src/NetPrints.Cli/Infrastructure/ProjectLocator.cs` and `Infrastructure/ProjectCommandBase.cs` (project resolution;
  SDK check through an `IMsBuildRegistration` seam over `MsBuildRegistration.EnsureRegistered`, `NoInlining` split kept).
- [ ] T018 [US1] Move `tests/NetPrints.Core.Tests/Projects/CliBuildTests.cs` to
  `tests/NetPrints.Cli.Tests/Commands/BuildCommandTests.cs` keeping every case; remove `CommandLineParser` from
  `Directory.Packages.props` and the `NetPrints.Core.Tests` `InternalsVisibleTo` from the CLI project; whole suite;
  commit.

### Batch C2 — model: sonnet — T019–T022

- [ ] T019 [US1] Test first: extend `tests/NetPrints.Cli.Tests/Commands/BuildCommandTests.cs` (CL-T04, CL-T06); add
  `tests/NetPrints.Cli.Tests/Commands/RunCommandTests.cs` (CL-T05) and
  `tests/NetPrints.Cli.Tests/Commands/MigrateCommandTests.cs` (CL-T09).
- [ ] T020 [US1] `src/NetPrints.Cli/Commands/BuildCommand.cs` and `Commands/RunCommand.cs` (arguments after `--` from
  `CommandContext.Remaining.Raw`; child exit code returned).
- [ ] T021 [US1] `src/NetPrints.Cli/Commands/MigrateCommand.cs` (files, directories or a project's graphs; reads
  `schemaVersion` with `Utf8JsonReader`; compares with `DocumentMigrator.CurrentSchemaVersion`; writes nothing).
- [ ] T022 [US1] `.github/workflows/ci.yml`: "CLI smoke" runs `--version` and `--help` and asserts rc 0 (replace the
  comment about exit 2); "CLI sample compile and run" uses `-- run samples/HelloWorld/HelloWorld.csproj`; whole suite;
  commit.

### Batch C3 — model: sonnet — T023–T027

- [ ] T023 [P] [US1] Test first: `tests/NetPrints.Core.Tests/Projects/GenerateRequestFactoryTests.cs` (CL-T13) and
  `tests/NetPrints.Core.Tests/Projects/GenerationModeTests.cs` (CL-T14).
- [ ] T024 [US1] Add `src/NetPrints.Generation/GenerateRequestFactory.cs` (`FromSnapshot(ProjectSnapshot)`); in
  `src/NetPrints.Generation/GraphCodeGenerator.cs` add `GenerationMode { Write, Check }`, a mode parameter on
  `GenerateAsync` (default `Write`) and `GeneratedFileResult.UpToDate`; `src/NetPrints.Generator/Program.cs` keeps
  `Write`.
- [ ] T025 [US1] Test first `tests/NetPrints.Cli.Tests/Commands/GenerateCommandTests.cs` (CL-T08, real SDK, temp copy
  of `samples/HelloWorld`); add `src/NetPrints.Cli/Commands/GenerateCommand.cs` (alias `regen`, `--check`, `--graph`).
- [ ] T026 [US1] `tests/NetPrints.Cli.Tests/EndToEnd/HelloWorldCliTests.cs` (CL-T11); `.github/workflows/ci.yml` new
  step "Graph checks" running `netprints regen --check samples/HelloWorld`.
- [ ] T027 [US1] Whole suite; batch C3 results in `specs/004-catalog-cli/implementation-notes.md`; commit.

### Batch C4 — model: haiku — T028–T030

- [ ] T028 [P] [US1] New `docs/guide/cli.md`: global options, each command with synopsis and example, the exit-code
  table, CI recipes (`regen --check`, `format --check`), the removed P1 flags (content: contracts/cli.md §1–§4).
- [ ] T029 [P] [US1] Update `docs/guide/install.md` ("Command-line tool": new commands), `docs/guide/projects.md`
  (`netprints generate` / `regen --check`), and the CLI section of `README.md`.
- [ ] T030 [US1] Whole suite and `scripts/build-docs.sh`; Checkpoint C report in
  `specs/004-catalog-cli/implementation-notes.md`; commit. **Checkpoint C** (SC-001; SC-002 run and regen parts).

---

## Phase 4: User Story 2 — catalog any library with the tool (sub-phase D, Priority P1)

**Goal**: `NetPrints.Catalog` engine, schema v1, profiles, run-time catalogs, `netprints catalog` (FR-012–FR-024,
contracts/catalog.md). **Independent test**: CT-T01–CT-T19; SC-004, SC-005 (extension path), SC-006.

### Batch D1 — model: sonnet — T031–T034

- [ ] T031 [US2] Test first: `tests/NetPrints.Catalog.Tests/Format/CatalogWriterTests.cs` (CT-T01, golden
  `tests/NetPrints.Catalog.Tests/Format/Golden/writer.npcat.json` from a hand-built model) and
  `Format/CatalogReaderTests.cs` (CT-T03).
- [ ] T032 [US2] Shared source (must also compile on netstandard2.0): `src/NetPrints.Catalog/Model/*.cs` (records per
  data-model.md §1), `Engine/Guard.cs`, `Engine/Polyfills.cs`, `Engine/CatalogDiagnosticCodes.cs` (NPC001–NPC006,
  NPC101–NPC103), `Engine/CatalogDiagnostic.cs`.
- [ ] T033 [US2] `src/NetPrints.Catalog/Json/CanonicalCatalogWriter.cs` (shared, no System.Text.Json),
  `Json/CatalogReader.cs`, `Json/CatalogJsonContext.cs`, `Json/CatalogFormatException.cs`.
- [ ] T034 [US2] `src/NetPrints.Catalog/Json/CatalogSchema.cs` (JsonSchemaExporter, like `NetPrintsJsonSchema`),
  committed `schemas/npcat.v1.schema.json`, test `tests/NetPrints.Catalog.Tests/Format/CatalogSchemaTests.cs`
  (CT-T16, npcat part); `eng/validate-schemas.sh` loops over every `schemas/*.schema.json` with its instance globs
  (`*.netpc.json`, `*.npcat.json`); `scripts/build-docs.sh` adds a `cmp` per schema; whole suite; commit.

### Batch D2 — model: sonnet — T035–T039

- [ ] T035 [US3] Reference `src/NetPrints.Annotations` from `tests/NetPrints.Catalog.Tests` with `Aliases="Annotations"`
  (extern alias; the shared engine types would otherwise clash with `NetPrints.Catalog`'s). Test first
  `tests/NetPrints.Catalog.Tests/Generator/AttributeInjectionTests.cs` (AN-T01); add
  `src/NetPrints.Annotations/AttributeSources.cs` (contracts/annotations.md §2, C# 7.3-compatible) and a first
  `src/NetPrints.Annotations/CatalogGenerator.cs` that only registers post-initialization output
  (`AddEmbeddedAttributeDefinition()` + the attributes).
- [ ] T036 [US2] Fixture `tests/Fixtures/Catalog/CatalogFixtureLib/` (`CatalogFixtureLib.csproj`: net10.0,
  `GenerateDocumentationFile`, version 1.0.0, analyzer `ProjectReference` to `src/NetPrints.Annotations` with
  `OutputItemType="Analyzer" ReferenceOutputAssembly="false"`; sources covering every edge case in spec §Edge Cases
  plus `Fixture.Attributes.ExposeAttribute(ExposeFlags flags)` and `[NetPrintsType]`/`[NetPrintsNode]`/
  `[NetPrintsIgnore]` uses); `tests/NetPrints.Catalog.Tests/Profiles/fixture-flags.npprofile.json` (contracts/catalog.md
  §6); `tests/NetPrints.Catalog.Tests/FixtureLibrary.cs` locator; Catalog.Tests references the fixture with
  `ReferenceOutputAssembly="false"`; add the fixture to `NetPrints.slnx`.
- [ ] T037 [US2] Test first: `tests/NetPrints.Catalog.Tests/Engine/GlobTests.cs` (CT-T07),
  `Engine/PublicApiProfileTests.cs` (CT-T04), `Engine/AnnotatedProfileTests.cs` (CT-T05, also proves the injected
  attributes are visible on the compiled fixture — research R11 risk), `Engine/CustomProfileTests.cs` (CT-T06),
  `Engine/DeterminismTests.cs` (CT-T02), `Engine/MissingDependencyTests.cs` (CT-T19); snapshots under
  `tests/NetPrints.Catalog.Tests/Snapshots/`; extend `ExperimentalApiTests` with `NPXE0004`.
- [ ] T038 [US2] Shared engine: `src/NetPrints.Catalog/Engine/ICatalogFilter.cs`, `CatalogProfile.cs`,
  `CatalogProfileFilter.cs`, `BuiltInCatalogProfiles.cs`, `Glob.cs`, `SymbolIds.cs`, `SummaryNormalizer.cs`
  (data-model.md §6), `IDocumentationSource.cs`, `XmlDocumentationSource.cs`, `CatalogIdentity.cs`,
  `CatalogBuilder.cs`, `CatalogBuildResult.cs`; `src/NetPrints.Catalog/ExperimentalApis.cs`;
  `[Experimental("NPXE0004")]` on `CatalogBuilder`, `ICatalogFilter`, `CatalogProfile`, `CatalogProfileFilter`,
  `BuiltInCatalogProfiles`; create the snapshots with `NETPRINTS_UPDATE_SNAPSHOTS=1` and review them line by line.
- [ ] T039 [US2] Shared `src/NetPrints.Catalog/Engine/ProfileJson.cs` (a minimal JSON reader for `*.npprofile.json` and
  inline profiles, no System.Text.Json, so the generator can use it) with
  `tests/NetPrints.Catalog.Tests/Engine/ProfileJsonTests.cs` (valid profile; malformed → NPC003); whole suite; commit.

### Batch D3 — model: sonnet — T040–T043

- [ ] T040 [US2] Test first: `tests/NetPrints.Catalog.Tests/Runtime/CatalogTypeCatalogTests.cs` (CT-T09, CT-T18) and
  `Runtime/ParityTests.cs` (CT-T08: `ReflectionProvider` over the fixture vs `CatalogTypeCatalog` over its
  `public-api` catalog, every query, 0 differences).
- [ ] T041 [US2] `src/NetPrints.Catalog/Runtime/SpecifierFactory.cs` (TypeRef → specifiers, `ReflectionConverter`
  rules), `Runtime/CatalogTypeCatalog.cs`, `Runtime/CatalogLoader.cs`, `Log.cs` (NPC103 warning via `[LoggerMessage]`).
- [ ] T042 [US2] Test first: new cases in `tests/NetPrints.Core.Tests/Extensibility/ContributionTests.cs` (profile
  contributed; built-in or duplicate id → NPX006, first wins); add `IExtensionBuilder.AddCatalogProfile`
  (`[Experimental("NPXE0004")]`) in `src/NetPrints.Extensibility/IExtensionBuilder.cs`, `ExtensionBuilder.cs`,
  `Loading/RegistryBuilder.cs`, `Loading/ExtensionRegistry.cs` (`CatalogProfiles`); `NetPrints.Extensibility`
  references `NetPrints.Catalog` (so every host — editor, generator, CLI — carries it).
- [ ] T043 [US2] `tests/NetPrints.Editor.Tests/Architecture/AssemblyReferenceGateTests.cs`: add `NetPrints.Catalog`
  and `NetPrints.Annotations` (no `Avalonia*`, no `Microsoft.Build*`); `scripts/verify-packages.sh` asserts
  `tools/net10.0/NetPrints.Catalog.dll` inside the `NetPrints.Sdk` package (the generator host carries it, so
  extensions share it); whole suite; commit.

### Batch D4 — model: sonnet — T044–T047

- [ ] T044 [US2] Test first `tests/NetPrints.Catalog.Tests/Config/CatalogConfigTests.cs` (CT-T10).
- [ ] T045 [US2] `src/NetPrints.Catalog/Config/CatalogConfig.cs`, `CatalogOverrides.cs`, `CatalogConfigResolver.cs`,
  `CatalogConfigJsonContext.cs`; generate and commit `schemas/netprints.catalog.v1.schema.json`; add its case to
  `Format/CatalogSchemaTests.cs` (CT-T16) and its instance glob (`netprints.catalog.json`) to `eng/validate-schemas.sh`.
- [ ] T046 [US2] `src/NetPrints.Catalog/Sources/CatalogSourceResolver.cs` (research R9: user project for `project`;
  temporary SDK project under `obj/netprints-catalog/<hash>/` for `assembly`/`package` with the isolation properties;
  `dotnet restore` through `IProcessRunner`; package assemblies identified by global-packages path) and
  `Sources/CatalogCompilationFactory.cs`; `tests/NetPrints.Catalog.Tests/Sources/CatalogSourceResolverTests.cs` with a
  fake `IProjectSystem`/`IProcessRunner` (temporary project content, cleanup, restore failure → error).
- [ ] T047 [US2] Test first `tests/NetPrints.Catalog.Tests/Emit/CSharpFormatTests.cs` (CT-T14); shared
  `src/NetPrints.Catalog/Emit/CSharpLiteral.cs` (escaping) and `Emit/CatalogCSharpEmitter.cs`; whole suite; commit.

### Batch D5 — model: sonnet — T048–T052

- [ ] T048 [US2] `tests/Fixtures/Extensions/Directory.Build.props` (TestExtension's extension settings, output
  `bin/$(Configuration)/extensions/<id>/`) and fixture extension `tests/Fixtures/Extensions/Fx.Catalog/` (id
  `fx.catalog`: contributes the fixture catalog loaded from a copied `public-api.npcat.json`, the `fixture-flags`
  profile, and project profile `fx.catalog.profile` with `CatalogProfileId = "fixture-flags"`); add to `NetPrints.slnx`;
  Core.Tests, Editor.Tests and Cli.Tests reference it with `ReferenceOutputAssembly="false"`.
- [ ] T049 [US2] Test first `tests/NetPrints.Cli.Tests/Commands/CatalogCommandTests.cs` (CT-T11, CT-T12 with a
  temporary feed holding a packed `CatalogFixtureLib`, CT-T13).
- [ ] T050 [US2] `src/NetPrints.Cli/Commands/CatalogCommand.cs` (options and resolution per contracts/catalog.md §4;
  loads `--extension` and project extensions for profiles; prints NPC diagnostics; `--check`).
- [ ] T051 [US2] End to end (CT-T15): `tests/NetPrints.Core.Tests/Samples/ExtensionCatalogTests.cs` (temporary project
  referencing `CatalogFixtureLib.dll` with `NetPrintsExtension` = `fx.catalog`; graph fixture
  `tests/NetPrints.Core.Tests/Fixtures/CatalogCall/CatalogCall.Program.netpc.json` calling
  `Fixture.Geometry.Vector2.Add`; builds and runs) and `tests/NetPrints.Editor.Tests/Reflection/CatalogSearchTests.cs`
  (`ReflectionHost` with the extension: search offers `Vector2.Add`).
- [ ] T052 [US2] `tests/NetPrints.Catalog.Tests/Engine/CatalogPerformanceTests.cs` (CT-T17); whole suite; commit.

### Batch D6 — model: haiku — T053–T055

- [ ] T053 [P] [US2] New `docs/guide/catalogs.md`: what a catalog is, the tool flavor (`netprints catalog`, config file
  reference, overrides, sources, `--check`, `--format csharp`), profiles (built-ins, custom file, extension
  contributions, project default), consuming a catalog from an extension, the schema link, and a "Diagnostics" section
  (anchor `diagnostics`) with NPC001–NPC006 and NPC101–NPC103.
- [ ] T054 [P] [US2] `docs/api/docfx.json`: add `NetPrints.Catalog/NetPrints.Catalog.csproj`; link the catalogs guide
  from `docs/guide/extensions.md` and `README.md`.
- [ ] T055 [US2] Whole suite and `scripts/build-docs.sh`; Checkpoint D report in
  `specs/004-catalog-cli/implementation-notes.md`; commit. **Checkpoint D** (SC-004, SC-005 extension path, SC-006).

---

## Phase 5: User Story 3 — ship prints with a library through annotations (sub-phase E, Priority P1)

**Goal**: generator-only `NetPrints.Annotations`, embedded catalogs, editor discovery, byte-identical flavors
(FR-025–FR-031, contracts/annotations.md). **Independent test**: AN-T01–AN-T14; SC-003, SC-005 (embedded path),
SC-013.

### Batch E1 — model: sonnet — T056–T060

- [ ] T056 [US3] Test first: `tests/NetPrints.Catalog.Tests/Generator/OwnSourceCatalogTests.cs` (AN-T02),
  `Generator/ReferencedCatalogTests.cs` (AN-T03), `Generator/DiagnosticsTests.cs` (AN-T04–AN-T06),
  `Generator/LanguageVersionTests.cs` (AN-T08), `Generator/DeterminismTests.cs` (AN-T14), all through
  `CSharpGeneratorDriver`.
- [ ] T057 [US3] Link the shared sources into `src/NetPrints.Annotations/NetPrints.Annotations.csproj`
  (`..\NetPrints.Catalog\Model\**`, `Engine\**`, `Json\CanonicalCatalogWriter.cs`, `Emit\**`) and make them build
  warning-free on netstandard2.0 (polyfills only in `Engine/Polyfills.cs`).
- [ ] T058 [US3] Shared `src/NetPrints.Catalog/Emit/EmbeddedCatalogEmitter.cs` (assembly attribute + accessor, escaped
  literal via `CSharpLiteral`).
- [ ] T059 [US3] Complete `src/NetPrints.Annotations/CatalogGenerator.cs` (own-source pipeline with
  `ForAttributeWithMetadataName`; referenced-assembly pipeline over `MetadataReferencesProvider` + attribute requests in
  a private compilation; profiles from `AdditionalFiles`; documentation from `NetPrintsReferenceDocumentation`
  additional files; root namespace from `build_property.RootNamespace`), `GeneratorDiagnostics.cs`,
  `AnalyzerReleases.Shipped.md`, `AnalyzerReleases.Unshipped.md`.
- [ ] T060 [US3] Test first `tests/NetPrints.Catalog.Tests/Generator/IncrementalTests.cs` (AN-T07), then tracking names
  and equatable models in the generator until it passes; whole suite; commit.

### Batch E2 — model: sonnet — T061–T066

- [ ] T061 [US3] Test first `tests/NetPrints.Catalog.Tests/Runtime/EmbeddedCatalogReaderTests.cs` (AN-T09); add
  `src/NetPrints.Catalog/Runtime/EmbeddedCatalogReader.cs` (System.Reflection.Metadata and `CustomAttributeData`) and
  `CatalogLoader.LoadEmbedded`.
- [ ] T062 [US3] `src/NetPrints.Annotations/build/NetPrints.Annotations.targets` (contracts/annotations.md §5) and the
  package layout (`analyzers/dotnet/cs/`, `build/`, `developmentDependency`, package README through
  `eng/PackageReadme.targets`).
- [ ] T063 [US3] `tests/NetPrints.Catalog.Tests/EndToEnd/AnnotationsPackageTests.cs` (AN-T10: pack into a temporary
  feed, `dotnet build` a netstandard2.0 library using the package, compare its embedded catalog with the snapshot).
- [ ] T064 [US3] Test first `tests/NetPrints.Catalog.Tests/EndToEnd/ReferenceAssemblyPackageTests.cs` (AN-T15: pack the
  fixture with `ProduceReferenceAssembly` and its reference assembly under `ref/net10.0/`, consume it, assert the
  catalog is discovered); add the `ref/` → `lib/` fallback to `src/NetPrints.Catalog/Runtime/EmbeddedCatalogReader.cs`,
  and switch the generated `NetPrintsEmbeddedCatalogAttribute` to `public` + `[Embedded]` only if the test shows
  Roslyn drops it; record the observed behaviour in `specs/004-catalog-cli/implementation-notes.md`.
- [ ] T065 [US3] `tests/NetPrints.Catalog.Tests/EndToEnd/CrossFlavorSnapshotTests.cs` (AN-T13, SC-003).
- [ ] T066 [US3] `scripts/verify-packages.sh`: expect `NetPrints.Catalog` (`.nupkg` + `.snupkg`,
  `lib/net10.0/NetPrints.Catalog.dll`) and `NetPrints.Annotations` (`.nupkg`; analyzer and targets present; no `lib/`;
  `developmentDependency`); run `scripts/pack-local.sh` and the script; whole suite; commit.

### Batch E3 — model: sonnet — T067–T069

- [ ] T067 [US3] Test first `tests/NetPrints.Editor.Tests/Reflection/EmbeddedCatalogDiscoveryTests.cs` (AN-T11).
- [ ] T068 [US3] `src/NetPrints.Editor/Hosting/ReflectionHost.cs`: after the registry's catalogs, add the embedded
  catalogs of `snapshot.References` (`EmbeddedCatalogReader` + `CatalogLoader`), first id wins (NPC103 logged),
  covered assemblies excluded from the live provider; `Hosting/Log.cs` entries.
- [ ] T069 [US3] `tests/NetPrints.Core.Tests/Samples/AnnotatedLibraryBuildTests.cs` (AN-T12); whole suite; commit.

### Batch E4 — model: haiku — T070–T072

- [ ] T070 [P] [US3] `docs/guide/catalogs.md`: section "Annotations" (package reference with `PrivateAssets="all"`, the
  four attributes, `[assembly: NetPrintsCatalog]` with profiles as `AdditionalFiles`, reference documentation,
  discovery by the editor, generator diagnostics NPC001–NPC006).
- [ ] T071 [P] [US3] Package README for `NetPrints.Annotations` and `NetPrints.Catalog` (per `eng/PackageReadme.targets`)
  and a short "Ship nodes with your library" paragraph in `README.md`.
- [ ] T072 [US3] Whole suite, `scripts/pack-local.sh` + `scripts/verify-packages.sh`; Checkpoint E report in
  `specs/004-catalog-cli/implementation-notes.md`; commit. **Checkpoint E** (SC-003, SC-005 embedded path, SC-013
  locally).

---

## Phase 6: User Story 4 — graphs stay canonical and reviewable in git (sub-phase F, Priority P2)

**Goal**: `format`, `show`, `merge`, `git-install`, SchemaStore entry (FR-032–FR-037, contracts/git.md).
**Independent test**: GI-T01–GI-T12; SC-002 (format part), SC-009, SC-010.

### Batch F1 — model: sonnet — T073–T076

- [ ] T073 [US4] Test first: `tests/NetPrints.Cli.Tests/Commands/FormatCommandTests.cs` (GI-T01) and
  `tests/NetPrints.Cli.Tests/Git/GraphSummaryTests.cs` (GI-T02; goldens
  `tests/NetPrints.Cli.Tests/Git/Snapshots/HelloWorld.show.txt`, `AllNodes.show.txt`).
- [ ] T074 [US4] `src/NetPrints.Cli/Commands/FormatCommand.cs` (format resolved through `DocumentFormatRegistry`, files
  through `FileSystemDocumentStore`; read, write through the same `IDocumentFormat` to memory, byte compare;
  `--check`).
- [ ] T075 [US4] `src/NetPrints.Cli/Git/GraphSummaryWriter.cs` (contracts/git.md §1) and
  `src/NetPrints.Cli/Commands/ShowCommand.cs` (document read through `DocumentFormatRegistry`).
- [ ] T076 [US4] `.github/workflows/ci.yml` "Graph checks": add `netprints format --check samples`; whole suite; commit.

### Batch F2 — model: sonnet — T077–T080

- [ ] T077 [US4] Test first `tests/NetPrints.Cli.Tests/Git/GraphMergerTests.cs` (GI-T03–GI-T08, GI-T12) with fixtures
  `tests/NetPrints.Cli.Tests/Git/Fixtures/<case>/{base,ours,theirs,expected}.netpc.json` (GI-T03 reuses the DF-T23
  base from `tests/NetPrints.Core.Tests/Serialization/MergeTests.cs`).
- [ ] T078 [US4] `src/NetPrints.Cli/Git/GraphMerger.cs` (inputs read through the `IDocumentFormat` resolved from
  `--path`; identity merge and validation, `MergeConflict`, `MergeOutcome` per data-model.md §7).
- [ ] T079 [US4] `src/NetPrints.Cli/Git/TextMergeFallback.cs` (`git merge-file -p` through `IProcessRunner`) and
  `src/NetPrints.Cli/Commands/MergeCommand.cs`.
- [ ] T080 [US4] Whole suite; batch F2 results in `specs/004-catalog-cli/implementation-notes.md`; commit.

### Batch F3 — model: sonnet — T081–T084

- [ ] T081 [US4] Test first `tests/NetPrints.Cli.Tests/Commands/GitInstallCommandTests.cs` (GI-T09, temporary git
  repositories).
- [ ] T082 [US4] `src/NetPrints.Cli/Git/GitConfig.cs`, `Git/GitAttributesFile.cs`,
  `src/NetPrints.Cli/Commands/GitInstallCommand.cs` (contracts/git.md §3).
- [ ] T083 [US4] `tests/NetPrints.Cli.Tests/Git/GitDriversEndToEndTests.cs` (GI-T10: `git diff` through `show`, `git
  merge` through the driver, plain-git baseline conflict).
- [ ] T084 [P] [US4] `eng/schemastore/catalog-entries.json` and `eng/schemastore/PULL_REQUEST.md` (with the line
  "**Owner action**: submit after the docs site serves both URLs; not done by agents"), and
  `tests/NetPrints.Cli.Tests/Git/SchemaStoreEntryTests.cs` (GI-T11); whole suite; commit.

### Batch F4 — model: haiku — T085–T087

- [ ] T085 [P] [US4] New `docs/guide/git.md`: CI checks (`format --check`, `regen --check`), `show`, `git-install`
  (options, local tools, uninstall), the merge driver's behaviour and limits (web merges, generated files), and
  SchemaStore (pending owner submission).
- [ ] T086 [P] [US4] `docs/guide/graph-format.md` (link the git guide, the checks and the schema's SchemaStore status) and
  `docs/guide/projects.md` ("regenerated, never hand-merged": mention the driver and `regen`).
- [ ] T087 [US4] Whole suite and `scripts/build-docs.sh`; Checkpoint F report in
  `specs/004-catalog-cli/implementation-notes.md`; commit. **Checkpoint F** (SC-002 format part, SC-009, SC-010).

---

## Phase 7: User Story 5 — extensions coexist safely (sub-phase G, Priority P2)

**Goal**: ADR-0010 §4 loader rules and the host multi-extension suite (FR-038–FR-043, contracts/extensions.md).
**Independent test**: MX-T01–MX-T16; SC-007, SC-008.

### Batch G1 — model: sonnet — T088–T090

- [ ] T088 [US5] Fixture projects under `tests/Fixtures/Extensions/`: `Fx.Alpha`, `Fx.Beta`, `Fx.LibV1`, `Fx.LibV2`,
  `Fixture.SharedLib.V1`, `Fixture.SharedLib.V2`, `Fx.PrefixedPrivate`, `NetPrintsFixture.Runtime`,
  `Fx.TypesProvider`, `Fx.TypesConsumer`, `Fx.Native` (each with `netprints-extension.json`, contracts/extensions.md
  §3); `ProjectReference`s from `tests/NetPrints.Core.Tests/NetPrints.Core.Tests.csproj`
  (`ReferenceOutputAssembly="false"`); `NetPrints.slnx` entries;
  `tests/NetPrints.Core.Tests/Extensibility/MultiExtension/FixtureExtensions.cs` (`CopyTo`).
- [ ] T089 [US5] `tests/NetPrints.Testing/Extensions/ExtensionHarness.cs` (contracts/extensions.md §4) and
  `tests/NetPrints.Core.Tests/Extensibility/MultiExtension/ExtensionHarnessTests.cs`.
- [ ] T090 [US5] `tests/NetPrints.Core.Tests/Extensibility/MultiExtension/CharacterizationTests.cs` (MX-T01: host type
  identity, NPX001–NPX007 and ordering with the new fixtures, green on the current loader); whole suite; commit.

### Batch G2 — model: sonnet — T091–T094

- [ ] T091 [US5] Test first, red on the current loader (record the failing run in implementation-notes.md):
  `MultiExtension/SharedAssemblyRuleTests.cs` (MX-T02, MX-T03) and `MultiExtension/DependencyTypeSharingTests.cs`
  (MX-T04, MX-T05).
- [ ] T092 [US5] `src/NetPrints.Extensibility/Loading/HostAssemblies.cs` and the ADR-0010 sharing rule in
  `Loading/ExtensionLoadContext.cs` (prefix list removed); `Log.HostAssemblyShadowed` in
  `src/NetPrints.Extensibility/Log.cs`.
- [ ] T093 [US5] Dependency delegation: `ExtensionLoadContext` dependency contexts and chain resolution;
  `Loading/ExtensionLoader.cs` passes contexts in topological order; `Loading/ExtensionLoadContextCache.cs`;
  `Log.DependencyAssemblyShadowed`; MX-T01–MX-T05 green.
- [ ] T094 [US5] `MultiExtension/VersionIsolationTests.cs` (MX-T06); whole suite; commit (tests and fix pushed together).

### Batch G3 — model: sonnet — T095–T098

- [ ] T095 [P] [US5] `MultiExtension/IdConflictTests.cs` (MX-T07, MX-T08).
- [ ] T096 [P] [US5] `MultiExtension/LoadOrderPermutationTests.cs` (MX-T09) and `MultiExtension/DocumentSubsetTests.cs`
  (MX-T12).
- [ ] T097 [P] [US5] `MultiExtension/FailureIsolationTests.cs` (MX-T10, MX-T11, MX-T13); if MX-T10 finds contributions
  of the throwing extension committed, make `src/NetPrints.Extensibility/Loading/RegistryBuilder.cs` commit per
  extension atomically (the test is the red step).
- [ ] T098 [P] [US5] `MultiExtension/ScaleTests.cs` (MX-T14), `MultiExtension/ReloadTests.cs` (MX-T15),
  `MultiExtension/NativeDependencyTests.cs` (MX-T16); whole suite; commit.

### Batch G4 — model: haiku — T099–T101

- [ ] T099 [P] [US5] `docs/guide/extensions.md`: section "Coexistence rules" (what the host shares — its own
  assemblies only — and what loads privately; the shadowing warnings).
- [ ] T100 [P] [US5] `docs/guide/extensions.md`: section "Depending on another extension" (`dependsOn`, reference the
  provider with `Private=false`, one type identity, diamonds) and a pointer to ADR-0010.
- [ ] T101 [US5] Whole suite; Checkpoint G report in `specs/004-catalog-cli/implementation-notes.md`; commit.
  **Checkpoint G** (SC-007, SC-008).

---

## Phase 8: Polish and cross-cutting (sub-phase H)

### Batch H1 — model: sonnet — T102–T106

- [ ] T102 `scripts/build-docs.sh`: 0 broken links; `website/build/schemas/` holds `netpc.v1`, `npcat.v1` and
  `netprints.catalog.v1` byte-identical to `schemas/` (SC-012); `docs/adr/README.md` lists 0010 and 0012–0016.
- [ ] T103 Run `specs/004-catalog-cli/quickstart.md` end to end; record the output summary in implementation-notes.md.
- [ ] T104 Release dry run locally (`scripts/pack-local.sh`, `scripts/verify-packages.sh`; SC-013); `git status
  samples/` clean.
- [ ] T105 Whole suite in Release plus the Desktop E2E run (AGENTS.md two-run split); `dotnet build -c Release` 0
  warnings; `dotnet format NetPrints.slnx --verify-no-changes`; `dotnet format analyzers --severity info
  --verify-no-changes` over the changed files.
- [ ] T106 **Checkpoint H**: SC-001…SC-013 table with evidence in implementation-notes.md, governance proposals
  (plan.md), mark the PR ready for the independent review (AGENTS.md).

---

## Dependencies and execution order

- Batches run strictly in order: A1 → B1 → B2 → C1 → C2 → C3 → C4 → D1 → … → H1 (one agent at a time, AGENTS.md).
- B (US6) precedes the stories so every later public API change is tracked; `NPXE0004` is applied in D2/D3 when the
  catalog API exists.
- C (US1) is the MVP and precedes D because `netprints catalog` and the checks are CLI commands.
- D2's T035 (attribute injection, US3) precedes the fixture library (T036) that uses the attributes; the rest of US3
  (E) needs D's engine, writer and emitters.
- F (US4) needs C's CLI skeleton only; G (US5) needs D's `Fx.Catalog` scaffolding (`tests/Fixtures/Extensions/
  Directory.Build.props`) and B's API tracking.
- Within a batch, [P] tasks touch different files; tasks without [P] run in order.

## Parallel opportunities (inside a batch, for a single agent)

- C3: T023 alongside T024's API sketch; C4: T028 ∥ T029.
- D6: T053 ∥ T054; E4: T070 ∥ T071; F4: T085 ∥ T086; G3: T095 ∥ T096 ∥ T097 ∥ T098; G4: T099 ∥ T100.

## Implementation strategy

1. MVP = Checkpoint C: the new CLI replaces P1's with every workflow the build supports, and CI flips to exit 0.
2. Catalogs next (D, E): tool first, then the generator reusing the same engine; Checkpoint E proves parity.
3. Git tooling (F) and coexistence (G) are independent increments on top.
4. H closes the phase: docs, quickstart, dry run, review.

## Requirement coverage

| Requirement | Tasks |
|---|---|
| FR-001–FR-006, FR-010, FR-011 | T015–T022 |
| FR-007, FR-008 | T023–T026 |
| FR-009 | T019, T021 |
| FR-012–FR-015 | T031–T039 |
| FR-016, FR-017 | T040–T041, T051 |
| FR-018 | T042 |
| FR-019–FR-024 | T044–T050 |
| FR-025–FR-030 | T035, T056–T065 |
| FR-031 | T064, T067–T069 |
| FR-032, FR-033 | T073–T076 |
| FR-034 | T077–T079 |
| FR-035 | T081–T083 |
| FR-036 | T084 |
| FR-037 | T034, T045, T102 |
| FR-038–FR-043 | T088–T098 |
| FR-044 | T006–T009 |
| FR-045 | T010–T013, T038, T042 |
| FR-046 | T013, T028–T029, T053–T054, T070–T071, T085–T086, T099–T100, T102 |
| FR-047 | T066, T104 |
