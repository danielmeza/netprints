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
        adapter.PanelsSuspending += OnPanelsSuspending;
    }

    /// <summary>Saves now, if a change is pending.</summary>
    public void Flush()
    {
        if (pending)
        {
            SaveNow();
        }
    }

    /// <summary>Saves now, pending change or not: what a splitter drag moved raises no change.</summary>
    public void SaveNow()
    {
        if (disposed || adapter.PanelsSuspended)
        {
            return;
        }

        bool userChanged = pending;
        pending = false;
        timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        if (adapter.Layout.VisibleDockables?.Any() != true)
        {
            return;
        }

        store.SaveLayout(LayoutSerializer.ToState(adapter.CaptureLayout()), userChanged);
    }

    /// <summary>Stops following the layout.</summary>
    public void Dispose()
    {
        disposed = true;
        adapter.LayoutChanged -= OnLayoutChanged;
        adapter.PanelsSuspending -= OnPanelsSuspending;
        timer.Dispose();
    }

    private void OnLayoutChanged(object? sender, EventArgs e)
    {
        if (disposed || adapter.PanelsSuspended)
        {
            return;
        }

        pending = true;
        timer.Change(delay, Timeout.InfiniteTimeSpan);
    }

    private void OnPanelsSuspending(object? sender, EventArgs e) => Flush();
}
