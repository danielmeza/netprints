# Data Model: Build Topology and Editor View-Model Graph (P0)

P0 does not change the persisted data. `.netpp`/`.netpc` DataContract XML is untouched, which
satisfies constitution VI. The "entities" here are (1) the projects that make up the build and
(2) the editor's view-model graph, which is rebuilt on Avalonia.

## 1. Build topology (after P0)

| Project | Path | SDK / TFMs | References | In `NetPrints.sln` | CI coverage |
|---------|------|-----------|------------|--------------------|-------------|
| NetPrints (Core) | `NetPrints/` | Microsoft.NET.Sdk; `net10.0` | Roslyn 5.9, Fody 6.9.3 + PropertyChanged.Fody 4.1.0 (P1 removes) | yes | build + NetPrintsUnitTests + Editor.Tests |
| NetPrints.Reflection | `NetPrints.Reflection/` (new) | `net10.0` | Core, Roslyn 5.9 | yes | Editor.Tests/Reflection |
| NetPrints.Editor | `NetPrints.Editor/` (new) | `net10.0` | Core, Reflection, Avalonia 12.1.3 (+Themes.Fluent, Fonts.Inter), Nodify.Avalonia 2.0.0, CommunityToolkit.Mvvm, DynamicData, Xaml.Behaviors.Avalonia, Material.Icons.Avalonia; contains `EditorApp` (App.axaml), views, VMs and the Avalonia service implementations | yes | Editor.Tests/ViewModels + Ui |
| NetPrints.Desktop | `NetPrints.Desktop/` (new) | `net10.0`, `WinExe` | Editor, Avalonia.Desktop (only `Program.cs`: `BuildAvaloniaApp()` → `EditorApp`, args, icon) | yes | build (+ `--help`-free startup is covered by the headless `EditorApp` tests) |
| NetPrintsCLI | `NetPrintsCLI/` | `net10.0`, `Exe` | Core, CommandLineParser 2.9.1 | yes | build + `--version` smoke |
| NetPrintsUnitTests | `NetPrintsUnitTests/` | `net10.0`, MTP exe | Core, MSTest 4.4.1 | yes | 11 tests |
| NetPrints.Editor.Tests | `NetPrints.Editor.Tests/` (new) | `net10.0`, MTP exe | Editor, Reflection, MSTest 4.4.1, Avalonia.Headless, Avalonia.Skia | yes | new tests |
| ~~NetPrintsEditor~~ | deleted | — | — | removed | — |
| ~~NetPrintsEditorUnitTests~~ | deleted | — | — | removed | — |
| NetPrintsVSIX | `NetPrintsVSIX/` (kept, `README.md` "pending P4") | legacy csproj, v4.6.1 | stale refs to the deleted editor | **removed** | none (P4 adds a chained Windows workflow) |

Dependency direction (no cycles; UI only in Editor/Desktop):
`Core ← Reflection ← Editor ← Desktop`; `Core ← CLI`; tests reference what they test.

Shared build files: `global.json`, `Directory.Build.props`, `Directory.Packages.props`
(CPM + transitive pinning), `NetPrints.sln`, `.github/workflows/ci.yml`.

## 2. Editor view-model graph (NetPrints.Editor/ViewModels)

The names are kept from the WPF editor to make the port traceable. Every VM derives from
`ObservableObject` (CommunityToolkit). Services come from `contracts/editor-services.md`.

