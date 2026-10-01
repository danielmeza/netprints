# Data Model: Editor Shell (P3a)

Editor-side entities (namespace roots under `NetPrints.Editor`) and the few model changes in `NetPrints.Core`.
View models stay free of UI-toolkit types (constitution II); only views and the docking adapter use Avalonia or Dock
types. Persisted formats are in [contracts/state-files.md](./contracts/state-files.md); the registry contract is in
[contracts/contributions.md](./contracts/contributions.md).

## Shell

| Entity | Fields | Rules |
|---|---|---|
| `ShellViewModel` | `Session: ProjectSessionViewModel?`, `Documents: ObservableCollection<DocumentViewModel>`, `ActiveDocument: DocumentViewModel?`, `Panels: IReadOnlyList<PanelViewModel>`, `StatusMessage: string?`, `Title: string` | One per window. `Session` is null while the start page shows. `Title` = `"<active graph> – <project>[*] – NetPrints"`, or `"NetPrints"` with no project. |
| `IShell` (service) | `OpenDocument(DocumentId)`, `ActivateDocument(DocumentId)`, `CloseDocument(DocumentId)`, `ShowPanel(string panelId)`, `HidePanel(string panelId)`, `FloatDocument(DocumentId)`, `DockDocument(DocumentId)`, `ResetLayout()`, `ActiveDocument` | The only shell API that features use; implemented by the Dock adapter (ADR-0018). |
| `DocumentId` | `Kind` (`Graph`, `StartPage`, `ProjectSettings`), `ClassPath` (class file path relative to the project), `GraphKey` (method or constructor id, event graph id, or `class`) | Value equality. Serialized as `graph:<classPath>#<graphKey>`, `start`, or `project-settings`. |
| `DocumentViewModel` | `Id: DocumentId`, `Title`, `IsUnsaved` (from the owning file), `Breadcrumbs: IReadOnlyList<BreadcrumbViewModel>` | `GraphDocumentViewModel` wraps the existing graph editor view model and adds `Viewport` (location, zoom). |
| `PanelViewModel` | `Id` (`netprints.panel.*`), `Title`, `IconKind`, `DefaultDock` (`Left`, `Right`, `Bottom`), `Order` | Built-ins: `projectTree`, `inspector`, `errors`, `output`, `csharp`. |

## Project session and lifecycle

| Entity | Fields | Rules |
|---|---|---|
| `ProjectSessionViewModel` | `Project`, `ProjectFilePath`, `ProjectKey` (first 16 hex chars of SHA-256 of the full project path), `UnsavedFiles: IReadOnlyList<UnsavedFile>` | Created on open; disposed on unload, after `ConfirmUnloadAsync` returns true. |
| `RunStateTracker` | `State` (`NotStarted`, `Building`, `Running`, `Exited`), `ExitCode?`, `StdoutTail`, `StderrTail` | Fed by the compile and run flow through `IProcessLauncher`; tails keep the last 200 lines; a new compile or run resets it. The Output panel and the automation pipe's `runState` reply (contracts/ci.md §3) read it. |
| `EditorDataPaths` | `Root` (`<ApplicationData>/NetPrints`, or `NETPRINTS_STATE_DIR`), `StateDirectory`, `BackupDirectory(projectKey)`, `ProjectKey(path)` | The only place that resolves per-user paths; writes go through `AtomicFileWriter` (temporary file, rename, user-only modes on Unix). |
| `UnsavedFile` | `Path`, `Kind` (`Class`, `Project`), `DisplayName` | Derived from `ClassGraph.IsDirty`, and from the project when a project-level change is pending. |
| `UndoRedoStack` (changed) | adds `MarkSaved()`, `IsAtSavedState` | Saved marker = undo depth plus the identity of the top command at save time. `Clear()` resets it to "no saved state". |
| `BackupService` | `Schedule(UnsavedFile)`, `Delete(path)`, `FindBackups(projectKey)`, `Prune(now)` | Debounce of 30 s per file; atomic write; user-only permissions on Unix; failures logged and reported once per session. |
| `Backup` | `OriginalPath`, `WrittenAt` (UTC), `ContentHash` (SHA-256 of the backup), `BackupPath` | Listed in `manifest.json`; `IsOlderThanFile` when the original's last write time is later than `WrittenAt`. |

Lifecycle of a class file:

```text
Saved --edit--> Unsaved --(30 s idle)--> Unsaved+BackedUp
Unsaved(+BackedUp) --save ok--> Saved (backup deleted)
Unsaved(+BackedUp) --undo to saved marker--> Saved (backup deleted)
Unsaved(+BackedUp) --unload: Don't save--> (closed, backup deleted)
Unsaved+BackedUp --crash--> next open: Recovery offered --Restore--> Unsaved (backup kept until save/discard)
                                                     --Discard--> Saved (backup deleted)
```

