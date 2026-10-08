using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Graph;

namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The tooltip of a connection: <c>Node.pin → Node.pin</c>, the data type or "execution", and the documentation of both ends (FR-064).</summary>
public sealed class ConnectionTooltipProvider : ITooltipProvider
{
    /// <inheritdoc/>
    public int Order => 0;

    /// <inheritdoc/>
    public TooltipContent? TryProvide(TooltipTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target is not { Kind: TooltipTargetKind.Connection, Subject: ConnectionViewModel connection })
        {
            return null;
        }

        string title = $"{Name(connection.Source)} → {Name(connection.Target)}";
        string type = connection.Source.Pin switch
        {
            NodeExecPin => "execution",
            NodeDataPin data => data.PinType.Value?.ToString() ?? "",
            _ => "type",
        };

        string[] documented =
        [
            .. new[] { connection.Source.Node, connection.Target.Node }
                .Select(node => (node.Name, Text: node.ToolTip))
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Text))
                .Select(entry => $"{entry.Name}: {entry.Text}"),
        ];
        return new TooltipContent(title, [type], documented.Length == 0 ? null : string.Join(Environment.NewLine, documented));
    }

    private static string Name(NodePinViewModel pin) => $"{pin.Node.Name}.{pin.Pin.Name}";
}
