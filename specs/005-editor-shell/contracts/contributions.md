# Contract: the contribution registry (editor-internal in P3a)

ADR-0020. Namespace `NetPrints.Editor.Contributions`. The types are `public` inside `NetPrints.Editor`, which is not a
published package. P3 moves them unchanged into the public extension surface. Every type here is free of Avalonia
and Dock types.

## 1. Registry

```csharp
public interface IContributionRegistry
{
    void AddCommand(CommandDescriptor command);
    void AddPanel(PanelDescriptor panel);
    void AddDashboardTile(DashboardTileDescriptor tile);
    void AddProjectTemplate(ProjectTemplateDescriptor template);
    void AddContextMenuItem(ContextMenuItemDescriptor item);
    void AddTooltipProvider(ITooltipProvider provider);
    void AddGoToProvider(IGoToProvider provider);

    IReadOnlyList<CommandDescriptor> Commands { get; }      // registration order
    IReadOnlyList<PanelDescriptor> Panels { get; }
    IReadOnlyList<DashboardTileDescriptor> DashboardTiles { get; }
    IReadOnlyList<ProjectTemplateDescriptor> ProjectTemplates { get; }
    IReadOnlyList<ContextMenuItemDescriptor> ContextMenuItems { get; }
    IReadOnlyList<ITooltipProvider> TooltipProviders { get; }
    IReadOnlyList<IGoToProvider> GoToProviders { get; }
    IReadOnlyList<ContributionIssue> Issues { get; }
    bool IsFrozen { get; }
    void Freeze();
}
```

- Every `Add*` validates its descriptor and records the owner (`netprints` for built-ins). After `Freeze()`, which
  the shell calls on startup, `Add*` throws `InvalidOperationException`.
- A duplicate id, within one kind, is recorded as `DuplicateId` and ignored: the first registration wins.
- Two commands whose gestures overlap in the same scope are recorded as `GestureConflict`, and only the first keeps
  the gesture. `Global` overlaps every scope, and two scope sets overlap when they share a scope.
- An invalid descriptor is recorded as `InvalidDescriptor` and ignored. Examples: a bad id, a blank label (command),
  title (panel, tile) or display name (template), a missing handler or view model factory, a blank template profile
  id, an invalid `CommandId` or a null group in a context-menu item, an unparseable gesture, or a single-key gesture
  in `Global` scope (function keys F1 to F24 are exempt, as F5, F7 and Shift+F5 are Global).
- Each issue is logged at warning level through a `[LoggerMessage]` method.

## 2. Descriptors

```csharp
public sealed record CommandDescriptor(
    string Id, string Label, ICommandHandler Handler,
    string? IconKind = null,                 // Material icon kind name
    IReadOnlyList<string>? DefaultGestures = null, // "Ctrl+Shift+B"; parsed by the view layer
    CommandScope Scope = CommandScope.Global,
    MenuPlacement? Menu = null,              // Path ("Build"), Group ("build"), Order
    int? CommandBarOrder = null);

[Flags] public enum CommandScope { Global = 0, Graph = 1, ProjectTree = 2 }   // a command may name several: Graph | ProjectTree
// Global is 0 and overlaps every scope; two scope sets overlap when they share a flag.

public sealed record CommandGesture            // UI-free parse of "Ctrl+Shift+B": Modifiers (CommandModifiers flags), Key,
{ static bool TryParse(string? text, out CommandGesture gesture); bool IsSingleKey; bool IsFunctionKey; }   // IsFunctionKey: F1..F24

public interface ICommandHandler
{
    bool CanExecute(CommandContext context);
    Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken);
    string? DynamicLabel(CommandContext context) => null;
}

public sealed record PanelDescriptor(string Id, string Title, Func<IServiceProvider, object> CreateViewModel,
    PanelDock DefaultDock, int Order, string? IconKind = null);

public sealed record DashboardTileDescriptor(string Id, string Title, int Order,
    Func<IServiceProvider, object> CreateViewModel);

public sealed record ProjectTemplateDescriptor(string Id, string DisplayName, string Description,
    string ProfileId, ProjectOutputType OutputType, string? IconKind = null);

public sealed record ContextMenuItemDescriptor(string Id, ContextMenuTarget Target, string CommandId,
    string Group, int Order);

public interface ITooltipProvider { int Order { get; } TooltipContent? TryProvide(TooltipTarget target); }

public interface IGoToProvider
{
    string Kind { get; }                     // "Graphs", "Nodes", "Variables", "Methods", "Commands"
    IAsyncEnumerable<GoToItem> SearchAsync(string text, CancellationToken cancellationToken);
}
```

