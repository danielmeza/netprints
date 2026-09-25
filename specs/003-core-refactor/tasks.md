---
description: "Task list for P1 — Core refactor and extension points (csproj model)"
---

# Tasks: Core Refactor and Extension Points

**Input**: `/specs/003-core-refactor/` — plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Start condition**: rebase `003-core-refactor` onto `master` after the reorganization PR merges
(plan.md). All paths below are post-reorg.

**Revision 2026-09-25**: renumbered for the csproj model (owner decision). The old custom project file,
reference resolver and `ProjectCompiler` tasks are gone; see "Test-ID map" at the end.

**Tests**: REQUIRED (constitution V). Test-obligation ids (`DF-`, `PS-`, `EX-`, `RC-`, `ED-Txx`) refer to
the tables at the end of each contract; a task that names an id must implement exactly that case. Every
test uses xUnit v3 and passes `TestContext.Current.CancellationToken`; UI tests use page objects and
`AutomationIds`, no sleeps (research U17). Tests that run MSBuild call `MsBuildRegistration.EnsureRegistered()`
from a `[ModuleInitializer]`. Checkpoints require `dotnet build NetPrints.slnx -c Release` (0 warnings) and
`dotnet test --solution NetPrints.slnx -c Release` green.

**Rules for the implementer**: match comment density (AGENTS.md); rationale in commit messages; one
commit per task or small group; never change a golden file without regenerating it with
`NETPRINTS_UPDATE_SNAPSHOTS=1` and saying why in the commit. One PR, opened as a draft after
Checkpoint E (plan.md).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelizable (different files, no dependency on an unfinished task)
- **[Story]**: US1…US7 (spec.md). Setup, Foundational and Polish tasks have no story label.

---

## Phase 1: Setup and characterization (sub-phase A) — on unmodified behavior

