using NetPrints.Editor.Contributions;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.Navigation;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>
/// The <c>goToSource</c> and <c>goToTarget</c> commands: move the view to a node at one end of a connection. The connection is
/// the parameter, or, without one, the only connection on the relevant side (incoming for source, outgoing for target) of the single selected node.
/// </summary>
/// <param name="end">The end to go to.</param>
public sealed class GoToConnectionEndCommandHandler(ConnectionEnd end) : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) =>
        context.ActiveDocument is not null && ConnectionOf(context) is not null;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        if (ConnectionOf(context) is { } connection && context.ActiveDocument is { } document)
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

    private ConnectionViewModel? ConnectionOf(CommandContext context)
    {
        if (context.Parameter is ConnectionViewModel connection)
        {
            return connection;
        }

        if (context.Parameter is not null || context.ActiveGraph is not { } graph || context.Selection.Nodes is not [NodeViewModel selected])
        {
            return null;
        }

        ConnectionViewModel[] ends = [.. graph.Connections.Where(candidate => (end == ConnectionEnd.Source ? candidate.Target : candidate.Source).Node == selected)];
        return ends.Length == 1 ? ends[0] : null;
    }

    private NodePinViewModel PinOf(ConnectionViewModel connection) => end == ConnectionEnd.Source ? connection.Source : connection.Target;
}
