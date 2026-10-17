using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Hosting;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Settings;
using NetPrints.Projects;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>FR-032, Clarifications answer 2, NPX012: a project's extensions live in its session group; the user copy of the same id adds nothing.</summary>
public class ProjectExtensionGroupTests : IAsyncDisposable
{
    private const string ExtensionId = "fx.editor";

    private readonly List<string> cleanup = [];
    private readonly ExtensionContributionRecorder recorder = new();
    private readonly TestEditor editor;
    private readonly ExtensionHost extensions;

    public ProjectExtensionGroupTests()
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

    private async Task<string> TrustedProjectAsync(params string[] extensionFolders)
    {
        string csproj = TestPaths.CopyHelloWorldSample();
        cleanup.Add(Path.GetDirectoryName(csproj) ?? csproj);
        ProjectSnapshot snapshot = await editor.Projects.LoadAsync(csproj, TestContext.Current.CancellationToken);
        editor.Projects.Seed(snapshot with { ExtensionFolders = extensionFolders });
        await editor.Settings.SetAsync(NetPrintsSettings.Descriptor, NetPrintsSettings.Empty with { TrustedProjects = [csproj] }, TestContext.Current.CancellationToken);
        return csproj;
    }

    [Fact]
    public async Task AProjectsExtensionContributesToItsSessionGroupAndLeavesWhenTheProjectCloses()
    {
        string csproj = await TrustedProjectAsync(TestExtensionFolder.FixtureFolder("Fx.Editor", "fx.editor"));
        using var rig = new ProjectRig(editor.Context);

        await rig.LoadProjectAsync(csproj);

        IContributionRegistry registry = rig.Shell.Registry;
        Assert.Contains(registry.Commands, c => c.Id == "fx.editor.hello");
        Assert.Contains(registry.Panels, p => p.Id == "fx.editor.panel");
        Assert.Contains(registry.DashboardTiles, t => t.Id == "fx.editor.tile");
        Assert.Equal("NetPrints editor facet fixture extension", registry.OwnerNameOf("fx.editor.panel"));

        await rig.Actions.Loader.CloseProjectAsync();

        Assert.DoesNotContain(registry.Commands, c => c.Id == "fx.editor.hello");
        Assert.Empty(registry.Panels.Where(p => p.Id == "fx.editor.panel"));
        Assert.DoesNotContain(registry.DashboardTiles, t => t.Id == "fx.editor.tile");
        Assert.DoesNotContain(extensions.Current.Loaded, m => m.Id == ExtensionId);
    }

    [Fact]
    public async Task ReopeningAProjectReplacesItsSessionGroup()
    {
        string csproj = await TrustedProjectAsync(TestExtensionFolder.FixtureFolder("Fx.Editor", "fx.editor"));
        using var rig = new ProjectRig(editor.Context);

        await rig.LoadProjectAsync(csproj);
        await rig.LoadProjectAsync(csproj);

        Assert.Single(rig.Shell.Registry.Panels, p => p.Id == "fx.editor.panel");
        Assert.DoesNotContain(rig.Shell.Registry.Issues, issue => issue.Kind == ContributionIssueKind.DuplicateId);
    }
}
