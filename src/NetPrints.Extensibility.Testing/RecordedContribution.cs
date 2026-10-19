using NetPrints.Extensibility.Editor;

namespace NetPrints.Extensibility.Testing;

/// <summary>One editor contribution an extension made while it was loaded.</summary>
/// <param name="ExtensionId">The id of the extension that made it.</param>
/// <param name="Descriptor">What it contributed: a descriptor of <see cref="IEditorContributions"/>, a provider, or the resource URI string of a view resource dictionary.</param>
public sealed record RecordedContribution(string ExtensionId, object Descriptor);

/// <summary>Collects the editor contributions of the extensions a harness loads instead of showing them in an editor.</summary>
internal sealed class EditorContributionRecorder
{
    private readonly List<RecordedContribution> items = [];

    public IReadOnlyList<RecordedContribution> Items => items;

    public IEditorContributions For(string extensionId) => new Recording(extensionId, items);

    private sealed class Recording(string extensionId, List<RecordedContribution> items) : IEditorContributions
    {
        public IEditorContributions AddCommand(CommandDescriptor command) => Record(command);

        public IEditorContributions AddPanel(PanelDescriptor panel) => Record(panel);

        public IEditorContributions AddDashboardTile(DashboardTileDescriptor tile) => Record(tile);

        public IEditorContributions AddContextMenuItem(ContextMenuItemDescriptor item) => Record(item);

        public IEditorContributions AddTooltipProvider(ITooltipProvider provider) => Record(provider);

        public IEditorContributions AddGoToProvider(IGoToProvider provider) => Record(provider);

        public IEditorContributions AddStatusBarItem(StatusBarItemDescriptor item) => Record(item);

        public IEditorContributions AddDialog(DialogDescriptor dialog) => Record(dialog);

        public IEditorContributions AddSettingsPage(SettingsPageDescriptor page) => Record(page);

        public IEditorContributions AddViewResources(string resourceUri) => Record(resourceUri);

        private Recording Record(object descriptor)
        {
            items.Add(new RecordedContribution(extensionId, descriptor));
            return this;
        }
    }
}
