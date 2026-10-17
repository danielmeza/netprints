using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetPrints.Editor.Commands;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Contributions;

/// <summary>The default <see cref="IContributionRegistry"/>.</summary>
/// <param name="logger">Receives one warning per issue.</param>
[Singleton<IContributionRegistry>(Factory = "Create")]
public sealed class ContributionRegistry(ILogger<ContributionRegistry> logger) : IContributionRegistry
{
    /// <summary>Creates the frozen registry of the built-in contributions and the go-to providers.</summary>
    /// <param name="logger">Receives one warning per issue.</param>
    /// <param name="services">Resolves the shell lazily, for the session the go-to providers read.</param>
    /// <returns>The frozen registry.</returns>
    internal static ContributionRegistry Create(ILogger<ContributionRegistry> logger, IServiceProvider services)
    {
        var registry = new ContributionRegistry(logger);
        BuiltInContributions.Register(registry);
        GoToProviderContributions.Register(registry, () => services.GetService<ShellHost>()?.Shell.Session);
        registry.Freeze();
        return registry;
    }

    private const int UnlistedPanelOrder = 100;

    private readonly List<CommandDescriptor> commands = [];
    private readonly List<PanelDescriptor> panels = [];
    private readonly List<DashboardTileDescriptor> tiles = [];
    private readonly List<StatusBarItemDescriptor> statusBarItems = [];
    private readonly List<DialogDescriptor> dialogs = [];
    private readonly List<ProjectTemplateDescriptor> templates = [];
    private readonly List<ContextMenuItemDescriptor> menuItems = [];
    private readonly List<ITooltipProvider> tooltipProviders = [];
    private readonly List<IGoToProvider> goToProviders = [];
    private readonly List<ContributionIssue> issues = [];
    private readonly List<ViewResourceDeclaration> viewResources = [];
    private readonly Dictionary<string, string> owners = new(StringComparer.Ordinal);
    private string currentOwner = ContributionIds.Owner;

    /// <inheritdoc/>
    public IReadOnlyList<ViewResourceDeclaration> ViewResources => viewResources;

    /// <summary>Gets the editor facet of one extension: what it adds is recorded as owned by <paramref name="owner"/>.</summary>
    /// <param name="owner">The extension id.</param>
    /// <returns>The facet.</returns>
    public IEditorContributions For(string owner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        return new OwnerFacet(this, owner);
    }

    /// <inheritdoc/>
    public string? OwnerOf(string id) => owners.GetValueOrDefault(id);

