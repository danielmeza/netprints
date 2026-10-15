using NetPrints.Core;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Serialization;

namespace NetPrints.Editor.Tests.Lifecycle;

public sealed class UnsavedChangesTrackerTests : IAsyncDisposable
{
    private readonly TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly List<ProjectSessionViewModel> sessions = [];
    private readonly List<string> cleanup = [];

    public async ValueTask DisposeAsync()
    {
        sessions.ForEach(session => session.Dispose());
        cleanup.ForEach(TestPaths.TryDelete);
        await editor.DisposeAsync();
    }

    private async Task<ProjectSessionViewModel> OpenAsync()
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        ProjectLoadResult loaded = await editor.Persistence.LoadAsync(path, TestContext.Current.CancellationToken);
        var session = new ProjectSessionViewModel(loaded.Project, editor.Context);
        sessions.Add(session);
        return session;
    }

    [Fact]
    public async Task ALoadedProjectHasNoUnsavedFiles()
    {
        ProjectSessionViewModel session = await OpenAsync();

        Assert.False(session.Unsaved.HasUnsavedFiles);
        Assert.Empty(session.Unsaved.UnsavedFiles);
        Assert.False(session.Unsaved.IsUnsaved(session.Project.Classes.Single()));
    }

    [Fact]
    public async Task AClassFileIsUnsavedAfterAnyChangeAndSavedAfterASuccessfulSave()
    {
        ProjectSessionViewModel session = await OpenAsync();
        ClassGraph cls = session.Project.Classes.Single();

        session.ContextFor(cls).CreateVariable();
        Assert.True(session.Unsaved.IsUnsaved(cls));

        Assert.True(await session.SaveAllAsync());
        Assert.False(session.Unsaved.IsUnsaved(cls));
    }

    [Fact]
    public async Task UndoingBackToTheSavedMarkerMakesTheClassSavedAgain()
    {
        ProjectSessionViewModel session = await OpenAsync();
        ClassGraph cls = session.Project.Classes.Single();
        ClassContext context = session.ContextFor(cls);

        context.CreateVariable();
        Assert.True(session.Unsaved.IsUnsaved(cls));
        context.UndoRedo.Undo();
        Assert.False(session.Unsaved.IsUnsaved(cls), "undo returned to the loaded state");

        context.UndoRedo.Redo();
        Assert.True(session.Unsaved.IsUnsaved(cls));
        Assert.True(await session.SaveAllAsync());
        context.CreateVariable();
        Assert.True(session.Unsaved.IsUnsaved(cls));
        context.UndoRedo.Undo();
        Assert.False(session.Unsaved.IsUnsaved(cls), "undo returned to the saved state");
        context.UndoRedo.Undo();
        Assert.True(session.Unsaved.IsUnsaved(cls), "undo went past the saved state");
    }

    [Fact]
    public async Task ANonUndoableEditMarksTheClassUnsavedUntilItIsSavedEvenIfUndoReturnsToTheMarker()
    {
        ProjectSessionViewModel session = await OpenAsync();
        ClassGraph cls = session.Project.Classes.Single();
        ClassContext context = session.ContextFor(cls);

        context.ClassInspector.Name = "Renamed";
        Assert.True(session.Unsaved.IsUnsaved(cls));

        context.CreateVariable();
        context.UndoRedo.Undo();
        Assert.True(session.Unsaved.IsUnsaved(cls), "the rename is not on the undo stack");

        Assert.True(await session.SaveAllAsync());
        Assert.False(session.Unsaved.IsUnsaved(cls));
    }

    [Fact]
    public async Task UnsavedFilesListsThePathKindAndDisplayNameOfEachFile()
    {
        ProjectSessionViewModel session = await OpenAsync();
        ClassGraph loaded = session.Project.Classes.Single();
        ClassGraph created = session.Project.CreateNewClass(DefaultProjectProfile.Instance);

        UnsavedFile file = Assert.Single(session.Unsaved.UnsavedFiles);
        Assert.Equal(new UnsavedFile(session.ClassPathOf(created), UnsavedFileKind.Class, created.Name), file);
        Assert.True(session.Unsaved.HasUnsavedFiles);

        session.ContextFor(loaded).CreateVariable();
        Assert.Equal([loaded.Name, created.Name], session.Unsaved.UnsavedFiles.Select(f => f.DisplayName));
    }

    [Fact]
    public async Task TheProjectFileIsUnsavedOnlyWhileAProjectLevelChangeIsPending()
    {
        ProjectSessionViewModel session = await OpenAsync();
        Assert.False(session.Unsaved.IsProjectFileUnsaved);

        session.Unsaved.MarkProjectChangePending();
        UnsavedFile file = Assert.Single(session.Unsaved.UnsavedFiles);
        Assert.Equal(UnsavedFileKind.Project, file.Kind);
        Assert.Equal(session.Project.Name, file.DisplayName);
        Assert.Equal(Path.GetFileName(session.ProjectFilePath), file.Path);

        Assert.True(await session.SaveAllAsync());
        Assert.False(session.Unsaved.IsProjectFileUnsaved);
        Assert.Empty(session.Unsaved.UnsavedFiles);
    }
}
