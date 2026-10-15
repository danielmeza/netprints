using NetPrints.Editor.Tests.Hosting;

namespace NetPrints.Editor.Tests.StartPage;

/// <summary>What the project flows do with the paths of a drop on the window (FR-049), without a window.</summary>
public sealed class ProjectDropActionTests : IDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly List<string> cleanup = [];
    private readonly List<ProjectRig> rigs = [];

    public void Dispose()
    {
        rigs.ForEach(rig => rig.Dispose());
        cleanup.ForEach(TestPaths.TryDelete);
    }

    private ProjectRig NewRig()
    {
        var rig = new ProjectRig(editor.Context);
        rigs.Add(rig);
        return rig;
    }

    [Fact]
    public async Task AFolderWithTwoProjectsIsRefusedWithTheReasonOnTheStartPage()
    {
        string sample = TestPaths.CopyHelloWorldSample();
        cleanup.Add(sample);
        string folder = Path.GetDirectoryName(sample) ?? throw new InvalidOperationException("No folder.");
        File.Copy(sample, Path.Combine(folder, "Second.csproj"));
        ProjectRig rig = NewRig();

        await rig.Actions.OpenDroppedAsync([folder], Token);

        Assert.Null(rig.Session);
        Assert.Contains("exactly one", rig.Shell.StartPageError, StringComparison.Ordinal);
        Assert.Contains("Drop", rig.Shell.StartPageError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMissingPathIsRefusedAsMissing()
    {
        ProjectRig rig = NewRig();

        await rig.Actions.OpenDroppedAsync([Path.Combine(Path.GetTempPath(), "netprints-no-such-drop.csproj")], Token);

        Assert.Contains("does not exist", rig.Shell.StartPageError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACsprojOpensAfterTheUnloadPromptAndClearsTheError()
    {
        string first = TestPaths.CopyHelloWorldSample();
        string second = TestPaths.CopyHelloWorldSample();
        cleanup.AddRange([first, second]);
        ProjectRig rig = NewRig();
        await rig.LoadProjectAsync(first);
        rig.Shell.Session?.ContextFor(rig.Shell.Session.Project.Classes[0]).CreateVariable();
        editor.Dialogs.UnsavedAnswer = NetPrints.Editor.Dialogs.UnloadChoice.Cancel;

        await rig.Actions.OpenDroppedAsync([second], Token);

        Assert.Equal(first, rig.Project?.Path);
        Assert.Single(editor.Dialogs.UnsavedCalls);

        editor.Dialogs.UnsavedAnswer = NetPrints.Editor.Dialogs.UnloadChoice.Discard;
        await rig.Actions.OpenDroppedAsync([second], Token);

        Assert.Equal(second, rig.Project?.Path);
    }

    [Fact]
    public async Task ARefusedDropWhileAProjectIsOpenShowsAStatusMessageAndAsksNothing()
    {
        string first = TestPaths.CopyHelloWorldSample();
        cleanup.Add(first);
        ProjectRig rig = NewRig();
        await rig.LoadProjectAsync(first);

        await rig.Actions.OpenDroppedAsync([first, first], Token);

        Assert.Equal(first, rig.Project?.Path);
        Assert.Contains("one", rig.Shell.StatusMessage, StringComparison.Ordinal);
        Assert.Null(rig.Shell.StartPageError);
        Assert.Empty(editor.Dialogs.UnsavedCalls);
    }
}
