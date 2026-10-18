using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetPrints.Editor.Commands;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.State;

namespace NetPrints.Editor.Contributions;

/// <summary>The default <see cref="IContributionRegistry"/>.</summary>
/// <param name="logger">Receives one warning per issue.</param>
[Singleton<IContributionRegistry>(Factory = "Create")]
public sealed class ContributionRegistry(ILogger<ContributionRegistry> logger) : IContributionRegistry
{
    /// <summary>Creates the registry of the built-in contributions and the go-to providers.</summary>
    /// <param name="logger">Receives one warning per issue.</param>
    /// <param name="services">Resolves the shell lazily, for the session the go-to providers read.</param>
    /// <returns>The registry, its initial registration ended.</returns>
    internal static ContributionRegistry Create(ILogger<ContributionRegistry> logger, IServiceProvider services)
    {
        var registry = new ContributionRegistry(logger);
        BuiltInContributions.Register(registry);
        GoToProviderContributions.Register(registry, () => services.GetService<ShellHost>()?.Shell.Session);
        services.GetService<ExtensionContributionRecorder>()?.ApplyToAppGroup(registry);
        if (services.GetService<EditorHostServices>() is { } host)
        {
            registry.SetUserBindings(host.Settings.Get(KeyboardSettings.Descriptor));
        }

        registry.Freeze();
        return registry;
    }

    private const int UnlistedPanelOrder = 100;

    private readonly ContributionGroup appGroup = new(null);
    private readonly List<ContributionGroup> sessions = [];
    private ContributionGroup? target;
    private readonly ContributionGuard guard = new(logger);
    private readonly HashSet<string> loggedReferences = new(StringComparer.Ordinal);
    private ContributionSnapshot? snapshot;
    private KeyboardSettings userBindings = new();
    private bool published;
    private int batching;
    private string currentOwner = ContributionIds.Owner;

    /// <inheritdoc/>
    public event EventHandler<ContributionSnapshot>? Changed;

