using NetPrints.Core;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.ProjectTree;

namespace NetPrints.Editor.Tests.Shell;

public sealed class ProjectSettingsDocumentViewModelTests : IAsyncDisposable
{
    private readonly ShellPanelRig rig = new();

    public ValueTask DisposeAsync() => rig.DisposeAsync();

    [Fact]
    public async Task TheDocumentHasTheProjectSettingsIdAndShowsTheProjectsBinaryType()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        using var document = new ProjectSettingsDocumentViewModel(session, rig.Context);

        Assert.Equal(DocumentId.ProjectSettings, document.Id);
        Assert.Equal("Project settings", document.Title);
        Assert.Equal(session.Project.OutputBinaryType, document.OutputBinaryType);
        Assert.Equal(Enum.GetValues<BinaryType>(), document.BinaryTypes);
    }

    [Fact]
    public async Task ChoosingABinaryTypeEditsTheProjectAndTheDocumentFollowsIt()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        using var document = new ProjectSettingsDocumentViewModel(session, rig.Context);
        BinaryType other = Enum.GetValues<BinaryType>().First(type => type != session.Project.OutputBinaryType);
        var changed = new List<string?>();
        document.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await document.SetOutputTypeCommand.ExecuteAsync(other);

        Assert.Equal(other, session.Project.OutputBinaryType);
        Assert.Equal(other, session.Project.Snapshot?.OutputType);
        Assert.Equal(other, document.OutputBinaryType);
        Assert.Contains(nameof(ProjectSettingsDocumentViewModel.OutputBinaryType), changed);
    }

    [Fact]
    public async Task ADisposedDocumentNoLongerFollowsTheProject()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        var document = new ProjectSettingsDocumentViewModel(session, rig.Context);
        var changed = new List<string?>();
        document.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        document.Dispose();

        session.Project.OutputBinaryType = Enum.GetValues<BinaryType>().First(type => type != session.Project.OutputBinaryType);

        Assert.Empty(changed);
    }
}