## Commands and contributions

See [contracts/contributions.md](./contracts/contributions.md) for the full shapes.

| Entity | Key fields | Rules |
|---|---|---|
| `CommandDescriptor` | `Id`, `Label`, `Handler`, `IconKind?`, `DefaultGestures?` (list), `Scope` (flags `Global` = 0, `Graph`, `ProjectTree`; a command may name several), `Menu?` (`Path`, `Group`, `Order`), `CommandBarOrder?` | Id pattern `^[a-z0-9]+(\.[a-zA-Z0-9]+)+$`. A single-key gesture other than a function key requires a non-global scope. |
| `ICommandHandler` | `CanExecute(CommandContext)`, `ExecuteAsync(CommandContext, CancellationToken)`, `DynamicLabel(CommandContext)?` | `DynamicLabel` provides "Undo Add node". |
| `CommandContext` | `Shell`, `Session?`, `ActiveDocument?`, `ActiveGraph?`, `Selection` (nodes, tree item), `Parameter?` | Built per invocation by an `ICommandContextProvider`; no Avalonia types. `Session` and `ActiveGraph` are the concrete editor view models until P3 replaces them with interfaces (ADR-0020). |
| `ContributionIssue` | `Kind` (`DuplicateId`, `GestureConflict`, `InvalidDescriptor`), `Id`, `Owner`, `Message` | Collected in `IContributionRegistry.Issues`; the first registration wins. |
| `DashboardTileDescriptor` | `Id`, `Title`, `Order`, `CreateViewModel` | Built-ins: recent, open, new, samples, what's new. |
| `ProjectTemplateDescriptor` | `Id`, `DisplayName`, `Description`, `ProfileId`, `OutputType`, `IconKind?` | Built-ins: `netprints.template.console`, `netprints.template.library`. |
| `ContextMenuItemDescriptor` | `Id`, `Target` (`Node`, `Pin`, `Connection`, `Canvas`, `TreeClass`, `TreeMember`, `TreeEventGraph`), `CommandId`, `Group`, `Order` | Items reference commands; they never carry their own logic. |
| `ITooltipProvider` | `Order`, `TryProvide(TooltipTarget) → TooltipContent?` | `TooltipContent` = title, lines, documentation text. |
| `IGoToProvider` | `Kind`, `Search(string, CancellationToken) → IAsyncEnumerable<GoToItem>` | `GoToItem` = kind, title, detail, `NavigationTarget`. |

## Navigation

| Entity | Fields | Rules |
|---|---|---|
| `NavigationTarget` | `Document: DocumentId`, `NodeId?`, `PinId?` | Resolving a target opens the document, centres and selects the item. |
| `NavigationEntry` | `Document`, `ViewportLocation` (x, y), `Zoom`, `SelectedNodeIds` | Recorded before every navigation. |
| `NavigationHistory` | `Back`, `Forward` (max 50 entries each) | A new navigation clears `Forward`; entries whose document no longer exists are skipped. |

## Persisted state

See [contracts/state-files.md](./contracts/state-files.md).

| Entity | Fields |
|---|---|
| `WindowState` | `X`, `Y`, `Width`, `Height`, `IsMaximized`, `ScreenBounds` (of the screen it was on) |
| `LayoutState` | `SchemaVersion`, `Engine` (`dock` or `grid`), `DockLayout` (opaque JSON from the adapter) |
| `RecentProjects` | `Entries: [{ Path, DisplayName, LastOpenedUtc, Pinned }]` |
| `SessionState` | `OpenDocuments: [DocumentId]`, `ActiveDocument`, `Viewports: { DocumentId: { X, Y, Zoom } }` |
| Theme preference | `EditorSettings.Theme` (`Dark`, `Light`, `System`) in `settings.json` through the P1 settings API |

## Core model changes (`NetPrints.Core`)

| Entity | Change | Rules |
|---|---|---|
| `EventGraph` | `Name` becomes settable through an undoable rename | Unique among the class's event graphs; not emitted in C#. |
| `EventEntryNode` (custom event) | `Name` editable; new `Arguments: IList<EventArgument>` (`Name`, `Type: TypeSpecifier`) mapped to output pins | Name unique among the class's methods and entries (P1 FR-027); argument names unique and valid C# identifiers; the generated method signature follows the order; serialized in the graph JSON (schema stays v1: additive optional property, readers ignore unknown properties). |
| `EventEntryNode` (override) | unchanged; the inspector shows the base signature read-only | — |

Validation errors surface as inspector messages, and the change is refused. They are never thrown into the view.