    private void AddViewResources(string owner, string resourceUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceUri);
        ThrowIfFrozen();
        viewResources.Add(new ViewResourceDeclaration(owner, resourceUri));
    }

    private void As(string owner, Action add)
    {
        string previous = currentOwner;
        currentOwner = owner;
        try
        {
            add();
        }
        finally
        {
            currentOwner = previous;
        }
    }

    private sealed class OwnerFacet(ContributionRegistry registry, string owner) : IEditorContributions
    {
        public IEditorContributions AddCommand(CommandDescriptor command) => Forward(() => registry.AddCommand(command));

        public IEditorContributions AddPanel(PanelDescriptor panel) => Forward(() => registry.AddPanel(panel));

        public IEditorContributions AddDashboardTile(DashboardTileDescriptor tile) => Forward(() => registry.AddDashboardTile(tile));

        public IEditorContributions AddContextMenuItem(ContextMenuItemDescriptor item) => Forward(() => registry.AddContextMenuItem(item));

        public IEditorContributions AddTooltipProvider(ITooltipProvider provider) => Forward(() => registry.AddTooltipProvider(provider));

        public IEditorContributions AddGoToProvider(IGoToProvider provider) => Forward(() => registry.AddGoToProvider(provider));

        public IEditorContributions AddStatusBarItem(StatusBarItemDescriptor item) => Forward(() => registry.AddStatusBarItem(item));

        public IEditorContributions AddDialog(DialogDescriptor dialog) => Forward(() => registry.AddDialog(dialog));

        public IEditorContributions AddViewResources(string resourceUri) => Forward(() => registry.AddViewResources(owner, resourceUri));

        private OwnerFacet Forward(Action add)
        {
            registry.As(owner, add);
            return this;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<CommandDescriptor> Commands => commands;

    /// <inheritdoc/>
    public IReadOnlyList<PanelDescriptor> Panels => panels;

    /// <inheritdoc/>
    public IReadOnlyList<DashboardTileDescriptor> DashboardTiles => tiles;

    /// <inheritdoc/>
    public IReadOnlyList<StatusBarItemDescriptor> StatusBarItems => statusBarItems;

    /// <inheritdoc/>
    public IReadOnlyList<DialogDescriptor> Dialogs => dialogs;

    /// <inheritdoc/>
    public IReadOnlyList<ProjectTemplateDescriptor> ProjectTemplates => templates;

    /// <inheritdoc/>
    public IReadOnlyList<ContextMenuItemDescriptor> ContextMenuItems => menuItems;

    /// <inheritdoc/>
    public IReadOnlyList<ITooltipProvider> TooltipProviders => tooltipProviders;

    /// <inheritdoc/>
    public IReadOnlyList<IGoToProvider> GoToProviders => goToProviders;

    /// <inheritdoc/>
    public IReadOnlyList<ContributionIssue> Issues => issues;

    /// <inheritdoc/>
    public bool IsFrozen { get; private set; }

    /// <inheritdoc/>
    public void AddCommand(CommandDescriptor command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ThrowIfFrozen();
        if (!TryAccept(command.Id, !IsBlank(command.Label.Text) && command.Handler is not null, "a label and a handler", commands.Select(c => c.Id)))
        {
            return;
        }

        var gestures = new List<CommandGesture>();
        foreach (CommandGesture gesture in command.DefaultGestures ?? [])
        {
            if (gesture is null)
            {
                Report(ContributionIssueKind.InvalidDescriptor, command.Id, "A default gesture is null.");
                return;
            }

            if (gesture.IsPlain && !gesture.IsFunctionKey && command.Scope == CommandScope.Global)
            {
                Report(ContributionIssueKind.InvalidDescriptor, command.Id, $"Single-key or Shift-only gesture '{gesture}' is not allowed in the Global scope (function keys are).");
                return;
            }

            if (!gestures.Contains(gesture))
            {
                gestures.Add(gesture);
            }
        }

        var kept = new List<CommandGesture>(gestures.Count);
        foreach (CommandGesture gesture in gestures)
        {
            var owner = commands.FirstOrDefault(c => ScopesOverlap(c.Scope, command.Scope) && (c.DefaultGestures ?? []).Contains(gesture));
            if (owner is null)
            {
                kept.Add(gesture);
            }
            else
            {
                Report(ContributionIssueKind.GestureConflict, command.Id, $"Gesture '{gesture}' is already bound to '{owner.Id}'; the first registration keeps it.");
            }
        }

        commands.Add(command.DefaultGestures is null || kept.Count == command.DefaultGestures.Count ? command : command with { DefaultGestures = kept });
    }

    /// <inheritdoc/>
    public void AddPanel(PanelDescriptor panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        ThrowIfFrozen();
        if (TryAccept(panel.Id, !IsBlank(panel.Title.Text) && panel.CreateViewModel is not null, "a title and a view model factory", panels.Select(p => p.Id)))
        {
            panels.Add(panel);
        }
    }

    /// <inheritdoc/>
    public void AddDashboardTile(DashboardTileDescriptor tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        ThrowIfFrozen();
        if (TryAccept(tile.Id, !IsBlank(tile.Title) && tile.CreateViewModel is not null, "a title and a view model factory", tiles.Select(t => t.Id)))
        {
            tiles.Add(tile);
        }
    }

    /// <summary>Adds a status bar item; an invalid or duplicate one is ignored.</summary>
    /// <param name="item">The item.</param>
    /// <exception cref="InvalidOperationException">The registry is frozen.</exception>
    public void AddStatusBarItem(StatusBarItemDescriptor item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ThrowIfFrozen();
        if (TryAccept(item.Id, item.CreateViewModel is not null, "a view model factory", statusBarItems.Select(s => s.Id)))
        {
            statusBarItems.Add(item);
        }
    }

    /// <summary>Adds a dialog; an invalid or duplicate one is ignored.</summary>
    /// <param name="dialog">The dialog.</param>
    /// <exception cref="InvalidOperationException">The registry is frozen.</exception>
    public void AddDialog(DialogDescriptor dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ThrowIfFrozen();
        if (TryAccept(dialog.Id, !IsBlank(dialog.Title) && dialog.CreateViewModel is not null, "a title and a view model factory", dialogs.Select(d => d.Id)))
        {
            dialogs.Add(dialog);
        }
    }

    /// <inheritdoc/>
    public void AddProjectTemplate(ProjectTemplateDescriptor template)
    {
        ArgumentNullException.ThrowIfNull(template);
        ThrowIfFrozen();
        if (TryAccept(template.Id, !IsBlank(template.DisplayName) && !IsBlank(template.ProfileId), "a display name and a profile id", templates.Select(t => t.Id)))
        {
            templates.Add(template);
        }
    }

    /// <inheritdoc/>
    public void AddContextMenuItem(ContextMenuItemDescriptor item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ThrowIfFrozen();
        if (!ContextMenuTargets.HasSurface(item.Target))
        {
            Report(ContributionIssueKind.InvalidDescriptor, item.Id, $"No view opens a {item.Target} context menu yet, so the item would never be shown.");
        }
        else if (TryAccept(item.Id, ContributionIds.IsValid(item.CommandId) && item.Group is not null, "a valid command id and a group", menuItems.Select(m => m.Id)))
        {
            menuItems.Add(item);
        }
    }

    /// <inheritdoc/>
    public void AddTooltipProvider(ITooltipProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ThrowIfFrozen();
        tooltipProviders.Add(provider);
    }

    /// <inheritdoc/>
    public void AddGoToProvider(IGoToProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ThrowIfFrozen();
        goToProviders.Add(provider);
    }

    /// <inheritdoc/>
    public void Freeze()
    {
        if (IsFrozen)
        {
            return;
        }

        int index = 0;
        foreach (PanelDescriptor panel in panels.ToArray())
        {
            if (!commands.Exists(command => command.Handler is ShowPanelCommandHandler { PanelId: var shown } && shown == panel.Id))
            {
                As(owners.GetValueOrDefault(panel.Id, currentOwner), () => AddCommand(new CommandDescriptor(
                    ContributionIds.CommandPrefix + "showPanel." + panel.Id,
                    panel.Title,
                    new ShowPanelCommandHandler(panel.Id),
                    IconId: panel.IconId,
                    Menu: new MenuPlacement(ViewContributions.MenuName, ViewContributions.PanelsGroup, UnlistedPanelOrder + index++))));
            }
        }

        IsFrozen = true;
    }

    private static bool IsBlank(string? text) => string.IsNullOrWhiteSpace(text);

    private static bool ScopesOverlap(CommandScope a, CommandScope b) =>
        a == CommandScope.Global || b == CommandScope.Global || (a & b) != 0;

    private void ThrowIfFrozen()
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException("The contribution registry is frozen.");
        }
    }

    private bool TryAccept(string? id, bool hasRequiredFields, string required, IEnumerable<string> existingIds)
    {
        if (!ContributionIds.IsValid(id))
        {
            Report(ContributionIssueKind.InvalidDescriptor, id ?? string.Empty, "The id does not match ^[a-z0-9]+(\\.[a-zA-Z0-9]+)+$.");
            return false;
        }

        if (!hasRequiredFields)
        {
            Report(ContributionIssueKind.InvalidDescriptor, id, $"The descriptor needs {required}.");
            return false;
        }

        if (existingIds.Contains(id, StringComparer.Ordinal))
        {
            Report(ContributionIssueKind.DuplicateId, id, "The id is already registered for this kind; the first registration wins.");
            return false;
        }

        owners[id] = currentOwner;
        return true;
    }

    private void Report(ContributionIssueKind kind, string id, string message)
    {
        issues.Add(new ContributionIssue(kind, id, currentOwner, message));
        Log.ContributionIssue(logger, kind, id, currentOwner, message);
    }
}
