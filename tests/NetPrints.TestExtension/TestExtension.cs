using NetPrints.Extensibility;
using NetPrints.Extensibility.Editor;

namespace NetPrints.TestExtension;

/// <summary>
/// The extension the loader tests load from a folder through its own <c>AssemblyLoadContext</c>: one node kind, both
/// emitters, a catalog, a profile, a settings section, a host channel factory and a project property.
/// </summary>
public sealed class TestExtension : INetPrintsExtension
{
    /// <summary>The extension id, also the manifest id.</summary>
    public const string Id = "netprints.test";

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder
            .AddNodeLibrary(new TestNodeLibrary())
            .AddJsonTypeInfoResolver(TestJsonContext.Default)
            .AddClassEmitter(new TestClassEmitter())
            .AddMemberEmitter(new TestMemberEmitter())
            .AddTypeCatalog(TestCatalog.Create())
            .AddProjectProfile(new TestProfile())
            .AddSettings(TestSettings.Descriptor)
            .AddHostChannel(new TestHostChannelFactory())
            .AddProjectProperty(TestProfile.ModeProperty)
            .AddProjectTemplate(new ProjectTemplateDescriptor(TestEditorIds.Template, "Test template", "A project from the test extension.", TestProfile.ProfileId, ProjectOutputType.Console))
            .AddSuggestionProvider(new TestSuggestionProvider())
            .AddDocumentMigration(new TestNodeMigration());
        builder.Editor
            .AddCommand(new CommandDescriptor(TestEditorIds.Command, "Test hello", new TestCommandHandler(), TestEditorIds.Icon, Menu: new MenuPlacement("File", "test", 0)))
            .AddPanel(new PanelDescriptor(TestEditorIds.Panel, "Test panel", _ => new TestContent("panel"), PanelDock.Right, 10))
            .AddDashboardTile(new DashboardTileDescriptor(TestEditorIds.Tile, "Test tile", 10, _ => new TestContent("tile")))
            .AddContextMenuItem(new ContextMenuItemDescriptor(TestEditorIds.ContextMenuItem, ContextMenuTarget.Connection, TestEditorIds.Command, "test", 0))
            .AddTooltipProvider(new TestTooltipProvider())
            .AddGoToProvider(new TestGoToProvider())
            .AddStatusBarItem(new StatusBarItemDescriptor(TestEditorIds.StatusBarItem, StatusBarAlignment.Right, 10, _ => new TestContent("status")))
            .AddSettingsPage(new SettingsPageDescriptor(TestEditorIds.SettingsPage, "Test settings", TestExtension.Id, 10))
            .AddDialog(new DialogDescriptor(TestEditorIds.Dialog, "Test dialog", _ => new TestContent("dialog")))
            .AddViewResources(TestEditorIds.ViewResourcesUri);
    }
}