- Ids match `^[a-z0-9]+(\.[a-zA-Z0-9]+)+$`. Built-ins use the prefixes `netprints.command.`, `netprints.panel.`,
  `netprints.tile.`, `netprints.template.`, `netprints.menu.`, `netprints.tooltip.` and `netprints.goto.`.
- `CreateViewModel` returns a view model whose view the shell finds by `DataTemplate x:DataType` (ADR-0007 D6).
- `CommandContext(IShell Shell, ProjectSessionViewModel? Session, DocumentId? ActiveDocument, NodeGraphViewModel?
  ActiveGraph, CommandSelection Selection, object? Parameter)` is built per invocation by an
  `ICommandContextProvider`; it holds no UI types.
- `IShell.ProjectActions` is an `IProjectActions`, the seam for the project flows (open, new, close, exit, project
  settings, references, class settings, add method, constructor, variable and event graph, tree rename and delete).
  `UnloadingCommandHandler` is the base of `openProject`, `newProject`, `closeProject` and `exit`: with a project
  open it calls `IProjectActions.ConfirmUnloadAsync` first and stops when that answers false.
- Handlers are stateless and have no `CanExecuteChanged`. The enabled state is queried when a command is invoked (a
  key press) or a surface is built. A surface that stays visible re-queries on the session's change notifications;
  `run` and `stop` depend on `ProjectSessionViewModel.IsRunning`, which raises `PropertyChanged` when the program
  starts or exits.
- The exact member names may change in implementation if the tests and this contract change together. The
  invariants may not: UI-free types, first-wins conflicts, the seven kinds, and a frozen registry after startup.

## 3. Surfaces generated from the registry

| Surface | Reads |
|---|---|
| Menu bar | commands with `Menu`, ordered by path order (File, Edit, View, Go, Build, Help), then group, then `Order` |
| Command bar | commands with `CommandBarOrder` |
| Key bindings | every command's gestures, by scope: `CommandKeyBindingsBehavior` adds window `KeyBindings` for `Global`; `ScopedCommandKeysBehavior` is a tunnel handler on the canvas for `Graph` and on the tree for `ProjectTree`, and leaves a key typed in a text box, check box or combo box to that control. Both run commands through `CommandInvoker`, which builds a fresh context and queries `CanExecute` at invocation; the view layer parses the gesture strings into `KeyGesture`s |
| Command palette, keyboard shortcuts sheet | every command, including disabled ones |
| Context menus | context-menu items by target, each resolving its command; an item whose command cannot execute for the target (for example `rename` on a constructor) is hidden, not shown disabled |
| Tooltips | tooltip providers in `Order`; the first non-null content wins |
| Go to anything | every go-to provider, with results grouped by `Kind` |
| Start page, New project | dashboard tiles and project templates |

## 4. Tests that pin this contract

- Registry unit tests: validation, duplicates, gesture conflicts, freeze.
- `RegistrySurfaceTests` (headless): every command appears in the menu (unless it is menu-less by design, such as
  `cancel`), the palette and the shortcuts sheet. Every gesture produces exactly one binding.
- A test contribution of each kind appears in its surface (US4 scenario 6).
