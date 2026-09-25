using System.Runtime.CompilerServices;
using Avalonia.Threading;

namespace NetPrints.Editor.Hosting.Avalonia;

/// <summary>
/// Reports exceptions that escape to the UI thread (async void event handlers, unguarded awaits in
/// async commands, unobserved tasks) in the error dialog, so the editor stays usable instead of
/// crashing. Logging is added with the logging infrastructure in P1.
/// </summary>
public sealed class UnhandledExceptionHandler : IDisposable
{
    // An exception from an async void handler reaches the dispatcher (reported there) and also
    // faults the dispatcher operation's task. When that task is finalized, the same exception
    // arrives again as an unobserved task exception; it must not be reported twice.
    private static readonly ConditionalWeakTable<Exception, object> Reported = [];

    private readonly IEditorDialogs dialogs;
    private readonly IUiDispatcher dispatcher;
    private bool reporting;

    public UnhandledExceptionHandler(IEditorDialogs dialogs, IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(dispatcher);
        this.dialogs = dialogs;
        this.dispatcher = dispatcher;

        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        Reported.AddOrUpdate(e.Exception, Reported);
        _ = ReportAsync(e.Exception);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        if (e.Exception.Flatten().InnerExceptions.All(inner => Reported.TryGetValue(inner, out _)))
        {
            return;
        }

        dispatcher.Post(() => _ = ReportAsync(e.Exception));
    }

    private async Task ReportAsync(Exception exception)
    {
        // An error while showing the error must not loop back into this handler.
        if (reporting)
        {
            return;
        }

        reporting = true;
        try
        {
            await dialogs.ShowErrorAsync("Unexpected error", exception.ToString());
        }
        finally
        {
            reporting = false;
        }
    }

    public void Dispose()
    {
        Dispatcher.UIThread.UnhandledException -= OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
    }
}
