using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>Names of the classes the checks build, and the filler values their nodes get.</summary>
internal static class ConformanceGraphs
{
    /// <summary>The namespace of the classes.</summary>
    public const string Namespace = "Conformance";

    /// <summary>The class name.</summary>
    public const string ClassName = "Subject";

    private const string FillerText = "conformance";

    /// <summary>Gives every input data pin of <paramref name="node"/> that has neither a connection nor a default a value, so the node translates.</summary>
    /// <param name="node">The node to fill.</param>
    /// <param name="graph">The graph the node is in; a pin that cannot hold a value of its own is connected to a literal added here.</param>
    public static void FillInputs(Node node, NodeGraph graph)
    {
        foreach (NodeInputDataPin pin in node.InputDataPins.Where(candidate => candidate.IncomingPin is null && !candidate.UsesExplicitDefaultValue))
        {
            if (!pin.UsesUnconnectedValue)
            {
                GraphUtil.ConnectDataPins(LiteralNode.WithValue(graph, FillerText).ValuePin, pin);
            }
            else if (pin.UnconnectedValue is null)
            {
                pin.UnconnectedValue = DefaultOf(pin.PinType.Value);
            }
        }
    }

    private static object? DefaultOf(BaseType? type) => type switch
    {
        _ when Equals(type, TypeSpecifier.FromType<bool>()) => false,
        _ when Equals(type, TypeSpecifier.FromType<int>()) => 0,
        _ when Equals(type, TypeSpecifier.FromType<long>()) => 0L,
        _ when Equals(type, TypeSpecifier.FromType<float>()) => 0f,
        _ when Equals(type, TypeSpecifier.FromType<double>()) => 0d,
        _ when Equals(type, TypeSpecifier.FromType<string>()) => string.Empty,
        _ => null,
    };
}
