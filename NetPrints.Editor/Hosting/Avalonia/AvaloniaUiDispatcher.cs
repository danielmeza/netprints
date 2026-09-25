using Avalonia.Threading;
using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Hosting.Avalonia;

/// <summary><see cref="IUiDispatcher"/> over <see cref="Dispatcher.UIThread"/>.</summary>
public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public void Post(Action action) => Dispatcher.UIThread.Post(action);

    public Task InvokeAsync(Action action) => Dispatcher.UIThread.InvokeAsync(action).GetTask();

    public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();
}