    /// <inheritdoc/>
    public ContributionSnapshot Snapshot
    {
        get
        {
            if (snapshot is null)
            {
                snapshot = new ContributionSnapshot(AllGroups(), userBindings);
                LogUnknownReferences(snapshot);
            }

            return snapshot;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<ViewResourceDeclaration> ViewResources => Snapshot.ViewResources;

    /// <inheritdoc/>
    public IReadOnlyList<CommandDescriptor> Commands => Snapshot.Commands;

    /// <inheritdoc/>
    public IReadOnlyList<PanelDescriptor> Panels => Snapshot.Panels;

    /// <inheritdoc/>
    public IReadOnlyList<DashboardTileDescriptor> DashboardTiles => Snapshot.DashboardTiles;

    /// <inheritdoc/>
    public IReadOnlyList<StatusBarItemDescriptor> StatusBarItems => Snapshot.StatusBarItems;

    /// <inheritdoc/>
    public IReadOnlyList<DialogDescriptor> Dialogs => Snapshot.Dialogs;

    /// <inheritdoc/>
    public IReadOnlyList<SettingsPageDescriptor> SettingsPages => Snapshot.SettingsPages;

    /// <inheritdoc/>
    public IReadOnlyList<ProjectTemplateDescriptor> ProjectTemplates => Snapshot.ProjectTemplates;

    /// <inheritdoc/>
    public IReadOnlyList<ContextMenuItemDescriptor> ContextMenuItems => Snapshot.ContextMenuItems;

    /// <inheritdoc/>
    public IReadOnlyList<ITooltipProvider> TooltipProviders => Snapshot.TooltipProviders;

    /// <inheritdoc/>
    public IReadOnlyList<IGoToProvider> GoToProviders => Snapshot.GoToProviders;

    /// <inheritdoc/>
    public IReadOnlyList<ContributionIssue> Issues => Snapshot.Issues;

    private ContributionGroup Target => target ?? appGroup;

    /// <summary>Gets the editor facet of one extension in the app group: what it adds is recorded as owned by <paramref name="owner"/>.</summary>
    /// <param name="owner">The extension id.</param>
    /// <param name="ownerName">The extension's <c>name</c>, shown beside its items; <see langword="null"/> to show the id.</param>
    /// <returns>The facet.</returns>
    public IEditorContributions For(string owner, string? ownerName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        return new OwnerFacet(this, appGroup, owner, ownerName);
    }

    /// <inheritdoc/>
    public string? OwnerOf(string id) => Snapshot.OwnerOf(id);

    /// <inheritdoc/>
    public string? OwnerNameOf(string id) => Snapshot.OwnerNameOf(id);

    /// <inheritdoc/>
    public void AddSessionGroup(string key, Action<IContributionGroupBuilder> populate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(populate);
        if (sessions.Exists(group => group.Key == key))
        {
            throw new InvalidOperationException($"A session group with the key '{key}' exists.");
        }

        var group = new ContributionGroup(key);
        sessions.Add(group);
        snapshot = null;
        bool filled = false;
        batching++;
        try
        {
            populate(new SessionBuilder(this, group));
            As(group, currentOwner, () => AddShowPanelCommands(group));
            filled = true;
        }
        finally
        {
            batching--;
            if (!filled)
            {
                sessions.Remove(group);
                snapshot = null;
            }
        }

        Publish(always: true);
    }

    /// <inheritdoc/>
    public bool IsRecordingGestures { get; set; }

    /// <summary>Sets the user's key bindings; the commands of the next snapshot carry the effective gestures and <see cref="Changed"/> is raised.</summary>
    /// <param name="bindings">The <c>keyboard</c> section of the settings.</param>
    public void SetUserBindings(KeyboardSettings bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        userBindings = bindings;
        Publish();
    }

    /// <inheritdoc/>
    public bool RemoveSessionGroup(string key)
    {
        int index = sessions.FindIndex(group => group.Key == key);
        if (index < 0)
        {
            return false;
        }

        sessions.RemoveAt(index);
        Publish(always: true);
        return true;
    }

    /// <inheritdoc/>
    public void AddCommand(CommandDescriptor command)
    {
        ArgumentNullException.ThrowIfNull(command);
        AcceptCommand(command);
        Publish();
    }

    /// <inheritdoc/>
    public void AddPanel(PanelDescriptor panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        if (TryAccept(panel.Id, !IsBlank(panel.Title.Text) && panel.CreateViewModel is not null, "a title and a view model factory", Ids(group => group.Panels.Select(p => p.Id))))
        {
            Target.Panels.Add(FromExtension && panel.CreateViewModel is { } create ? panel with { CreateViewModel = guard.Factory(create, "the panel " + panel.Id, OwnerName()) } : panel);
            if (published && Target == appGroup)
            {
                AddShowPanelCommands(appGroup);
            }
        }

        Publish();
    }

    /// <inheritdoc/>
    public void AddDashboardTile(DashboardTileDescriptor tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        if (TryAccept(tile.Id, !IsBlank(tile.Title.Text) && tile.CreateViewModel is not null, "a title and a view model factory", Ids(group => group.Tiles.Select(t => t.Id))))
        {
            Target.Tiles.Add(FromExtension && tile.CreateViewModel is { } create ? tile with { CreateViewModel = guard.Factory(create, "the tile " + tile.Id, OwnerName()) } : tile);
        }

        Publish();
    }

    /// <summary>Adds a status bar item; an invalid or duplicate one is ignored.</summary>
    /// <param name="item">The item.</param>
    public void AddStatusBarItem(StatusBarItemDescriptor item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (TryAccept(item.Id, item.CreateViewModel is not null, "a view model factory", Ids(group => group.StatusBarItems.Select(s => s.Id))))
        {
            Target.StatusBarItems.Add(FromExtension && item.CreateViewModel is { } create
                ? item with
                {
                    CreateViewModel = guard.Factory(create, "the status bar item " + item.Id, OwnerName()),
                    AppliesTo = guard.Predicate(item.AppliesTo, "the status bar item " + item.Id, OwnerName()),
                }
                : item);
        }

        Publish();
    }

    /// <summary>Adds a dialog; an invalid or duplicate one is ignored.</summary>
    /// <param name="dialog">The dialog.</param>
    public void AddDialog(DialogDescriptor dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        if (TryAccept(dialog.Id, !IsBlank(dialog.Title.Text) && dialog.CreateViewModel is not null, "a title and a view model factory", Ids(group => group.Dialogs.Select(d => d.Id))))
        {
            Target.Dialogs.Add(FromExtension && dialog.CreateViewModel is { } create ? dialog with { CreateViewModel = guard.Factory(create, "the dialog " + dialog.Id, OwnerName()) } : dialog);
        }

        Publish();
    }

    /// <summary>Adds a settings page; an invalid or duplicate one is ignored.</summary>
    /// <param name="page">The page.</param>
    public void AddSettingsPage(SettingsPageDescriptor page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (TryAccept(page.Id, !IsBlank(page.Title.Text), "a title", Ids(group => group.SettingsPages.Select(p => p.Id))))
        {
            Target.SettingsPages.Add(FromExtension && page.CreateViewModel is { } create
                ? page with { CreateViewModel = guard.Factory(create, "the settings page " + page.Id, OwnerName()) }
                : page);
        }

        Publish();
    }

    /// <inheritdoc/>
    public void AddProjectTemplate(ProjectTemplateDescriptor template)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (TryAccept(template.Id, !IsBlank(template.DisplayName.Text) && !IsBlank(template.ProfileId), "a display name and a profile id", Ids(group => group.Templates.Select(t => t.Id))))
        {
            Target.Templates.Add(template);
        }

        Publish();
    }

    /// <inheritdoc/>
    public void AddContextMenuItem(ContextMenuItemDescriptor item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!ContextMenuTargets.HasSurface(item.Target))
        {
            Report(ContributionIssueKind.InvalidDescriptor, item.Id, $"No view opens a {item.Target} context menu yet, so the item would never be shown.");
        }
        else if (TryAccept(item.Id, ContributionIds.IsValid(item.CommandId) && item.Group is not null, "a valid command id and a group", Ids(group => group.MenuItems.Select(m => m.Id))))
        {
            Target.MenuItems.Add(FromExtension ? item with { AppliesTo = guard.Predicate(item.AppliesTo, "the context menu item " + item.Id, OwnerName()) } : item);
        }

        Publish();
    }

