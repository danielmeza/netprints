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
}
```

- Every `Add*` validates its descriptor and records the owner (`netprints` for built-ins). After `Freeze()`, which
  the shell calls on startup, `Add*` throws `InvalidOperationException`.
- A duplicate id, within one kind, is recorded as `DuplicateId` and ignored: the first registration wins.
- Two commands whose gestures overlap in the same scope are recorded as `GestureConflict`, and only the first keeps
  the gesture. `Global` overlaps every scope.
- An invalid descriptor is recorded as `InvalidDescriptor` and ignored. Examples: an empty label, a bad id, or a
  single-key gesture in `Global` scope.
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

public enum CommandScope { Global, Graph, ProjectTree }

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
- `CommandContext` carries `IShell`, the project session (or null), the active document, the selection and an
  optional parameter.
- The exact member names may change in implementation if the tests and this contract change together. The
  invariants may not: UI-free types, first-wins conflicts, the seven kinds, and a frozen registry after startup.

## 3. Surfaces generated from the registry

| Surface | Reads |
|---|---|
| Menu bar | commands with `Menu`, ordered by path order (File, Edit, View, Go, Build, Help), then group, then `Order` |
| Command bar | commands with `CommandBarOrder` |
| Key bindings | every command's gestures, by scope (shell `KeyBindings` for `Global`; the tunnel behavior on the canvas for `Graph`; the tree for `ProjectTree`) |
| Command palette, keyboard shortcuts sheet | every command, including disabled ones |
| Context menus | context-menu items by target, each resolving its command |
| Tooltips | tooltip providers in `Order`; the first non-null content wins |
| Go to anything | every go-to provider, with results grouped by `Kind` |
| Start page, New project | dashboard tiles and project templates |

## 4. Tests that pin this contract

- Registry unit tests: validation, duplicates, gesture conflicts, freeze.
- `RegistrySurfaceTests` (headless): every command appears in the menu (unless it is menu-less by design, such as
  `cancel`), the palette and the shortcuts sheet. Every gesture produces exactly one binding.
- A test contribution of each kind appears in its surface (US4 scenario 6).
