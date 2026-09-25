namespace NetPrints.Editor.Hosting;

/// <summary>
/// Access to the UI thread without depending on a UI toolkit.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>Queues an action on the UI thread.</summary>
    void Post(Action action);

    /// <summary>Runs an action on the UI thread and completes when it has run.</summary>
    Task InvokeAsync(Action action);

    /// <summary>Whether the caller is on the UI thread.</summary>
    bool CheckAccess();
}
