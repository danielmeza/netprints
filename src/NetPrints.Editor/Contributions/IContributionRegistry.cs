namespace NetPrints.Editor.Contributions;

/// <summary>
/// The single registry of everything the editor's surfaces are generated from (ADR-0020). Every
/// <c>Add*</c> validates, records the owner and throws <see cref="InvalidOperationException"/> after
/// <see cref="IsFrozen"/>. Rejected or altered contributions are listed in <see cref="Issues"/>; the first
/// registration wins.
/// </summary>
public interface IContributionRegistry
{
    /// <summary>Gets the accepted commands, in registration order.</summary>
    IReadOnlyList<CommandDescriptor> Commands { get; }

    /// <summary>Gets the accepted panels, in registration order.</summary>
    IReadOnlyList<PanelDescriptor> Panels { get; }

    /// <summary>Gets the accepted dashboard tiles, in registration order.</summary>
    IReadOnlyList<DashboardTileDescriptor> DashboardTiles { get; }

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

    /// <summary>Gets a value indicating whether <see cref="Freeze"/> was called.</summary>
    bool IsFrozen { get; }

    /// <summary>Adds a command; an invalid or duplicate one is ignored, a conflicting gesture is dropped.</summary>
    /// <param name="command">The command.</param>
    /// <exception cref="InvalidOperationException">The registry is frozen.</exception>
    void AddCommand(CommandDescriptor command);

    /// <summary>Adds a panel; an invalid or duplicate one is ignored.</summary>
    /// <param name="panel">The panel.</param>
    /// <exception cref="InvalidOperationException">The registry is frozen.</exception>
    void AddPanel(PanelDescriptor panel);

    /// <summary>Adds a dashboard tile; an invalid or duplicate one is ignored.</summary>
    /// <param name="tile">The tile.</param>
    /// <exception cref="InvalidOperationException">The registry is frozen.</exception>
    void AddDashboardTile(DashboardTileDescriptor tile);

    /// <summary>Adds a project template; an invalid or duplicate one is ignored.</summary>
    /// <param name="template">The template.</param>
    /// <exception cref="InvalidOperationException">The registry is frozen.</exception>
    void AddProjectTemplate(ProjectTemplateDescriptor template);

    /// <summary>Adds a context-menu item; an invalid or duplicate one is ignored.</summary>
    /// <param name="item">The item.</param>
    /// <exception cref="InvalidOperationException">The registry is frozen.</exception>
    void AddContextMenuItem(ContextMenuItemDescriptor item);

    /// <summary>Adds a tooltip provider.</summary>
    /// <param name="provider">The provider.</param>
    /// <exception cref="InvalidOperationException">The registry is frozen.</exception>
    void AddTooltipProvider(ITooltipProvider provider);

    /// <summary>Adds a go-to provider.</summary>
    /// <param name="provider">The provider.</param>
    /// <exception cref="InvalidOperationException">The registry is frozen.</exception>
    void AddGoToProvider(IGoToProvider provider);

    /// <summary>Ends registration; every later <c>Add*</c> throws.</summary>
    void Freeze();
}
