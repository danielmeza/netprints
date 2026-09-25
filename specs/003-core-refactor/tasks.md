---
description: "Task list for P1 — Core refactor and extension points"
---

# Tasks: Core Refactor and Extension Points

**Input**: `/specs/003-core-refactor/` — plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Start condition**: rebase `003-core-refactor` onto `master` after the reorganization PR merges
(plan.md). All paths below are post-reorg.

**Tests**: REQUIRED (constitution V). Test-obligation ids (`DF-`, `EX-`, `RC-`, `ED-Txx`) refer to the
tables at the end of each contract; a task that names an id must implement exactly that case. Every
test uses xUnit v3 and passes `TestContext.Current.CancellationToken`; UI tests use page objects and
`AutomationIds`, no sleeps (research U17). Checkpoints require
`dotnet build NetPrints.slnx -c Release` (0 warnings) and `dotnet test --solution NetPrints.slnx -c Release` green.

**Rules for the implementer**: match comment density (AGENTS.md); rationale in commit messages; one
commit per task or small group; never change a golden file without regenerating it with
`NETPRINTS_UPDATE_SNAPSHOTS=1` and saying why in the commit.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelizable (different files, no dependency on an unfinished task)
- **[Story]**: US1…US7 (spec.md). Setup, Foundational and Polish tasks have no story label.

---

## Phase 1: Setup and characterization (sub-phase A) — on unmodified behavior

**Purpose**: freeze today's behavior before anything changes (research K1, K2).

