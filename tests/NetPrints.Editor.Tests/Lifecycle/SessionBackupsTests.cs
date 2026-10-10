using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NetPrints.Core;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.Shell;
using NetPrints.Editor.State;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.State;
using NetPrints.Serialization;

namespace NetPrints.Editor.Tests.Lifecycle;

/// <summary>Backups follow the unsaved changes of a real session (FR-024, FR-026).</summary>
public sealed class SessionBackupsTests : IAsyncDisposable
{
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(30);

    private readonly TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly InMemoryEditorFileSystem fs = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero));
    private readonly EditorDataPaths paths = new("state-root");
    private readonly List<IDisposable> disposables = [];
    private readonly List<string> cleanup = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        disposables.ForEach(item => item.Dispose());
        cleanup.ForEach(TestPaths.TryDelete);
        await editor.DisposeAsync();
    }

    private async Task<(ProjectSessionViewModel Session, SessionBackups Backups, BackupService Service)> OpenAsync()
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        ProjectLoadResult loaded = await editor.Persistence.LoadAsync(path, Token);
        var session = new ProjectSessionViewModel(loaded.Project, editor.Context);
        var service = new BackupService(paths, fs, time, path, Delay, NullLogger<BackupService>.Instance, _ => { });
        var backups = new SessionBackups(session, service, (cls, token) => editor.Persistence.RenderClassAsync(cls, token), editor.Context.Dispatcher);
        disposables.AddRange([backups, service, session]);
        return (session, backups, service);
    }

    [Fact]
    public async Task AnEditedClassIsBackedUpWithTheJsonASaveWouldWrite()
    {
        (ProjectSessionViewModel session, _, BackupService service) = await OpenAsync();
        ClassGraph cls = session.Project.Classes.Single();

        session.ContextFor(cls).CreateVariable();
        time.Advance(Delay);

        BackupEntry entry = Assert.Single(service.List());
        Assert.Equal(session.ClassPathOf(cls), entry.OriginalPath);
        Assert.Equal(await editor.Persistence.RenderClassAsync(cls, Token), service.Read(entry));
    }

    [Fact]
    public async Task EachEditRestartsTheWait()
    {
        (ProjectSessionViewModel session, _, BackupService service) = await OpenAsync();
        ClassContext context = session.ContextFor(session.Project.Classes.Single());

        context.CreateVariable();
        time.Advance(TimeSpan.FromSeconds(20));
        context.CreateVariable();
        time.Advance(TimeSpan.FromSeconds(20));
        Assert.Empty(service.List());

        time.Advance(TimeSpan.FromSeconds(10));
        Assert.Single(service.List());
    }

    [Fact]
    public async Task SavingDeletesTheBackup()
    {
        (ProjectSessionViewModel session, _, BackupService service) = await OpenAsync();
        session.ContextFor(session.Project.Classes.Single()).CreateVariable();
        time.Advance(Delay);
        Assert.Single(service.List());

        Assert.True(await session.SaveAllAsync());

        Assert.Empty(service.List());
    }

    [Fact]
    public async Task SavingBeforeTheWaitEndsWritesNoBackup()
    {
        (ProjectSessionViewModel session, _, BackupService service) = await OpenAsync();
        session.ContextFor(session.Project.Classes.Single()).CreateVariable();

        Assert.True(await session.SaveAllAsync());
        time.Advance(Delay);

        Assert.Empty(service.List());
    }

    [Fact]
    public async Task UndoingBackToTheSavedStateDeletesTheBackup()
    {
        (ProjectSessionViewModel session, _, BackupService service) = await OpenAsync();
        ClassContext context = session.ContextFor(session.Project.Classes.Single());
        context.CreateVariable();
        time.Advance(Delay);
        Assert.Single(service.List());

        context.UndoRedo.Undo();

        Assert.Empty(service.List());
    }

    [Fact]
    public async Task DiscardDeletesTheBackupsOfTheGivenClassesAndCancelsTheirWaits()
    {
        (ProjectSessionViewModel session, SessionBackups backups, BackupService service) = await OpenAsync();
        ClassContext context = session.ContextFor(session.Project.Classes.Single());
        context.CreateVariable();
        time.Advance(Delay);
        context.CreateVariable();

        backups.Discard(backups.UnsavedPaths);
        time.Advance(Delay);

        Assert.Empty(service.List());
    }

    [Fact]
    public async Task DiscardLeavesTheBackupsOfOtherPaths()
    {
        (ProjectSessionViewModel session, SessionBackups backups, BackupService service) = await OpenAsync();
        session.ContextFor(session.Project.Classes.Single()).CreateVariable();
        service.Schedule("Other.netpc.json", () => Task.FromResult<byte[]?>([1]));
        time.Advance(Delay);
        Assert.Equal(2, service.List().Count);

        backups.Discard(backups.UnsavedPaths);

        Assert.Equal("Other.netpc.json", Assert.Single(service.List()).OriginalPath);
    }

    [Fact]
    public async Task FlushWritesTheBackupsStillWaiting()
    {
        (ProjectSessionViewModel session, SessionBackups backups, BackupService service) = await OpenAsync();
        session.ContextFor(session.Project.Classes.Single()).CreateVariable();

        await backups.FlushAsync();

        Assert.Single(service.List());
    }

    [Fact(Timeout = 30000)]
    public async Task AnEditMadeDuringASaveKeepsItsBackup()
    {
        List<GatedDocumentStore> stores = [];
        ProjectPersistence persistence = TestEditor.CreatePersistence(editor.Projects, store =>
        {
            var gated = new GatedDocumentStore(store);
            stores.Add(gated);
            return gated;
        });
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        ProjectLoadResult loaded = await editor.Persistence.LoadAsync(path, Token);
        var session = new ProjectSessionViewModel(loaded.Project, editor.Context with { Persistence = persistence });
        var service = new BackupService(paths, fs, time, path, Delay, NullLogger<BackupService>.Instance, _ => { });
        var backups = new SessionBackups(session, service, (cls, token) => persistence.RenderClassAsync(cls, token), editor.Context.Dispatcher);
        disposables.AddRange([backups, service, session]);
        ClassContext context = session.ContextFor(session.Project.Classes.Single());
        context.CreateVariable();

        Task<bool> save = session.SaveAllAsync();
        try
        {
            await stores.Single().FirstWriteStarted.WaitAsync(TimeSpan.FromSeconds(10), Token);
            context.CreateVariable();
        }
        finally
        {
            stores.ForEach(store => store.Release());
        }

        await save.WaitAsync(TimeSpan.FromSeconds(10), Token);
        time.Advance(Delay);

        Assert.True(session.Project.Classes.Single().IsDirty);
        BackupEntry entry = Assert.Single(service.List());
        Assert.Equal(await persistence.RenderClassAsync(session.Project.Classes.Single(), Token), service.Read(entry));
    }
}