    /// <inheritdoc/>
    public void AddTooltipProvider(ITooltipProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        Target.TooltipProviders.Add(FromExtension ? guard.Tooltips(provider, OwnerName()) : provider);
        Publish();
    }

    /// <inheritdoc/>
    public void AddGoToProvider(IGoToProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        Target.GoToProviders.Add(FromExtension ? guard.GoTo(provider, OwnerName()) : provider);
        Publish();
    }

    /// <inheritdoc/>
    public void Freeze()
    {
        if (published)
        {
            return;
        }

        batching++;
        try
        {
            As(appGroup, currentOwner, () => AddShowPanelCommands(appGroup));
        }
        finally
        {
            batching--;
        }

        published = true;
        Publish();
    }

    private static bool IsBlank(string? text) => string.IsNullOrWhiteSpace(text);

    private static bool ScopesOverlap(CommandScope a, CommandScope b) =>
        a == CommandScope.Global || b == CommandScope.Global || (a & b) != 0;

    private List<ContributionGroup> AllGroups() => [appGroup, .. sessions];

    private IEnumerable<string> Ids(Func<ContributionGroup, IEnumerable<string>> select) => AllGroups().SelectMany(select);

    private void Publish(bool always = false)
    {
        snapshot = null;
        if (batching == 0 && (always || published))
        {
            Changed?.Invoke(this, Snapshot);
        }
    }

