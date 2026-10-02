using NetPrints.Core;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Serialization;

namespace NetPrints.Editor.Tests.Shell;

public sealed class ClassContextTests : IAsyncDisposable
{
    private readonly TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly List<string> cleanup = [];

    public async ValueTask DisposeAsync()
    {
        cleanup.ForEach(TestPaths.TryDelete);
        await editor.DisposeAsync();
    }

    private async Task<Project> LoadSampleAsync()
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        ProjectLoadResult loaded = await editor.Persistence.LoadAsync(path, TestContext.Current.CancellationToken);
        return loaded.Project;
    }

    [Fact]
    public async Task EachClassHasOneContextOnTheStackOfTheSession()
    {
        Project project = await LoadSampleAsync();
        ClassGraph other = project.CreateNewClass(DefaultProjectProfile.Instance);
        using var session = new ProjectSessionViewModel(project, editor.Context);
        ClassGraph first = project.Classes[0];

        ClassContext context = session.ContextFor(first);

        Assert.Same(context, session.ContextFor(first));
        Assert.NotSame(context, session.ContextFor(other));
        Assert.Same(first, context.Class);
        Assert.Same(session.UndoStackFor(first), context.UndoRedo);
        Assert.Same(context.UndoRedo, context.Services.UndoRedo);
    }

    [Fact]
    public async Task AnEditThroughTheContextIsUndoneByTheSessionStackAndMarksTheClassDirty()
    {
        Project project = await LoadSampleAsync();
        await editor.Persistence.SaveAsync(project, _ => "// generated\n", TestContext.Current.CancellationToken);
        using var session = new ProjectSessionViewModel(project, editor.Context);
        ClassGraph cls = project.Classes[0];
        ClassContext context = session.ContextFor(cls);
        Assert.False(cls.IsDirty);
        int before = cls.Variables.Count;

        context.CreateVariable();

        Assert.Equal(before + 1, cls.Variables.Count);
        Assert.True(cls.IsDirty);
        Assert.True(session.UndoStackFor(cls).CanUndo);
        session.UndoStackFor(cls).Undo();
        Assert.Equal(before, cls.Variables.Count);
    }

    [Fact]
    public async Task RemovingAClassDisposesItsContext()
    {
        Project project = await LoadSampleAsync();
        ClassGraph other = project.CreateNewClass(DefaultProjectProfile.Instance);
        using var session = new ProjectSessionViewModel(project, editor.Context);
        ClassContext kept = session.ContextFor(project.Classes[0]);
        ClassContext removed = session.ContextFor(other);

        project.Classes.Remove(other);

        Assert.True(removed.IsDisposed);
        Assert.False(kept.IsDisposed);
    }

    [Fact]
    public async Task DisposingTheSessionDisposesEveryContext()
    {
        Project project = await LoadSampleAsync();
        var session = new ProjectSessionViewModel(project, editor.Context);
        ClassContext context = session.ContextFor(project.Classes[0]);

        session.Dispose();

        Assert.True(context.IsDisposed);
    }
}
