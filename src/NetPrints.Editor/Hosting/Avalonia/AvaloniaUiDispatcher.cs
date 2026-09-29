using Avalonia.Threading;
using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Hosting.Avalonia;

/// <summary><see cref="IUiDispatcher"/> over <see cref="Dispatcher.UIThread"/>.</summary>
public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    /// <inheritdoc/>
    public void Post(Action action) => Dispatcher.UIThread.Post(action);

    /// <inheritdoc/>
    public Task InvokeAsync(Action action) => Dispatcher.UIThread.InvokeAsync(action).GetTask();

    /// <inheritdoc/>
    public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();
}
