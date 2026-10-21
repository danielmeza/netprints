using System.Collections.Immutable;
using NetPrints.Editor.State;

namespace NetPrints.Editor.Contributions;

/// <summary>An immutable copy of the contributions of the app group and of every session group, in group order.</summary>
public sealed class ContributionSnapshot : IContributionSnapshot
{
    private readonly ImmutableDictionary<string, string> owners;
    private readonly ImmutableDictionary<string, string> ownerNames;

    private readonly HashSet<string> shadowed;

    internal ContributionSnapshot(IReadOnlyList<ContributionGroup> groups, KeyboardSettings user)
    {
        shadowed = new HashSet<string>(groups.Where(group => group.Key is not null).SelectMany(group => group.ShadowedOwners), StringComparer.Ordinal);
        CommandDescriptor[] registered = [.. groups.SelectMany(group => group.Commands.Where(command => !IsHidden(group, command.Id)))];
        KeyBindings = KeyBindingResolver.Resolve(registered, user);
        Commands = [.. registered.Select(WithEffectiveGestures)];
        Panels = [.. groups.SelectMany(group => group.Panels.Where(item => !IsHidden(group, item.Id)))];
        DashboardTiles = [.. groups.SelectMany(group => group.Tiles.Where(item => !IsHidden(group, item.Id)))];
        StatusBarItems = [.. groups.SelectMany(group => group.StatusBarItems.Where(item => !IsHidden(group, item.Id)))];
        Dialogs = [.. groups.SelectMany(group => group.Dialogs.Where(item => !IsHidden(group, item.Id)))];
        SettingsPages = [.. groups.SelectMany(group => group.SettingsPages.Where(item => !IsHidden(group, item.Id)))];
        ProjectTemplates = [.. groups.SelectMany(group => group.Templates.Where(item => !IsHidden(group, item.Id)))];
        ContextMenuItems = [.. groups.SelectMany(group => group.MenuItems.Where(item => !IsHidden(group, item.Id)))];
        TooltipProviders = [.. groups.SelectMany(group => group.TooltipProviders)];
        GoToProviders = [.. groups.SelectMany(group => group.GoToProviders)];
        UnknownReferences = FindUnknownReferences(groups);
        Issues = [.. groups.SelectMany(group => group.Issues), .. UnknownReferences];
        ViewResources = [.. groups.SelectMany(group => group.ViewResources.Where(declaration => group.Key is not null || !shadowed.Contains(declaration.Owner)))];
        SessionKeys = [.. groups.Select(group => group.Key).OfType<string>()];
        owners = Merge(groups.Select(group => group.Owners));
        ownerNames = Merge([.. groups.Where(group => group.Key is not null).Select(group => group.OwnerNames), .. groups.Where(group => group.Key is null).Select(group => group.OwnerNames)]);
    }

    private bool IsHidden(ContributionGroup group, string id) =>
        group.Key is null && shadowed.Count > 0 && group.Owners.TryGetValue(id, out string? owner) && shadowed.Contains(owner);

    /// <summary>Gets the accepted commands, in registration order. <see cref="CommandDescriptor.DefaultGestures"/> holds the gestures in force (user overrides applied); <see cref="KeyBindings"/> keeps the registered defaults.</summary>
    public IReadOnlyList<CommandDescriptor> Commands { get; }

    /// <summary>Gets the effective key bindings, computed once for this snapshot.</summary>
    internal EffectiveKeyBindings KeyBindings { get; }

    /// <inheritdoc/>
    public IReadOnlyList<PanelDescriptor> Panels { get; }

    /// <inheritdoc/>
    public IReadOnlyList<DashboardTileDescriptor> DashboardTiles { get; }

    /// <inheritdoc/>
    public IReadOnlyList<StatusBarItemDescriptor> StatusBarItems { get; }

    /// <inheritdoc/>
    public IReadOnlyList<DialogDescriptor> Dialogs { get; }

