using NetPrints.Extensibility.Editor;

namespace NetPrints.Extensibility;

/// <summary>
/// The editor facet of one <see cref="ExtensionBuilder"/>: it checks each call, buffers it, and replays the buffer to the host's facet
/// when <see cref="INetPrintsExtension.Register"/> has returned normally, so a throwing extension contributes nothing to the editor either.
/// </summary>
/// <param name="extensionId">The id of the extension being registered, for the exception message.</param>
/// <param name="isSealed">Tells whether <see cref="INetPrintsExtension.Register"/> has returned.</param>
internal sealed class BufferedEditorContributions(string extensionId, Func<bool> isSealed) : IEditorContributions
{
    private readonly List<Action<IEditorContributions>> calls = [];

    public IEditorContributions AddCommand(CommandDescriptor command) => Buffer(command, target => target.AddCommand(command));

    public IEditorContributions AddPanel(PanelDescriptor panel) => Buffer(panel, target => target.AddPanel(panel));

    public IEditorContributions AddDashboardTile(DashboardTileDescriptor tile) => Buffer(tile, target => target.AddDashboardTile(tile));

    public IEditorContributions AddContextMenuItem(ContextMenuItemDescriptor item) => Buffer(item, target => target.AddContextMenuItem(item));

    public IEditorContributions AddTooltipProvider(ITooltipProvider provider) => Buffer(provider, target => target.AddTooltipProvider(provider));

    public IEditorContributions AddGoToProvider(IGoToProvider provider) => Buffer(provider, target => target.AddGoToProvider(provider));

    public IEditorContributions AddStatusBarItem(StatusBarItemDescriptor item) => Buffer(item, target => target.AddStatusBarItem(item));

    public IEditorContributions AddDialog(DialogDescriptor dialog) => Buffer(dialog, target => target.AddDialog(dialog));

    public IEditorContributions AddViewResources(string resourceUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceUri);
        return Buffer(resourceUri, target => target.AddViewResources(resourceUri));
    }

    /// <summary>
    /// Plays the buffered calls, in order, on <paramref name="target"/>.
    /// </summary>
    /// <param name="target">The host's facet.</param>
    public void ReplayTo(IEditorContributions target)
    {
        foreach (Action<IEditorContributions> call in calls)
        {
            call(target);
        }
    }

    private BufferedEditorContributions Buffer(object contribution, Action<IEditorContributions> call)
    {
        ArgumentNullException.ThrowIfNull(contribution);
        if (isSealed())
        {
            throw new InvalidOperationException($"Extension '{extensionId}' already returned from Register; contributions can no longer be added.");
        }

        calls.Add(call);
        return this;
    }
}
