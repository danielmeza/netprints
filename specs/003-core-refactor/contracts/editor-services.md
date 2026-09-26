# Contract: Editor services, composition, error model and logging (delta to P0)

Base contract: `specs/001-modernize-build/contracts/editor-services.md` (unchanged unless listed here).
Projects: `src/NetPrints.Editor`, `src/NetPrints.Desktop`. Tests: `tests/NetPrints.Editor.Tests`,
`tests/NetPrints.Editor.UITests`. Test obligations (`ED-Txx`) at the end.

## 1. `EditorContext` — `src/NetPrints.Editor/Hosting/EditorContext.cs`

```csharp
namespace NetPrints.Editor.Hosting;

public sealed record EditorContext(
    IFilePickerService FilePicker,
    IEditorDialogs Dialogs,
    IClipboardService Clipboard,
    IUiDispatcher Dispatcher,
    IReflectionHost Reflection,
    IWindowService Windows,
    IProcessLauncher Processes,
    IScheduler Scheduler,
    IScheduler CodeRefreshScheduler,
    Func<IMessenger> CreateMessenger,
    // new in P1 (all required, no defaults):
    ILoggerFactory LoggerFactory,
    IExtensionHost Extensions,
    ProjectPersistence Persistence,
    IProjectSystem Projects,
    IHostChannel HostChannel,
    ISettingsStore Settings,
    ICodeAnalysisHost CodeAnalysis);
```

## 2. New editor services — `src/NetPrints.Editor/Hosting/*.cs`, `…/Diagnostics/*.cs`

```csharp
namespace NetPrints.Editor.Hosting;

public interface IReflectionHost   // P0 members unchanged, plus:
{
    ProjectSnapshot? Snapshot { get; }                        // last loaded snapshot (references, other sources, options)
    // ReloadAsync(Project, CancellationToken) now takes references and other sources from Project.Snapshot
    // (IProjectSystem.LoadAsync, project-system.md §4), adds the in-memory translations of the project's graphs,
    // and composes CompositeReflectionProvider(catalogs…, live) — extension-points.md §4
}

public sealed class ReflectionHost : IReflectionHost
{
    public ReflectionHost(IUiDispatcher dispatcher, IExtensionHost extensions, ILogger<ReflectionHost> logger); // catalogs from extensions.Current
}

namespace NetPrints.Editor.Diagnostics;

/// Debounced background analysis of the open project's generated code.
public interface ICodeAnalysisHost : IDisposable
{
    IObservable<CodeAnalysisSnapshot> Snapshots { get; }       // emitted on the UI thread
    void RequestAnalysis(Project project);                      // debounced 500 ms on EditorContext.Scheduler; cancels the running one
    Task<QuickInfo?> GetQuickInfoAsync(string classFullName, int position, CancellationToken cancellationToken);
}

public sealed record CodeAnalysisSnapshot(IReadOnlyDictionary<string, TranslatedClass> Classes, IReadOnlyList<CodeDiagnostic> Diagnostics);

public sealed class CodeAnalysisHost : ICodeAnalysisHost
{
    public CodeAnalysisHost(IReflectionHost reflection, IExtensionHost extensions, IScheduler scheduler, IUiDispatcher dispatcher, ILogger<CodeAnalysisHost> logger); // translation = extensions.Current.Translation
}
```

| Rule | Contract |
|---|---|
| Threading | Translation of the model runs on the UI thread (model is not thread-safe; moving it off is P8); `CodeAnalysisSession.AnalyzeAsync` runs via `Task.Run`; results marshalled with `IUiDispatcher`. |
| Latency | ≤ 2 s after the last edit for the sample classes (SC-006): debounce 500 ms + analysis. |
| Failure | Analysis exceptions → no snapshot change, log `CodeAnalysisFailed` (1030). |
| Session | Recreated on `IReflectionHost.Reloaded` from `Snapshot.References`, `Snapshot.OtherSources`, `Snapshot.CompilationOptionsJson`. |