- [ ] T001 Add to `Directory.Packages.props`: `Avalonia.AvaloniaEdit` 12.0.0, `AvaloniaEdit.TextMate` 12.0.0, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Logging`, `Microsoft.Extensions.Logging.Console` 10.0.12 (research §3). Do not remove Fody yet
- [ ] T002 [P] Create `tests/NetPrints.Core.Tests/Characterization/AllNodesFixtureFactory.cs`: builds, through the current Core API, project `AllNodes` with class `AllNodes.Everything` that uses every one of the 23 built-in node kinds at least once (document-format.md §1.5 minus `eventEntry`), including: method with 2 arguments, 1 generic argument and 2 return values; constructor; class variable with getter and setter and a generic type built in its type graph; a class interface pin; `MakeArrayNode` with predefined size and with 3 elements; data/exec/type reroutes; renamed editable pins; unconnected values of types bool, int, long, double (`NaN`), decimal, char, string with quotes and newline, and an enum. Writes legacy XML with `SerializationHelper`/`Project.Save` only when `NETPRINTS_REGENERATE_SAMPLES=1`
- [ ] T003 Generate and commit the legacy fixtures: `tests/NetPrints.Core.Tests/Fixtures/Legacy/AllNodes/*` (T002) and a copy of `samples/HelloWorld/*` into `tests/NetPrints.Core.Tests/Fixtures/Legacy/HelloWorld/`; link them as test content in `tests/NetPrints.Core.Tests/NetPrints.Core.Tests.csproj`
- [ ] T004 Add `tests/NetPrints.Core.Tests/Characterization/GoldenCSharpTests.cs`: for each class of both legacy fixtures, load with `Project.LoadFromPath`, translate with today's `ClassTranslator`, compare to `Fixtures/Golden/<Class.FullName>.cs` (write when `NETPRINTS_UPDATE_SNAPSHOTS=1`). Generate and commit the golden files (DF-T01 baseline)
- [ ] T005 [P] Add `tests/NetPrints.Core.Tests/Characterization/NotificationMapTests.cs`: for every public non-abstract type in `NetPrints.Core`/`NetPrints.Graph` implementing `INotifyPropertyChanged` (instances created via the AllNodes fixture), set each public settable property to a different valid value and record the raised property names (a setter that throws for that instance, e.g. `IsPure` when `CanSetPure` is false, is recorded as `"<throws>"`; enumeration order = ordinal type name, then property name); compare to `Characterization/NotificationMap.golden.json` (sorted). Generate the golden file on the Fody build and commit (research R6)
- [ ] T006 Run the suite; commit. **Checkpoint A**: golden and notification-map tests pass on the current code

---

## Phase 2: Foundational (sub-phase B) — blocks all stories

- [ ] T007 Create `src/NetPrints.Core/Core/ModelObject.cs` (data-model.md §1); add `CommunityToolkit.Mvvm` to `src/NetPrints.Core/NetPrints.Core.csproj`
- [ ] T008 Migrate `NetPrints.Graph.Node` and `NodePin` (+ all pin subclasses) to `ModelObject` with `[ObservableProperty]` partial properties per data-model.md §1; rename `OnInputTypeChanged` → `HandleInputTypeChanged` in `Node` and all overrides in `src/NetPrints.Core/Graph/*.cs`
- [ ] T009 Migrate `Variable` and `Project` (`src/NetPrints.Core/Core/Variable.cs`, `Project.cs`) to `ModelObject`; computed properties raise via `[NotifyPropertyChangedFor]`
- [ ] T010 Remove `Fody`/`PropertyChanged.Fody` from `src/NetPrints.Core/NetPrints.Core.csproj` and `Directory.Packages.props`; delete `src/NetPrints.Core/FodyWeavers.xml` and `.xsd`; remove `[DoNotNotify]` in `Graph/TypeNode.cs`. T005 and T004 must pass unchanged (FR-036)
- [ ] T011 [P] Enable nullable and warnings-as-errors in `src/NetPrints.Core` (`Core/`, `Graph/` folders): remove `Nullable=disable`; annotate; fix warnings without behavior change
- [ ] T012 [P] Same for `src/NetPrints.Core` `Translator/`, `Compilation/`, `Serialization/` folders
- [ ] T013 [P] Same for `src/NetPrints.Reflection` (incl. the 10 `RS1024` sites: use `SymbolEqualityComparer.Default`)
- [ ] T014 [P] Same for `tests/NetPrints.Core.Tests` and `src/NetPrints.Cli`; set `TreatWarningsAsErrors=true` as the default in `Directory.Build.props` and delete every per-project `Nullable=disable`/`TreatWarningsAsErrors` override (FR-037)
- [ ] T015 Add `Node.Id`, `NodeGraph.AllocateNodeId/FindNode/AssignLegacyNodeIds/PreservedDocumentState` and `GraphKeys` (data-model.md §2) in `src/NetPrints.Core/Graph/Node.cs`, `Core/NodeGraph.cs`, `Core/GraphKeys.cs`; `[OnDeserializing]` init; tests `tests/NetPrints.Core.Tests/Core/NodeIdTests.cs` (DF-T18 id rules, `GraphKeys` round trip for every graph of AllNodes)
- [ ] T016 Extract `GraphTypeInference.Relax(NodeGraph)` into `src/NetPrints.Core/Graph/GraphTypeInference.cs` from `MethodGraph.OnDeserialized` (which now calls it); golden tests unchanged
- [ ] T017 Create `src/NetPrints.Serialization/NetPrints.Serialization.csproj` and `src/NetPrints.Extensibility/NetPrints.Extensibility.csproj` (net10.0, nullable, warnings as errors, `CA1848`/`CA2254` as errors, logging abstractions), add both to `NetPrints.slnx`; `InternalsVisibleTo("NetPrints.Serialization")` in Core
- [ ] T018 Logging foundation: add `Microsoft.Extensions.Logging.Abstractions` to `src/NetPrints.Editor` and Logging + Console to `src/NetPrints.Desktop`; `CA1848`/`CA2254` as errors in Editor and Desktop. Introduce `EditorHostServices` with its first member `ILoggerFactory LoggerFactory` (the rest arrive in T066), `EditorApp.HostServices` (set by Desktop `Program` before start; `InvalidOperationException` if unset), `EditorComposition(EditorHostServices host, …)` (the P0 `customize` hook stays until T097), required `EditorContext.LoggerFactory`, `src/NetPrints.Desktop/AvaloniaLogSink.cs` replacing `.LogToTrace()`; `src/NetPrints.Editor/Hosting/Log.cs` with 1001/1002 used by `UnhandledExceptionHandler` (logger via ctor). Test ED-T12 in `tests/NetPrints.Editor.Tests/Hosting/UnhandledExceptionHandlerTests.cs` with a collecting logger; test compositions pass `NullLoggerFactory` explicitly
- [ ] T019 Commit. **Checkpoint B**: 0 warnings in Core/Reflection; A-gates unchanged; no Fody in the repo (SC-007 partial)

---

## Phase 3: User Story 1 — readable versioned documents (P1) 🎯 MVP (sub-phase C)

**Goal**: legacy loads, saves as JSON, identical C#. **Independent test**: DF-T01…T03 green.

- [ ] T020 [P] [US1] `src/NetPrints.Serialization/DocumentId.cs`, `DocumentIssue.cs`, `DocumentExceptions.cs` (document-format.md §2.1) + tests `tests/NetPrints.Core.Tests/Serialization/DocumentIdTests.cs`
- [ ] T021 [P] [US1] DTO records in `src/NetPrints.Serialization/Documents/` (`ProjectDocument.cs`, `ReferenceDocuments.cs`, `ClassDocument.cs`, `GraphDocuments.cs`, `NodeDocuments.cs` with all 24 kinds, `Refs.cs`) exactly per §1.3–§1.6/§2.4
- [ ] T022 [US1] `src/NetPrints.Serialization/Json/NetPrintsJsonContext.cs`, `NetPrintsJsonOptions.cs`, `NodeListConverter.cs` (§2.3) + test DF-T04 in `tests/NetPrints.Core.Tests/Serialization/CanonicalJsonTests.cs`
- [ ] T023 [US1] `src/NetPrints.Serialization/Mapping/TypedValueConverter.cs` + DF-T07 in `…/Serialization/TypedValueTests.cs`
- [ ] T024 [US1] `Mapping/NodeMappingContext.cs` (type/method/constructor/variable refs) + unit tests for each `ToRef`/`FromRef` pair in `…/Serialization/RefMappingTests.cs`
- [ ] T025 [US1] `Mapping/INodeDocumentConverter.cs`, `NodeDocumentConverterRegistry.cs` (§2.6, ctor validation) + registry tests
- [ ] T026 [P] [US1] Built-in converters, entry/return group: `methodEntry`, `constructorEntry`, `return`, `classReturn`, `typeReturn` in `Mapping/BuiltIn/EntryReturnConverters.cs`
- [ ] T027 [P] [US1] Built-in converters, member access group: `callMethod`, `constructor`, `makeDelegate`, `variableGetter`, `variableSetter` in `Mapping/BuiltIn/MemberConverters.cs`
- [ ] T028 [P] [US1] Built-in converters, values/types group: `literal`, `type`, `makeArrayType`, `makeArray`, `explicitCast`, `typeOf`, `default` in `Mapping/BuiltIn/ValueConverters.cs`
- [ ] T029 [P] [US1] Built-in converters, flow group: `ifElse`, `forLoop`, `ternary`, `await`, `throw`, `reroute` in `Mapping/BuiltIn/FlowConverters.cs`
- [ ] T030 [US1] `Mapping/DocumentMapper.cs` (class + project mapping, pins, edges, layout, preserved unknown nodes, issues) + DF-T06 and DF-T08 in `…/Serialization/MapperTests.cs`
- [ ] T031 [US1] `Migrations/IDocumentMigration.cs`, `DocumentMigrator.cs` + DF-T10 in `…/Serialization/MigrationTests.cs` (synthetic v1→v2 migration defined in the test)
- [ ] T032 [US1] `Json/JsonDocumentFormat.cs` + DF-T09 in `…/Serialization/JsonFormatTests.cs`
- [ ] T033 [US1] `Legacy/LegacyXmlDocumentFormat.cs` (§2.2, §3) + test: both legacy fixtures import to documents without issues
- [ ] T034 [US1] `DocumentFormatRegistry.cs` + DF-T16
- [ ] T035 [P] [US1] `Stores/IDocumentStore.cs`, `Stores/InMemoryDocumentStore.cs`, `Stores/FileSystemDocumentStore.cs` (§2.7) + shared abstract store tests DF-T12…T14 in `…/Serialization/Stores/` (virtual time via `Microsoft.Reactive.Testing`)
- [ ] T036 [US1] `ProjectPersistence.cs`, `ProjectFiles.cs` + DF-T11, DF-T15 in `…/Serialization/ProjectPersistenceTests.cs`
- [ ] T037 [US1] Switch `GoldenCSharpTests` (T004) to the new importer (DF-T01) and add DF-T02, DF-T03 and DF-T05 in `…/Serialization/RoundTripTests.cs` (AllNodes + HelloWorld fixtures)
- [ ] T038 [US1] Remove persistence from `src/NetPrints.Core/Core/Project.cs` (`LoadFromPath`, `Save`, `SaveClassInProjectDirectory`, `AddExistingClass`), delete `src/NetPrints.Core/Serialization/SerializationHelper.cs`; `GetClassStoragePath` → `.netpc.json`; switch Core tests and `SampleProjectFactory` to `ProjectPersistence`
- [ ] T039 [US1] Editor: `MainEditorVM` open/save/create/add-existing via `EditorContext.Persistence` (async); `FileFilter` both formats; issues dialog + log 1040; ED-T13 in `tests/NetPrints.Editor.Tests/Main/MainEditorVMTests.cs` (update the existing `.netpp` assertions)
- [ ] T040 [US1] Editor: `ClassEditorVM.Save` via persistence; add the required member `Persistence` to `EditorContext` (the other P1 members are added by T049, T066 and T086, each required); update `tests/NetPrints.Editor.Tests` and `tests/NetPrints.Editor.UITests` test compositions
- [ ] T041 [US1] CLI `src/NetPrints.Cli/Program.cs`: load via `ProjectPersistence.LoadFileAsync` (both formats), explicit wiring (editor-services.md §4 CLI row)
- [ ] T042 [US1] Convert `samples/HelloWorld` to JSON via `SampleProjectFactory` (`NETPRINTS_REGENERATE_SAMPLES=1`), delete the legacy sample files, update every sample path (`tests/NetPrints.Editor.Tests/TestPaths.cs`, `tests/NetPrints.Editor.UITests/Hosting/TestDoubles.cs`, `tests/NetPrints.Testing.Ui/Scenarios/SmokeScenarios.cs`, `tests/NetPrints.Desktop.E2ETests/Scenarios/X11SmokeTests.cs`, README) (FR-010)
- [ ] T043 [US1] **Checkpoint C**: SC-001, SC-002 green; commit

---

## Phase 4: User Story 2 — reference packs and documentation (P1) (sub-phase D)

**Goal**: compile/run/docs from packs. **Independent test**: RC-T01…T05, RC-T07, RC-T09.

- [ ] T044 [P] [US2] `src/NetPrints.Core/References/DotNetEnvironment.cs`, `DotNetHost.cs` (moved logic) and the records of references-and-compilation.md §2
- [ ] T045 [US2] `src/NetPrints.Core/References/ReferencePackResolver.cs` (probe table §2.1, §2.2) + RC-T01…T05 in `tests/NetPrints.Core.Tests/References/ReferencePackResolverTests.cs` (fake roots in temp dirs)
- [ ] T046 [US2] Project model additions (data-model.md §5: `TargetFramework`, `FrameworkReferences`, `ProfileId`, `ExtensionIds`, `ExtensionSettings`, `CreateNew(…, IProjectProfile)`), `src/NetPrints.Core/Profiles/IProjectProfile.cs`, `ClassTemplate.cs`, `DefaultProjectProfile.cs` (extension-points.md §5); mapper/DTO wiring for the new fields; delete `ReferenceAssemblyResolver.cs` and the `ProgramFilesX86` code in `FrameworkAssemblyReference.cs`; update every `Project.CreateNew` call site in `src/` and `tests/` to pass `DefaultProjectProfile.Instance` (the `addDefaultReferences` parameter disappears: `References` now starts empty because framework references come from the profile)
- [ ] T047 [US2] Compilation API (references-and-compilation.md §3): `SourceFile`, `CodeDiagnostic`, new `ICodeCompiler`/`CodeCompiler`, `ProjectCompiler.cs`; `Project.LastDiagnostics`; remove `Project.CompileProject`; runtimeconfig from `RuntimeFramework`; RC-T07 in `tests/NetPrints.Core.Tests/Compilation/ProjectCompilerTests.cs` (replaces the P0 sample compile test)
- [ ] T048 [US2] `src/NetPrints.Reflection`: `ReflectionProvider(IReadOnlyList<ResolvedAssembly>, …, IReadOnlySet<string>)` and `DocumentationUtil` from `DocumentationPath`; RC-T09 in `tests/NetPrints.Editor.Tests/Reflection/DocumentationTests.cs`
- [ ] T049 [US2] Editor: `ReflectionHost` uses `IReferenceResolver` (ctor per editor-services.md §2) and logs 1010–1013; `MainEditorVM`/`ClassEditorVM` compile/run via `ProjectCompiler`; error list temporarily shows `CodeDiagnostic.Message` (structured rows in US6); add `References`, `Compiler` to `EditorContext`
- [ ] T050 [US2] Settings source for pack roots: `NETPRINTS_REFERENCE_PACKS` + (after T061) `netprints.references.packRoots` in `EditorComposition` and CLI
- [ ] T051 [US2] Headless test: `WriteLine` node tooltip contains the summary (D5, SC-003) in `tests/NetPrints.Editor.UITests/Graph/NodeTooltipTests.cs`
- [ ] T052 [US2] **Checkpoint D**: SC-003; grep test RC-T05 (no `ProgramFilesX86` under `src/`); commit

---

## Phase 5: User Story 3 — extension points and loading (P1) (sub-phase E)

**Goal**: all seams + isolated loading. **Independent test**: EX-T01…T13.

- [ ] T053 [US3] Core translation seams (extension-points.md §2.1): `INodeTranslator`, `IExecutionTranslationContext`, `NodeTranslatorRegistry`, `TranslationEnvironment` in `src/NetPrints.Core/Translator/Extensibility/`; refactor `ExecutionGraphTranslator` to the registry (built-ins become internal translators); `NPT006`; golden tests unchanged
- [ ] T054 [US3] Emitters (§3) in `src/NetPrints.Core/Translator/Extensibility/Emitters.cs`; `ClassTranslator(TranslationEnvironment)` applies them; update all `new ClassTranslator()` call sites; EX-T04, EX-T05 in `tests/NetPrints.Core.Tests/Translator/EmitterTests.cs`
- [ ] T055 [P] [US3] Catalogs (§4) in `src/NetPrints.Reflection/Catalogs/`: `ITypeCatalog`, `CatalogInfo`, `CompositeReflectionProvider`, `InMemoryTypeCatalog`; live-provider exclusion; EX-T06 in `tests/NetPrints.Editor.Tests/Reflection/CompositeReflectionProviderTests.cs`
- [ ] T056 [US3] `src/NetPrints.Extensibility/`: `ExtensionApi`, `INetPrintsExtension`, `IExtensionBuilder` (buffered builder), `Nodes/INodeLibrary.cs`, `NodeKindDescriptor`, `NodeSuggestion`, `GraphKinds`
- [ ] T057 [US3] `BuiltInNodeLibrary` (24 kinds: built-in converters + `NodeTranslatorRegistry.BuiltIn` + PAR-53 suggestions moved from `src/NetPrints.Editor/Search/SuggestionItem.cs`) and `BuiltInExtension`; EX-T12
- [ ] T058 [US3] `Loading/ExtensionManifest.cs` (+ `ExtensionManifestException`), `ExtensionLoadResult.cs`, `ExtensionLoaderOptions.cs`
- [ ] T059 [US3] `Loading/ExtensionLoadContext.cs` (ALC rules §8.2) and `Loading/ExtensionLoader.cs` (discovery/order §8.1, logging 2001–2005)
- [ ] T060 [US3] `ExtensionRegistry.cs` (build, conflict rules, `Translation`, `NodeConverters`, `FindProfile`, `FindHostChannel`, disposal)
- [ ] T061 [P] [US3] Settings (§7): `Settings/ExtensionSettingsDescriptor.cs`, `ISettingsStore.cs`, `JsonFileSettingsStore.cs` (log 2006), `NetPrintsSettings.cs`, `ProjectExtensionSettings.cs`; EX-T09
- [ ] T062 [P] [US3] Host channel (§6): `Hosting/*.cs` (`IHostChannel`, `HostMessage`, `HostMessageTypes`, `IHostChannelFactory`, `HostLaunchContext`, `NullHostChannel`, `InMemoryHostChannel`)
- [ ] T063 [US3] Test asset `tests/NetPrints.TestExtension/` (in `NetPrints.slnx`, not packed): manifest, `EnableDynamicLoading`, NetPrints refs `Private=false`/`ExcludeAssets=runtime`; contributes node kind `netprints.test/Log` (DTO in its own `JsonSerializerContext`, translator emitting `System.Console.WriteLine(<in>)`), a class emitter (`[System.Obsolete("test")]` attribute + using), a member emitter (`DeclarePartial` on properties of classes named `Partial*`), an `InMemoryTypeCatalog` with one type, profile `netprints.test`, a settings section, host-channel factory `test`; output to `bin/<cfg>/extensions/netprints.test/`
- [ ] T064 [US3] Loader tests EX-T01, EX-T10, EX-T11, EX-T13 in `tests/NetPrints.Core.Tests/Extensibility/ExtensionLoaderTests.cs` (build variants of the manifest into temp dirs; the core test project references the test extension with `ReferenceOutputAssembly=false` to get it built)
- [ ] T065 [US3] End-to-end in-process tests EX-T02, EX-T03, EX-T07, DF-T17 in `tests/NetPrints.Core.Tests/Extensibility/ContributionTests.cs`
- [ ] T066 [US3] Desktop composition (editor-services.md §4): `src/NetPrints.Desktop/Program.cs` additionally creates settings, `ExtensionLoader`, host channel and completes `EditorHostServices` (`Extensions`, `Settings`, `HostChannel`, `Environment`); `EditorComposition` builds the remaining services from it; add `Extensions`, `HostChannel`, `Settings` to `EditorContext`; extension-failure dialog (ED-T11)
- [ ] T067 [US3] Editor use of contributions: node search shows extension `NodeSuggestion`s by `AllowedIn` (`src/NetPrints.Editor/Search/SuggestionListVM.cs`); `ReflectionHost` composes `registry.TypeCatalogs`; `ProjectCompiler`/`CodeAnalysisHost` use `registry.Translation`; persistence uses `registry.NodeConverters`; `MainEditorVM.CreateProject`/`NewClass` use the project's profile (`FindProfile(ProjectId)`, default template, first base type); host bridge `src/NetPrints.Editor/Hosting/HostChannelBridge.cs` (types-changed → reload; focus-document → open class/graph/node; logs 1020–1022); EX-T08 in `tests/NetPrints.Editor.Tests/Hosting/HostChannelBridgeTests.cs`
- [ ] T068 [US3] CLI: `ExtensionLoader` from settings/env; missing profile/extension issues printed
- [ ] T069 [US3] **Checkpoint E**: SC-004; commit

---

## Phase 6: User Story 4 — event graphs (P2) (sub-phase F)

- [ ] T070 [US4] Model: `EventGraph`, `EventEntryNode`, `ClassGraph.EventGraphs` (data-model.md §4) in `src/NetPrints.Core/Core/EventGraph.cs`, `src/NetPrints.Core/Graph/EventEntryNode.cs`; `GraphKeys` for `eventGraphs/<i>`
- [ ] T071 [US4] Translator: `ExecutionGraphTranslator.TranslateEventEntry` (reachable-set translation, `NPT001`, `NPT002`), `ClassTranslator` emits event methods after methods; tests `tests/NetPrints.Core.Tests/Translator/EventGraphTranslatorTests.cs`: golden `EventGraphs.cs` for a class with two custom events (`OnStart`, `OnTick`) and one override of `public virtual void OnReset()` declared by a base class `EventBase` in a source-directory reference of the fixture; plus the `NPT001` and `NPT002` error cases
- [ ] T072 [US4] Serialization: `eventEntry` converter, `EventGraphDocument` mapping; round-trip test in `…/Serialization/EventGraphRoundTripTests.cs`
- [ ] T073 [US4] Run test: a sample with `Main` calling `OnStart()` and `OnTick()` compiles and prints both lines (`tests/NetPrints.Core.Tests/Compilation/EventGraphRunTests.cs`)
- [ ] T074 [US4] Editor: `EventGraphVM` list in `src/NetPrints.Editor/ClassEditor/ClassEditorWindow.axaml` + `ClassEditorVM` (create/open/remove, undoable via `EditorCommands`); built-in search set for event graphs; "Custom Event" and "Override <method>" suggestions; `AutomationIds.EventGraphList`, `CreateEventGraphButton`
- [ ] T075 [US4] UI test ED-T07 in `tests/NetPrints.Editor.UITests/Events/EventGraphTests.cs` with a new `EventGraphsPage` in `tests/NetPrints.Testing.Ui`
- [ ] T076 [US4] **Checkpoint F**; commit

---

## Phase 7: User Story 5 — method-local variables (P2) (sub-phase G)

- [ ] T077 [US5] Model: `LocalVariable`, `VariableScope`, `ExecutionGraph.LocalVariables`, `IsLocalNameAvailable`, `VariableSpecifier.Scope`; `VariableNode.IsLocalVariable` keyed on `Scope` (data-model.md §3)
- [ ] T078 [US5] Translator: declare locals first, reserve names, getter/setter emission; `NPT004`; golden `Locals.cs` in `tests/NetPrints.Core.Tests/Translator/LocalVariableTranslatorTests.cs`; run test (loop increments a local, prints it)
- [ ] T079 [US5] Serialization: `locals` + `VariableRef.scope` mapping; round-trip test
- [ ] T080 [US5] Editor: `VariablesPanelVM`, `LocalVariableVM` (editor-services.md §3); `EditorCommands` for create/rename/retype/remove local (removal also removes its nodes); search category "Method Variables"; drag to canvas → Get/Set chooser; `AutomationIds.VariablesClassGroup`, `VariablesMethodGroup`, `CreateLocalVariableButton`
- [ ] T081 [US5] VM tests (undo/redo of each command) in `tests/NetPrints.Editor.Tests/Variables/LocalVariableTests.cs`; UI test ED-T06 in `tests/NetPrints.Editor.UITests/Variables/LocalVariablePanelTests.cs`
- [ ] T082 [US5] **Checkpoint G**; commit

---

## Phase 8: User Story 6 — C# code view and diagnostics (P2) (sub-phase H)

- [ ] T083 [US6] Source map (research R3): `ExecutionGraphTranslator.NodeOffsets`, `SourceMap`, `TranslatedClass`, `ClassTranslator.Translate` with annotations through `TranslatorUtil.FormatCode`; RC-T06 in `tests/NetPrints.Core.Tests/Translator/SourceMapTests.cs`
- [ ] T084 [US6] Diagnostic mapping in `ProjectCompiler` (per-class `SourceFile` paths → `SourceMap.Find`); `TranslationException` → `NPT` diagnostics; RC-T10
- [ ] T085 [US6] `src/NetPrints.Core/Compilation/CodeAnalysisSession.cs` (analysis + quick info); RC-T08 in `tests/NetPrints.Core.Tests/Compilation/CodeAnalysisSessionTests.cs`
- [ ] T086 [US6] `src/NetPrints.Editor/Diagnostics/ICodeAnalysisHost.cs`, `CodeAnalysisHost.cs` (debounce on `EditorContext.Scheduler`, log 1030); add `CodeAnalysis` to `EditorContext`; VM test ED-T02 (virtual time) in `tests/NetPrints.Editor.Tests/Diagnostics/CodeAnalysisHostTests.cs`
- [ ] T087 [US6] Bundle Cascadia Mono (OFL; license file next to it) in `src/NetPrints.Editor/Assets/Fonts/`; `CodeFontFamily` resource
- [ ] T088 [US6] `src/NetPrints.Editor/CodeView/CodeViewVM.cs`, `CodeView.axaml(.cs)`, `SquiggleRenderer.cs`, `RoslynFoldingStrategy.cs` (editor-services.md §3); AvaloniaEdit style include in `EditorApp.axaml`; theme-variant grammar switch; plain-text fallback if TextMate throws
- [ ] T089 [US6] Replace the TextBox in `src/NetPrints.Editor/Inspectors/ClassInspectorView.axaml` with `CodeView` (`AutomationIds.ClassInspectorCodeView`); update page objects in `tests/NetPrints.Testing.Ui`
- [ ] T090 [US6] `ErrorListVM`, `DiagnosticRowVM`, `NavigateToNodeMessage` (`src/NetPrints.Editor/Diagnostics/`); error tab in `ClassEditorWindow.axaml` bound to rows; `ClassEditorVM` navigation + `NodeGraphVM.RevealNode`; VM tests ED-T03 (VM level), ED-T04
- [ ] T091 [US6] Headless UI tests ED-T01, ED-T03, ED-T05 in `tests/NetPrints.Editor.UITests/CodeView/CodeViewTests.cs`; regenerate and review affected baselines (`class-inspector*`)
- [ ] T092 [US6] **Checkpoint H**: SC-006 measured and recorded in the test output; commit

---

## Phase 9: User Story 7 — maintainability follow-ups (P3) (sub-phase I)

- [ ] T093 [US7] `ClassEditorServices`; `MemberVariableVM` and `NodeGraphVM` take it instead of `ClassEditorVM`; `SelectInspectorMessage`; `ClassEditorVM` reacts to model `CollectionChanged` for cleanup; ED-T08 in `tests/NetPrints.Editor.Tests/ClassEditor/ModelDrivenCleanupTests.cs`
- [ ] T094 [US7] Replace model wrapper setters (`MethodVM`, `MemberVariableVM`, inspector pass-throughs) with assign-only + model `PropertyChanged` forwarding; existing VM tests stay green
- [ ] T095 [US7] Nodify commands in `NodeGraphVM` bound in `src/NetPrints.Editor/Graph/GraphEditorView.axaml`; delete the corresponding handlers in `GraphEditorView.axaml.cs`; disable Nodify's default Alt+click disconnect; ED-T09
- [ ] T096 [US7] Architecture gate ED-T10 in `tests/NetPrints.Editor.Tests/Architecture/ArchitectureGateTests.cs` + fixture `Architecture/Fixtures/ViolatingVM.cs.txt`; first commit shows the fixture failing the scanner (assert on its two violations)
- [ ] T097 [US7] Remove the `customize` hook from `EditorComposition` (ctor = `EditorHostServices` only) and move `tests/NetPrints.Editor.UITests/Hosting/HeadlessApp.cs` to an explicit `TestComposition` that builds `EditorContext` itself; no other test hooks in production code; ED-T14 (reflection test over `src/NetPrints.Editor` public constructors)
- [ ] T098 [US7] Log call sites in Serialization (3001–3005) with tests using a collecting logger in `tests/NetPrints.Core.Tests/Serialization/LoggingTests.cs`
- [ ] T099 [US7] **Checkpoint I**: SC-007, SC-008; commit

---

## Phase 10: Polish (sub-phase J)

- [ ] T100 [P] README: JSON formats, reference packs, `NETPRINTS_EXTENSION_PATH`/`NETPRINTS_REFERENCE_PACKS`/`NETPRINTS_HOST_CHANNEL`/`NETPRINTS_LOG_LEVEL`, extension project requirements (short; link contracts)
- [ ] T101 [P] SC-005 measurement: time `ProjectPersistence.LoadFileAsync` + extension loading + reflection reload for HelloWorld vs `master` (record in the PR, no assertion beyond a 3× regression bound, like P0 SC-005)
- [ ] T102 Run `dotnet format --verify-no-changes` (reorg CI rule), full suite, and the E2E suite once on a private Xvfb (quickstart §6)
- [ ] T103 Update `specs/003-core-refactor/tasks.md` checkboxes and research notes with implementation findings
- [ ] T104 PR "P1: Core refactor and extension points" against `danielmeza/netprints:master` (`gh pr create --repo danielmeza/netprints --base master`); CI green; independent review per AGENTS.md

---

## Dependencies & Execution Order

- A (T001–T006) → B (T007–T019) → C/US1 (T020–T043) → D/US2 (T044–T052) → E/US3 (T053–T069) → F/US4, G/US5 (independent of each other) → H/US6 → I/US7 → J.
- Hard edges: T004/T005 before T007 (goldens from the unmodified code); T015 before T030; T025 before T026–T029; T046 before T047; T053 before T057 and T071; T060 before T066; T061 before T050's settings part; T083 before T084/T090.
- US4 and US5 both need US1 (serialization) and T053 (translator registry); US6 needs US2 (references) and T053.

## Parallel examples

- B: T011, T012, T013, T014 (different folders/projects).
- C: T020 ∥ T021; T026 ∥ T027 ∥ T028 ∥ T029; T035 alongside T030–T034.
- E: T055 ∥ T061 ∥ T062 once T056 exists.

## Implementation strategy

MVP = A + B + C (the roadmap "done when": old sample loads, saves as JSON, identical C#). Each later
sub-phase is independently testable and ends with a checkpoint commit. If the owner chooses the
P1a/P1b split (plan.md), P1a = T001–T052 + T100–T104, P1b = T053–T099 + T100–T104.