    private void AddViewResources(string owner, string resourceUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceUri);
        Target.ViewResources.Add(new ViewResourceDeclaration(owner, resourceUri));
        Publish();
    }

    private void As(ContributionGroup group, string owner, Action add)
    {
        ContributionGroup? previousTarget = target;
        string previousOwner = currentOwner;
        target = group;
        currentOwner = owner;
        try
        {
            add();
        }
        finally
        {
            target = previousTarget;
            currentOwner = previousOwner;
        }
    }

    private void AcceptCommand(CommandDescriptor command)
    {
        if (!TryAccept(command.Id, !IsBlank(command.Label.Text) && command.Handler is not null, "a label and a handler", Ids(group => group.Commands.Select(c => c.Id))))
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
        CommandDescriptor[] existing = [.. AllGroups().SelectMany(group => group.Commands)];
        foreach (CommandGesture gesture in gestures)
        {
            var owner = existing.FirstOrDefault(c => ScopesOverlap(c.Scope, command.Scope) && (c.DefaultGestures ?? []).Contains(gesture));
            if (owner is null)
            {
                kept.Add(gesture);
            }
            else
            {
                Report(ContributionIssueKind.GestureConflict, command.Id, $"Gesture '{gesture}' is already bound to '{owner.Id}'; the first registration keeps it.");
            }
        }

        CommandDescriptor accepted = command.DefaultGestures is null || kept.Count == command.DefaultGestures.Count ? command : command with { DefaultGestures = kept };
        if (FromExtension && accepted.Handler is not IEditorCommandHandler)
        {
            accepted = accepted with { Handler = guard.Command(accepted.Handler, accepted.Id, OwnerName()) };
        }

        Target.Commands.Add(accepted);
    }

    private bool FromExtension => currentOwner != ContributionIds.Owner && currentOwner != BuiltInContributions.OwnerId;

    private Func<string> OwnerName()
    {
        ContributionGroup group = Target;
        string owner = currentOwner;
        return () => group.OwnerNames.GetValueOrDefault(owner, owner);
    }

    private void LogUnknownReferences(ContributionSnapshot built)
    {
        if (!published)
        {
            return;
        }

        foreach (ContributionIssue issue in built.UnknownReferences)
        {
            if (issue.Owner != ContributionIds.Owner && issue.Owner != BuiltInContributions.OwnerId && loggedReferences.Add(issue.Owner + "|" + issue.Id))
            {
                Log.ContributionIssue(logger, issue.Kind, issue.Id, issue.Owner, issue.Message);
            }
        }
    }

    private void AddShowPanelCommands(ContributionGroup group)
    {
        CommandDescriptor[] commands = [.. AllGroups().SelectMany(g => g.Commands)];
        int index = commands.Count(command => command.Handler is ShowPanelCommandHandler);
        foreach (PanelDescriptor panel in group.Panels.ToArray())
        {
            if (!commands.Any(command => command.Handler is ShowPanelCommandHandler { PanelId: var shown } && shown == panel.Id))
            {
                As(group, group.Owners.GetValueOrDefault(panel.Id, currentOwner), () => AcceptCommand(new CommandDescriptor(
                    ContributionIds.CommandPrefix + "showPanel." + panel.Id,
                    panel.Title,
                    new ShowPanelCommandHandler(panel.Id),
                    IconId: panel.IconId,
                    Menu: new MenuPlacement(ViewContributions.MenuName, ViewContributions.PanelsGroup, UnlistedPanelOrder + index++))));
            }
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

        Target.Owners[id] = currentOwner;
        return true;
    }

    private void Report(ContributionIssueKind kind, string id, string message)
    {
        Target.Issues.Add(new ContributionIssue(kind, id, currentOwner, message));
        Log.ContributionIssue(logger, kind, id, currentOwner, message);
    }

    private sealed class SessionBuilder(ContributionRegistry registry, ContributionGroup group) : IContributionGroupBuilder
    {
        public IEditorContributions For(string owner, string? ownerName = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(owner);
            return new OwnerFacet(registry, group, owner, ownerName);
        }

        public void AddProjectTemplate(ProjectTemplateDescriptor template, string? owner = null) =>
            registry.As(group, owner ?? ContributionIds.Owner, () => registry.AddProjectTemplate(template));
    }

    private sealed class OwnerFacet : IEditorContributions
    {
        private readonly ContributionRegistry registry;
        private readonly ContributionGroup group;
        private readonly string owner;

        public OwnerFacet(ContributionRegistry registry, ContributionGroup group, string owner, string? ownerName)
        {
            this.registry = registry;
            this.group = group;
            this.owner = owner;
            if (ownerName is not null)
            {
                group.OwnerNames[owner] = ownerName;
                registry.snapshot = null;
            }
        }

        public IEditorContributions AddCommand(CommandDescriptor command) => Forward(() => registry.AddCommand(command));

        public IEditorContributions AddPanel(PanelDescriptor panel) => Forward(() => registry.AddPanel(panel));

        public IEditorContributions AddDashboardTile(DashboardTileDescriptor tile) => Forward(() => registry.AddDashboardTile(tile));

        public IEditorContributions AddContextMenuItem(ContextMenuItemDescriptor item) => Forward(() => registry.AddContextMenuItem(item));

        public IEditorContributions AddTooltipProvider(ITooltipProvider provider) => Forward(() => registry.AddTooltipProvider(provider));

        public IEditorContributions AddGoToProvider(IGoToProvider provider) => Forward(() => registry.AddGoToProvider(provider));

        public IEditorContributions AddStatusBarItem(StatusBarItemDescriptor item) => Forward(() => registry.AddStatusBarItem(item));

        public IEditorContributions AddDialog(DialogDescriptor dialog) => Forward(() => registry.AddDialog(dialog));

        public IEditorContributions AddSettingsPage(SettingsPageDescriptor page) => Forward(() => registry.AddSettingsPage(page));

        public IEditorContributions AddViewResources(string resourceUri) => Forward(() => registry.AddViewResources(owner, resourceUri));

        private OwnerFacet Forward(Action add)
        {
            registry.As(group, owner, add);
            return this;
        }
    }
}
