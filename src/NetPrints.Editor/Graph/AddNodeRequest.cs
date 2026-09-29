using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Editor.Graph;

/// <summary>
/// Parameters for creating a node: a node of <see cref="NodeType"/> in <see cref="Graph"/> at
/// <see cref="Position"/>. When <see cref="SuggestionPin"/> is set, the new node is connected to it.
/// </summary>
public sealed class AddNodeRequest
{
    /// <summary>Concrete <see cref="Node"/> subclass to instantiate.</summary>
    public Type NodeType { get; }

    /// <summary>Graph to add the node to.</summary>
    public NodeGraph Graph { get; }

    /// <summary>Canvas position for the new node.</summary>
    public GraphPoint Position { get; }

    /// <summary>Pin to connect the new node to, or <see langword="null"/> for none.</summary>
    public NodePin? SuggestionPin { get; }

    /// <summary>Extra arguments passed to <see cref="NodeType"/>'s constructor after the graph.</summary>
    public object[] ConstructorParameters { get; }

    /// <summary>
    /// Creates a node-creation request.
    /// </summary>
    /// <param name="nodeType">Concrete <see cref="Node"/> subclass to instantiate.</param>
    /// <param name="graph">Graph to add the node to.</param>
    /// <param name="position">Canvas position for the new node.</param>
    /// <param name="suggestionPin">Pin to connect the new node to, or <see langword="null"/> for none.</param>
    /// <param name="constructorParameters">Extra arguments passed to <paramref name="nodeType"/>'s constructor after the graph.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="nodeType"/> is not a concrete <see cref="Node"/> subclass, or has no constructor
    /// matching a leading <see cref="NodeGraph"/> parameter followed by <paramref name="constructorParameters"/>'s types.
    /// </exception>
    public AddNodeRequest(Type nodeType, NodeGraph graph, GraphPoint position, NodePin? suggestionPin, params object[] constructorParameters)
    {
        if (!nodeType.IsSubclassOf(typeof(Node)) || nodeType.IsAbstract)
        {
            throw new ArgumentException("Invalid type for node", nameof(nodeType));
        }

        Type[] parameterTypes = [typeof(NodeGraph), .. constructorParameters.Select(p => p.GetType())];
        if (nodeType.GetConstructor(parameterTypes) is null)
        {
            throw new ArgumentException($"Invalid parameters for constructor of {nodeType.FullName}", nameof(constructorParameters));
        }

        NodeType = nodeType;
        Graph = graph;
        Position = position;
        SuggestionPin = suggestionPin;
        ConstructorParameters = constructorParameters;
    }
}