| VM | Wraps (model) | Key state | Commands / operations | Messages |
|----|---------------|-----------|----------------------|----------|
| `MainEditorVM` | `Project?` | `IsProjectOpen`, `CanCompile`, `CanCompileAndRun`, `IsProjectPaneOpen`, `IsSettingsPaneOpen`, `IsBusy` (progress overlay), `Classes` | `CreateProject`, `OpenProject(path?)`, `SaveProject`, `Compile`, `Run`, `NewClass`, `AddExistingClass`, `OpenClass(ClassGraph)`, `RemoveClass(ClassGraph)`, `ShowReferences` | — |
| `ReferenceListVM` | `Project` | `References` (→ `CompilationReferenceVM`) | `AddAssembly`, `AddSourceDirectory`, `Remove(ref)` | — |
| `CompilationReferenceVM` | `CompilationReference` | `DisplayText`, `ShowIncludeInCompilation`, `IncludeInCompilation` | — | — |
| `ClassEditorVM` | `ClassGraph` | `Methods`, `Constructors` (`MethodVM`; as implemented, a light wrapper instead of a full `NodeGraphVM` per entry), `Variables` (`MemberVariableVM`), `OverridableMethods`, `OpenedGraph`, `Inspector` (Class/Variable/Method), `GeneratedCode` (debounced ~1 s), class props | `CreateMethod`, `CreateConstructor`, `CreateOverride(MethodSpecifier)`, `CreateVariable` (undoable), `RemoveMethod/Constructor`, `OpenGraph`, `OpenClassGraph`, `SelectVariable`, `SelectMethod`, `Save`, `Compile`, `Run`, `DeleteSelectedNodes`, `Undo`, `Redo` | recv `OpenGraphMessage` |
| `MethodVM` (as implemented) | `ExecutionGraph` | `Name` (read-only for constructors), `IsConstructor`, `Visibility`, `IsSealed/IsAbstract/IsStatic/IsVirtual/IsOverride/IsAsync` | `ToMethodSpecifier`, `ToConstructorSpecifier` (drag &amp; drop) | — |
| `MemberVariableVM` | `Variable` | `Name`, `Visibility`, `Modifiers`, `HasGetter/HasSetter`, `Specifier` | `AddGetter/RemoveGetter/AddSetter/RemoveSetter` (undoable), `OpenGetter/Setter/TypeGraph` | send `OpenGraphMessage` |
| `NodeGraphVM` | `NodeGraph` (method, constructor, class, type graph) | `Nodes` (`NodeVM`), `Connections` (derived from connected pins), `SelectedNodes`, `Name`, `IsConstructor`, `Visibility`, `Modifiers`, `SuggestionPin`, `Search` (`SuggestionListVM`), `GetSetChooser` | `UpdateSuggestions(GraphPoint, NodePin?)`, `AddNode(AddNodeMessage)`, `SelectNodes`, `DeselectNodes`, `DragStart/Move/End` (grid snap 28), `DropMethod`, `DropConstructor`, `DropVariable`, `Connect(pin, pin)` | send/recv `NodeSelectionMessage`, `AddNodeMessage` |
| `NodeVM` | `Node` | `Label`, `ToolTip`, `VisualKind`, `IsSelected`, `ZIndex`, `IsRerouteNode`, pin collections ×6, `Overloads`, `ShowOverloads`, `IsPure`, `CanSetPure`, `ShowLeft/RightPinButtons` + tooltips, `Location` (`GraphPoint`) | `ChangeOverload` (undoable), `Left/RightPinsPlus/Minus`, `Select` | send `NodeSelectionMessage` |
| `NodePinVM` | `NodePin` | `PinKind`, `IsInput`, `DisplayName`, `Name`/`IsNameEditable`, `ToolTip`, `UnconnectedValue` + `ShowUnconnectedValue/ShowEnumValue/ShowBooleanValue`, `PossibleEnumNames`, `UnconnectedTextWatermark`, `ShowDefaultValueIndicator`, `IsConnected`, `ConnectedPin`, `IsFaint`, `IsBeingConnected`, `Anchor` (`GraphPoint`, set by the view) | `ConnectTo(other)` (validated by `GraphUtil.CanConnectNodePins`), `DisconnectAll`, `AddRerouteNode`, `ClearUnconnectedValue`, `ToggleFaint` | — |
| `ConnectionVM` (new) | pair of `NodePinVM` | `Source`, `Target`, `PinKind`, `IsFaint` | `Disconnect`, `InsertReroute`, `ToggleFaint` | — |
| `SuggestionListVM` | suggestions for a graph/pin | `SearchText`, `Items` (DynamicData-filtered `ReadOnlyObservableCollection<SuggestionItem>`), `Position` | `Select(SuggestionItem)` → node creation / dialogs via `IEditorDialogs` | send `AddNodeMessage` |
| `SuggestionItem` (was `SearchableComboBoxItem`) | `(Category, Value)` | `Text`, `IconKey` (the old `SuggestionListConverter` logic moved into the VM), `SearchText` | — | — |
| `GetSetChooserVM` (new) | `VariableSpecifier` + position | `CanGet`, `CanSet`, `IsOpen` | `Get`, `Set` | send `AddNodeMessage` |

**Undo/redo** (`UndoRedoStack`, per class editor instead of a global singleton): command pairs
exactly as in PAR-60; they are implemented as `IUndoableCommand` objects rather than WPF
`RoutedUICommand`s.

**State transitions**
- Project: none → open (created/loaded) → dirty/saved; compile: Ready → Compiling → Succeeded |
  Failed(N). The reflection host reloads on open, on references change and on compile end.
- Pin connection drag: idle → pending (preview follows the pointer) → completed on a compatible
  pin (connect) | completed on empty canvas (open search with `SuggestionPin`) | cancelled.
- Search popup: closed → open (items computed, text cleared, focus) → item chosen (node created,
  popup closed) | dismissed.