`IEditorDialogs` gains (desktop: modal Avalonia dialogs; tests: recorded canned answers):

```csharp
Task<bool> ConfirmTrustAsync(string projectPath, IReadOnlyList<string> extensionFolders);         // true → added to netprints.trustedProjects
Task ShowIssuesAsync(string title, IReadOnlyList<CodeDiagnostic> issues);
```

Create Project (PAR-03) uses `IFilePickerService.SaveFileAsync("Create Project", "MyProject.csproj", "csproj", …)`;
the chosen path gives the directory and project name, then `IProjectSystem.CreateAsync`.

## 3. View models (new/changed) — feature folders of `src/NetPrints.Editor`

| VM / type | File | Members (condensed) | Replaces |
|---|---|---|---|
| `CodeViewVM` | `CodeView/CodeViewVM.cs` | `string Code`, `IReadOnlyList<CodeDiagnostic> Diagnostics`, `IReadOnlyList<FoldingRange> Foldings` (`record FoldingRange(int Start, int End, string Title)`), `Task<QuickInfo?> GetQuickInfoAsync(int position, CancellationToken)` | `ClassEditorVM.GeneratedCode` + TextBox |
| `CodeView` (control) | `CodeView/CodeView.axaml(.cs)` | AvaloniaEdit `TextEditor` (`IsReadOnly`, `ShowLineNumbers`, `WordWrap=false`), TextMate `source.cs` (DarkPlus/LightPlus by theme variant), `FoldingManager`, `SquiggleRenderer : IBackgroundRenderer`, hover → `ToolTip` with `QuickInfo`; font `avares://NetPrints.Editor/Assets/Fonts#Cascadia Mono` | `ClassInspectorView` TextBox |
| `DiagnosticRowVM` | `Diagnostics/DiagnosticRowVM.cs` | `Severity`, `Id`, `Message`, `Location` (`"Class.Method"` or `"Class"`), `GraphKey?`, `NodeId?`, `IRelayCommand NavigateCommand` | strings in `Project.LastCompileErrors` |
| `ErrorListVM` | `Diagnostics/ErrorListVM.cs` | `ReadOnlyObservableCollection<DiagnosticRowVM> Rows` = compile diagnostics ∪ live diagnostics (live replaced per snapshot), sorted Error > Warning > Info, then class, line | `ListBox` of strings |
| `NavigateToNodeMessage` | `Diagnostics/NavigateToNodeMessage.cs` | `record NavigateToNodeMessage(string ClassFullName, string GraphKey, string NodeId)` | — |
| `EventGraphVM` | `Events/EventGraphVM.cs` | `Name` (validated, `NPT002`), `Open`, `Remove` | — |
| `LocalVariableVM` | `Variables/LocalVariableVM.cs` | `Name` (validated, `NPT004`), `Type`, `ChangeTypeCommand` (Select Type dialog), `Remove` (undoable), drag source | — |
| `VariablesPanelVM` | `Variables/VariablesPanelVM.cs` | `ClassVariables`, `MethodVariables` (for `OpenedGraph` if method/ctor), `MethodGroupTitle` (`"Method: <name>"`), `CreateLocalCommand` | lists in `ClassEditorVM` |
| `MemberVariableVM` | `Variables/MemberVariableVM.cs` | ctor `(Variable variable, ClassEditorServices services)` — no `ClassEditorVM` | ctor with `ClassEditorVM owner` |
| `NodeGraphVM` | `Graph/NodeGraphVM.cs` | ctor `(NodeGraph graph, ClassEditorServices services)`; Nodify commands `ConnectionCompletedCommand`, `DisconnectConnectorCommand`, `RemoveConnectionCommand`, `InsertRerouteCommand`, `ToggleFaintCommand` | ctor with `ClassEditorVM owner`; code-behind gestures |
| `ClassEditorServices` | `ClassEditor/ClassEditorServices.cs` | `sealed record ClassEditorServices(EditorContext Context, UndoRedoStack UndoRedo, IMessenger Messenger)` | the owner reference |

