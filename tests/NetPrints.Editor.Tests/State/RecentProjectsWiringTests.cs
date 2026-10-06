using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.State;
using NetPrints.Editor.Tests.Hosting;

namespace NetPrints.Editor.Tests.State;

/// <summary>Opening or creating a project records it in the recent list (FR-041).</summary>
public sealed class RecentProjectsWiringTests : IDisposable
{
    private readonly TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly InMemoryEditorFileSystem fs = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero));
    private readonly List<string> cleanup = [];
    private readonly List<ProjectRig> rigs = [];

    public void Dispose()
    {
        rigs.ForEach(rig => rig.Dispose());
        cleanup.ForEach(TestPaths.TryDelete);
    }

    private (ProjectRig Rig, RecentProjects Recent) NewRig()
    {
        var store = new JsonEditorStateStore(new EditorDataPaths("state-root"), fs, NullLogger.Instance);
        var recent = new RecentProjects(store, new RealEditorFileSystem(), time);
        var rig = new ProjectRig(editor.Context with { Recent = recent }, time);
        rigs.Add(rig);
        return (rig, recent);
    }

    [Fact]
    public async Task OpeningAProjectRecordsIt()
    {
        (ProjectRig rig, RecentProjects recent) = NewRig();
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);

        await rig.Actions.OpenProjectAsync(path, TestContext.Current.CancellationToken);

        RecentProject entry = Assert.Single(recent.List());
        Assert.Equal(path, entry.Path);
        Assert.Equal(rig.Project?.Name, entry.DisplayName);
        Assert.True(entry.IsAvailable);
        Assert.Equal(time.GetUtcNow().UtcDateTime, entry.LastOpenedUtc);
    }

    [Fact]
    public async Task CreatingAProjectRecordsIt()
    {
        (ProjectRig rig, RecentProjects recent) = NewRig();
        string dir = TestPaths.CreateTempDirectory();
        cleanup.Add(dir);
        string path = Path.Combine(dir, "Chosen.csproj");
        editor.Dialogs.NewProjectScript = async dialog =>
        {
            dialog.Name = "Chosen";
            dialog.Folder = dir;
            await dialog.CreateCommand.ExecuteAsync(null);
        };

        await rig.Actions.NewProjectAsync(TestContext.Current.CancellationToken);

        RecentProject entry = Assert.Single(recent.List());
        Assert.Equal((path, "Chosen"), (entry.Path, entry.DisplayName));
    }

    [Fact]
    public async Task APathThatIsNotAProjectIsNotRecorded()
    {
        (ProjectRig rig, RecentProjects recent) = NewRig();

        await rig.Actions.OpenProjectAsync("/somewhere/Old.netproj", TestContext.Current.CancellationToken);

        Assert.Empty(recent.List());
    }

    [Fact]
    public async Task OpeningWithoutARecentListStillWorks()
    {
        var rig = new ProjectRig(editor.Context, time);
        rigs.Add(rig);
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);

        await rig.Actions.OpenProjectAsync(path, TestContext.Current.CancellationToken);

        Assert.NotNull(rig.Project);
    }
}
