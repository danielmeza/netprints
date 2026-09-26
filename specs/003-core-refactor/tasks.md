---
description: "Task list for P1 — Core refactor and extension points (csproj model, VCS-friendly graph format)"
---

# Tasks: Core Refactor and Extension Points

**Input**: `/specs/003-core-refactor/` — plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Start condition**: met. `003-core-refactor` is rebased on `master` after the reorganization (PR #5);
all paths below exist or are created by the task that names them. The original Core, Reflection and
Core.Tests sources are CRLF and marked `-text` in `.gitattributes`: keep their line endings when
editing them.

**Revision 2026-09-25 (csproj model)**: the old custom project file, reference resolver and
`ProjectCompiler` tasks are gone. **Revision 2026-09-25 (graph format, research R17)**: tasks renumbered
from T015 on; new tasks T015, T017, T018, T019, T027, T041 and T060; see "Test-ID map" at the end.
**Revision 2026-09-25 (release and docs, research R18–R19)**: sub-phase L appended as T109–T122 (US8,
`contracts/release-and-docs.md`, `RL-Txx`); T104, T106 and T108 amended; T108 now runs after Checkpoint L.
Nothing is published during P1: no `v*` tag, no `dotnet nuget push`, no `PUBLISH_DOCS`/`PUBLISH_WIKI` variable.

**Tests**: REQUIRED (constitution V). Test-obligation ids (`DF-`, `PS-`, `EX-`, `RC-`, `ED-Txx`) refer to
the tables at the end of each contract (`RL-` = release-and-docs.md); a task that names an id must implement exactly that case. Every
test uses xUnit v3 and passes `TestContext.Current.CancellationToken`; UI tests use page objects and
`AutomationIds`, no sleeps (research U17). Tests that create nodes or members and compare ids or
documents run inside `using var _ = IdGeneration.Use(new SeededIdGenerator(<seed>));`. Tests that run
MSBuild call `MsBuildRegistration.EnsureRegistered()` from a `[ModuleInitializer]`. Checkpoints require
`dotnet build NetPrints.slnx -c Release` (0 warnings), `dotnet format NetPrints.slnx --verify-no-changes`
and `dotnet test --solution NetPrints.slnx -c Release` green.

**Rules for the implementer**: match comment density (AGENTS.md); rationale in commit messages; one
commit per task or small group; never change a golden file without regenerating it with
`NETPRINTS_UPDATE_SNAPSHOTS=1` and saying why in the commit. One PR, opened as a draft after
Checkpoint E (plan.md). Out of scope (follow-ups, do not implement): `netprints merge` and
`git-install`, `textconv` diffs, CLI `format --check`/`regen --check`, SchemaStore registration.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelizable (different files, no dependency on an unfinished task)
- **[Story]**: US1…US8 (spec.md). Setup, Foundational and Polish tasks have no story label.

---

## Phase 1: Setup and characterization (sub-phase A) — on unmodified behavior

- [x] T001 Add to `Directory.Packages.props`: `Avalonia.AvaloniaEdit` 12.0.0, `AvaloniaEdit.TextMate` 12.0.0, `Microsoft.Extensions.Logging.Abstractions`/`Logging`/`Logging.Console` 10.0.12, `Microsoft.Build.Locator` 1.11.2, `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.9.0, `Microsoft.Build` and `Microsoft.Build.Framework` 18.0.2 (research §3, R16). Do not remove Fody yet
- [x] T002 [P] Create `tests/NetPrints.Core.Tests/Characterization/AllNodesFixtureFactory.cs`: builds, through the current Core API, project `AllNodes` with class `AllNodes.Everything` that uses every one of the 23 built-in node kinds at least once (document-format.md §1.5 minus `eventEntry`), including: method with 2 arguments, 1 generic argument and 2 return values; constructor; class variable with getter and setter and a generic type built in its type graph; a class interface pin; `MakeArrayNode` with predefined size and with 3 elements; data/exec/type reroutes; renamed editable pins; unconnected values of types bool, int, long, double (`NaN`), decimal, char, string with quotes and newline, and an enum; one `AssemblyReference` and one `SourceDirectoryReference`. Writes legacy XML with `Project.Save` only when `NETPRINTS_REGENERATE_SAMPLES=1`
- [x] T003 Generate and commit the legacy fixtures: `tests/NetPrints.Core.Tests/Fixtures/Legacy/AllNodes/*` (T002) and a copy of `samples/HelloWorld/*` into `tests/NetPrints.Core.Tests/Fixtures/Legacy/HelloWorld/`; link them as test content in `tests/NetPrints.Core.Tests/NetPrints.Core.Tests.csproj`
- [x] T004 Add `tests/NetPrints.Core.Tests/Characterization/GoldenCSharpTests.cs`: for each class of both legacy fixtures, load with `Project.LoadFromPath`, translate with today's `ClassTranslator`, compare to `Fixtures/Golden/<Class.FullName>.cs` (write when `NETPRINTS_UPDATE_SNAPSHOTS=1`). Generate and commit the golden files (DF-T01 baseline)
- [x] T005 [P] Add `tests/NetPrints.Core.Tests/Characterization/NotificationMapTests.cs`: for every public non-abstract type in `NetPrints.Core`/`NetPrints.Graph` implementing `INotifyPropertyChanged` (instances from the AllNodes fixture), set each public settable property to a different valid value and record the raised property names (a setter that throws for that instance, e.g. `IsPure` when `CanSetPure` is false, is recorded as `"<throws>"`; order = ordinal type name, then property name); compare to `Characterization/NotificationMap.golden.json`. Generate on the Fody build and commit (research R6)
- [x] T006 Run the suite; commit. **Checkpoint A**

---

## Phase 2: Foundational (sub-phase B) — blocks all stories

- [x] T007 Create `src/NetPrints.Core/Core/ModelObject.cs` (data-model.md §1); add `CommunityToolkit.Mvvm` to `src/NetPrints.Core/NetPrints.Core.csproj`
- [x] T008 Migrate `NetPrints.Graph.Node`, `NodePin` and all pin subclasses to `ModelObject` with `[ObservableProperty]` partial properties (data-model.md §1); rename `OnInputTypeChanged` → `HandleInputTypeChanged` in `Node` and all overrides in `src/NetPrints.Core/Graph/*.cs`
- [x] T009 Migrate `Variable` and `Project` (`src/NetPrints.Core/Core/Variable.cs`, `Project.cs`) to `ModelObject`; computed properties raise via `[NotifyPropertyChangedFor]`
- [x] T010 Remove `Fody`/`PropertyChanged.Fody` from `src/NetPrints.Core/NetPrints.Core.csproj` and `Directory.Packages.props`; delete `src/NetPrints.Core/FodyWeavers.xml/.xsd` and their `.gitattributes` lines; remove `[DoNotNotify]` in `src/NetPrints.Core/Graph/TypeNode.cs`. T004 and T005 pass unchanged (FR-036)
- [x] T011 [P] Nullable + warnings-as-errors in `src/NetPrints.Core` `Core/` and `Graph/`
- [x] T012 [P] Same for `src/NetPrints.Core` `Translator/`, `Compilation/`, `Serialization/`
- [x] T013 [P] Same for `src/NetPrints.Reflection` (10 `RS1024` sites → `SymbolEqualityComparer.Default`). This is not a mechanical swap: the `dotnet format` auto-fix of 4 sites in `ReflectionProvider.cs` broke `CategoriesPerPinKind` in PR #5 (fixed in 2de1d4c). Change one site at a time, find out why a comparer changes the result there (e.g. symbols from different compilations, or nullability) and keep the old behaviour; the full test suite must stay green. Then remove `--exclude-diagnostics RS1024` from the "Format check" step of `.github/workflows/ci.yml` and its comment
- [x] T014 [P] Same for `tests/NetPrints.Core.Tests` and `src/NetPrints.Cli`; make `TreatWarningsAsErrors=true` the default in the root `Directory.Build.props` (drop its "legacy projects" comment) and delete every per-project `Nullable=disable`/`TreatWarningsAsErrors` override (`src/NetPrints.Core`, `src/NetPrints.Reflection`, `src/NetPrints.Editor`, `src/NetPrints.Desktop`, all `tests/*` projects) (FR-037)
- [x] T015 [US1] Id generation (data-model.md §2): `src/NetPrints.Core/Core/Ids.cs` with `IIdGenerator`, `RandomIdGenerator`, `SeededIdGenerator`, `IdGeneration` (`AsyncLocal` scope), `StableIds`; tests `tests/NetPrints.Core.Tests/Core/IdGenerationTests.cs`: 10 000 ids match `^m[0-9a-hjkmnp-tv-z]{6}$` for prefix `m`; two `SeededIdGenerator(42)` give the same sequence; `Use` restores the previous generator on `Dispose`, flows across `await` and does not leak into a parallel task; `StableIds.SeedFor("")` = `unchecked((int)2166136261)` and `SeedFor("a")` = `unchecked((int)0xE40C292C)`; `IsValidDocumentId` rejects `""`, `"a/b"`, `"a b"`
- [x] T016 [US1] Node identity: `Node.Id`, `Node.DefaultName`, `NodeGraph.AllocateNodeId/FindNode/AssignLegacyNodeIds/PreservedDocumentState` (data-model.md §2) in `src/NetPrints.Core/Graph/Node.cs`, `src/NetPrints.Core/Core/NodeGraph.cs`; `[OnDeserializing]` init; tests `tests/NetPrints.Core.Tests/Core/NodeIdTests.cs`: DF-T18 node part (random ids, retry on a used id with a stub generator, seeded determinism, legacy `n0…nK`, `AssignLegacyNodeIds` throws when ids are set, undo-style remove/re-add keeps the id)
- [x] T017 [US1] Member identity, graph keys and dirty state: `Id` on `MethodGraph`, `ConstructorGraph`, `Variable` (constructor-assigned, `internal set`); `ClassGraph.Members`, `EnsureUniqueMemberIds`, `AssignLegacyMemberIds`, `IsDirty`/`MarkDirty`/`MarkClean` in `src/NetPrints.Core/Core/ClassGraph.cs`; `src/NetPrints.Core/Core/GraphKeys.cs` (`class`, `<memberId>`, `<variableId>/type|get|set`); `Project.CreateNewClass` sets dirty (T055 adds the method; until then only the flag exists); tests `tests/NetPrints.Core.Tests/Core/GraphKeyTests.cs`: `For`/`Resolve` round trip for every graph of AllNodes, keys unchanged after reordering `Methods`, `AssignLegacyMemberIds` gives the same ids for two loads of the same legacy class, `EnsureUniqueMemberIds` renames only the later duplicate, `For` throws for a detached graph (DF-T22 model part)
- [x] T018 [US1] Pin keys (document-format.md §1.4.2): `Node.GetPinKeyName` (virtual) with overrides in `MethodEntryNode`, `ConstructorEntryNode`, `ReturnNode`; `src/NetPrints.Core/Graph/PinKeys.cs` (`For`, `Find`); tests `tests/NetPrints.Core.Tests/Core/PinKeyTests.cs`: DF-T19 model part — golden `tests/NetPrints.Core.Tests/Fixtures/Golden/PinKeys.golden.txt` (one line `<class>/<graph key>/<node id>: <pin ref>` per pin of AllNodes, ordinal-sorted; written with `NETPRINTS_UPDATE_SNAPSHOTS=1`), every reference unique per node and `Find(PinKeys.For(p)) == p`, `~2` for a call node with two `Int32` return values, a renamed argument pin keeps `out.data.Input0`, `Find` returns null for `in.data.0` and for malformed input (`""`, `"in.data"`, `"x.data.a"`)
- [x] T019 [US1] Auto-placement: `src/NetPrints.Core/Graph/GraphAutoLayout.cs` exactly per data-model.md §2; tests `tests/NetPrints.Core.Tests/Graph/GraphAutoLayoutTests.cs` (DF-T20 model part): upstream neighbour at (100, 50) → (400, 50); downstream only at (700, 50) → (400, 50); no neighbour with placed nodes spanning x 0…600 → (900, minY), the next such node (900, minY + 120); a collision moves down in 120 steps; empty graph → (0, 0), (0, 120); same input twice → same output
- [x] T020 Extract `GraphTypeInference.Relax(NodeGraph)` into `src/NetPrints.Core/Graph/GraphTypeInference.cs` from `MethodGraph.OnDeserialized`; the hook's body becomes a call to `GraphTypeInference.Relax(this)` (document-format.md §3.1); golden tests unchanged; DF-T27 (legacy-import half: relaxation still runs on that path)
- [x] T021 Create project shells, all net10.0, nullable, warnings as errors, `CA1848`/`CA2254` as errors, in `NetPrints.slnx` (`/src/` folder; the test asset of T071 goes in `/tests/`): `src/NetPrints.Serialization`, `src/NetPrints.Extensibility`, `src/NetPrints.Workspace` (Locator + Workspaces.MSBuild; `Microsoft.Build`/`Microsoft.Build.Framework` with `ExcludeAssets="runtime" PrivateAssets="all"`, research R14 MSBL001), `src/NetPrints.Generator` (Exe), `src/NetPrints.Sdk` (pack-only: `IncludeBuildOutput=false`, `DevelopmentDependency=true`, `SuppressDependenciesWhenPacking=true`); `InternalsVisibleTo("NetPrints.Serialization")` and `InternalsVisibleTo("NetPrints.Core.Tests")` in Core
- [x] T022 Logging foundation: Logging.Abstractions in `src/NetPrints.Editor`, Logging + Console in `src/NetPrints.Desktop`; `CA1848`/`CA2254` errors in Editor and Desktop; `EditorHostServices` with first member `ILoggerFactory LoggerFactory` (rest in T075), `EditorApp.HostServices` (set by Desktop `Program`; `InvalidOperationException` if unset), `EditorComposition(EditorHostServices host, …)` (P0 `customize` hook stays until T098), required `EditorContext.LoggerFactory`, `src/NetPrints.Desktop/AvaloniaLogSink.cs` replacing `.LogToTrace()`, `src/NetPrints.Editor/Hosting/Log.cs` with 1001/1002 used by `src/NetPrints.Editor/Hosting/Avalonia/UnhandledExceptionHandler.cs`; ED-T12 in `tests/NetPrints.Editor.Tests/Hosting/UnhandledExceptionHandlerTests.cs`; test compositions pass `NullLoggerFactory`
- [x] T023 Commit. **Checkpoint B**: 0 warnings; A-gates unchanged; no Fody (SC-007 partial)

---

## Phase 3: User Story 1a — graph documents (P1) 🎯 (sub-phase C)

- [x] T024 [P] [US1] `src/NetPrints.Serialization/DocumentId.cs`, `DocumentIssue.cs` (with the `NPD001–006` code constants of document-format.md §2.1), `DocumentExceptions.cs`, `DiagnosticExtensions.cs` (document-format.md §2.1, compilation-and-diagnostics.md §1) + `tests/NetPrints.Core.Tests/Serialization/DocumentIdTests.cs`
- [x] T025 [P] [US1] DTO records in `src/NetPrints.Serialization/Documents/` (`ClassDocument.cs`, `GraphDocuments.cs` with member `Id`s, `NodeDocuments.cs` with all 24 kinds and nullable `Name`, `Refs.cs`) exactly per document-format.md §1.4–§1.6/§2.4 (layout `SortedDictionary<string, SortedDictionary<string, int[]>>?`)
- [x] T026 [US1] `Json/NetPrintsJsonContext.cs`, `NetPrintsJsonOptions.cs` (serializer options and `DocumentOptions` per §2.3), `NodeListConverter.cs` (`$kind`/`id` anywhere in the object; unknown kinds kept) + converter tests in `tests/NetPrints.Core.Tests/Serialization/NodeListConverterTests.cs`: `$kind` after `id` and after kind fields deserializes; unknown kind → `UnknownNodeDocument`; missing `$kind`/`id` → `DocumentFormatException`
- [x] T027 [US1] `Json/CanonicalJsonWriter.cs` exactly per document-format.md §2.3.1 + DF-T04 in `tests/NetPrints.Core.Tests/Serialization/CanonicalJsonTests.cs` (expected text literal), plus: every inline rule 1–5 and both block forms asserted separately; output re-parses; empty `{}`/`[]` inline; `NaN` number → `ArgumentException`
- [x] T028 [US1] `Mapping/TypedValueConverter.cs` + DF-T07
- [x] T029 [US1] `Mapping/NodeMappingContext.cs` + `ToRef`/`FromRef` tests in `…/Serialization/RefMappingTests.cs`
- [x] T030 [US1] `Mapping/INodeDocumentConverter.cs`, `NodeDocumentConverterRegistry.cs` + registry tests
- [x] T031 [P] [US1] Built-in converters `methodEntry`, `constructorEntry`, `return`, `classReturn`, `typeReturn` in `Mapping/BuiltIn/EntryReturnConverters.cs`
- [x] T032 [P] [US1] `callMethod`, `constructor`, `makeDelegate`, `variableGetter`, `variableSetter` in `Mapping/BuiltIn/MemberConverters.cs`
- [x] T033 [P] [US1] `literal`, `type`, `makeArrayType`, `makeArray`, `explicitCast`, `typeOf`, `default` in `Mapping/BuiltIn/ValueConverters.cs`
- [x] T034 [P] [US1] `ifElse`, `forLoop`, `ternary`, `await`, `throw`, `reroute` in `Mapping/BuiltIn/FlowConverters.cs`
- [x] T035 [US1] `Mapping/DocumentMapper.cs` (class mapping, member ids, pins by key via `PinKeys`, default names, edges, integer layout, auto-placement, preserved unknown nodes, issues `NPD001–004`, `GraphTypeInference.Relax` at the end of each graph per document-format.md §3.1) + DF-T06, DF-T08, DF-T19 (document part, incl. the `M(int a, int b)` → `M(int a, string inserted, int b)` hand edit), DF-T20 (document part), DF-T22 (document part), DF-T25, DF-T27 (JSON-path half) in `tests/NetPrints.Core.Tests/Serialization/DocumentMapperTests.cs`
- [x] T036 [US1] `Migrations/IDocumentMigration.cs`, `DocumentMigrator.cs` + DF-T10
- [x] T037 [US1] `Json/JsonDocumentFormat.cs` (read pipeline with `$schema` stripping, write pipeline with `$schema` first, document-format.md §2.2) + DF-T09, DF-T21 in `tests/NetPrints.Core.Tests/Serialization/JsonDocumentFormatTests.cs`
- [x] T038 [US1] Add DataContract-compatible **copies** of the legacy project/reference classes in `src/NetPrints.Serialization/Legacy/` (`LegacyProject`, `LegacyCompilationReference`, `LegacyAssemblyReference`, `LegacyFrameworkAssemblyReference`, `LegacySourceDirectoryReference`; `[DataContract(Name = …, Namespace = …)]` and `[KnownType]` matching the originals so `.netpp` files deserialize; Core's originals stay until T063; data-model.md §5) + a test reading both fixtures' `.netpp`; `Legacy/LegacyXmlDocumentFormat.cs` (§2.2, §3: `AssignLegacyNodeIds` and `AssignLegacyMemberIds` on every class) + test: both fixtures' classes import without issues, and importing the same fixture twice gives byte-identical documents (DF-T18 legacy part)
- [x] T039 [US1] `DocumentFormatRegistry.cs` + DF-T16
- [x] T040 [P] [US1] `Stores/IDocumentStore.cs`, `InMemoryDocumentStore.cs`, `FileSystemDocumentStore.cs` (§2.7) + shared abstract store tests DF-T12…T14 in `…/Serialization/Stores/`
- [x] T040a [US1] Snowflake ids (owner decision 2026-09-25, data-model.md §2): `Core/Ids.cs` rewritten — `IdFormat` (format/parse, `Pattern = "^[nm][0-9a-hjkmnp-tv-z]{13}$"`, the single source T041's schema regex reads from), `SnowflakeIdGenerator` (63-bit value = 41-bit ms since 2026-01-01T00:00:00Z | 16-bit session | 6-bit sequence, `TimeProvider` + session id, monotonic — sequence overflow or a backwards clock advances a logical millisecond, never reuses or goes back), `RandomIdGenerator`/`SeededIdGenerator` kept as the same public API over a `SnowflakeIdGenerator` (real clock + random session; fixed `Epoch` clock + seed-derived session) so every existing call site keeps working. `StableIds.AllocateUnique(prefix, existingIds)` factored out of `ClassGraph`'s member-id allocator, shared by node-id duplicate repair (T040c). Tests: `tests/NetPrints.Core.Tests/Core/SnowflakeIdGeneratorTests.cs` (bit layout, monotonic sequence/overflow/clock-backwards via a `FakeTimeProvider`/small rewindable-clock double, format/parse round trip, alias parsing), `IdGenerationTests.cs`/`NodeIdTests.cs` regexes updated to the 13-digit pattern
- [x] T040b [US1] `NodeGraph` id→node `Dictionary` index (data-model.md §2): `FindNode` O(1); `AllocateNodeId` no longer retries or searches (`IdGeneration.Current.NewId('n')` — the generator's own uniqueness guarantee); the index is `[IgnoreDataMember]`, built lazily (DataContract skips field initializers, document-format.md §3.1) and kept current via `Nodes.CollectionChanged` (add/remove) plus an explicit `ReindexNode` call wherever a node's `Id` changes after being added (`AssignLegacyNodeIds`, `DocumentMapper`'s document-id overwrite and duplicate-id repair)
- [x] T040c [US1] Load-time duplicate id repair (merge safety, document-format.md §2.6): a document with two nodes sharing an id (within one graph) or two members sharing an id (within a class) no longer fails the whole load with `DocumentFormatException` — `DocumentMapper` reassigns the later occurrence (document order) a fresh id via `StableIds.AllocateUnique` and reports `DocumentIssue.DuplicateIdReassigned` (new code `NPD007`) at `Warning`; `ValidateMemberIds` renamed `ValidateMemberIdShapes` (shape only, no longer duplicates) since the fix only needs uniqueness against ids seen so far. Tests: `DocumentMapperTests.DuplicateMemberIdIsReassignedAndReported`, `DuplicateNodeIdIsReassignedAndReported`. Fixture regeneration: `tests/NetPrints.Core.Tests/Fixtures/Golden/PinKeys.golden.txt` (only ids/graph keys changed, verified by diffing with ids normalized out)
- [x] T041 [US1] JSON Schema (document-format.md §6): `Json/NetPrintsJsonSchema.cs`; generate and commit `schemas/netpc.v1.schema.json`; DF-T24 in `tests/NetPrints.Core.Tests/Serialization/SchemaTests.cs` (the test locates the repository root with `SampleProjectFactory.FindRepositoryRoot()`). Inspect the exporter's polymorphism output first and record what `TransformSchemaNode` had to fix in research.md R17 (open item K14)
- [x] T042 [US1] Round trips DF-T02, DF-T03, DF-T05 (graph level) and the merge test DF-T23 in `…/Serialization/RoundTripTests.cs` and `…/Serialization/MergeTests.cs` (`git merge-file -p` via `System.Diagnostics.Process`, temp directory); switch `GoldenCSharpTests` to the new importer (DF-T01)
- [x] T043 [US1] **Checkpoint C**; commit

## Phase 4: User Story 1b — build pipeline (P1) (sub-phase D)

- [x] T044 [US1] `src/NetPrints.Generator/GraphCodeGenerator.cs` (`GenerateAsync`, `RenderFile`; project-system.md §3), `GenerateRequestFile.cs`, `Program.cs` (`generate` command, exit codes, canonical error lines with member-id graph keys) + unit tests PS-T06 (header, `\n`, determinism) in `tests/NetPrints.Core.Tests/Projects/GraphCodeGeneratorTests.cs`
- [x] T045 [US1] `src/NetPrints.Sdk/build/NetPrints.Sdk.props` and `NetPrints.Sdk.targets` exactly per project-system.md §2 (items, `Update` metadata, `Compile Remove`/`Include`, request file, `Exec`, `Touch`, `UpToDateCheckInput`); pack layout `tools/net10.0/` = framework-dependent publish of the generator
- [x] T046 [US1] In-repo development mode (project-system.md §2.1): `samples/Directory.Build.props`, `samples/Directory.Build.targets`, `samples/Directory.Packages.props`; `tests/NetPrints.Core.Tests/Projects/LocalSdkLayout.cs` helper; test projects reference `src/NetPrints.Generator` with `ReferenceOutputAssembly=false`
- [x] T047 [US1] Targets tests PS-T01, PS-T02, PS-T03, PS-T04 in `tests/NetPrints.Core.Tests/Projects/SdkTargetsTests.cs` (temp projects via `LocalSdkLayout`, `dotnet build -v n` log assertions, `-getItem:Compile`)
- [x] T048 [US1] Package test PS-T05 in `tests/NetPrints.Core.Tests/Projects/SdkPackageTests.cs` (`dotnet pack src/NetPrints.Sdk` into a temp feed, temp `nuget.config`, isolated `NUGET_PACKAGES`)
- [x] T049 [US1] **Checkpoint D**; commit

## Phase 5: User Stories 1c + 2 — project system, conversion, references (P1) (sub-phase E)

- [x] T050 [US2] Core records `src/NetPrints.Core/Projects/*.cs` (`ResolvedAssembly`, `ProjectMessage`, `ProjectSnapshot`, `ProjectReferenceInfo`, `ProjectEdit`, `BuildResult`, `ProcessStartRequest`, `IProjectSystem`, `IProcessRunner`, `ProcessResult`, `ProjectSystemException`) per project-system.md §4; `ProjectFiles.cs` with `GitAttributesLines` and an `EnsureGitAttributesAsync(string directory, CancellationToken)` helper implementing project-system.md §1.1 (returns the previous bytes or `null` so callers can roll back); `src/NetPrints.Core/Projects/ProcessRunner.cs` (System.Diagnostics.Process)
- [x] T051 [US2] `src/NetPrints.Workspace/MsBuildRegistration.cs` (UnrealSharp logic, idempotent, `false` without SDK, log 4005) and `MsBuildMessageParser.cs` + PS-T10 in `tests/NetPrints.Core.Tests/Projects/MsBuildMessageParserTests.cs`
- [ ] T052 [US1] Profiles: `src/NetPrints.Core/Profiles/IProjectProfile.cs`, `ClassTemplate.cs`, `DefaultProjectProfile.cs` (extension-points.md §5, `ProjectTemplate` = project-system.md §1)
- [ ] T053 [US2] `src/NetPrints.Workspace/MsBuildProjectSystem.cs`: `LoadAsync` (restore check, evaluation, MSBuildWorkspace, docs paths; logs 4001–4003), `ApplyAsync` (`ProjectRootElement`, atomic save), `CreateAsync` (`.csproj` + `.gitattributes`), `BuildAsync` (log 4004), `GetRunCommand`; tests PS-T07, PS-T08, PS-T09, PS-T11 and the `CreateAsync` part of PS-T15 in `tests/NetPrints.Core.Tests/Projects/MsBuildProjectSystemTests.cs` (local test package built into a temp feed for PS-T08)
- [ ] T054 [US1] `src/NetPrints.Serialization/Legacy/ProjectConverter.cs` (project-system.md §5 incl. `.gitattributes` and rollback, log 3006) + `convert` command in `src/NetPrints.Generator/Program.cs`; PS-T12 and the conversion part of PS-T15 in `tests/NetPrints.Core.Tests/Projects/ProjectConverterTests.cs`
- [ ] T055 [US1] Project model from snapshot (data-model.md §5): add `Project.FromSnapshot`, `Snapshot`, `GetGraphFilePath`, `CreateNewClass(profile)` (class starts dirty), `LastDiagnostics` next to the old members (which stay until T063 so the solution keeps building while the editor and CLI move over)
- [ ] T056 [US1] `src/NetPrints.Serialization/ProjectPersistence.cs` (document-format.md §2.8: dirty classes only, `EnsureUniqueMemberIds`, `MarkClean`) + DF-T11, DF-T15 in `…/Serialization/ProjectPersistenceTests.cs`
- [ ] T057 [US1] Convert `samples/HelloWorld` with the generator's `convert`, commit `HelloWorld.csproj` (with the conditional `PackageReference`), `HelloWorld.Program.netpc.json`, `samples/HelloWorld/.gitattributes` and the built `HelloWorld.Program.netpc.g.cs`; delete the legacy sample files; replace `SampleProjectFactory` output with this layout; update sample paths in `tests/NetPrints.Core.Tests/Samples/HelloWorldSampleTests.cs` (load through `ProjectPersistence`; compile/run through `IProjectSystem`), `tests/NetPrints.Editor.Tests/TestPaths.cs`, `tests/NetPrints.Editor.UITests/Hosting/TestDoubles.cs`, `tests/NetPrints.Testing.Ui/Scenarios/SmokeScenarios.cs`, `tests/NetPrints.Desktop.E2ETests/Scenarios/X11SmokeTests.cs`, README (FR-010); point the "CLI sample compile and run" step of `.github/workflows/ci.yml` at `tests/NetPrints.Core.Tests/Fixtures/Legacy/HelloWorld/HelloWorld.netpp` until T062; end-to-end DF-T01/SC-001 test: convert both fixtures, build, compare every `.g.cs` body with golden; add `*.netpc.json text eol=lf` and `*.netpc.g.cs text eol=lf` to the root `.gitattributes` (no `linguist-generated`: generated diffs stay visible in PRs); DF-T26 in `tests/NetPrints.Core.Tests/Samples/CommittedSampleTests.cs` (canonical graphs and up-to-date `.netpc.g.cs`; the failure message names the file and says to run the generator or `NETPRINTS_UPDATE_SNAPSHOTS=1`)
- [ ] T058 [US2] `src/NetPrints.Reflection`: `ReflectionProvider(IReadOnlyList<ResolvedAssembly>, IReadOnlyList<SourceFile> sources, IReadOnlySet<string> excludedAssemblyNames)`; `DocumentationUtil` from `DocumentationPath`; delete its `ProgramFilesX86` probe; update `tests/NetPrints.Editor.Tests/Reflection/ReflectionProviderTests.cs`; RC-T09 in `tests/NetPrints.Editor.Tests/Reflection/DocumentationTests.cs`
- [ ] T059 [US2] Editor switch-over: `EditorContext` gains `Projects`, `Persistence`, `Converter`; `MsBuildRegistration` in Desktop `Main` (`src/NetPrints.Desktop/Program.cs`; `EditorHostServices.MsBuildAvailable`, `NoSdkProjectSystem` for PS-T13); `ReflectionHost` from `Project.Snapshot` (logs 1010–1013; the catalog/`IExtensionHost` parameter is added in T076, until then the provider is the live one only; the mapper uses `NodeDocumentConverterRegistry.BuiltIn` until T076); `MainEditorVM` create/open (`*.csproj`, `*.netpp` → `ConfirmConversionAsync` → convert → open)/save/add-existing via persistence; Settings pane binary type via `ApplyAsync(SetOutputType)`, Output-flags chooser removed; `ReferenceListVM` on `DeclaredReferences` + `ProjectEdit`s; `src/NetPrints.Editor/Hosting/FileFilter.cs` updates; ED-T13 and PS-T13 in `tests/NetPrints.Editor.Tests/Main/MainEditorVMTests.cs` (update existing `.netpp` assertions), reference VM tests updated
- [ ] T060 [US1] Editor dirty tracking (editor-services.md §3): `UndoRedoStack.Applied` event (`src/NetPrints.Editor/UndoRedo/UndoRedoStack.cs`, raised by `Do`/`Undo`/`Redo`, not `Clear`); `ClassEditorVM` marks its class dirty on `Applied`, on `Node.OnPositionChanged` of every node of the class (subscribe/unsubscribe as nodes are added/removed) and in the inspector/wrapper setters (`MethodVM`, `MemberVariableVM`, class inspector); ED-T15 in `tests/NetPrints.Editor.Tests/ClassEditor/DirtyTrackingTests.cs`
- [ ] T061 [US2] Compile/Run via `IProjectSystem`: save all (dirty classes) → `BuildAsync` → `DiagnosticMapper.FromBuild` → `Project` state; Run through `GetRunCommand` + `IProcessLauncher` (Output pane); update `ClassEditorVM`, `MainEditorVM` and their tests
- [ ] T062 [US2] CLI `src/NetPrints.Cli/Program.cs` per editor-services.md §4 (build/run `.csproj`, convert `.netpp`); change the "CLI sample compile and run" step of `.github/workflows/ci.yml` to `-p samples/HelloWorld/HelloWorld.csproj -r`
- [ ] T063 [US2] Delete the old model/persistence APIs now that nothing calls them: `Project.CreateNew/LoadFromPath/Save/SaveClassInProjectDirectory/AddExistingClass/CompileProject/RunProject/GetRunCommand/References/ClassPaths/CompilationOutput/LastCompileErrors`, `src/NetPrints.Core/Serialization/SerializationHelper.cs`, `src/NetPrints.Core/Core/ReferenceAssemblyResolver.cs`, `src/NetPrints.Core/Compilation/CodeCompiler.cs`, `ICodeCompiler.cs`, and Core's legacy reference classes (`AssemblyReference`, `FrameworkAssemblyReference`, `SourceDirectoryReference`, `CompilationReference`, `ICompilationReference` in `src/NetPrints.Core/Core/`), with their `.gitattributes` lines; delete `tests/NetPrints.Core.Tests/Core/ReferenceAssemblyResolverTests.cs` (superseded by PS-T07) and rewrite `tests/NetPrints.Core.Tests/Compilation/DeterministicCompileTests.cs` on the build path (a temp copy of the converted HelloWorld plus 8 classes from `CreateNewClass`, saved with `ProjectPersistence.SaveAsync`; `IProjectSystem.BuildAsync`, record the output assembly and every `.netpc.g.cs`, delete `bin/` and `obj/`, build again, assert identical bytes); update `tests/NetPrints.Editor.UITests/Graph/EditFlowTests.cs` (reload through `ProjectPersistence.LoadAsync`); the T004 golden test now reads fixtures only through the new importer. Headless test: `WriteLine` node tooltip contains the summary (D5, SC-003) in `tests/NetPrints.Editor.UITests/Graph/NodeTooltipTests.cs`; grep test: no `ProgramFilesX86` under `src/`. Also remove the four temporary XML-documentation exemption sections in the root `.editorconfig` (`[src/NetPrints.Core/Serialization/SerializationHelper.cs]`, `[src/NetPrints.Core/Compilation/CodeCompiler.cs]`, `[src/NetPrints.Core/Compilation/ICodeCompiler.cs]`, `[src/NetPrints.Core/Core/ReferenceAssemblyResolver.cs]`) now that the files are gone
- [ ] T064 **Checkpoint E**: SC-001…SC-003, SC-009, SC-010; open the draft PR (`gh pr create --repo danielmeza/netprints --base master --draft`) for early review of A–E

---

## Phase 6: User Story 3 — extension points and loading (P1) (sub-phase F)

- [ ] T065 [US3] Translation seams (extension-points.md §2.1) in `src/NetPrints.Core/Translator/Extensibility/`; `ExecutionGraphTranslator` on `NodeTranslatorRegistry` (built-ins internal); `NPT006`; emitters (§3) and `ClassTranslator(TranslationEnvironment)`; update call sites (generator, editor); EX-T04, EX-T05 in `tests/NetPrints.Core.Tests/Translator/EmitterTests.cs`; golden tests unchanged
- [ ] T066 [P] [US3] Catalogs (§4) in `src/NetPrints.Reflection/Catalogs/`; EX-T06 in `tests/NetPrints.Editor.Tests/Reflection/CompositeReflectionProviderTests.cs`
- [ ] T067 [US3] `src/NetPrints.Extensibility/`: `ExtensionApi`, `INetPrintsExtension`, `IExtensionBuilder` (buffered; `AddProjectProperty`), `Nodes/*`, `BuiltInNodeLibrary` (PAR-53 suggestions moved from `src/NetPrints.Editor/Search/SuggestionItem.cs`), `BuiltInExtension`; EX-T12
- [ ] T068 [US3] `Loading/ExtensionManifest.cs`, `ExtensionLoadResult.cs`, `ExtensionLoaderOptions.cs` (`ExtensionFolders`), `ExtensionLoadContext.cs` (§8.2 incl. `Microsoft.Build*`), `ExtensionLoader.cs` (§8.1, logs 2001–2005), `ExtensionRegistry.cs`, `IExtensionHost`/`ExtensionHost` (load-context cache)
- [ ] T069 [P] [US3] Settings (§7): `ExtensionSettingsDescriptor`, `ISettingsStore`, `JsonFileSettingsStore` (log 2006), `NetPrintsSettings` (`ExtensionPaths`, `TrustedProjects`); EX-T09
- [ ] T070 [P] [US3] Host channel (§6) in `src/NetPrints.Extensibility/Hosting/`
- [ ] T071 [US3] Test asset `tests/NetPrints.TestExtension/` (added to `NetPrints.slnx` under `/tests/`; manifest, `EnableDynamicLoading`, NetPrints refs `Private=false`/`ExcludeAssets=runtime`): node kind `netprints.test/Log` (own `JsonSerializerContext`; translator emits `System.Console.WriteLine(<in>)`), class emitter (`[System.Obsolete("test")]` + using), member emitter (`DeclarePartial` for properties of classes named `Partial*`), catalog with one type, profile `netprints.test`, settings section, host-channel factory `test`, project property `NetPrintsTestMode`; output `bin/<cfg>/extensions/netprints.test/`
- [ ] T072 [US3] Loader tests EX-T01, EX-T10, EX-T11 in `tests/NetPrints.Core.Tests/Extensibility/ExtensionLoaderTests.cs`
- [ ] T073 [US3] Contribution tests EX-T02, EX-T03, EX-T07, DF-T17 in `…/Extensibility/ContributionTests.cs` (DF-T17 also asserts the extension node's canonical form: `name` omitted by default, pins by key, inline rules by property name)
- [ ] T074 [US3] Generator uses the request's `extension=` folders (project-system.md §3); PS-T14 in `tests/NetPrints.Core.Tests/Projects/GeneratorExtensionTests.cs`
- [ ] T075 [US3] Desktop/editor composition (editor-services.md §4): settings, `ExtensionHost`, host channel, complete `EditorHostServices`; `EditorContext.Extensions/HostChannel/Settings`; extension-failure dialog (ED-T11); trust flow on project open (`ConfirmTrustAsync`, `LoadForProject`, `NPD006`) — EX-T13 in `tests/NetPrints.Editor.Tests/Hosting/ProjectTrustTests.cs`
- [ ] T076 [US3] Editor use of contributions: search shows `NodeSuggestion`s by `AllowedIn`; `ReflectionHost` composes catalogs; persistence/mapper rebuilt on `RegistryChanged`; New Class uses the project's profile; `HostChannelBridge.cs` (types-changed → reload; focus-document → open/select; logs 1020–1022) + EX-T08; `ProjectSystemOptions.ExtraProperties` from the registry's `AddProjectProperty` names, test: the test extension reads `NetPrintsTestMode` via `ProjectSnapshot.GetProperty` (`tests/NetPrints.Editor.Tests/Hosting/ProjectPropertyTests.cs`, FR-025)
- [ ] T077 [US3] **Checkpoint F**: SC-004; commit

## Phase 7: User Story 4 — event graphs (P2) (sub-phase G)

- [ ] T078 [US4] Model `EventGraph` (with member `Id`, included in `ClassGraph.Members`/`EnsureUniqueMemberIds`), `EventEntryNode` (`GetPinKeyName` override: `Input<i>` for argument pins), `ClassGraph.EventGraphs` (data-model.md §4); `GraphKeys` `<eventGraphId>`; `GraphKeyTests` extended
- [ ] T079 [US4] `ExecutionGraphTranslator.TranslateEventEntry`, `ClassTranslator` event methods; golden `EventGraphs.cs` (custom `OnStart`, `OnTick`, override of `public virtual void OnReset()` from a hand-written `EventBase.cs` in the fixture project) + `NPT001`/`NPT002` cases in `tests/NetPrints.Core.Tests/Translator/EventGraphTranslatorTests.cs`; a test that pins the current rule "event methods follow entry node order" (swapping two entries in `Nodes` swaps the methods), recording open item K13
- [ ] T080 [US4] `eventEntry` converter + `EventGraphDocument` mapping (member `id`, layout key); round-trip test
- [ ] T081 [US4] Build/run test: fixture `.csproj` (LocalSdkLayout) whose `Main` calls `OnStart()`/`OnTick()`; `dotnet build` + run prints both lines
- [ ] T082 [US4] Editor event graph list, create/open/remove (undoable), search set, "Custom Event"/"Override <method>" suggestions; `AutomationIds.EventGraphList`, `CreateEventGraphButton`
- [ ] T083 [US4] ED-T07 in `tests/NetPrints.Editor.UITests/Events/EventGraphTests.cs` with `EventGraphsPage` in `tests/NetPrints.Testing.Ui`; **Checkpoint G**

## Phase 8: User Story 5 — method-local variables (P2) (sub-phase H)

- [ ] T084 [US5] Model `LocalVariable`, `VariableScope`, `ExecutionGraph.LocalVariables`, `IsLocalNameAvailable`, `VariableSpecifier.Scope`; `IsLocalVariable` on `Scope`
- [ ] T085 [US5] Translator: locals first, reserved names, getter/setter; `NPT004`; golden `Locals.cs`; build/run test (loop increments a local, prints it)
- [ ] T086 [US5] `locals` (inline records) + `VariableRef.scope` mapping; round-trip test
- [ ] T087 [US5] Editor `VariablesPanelVM`, `LocalVariableVM`, `EditorCommands` (create/rename/retype/remove incl. nodes), "Method Variables" search category, drag to canvas; `AutomationIds.VariablesClassGroup`, `VariablesMethodGroup`, `CreateLocalVariableButton`
- [ ] T088 [US5] VM tests in `tests/NetPrints.Editor.Tests/Variables/LocalVariableTests.cs`; ED-T06 in `tests/NetPrints.Editor.UITests/Variables/LocalVariablePanelTests.cs`; **Checkpoint H**

## Phase 9: User Story 6 — C# code view and diagnostics (P2) (sub-phase I)

- [ ] T089 [US6] Source map (research R3): `NodeOffsets`, `SourceMap`, `TranslatedClass`, `ClassTranslator.Translate`; RC-T06 in `tests/NetPrints.Core.Tests/Translator/SourceMapTests.cs`
- [ ] T090 [US6] `src/NetPrints.Core/Compilation/CodeDiagnostic.cs`, `DiagnosticMapper.cs` (compilation-and-diagnostics.md §1); RC-T10
- [ ] T091 [US6] `CodeAnalysisSession` (references/sources/options from `ProjectSnapshot`); RC-T08
- [ ] T092 [US6] `src/NetPrints.Editor/Diagnostics/ICodeAnalysisHost.cs`, `CodeAnalysisHost.cs` (log 1030); `EditorContext.CodeAnalysis`; ED-T02 (virtual time)
- [ ] T093 [US6] Bundle Cascadia Mono (OFL + license) in `src/NetPrints.Editor/Assets/Fonts/`
- [ ] T094 [US6] `CodeView/CodeViewVM.cs`, `CodeView.axaml(.cs)`, `SquiggleRenderer.cs`, `RoslynFoldingStrategy.cs`; AvaloniaEdit style include; theme switch; plain-text fallback
- [ ] T095 [US6] Replace the TextBox in `src/NetPrints.Editor/Inspectors/ClassInspectorView.axaml` (`AutomationIds.ClassInspectorCodeView`); page objects
- [ ] T096 [US6] `ErrorListVM`, `DiagnosticRowVM`, `NavigateToNodeMessage`; bind the Errors tab; navigation + `NodeGraphVM.RevealNode`; ED-T03 (VM), ED-T04
- [ ] T097 [US6] Headless ED-T01, ED-T03, ED-T05 in `tests/NetPrints.Editor.UITests/CodeView/CodeViewTests.cs`; regenerate/review baselines; **Checkpoint I** (SC-006 recorded)

## Phase 10: User Story 7 — maintainability follow-ups (P3) (sub-phase J)

- [ ] T098 [US7] Remove the `customize` hook (`EditorComposition(EditorHostServices)` only); `tests/NetPrints.Editor.UITests/Hosting/HeadlessApp.cs` → explicit `TestComposition`; ED-T14
- [ ] T099 [US7] `ClassEditorServices`; `MemberVariableVM`/`NodeGraphVM` take it; `SelectInspectorMessage`; model-driven cleanup; ED-T08 (dirty marking of T060 keeps working: ED-T15 stays green)
- [ ] T100 [US7] Model wrapper setters → assign-only + model `PropertyChanged` forwarding (each still marks the class dirty, ED-T15)
- [ ] T101 [US7] Nodify commands in `NodeGraphVM`, bound in `src/NetPrints.Editor/Graph/GraphEditorView.axaml`; remove handlers from `GraphEditorView.axaml.cs`; disable Alt+click disconnect; ED-T09
- [ ] T102 [US7] Architecture gate ED-T10 + fixture `tests/NetPrints.Editor.Tests/Architecture/Fixtures/ViolatingVM.cs.txt` (fails on the fixture first); rule A3 includes Workspace/Generator/Sdk never referencing Avalonia and Generator never referencing `Microsoft.Build*`
- [ ] T103 [US7] Serialization log call sites (3001–3006) with a collecting logger in `tests/NetPrints.Core.Tests/Serialization/LoggingTests.cs`; **Checkpoint J** (SC-007, SC-008)
- [ ] T103a [US7] Findings from the XML-doc pass (2026-09-25). Each gets a failing test first, where it is a bug:
  - `GenericType.Equals(object)` returns `true` for any `TypeSpecifier`, and `TypeSpecifier.Equals(object)` has the mirror placeholder for `GenericType` and throws `ArgumentException` when only `IsEnum` differs. Define the intended equality (with `GetHashCode`) and test it.
  - The `genericArgumentTypes` constructor parameter of `CallMethodNode` is unused: use it or remove it.
  - Remove the unreferenced `[Obsolete]` visibility values in `VariableModifiers`, `MethodModifiers` and `ClassModifiers`. Legacy import must still read old files: check with the legacy fixture and map them if old files use them.
  - Remove the marker interface `ICompilationReference`, or make `Project.References` use it.
  - Remove the dead `ExecutionGraphTranslator.TranslateMethodEntry` body, or restore its intent. Golden tests must stay unchanged.
  - Add tests for the null guards added in T011/T012 that are not covered yet: the `Graph.Class` guard in `NodeGraphVM.Drop` and the two `ExecutionGraphTranslator` guards.

## Phase 11: Polish (sub-phase K)

- [ ] T104 [P] User docs as site pages (release-and-docs.md §8 content layout), each with a one-line summary in the README that links it: `docs/guide/projects.md` (`.csproj` + `NetPrints.Sdk`, committed `.netpc.g.cs`: regenerate, never hand-merge, keep visible in PRs; VS Code nesting pattern; SDK requirement), `docs/guide/graph-format.md` (the graph file format for version control: `$schema`, `.gitattributes`, what a node move or add looks like in a diff; links to the document-format contract on GitHub), `docs/guide/extensions.md` (`NETPRINTS_EXTENSION_PATH`/`NETPRINTS_HOST_CHANNEL`/`NETPRINTS_LOG_LEVEL`, extension project requirements, `NetPrintsExtension` items). Plain CommonMark (the site renders `.md` without MDX); no HTML
- [ ] T105 [P] SC-005 measurement (open HelloWorld restored: evaluation + workspace + extensions + types), recorded in the PR; 3× regression bound only
- [ ] T106 Manual IDE check (research K9): build and nesting of the converted HelloWorld in Visual Studio 2022, Visual Studio 2026 and Rider; VS Code shows schema validation for `HelloWorld.Program.netpc.json` once the `$schema` URL resolves (after the owner enabled Pages; else note it); record results in the PR
- [ ] T107 `dotnet format --verify-no-changes`, full suite, E2E once on a private Xvfb (quickstart §6)
- [ ] T108 **After Checkpoint L (T122).** Update checkboxes and research notes with findings (R17 open items K13, K14; R18/R19 findings); mark the PR ready; CI, Docs and the Release dry run green; independent review per AGENTS.md

---

## Phase 12: User Story 8 — release, packages and docs (P3) (sub-phase L)

Normative: `contracts/release-and-docs.md` (§ numbers below). Each task ends with its acceptance commands
passing locally; T122 checks them in CI. Never push a tag, push packages or set `PUBLISH_DOCS`/`PUBLISH_WIKI`.

- [ ] T109 [US8] Versioning (§1): `MinVer` 8.0.0 as `GlobalPackageReference` in `Directory.Packages.props`; `MinVerTagPrefix`, `MinVerMinimumMajorMinor`, `MSBuildWarningsNotAsErrors` `MINVER1001` in `Directory.Build.props`; delete `<Version>` and `<Copyright>` from `src/NetPrints.Core/NetPrints.Core.csproj`; `fetch-depth: 0` on the `build-test` checkout in `.github/workflows/ci.yml`. Accept: `dotnet msbuild src/NetPrints.Core/NetPrints.Core.csproj -t:MinVer -getProperty:MinVerVersion -p:Configuration=Release` prints `0.1.0-alpha.0.<n>`; `dotnet pack src/NetPrints.Core -c Release -p:MinVerVersionOverride=9.9.9 -o /tmp/p` writes `NetPrints.Core.9.9.9.nupkg`; a copy of the repository without `.git` (`git archive HEAD | tar -x -C /tmp/src`) builds with a `MINVER1001` warning and 0 errors; `grep -rn "<Version>" src tests` finds nothing; DF-T26 and the golden tests stay green (the generated header has no version, project-system.md §3)
- [ ] T110 [US8] Package metadata (§2): `IsPackable=false` and the "Package metadata" group in `Directory.Build.props`; new `Directory.Build.targets`; `eng/PackageReadme.targets`; `assets/icons/netprints-icon.png` = `convert 'src/NetPrints.Desktop/NetPrintsLogo.ico[4]' assets/icons/netprints-icon.png` (the 256×256 frame). Accept: `dotnet pack NetPrints.slnx -c Release -o /tmp/p0` packs nothing yet except projects already marked packable (none before T111); `dotnet build NetPrints.slnx -c Release` 0 warnings
- [ ] T111 [US8] Packable projects (§3): `IsPackable`, descriptions with the attribution sentence, `EnablePackageValidation` (libraries) in `src/NetPrints.Core`, `src/NetPrints.Reflection`; `IsPackable` + `IncludeSymbols=false` in `src/NetPrints.Sdk`; `PackAsTool`/`ToolCommandName=netprints`/`PackageId=NetPrints.Cli` in `src/NetPrints.Cli`; direct `Microsoft.CodeAnalysis.Workspaces.MSBuild` reference in `src/NetPrints.Cli` and `src/NetPrints.Desktop` (roslyn#80127). Accept RL-T01: `dotnet pack NetPrints.slnx -c Release -o /tmp/p1` writes exactly the seven files of §3; `unzip -l` of the Cli package lists `tools/net10.0/any/BuildHost-netcore/`; 0 warnings (warnings as errors)
- [ ] T112 [US8] Local feed (§4): `NuGet.config`, `local-packages/.gitkeep`, the `.gitignore` block, `scripts/pack-local.sh` (executable). Accept: with an empty `local-packages/`, `dotnet restore NetPrints.slnx` succeeds (no NU1301); `scripts/pack-local.sh --print-version` prints only a version matching `^0\.1\.0-local\.[0-9]{14}$` and fills `local-packages/`; `git status --porcelain` shows no file under `local-packages/`
- [ ] T113 [US8] `scripts/verify-packages.sh` (§4 steps 1–4) and the `packages` job in `.github/workflows/ci.yml` (§6). Accept RL-T02…RL-T05: `scripts/verify-packages.sh local-packages "$(scripts/pack-local.sh --print-version)"` exits 0; it exits non-zero with a message naming the check when a package is removed from the feed, and when `README.md` is replaced by one containing `<p>` (both shown once in the commit message)
- [ ] T114 [US8] Self-contained editor (§5, research R19): publish properties in `src/NetPrints.Desktop/NetPrints.Desktop.csproj`; `src/NetPrints.Desktop/ProjectCheck.cs` and the `--check-project` branch in `Program.Main` (exit codes of §5); `scripts/smoke-desktop.sh`; the `desktop-publish` job in `.github/workflows/ci.yml` (§6); RL-T07 grep test `tests/NetPrints.Core.Tests/Architecture/NoRuntimeDirectoryReferencesTests.cs` (locates the repository with `SampleProjectFactory.FindRepositoryRoot()`); `ProjectCheck` unit tests in `tests/NetPrints.Editor.Tests/Hosting/ProjectCheckTests.cs` for exit codes 1 (a copy of HelloWorld with a broken connection → `analysis: 1 errors` and the canonical error line), 2 (no argument) and 3 (fake registration without SDK → `NPW001`). Accept RL-T06: the §6 publish command then `scripts/smoke-desktop.sh out/linux-x64` exits 0 with no `DISPLAY`
- [ ] T115 [US8] Release workflow (§7): `.github/workflows/release.yml`, `.github/release.yml`, `.github/release-notes.md`, `scripts/archive-desktop.sh`. Accept: `actionlint` clean; locally `scripts/archive-desktop.sh out/linux-x64 0.0.0-test linux-x64` gives a `.tar.gz` whose first entry is `NetPrints-0.0.0-test-linux-x64/` and whose apphost keeps mode `755`, and the `win-x64` form gives a `.zip` with the same top folder; RL-T12 by inspection; RL-T10 in T122
- [ ] T116 [P] [US8] API reference (§9): `.config/dotnet-tools.json` (docfx 2.81.0), `docs/api/docfx.json`, `docs/api/index.md`, `docs/api/toc.yml`. Accept: `dotnet tool restore && dotnet docfx docs/api/docfx.json` exits 0; `docs/api/_site/index.html` and the `NetPrints.Graph.Node` page exist; `git status` shows no generated file (ignored by §4)
- [ ] T117 [US8] Docs site (§8): `website/` (`package.json` + `package-lock.json` from `npm install`, `.nvmrc`, `docusaurus.config.ts`, `tsconfig.json`, `src/css/custom.css`, `src/remark/repo-links.mjs`), `docs/index.md`, `docs/guide/_category_.json`, `docs/contributing/_category_.json` + `releasing.md` (local feed, release process, the owner steps of §11), `docs/adr/_category_.json`, `docs/research/_category_.json`; `scripts/build-docs.sh`; fix whatever the first build reports in existing `docs/**` pages (only links, headings and raw HTML; content unchanged). Accept RL-T08, RL-T09: `scripts/build-docs.sh` exits 0; the files RL-T08 lists exist; `grep -r "/specs/" website/build --include=*.html -l` lists only pages whose links point to `github.com/danielmeza/netprints/blob/`; adding `[x](./missing.md)` to `docs/index.md` makes the build fail (revert; note in the commit message)
- [ ] T118 [US8] `.github/workflows/docs.yml` (§10) and the `npm` entry in `.github/dependabot.yml`. Accept: `actionlint` clean; on the PR the `build` job is green and uploads the Pages artifact, `deploy` is skipped
- [ ] T119 [P] [US8] Wiki (§10): `.github/wiki/Home.md`, `.github/wiki/_Sidebar.md`, `.github/workflows/wiki.yml`. Accept: `actionlint` clean; every link in both pages is absolute; the workflow has no `pull_request` trigger and its job is gated by `vars.PUBLISH_WIKI` (RL-T11)
- [ ] T120 [US8] README (§12): badges (CI, Release, NuGet `NetPrints.Sdk`, Docs, License); "Install" section (editor download table, `dotnet tool install -g NetPrints.Cli`, `dotnet add package NetPrints.Sdk`, the .NET 10 SDK requirement and "running a compiled graph needs the .NET runtime the SDK includes"); link to the site; shortened build-from-source part under "Contributing". `docs/guide/install.md`: the same three routes in full, the SDK and runtime requirement (10.0 SDK specifically, research R19), unsigned-build steps (SmartScreen, macOS *Open Anyway*/`xattr`), checksum and attestation verification, `--check-project`. Accept: every README link and anchor resolves (relative links on GitHub; `scripts/build-docs.sh` for the page); the packed README of `NetPrints.Core` passes `verify-packages.sh` step 2
- [ ] T121 [P] [US8] `docs/adr/0002-release-and-docs-stack.md` (§12, format of ADR 0001). Accept: shows in the site's Decisions category after `scripts/build-docs.sh`
- [ ] T122 [US8] **Checkpoint L**: full suite, `dotnet format --verify-no-changes`, `scripts/verify-packages.sh`, `scripts/smoke-desktop.sh`, `scripts/build-docs.sh`, `actionlint` locally; push; on the PR: `CI` (incl. `packages`, `desktop-publish`), `Docs` and the `Release` dry run green (RL-T10: download `release-assets`, `sha256sum -c SHA256SUMS.txt`, 7 package files and 3 archives); list the owner steps of §11 in the PR description (SC-011…SC-015)

