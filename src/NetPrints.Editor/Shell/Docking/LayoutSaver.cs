using NetPrints.Editor.Hosting;
using NetPrints.Editor.State;

namespace NetPrints.Editor.Shell.Docking;

/// <summary>Saves the dock layout to <c>layout.json</c> a moment after it last changed.</summary>
internal sealed class LayoutSaver : IDisposable
{
    private readonly DockShellAdapter adapter;
    private readonly IEditorStateStore store;
    private readonly IUiDispatcher dispatcher;
    private readonly TimeSpan delay;
    private readonly ITimer timer;
    private volatile bool pending;
    private volatile bool disposed;

    /// <summary>Creates the saver and starts following the adapter.</summary>
    /// <param name="adapter">The layout to save.</param>
    /// <param name="store">Where the layout is saved.</param>
    /// <param name="time">The clock of the delay.</param>
    /// <param name="dispatcher">Brings the save to the UI thread, where the layout may be read.</param>
    /// <param name="delay">The quiet time that triggers a save.</param>
    public LayoutSaver(DockShellAdapter adapter, IEditorStateStore store, TimeProvider time, IUiDispatcher dispatcher, TimeSpan delay)
    {
        this.adapter = adapter;
        this.store = store;
        this.dispatcher = dispatcher;
        this.delay = delay;
        timer = time.CreateTimer(_ => this.dispatcher.Post(Flush), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        adapter.LayoutChanged += OnLayoutChanged;
    }

    /// <summary>Saves now, if a change is pending.</summary>
    public void Flush()
    {
        if (disposed || !pending)
        {
            return;
        }

        pending = false;
        timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        try
        {
            store.SaveLayout(LayoutSerializer.ToState(adapter.CaptureLayout()));
        }
        catch (InvalidOperationException)
        {
            // A layout with no main content has nothing worth keeping.
        }
    }

    /// <summary>Stops following the layout.</summary>
    public void Dispose()
    {
        disposed = true;
        adapter.LayoutChanged -= OnLayoutChanged;
        timer.Dispose();
    }

    private void OnLayoutChanged(object? sender, EventArgs e)
    {
        if (disposed)
        {
            return;
        }

        pending = true;
        timer.Change(delay, Timeout.InfiniteTimeSpan);
    }
}
