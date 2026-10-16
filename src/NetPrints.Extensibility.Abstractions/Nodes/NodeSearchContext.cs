using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Projects;
using NetPrints.Reflection;

namespace NetPrints.Extensibility.Nodes;

/// <summary>What a provider needs to know about the search being built: the pin, the graph, its class and the reflection provider.</summary>
[Experimental(ExperimentalApiIds.NodeSearchProviders, UrlFormat = ExperimentalApiIds.UrlFormat)]
public sealed class NodeSearchContext
{
    /// <summary>Initializes a new instance of the <see cref="NodeSearchContext"/> class.</summary>
    /// <param name="graph">The graph the node is added to.</param>
    /// <param name="pin">The pin the search was opened from, or <see langword="null"/> for the canvas.</param>
    /// <param name="reflection">The reflection provider of the open project.</param>
    /// <param name="nodeKinds">The node kinds of the extension registry in use.</param>
    public NodeSearchContext(NodeGraph graph, NodePin? pin, IReflectionProvider reflection, IReadOnlyList<NodeKindDescriptor> nodeKinds)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(reflection);
        ArgumentNullException.ThrowIfNull(nodeKinds);
        Pin = pin;
        Graph = graph;
        Reflection = reflection;
        Class = graph.Class;
        ClassType = Class?.Type;
        BaseTypes = Class?.AllBaseTypes.ToList() ?? [];
        GraphKind = NodeGraphKinds.Of(graph);
        NodeKinds = nodeKinds;
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

    /// <summary>Gets the C# target of the open project; <see cref="CSharpTarget.Unconstrained"/> for a graph with no project.</summary>
    public CSharpTarget Target => Graph.Project?.CSharpTarget ?? CSharpTarget.Unconstrained;

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