---

## Dependencies & Execution Order

- A (T001–T006) → B (T007–T023) → C (T024–T043) → D (T044–T049) → E (T050–T064) → F (T065–T077) → G (T078–T083), H (T084–T088) (independent of each other) → I (T089–T097) → J (T098–T103) → K (T104–T107) → L (T109–T122) → T108.
- Hard edges: T004/T005 before T007; T015 before T016, T017; T016–T019 before T035; T025 before T026, T027; T027 before T037, T041; T030 before T031–T034; T044 before T045–T048; T046 before T047, T057, T081; T050 before T053, T054, T056; T052 before T053 and T054; T054 before T057; T055 before T056, T059; T059 before T060; T059–T062 before T063; T065 before T067, T074, T079; T068 before T075; T089 before T090, T096; T041, T057, T062, T063 before L; T109 before T110; T110 before T111; T111 before T112, T113, T114; T112 before T113; T114 before T115; T116 before T117; T117 before T118, T120, T121; T104 before T117 (its pages are in the site).

## Parallel examples

- B: T011 ∥ T012 ∥ T013 ∥ T014; T018 ∥ T019 once T016 exists. C: T024 ∥ T025; T031 ∥ T032 ∥ T033 ∥ T034; T040 alongside T035–T039.
- E: T051 ∥ T053 once T050 exists. F: T066 ∥ T069 ∥ T070 once T067 exists.
- L: T116 ∥ T113 once T111 exists; T119 ∥ T121 any time after T117.