    /// <inheritdoc/>
    public IReadOnlyList<SettingsPageDescriptor> SettingsPages { get; }

    /// <inheritdoc/>
    public IReadOnlyList<ProjectTemplateDescriptor> ProjectTemplates { get; }

    /// <inheritdoc/>
    public IReadOnlyList<ContextMenuItemDescriptor> ContextMenuItems { get; }

    /// <inheritdoc/>
    public IReadOnlyList<ITooltipProvider> TooltipProviders { get; }

    /// <inheritdoc/>
    public IReadOnlyList<IGoToProvider> GoToProviders { get; }

    /// <inheritdoc/>
    public IReadOnlyList<ContributionIssue> Issues { get; }

    /// <summary>Gets the issues of contributions that name an id nothing registered; they are part of <see cref="Issues"/>.</summary>
    internal IReadOnlyList<ContributionIssue> UnknownReferences { get; }

    /// <inheritdoc/>
    public IReadOnlyList<ViewResourceDeclaration> ViewResources { get; }

    /// <summary>Gets the keys of the session groups, in the order they were added.</summary>
    public IReadOnlyList<string> SessionKeys { get; }

    /// <inheritdoc/>
    public string? OwnerOf(string id) => owners.GetValueOrDefault(id);

    /// <inheritdoc/>
    public string? OwnerNameOf(string id)
    {
        string? owner = OwnerOf(id);
        if (owner is null || owner == ContributionIds.Owner || owner == BuiltIn.BuiltInContributions.OwnerId)
        {
            return null;
        }

        return ownerNames.GetValueOrDefault(owner, owner);
    }

    private CommandDescriptor WithEffectiveGestures(CommandDescriptor command) =>
        KeyBindings.TryGet(command.Id, out CommandBindings bindings) && bindings.IsChanged ? command with { DefaultGestures = bindings.Effective } : command;

    private ImmutableArray<ContributionIssue> FindUnknownReferences(IReadOnlyList<ContributionGroup> groups)
    {
        HashSet<string> commandIds = [.. Commands.Select(command => command.Id)];
        HashSet<string> pageIds = [.. SettingsPages.Select(page => page.Id)];
        ImmutableArray<ContributionIssue>.Builder found = ImmutableArray.CreateBuilder<ContributionIssue>();
        foreach (ContributionGroup group in groups)
        {
            bool FromExtension(string id) => group.Owners.GetValueOrDefault(id, ContributionIds.Owner) is var owner && owner != ContributionIds.Owner && owner != BuiltIn.BuiltInContributions.OwnerId;

            foreach (ContextMenuItemDescriptor item in group.MenuItems.Where(item => !IsHidden(group, item.Id) && FromExtension(item.Id) && !commandIds.Contains(item.CommandId)))
            {
                found.Add(new ContributionIssue(ContributionIssueKind.UnknownReference, item.Id, group.Owners.GetValueOrDefault(item.Id, ContributionIds.Owner),
                    $"The context menu item names the command '{item.CommandId}', which nothing registered."));
            }

            foreach (SettingsPageDescriptor page in group.SettingsPages.Where(page => !IsHidden(group, page.Id) && FromExtension(page.Id) && page.ParentPageId is { } parent && !pageIds.Contains(parent)))
            {
                found.Add(new ContributionIssue(ContributionIssueKind.UnknownReference, page.Id, group.Owners.GetValueOrDefault(page.Id, ContributionIds.Owner),
                    $"The settings page names the parent page '{page.ParentPageId}', which nothing registered."));
            }
        }

        return found.ToImmutable();
    }

    private static ImmutableDictionary<string, string> Merge(IEnumerable<Dictionary<string, string>> parts)
    {
        ImmutableDictionary<string, string>.Builder merged = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> pair in parts.SelectMany(part => part))
        {
            merged.TryAdd(pair.Key, pair.Value);
        }

        return merged.ToImmutable();
    }
}
