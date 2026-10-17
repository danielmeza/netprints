namespace NetPrints.Editor.Contributions;

/// <summary>What the editor's surfaces are generated from: the contributions of the app group and of every session group, in group order.</summary>
public interface IContributionSnapshot
{
    /// <summary>Gets the accepted commands, in registration order.</summary>
    IReadOnlyList<CommandDescriptor> Commands { get; }

    /// <summary>Gets the accepted panels, in registration order.</summary>
    IReadOnlyList<PanelDescriptor> Panels { get; }

    /// <summary>Gets the accepted dashboard tiles, in registration order.</summary>
    IReadOnlyList<DashboardTileDescriptor> DashboardTiles { get; }

    /// <summary>Gets the accepted status bar items, in registration order.</summary>
    IReadOnlyList<StatusBarItemDescriptor> StatusBarItems { get; }

    /// <summary>Gets the accepted dialogs, in registration order.</summary>
    IReadOnlyList<DialogDescriptor> Dialogs { get; }

    /// <summary>Gets the accepted settings pages, in registration order.</summary>
    IReadOnlyList<SettingsPageDescriptor> SettingsPages { get; }

    /// <summary>Gets the accepted project templates, in registration order.</summary>
    IReadOnlyList<ProjectTemplateDescriptor> ProjectTemplates { get; }

    /// <summary>Gets the accepted context-menu items, in registration order.</summary>
    IReadOnlyList<ContextMenuItemDescriptor> ContextMenuItems { get; }

    /// <summary>Gets the tooltip providers, in registration order.</summary>
    IReadOnlyList<ITooltipProvider> TooltipProviders { get; }

    /// <summary>Gets the go-to providers, in registration order.</summary>
    IReadOnlyList<IGoToProvider> GoToProviders { get; }

    /// <summary>Gets every problem found so far.</summary>
    IReadOnlyList<ContributionIssue> Issues { get; }

    /// <summary>Gets the view resource dictionaries extensions added, in registration order.</summary>
    IReadOnlyList<ViewResourceDeclaration> ViewResources { get; }

    /// <summary>Gets the id of the extension that registered the contribution with the given id.</summary>
    /// <param name="id">The id of a command, panel, tile or context-menu item.</param>
    /// <returns>The extension id, or <see langword="null"/> when no accepted contribution has that id.</returns>
    string? OwnerOf(string id);

    /// <summary>Gets the name to show for the owner of a contribution: the extension's <c>name</c>.</summary>
    /// <param name="id">The id of a command, panel, tile or context-menu item.</param>
    /// <returns>The name; the extension id when it gave no name; <see langword="null"/> for a built-in contribution or an unknown id.</returns>
    string? OwnerNameOf(string id);
}