- [ ] T001 Add to `Directory.Packages.props`: `Avalonia.AvaloniaEdit` 12.0.0, `AvaloniaEdit.TextMate` 12.0.0, `Microsoft.Extensions.Logging.Abstractions`/`Logging`/`Logging.Console` 10.0.12, `Microsoft.Build.Locator` 1.11.2, `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.9.0, `Microsoft.Build` and `Microsoft.Build.Framework` 18.0.2 (research §3, R16). Do not remove Fody yet
- [ ] T002 [P] Create `tests/NetPrints.Core.Tests/Characterization/AllNodesFixtureFactory.cs`: builds, through the current Core API, project `AllNodes` with class `AllNodes.Everything` that uses every one of the 23 built-in node kinds at least once (document-format.md §1.5 minus `eventEntry`), including: method with 2 arguments, 1 generic argument and 2 return values; constructor; class variable with getter and setter and a generic type built in its type graph; a class interface pin; `MakeArrayNode` with predefined size and with 3 elements; data/exec/type reroutes; renamed editable pins; unconnected values of types bool, int, long, double (`NaN`), decimal, char, string with quotes and newline, and an enum; one `AssemblyReference` and one `SourceDirectoryReference`. Writes legacy XML with `Project.Save` only when `NETPRINTS_REGENERATE_SAMPLES=1`
- [ ] T003 Generate and commit the legacy fixtures: `tests/NetPrints.Core.Tests/Fixtures/Legacy/AllNodes/*` (T002) and a copy of `samples/HelloWorld/*` into `tests/NetPrints.Core.Tests/Fixtures/Legacy/HelloWorld/`; link them as test content in `tests/NetPrints.Core.Tests/NetPrints.Core.Tests.csproj`
- [ ] T004 Add `tests/NetPrints.Core.Tests/Characterization/GoldenCSharpTests.cs`: for each class of both legacy fixtures, load with `Project.LoadFromPath`, translate with today's `ClassTranslator`, compare to `Fixtures/Golden/<Class.FullName>.cs` (write when `NETPRINTS_UPDATE_SNAPSHOTS=1`). Generate and commit the golden files (DF-T01 baseline)
- [ ] T005 [P] Add `tests/NetPrints.Core.Tests/Characterization/NotificationMapTests.cs`: for every public non-abstract type in `NetPrints.Core`/`NetPrints.Graph` implementing `INotifyPropertyChanged` (instances from the AllNodes fixture), set each public settable property to a different valid value and record the raised property names (a setter that throws for that instance, e.g. `IsPure` when `CanSetPure` is false, is recorded as `"<throws>"`; order = ordinal type name, then property name); compare to `Characterization/NotificationMap.golden.json`. Generate on the Fody build and commit (research R6)
- [ ] T006 Run the suite; commit. **Checkpoint A**

---

## Phase 2: Foundational (sub-phase B) — blocks all stories

- [ ] T007 Create `src/NetPrints.Core/Core/ModelObject.cs` (data-model.md §1); add `CommunityToolkit.Mvvm` to `src/NetPrints.Core/NetPrints.Core.csproj`
- [ ] T008 Migrate `NetPrints.Graph.Node`, `NodePin` and all pin subclasses to `ModelObject` with `[ObservableProperty]` partial properties (data-model.md §1); rename `OnInputTypeChanged` → `HandleInputTypeChanged` in `Node` and all overrides in `src/NetPrints.Core/Graph/*.cs`
- [ ] T009 Migrate `Variable` and `Project` (`src/NetPrints.Core/Core/Variable.cs`, `Project.cs`) to `ModelObject`; computed properties raise via `[NotifyPropertyChangedFor]`
- [ ] T010 Remove `Fody`/`PropertyChanged.Fody` from `src/NetPrints.Core/NetPrints.Core.csproj` and `Directory.Packages.props`; delete `FodyWeavers.xml/.xsd`; remove `[DoNotNotify]` in `Graph/TypeNode.cs`. T004 and T005 pass unchanged (FR-036)
- [ ] T011 [P] Nullable + warnings-as-errors in `src/NetPrints.Core` `Core/` and `Graph/`
- [ ] T012 [P] Same for `src/NetPrints.Core` `Translator/`, `Compilation/`, `Serialization/`
- [ ] T013 [P] Same for `src/NetPrints.Reflection` (10 `RS1024` sites → `SymbolEqualityComparer.Default`)
- [ ] T014 [P] Same for `tests/NetPrints.Core.Tests` and `src/NetPrints.Cli`; make `TreatWarningsAsErrors=true` the default in `Directory.Build.props` and delete every per-project `Nullable=disable`/`TreatWarningsAsErrors` override (FR-037)
- [ ] T015 `Node.Id`, `NodeGraph.AllocateNodeId/FindNode/AssignLegacyNodeIds/PreservedDocumentState`, `GraphKeys` (data-model.md §2) in `src/NetPrints.Core/Graph/Node.cs`, `Core/NodeGraph.cs`, `Core/GraphKeys.cs`; `[OnDeserializing]` init; tests `tests/NetPrints.Core.Tests/Core/NodeIdTests.cs` (DF-T18, `GraphKeys` round trip for every graph of AllNodes)
- [ ] T016 Extract `GraphTypeInference.Relax(NodeGraph)` into `src/NetPrints.Core/Graph/GraphTypeInference.cs` from `MethodGraph.OnDeserialized`; golden tests unchanged
- [ ] T017 Create project shells, all net10.0, nullable, warnings as errors, `CA1848`/`CA2254` as errors, in `NetPrints.slnx`: `src/NetPrints.Serialization`, `src/NetPrints.Extensibility`, `src/NetPrints.Workspace` (Locator + Workspaces.MSBuild; `Microsoft.Build`/`Microsoft.Build.Framework` with `ExcludeAssets="runtime" PrivateAssets="all"`, research R14 MSBL001), `src/NetPrints.Generator` (Exe), `src/NetPrints.Sdk` (pack-only: `IncludeBuildOutput=false`, `DevelopmentDependency=true`, `SuppressDependenciesWhenPacking=true`); `InternalsVisibleTo("NetPrints.Serialization")` in Core
- [ ] T018 Logging foundation: Logging.Abstractions in `src/NetPrints.Editor`, Logging + Console in `src/NetPrints.Desktop`; `CA1848`/`CA2254` errors in Editor and Desktop; `EditorHostServices` with first member `ILoggerFactory LoggerFactory` (rest in T060), `EditorApp.HostServices` (set by Desktop `Program`; `InvalidOperationException` if unset), `EditorComposition(EditorHostServices host, …)` (P0 `customize` hook stays until T091), required `EditorContext.LoggerFactory`, `src/NetPrints.Desktop/AvaloniaLogSink.cs` replacing `.LogToTrace()`, `src/NetPrints.Editor/Hosting/Log.cs` with 1001/1002 used by `UnhandledExceptionHandler`; ED-T12 in `tests/NetPrints.Editor.Tests/Hosting/UnhandledExceptionHandlerTests.cs`; test compositions pass `NullLoggerFactory`
- [ ] T019 Commit. **Checkpoint B**: 0 warnings; A-gates unchanged; no Fody (SC-007 partial)

---

## Phase 3: User Story 1a — graph documents (P1) 🎯 (sub-phase C)

- [ ] T020 [P] [US1] `src/NetPrints.Serialization/DocumentId.cs`, `DocumentIssue.cs`, `DocumentExceptions.cs`, `DiagnosticExtensions.cs` (document-format.md §2.1, compilation-and-diagnostics.md §1) + `tests/NetPrints.Core.Tests/Serialization/DocumentIdTests.cs`
- [ ] T021 [P] [US1] DTO records in `src/NetPrints.Serialization/Documents/` (`ClassDocument.cs`, `GraphDocuments.cs`, `NodeDocuments.cs` with all 24 kinds, `Refs.cs`) exactly per document-format.md §1.4–§1.6/§2.4
- [ ] T022 [US1] `Json/NetPrintsJsonContext.cs`, `NetPrintsJsonOptions.cs`, `NodeListConverter.cs` (§2.3) + DF-T04 in `…/Serialization/CanonicalJsonTests.cs`
- [ ] T023 [US1] `Mapping/TypedValueConverter.cs` + DF-T07
- [ ] T024 [US1] `Mapping/NodeMappingContext.cs` + `ToRef`/`FromRef` tests in `…/Serialization/RefMappingTests.cs`
- [ ] T025 [US1] `Mapping/INodeDocumentConverter.cs`, `NodeDocumentConverterRegistry.cs` + registry tests
- [ ] T026 [P] [US1] Built-in converters `methodEntry`, `constructorEntry`, `return`, `classReturn`, `typeReturn` in `Mapping/BuiltIn/EntryReturnConverters.cs`
- [ ] T027 [P] [US1] `callMethod`, `constructor`, `makeDelegate`, `variableGetter`, `variableSetter` in `Mapping/BuiltIn/MemberConverters.cs`
- [ ] T028 [P] [US1] `literal`, `type`, `makeArrayType`, `makeArray`, `explicitCast`, `typeOf`, `default` in `Mapping/BuiltIn/ValueConverters.cs`
- [ ] T029 [P] [US1] `ifElse`, `forLoop`, `ternary`, `await`, `throw`, `reroute` in `Mapping/BuiltIn/FlowConverters.cs`
- [ ] T030 [US1] `Mapping/DocumentMapper.cs` (class mapping, pins, edges, layout, preserved unknown nodes, issues) + DF-T06, DF-T08
- [ ] T031 [US1] `Migrations/IDocumentMigration.cs`, `DocumentMigrator.cs` + DF-T10
- [ ] T032 [US1] `Json/JsonDocumentFormat.cs` + DF-T09
- [ ] T033 [US1] Add DataContract-compatible **copies** of the legacy project/reference classes in `src/NetPrints.Serialization/Legacy/` (`LegacyProject`, `LegacyCompilationReference`, `LegacyAssemblyReference`, `LegacyFrameworkAssemblyReference`, `LegacySourceDirectoryReference`; `[DataContract(Name = …, Namespace = …)]` and `[KnownType]` matching the originals so `.netpp` files deserialize; Core's originals stay until T056; data-model.md §5) + a test reading both fixtures' `.netpp`; `Legacy/LegacyXmlDocumentFormat.cs` (§2.2, §3) + test: both fixtures' classes import without issues
- [ ] T034 [US1] `DocumentFormatRegistry.cs` + DF-T16
- [ ] T035 [P] [US1] `Stores/IDocumentStore.cs`, `InMemoryDocumentStore.cs`, `FileSystemDocumentStore.cs` (§2.7) + shared abstract store tests DF-T12…T14 in `…/Serialization/Stores/`
- [ ] T036 [US1] Round trips DF-T02, DF-T03, DF-T05 (graph level) in `…/Serialization/RoundTripTests.cs`; switch `GoldenCSharpTests` to the new importer (DF-T01)
- [ ] T037 [US1] **Checkpoint C**; commit

## Phase 4: User Story 1b — build pipeline (P1) (sub-phase D)

- [ ] T038 [US1] `src/NetPrints.Generator/GraphCodeGenerator.cs` (`GenerateAsync`, `RenderFile`; project-system.md §3), `GenerateRequestFile.cs`, `Program.cs` (`generate` command, exit codes, canonical error lines) + unit tests PS-T06 (header, `\n`, determinism) in `tests/NetPrints.Core.Tests/Projects/GraphCodeGeneratorTests.cs`
- [ ] T039 [US1] `src/NetPrints.Sdk/build/NetPrints.Sdk.props` and `NetPrints.Sdk.targets` exactly per project-system.md §2 (items, `Update` metadata, `Compile Remove`/`Include`, request file, `Exec`, `Touch`, `UpToDateCheckInput`); pack layout `tools/net10.0/` = framework-dependent publish of the generator
- [ ] T040 [US1] In-repo development mode (project-system.md §2.1): `samples/Directory.Build.props`, `samples/Directory.Build.targets`, `samples/Directory.Packages.props`; `tests/NetPrints.Core.Tests/Projects/LocalSdkLayout.cs` helper; test projects reference `src/NetPrints.Generator` with `ReferenceOutputAssembly=false`
- [ ] T041 [US1] Targets tests PS-T01, PS-T02, PS-T03, PS-T04 in `tests/NetPrints.Core.Tests/Projects/SdkTargetsTests.cs` (temp projects via `LocalSdkLayout`, `dotnet build -v n` log assertions, `-getItem:Compile`)
- [ ] T042 [US1] Package test PS-T05 in `tests/NetPrints.Core.Tests/Projects/SdkPackageTests.cs` (`dotnet pack src/NetPrints.Sdk` into a temp feed, temp `nuget.config`, isolated `NUGET_PACKAGES`)
- [ ] T043 [US1] **Checkpoint D**; commit

## Phase 5: User Stories 1c + 2 — project system, conversion, references (P1) (sub-phase E)

- [ ] T044 [US2] Core records `src/NetPrints.Core/Projects/*.cs` (`ResolvedAssembly`, `ProjectMessage`, `ProjectSnapshot`, `ProjectReferenceInfo`, `ProjectEdit`, `BuildResult`, `ProcessStartRequest`, `IProjectSystem`, `IProcessRunner`, `ProcessResult`, `ProjectSystemException`) per project-system.md §4; `src/NetPrints.Core/Projects/ProcessRunner.cs` (System.Diagnostics.Process)
- [ ] T045 [US2] `src/NetPrints.Workspace/MsBuildRegistration.cs` (UnrealSharp logic, idempotent, `false` without SDK, log 4005) and `MsBuildMessageParser.cs` + PS-T10 in `tests/NetPrints.Core.Tests/Projects/MsBuildMessageParserTests.cs`
- [ ] T046 [US1] Profiles: `src/NetPrints.Core/Profiles/IProjectProfile.cs`, `ClassTemplate.cs`, `DefaultProjectProfile.cs` (extension-points.md §5, `ProjectTemplate` = project-system.md §1)
- [ ] T047 [US2] `src/NetPrints.Workspace/MsBuildProjectSystem.cs`: `LoadAsync` (restore check, evaluation, MSBuildWorkspace, docs paths; logs 4001–4003), `ApplyAsync` (`ProjectRootElement`, atomic save), `CreateAsync`, `BuildAsync` (log 4004), `GetRunCommand`; tests PS-T07, PS-T08, PS-T09, PS-T11 in `tests/NetPrints.Core.Tests/Projects/MsBuildProjectSystemTests.cs` (local test package built into a temp feed for PS-T08)
- [ ] T048 [US1] `src/NetPrints.Serialization/Legacy/ProjectConverter.cs` (project-system.md §5, log 3006) + `convert` command in `src/NetPrints.Generator/Program.cs`; PS-T12 in `tests/NetPrints.Core.Tests/Projects/ProjectConverterTests.cs`
- [ ] T049 [US1] Project model from snapshot (data-model.md §5): add `Project.FromSnapshot`, `Snapshot`, `GetGraphFilePath`, `CreateNewClass(profile)`, `LastDiagnostics` next to the old members (which stay until T056 so the solution keeps building while the editor and CLI move over)
- [ ] T050 [US1] `src/NetPrints.Serialization/ProjectPersistence.cs` (document-format.md §2.8) + DF-T11, DF-T15 in `…/Serialization/ProjectPersistenceTests.cs`
- [ ] T051 [US1] Convert `samples/HelloWorld` with the generator's `convert`, commit `HelloWorld.csproj` (with the conditional `PackageReference`), `HelloWorld.Program.netpc.json` and the built `HelloWorld.Program.netpc.g.cs`; delete the legacy sample files; replace `SampleProjectFactory` output with this layout; update sample paths in `tests/NetPrints.Editor.Tests/TestPaths.cs`, `tests/NetPrints.Editor.UITests/Hosting/TestDoubles.cs`, `tests/NetPrints.Testing.Ui/Scenarios/SmokeScenarios.cs`, `tests/NetPrints.Desktop.E2ETests/Scenarios/X11SmokeTests.cs`, README (FR-010); end-to-end DF-T01/SC-001 test: convert both fixtures, build, compare every `.g.cs` body with golden; add `*.netpc.g.cs linguist-generated=true eol=lf` to `.gitattributes`; test that every committed `samples/**/*.netpc.g.cs` equals `GraphCodeGenerator.RenderFile` of its graph (stale-file guard)
- [ ] T052 [US2] `src/NetPrints.Reflection`: `ReflectionProvider(IReadOnlyList<ResolvedAssembly>, IReadOnlyList<SourceFile> sources, IReadOnlySet<string> excludedAssemblyNames)`; `DocumentationUtil` from `DocumentationPath`; delete its `ProgramFilesX86` probe; RC-T09 in `tests/NetPrints.Editor.Tests/Reflection/DocumentationTests.cs`
- [ ] T053 [US2] Editor switch-over: `EditorContext` gains `Projects`, `Persistence`, `Converter`; `MsBuildRegistration` in Desktop `Main` (`EditorHostServices.MsBuildAvailable`, `NoSdkProjectSystem` for PS-T13); `ReflectionHost` from `Project.Snapshot` (logs 1010–1013; the catalog/`IExtensionHost` parameter is added in T069, until then the provider is the live one only; the mapper uses `NodeDocumentConverterRegistry.BuiltIn` until T069); `MainEditorVM` create/open (`*.csproj`, `*.netpp` → `ConfirmConversionAsync` → convert → open)/save/add-existing via persistence; Settings pane binary type via `ApplyAsync(SetOutputType)`, Output-flags chooser removed; `ReferenceListVM` on `DeclaredReferences` + `ProjectEdit`s; `FileFilter` updates; ED-T13 and PS-T13 in `tests/NetPrints.Editor.Tests/Main/MainEditorVMTests.cs` (update existing `.netpp` assertions), reference VM tests updated
- [ ] T054 [US2] Compile/Run via `IProjectSystem`: save all → `BuildAsync` → `DiagnosticMapper.FromBuild` → `Project` state; Run through `GetRunCommand` + `IProcessLauncher` (Output pane); update `ClassEditorVM`, `MainEditorVM` and their tests
- [ ] T055 [US2] CLI `src/NetPrints.Cli/Program.cs` per editor-services.md §4 (build/run `.csproj`, convert `.netpp`)
- [ ] T056 [US2] Delete the old model/persistence APIs now that nothing calls them: `Project.CreateNew/LoadFromPath/Save/SaveClassInProjectDirectory/AddExistingClass/CompileProject/RunProject/GetRunCommand/References/ClassPaths/CompilationOutput/LastCompileErrors`, `src/NetPrints.Core/Serialization/SerializationHelper.cs`, `Core/ReferenceAssemblyResolver.cs`, `Compilation/CodeCompiler.cs`, `ICodeCompiler.cs`, and Core's legacy reference classes (`AssemblyReference`, `FrameworkAssemblyReference`, `SourceDirectoryReference`, `CompilationReference`, `ICompilationReference`); the T004 golden test now reads fixtures only through the new importer. Headless test: `WriteLine` node tooltip contains the summary (D5, SC-003) in `tests/NetPrints.Editor.UITests/Graph/NodeTooltipTests.cs`; grep test: no `ProgramFilesX86` under `src/`
- [ ] T057 **Checkpoint E**: SC-001…SC-003; open the draft PR (`gh pr create --repo danielmeza/netprints --base master --draft`) for early review of A–E

---

## Phase 6: User Story 3 — extension points and loading (P1) (sub-phase F)

- [ ] T058 [US3] Translation seams (extension-points.md §2.1) in `src/NetPrints.Core/Translator/Extensibility/`; `ExecutionGraphTranslator` on `NodeTranslatorRegistry` (built-ins internal); `NPT006`; emitters (§3) and `ClassTranslator(TranslationEnvironment)`; update call sites (generator, editor); EX-T04, EX-T05 in `tests/NetPrints.Core.Tests/Translator/EmitterTests.cs`; golden tests unchanged
- [ ] T059 [P] [US3] Catalogs (§4) in `src/NetPrints.Reflection/Catalogs/`; EX-T06 in `tests/NetPrints.Editor.Tests/Reflection/CompositeReflectionProviderTests.cs`
- [ ] T060 [US3] `src/NetPrints.Extensibility/`: `ExtensionApi`, `INetPrintsExtension`, `IExtensionBuilder` (buffered; `AddProjectProperty`), `Nodes/*`, `BuiltInNodeLibrary` (PAR-53 suggestions moved from `src/NetPrints.Editor/Search/SuggestionItem.cs`), `BuiltInExtension`; EX-T12
- [ ] T061 [US3] `Loading/ExtensionManifest.cs`, `ExtensionLoadResult.cs`, `ExtensionLoaderOptions.cs` (`ExtensionFolders`), `ExtensionLoadContext.cs` (§8.2 incl. `Microsoft.Build*`), `ExtensionLoader.cs` (§8.1, logs 2001–2005), `ExtensionRegistry.cs`, `IExtensionHost`/`ExtensionHost` (load-context cache)
- [ ] T062 [P] [US3] Settings (§7): `ExtensionSettingsDescriptor`, `ISettingsStore`, `JsonFileSettingsStore` (log 2006), `NetPrintsSettings` (`ExtensionPaths`, `TrustedProjects`); EX-T09
- [ ] T063 [P] [US3] Host channel (§6) in `src/NetPrints.Extensibility/Hosting/`
- [ ] T064 [US3] Test asset `tests/NetPrints.TestExtension/` (manifest, `EnableDynamicLoading`, NetPrints refs `Private=false`/`ExcludeAssets=runtime`): node kind `netprints.test/Log` (own `JsonSerializerContext`; translator emits `System.Console.WriteLine(<in>)`), class emitter (`[System.Obsolete("test")]` + using), member emitter (`DeclarePartial` for properties of classes named `Partial*`), catalog with one type, profile `netprints.test`, settings section, host-channel factory `test`, project property `NetPrintsTestMode`; output `bin/<cfg>/extensions/netprints.test/`
- [ ] T065 [US3] Loader tests EX-T01, EX-T10, EX-T11 in `tests/NetPrints.Core.Tests/Extensibility/ExtensionLoaderTests.cs`
- [ ] T066 [US3] Contribution tests EX-T02, EX-T03, EX-T07, DF-T17 in `…/Extensibility/ContributionTests.cs`
- [ ] T067 [US3] Generator uses the request's `extension=` folders (project-system.md §3); PS-T14 in `tests/NetPrints.Core.Tests/Projects/GeneratorExtensionTests.cs`
- [ ] T068 [US3] Desktop/editor composition (editor-services.md §4): settings, `ExtensionHost`, host channel, complete `EditorHostServices`; `EditorContext.Extensions/HostChannel/Settings`; extension-failure dialog (ED-T11); trust flow on project open (`ConfirmTrustAsync`, `LoadForProject`, `NPD006`) — EX-T13 in `tests/NetPrints.Editor.Tests/Hosting/ProjectTrustTests.cs`
- [ ] T069 [US3] Editor use of contributions: search shows `NodeSuggestion`s by `AllowedIn`; `ReflectionHost` composes catalogs; persistence/mapper rebuilt on `RegistryChanged`; New Class uses the project's profile; `HostChannelBridge.cs` (types-changed → reload; focus-document → open/select; logs 1020–1022) + EX-T08; `ProjectSystemOptions.ExtraProperties` from the registry's `AddProjectProperty` names, test: the test extension reads `NetPrintsTestMode` via `ProjectSnapshot.GetProperty` (`tests/NetPrints.Editor.Tests/Hosting/ProjectPropertyTests.cs`, FR-025)
- [ ] T070 [US3] **Checkpoint F**: SC-004; commit

## Phase 7: User Story 4 — event graphs (P2) (sub-phase G)

- [ ] T071 [US4] Model `EventGraph`, `EventEntryNode`, `ClassGraph.EventGraphs` (data-model.md §4); `GraphKeys` `eventGraphs/<i>`
- [ ] T072 [US4] `ExecutionGraphTranslator.TranslateEventEntry`, `ClassTranslator` event methods; golden `EventGraphs.cs` (custom `OnStart`, `OnTick`, override of `public virtual void OnReset()` from a hand-written `EventBase.cs` in the fixture project) + `NPT001`/`NPT002` cases in `tests/NetPrints.Core.Tests/Translator/EventGraphTranslatorTests.cs`
- [ ] T073 [US4] `eventEntry` converter + `EventGraphDocument` mapping; round-trip test
- [ ] T074 [US4] Build/run test: fixture `.csproj` (LocalSdkLayout) whose `Main` calls `OnStart()`/`OnTick()`; `dotnet build` + run prints both lines
- [ ] T075 [US4] Editor event graph list, create/open/remove (undoable), search set, "Custom Event"/"Override <method>" suggestions; `AutomationIds.EventGraphList`, `CreateEventGraphButton`
- [ ] T076 [US4] ED-T07 in `tests/NetPrints.Editor.UITests/Events/EventGraphTests.cs` with `EventGraphsPage` in `tests/NetPrints.Testing.Ui`; **Checkpoint G**

## Phase 8: User Story 5 — method-local variables (P2) (sub-phase H)

- [ ] T077 [US5] Model `LocalVariable`, `VariableScope`, `ExecutionGraph.LocalVariables`, `IsLocalNameAvailable`, `VariableSpecifier.Scope`; `IsLocalVariable` on `Scope`
- [ ] T078 [US5] Translator: locals first, reserved names, getter/setter; `NPT004`; golden `Locals.cs`; build/run test (loop increments a local, prints it)
- [ ] T079 [US5] `locals` + `VariableRef.scope` mapping; round-trip test
- [ ] T080 [US5] Editor `VariablesPanelVM`, `LocalVariableVM`, `EditorCommands` (create/rename/retype/remove incl. nodes), "Method Variables" search category, drag to canvas; `AutomationIds.VariablesClassGroup`, `VariablesMethodGroup`, `CreateLocalVariableButton`
- [ ] T081 [US5] VM tests in `tests/NetPrints.Editor.Tests/Variables/LocalVariableTests.cs`; ED-T06 in `tests/NetPrints.Editor.UITests/Variables/LocalVariablePanelTests.cs`; **Checkpoint H**

## Phase 9: User Story 6 — C# code view and diagnostics (P2) (sub-phase I)

- [ ] T082 [US6] Source map (research R3): `NodeOffsets`, `SourceMap`, `TranslatedClass`, `ClassTranslator.Translate`; RC-T06 in `tests/NetPrints.Core.Tests/Translator/SourceMapTests.cs`
- [ ] T083 [US6] `src/NetPrints.Core/Compilation/CodeDiagnostic.cs`, `DiagnosticMapper.cs` (compilation-and-diagnostics.md §1); RC-T10
- [ ] T084 [US6] `CodeAnalysisSession` (references/sources/options from `ProjectSnapshot`); RC-T08
- [ ] T085 [US6] `src/NetPrints.Editor/Diagnostics/ICodeAnalysisHost.cs`, `CodeAnalysisHost.cs` (log 1030); `EditorContext.CodeAnalysis`; ED-T02 (virtual time)
- [ ] T086 [US6] Bundle Cascadia Mono (OFL + license) in `src/NetPrints.Editor/Assets/Fonts/`
- [ ] T087 [US6] `CodeView/CodeViewVM.cs`, `CodeView.axaml(.cs)`, `SquiggleRenderer.cs`, `RoslynFoldingStrategy.cs`; AvaloniaEdit style include; theme switch; plain-text fallback
- [ ] T088 [US6] Replace the TextBox in `src/NetPrints.Editor/Inspectors/ClassInspectorView.axaml` (`AutomationIds.ClassInspectorCodeView`); page objects
- [ ] T089 [US6] `ErrorListVM`, `DiagnosticRowVM`, `NavigateToNodeMessage`; bind the Errors tab; navigation + `NodeGraphVM.RevealNode`; ED-T03 (VM), ED-T04
- [ ] T090 [US6] Headless ED-T01, ED-T03, ED-T05 in `tests/NetPrints.Editor.UITests/CodeView/CodeViewTests.cs`; regenerate/review baselines; **Checkpoint I** (SC-006 recorded)

## Phase 10: User Story 7 — maintainability follow-ups (P3) (sub-phase J)

- [ ] T091 [US7] Remove the `customize` hook (`EditorComposition(EditorHostServices)` only); `tests/NetPrints.Editor.UITests/Hosting/HeadlessApp.cs` → explicit `TestComposition`; ED-T14
- [ ] T092 [US7] `ClassEditorServices`; `MemberVariableVM`/`NodeGraphVM` take it; `SelectInspectorMessage`; model-driven cleanup; ED-T08
- [ ] T093 [US7] Model wrapper setters → assign-only + model `PropertyChanged` forwarding
- [ ] T094 [US7] Nodify commands in `NodeGraphVM`, bound in `GraphEditorView.axaml`; remove handlers from `GraphEditorView.axaml.cs`; disable Alt+click disconnect; ED-T09
- [ ] T095 [US7] Architecture gate ED-T10 + fixture `Architecture/Fixtures/ViolatingVM.cs.txt` (fails on the fixture first); rule A3 includes Workspace/Generator/Sdk never referencing Avalonia and Generator never referencing `Microsoft.Build*`
- [ ] T096 [US7] Serialization log call sites (3001–3006) with a collecting logger in `tests/NetPrints.Core.Tests/Serialization/LoggingTests.cs`; **Checkpoint J** (SC-007, SC-008)

## Phase 11: Polish (sub-phase K)

- [ ] T097 [P] README: `.csproj` + `NetPrints.Sdk`, committed `.netpc.g.cs`, VS Code nesting pattern, SDK requirement, `NETPRINTS_EXTENSION_PATH`/`NETPRINTS_HOST_CHANNEL`/`NETPRINTS_LOG_LEVEL`, extension project requirements, `NetPrintsExtension` items (short; link contracts)
- [ ] T098 [P] SC-005 measurement (open HelloWorld restored: evaluation + workspace + extensions + types), recorded in the PR; 3× regression bound only
- [ ] T099 Manual IDE check (research K9): build and nesting of the converted HelloWorld in Visual Studio 2022, Visual Studio 2026 and Rider; record results in the PR
- [ ] T100 `dotnet format --verify-no-changes`, full suite, E2E once on a private Xvfb (quickstart §6)
- [ ] T101 Update checkboxes and research notes with findings; mark the PR ready; CI green; independent review per AGENTS.md

---

## Dependencies & Execution Order

- A (T001–T006) → B (T007–T019) → C (T020–T037) → D (T038–T043) → E (T044–T057) → F (T058–T070) → G (T071–T076), H (T077–T081) (independent of each other) → I (T082–T090) → J (T091–T096) → K (T097–T101).
- Hard edges: T004/T005 before T007; T015 before T030; T025 before T026–T029; T038 before T039–T042; T040 before T041, T051, T074; T044 before T047, T050; T046 before T047 and T048; T053–T055 before T056; T048 before T051; T049 before T053; T058 before T060, T067, T072; T061 before T068; T082 before T083, T089.

## Parallel examples

- B: T011 ∥ T012 ∥ T013 ∥ T014. C: T020 ∥ T021; T026 ∥ T027 ∥ T028 ∥ T029; T035 alongside T030–T034.
- E: T045 ∥ T047 once T044 exists. F: T059 ∥ T062 ∥ T063 once T060 exists.

## Implementation strategy

MVP = A–E: the roadmap "done when" (old sample → `.csproj` + JSON graphs, builds, identical C#), reviewed
early as a draft PR. F–K follow in the same PR.

## Test-ID map (revision 2026-09-25)

| Test ID | Status | Now in |
|---|---|---|
| DF-T01…T14, T16…T18 | kept | T004/T036/T051, T022, T023, T030–T035, T015, T066 |
| DF-T11 | changed (malformed graph among others; no project JSON) | T050 |
| DF-T15 | changed (`ProjectPersistence.SaveAsync` writes graphs + `.g.cs`, never the `.csproj`) | T050 |
| RC-T01…T05 | retired (custom resolver removed) → PS-T07, PS-T08, PS-T12 | T047, T048 |
| RC-T06, T08, T09, T10 | kept | T082, T084, T052, T083 |
| RC-T07 | retired → PS-T11 | T047 |
| PS-T01…T14 | new | T038–T042, T045, T047, T048, T053, T067 |
| EX-T01…T12 | kept (EX-T07 reworded for `CreateAsync`) | T058–T069 |
| EX-T13 | changed (trust flow) | T068 |
| ED-T01…T14 | kept (ED-T04, ED-T13 reworded for builds and conversion) | T018, T053, T068, T076, T081, T085, T089–T095 |
