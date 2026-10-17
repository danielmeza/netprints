using NetPrints.Extensibility.Editor;

namespace NetPrints.Extensibility;

/// <summary>
/// The editor facet of a host without an editor (the CLI, the SDK generator, the build): it accepts every call and ignores it.
/// </summary>
internal sealed class NullEditorContributions : IEditorContributions
{
    public static NullEditorContributions Instance { get; } = new();

    public IEditorContributions AddCommand(CommandDescriptor command) => this;

    public IEditorContributions AddPanel(PanelDescriptor panel) => this;

    public IEditorContributions AddDashboardTile(DashboardTileDescriptor tile) => this;

    public IEditorContributions AddContextMenuItem(ContextMenuItemDescriptor item) => this;

    public IEditorContributions AddTooltipProvider(ITooltipProvider provider) => this;

    public IEditorContributions AddGoToProvider(IGoToProvider provider) => this;

    public IEditorContributions AddStatusBarItem(StatusBarItemDescriptor item) => this;

    public IEditorContributions AddProjectTemplate(ProjectTemplateDescriptor template) => this;

    public IEditorContributions AddDialog(DialogDescriptor dialog) => this;

    public IEditorContributions AddViewResources(string resourceUri) => this;
}
