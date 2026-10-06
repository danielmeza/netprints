using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Lifecycle;

/// <summary>
/// Connects a <see cref="BackupService"/> to the unsaved files of a session (FR-024): an edit schedules the class's backup, a save
/// or an undo back to the saved state deletes it, and Discard deletes them all. The classes are read on the UI thread.
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

    /// <summary>Deletes every backup of the project: the user chose not to keep the unsaved changes.</summary>
    public void DiscardAll()
    {
        tracked.Clear();
        service.DeleteAll();
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
        string path = session.ClassPathOf(cls);
        tracked[path] = cls;
        service.Schedule(path, () => RenderOnUiThreadAsync(cls));
    }

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
