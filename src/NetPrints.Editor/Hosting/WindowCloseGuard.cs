using Avalonia.Controls;
using Microsoft.Extensions.Logging;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Holds the shell window's close while <see cref="ShellProjectActions.ExitNeedsConfirmation"/>: the close is cancelled,
/// <see cref="ShellProjectActions.ConfirmExitAsync"/> asks, and when the answer is to go on the window closes again,
/// this time unhindered. A close made while the answer is pending is cancelled too and joins it.
/// </summary>
internal sealed class WindowCloseGuard : IDisposable
{
    private readonly Window window;
    private readonly ShellProjectActions actions;
    private readonly ILogger logger;
    private Task? pending;

    /// <summary>Guards <paramref name="window"/>.</summary>
    /// <param name="window">The shell window.</param>
    /// <param name="actions">The project flows that ask.</param>
    /// <param name="logger">Logs a failure of the question.</param>
    public WindowCloseGuard(Window window, ShellProjectActions actions, ILogger logger)
    {
        this.window = window;
        this.actions = actions;
        this.logger = logger;
        window.Closing += OnClosing;
    }

    /// <summary>Stops guarding the window.</summary>
    public void Dispose() => window.Closing -= OnClosing;

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!actions.ExitNeedsConfirmation)
        {
            return;
        }

        e.Cancel = true;
        if (pending is not { IsCompleted: false })
        {
            pending = CloseWhenConfirmedAsync();
            pending.Forget(logger);
        }
    }

    private async Task CloseWhenConfirmedAsync()
    {
        if (await actions.ConfirmExitAsync(CancellationToken.None).ConfigureAwait(true))
        {
            window.Close();
        }
    }
}
