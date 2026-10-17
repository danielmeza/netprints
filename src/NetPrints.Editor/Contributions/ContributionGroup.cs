namespace NetPrints.Editor.Contributions;

/// <summary>The contributions of the app or of one open project, while the registry fills them.</summary>
/// <param name="key">The session key, or null for the app group.</param>
internal sealed class ContributionGroup(string? key)
{
    /// <summary>Gets the session key, or null for the app group.</summary>
    public string? Key { get; } = key;

    /// <summary>Gets the commands.</summary>
    public List<CommandDescriptor> Commands { get; } = [];

    /// <summary>Gets the panels.</summary>
    public List<PanelDescriptor> Panels { get; } = [];

    /// <summary>Gets the dashboard tiles.</summary>
    public List<DashboardTileDescriptor> Tiles { get; } = [];

    /// <summary>Gets the status bar items.</summary>
    public List<StatusBarItemDescriptor> StatusBarItems { get; } = [];

    /// <summary>Gets the dialogs.</summary>
    public List<DialogDescriptor> Dialogs { get; } = [];

    /// <summary>Gets the settings pages.</summary>
    public List<SettingsPageDescriptor> SettingsPages { get; } = [];

    /// <summary>Gets the project templates.</summary>
    public List<ProjectTemplateDescriptor> Templates { get; } = [];

    /// <summary>Gets the context-menu items.</summary>
    public List<ContextMenuItemDescriptor> MenuItems { get; } = [];

    /// <summary>Gets the tooltip providers.</summary>
    public List<ITooltipProvider> TooltipProviders { get; } = [];

    /// <summary>Gets the go-to providers.</summary>
    public List<IGoToProvider> GoToProviders { get; } = [];

    /// <summary>Gets the issues.</summary>
    public List<ContributionIssue> Issues { get; } = [];

    /// <summary>Gets the view resources.</summary>
    public List<ViewResourceDeclaration> ViewResources { get; } = [];

    /// <summary>Gets the owner of each accepted id.</summary>
    public Dictionary<string, string> Owners { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the display name of each extension that gave one.</summary>
    public Dictionary<string, string> OwnerNames { get; } = new(StringComparer.Ordinal);
}