Messages replacing parent calls (all via the per-class-editor `IMessenger`): `OpenGraphMessage`
(existing), `SelectInspectorMessage(object Target)`, `NavigateToNodeMessage`. Removal goes through
`UndoRedo.Do(EditorCommands.Remove…)`; `ClassEditorVM` observes `ClassGraph.Variables/Methods/Constructors/EventGraphs`
and `ExecutionGraph.LocalVariables` `CollectionChanged` to close graphs and clear the inspector (so undo
and redo get the same cleanup, P0 review).

Dirty tracking (document-format.md §2.8, data-model.md §2): `ClassEditorVM` calls `ClassGraph.MarkDirty()`
when its `UndoRedoStack` applies a command (`Do`, `Undo`, `Redo`; a new `UndoRedoStack.Applied` event,
not raised by `Clear`), when a node of the class raises `Node.OnPositionChanged`, and in every model
setter that bypasses the undo stack (inspector and wrapper setters below). Opening, viewing, selecting,
panning or zooming never marks a class dirty. Save / Save All / Compile call `ProjectPersistence.SaveAsync`,
which writes only dirty classes.

Model wrapper setters (`MethodVM.Name/Visibility/Modifiers`, `MemberVariableVM` pass-throughs) assign
the model only; the VM re-raises from the model's `PropertyChanged` (now CTK-generated). No
`OnPropertyChanged()` after a model assignment.

New `AutomationIds` (constants in `src/NetPrints.Editor/AutomationIds.cs`): `ClassInspectorCodeView`
(replaces `ClassInspectorGeneratedCode`), `ErrorListRow`, `VariablesClassGroup`,
`VariablesMethodGroup`, `CreateLocalVariableButton`, `EventGraphList`, `CreateEventGraphButton`,
`ExtensionLoadErrors`.

## 4. Composition and DI wiring (explicit, no fallbacks, no container)

`EditorComposition(EditorHostServices host)` replaces `EditorComposition(Func<EditorContext, EditorContext>? customize = null)`.
Tests build their own `EditorContext` in the test project (`tests/NetPrints.Editor.UITests/Hosting/TestComposition.cs`,
`tests/NetPrints.Editor.Tests/Hosting/TestEditor.cs`), never through a hook in production code.

```csharp
namespace NetPrints.Editor.Hosting;

public sealed record EditorHostServices(
    ILoggerFactory LoggerFactory,
    IExtensionHost Extensions,
    ISettingsStore Settings,
    IHostChannel HostChannel,
    bool MsBuildAvailable);    // result of MsBuildRegistration.EnsureRegistered() in Desktop Main

public sealed class EditorComposition
{
    public EditorComposition(EditorHostServices host);
    public EditorContext Context { get; }
    public WindowService Windows { get; }
    public MainEditorVM? MainEditor { get; }
    public IDisposable InstallUnhandledExceptionHandler();
    public MainWindow CreateMainWindow();
}
```

