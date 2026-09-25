# Contract: Editor services (view-model ↔ host boundary)

View models in `NetPrints.Editor` depend only on these interfaces (namespace
`NetPrints.Editor.Services`), never on Avalonia or Nodify types. The Avalonia implementations live in
`src/NetPrints.Editor/Services/Avalonia` (so the headless tests can run the real app) and are wired by
`EditorComposition`; view models receive them bundled in an `EditorContext` record. Tests provide fakes.

**As implemented (P0)**: `IEditorDialogs` has `ShowReferencesAsync(ReferenceListVM)` instead of
`ShowProgress` (the progress overlay is `MainEditorVM.IsBusy/BusyTitle/BusyMessage`, which keeps it
testable without a UI); `IWindowService` is keyed by `ClassGraph` (`TryActivateClassEditor`,
`OpenClassEditor`, `CloseClassEditor`, `CloseAllClassEditors`). `IReflectionHost` also exposes
`LastWarnings` and an explicit loaded state: `IsLoaded`, and `Loaded` (a task). `Provider` throws
before the first load. `ReloadAsync(Project, CancellationToken)`. `EditorContext` has two more
required members (review follow-up; no defaults):
- `IScheduler Scheduler`, for time-based work such as the search throttle (`DefaultScheduler` on
  the desktop, `TestScheduler` in tests);
- `Func<IMessenger> CreateMessenger`, one messenger per class editor. P3 (extension host), P4 (VS host) and P5
(browser/sidecar) provide their own implementations, which is why this is a contract.

| Service | Members (signatures condensed) | Replaces (WPF) | Desktop impl | Test fake |
|---------|--------------------------------|----------------|--------------|-----------|
| `IFilePickerService` | `Task<string?> OpenFileAsync(string title, IReadOnlyList<FileFilter> filters)`; `Task<string?> SaveFileAsync(string title, string suggestedName, string defaultExtension, IReadOnlyList<FileFilter> filters)`; `Task<string?> OpenFolderAsync(string title)` | `OpenFileDialog`, `SaveFileDialog`, WinForms `FolderBrowserDialog` | `TopLevel.StorageProvider` (paths from `TryGetLocalPath()`) | queue of canned answers |
| `IEditorDialogs` | `Task ShowErrorAsync(string title, string message)`; `Task<TypeSpecifier?> SelectTypeAsync(IEnumerable<TypeSpecifier> types, TypeSpecifier initial)`; `Task<MethodSpecifier?> SelectMethodAsync(IEnumerable<MethodSpecifier> methods)`; `Task ShowReferencesAsync(ReferenceListVM references)` | `MessageBox`, MahApps `ShowMessageAsync`/`ShowProgressAsync`, `SelectTypeDialog`, `SelectMethodDialog` | Avalonia dialog windows + overlay | records calls, returns canned values |
| `IClipboardService` | `Task SetTextAsync(string text)` | `Clipboard.SetText` | `TopLevel.Clipboard` | in-memory |
| `IUiDispatcher` | `void Post(Action a)`; `Task InvokeAsync(Action a)`; `bool CheckAccess()` | `Dispatcher.Invoke`, `Dispatcher.CurrentDispatcher` | `Avalonia.Threading.Dispatcher.UIThread` | runs inline |
| `IReflectionHost` | `IReflectionProvider Provider { get; }`; `ReadOnlyObservableCollection<TypeSpecifier> NonStaticTypes { get; }`; `IReadOnlyList<string> LastWarnings { get; }`; `Task ReloadAsync(Project project)`; `event EventHandler? Reloaded` | static `App.ReflectionProvider`, `App.NonStaticTypes`, `App.ReloadReflectionProvider` | `ReferenceAssemblyResolver` + `MemoizedReflectionProvider(new ReflectionProvider(...))`, built off the UI thread and published via `IUiDispatcher` | same implementation over the runtime set, or a stub provider |
| `IWindowService` | `bool TryActivateClassEditor(ClassGraph cls)`; `void OpenClassEditor(ClassEditorVM vm)`; `void CloseClassEditor(ClassGraph cls)`; `void CloseAllClassEditors()` | `MainEditorWindow.classEditorWindows` code-behind | a window registry keyed by `ClassGraph` | records calls |
| `IProcessLauncher` | `void Start(string fileName, string? arguments)` | `Process.Start(exe)` in `Project.RunProject` (Core keeps the default) | `Process.Start` | records calls (UI tests do not spawn processes) |
| `IMessenger` (CommunityToolkit) | `Send`/`Register` for `OpenGraphMessage` (the only message; node selection and node creation are direct VM calls, `AddNodeRequest` is a parameter object) | MvvmLight `MessengerInstance` | `WeakReferenceMessenger.Default` | `new StrongReferenceMessenger()` per test |

`FileFilter` is `record FileFilter(string Name, IReadOnlyList<string> Patterns)`, for example
`new("Project Files", ["*.netpp"])` and `new("Class Files", ["*.netpc"])`.

## Plain value types used by view models (no UI types)

- `readonly record struct GraphPoint(double X, double Y)`, with `+`, `-` and `Lerp` helpers. It
  replaces WPF `Point`/`Vector` in `NodePinVM` (`AbsolutePosition`, `ConnectedCP1/2`,
  `ConnectingAbsolutePosition`, `NodeRelativePosition`). A view converter maps it to/from
  `Avalonia.Point`.
- `enum NodeVisualKind { Default, Entry, Return, CallMethod, CallStatic, Constructor, MakeDelegate, Type, VariableGetter, VariableSetter, MakeArray, Throw, Ternary }`
  and `enum PinKind { Exec, Data, Type }`. They replace the brushes. The view maps them to brushes
  in a resource dictionary with the same ARGB values as the WPF editor.
- `bool IsSelected`, `bool IsFaint`, `bool IsConnected` drive style classes (selected border,
  dimmed fill, faint cable) instead of brush properties.
