---
description: "Task list for P0 — Modernize build and migrate editor to Avalonia"
---

# Tasks: Modernize Build and Migrate Editor to Avalonia

**Input**: Design documents from `/specs/001-modernize-build/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: REQUIRED. FR-017/FR-018 require reflection, view-model, headless UI and sample
end-to-end tests. Write each test task before or together with the code it covers; every
checkpoint requires `dotnet test --solution NetPrints.sln` to be green.

**Governance gate**: satisfied — constitution 1.1.0 and 1.2.0 were applied by the owner.
**Scope update (constitution 1.2.0)**: every project targets `net10.0` only with the latest stable
dependencies; tasks below were adjusted accordingly (research.md "Scope update").

**Organization**: phases follow the dependency order build foundation → reflection library →
Avalonia shell → class editor → graph canvas → search/dialogs/drag & drop → UI tests → CI →
cleanup. Commit after each task or checkpoint; the solution must build on Linux at every
checkpoint.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1…US5 from spec.md; PAR-xx = Editor Parity Inventory item(s) covered
- Paths are relative to the repository root

---

## Phase 1: Setup (shared infrastructure)

**Purpose**: repository-wide build files; take the Windows-only projects out of the solution so the
build can be green on Linux from here on.

- [X] T001 Create `global.json`: `{"sdk":{"version":"10.0.100","rollForward":"latestFeature","allowPrerelease":false},"test":{"runner":"Microsoft.Testing.Platform"}}` (research e, p)
- [X] T002 [P] Create `Directory.Build.props` with `LangVersion=latest`, `Deterministic=true`, `ContinuousIntegrationBuild` when `$(CI)=='true'`, `Nullable=enable`, `TreatWarningsAsErrors=false`, `AvaloniaUseCompiledBindingsByDefault=true`, and `ManagePackageVersionsCentrally=false` when `$(MSBuildProjectName)=='NetPrintsVSIX'` (research d, f)
- [X] T003 [P] Create `Directory.Packages.props`: `ManagePackageVersionsCentrally` defaults to true (only if empty), `CentralPackageTransitivePinningEnabled=true`, and every `PackageVersion` from research "Scope update" (Roslyn 5.9.0, Fody 6.9.3, PropertyChanged.Fody 4.1.0, CommandLineParser 2.9.1, MSTest 4.4.1, Avalonia* 12.1.3, Nodify.Avalonia 2.0.0, CommunityToolkit.Mvvm 8.4.2, DynamicData 9.4.33, Xaml.Behaviors.Avalonia 12.0.7, Material.Icons.Avalonia 3.0.2)
- [X] T004 Remove the `NetPrintsEditor`, `NetPrintsEditorUnitTests` and `NetPrintsVSIX` projects (and their configuration entries) from `NetPrints.sln`. Keep the folders on disk for now: they are the porting reference (FR-020)
- [X] T005 [P] Add `NetPrintsVSIX/README.md` stating "Pending P4 rework: not in NetPrints.sln, does not build, not in CI; P4 adds a Windows workflow chained after CI" (FR-020)
- [X] T006 [P] Add `TestResults/` and `samples/**/Compiled_*/` to `.gitignore`

---

## Phase 2: Foundational (blocking prerequisites)

**Purpose**: Core builds on the .NET 10 SDK for both targets. Everything depends on this.

- [X] T007 Rewrite `NetPrints/NetPrints.csproj`: `TargetFramework=net10.0` (constitution 1.2.0), remove `LangVersion`, remove Gapotchenko.FX (no polyfill needed), versionless `PackageReference`s for Roslyn, `Fody` and `PropertyChanged.Fody` (both `PrivateAssets="all"`), set `Nullable=disable` (research a, b, h, Scope update)
- [X] T008 Build `NetPrints/NetPrints.csproj` on Linux. Expect 0 errors and only the known Fody `OnInputTypeChanged` and analyzer warnings. No source changes should be needed

**Checkpoint**: Core builds for `net10.0` on Linux.

---

## Phase 3: User Story 1 — Core toolchain on Linux (Priority: P1) 🎯 MVP

**Goal**: CLI and core tests run on Linux, the 11 tests pass, and compile/run works on Linux through the runtime-assembly fallback.

**Independent Test**: `dotnet test --project NetPrintsUnitTests` → 11+ passed; `dotnet run --project NetPrintsCLI -- -p samples/HelloWorld/HelloWorld.netpp -r` prints `Hello, World!`.

### Retarget

- [X] T009 [P] [US1] Rewrite `NetPrintsCLI/NetPrintsCLI.csproj` to target `net10.0` with a versionless CommandLineParser reference. In `NetPrintsCLI/Program.cs`, make `ProjectPath` `string?` for nullable. Keep the exit codes unchanged (FR-005, research g)
- [X] T010 [P] [US1] Rewrite `NetPrintsUnitTests/NetPrintsUnitTests.csproj`: `net10.0`, `OutputType=Exe`, `EnableMSTestRunner=true`, versionless `MSTest` reference, `Nullable=disable`. Remove `Microsoft.NET.Test.Sdk`, `MSTest.TestAdapter`, `MSTest.TestFramework` and the `Properties` folder item (FR-006)
- [X] T011 [US1] Change `Assert.AreEqual(typeA, genType1/2)` to `Assert.AreEqual<BaseType>(…)` in `NetPrintsUnitTests/TypeTests.cs` lines 29–30 (research c.4)
- [X] T012 [US1] Run `dotnet build NetPrints.sln` and `dotnet test --solution NetPrints.sln`. Expect 11/11 passed on Linux (checkpoint for SC-002, partial)

### Cross-platform reference fallback (FR-008..010)

- [X] T013 [US1] Write failing tests in `NetPrintsUnitTests/ReferenceAssemblyResolverTests.cs`: an existing path is kept; a missing `FrameworkAssemblyReference` expands once to the managed dlls of `RuntimeEnvironment.GetRuntimeDirectory()` (contains `System.Private.CoreLib.dll` and `mscorlib.dll`); a missing plain `AssemblyReference` is skipped with a warning; the `UsesRuntimeAssemblies` flag is set
- [X] T014 [US1] Implement `NetPrints/Core/ReferenceAssemblyResolver.cs` (`ResolveAssemblyPaths(IEnumerable<AssemblyReference>, ICollection<string> warnings)`, `UsesRuntimeAssemblies`). Filter out native dlls with `AssemblyName.GetAssemblyName` in try/catch (research c)
- [X] T015 [US1] In `NetPrints/Core/Project.cs` `CompileProject`: resolve assembly paths through the resolver, prepend its warnings to the compile errors, and write `{Name}.runtimeconfig.json` (framework `Microsoft.NETCore.App`, the running `major.minor.0`) next to the output when runtime assemblies were used (FR-008, FR-009)
- [X] T016 [US1] In `NetPrints/Core/Project.cs`: add `public (string FileName, string Arguments) GetRunCommand()`, which returns `dotnet "<exe>"` when the runtimeconfig exists and otherwise `(<exe>, "")`. `RunProject()` starts that command (FR-010). Leave the Windows `ProgramFilesX86` logic in `FrameworkAssemblyReference.cs` untouched (P1)
- [X] T017 [US1] Add a sample generator in `NetPrintsUnitTests/Samples/SampleProjectFactory.cs`. It builds `HelloWorld` through the Core API: project `HelloWorld` with the default references and an executable binary type; class `HelloWorld.Program` with a public static `Main` whose exec chain calls `System.Console.WriteLine(string)` with the unconnected value `"Hello, World!"`. It writes the files only when `NETPRINTS_REGENERATE_SAMPLES=1`. Use it once to create `samples/HelloWorld/HelloWorld.netpp` and `samples/HelloWorld/HelloWorld.Program.netpc`, then commit them (FR-018)
- [X] T018 [US1] Add `NetPrintsUnitTests/Samples/HelloWorldSampleTests.cs`, which copies `samples/HelloWorld` to a temporary directory, loads it, calls `CompileProject` and waits for `IsCompiling == false`, then asserts success. It runs `GetRunCommand()`, captures stdout and asserts `Hello, World!` (SC-008). Link the sample files as test content via `<None Include="../samples/**" CopyToOutputDirectory="PreserveNewest" LinkBase="samples"/>`
- [X] T019 [US1] Check by hand on Linux: `dotnet run --project NetPrintsCLI -- --version` and `-- -p samples/HelloWorld/HelloWorld.netpp -r` behave as described in quickstart §3

**Checkpoint**: MVP — core, CLI, tests and sample compile/run are green on Linux.

---

## Phase 4: User Story 5 — Headless-testable reflection library (Priority: P3, prerequisite for US2)

**Goal**: move reflection out of the WPF editor into the UI-free `NetPrints.Reflection` (net10.0).

**Independent Test**: `NetPrints.Editor.Tests` reflection tests pass without starting any UI session.

- [X] T020 [US5] Create `NetPrints.Reflection/NetPrints.Reflection.csproj`: `net10.0`, `Nullable=disable`, a reference to `NetPrints` and versionless Roslyn. Add it to `NetPrints.sln`
- [X] T021 [US5] `git mv` `NetPrintsEditor/Reflection/{IReflectionProvider,ReflectionProvider,MemoizedReflectionProvider,Memoization,ReflectionConverter,DocumentationUtil,DefaultOperatorSpecifiers}.cs` to `NetPrints.Reflection/`, and change the namespace `NetPrintsEditor.Reflection` → `NetPrints.Reflection` (FR-011)
- [X] T022 [US5] Add `[RefKind.RefReadOnlyParameter] = MethodParameterPassType.In` to `refKindToPassType` in `NetPrints.Reflection/ReflectionConverter.cs` (research a)
- [X] T023 [US5] Harden `NetPrints.Reflection/ReflectionProvider.cs` and `DocumentationUtil.cs`: skip assembly paths that do not exist, instead of letting `MetadataReference.CreateFromFile` throw, and treat a missing Windows documentation path as "no docs". The caller passes paths that the resolver has already expanded (FR-009)
- [X] T024 [US5] Create `NetPrints.Editor.Tests/NetPrints.Editor.Tests.csproj`: `net10.0`, `OutputType=Exe`, `EnableMSTestRunner=true`, versionless `MSTest`, `Avalonia.Headless`, `Avalonia.Skia`, and a reference to `NetPrints.Reflection` (the editor reference is added in T030). Add `[assembly: DoNotParallelize]` in `NetPrints.Editor.Tests/AssemblyInfo.cs` and add the project to `NetPrints.sln`
- [X] T025 [P] [US5] Write `NetPrints.Editor.Tests/Reflection/ReflectionProviderTests.cs` over the resolver-expanded runtime set. It asserts: more than 4,000 non-static types; static-method enumeration does not throw (the `ref readonly` case); `string` has more than 100 instance methods; `GetPublicMethodOverloads` works for `Console.WriteLine`; `GetConstructors(List<int>)` is not empty; the memoized provider returns equal results. Also assert via `typeof(ReflectionProvider).Assembly.GetReferencedAssemblies()` that there is no `Avalonia*`, `PresentationFramework` or `System.Windows*` reference (US5 AS-2)

**Checkpoint**: reflection is UI-free, tested, and in the solution.

---

## Phase 5: User Story 2 — Avalonia editor shell (Priority: P1)

**Goal**: new editor and desktop projects, app, theme and services; main window with project lifecycle, settings, references and class list (PAR-01..21 except class windows).

**Independent Test**: `MainEditorVMTests` and `ReferenceListVMTests` pass; the desktop app starts on Linux and opens the sample from the command line.

- [X] T026 [US2] Create `NetPrints.Editor/NetPrints.Editor.csproj` (`net10.0` library; Avalonia, Themes.Fluent, Fonts.Inter, Nodify.Avalonia, CommunityToolkit.Mvvm, DynamicData, Xaml.Behaviors.Avalonia, Material.Icons.Avalonia; references to NetPrints and NetPrints.Reflection; `AvaloniaResource Include="Assets/**"`). Add it to `NetPrints.sln`
- [X] T027 [P] [US2] Create `NetPrints.Desktop/NetPrints.Desktop.csproj` (`net10.0`, `WinExe`, Avalonia.Desktop, reference to NetPrints.Editor, `ApplicationIcon`) and `NetPrints.Desktop/Program.cs` (`[STAThread] Main` → `BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)`, `AppBuilder.Configure<EditorApp>().UsePlatformDetect().WithInterFont().LogToTrace()`). Add it to `NetPrints.sln`
- [X] T028 [P] [US2] Copy `NetPrintsEditor/Resources/*.png` (16 files) and `NetPrintsLogo.ico` to `NetPrints.Editor/Assets/`, and the icon to `NetPrints.Desktop/` (PAR-01, PAR-52 icons)
- [X] T029 [P] [US2] Create the service contracts and plain value types in `NetPrints.Editor/Services/` (`IFilePickerService.cs`, `IEditorDialogs.cs`, `IClipboardService.cs`, `IUiDispatcher.cs`, `IReflectionHost.cs`, `IWindowService.cs`, `IProcessLauncher.cs`, `FileFilter.cs`) and in `NetPrints.Editor/ViewModels/` (`GraphPoint.cs`, `NodeVisualKind.cs`, `PinKind.cs`), following contracts/editor-services.md
- [X] T030 [US2] Add the `NetPrints.Editor` project reference to the test project. Create the test fakes in `NetPrints.Editor.Tests/Fakes/` (`FakeFilePicker.cs`, `FakeDialogs.cs`, `FakeClipboard.cs`, `InlineDispatcher.cs`, `FakeWindowService.cs`, `FakeProcessLauncher.cs`)
- [X] T031 [US2] Implement `NetPrints.Editor/Services/ReflectionHost.cs`. It builds `MemoizedReflectionProvider(new ReflectionProvider(resolvedPaths, sourcePaths, generatedSources))` off the UI thread using `ReferenceAssemblyResolver`, publishes `NonStaticTypes` via `IUiDispatcher`, and raises `Reloaded` (PAR-15). Test it in `NetPrints.Editor.Tests/ViewModels/ReflectionHostTests.cs`
- [X] T032 [P] [US2] Implement the Avalonia services in `NetPrints.Editor/Services/Avalonia/`: `StorageFilePickerService.cs` (open, save and folder pickers via `TopLevel.StorageProvider`, `TryGetLocalPath`), `AvaloniaClipboardService.cs`, `AvaloniaUiDispatcher.cs`, `WindowService.cs` (a registry keyed by `ClassGraph` that activates, restores from minimized and closes windows), `ProcessLauncher.cs`
- [ ] T033 [US2] Create `NetPrints.Editor/EditorApp.axaml(.cs)`: `FluentTheme` with `RequestedThemeVariant=Dark` and emerald accent `#FF008A00`; Nodify `Themes/Controls.xaml` StyleInclude plus `Themes/Dark.xaml` ResourceInclude; `MaterialIconStyles`. Add a composition root (`EditorServices`) that creates the Avalonia services and `MainEditorVM`, reads the startup arguments and opens `MainWindow` (research m, n; PAR-05; FR-015, FR-016) Then add the headless harness `NetPrints.Editor.Tests/Ui/UiTest.cs`: `TestAppBuilder` (`AppBuilder.Configure<EditorApp>().UseSkia().UseHeadless(new(){UseHeadlessDrawing=false})`), a shared `HeadlessUnitTestSession` that is never disposed (disposal hangs on Avalonia 12.1.3, research "Scope update"), and `RunAsync(Action|Func<Task>)` that captures exceptions inside `Dispatch` and rethrows outside (research e gotcha)
- [X] T034 [US2] Write `NetPrints.Editor.Tests/ViewModels/MainEditorVMTests.cs` (initially failing) with the fakes. Cover: create project with cancel → previous project restored, and with a path → name taken from the file (PAR-03); open failure → error dialog + clipboard (PAR-04); startup argument opens the project (PAR-05); save prompts only when there is no path (PAR-06); Settings choosers disabled without a project (PAR-07); compile/run enablement and run via `IProcessLauncher` after success (PAR-09, PAR-10); unique `MyClass#` (PAR-12); existing class plus error path (PAR-13); remove class closes its window (PAR-11); close-all (PAR-14); reflection reload on open, references change and compile end (PAR-15)
- [X] T035 [US2] Port `NetPrints.Editor/ViewModels/MainEditorVM.cs`: `ObservableObject`, `[ObservableProperty] Project`, `partial void OnProjectChanged` (the former Fody hook), `[NotifyPropertyChangedFor]` for `IsProjectOpen`/`CanCompile`/`CanCompileAndRun`, and `[RelayCommand]`s for PAR-02..14 through the services. Make T034 pass
- [X] T036 [US2] Create `NetPrints.Editor/Views/MainWindow.axaml(.cs)`: title bound to the project name, icon, 800×600; five round buttons (Project, References, Settings, Compile, Run) with tooltips shown even when disabled; a `SplitView` for the Project and Settings panes (mutually exclusive); a Classes list with open/remove buttons (Material `Minus` icon); an indeterminate progress overlay. On close, call `CloseAllClassEditors` (PAR-01, 02, 07, 08, 09, 10, 11, 12, 13, 14)
- [X] T037 [US2] Write `NetPrints.Editor.Tests/ViewModels/ReferenceListVMTests.cs` (initially failing): add assembly and duplicate (case-insensitive full path) → one entry; add folder and duplicate; error dialog on invalid input; Include/Exclude only for source directories; remove (PAR-16..20)
- [X] T038 [US2] Port `NetPrints.Editor/ViewModels/ReferenceListVM.cs` and `CompilationReferenceVM.cs`, and create `NetPrints.Editor/Views/ReferencesDialog.axaml(.cs)`: list with text and tooltip, `ToggleSwitch` Include/Exclude, remove button, Assembly and Source-code add buttons, Close (PAR-16..21). Make T037 pass
- [X] T039 [US2] Implement `NetPrints.Editor/Services/Avalonia/EditorDialogs.cs` plus `Views/Dialogs/ErrorDialog.axaml(.cs)` (message, copy-friendly text). Wire `ShowProgress` to the MainWindow overlay (PAR-04)

**Checkpoint**: the app starts on Linux (`dotnet run --project NetPrints.Desktop -- samples/HelloWorld/HelloWorld.netpp`) and the main-window VM tests pass.

---

## Phase 6: User Story 2 — Class editor window (Priority: P1)

**Goal**: class window with lists, inspectors, undo/redo, generated code, compile output (PAR-22..37, 60).

**Independent Test**: `ClassEditorVMTests`, `MemberVariableVMTests` and `UndoRedoStackTests` pass.

- [X] T040 [P] [US2] Write `NetPrints.Editor.Tests/ViewModels/UndoRedoStackTests.cs` (initially failing) for the PAR-60 pairs: add/remove variable, add/remove getter and setter, overload change restores the previous overload, remove method has a no-op undo, a new action clears redo
- [X] T041 [US2] Implement `NetPrints.Editor/Commands/UndoRedoStack.cs` (one instance per class editor) and `NetPrints.Editor/Commands/EditorCommands.cs` (`IUndoableCommand` objects replacing `NetPrintsCommands`/`EditorCommands` `RoutedUICommand`s and the `MakeUndoCommand` table). Make T040 pass
- [X] T042 [P] [US2] Port `NetPrints.Editor/Messages/{OpenGraphMessage,NodeSelectionMessage,AddNodeMessage}.cs` unchanged, except for the namespace
- [X] T043 [US2] Write `NetPrints.Editor.Tests/ViewModels/MemberVariableVMTests.cs` and `ClassEditorVMTests.cs` (initially failing). Cover: getter and setter creation with node positions and a connected type pin (PAR-29); unique `Variable#` with type `object`, undoable (PAR-30); `Method#` with entry and return connected and the graph opened (PAR-25); override creates and opens (PAR-26); constructor (PAR-28); removing a method or constructor clears the inspector and graph (PAR-24, 27); inspector switching (PAR-33); generated code refreshes within 2 s (PAR-34); Delete keeps entry, class-return and main-return nodes (PAR-37); Save, Compile and Run commands (PAR-23, 32)
- [X] T044 [US2] Port `NetPrints.Editor/ViewModels/MemberVariableVM.cs`, replacing `MessengerInstance` with an injected `IMessenger` and routing add/remove through the undo stack
- [X] T045 [US2] Port `NetPrints.Editor/ViewModels/ClassEditorVM.cs`: `Class` property with `partial OnClassChanged` (the former Fody hook), `Methods`/`Constructors`/`Variables` collections (port `ObservableViewModelCollection.cs` to `NetPrints.Editor/ViewModels/`), `OverridableMethods`, `OpenedGraph`, an `Inspector` enum, and `GeneratedCode` via an async 1 s debounce loop plus `IUiDispatcher` instead of `System.Timers.Timer` + `Dispatcher`. Commands for PAR-23..30, 33, 37; `IRecipient<OpenGraphMessage>`. Make T043 pass
- [X] T046 [P] [US2] Port the flag converters into `NetPrints.Editor/Converters/ModifierFlagConverters.cs` (class, method and variable modifier ↔ checkbox, fixing the stateful `ConvertBack` so it XORs against the bound value) and `NetPrints.Editor/Converters/MethodSpecifierConverter.cs`
- [X] T047 [P] [US2] Create `NetPrints.Editor/Views/Inspectors/ClassInspectorView.axaml`: Name and Namespace (update as typed), Visibility, Sealed/Abstract/Static/Partial, and a read-only monospace generated-code box with scrollbars (PAR-34)
- [X] T048 [P] [US2] Create `NetPrints.Editor/Views/Inspectors/MethodInspectorView.axaml`: Name read-only for constructors, Visibility, and six modifier checkboxes hidden for constructors (PAR-35)
- [X] T049 [P] [US2] Create `NetPrints.Editor/Views/Inspectors/VariableInspectorView.axaml`: Name, Visibility, ReadOnly/Const/Static/New (PAR-36)
- [X] T050 [P] [US2] Create `NetPrints.Editor/Views/MemberVariableView.axaml(.cs)`: remove, name (click shows the inspector; drag source), Add getter/setter or Get/Set rows with remove, double click opens the graph, "Open type graph" (PAR-29, PAR-57 drag source)
- [X] T051 [US2] Create `NetPrints.Editor/Views/ClassEditorWindow.axaml(.cs)`: title = class name, icon, `WindowState=Maximized`; toolbar with Compile, Run, Class and Save and tooltips; left column with Methods, Constructors and Variables lists (single click → inspector, double click → open graph, remove buttons), Create buttons and the "Override a method" chooser that resets after use; `GridSplitter`s; center graph host, compile-error list and status line; right `ContentControl` inspector selected by `Inspector` (PAR-22..33)
- [X] T052 [US2] In `ClassEditorWindow.axaml`, add `KeyBinding`s for Delete → `DeleteSelectedNodesCommand`, Ctrl+Z → Undo and Ctrl+Y → Redo (PAR-37)

**Checkpoint**: a class window opens from the main window; lists, inspectors, undo/redo and compile output work.

---

## Phase 7: User Story 2 — Graph canvas (Priority: P1)

**Goal**: node graph editing on Nodify: nodes, pins, cables, selection, dragging, pan and zoom (PAR-38..51).

**Independent Test**: `NodePinVMTests`, `NodeVMTests` and `NodeGraphVMTests` pass; a headless render of the sample `Main` graph realizes all nodes.

- [X] T053 [US2] Write `NetPrints.Editor.Tests/ViewModels/NodePinVMTests.cs` and `NodeVMTests.cs` (initially failing). Cover: `PinKind`/shape flags; dimmed fill when unconnected; the default-value indicator; unconnected editors (text, enum names, bool); `ClearUnconnectedValue`; `IsNameEditable`; `ConnectTo` rejects incompatible pins and accepts subclass or implicit-cast ones; `DisconnectAll`; `AddRerouteNode` midway; `ToggleFaint` (PAR-43..48); `NodeVisualKind` for all 13 kinds; overload lists and undoable change; Pure; left/right +/- for array, entry, return and class-return nodes with tooltips (PAR-39..42)
- [X] T054 [US2] Port `NetPrints.Editor/ViewModels/NodePinVM.cs`: `GraphPoint` instead of WPF `Point`, `PinKind` instead of `Brush`, `partial OnPinChanged` subscription (the former Fody hook), and `IReflectionHost` for `CanConnectNodePins`. Add `Anchor` (set by the view) and `ToggleFaint`. Make the NodePinVM tests pass
- [X] T055 [US2] Port `NetPrints.Editor/ViewModels/NodeVM.cs`: `NodeVisualKind` instead of 15 brushes, `Location` as a `GraphPoint` synced with `Node.PositionX/Y`, `IsSelected`/`ZIndex` notifications, overloads through `IReflectionHost`, `ChangeOverload` through the undo stack, pin +/- operations. Make the NodeVM tests pass
- [X] T056 [US2] Write `NetPrints.Editor.Tests/ViewModels/NodeGraphVMTests.cs` (initially failing). Cover: node and connection collections track model changes (add or remove node, connect or disconnect pins, reroute); box and click selection via messages; drag of multiple selected nodes applies the zoom scale and snaps to 28 px on release; `AddNode` clamps to non-negative positions and auto-connects to `SuggestionPin` (PAR-46, 47, 49, 50)
- [X] T057 [US2] Create `NetPrints.Editor/ViewModels/ConnectionVM.cs` and port `NetPrints.Editor/ViewModels/NodeGraphVM.cs` (connection tracking from `NodeInputDataPin.IncomingPin`, `NodeOutputExecPin.OutgoingPin` and `NodeInputTypePin.IncomingPin`; selection; drag handling; `AddNode`; `IMessenger` recipients). Make T056 pass
- [X] T058 [P] [US2] Create `NetPrints.Editor/Views/Graph/GraphResources.axaml`: brushes keyed by `NodeVisualKind` and `PinKind` with the WPF ARGB values (header colors from `NodeVM`, pins E0FFE0/E0E0FF/FFE0E0, 60 % when unconnected, default indicator 10EEFF at FF/7F alpha, selected border 009900). Add `Converters/GraphConverters.cs` (`GraphPoint`↔`Avalonia.Point`, kind→brush) and style classes (`selected`, `faint`, `unconnected`, `:pointerover` outline replacing the 3 WPF triggers) (PAR-39, 43, 48; research n)
- [X] T059 [US2] Create `NetPrints.Editor/Views/Graph/GraphEditorView.axaml(.cs)`: `NodifyEditor` bound to `Nodes`/`Connections`/`SelectedItems`, `GridCellSize=28` with a 28-px grid background, `MinViewportZoom=0.3`, `MaxViewportZoom=1.0`, zoom step 1.3 around the pointer, right-drag pan with a move cursor (right-click without drag opens search in T072), `ItemsDragCompletedCommand` snaps to the grid, viewport reset when the graph changes, graph-name watermark, and the `ItemContainer` `Location` binding style (PAR-38, 49, 50, 51)
- [X] T060 [US2] Create `NetPrints.Editor/Views/Graph/NodeView.axaml(.cs)`: header color by kind, shadow, label, documentation tooltip, overload `ComboBox` (16 px) that goes through the undo stack, Pure `CheckBox`, left and right `+`/`-` buttons (Material icons, tooltips), six pin lists (inputs left, outputs right), and a compact reroute template (PAR-39..42)
- [X] T061 [US2] Create `NetPrints.Editor/Views/Graph/PinView.axaml(.cs)`: a Nodify `NodeInput`/`NodeOutput` with a custom connector template (square, circle or triangle, 14 px, hover outline); default-value indicator; unconnected editors (TextBox with watermark, enum ComboBox, CheckBox) where middle-click clears; editable name TextBox or a label; alignment by direction; push the connector `Anchor` to `NodePinVM.Anchor`. Middle-click on the pin → `DisconnectAll`, XButton1 → `ToggleFaint` (PAR-43, 44, 45, 48)
- [X] T062 [US2] Create the connection template in `GraphEditorView.axaml`: Nodify `Connection` bezier from source to target anchors, colored by `PinKind`, full opacity on hover (0.7 otherwise, 0.1 when faint), thickness 4 (2 when faint). Middle-click → `Disconnect`, double click → `InsertReroute`, XButton1 → `ToggleFaint`. Implement the gestures with Xaml.Behaviors.Avalonia `EventTriggerBehavior` or view code-behind (PAR-48)
- [X] T063 [US2] Wire pin linking in `GraphEditorView.axaml(.cs)`: `PendingConnectionTemplate` preview; `ConnectionCompletedCommand` → `NodePinVM.ConnectTo` when compatible (`GraphUtil.CanConnectNodePins` with subclass and implicit-cast rules). A completion with no target opens node search at the drop point with `SuggestionPin` set (PAR-46, 47)
- [ ] T064 [US2] Add the headless render test `NetPrints.Editor.Tests/Ui/GraphRenderTests.cs`. Open the sample `Main` graph in a `GraphEditorView` inside a headless `Window`. Assert that every `NodeVM` has a realized `ItemContainer` at its `Location`, that connections are realized, and that `CaptureRenderedFrame()` is not null (PAR-38..45 smoke)

**Checkpoint**: graphs can be viewed and edited with the pointer; VM and render tests pass.

---

## Phase 8: User Story 2 — Node search, dialogs, drag & drop (Priority: P1)

**Goal**: suggestion popup (DynamicData), Select Type/Method dialogs, Get/Set chooser, list-to-canvas drag & drop (PAR-52..59).

**Independent Test**: `SuggestionListVMTests` pass, including the SC-005 timing test; headless search popup test passes.

- [X] T065 [US2] Write `NetPrints.Editor.Tests/ViewModels/SuggestionListVMTests.cs` (initially failing). Cover: categories per pin kind and per graph kind; the built-in lists for method (14), constructor (12) and class (2) graphs (PAR-53); multi-term case-insensitive filtering (PAR-52); Construct → `SelectTypeAsync` then the first constructor; Literal and Type → `SelectTypeAsync`; Make Delegate → `SelectMethodAsync`; variable → Get/Set chooser (PAR-54); **performance**: with the full runtime set, building suggestions without a pin takes < 2 s and a keystroke filter step takes < 300 ms (SC-005; mark `[TestCategory("Performance")]`, log the measured timings, and allow a 3× budget when `CI=true` to avoid flaky runs on shared runners)
- [X] T066 [P] [US2] Create `NetPrints.Editor/ViewModels/SuggestionItem.cs` (category, value, display text and icon key; port of `SuggestionListConverter` logic including the operator names via `OperatorUtil`)
- [X] T067 [US2] Port `NetPrints.Editor/ViewModels/SuggestionListVM.cs`: a DynamicData `SourceList<SuggestionItem>` with `Filter` driven by `SearchText` (split on spaces, throttled about 100 ms, switchable to immediate for tests) → `ReadOnlyObservableCollection`; `Select(item)` → `AddNodeMessage` or dialogs via `IEditorDialogs`; `HideRequested` event. Move `NodeGraphVM.UpdateSuggestions` (with categories as in WPF) onto `IReflectionHost` (PAR-52..54). Make T065 pass
- [X] T068 [US2] Create `NetPrints.Editor/Views/Graph/NodeSearchPopup.axaml(.cs)`: a 700×300 `Popup`/`Flyout` at the pointer with a search `TextBox` that is cleared and focused on open, and a virtualized `ListBox` with non-selectable category header rows and 16-px icons from `avares://NetPrints.Editor/Assets/`. Clicking an item or pressing Enter selects it. Open it on right-click without drag in `GraphEditorView` and on pending connections without a target (PAR-52, 47)
- [X] T069 [P] [US2] Create `NetPrints.Editor/Views/Dialogs/SelectTypeDialog.axaml(.cs)`: an `AutoCompleteBox` or editable chooser over `IReflectionHost.NonStaticTypes`, defaulting to `object`, and a Select button; returns `TypeSpecifier?` (PAR-58)
- [X] T070 [P] [US2] Create `NetPrints.Editor/Views/Dialogs/SelectMethodDialog.axaml(.cs)`: a method chooser (first item preselected) and a Select button; returns `MethodSpecifier?` (PAR-59)
- [X] T071 [US2] Create `NetPrints.Editor/ViewModels/GetSetChooserVM.cs` and `NetPrints.Editor/Views/Graph/GetSetChooser.axaml(.cs)`: a popup at the pointer or drop point with Get and Set buttons enabled via `NetPrintsUtil.IsVisible`, closing on pointer exit, that creates `VariableGetterNode`/`VariableSetterNode`. Add a VM test in `NetPrints.Editor.Tests/ViewModels/GetSetChooserVMTests.cs` (PAR-55)
- [X] T072 [US2] Implement list-to-canvas drag & drop. Drag sources are the method, constructor and variable rows (`DragDrop.DoDragDrop` with custom formats `netprints/graph` and `netprints/variable`). The `GraphEditorView` drop target converts the drop point to canvas coordinates and calls `NodeGraphVM.DropMethod`/`DropConstructor` (call or constructor node) or `DropVariable` (Get/Set chooser). Add VM tests for the three drop operations in `NodeGraphVMTests.cs` (PAR-56, 57)

**Checkpoint**: every PAR item is implemented.

---

## Phase 9: User Story 2 — Headless UI smoke tests and parity sign-off (Priority: P1)

**Goal**: FR-017 end-to-end UI tests on Linux; manual parity walkthrough.

**Independent Test**: `dotnet test --project NetPrints.Editor.Tests` with every `Ui` test green and no display.

- [ ] T073 [US2] Add `NetPrints.Editor.Tests/Ui/AppStartupTests.cs`. Start `EditorApp` headless, show `MainWindow`, assert a rendered frame and that the startup time is under 5 s (SC-006). Open `samples/HelloWorld/HelloWorld.netpp` via the startup-argument path and assert that the class list shows `HelloWorld.Program` (PAR-05, PAR-11)
- [ ] T074 [US2] Add `NetPrints.Editor.Tests/Ui/EditFlowTests.cs`: open the sample; open the class window via `WindowService`; open `Main`; create an `If Else` node through `SuggestionListVM` at a point, and assert that a new `ItemContainer` is realized; connect the entry exec output to the If input via `NodePinVM.ConnectTo`, and assert that a connection is realized; save to a temporary copy, reload, and assert that the node and connection are present (FR-017 flow; PAR-46, 52, 53, 06)
- [ ] T075 [US2] Add `NetPrints.Editor.Tests/Ui/SearchPopupTests.cs`: open the popup on the `Main` graph; type "write line"; assert the visible items contain `Console WriteLine`; select → node created; Literal → the fake `SelectTypeAsync` is called (PAR-52, 54, 58)
- [ ] T076 [US2] Run the quickstart §5 parity walkthrough on Linux (and Windows if available). Record PAR-01..60 pass/fail with the verification method in the PR description (SC-004). File follow-ups for anything deferred

**Checkpoint**: US2 done — editor at parity, verified.

---

## Phase 10: User Story 3 — CI on every change (Priority: P2)

**Goal**: the Linux-only `CI` workflow gates merges (FR-019; contracts/ci-workflow.md).

**Independent Test**: push the branch → `CI` is green; a deliberately failing test turns it red (then revert).

- [ ] T077 [US3] Create `.github/workflows/ci.yml` exactly per contracts/ci-workflow.md: `name: CI`; triggers push/PR to `master` + `workflow_dispatch`; `permissions: contents: read`; concurrency; job `build-test` on `ubuntu-latest`; checkout@v7; setup-dotnet@v6 `10.0.x`; restore, build and test with `--report-trx --results-directory TestResults`; the CLI `--version` smoke tolerating exit code 1; upload-artifact@v7 `test-results` `if: always()`. No Windows jobs and no VSIX workflow
- [ ] T078 [P] [US3] Delete `.travis.yml` and replace the Travis badges in `README.md` with the `CI` workflow badge (FR-021)
- [ ] T079 [US3] Push the branch and confirm `CI` is green with every test passing and the TRX artifact uploaded. Record the run URL in the PR (SC-002, SC-003)

---

## Phase 11: User Story 4 — One place for versions; dead code removed (Priority: P3)

**Goal**: remove the WPF editor and verify dependency hygiene (FR-003, FR-006, FR-012, SC-007).

**Independent Test**: the grep checks in T081 return nothing; the solution still builds and tests green.

- [ ] T080 [US4] `git rm -r NetPrintsEditor/ NetPrintsEditorUnitTests/` (after T076 sign-off). Make sure nothing in `NetPrints.sln` or in the remaining projects references them
- [ ] T081 [US4] Run the hygiene checks and fix any hit. Search SDK-style `*.csproj` for `Version=` on `PackageReference` → none. Search for `Gapotchenko|System.Management|MahApps|MvvmLight|UseWPF|System.Windows.Forms|PresentationFramework` outside `NetPrintsVSIX/` and `specs/` → none. `Fody` should appear only in `NetPrints/NetPrints.csproj`, `NetPrints/FodyWeavers.*` and `Directory.Packages.props` (SC-007)
- [ ] T082 [US4] Update the `README.md` sections "Target Frameworks" (new table: every project net10.0), "Download/Build" (quickstart commands), "Standalone Editor Guide" (Avalonia, Linux/macOS/Windows, runtime-assembly fallback note) and "Visual Studio Extension" (pending P4) (FR-021)

---

## Phase 12: Polish & cross-cutting concerns

- [ ] T083 [P] Check deterministic output: clean-build `NetPrints/NetPrints.csproj -c Release` twice and compare the `sha256sum` of the outputs (SC-009)
- [ ] T084 Run quickstart.md §2–4 from a fresh clone in a clean Linux container (only the .NET 10 SDK installed). Confirm the two-command build/test (SC-001)
- [ ] T085 In the PR description, list the follow-ups: P1 (ref-pack resolution and target selection, Fody removal, nullable in Core/Reflection, RS1024, MSTEST0017, `LanguageVersion.Preview` constant), P2 (CLI exit codes), P4 (VSIX workflow chained after `CI`; VS hosting is out of scope per constitution 1.2.0), and the net10.0-only scope update

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (1)** → **Foundational (2)** → **US1 (3)** → **US5 (4)** → **US2 shell (5)** → **US2 class editor (6)** → **US2 canvas (7)** → **US2 search/dialogs/DnD (8)** → **US2 UI tests & sign-off (9)** → **US4 cleanup (11)** → **Polish (12)**.
- **US3 (10)** can start any time after US1 (Phase 3). The workflow is useful early; T079's final green run must follow Phase 9.
- US5 has priority P3 but runs before US2, because the editor consumes `NetPrints.Reflection` (FR-011).

### User-story dependencies

- **US1 (P1)**: depends only on Foundational. It is the MVP.
- **US5 (P3)**: depends on US1 (the resolver and test infrastructure).
- **US2 (P1)**: depends on US1 (resolver, sample) and US5 (reflection library).
- **US3 (P2)**: depends on US1; it is final once US2's tests exist.
- **US4 (P3)**: depends on US2 sign-off (T076) before the WPF sources are deleted.

### Within phases

- Tests marked "initially failing" come before their implementation task.
- Service contracts (T029) come before fakes (T030) and implementations (T031, T032); the composition root (T033) comes after both.
- VMs come before views: T035 → T036, T045 → T051, T054/T055/T057 → T059–T063, T067 → T068.

### Parallel opportunities

- Phase 1: T002, T003, T005 and T006 in parallel after T001.
- Phase 3: T009 and T010 in parallel.
- Phase 5: T027, T028, T029 and T032 in parallel once T026 exists (T032 after T029).
- Phase 6: T040 and T042 in parallel; the inspectors T047–T050 in parallel after T045 and T046.
- Phase 7: T058 in parallel with the VM ports.
- Phase 8: T066, T069 and T070 in parallel.

## Parallel Example: Phase 6 inspectors

```bash
Task: "T047 ClassInspectorView.axaml in NetPrints.Editor/Views/Inspectors/"
Task: "T048 MethodInspectorView.axaml in NetPrints.Editor/Views/Inspectors/"
Task: "T049 VariableInspectorView.axaml in NetPrints.Editor/Views/Inspectors/"
Task: "T050 MemberVariableView.axaml in NetPrints.Editor/Views/"
```

## Parity traceability (PAR → tasks)

| PAR | Tasks | PAR | Tasks |
|-----|-------|-----|-------|
| 01 | T028, T036 | 31 | T051 |
| 02 | T035, T036 | 32 | T045, T051 |
| 03–04 | T034, T035, T039 | 33 | T045, T051 |
| 05 | T033, T034, T073 | 34 | T045, T047 |
| 06 | T034, T035, T074 | 35 | T048 |
| 07–10 | T034–T036 | 36 | T049 |
| 11 | T032, T034, T036, T073 | 37 | T043, T045, T052 |
| 12–14 | T034–T036 | 38 | T059, T064 |
| 15 | T031, T034 | 39–42 | T053, T055, T058, T060 |
| 16–20 | T037, T038 | 43–45 | T053, T054, T058, T061 |
| 21 | T038 | 46–47 | T053, T056, T057, T063, T068, T074 |
| 22 | T032, T051 | 48 | T053, T054, T061, T062 |
| 23 | T043, T045, T051 | 49–50 | T056, T057, T059 |
| 24–28 | T043, T045, T051 | 51 | T059 |
| 29–30 | T043, T044, T050 | 52–54 | T065–T068, T075 |
| — | — | 55 | T071 |
| — | — | 56–57 | T050, T072 |
| — | — | 58–59 | T069, T070, T075 |
| — | — | 60 | T040, T041 |

Every PAR item is also checked by hand in T076.

## Implementation Strategy

### MVP first (US1)

1. Phases 1–3 give Core, CLI, tests and sample compile/run green on Linux. **Stop and validate.**
2. Add the CI workflow early (T077) so every later commit is gated.

### Incremental delivery

1. US5 reflection library → a commit with green tests.
2. US2 in five sub-phases (shell → class editor → canvas → search/dialogs/DnD → UI tests). Each ends
   at a checkpoint with the app runnable and the tests green.
3. US4 cleanup after parity sign-off; polish; open the PR against `danielmeza/netprints:master`.

## Notes

- Keep the persisted format unchanged: no edits to DataContract attributes in Core.
- Do not implement P1–P5 items (serialization, extension points, Spectre CLI, VSIX workflow).
- The known WPF defect (remove-class button, PAR-11) is intentionally fixed.
- UI tests must use `UiTest.RunAsync`, never raw `session.Dispatch` with assertions inside, and need `[Timeout]`.