| Service | Created by | Lifetime | Depends on |
|---|---|---|---|
| `ILoggerFactory` | Desktop `Program` (`LoggerFactory.Create(b => b.AddSimpleConsole(...).SetMinimumLevel(Information))`; `NETPRINTS_LOG_LEVEL` overrides) | process; disposed on exit | — |
| Avalonia log sink `AvaloniaLogSink : ILogSink` | Desktop `Program` (`.LogToTrace()` replaced by `Logger.Sink = new AvaloniaLogSink(loggerFactory)`) | process | `ILoggerFactory` |
| `ISettingsStore` (`JsonFileSettingsStore`) | Desktop | process | path from `JsonFileSettingsStore.DefaultFilePath` |
| MSBuild registration | Desktop `Main` first statement: `MsBuildRegistration.EnsureRegistered()` (project-system.md §4) | process | — |
| `IExtensionHost` (`ExtensionHost`) | Desktop: `ExtensionLoader` over settings `ExtensionPaths` + `NETPRINTS_EXTENSION_PATH`; `LoadForProject` on open of a trusted project | process; disposed on exit | `ILoggerFactory`, settings |
| `IHostChannel` | Desktop (`NETPRINTS_HOST_CHANNEL`, extension-points.md §6) | process; `DisposeAsync` on exit | registry |
| `EditorHostServices` | Desktop, passed to `EditorApp` (`EditorApp.HostServices` static init property set before `StartWithClassicDesktopLifetime`; `InvalidOperationException` if unset) | process | above |
| `IProjectSystem` (`MsBuildProjectSystem`) | `EditorComposition` | editor | `ProjectSystemOptions` (properties requested by extensions), `ProcessRunner`, logger; if `MsBuildAvailable` is false a `NoSdkProjectSystem` whose members throw `ProjectSystemException(NPW001)` |
| `ReflectionHost` | `EditorComposition` | editor | dispatcher, extension host (catalogs), logger |
| `DocumentFormatRegistry`, `DocumentMapper`, `ProjectPersistence` | `EditorComposition`; mapper rebuilt on `RegistryChanged` | editor | `IProjectSystem`, `Current.NodeConverters`, `FileSystemDocumentStore` factory, logger |
| `CodeAnalysisHost` | `EditorComposition` | editor; disposed with the main window | reflection, extension host, scheduler, dispatcher, logger |
| P0 services (pickers, dialogs, clipboard, dispatcher, windows, processes, schedulers, messenger factory) | `EditorComposition` (unchanged) | as in P0 | — |
| `IMessenger` | `Context.CreateMessenger()` per class editor | class editor | — |
| `UndoRedoStack`, `ClassEditorServices` | `ClassEditorVM` | class editor | — |

CLI (`src/NetPrints.Cli/Program.cs`, P0 behavior until P2): `MsBuildRegistration.EnsureRegistered()`, then
`-p <file>`: a `.csproj` is built with `IProjectSystem.BuildAsync` (and run with `GetRunCommand` for `-r`); a
`.netpp` or any other file is rejected with a message that only `.csproj` projects are supported (no conversion,
research R21). Console logger. Exit codes unchanged (the rejection uses the bad-arguments code).

Generator (`src/NetPrints.Generator`, project-system.md §3): `ExtensionLoader` (request folders only),
`DocumentFormatRegistry`, `DocumentMapper`, `GraphCodeGenerator`; console logger at Warning; no MSBuild.

## 5. Error model and UI mapping

| Source | Type | Where it shows |
|---|---|---|
| Document load/save | `DocumentIssue` (`NPD001–009`), `DocumentFormatException`, `DocumentVersionException` | Error dialog on open (issues listed, project stays open unless the project file itself failed); log 1040 |
| Project system | `ProjectMessage` (`NPW001–005`, MSBuild `MSB*`, NuGet `NU*`), `ProjectSystemException` | Error list rows after load/build; `NPW001` (no SDK) and `NPW003` (evaluation failed) as a dialog |
| Translation | `TranslationException` → `CodeDiagnostic` (`NPT001–007`) | Error list rows with node link; squiggle when a span exists |
| Compiler (live) | Roslyn → `CodeDiagnostic` (`CSxxxx`) via `DiagnosticMapper.FromRoslyn` | Squiggles in the code view + error list rows with node link |
| Build | `BuildResult.Messages` → `DiagnosticMapper.FromBuild` | Error list rows with node link (generated-file spans mapped through the in-memory source map) |
| Extensions | `ExtensionLoadResult.Failed` (`NPX001–007`) | One dialog at startup listing failures (`AutomationIds.ExtensionLoadErrors`); log 2003 |
| Unhandled | exceptions on the UI thread / unobserved tasks | Error dialog (P0); log 1001/1002 |

Navigation: `DiagnosticRowVM.NavigateCommand` (double-click) sends `NavigateToNodeMessage`;
`ClassEditorVM` opens `GraphKeys.Resolve(cls, key)`, selects the node with `NodeId`, and asks the view
to bring it into view (`NodeGraphVM.RevealNode(string nodeId)` → view centers the viewport). Rows
without `NodeId` open the graph only; rows without `GraphKey` do nothing.

