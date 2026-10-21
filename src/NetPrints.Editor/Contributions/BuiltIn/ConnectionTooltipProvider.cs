using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Graph;

namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The tooltip of a connection: <c>Node.pin → Node.pin</c>, the data type or "execution", and the documentation of both ends (FR-064).</summary>
public sealed class ConnectionTooltipProvider : IEditorTooltipProvider
{
    /// <inheritdoc/>
    public int Order => 0;

    /// <inheritdoc/>
    public TooltipContent? TryProvide(TooltipTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target is not { Kind: TooltipTargetKind.Connection, Subject: TooltipConnection connection })
        {
            return null;
        }

        string title = $"{connection.Source.Node.Name}.{connection.Source.Name} → {connection.Target.Node.Name}.{connection.Target.Name}";
        return new TooltipContent(title, [TypeLine(connection.Source)]);
    }

    /// <inheritdoc/>
    TooltipContent? IEditorTooltipProvider.TryProvide(TooltipTarget target, object editorSubject)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(editorSubject);
        if (target.Kind != TooltipTargetKind.Connection || editorSubject is not ConnectionViewModel connection)
        {
            return TryProvide(target);
        }

        string title = $"{Name(connection.Source)} → {Name(connection.Target)}";
        string[] documented =
        [
            .. new[] { connection.Source.Node, connection.Target.Node }
                .Select(node => (node.Name, Text: node.ToolTip))
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Text))
                .Select(entry => $"{entry.Name}: {entry.Text}"),
        ];
        return new TooltipContent(title, [TypeLine(connection.Source.Pin)], documented.Length == 0 ? null : string.Join(Environment.NewLine, documented));
    }

    private static string TypeLine(NodePin pin) => pin switch
    {
        NodeExecPin => "execution",
        NodeDataPin data => data.PinType.Value?.ToString() ?? "",
        _ => "type",
    };

    private static string Name(NodePinViewModel pin) => $"{pin.Node.Name}.{pin.Pin.Name}";
}
