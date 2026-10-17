using System.Collections.Immutable;

namespace NetPrints.Editor.Contributions;

/// <summary>An immutable copy of the contributions of the app group and of every session group, in group order.</summary>
public sealed class ContributionSnapshot : IContributionSnapshot
{
    private readonly ImmutableDictionary<string, string> owners;
    private readonly ImmutableDictionary<string, string> ownerNames;

    internal ContributionSnapshot(IReadOnlyList<ContributionGroup> groups)
    {
        Commands = [.. groups.SelectMany(group => group.Commands)];
        Panels = [.. groups.SelectMany(group => group.Panels)];
        DashboardTiles = [.. groups.SelectMany(group => group.Tiles)];
        StatusBarItems = [.. groups.SelectMany(group => group.StatusBarItems)];
        Dialogs = [.. groups.SelectMany(group => group.Dialogs)];
        SettingsPages = [.. groups.SelectMany(group => group.SettingsPages)];
        ProjectTemplates = [.. groups.SelectMany(group => group.Templates)];
        ContextMenuItems = [.. groups.SelectMany(group => group.MenuItems)];
        TooltipProviders = [.. groups.SelectMany(group => group.TooltipProviders)];
        GoToProviders = [.. groups.SelectMany(group => group.GoToProviders)];
        Issues = [.. groups.SelectMany(group => group.Issues)];
        ViewResources = [.. groups.SelectMany(group => group.ViewResources)];
        SessionKeys = [.. groups.Select(group => group.Key).OfType<string>()];
        owners = Merge(groups.Select(group => group.Owners));
        ownerNames = Merge(groups.Select(group => group.OwnerNames));
    }

    /// <inheritdoc/>
    public IReadOnlyList<CommandDescriptor> Commands { get; }

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
