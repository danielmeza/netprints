using Microsoft.Extensions.Logging;
using NetPrints.Editor.Commands;
using NetPrints.Editor.Contributions.BuiltIn;

namespace NetPrints.Editor.Contributions;

/// <summary>The default <see cref="IContributionRegistry"/>.</summary>
/// <param name="logger">Receives one warning per issue.</param>
public sealed class ContributionRegistry(ILogger<ContributionRegistry> logger) : IContributionRegistry
{
    private const int UnlistedPanelOrder = 100;

    private readonly List<CommandDescriptor> commands = [];
    private readonly List<PanelDescriptor> panels = [];
    private readonly List<DashboardTileDescriptor> tiles = [];
    private readonly List<ProjectTemplateDescriptor> templates = [];
    private readonly List<ContextMenuItemDescriptor> menuItems = [];
    private readonly List<ITooltipProvider> tooltipProviders = [];
    private readonly List<IGoToProvider> goToProviders = [];
    private readonly List<ContributionIssue> issues = [];

    /// <inheritdoc/>
    public IReadOnlyList<CommandDescriptor> Commands => commands;

    /// <inheritdoc/>
    public IReadOnlyList<PanelDescriptor> Panels => panels;

    /// <inheritdoc/>
    public IReadOnlyList<DashboardTileDescriptor> DashboardTiles => tiles;

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
        if (!TryAccept(command.Id, !IsBlank(command.Label) && command.Handler is not null, "a label and a handler", commands.Select(c => c.Id)))
        {
            return;
        }

        var gestures = new List<(string Text, string Canonical)>();
        foreach (string text in command.DefaultGestures ?? [])
        {
            if (!CommandGesture.TryParse(text, out var gesture))
            {
                Report(ContributionIssueKind.InvalidDescriptor, command.Id, $"Gesture '{text}' cannot be parsed.");
                return;
            }

            if (gesture.IsPlain && !gesture.IsFunctionKey && command.Scope == CommandScope.Global)
            {
                Report(ContributionIssueKind.InvalidDescriptor, command.Id, $"Single-key or Shift-only gesture '{text}' is not allowed in the Global scope (function keys are).");
                return;
            }

            if (!gestures.Exists(g => g.Canonical == gesture.ToString()))
            {
                gestures.Add((text, gesture.ToString()));
            }
        }

        var kept = new List<string>(gestures.Count);
        foreach (var (text, canonical) in gestures)
        {
            var owner = commands.FirstOrDefault(c => ScopesOverlap(c.Scope, command.Scope) && GesturesOf(c).Contains(canonical));
            if (owner is null)
            {
                kept.Add(text);
            }
            else
            {
                Report(ContributionIssueKind.GestureConflict, command.Id, $"Gesture '{text}' is already bound to '{owner.Id}'; the first registration keeps it.");
            }
        }

        commands.Add(command.DefaultGestures is null || kept.Count == command.DefaultGestures.Count ? command : command with { DefaultGestures = kept });
    }

    /// <inheritdoc/>
    public void AddPanel(PanelDescriptor panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        ThrowIfFrozen();
        if (TryAccept(panel.Id, !IsBlank(panel.Title) && panel.CreateViewModel is not null, "a title and a view model factory", panels.Select(p => p.Id)))
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
        if (TryAccept(item.Id, ContributionIds.IsValid(item.CommandId) && item.Group is not null, "a valid command id and a group", menuItems.Select(m => m.Id)))
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
                AddCommand(new CommandDescriptor(
                    ContributionIds.CommandPrefix + "showPanel." + panel.Id,
                    panel.Title,
                    new ShowPanelCommandHandler(panel.Id),
                    IconId: panel.IconId,
                    Menu: new MenuPlacement(ViewContributions.MenuName, ViewContributions.PanelsGroup, UnlistedPanelOrder + index++)));
            }
        }

        IsFrozen = true;
    }

    private static bool IsBlank(string? text) => string.IsNullOrWhiteSpace(text);

    private static bool ScopesOverlap(CommandScope a, CommandScope b) =>
        a == CommandScope.Global || b == CommandScope.Global || (a & b) != 0;

    private static HashSet<string> GesturesOf(CommandDescriptor command) =>
        [.. (command.DefaultGestures ?? []).Select(g => CommandGesture.TryParse(g, out var parsed) ? parsed.ToString() : g)];

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

        return true;
    }

    private void Report(ContributionIssueKind kind, string id, string message)
    {
        issues.Add(new ContributionIssue(kind, id, ContributionIds.Owner, message));
        Log.ContributionIssue(logger, kind, id, ContributionIds.Owner, message);
    }
}
