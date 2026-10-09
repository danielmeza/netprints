using System.Collections.ObjectModel;
using System.Threading.Channels;
using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Projects;
using NetPrints.Reflection;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>
/// A references reload the loader starts in the background stops when a newer one starts or the loader is disposed,
/// instead of building and warming a provider nobody will use (each one holds every referenced assembly and a CPU).
/// </summary>
public sealed class ReflectionReloadCancellationTests : IAsyncDisposable
{
    private readonly TestEditor testEditor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly List<string> cleanup = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        cleanup.ForEach(TestPaths.TryDelete);
        await testEditor.DisposeAsync();
    }

    [Fact]
    public async Task DisposingTheLoaderCancelsTheReloadStillRunning()
    {
        var host = new HeldReloadHost(testEditor.Context.Reflection);
        var rig = new ProjectRig(testEditor.Context with { Reflection = host });
        await rig.LoadProjectAsync(Track(TestPaths.CopyHelloWorldSample()));
        CancellationToken reload = await host.NextReloadAsync(Token);

        rig.Dispose();

        Assert.True(reload.IsCancellationRequested, "the reload of a disposed loader keeps running");
    }

    [Fact]
    public async Task ANewerReloadCancelsTheOneStillRunning()
    {
        var host = new HeldReloadHost(testEditor.Context.Reflection);
        var rig = new ProjectRig(testEditor.Context with { Reflection = host });
        await rig.LoadProjectAsync(Track(TestPaths.CopyHelloWorldSample()));
        CancellationToken first = await host.NextReloadAsync(Token);

        Task newer = rig.Actions.Loader.ReloadReflectionAsync();
        CancellationToken second = await host.NextReloadAsync(Token);

        Assert.True(first.IsCancellationRequested, "a superseded reload keeps running");
        Assert.False(second.IsCancellationRequested);
        rig.Dispose();
        await newer.WaitAsync(Token);
    }

    private string Track(string path)
    {
        cleanup.Add(path);
        return path;
    }

    /// <summary>Records the token of each reload and holds the reload until that token is cancelled.</summary>
    private sealed class HeldReloadHost(IReflectionHost inner) : IReflectionHost
    {
        private readonly Channel<CancellationToken> reloads = Channel.CreateUnbounded<CancellationToken>();

        public bool IsLoaded => inner.IsLoaded;

        public Task Loaded => inner.Loaded;

        public IReflectionProvider Provider => inner.Provider;

        public ProjectSnapshot? Snapshot => inner.Snapshot;

        public ReadOnlyObservableCollection<TypeSpecifier> NonStaticTypes => inner.NonStaticTypes;

        public IReadOnlyList<string> LastWarnings => inner.LastWarnings;

        public event EventHandler? Reloaded
        {
            add => inner.Reloaded += value;
            remove => inner.Reloaded -= value;
        }

        public ValueTask<CancellationToken> NextReloadAsync(CancellationToken cancellationToken) => reloads.Reader.ReadAsync(cancellationToken);

        public async Task ReloadAsync(Project project, CancellationToken cancellationToken = default)
        {
            reloads.Writer.TryWrite(cancellationToken);
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }
}
