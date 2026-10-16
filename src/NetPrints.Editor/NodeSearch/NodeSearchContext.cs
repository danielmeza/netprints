using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Extensibility.Nodes;
using NetPrints.Graph;
using NetPrints.Reflection;

namespace NetPrints.Editor.NodeSearch;

/// <summary>What a provider needs to know about the search being built: the pin, the graph, its class and the reflection provider.</summary>
internal sealed class NodeSearchContext
{
    /// <summary>Initializes a new instance of the <see cref="NodeSearchContext"/> class.</summary>
    /// <param name="graph">The graph view model the search is opened for.</param>
    /// <param name="pin">The pin the search was opened from, or <see langword="null"/> for the canvas.</param>
    public NodeSearchContext(NodeGraphViewModel graph, NodePin? pin)
    {
        ArgumentNullException.ThrowIfNull(graph);
        Pin = pin;
        Graph = graph.Graph;
        Reflection = graph.Context.Reflection.Provider;
        Class = Graph.Class;
        ClassType = Class?.Type;
        BaseTypes = Class?.AllBaseTypes.ToList() ?? [];
        GraphKind = NodeGraphKinds.Of(Graph);
        NodeKinds = graph.Context.Extensions.Current.NodeKinds;
    }

    /// <summary>Gets the pin the search was opened from, or <see langword="null"/> for the canvas.</summary>
    public NodePin? Pin { get; }

    /// <summary>Gets the type of the output data pin the search was opened from, or <see langword="null"/> for any other pin.</summary>
    public TypeSpecifier? OutputDataType => Pin is NodeOutputDataPin { PinType.Value: TypeSpecifier type } ? type : null;

    /// <summary>Gets the type of the input data pin the search was opened from, or <see langword="null"/> for any other pin.</summary>
    public TypeSpecifier? InputDataType => Pin is NodeInputDataPin { PinType.Value: TypeSpecifier type } ? type : null;

    /// <summary>Gets a value indicating whether the search was opened from an execution pin.</summary>
    public bool IsExecPin => Pin is NodeOutputExecPin or NodeInputExecPin;

    /// <summary>Gets a value indicating whether the search was opened from the canvas of an execution graph.</summary>
    public bool IsExecutionCanvas => Pin is null && Graph is ExecutionGraph;

    /// <summary>Gets the graph the node is added to.</summary>
    public NodeGraph Graph { get; }

    /// <summary>Gets the kind of <see cref="Graph"/>.</summary>
    public GraphKinds GraphKind { get; }

    /// <summary>Gets the class of the graph, or <see langword="null"/> when it has none.</summary>
    public ClassGraph? Class { get; }

    /// <summary>Gets the type of <see cref="Class"/>, or <see langword="null"/> when there is no class.</summary>
    public TypeSpecifier? ClassType { get; }

    /// <summary>Gets the class and its base types, as the search offers their members.</summary>
    public IReadOnlyList<TypeSpecifier> BaseTypes { get; }

    /// <summary>Gets the reflection provider of the open project.</summary>
    public IReflectionProvider Reflection { get; }

    /// <summary>Gets the node kinds of the extension registry in use.</summary>
    public IReadOnlyList<NodeKindDescriptor> NodeKinds { get; }

    /// <summary>Gets the node kinds that offer suggestions in the graph.</summary>
    public IReadOnlyList<NodeKindDescriptor> SuggestedKinds =>
        GraphKind == GraphKinds.None
            ? []
            : NodeKinds.Where(kind => kind.Suggestions.Count > 0 && kind.AllowedIn.HasFlag(GraphKind)).ToList();

    /// <summary>Creates a method query limited to what the class sees.</summary>
    /// <returns>The query.</returns>
    public ReflectionProviderMethodQuery MethodQuery() => ClassType is null ? new() : new ReflectionProviderMethodQuery().WithVisibleFrom(ClassType);

    /// <summary>Creates a variable query limited to what the class sees.</summary>
    /// <returns>The query.</returns>
    public ReflectionProviderVariableQuery VariableQuery() => ClassType is null ? new() : new ReflectionProviderVariableQuery().WithVisibleFrom(ClassType);
}