## Implementation strategy

MVP = A–E: the roadmap "done when" (old sample → `.csproj` + JSON graphs in the VCS-friendly format,
builds, identical C#, committed `.netpc.g.cs` up to date), reviewed early as a draft PR. F–K follow in
the same PR.

## Requirement coverage (graph format)

| Requirement | Tasks |
|---|---|
| FR-043 random, seedable node ids; legacy `n0…` | T015, T016, T038 |
| FR-044 member ids; layout keyed by them | T017, T035, T078, T080 |
| FR-045 pins by name | T018, T035 |
| FR-046 canonical writer, integer positions, `$schema`, default names omitted | T027, T035, T037 |
| FR-047 tolerant reader | T026, T037 |
| FR-048 edited-only saves; auto-placement | T017, T019, T035, T056, T060 |
| FR-049 JSON Schema | T041 |
| FR-050 `.gitattributes`; committed-file checks in CI | T050, T053, T054, T057 |
| SC-009 merge and reorder behaviour | T017, T035, T042 |
| SC-010 committed schema, graphs, `.netpc.g.cs` up to date | T041, T057 |

## Requirement coverage (release, packages and docs)

| Requirement | Tasks |
|---|---|
| FR-051 versions from `v*` tags, none hard-coded | T109, T112, T113, T115 |
| FR-052 exactly four packable projects | T110, T111 |
| FR-053 package metadata, icon, README, symbols, attribution | T110, T111, T113 |
| FR-054 local feed, pack and verify scripts in CI | T112, T113 |
| FR-055 release workflow: tags publish, dry runs never | T115, T122 |
| FR-056 Trusted Publishing, GitHub Release, checksums, attestations, notes | T115 |
| FR-057 self-contained folder archives for three RIDs | T114, T115 |
| FR-058 MSBuild-resolved references only; `--check-project`; SDK/runtime requirement stated | T063, T114, T115, T120 |
| FR-059 docs site, API reference, research in, specs out | T104, T116, T117, T118 |
| FR-060 `$schema` URL on the site | T041, T117, T118 |
| FR-061 wiki pointer | T119 |
| FR-062 README install section, badges; ADR 0002 | T120, T121 |
| FR-063 owner steps documented and tolerated | T113, T115, T117, T118, T119, T122 |
| SC-011 dry run green, publishes nothing | T115, T122 |
| SC-012 tool and SDK from the local feed | T113 |
| SC-013 self-contained editor analyses and builds HelloWorld | T114 |
| SC-014 docs site builds with 0 broken links, API and schema | T116, T117, T118 |
| SC-015 no hard-coded versions; version patterns | T109, T112, T113 |

## Test-ID map (revisions 2026-09-25)

| Test ID | Status | Now in |
|---|---|---|
| DF-T01 | kept | T004 (baseline), T042, T057 |
| DF-T02, T03, T05 | kept (T03 adds re-parse; T05 is now "exactly one line") | T042 |
| DF-T04 | changed (inline records, `$schema`, integer coordinates, escaping) | T027 |
| DF-T06, T08 | kept (T08: re-emitted canonical, byte-identical for canonical input) | T035 |
| DF-T07 | kept | T028 |
| DF-T09 | kept | T037 |
| DF-T10 | kept | T036 |
| DF-T11 | changed (malformed graph among others; no project JSON) | T056 |
| DF-T12…T14 | kept | T040 |
| DF-T15 | changed (dirty classes only) | T056 |
| DF-T16 | kept | T039 |
| DF-T17 | kept (+ canonical form of the extension node) | T073 |
| DF-T18 | changed (random seeded ids, member ids, deterministic legacy import) | T016, T017, T038 |
| DF-T19…T26 | new (pins by name, auto-placement, tolerant read, member ids, merge, schema, default names, committed samples) | T018/T035, T019/T035, T026/T037, T017/T035, T042, T041, T035, T057 |
| DF-T27 | new (§3.1: post-load type-pin wiring and relaxation, both paths) | T020 (legacy-import half), T035 (JSON-path half) |
| RC-T01…T05 | retired (custom resolver removed) → PS-T07, PS-T08, PS-T12 | T053, T054 |
| RC-T06, T08, T09, T10 | kept | T089, T091, T058, T090 |
| RC-T07 | retired → PS-T11 | T053 |
| PS-T01…T14 | new | T044–T048, T051, T053, T054, T059, T074 |
| PS-T15 | new (`.gitattributes`) | T053, T054 |
| EX-T01…T12 | kept (EX-T07 reworded for `CreateAsync`) | T065–T076 |
| EX-T13 | changed (trust flow) | T075 |
| ED-T01…T14 | kept (ED-T04, ED-T13 reworded for builds, conversion and edited-only saves) | T022, T059, T075, T083, T088, T092, T096–T102 |
| ED-T15 | new (dirty tracking) | T060 |
| RL-T01…T12 | new (release, packages, docs) | T111 (T01), T113 (T02–T05), T114 (T06, T07), T117 (T08, T09), T122 (T10), T115/T118/T119 (T11), T115 (T12) |
