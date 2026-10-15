using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.Shell;
using NetPrints.Serialization;

namespace NetPrints.Editor.Tests.Commands;

/// <summary>A test editor, a fake shell and project sessions over copies of the sample project, all cleaned up on disposal.</summary>
public abstract class SessionCommandTests : IAsyncDisposable
{
    private readonly List<string> cleanup = [];
    private readonly List<ProjectSessionViewModel> sessions = [];
    private readonly List<IDisposable> disposables = [];

    protected TestEditor Editor { get; } = TestEditor.Create(TestEditor.CreateReflectionHost);

    protected FakeShell Shell { get; } = new();

    public async ValueTask DisposeAsync()
    {
        disposables.ForEach(disposable => disposable.Dispose());
        sessions.ForEach(session => session.Dispose());
        cleanup.ForEach(TestPaths.TryDelete);
        await Editor.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected T Track<T>(T disposable)
        where T : IDisposable
    {
        disposables.Add(disposable);
        return disposable;
    }

    protected async Task<ProjectSessionViewModel> OpenSessionAsync(EditorContext? context = null)
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        ProjectLoadResult loaded = await Editor.Persistence.LoadAsync(path, TestContext.Current.CancellationToken);
        var session = new ProjectSessionViewModel(loaded.Project, context ?? Editor.Context);
        sessions.Add(session);
        return session;
    }
}
