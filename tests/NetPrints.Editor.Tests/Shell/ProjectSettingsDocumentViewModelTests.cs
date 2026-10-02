using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.ProjectTree;
using NetPrints.Projects;

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

    [Fact]
    public async Task ConcurrentOutputTypeTogglesLetOneCommandWinDeterministically()
    {
        // R2-10: SetOutputTypeCommand is a plain [RelayCommand] async Task, so its CanExecute is false while it runs;
        // the gate holds the first edit in flight so that check is deterministic instead of racing a fake that
        // completes synchronously.
        var gate = new TaskCompletionSource();
        EditorContext context = rig.Context with { Projects = new GatedProjectSystem(rig.Context.Projects, gate.Task) };
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        using var document = new ProjectSettingsDocumentViewModel(session, context);
        BinaryType other = Enum.GetValues<BinaryType>().First(type => type != session.Project.OutputBinaryType);

        Task first = document.SetOutputTypeCommand.ExecuteAsync(other);
        Assert.True(document.SetOutputTypeCommand.IsRunning);
        Assert.False(document.SetOutputTypeCommand.CanExecute(other), "a second toggle is rejected while the first is in flight");

        gate.SetResult();
        await first;

        Assert.Equal(other, document.OutputBinaryType);
        Assert.Equal(other, session.Project.OutputBinaryType);
    }

    /// <summary>Delays every <see cref="ApplyAsync"/> until the gate completes.</summary>
    private sealed class GatedProjectSystem(IProjectSystem inner, Task gate) : IProjectSystem
    {
        public Task<ProjectSnapshot> LoadAsync(string projectFilePath, CancellationToken cancellationToken) =>
            inner.LoadAsync(projectFilePath, cancellationToken);

        public async Task<ProjectSnapshot> ApplyAsync(string projectFilePath, IReadOnlyList<ProjectEdit> edits, CancellationToken cancellationToken)
        {
            await gate;
            return await inner.ApplyAsync(projectFilePath, edits, cancellationToken);
        }

        public Task<string> CreateAsync(string directory, string projectName, IProjectProfile profile, string rootNamespace, CancellationToken cancellationToken) =>
            inner.CreateAsync(directory, projectName, profile, rootNamespace, cancellationToken);

        public Task<BuildResult> BuildAsync(string projectFilePath, CancellationToken cancellationToken) =>
            inner.BuildAsync(projectFilePath, cancellationToken);

        public ProcessStartRequest GetRunCommand(string projectFilePath) => inner.GetRunCommand(projectFilePath);
    }
}
