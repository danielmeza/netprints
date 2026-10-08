using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>navigateBack</c> and <c>navigateForward</c> commands: restore the previous or the next view of the navigation history.</summary>
/// <param name="forward"><see langword="true"/> for Forward, <see langword="false"/> for Back.</param>
public sealed class NavigateHistoryCommandHandler(bool forward) : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) =>
        forward ? context.Shell.Navigation.CanGoForward : context.Shell.Navigation.CanGoBack;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        if (forward)
        {
            context.Shell.Navigation.GoForward();
        }
        else
        {
            context.Shell.Navigation.GoBack();
        }

        return Task.CompletedTask;
    }
}
