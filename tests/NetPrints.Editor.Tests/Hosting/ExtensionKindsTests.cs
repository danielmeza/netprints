using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Settings;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Shell;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Settings;
using NetPrints.Graph;
using NetPrints.Projects;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>SC-004, US4 independent test: a project that declares the test extension shows one item of every kind on its surface, and none is left after the project unloads.</summary>
public sealed class ExtensionKindsTests : IAsyncDisposable
{
    private static class TestEditorIds
    {
        public const string Command = "netprints.test.hello";

        public const string Panel = "netprints.test.panel";

        public const string Tile = "netprints.test.tile";

        public const string Template = "netprints.test.template";

        public const string StatusBarItem = "netprints.test.status";

        public const string SettingsPage = "netprints.test.settings";

        public const string Dialog = "netprints.test.dialog";

        public const string ViewResourcesUri = "avares://NetPrints.TestExtension/TestIcons.axaml";

        public const string SuggestionProvider = "netprints.test.rows";

        public const string GoToQuery = "hello";

        public const string LogKind = "netprints.test/Legacy";

        public const string MigratedMember = "migratedByTest";
    }

    private readonly ExtensionContributionRecorder recorder = new();
    private readonly List<string> cleanup = [];
    private readonly ExtensionHost extensions;
    private readonly TestEditor editor;

    public ExtensionKindsTests()
    {
        extensions = new ExtensionHost(
            new ExtensionLoaderOptions([], [], [BuiltInExtension.InProcessEntry]) { EditorContributions = recorder.Factory }, NullLoggerFactory.Instance);
        editor = TestEditor.Create(TestEditor.CreateReflectionHost, extensions, recorder: recorder);
    }

    public async ValueTask DisposeAsync()
    {
        await editor.DisposeAsync();
        cleanup.ForEach(TestPaths.TryDelete);
    }

    private sealed class Contexts : ICommandContextProvider
    {
        public event EventHandler? CommandStatesChanged
        {
            add { }
            remove { }
        }

        public CommandContext Create(object? parameter = null, CommandScope scope = CommandScope.Global) => new(new FakeShell(), null, null, null, CommandSelection.None, parameter, scope);
    }

    private static TooltipConnection AConnection()
    {
        var method = new MethodGraph("Probe");
        return new TooltipConnection(((MethodEntryNode)method.EntryNode).ExecPin, ((ReturnNode)method.MainReturnNode).ExecPin);
    }

    private async Task<string> ProjectDeclaringTheTestExtensionAsync()
    {
        string csproj = TestPaths.CopyHelloWorldSample();
        cleanup.Add(Path.GetDirectoryName(csproj) ?? csproj);
        ProjectSnapshot snapshot = await editor.Projects.LoadAsync(csproj, TestContext.Current.CancellationToken);
        editor.Projects.Seed(snapshot with { ExtensionFolders = [TestExtensionFolder.Folder] });
        await editor.Settings.SetAsync(NetPrintsSettings.Descriptor, NetPrintsSettings.Empty with { TrustedProjects = [csproj] }, TestContext.Current.CancellationToken);
        return csproj;
    }

    private async Task<IReadOnlyList<string>> KindsOnSurfacesAsync(ProjectRig rig)
    {
        IContributionRegistry registry = rig.Shell.Registry;
        var invoker = new CommandInvoker(registry, new Contexts(), _ => { });
        List<string> found = [];

        if (registry.Commands.Any(command => command.Id == TestEditorIds.Command && command.Menu is not null))
        {
            found.Add("command");
        }

        if (rig.Shell.FindPanel(TestEditorIds.Panel)?.Content is { } content && content is not ContributionErrorViewModel && content.ToString()?.Contains("panel", StringComparison.Ordinal) == true)
        {
            found.Add("panel");
        }

        if (registry.DashboardTiles.Any(tile => tile.Id == TestEditorIds.Tile))
        {
            found.Add("tile");
        }

        if (editor.Extensions.Current.ProjectTemplates.Any(template => template.Id == TestEditorIds.Template))
        {
            found.Add("template");
        }

        if (invoker.ContextMenuCommands(ContextMenuTarget.Connection).Any(command => command.Id == TestEditorIds.Command))
        {
            found.Add("context menu item");
        }

        if (invoker.TooltipFor(new TooltipTarget(TooltipTargetKind.Connection, AConnection())) is { Title: "Test connection" })
        {
            found.Add("tooltip provider");
        }

        foreach (IGoToProvider provider in registry.GoToProviders)
        {
            await foreach (GoToItem item in provider.SearchAsync(TestEditorIds.GoToQuery, TestContext.Current.CancellationToken))
            {
                if (item.CommandId == TestEditorIds.Command)
                {
                    found.Add("go-to provider");
                }
            }
        }

        if (rig.Shell.StatusBar.RightItems.Any(item => item.Id == TestEditorIds.StatusBarItem))
        {
            found.Add("status bar item");
        }

        using (var settings = new SettingsDocumentViewModel(editor.Settings, registry, editor.Extensions, new InlineDispatcher(), _ => new EditorContexts(new Contexts().Create()), null, _ => Task.CompletedTask))
        {
            if (settings.Pages.Any(page => page.Id == TestEditorIds.SettingsPage && page.Items.OfType<SettingsFieldViewModel>().Any()))
            {
                found.Add("settings page");
            }
        }

        if (registry.Dialogs.Any(dialog => dialog.Id == TestEditorIds.Dialog))
        {
            found.Add("dialog");
        }

        if (registry.ViewResources.Any(resource => resource.Uri == TestEditorIds.ViewResourcesUri))
        {
            found.Add("icon");
        }

        if (editor.Extensions.Current.SuggestionProviders.Any(provider => provider.Id == TestEditorIds.SuggestionProvider))
        {
            found.Add("node search provider");
        }

        var document = new JsonObject { ["classGraph"] = new JsonObject { ["nodes"] = new JsonArray(new JsonObject { ["$kind"] = TestEditorIds.LogKind, ["id"] = "n0000000000ab1" }) } };
        JsonObject upgraded = editor.Extensions.Current.NodeMigrator.Upgrade(document);
        JsonObject node = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(Assert.IsType<JsonObject>(upgraded["classGraph"])["nodes"])[0]);
        if (node[TestEditorIds.MigratedMember] is not null)
        {
            found.Add("document migration");
        }

        return found;
    }

    [Fact]
    public async Task EveryKindIsOnItsSurfaceWhileTheProjectIsOpenAndNoneRemainsAfterItUnloads()
    {
        string csproj = await ProjectDeclaringTheTestExtensionAsync();
        using var rig = new ProjectRig(editor.Context);
        string[] everyKind =
        [
            "command", "panel", "tile", "template", "context menu item", "tooltip provider", "go-to provider",
            "status bar item", "settings page", "dialog", "icon", "node search provider", "document migration",
        ];
        Assert.Empty(await KindsOnSurfacesAsync(rig));

        await rig.LoadProjectAsync(csproj);

        Assert.Equal(everyKind, await KindsOnSurfacesAsync(rig));
        Assert.Empty(rig.Shell.Registry.Issues);

        await rig.Actions.Loader.CloseProjectAsync();

        Assert.Empty(await KindsOnSurfacesAsync(rig));
    }
}
