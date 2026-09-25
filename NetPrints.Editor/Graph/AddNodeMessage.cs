using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Editor.Graph;

/// <summary>
/// Requests creation of a node of <see cref="NodeType"/> in <see cref="Graph"/> at
/// <see cref="Position"/>. When <see cref="SuggestionPin"/> is set, the new node is connected to it.
/// </summary>
public sealed class AddNodeMessage
{
    public Type NodeType { get; }
    public NodeGraph Graph { get; }
    public GraphPoint Position { get; }
    public NodePin? SuggestionPin { get; }
    public object[] ConstructorParameters { get; }

    public AddNodeMessage(Type nodeType, NodeGraph graph, GraphPoint position, NodePin? suggestionPin, params object[] constructorParameters)
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
