using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Lifecycle;

/// <summary>
/// Connects a <see cref="BackupService"/> to the unsaved files of a session (FR-024): an edit schedules the class's backup, a save
/// or an undo back to the saved state deletes it, and Discard deletes those of the classes it names. The classes are read on the UI thread.
/// </summary>
public sealed class SessionBackups : IDisposable
{
    private readonly ProjectSessionViewModel session;
    private readonly BackupService service;
    private readonly Func<ClassGraph, CancellationToken, Task<byte[]>> render;
    private readonly IUiDispatcher dispatcher;
    private readonly Dictionary<string, ClassGraph> tracked = new(StringComparer.Ordinal);

    /// <summary>Starts following <paramref name="session"/>.</summary>
    /// <param name="session">The open project's session.</param>
    /// <param name="service">Writes and deletes the backups.</param>
    /// <param name="render">Renders the graph file a save of a class would write; reads the class before its first await.</param>
    /// <param name="dispatcher">Runs the reading of a class on the UI thread.</param>
    public SessionBackups(ProjectSessionViewModel session, BackupService service, Func<ClassGraph, CancellationToken, Task<byte[]>> render, IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(session);
        this.session = session;
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        this.render = render ?? throw new ArgumentNullException(nameof(render));
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        session.ClassEdited += OnClassEdited;
        session.CommandStatesChanged += OnStatesChanged;
        session.Saved += OnSaved;
    }

    /// <summary>Gets the class paths with unsaved changes: the classes followed since an edit or a restore, and the session's dirty ones.</summary>
    public IReadOnlyCollection<string> UnsavedPaths =>
        [.. tracked.Keys.Union(session.Project.Classes.Where(cls => cls.IsDirty).Select(PathOf), StringComparer.Ordinal)];

    /// <summary>Deletes the backups of the given classes and cancels their waits: the user chose not to keep those changes. Other backups stay.</summary>
    /// <param name="classPaths">The class paths the user was asked about.</param>
    public void Discard(IEnumerable<string> classPaths)
    {
        ArgumentNullException.ThrowIfNull(classPaths);
        foreach (string path in classPaths)
        {
            tracked.Remove(path);
            service.Delete(path);
        }
    }

    /// <summary>Starts following a class that was restored from its backup: the backup stays until the class is saved or discarded.</summary>
    /// <param name="cls">A class of the session's project that is unsaved.</param>
    public void Track(ClassGraph cls)
    {
        ArgumentNullException.ThrowIfNull(cls);
        tracked[PathOf(cls)] = cls;
    }

    /// <summary>Writes the backups still waiting.</summary>
    /// <returns>A task that completes when they are written.</returns>
    public Task FlushAsync() => service.FlushAsync();

    /// <summary>Stops following the session; the backups already written stay.</summary>
    public void Dispose()
    {
        session.ClassEdited -= OnClassEdited;
        session.CommandStatesChanged -= OnStatesChanged;
        session.Saved -= OnSaved;
    }

    private void OnClassEdited(object? sender, ClassGraph cls)
    {
        string path = PathOf(cls);
        foreach (string stale in tracked.Where(item => ReferenceEquals(item.Value, cls) && item.Key != path).Select(item => item.Key).ToList())
        {
            tracked.Remove(stale);
            service.Delete(stale);
        }

        tracked[path] = cls;
        service.Schedule(path, () => RenderOnUiThreadAsync(cls));
    }

    // The file a save would write now, not the path the session first saw: a class renamed before its first save is backed up under its final name.
    private string PathOf(ClassGraph cls) => ProjectSessionViewModel.CurrentClassPath(session.Project, cls);

    private void OnStatesChanged(object? sender, EventArgs e) => DropSaved();

    private void OnSaved(object? sender, int files) => DropSaved();

    private void DropSaved()
    {
        foreach (string path in tracked.Where(item => !item.Value.IsDirty).Select(item => item.Key).ToList())
        {
            tracked.Remove(path);
            service.Delete(path);
        }
    }

    private async Task<byte[]?> RenderOnUiThreadAsync(ClassGraph cls)
    {
        Task<byte[]>? rendering = null;
        await dispatcher.InvokeAsync(() =>
        {
            if (cls.IsDirty)
            {
                rendering = render(cls, CancellationToken.None);
            }
        }).ConfigureAwait(true);
        return rendering is null ? null : await rendering.ConfigureAwait(true);
    }
}