## 6. Logging — `[LoggerMessage]` event ids

One `internal static partial class Log` per feature folder (`…/Log.cs`). `CA1848` and `CA2254` are
errors in Editor, Desktop, Extensibility, Serialization, Workspace and Generator. Categories = the owning type.

| Id | Name | Level | Project / class | Message template |
|---|---|---|---|---|
| 1001 | `UnhandledUiException` | Error | Editor / `UnhandledExceptionHandler` | `Unhandled exception on the UI thread` (exception attached) |
| 1002 | `UnobservedTaskException` | Error | same | `Unobserved task exception` |
| 1010 | `ReflectionReloadStarted` | Debug | Editor / `ReflectionHost` | `Reloading types for {ProjectName}` |
| 1011 | `ReflectionReloadCompleted` | Information | same | `Types loaded for {ProjectName} in {ElapsedMs} ms ({AssemblyCount} assemblies)` |
| 1012 | `ProjectMessage` | Warning | same | `{Code}: {Message}` |
| 1013 | `ReflectionReloadFailed` | Error | same | `Loading types for {ProjectName} failed` |
| 1020 | `HostMessageReceived` | Debug | Editor / `HostChannelBridge` | `Host message {Type}` |
| 1021 | `HostChannelUnknown` | Error | Desktop / `Program` | `Host channel {Id} is not provided by any extension` |
| 1022 | `HostMessageIgnored` | Debug | Editor / `HostChannelBridge` | `Ignoring host message {Type}` |
| 1030 | `CodeAnalysisFailed` | Warning | Editor / `CodeAnalysisHost` | `Code analysis failed for {ProjectName}` |
| 1040 | `ProjectLoadIssue` | Warning | Editor / `MainEditorVM` | `{Code}: {Message} ({Document})` |
| 1050 | `GridShaderUnavailable` | Warning | Editor / `GridBackground` (via Avalonia sink) | existing text (P0.1), forwarded by `AvaloniaLogSink` |
| 2001 | `ExtensionDiscovered` | Debug | Extensibility / `ExtensionLoader` | `Found extension {Id} at {ManifestPath}` |
| 2002 | `ExtensionLoaded` | Information | same | `Loaded extension {Id} {Version}` |
| 2003 | `ExtensionLoadFailed` | Error | same | `Extension {Id} failed: {Code} {Reason}` |
| 2004 | `ExtensionDuplicate` | Warning | same | `Extension {Id} at {ManifestPath} ignored: already loaded` |
| 2005 | `NodeKindConflict` | Error | same | `Node kind {Kind} from {Id} rejected: {Reason}` |
| 2006 | `SettingsSectionInvalid` | Warning | Extensibility / `JsonFileSettingsStore` | `Settings section {Section} is invalid; using defaults` |
| 3001 | `DocumentMigrated` | Information | Serialization / `DocumentMigrator` | `Migrated {Document} from schema {From} to {To}` |
| 3002 | `UnknownNodeKindPreserved` | Warning | Serialization / `DocumentMapper` | `Node {NodeId} of unknown kind {Kind} in {Document} is preserved` |
| 3003 | `ConnectionDropped` | Warning | same | `Connection {From} -> {To} in {Document} dropped: {Reason}` |
| 3004 | *(retired 2026-09-26, research R21: no legacy import; the id is not reused)* | | | |
| 3005 | `ExternalChange` | Debug | Serialization / `FileSystemDocumentStore` | `{Kind} {Document}` |
| 3006 | *(retired 2026-09-26, research R21: no `ProjectConverter`; the id is not reused)* | | | |
| 4001 | `RestoreStarted` | Debug | Workspace / `MsBuildProjectSystem` | `Restoring {Project}` |
| 4002 | `ProjectLoaded` | Information | same | `Loaded {Project} in {ElapsedMs} ms ({ReferenceCount} references, {GraphCount} graphs)` |
| 4003 | `WorkspaceDiagnostic` | Warning | same | `{Message}` |
| 4004 | `BuildFinished` | Information | same | `Build of {Project} {Result} in {ElapsedMs} ms ({ErrorCount} errors)` |
| 4005 | `MsBuildNotFound` | Error | Workspace / `MsBuildRegistration` | `No .NET SDK found; projects cannot be opened` |

