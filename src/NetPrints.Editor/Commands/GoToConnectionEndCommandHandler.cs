using NetPrints.Editor.Contributions;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.Navigation;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>goToSource</c> and <c>goToTarget</c> commands: move the view to a node at one end of the connection passed as the parameter.</summary>
/// <param name="end">The end to go to.</param>
public sealed class GoToConnectionEndCommandHandler(ConnectionEnd end) : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) =>
        context.Parameter is ConnectionViewModel && context.ActiveDocument is not null;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        if (context.Parameter is ConnectionViewModel connection && context.ActiveDocument is { } document)
        {
            context.Shell.Navigation.NavigateTo(new NavigationTarget(document, PinOf(connection).Node.Node.Id));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public string? DynamicLabel(CommandContext context)
    {
        if (context.Parameter is not ConnectionViewModel connection)
        {
            return null;
        }

        NodePinViewModel pin = PinOf(connection);
        return $"{(end == ConnectionEnd.Source ? "Go to source" : "Go to target")} ({pin.Node.Name}.{pin.Pin.Name})";
    }

    private NodePinViewModel PinOf(ConnectionViewModel connection) => end == ConnectionEnd.Source ? connection.Source : connection.Target;
}