`GridBackground` keeps Avalonia's `Logger` (controls have no DI); `AvaloniaLogSink` forwards Avalonia
log areas to `ILogger` categories `Avalonia.<Area>` so it reaches the console (FR-042).

## 7. Architecture gate — `tests/NetPrints.Editor.Tests/Architecture/ArchitectureGateTests.cs`

Roslyn syntax/semantic scan of `src/NetPrints.Editor/**/*VM.cs` and `src/NetPrints.Editor/**/*ViewModel*.cs`
(compiled against the editor's references):

| Rule | Violation |
|---|---|
| A1 | a VM file uses a type from namespaces `Avalonia*` or `Nodify*` |
| A2 | a VM constructor or field has type `ClassEditorVM` or `MainEditorVM` unless the file is that VM itself |
| A3 | `src/NetPrints.Core`, `NetPrints.Reflection`, `NetPrints.Serialization`, `NetPrints.Extensibility` reference an assembly named `Avalonia*` (project-reference/package scan) |

Fixture `tests/NetPrints.Editor.Tests/Architecture/Fixtures/ViolatingVM.cs.txt` (not compiled into the
editor) contains one violation of A1 and A2; the test asserts the scanner reports exactly those two,
then asserts zero violations for the real sources and a non-empty set of scanned files.

## 8. Test obligations

| ID | Case |
|---|---|
| ED-T01 | Code view (headless): highlighted tokens (pixel check or TextMate token color), line numbers visible, no wrap (horizontal scroll extent > viewport), folding markers for type and method; snapshot `class-inspector-code-view` regenerated and reviewed |
| ED-T02 | Live diagnostics: a type error introduced via the model yields a squiggle and an error row within virtual-time debounce; fixing it clears both |
| ED-T03 | Navigation: double-click on a row with `NodeId` opens the graph, selects that node and brings it into view |
| ED-T04 | Build errors (a CS error in a generated file and an NPT generator error) appear as structured rows linked to the node |
| ED-T05 | Hover over `WriteLine` shows signature and summary |
| ED-T06 | Variables panel groups "Class" and "Method: Main"; create/rename/retype/remove local, undo/redo each; drag local → Get/Set chooser |
| ED-T07 | Event graphs: create, open, add custom event via search, remove (undoable) |
| ED-T08 | Undo of a removed variable/method closes and restores graphs and inspector exactly like the command path (no parent call) |
| ED-T09 | Nodify commands: connection completed on empty canvas opens search; disconnect (middle click) and insert reroute (double click) go through `NodeGraphVM` commands (VM-level tests + one headless gesture each) |
| ED-T10 | Architecture gate demonstrated to fail on the fixture and pass on the sources |
| ED-T11 | Extension load failure dialog lists failures; editor usable |
| ED-T12 | `UnhandledExceptionHandler` logs 1001/1002 through a collecting logger and still shows the dialog |
| ED-T13 | Open offers `*.csproj` only; opening a `.netpp` shows a message and writes nothing; Save writes edited graphs and their `.netpc.g.cs`; the References dialog and binary-type chooser edit the `.csproj` (PS-T09 at VM level) (revised 2026-09-26: the conversion flow is gone) |
| ED-T14 | No production type has an optional `customize`/test hook parameter (`EditorComposition` ctor takes `EditorHostServices` only) |
| ED-T15 | Dirty tracking: open a project with two classes, open both editors, pan, zoom and select → Save writes 0 files; move one node of class A → A dirty, Save writes A's graph and `.netpc.g.cs` only; add a node then undo → A dirty (the file is rewritten only if bytes differ); rename a method in the inspector → dirty; after Save both classes are clean |
