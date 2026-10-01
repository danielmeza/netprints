using System.Text.Json.Serialization;
using NetPrints.Core;
using NetPrints.Extensibility.Nodes;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Mapping;
using NetPrints.Translator;

namespace Fx.Kit;

/// <summary>A node with one exec in and one exec out.</summary>
public sealed class FxNode : Node
{
    /// <summary>Creates the node and adds it to <paramref name="graph"/>.</summary>
    /// <param name="graph">The graph.</param>
    public FxNode(NodeGraph graph)
        : base(graph)
    {
        AddInputExecPin("Exec");
        AddOutputExecPin("Then");
    }
}

/// <summary>The document form of the fixture nodes.</summary>
/// <param name="Id">The node id.</param>
/// <param name="Name">The node name.</param>
/// <param name="Pins">The stored pin states.</param>
public sealed record FxNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins) : NodeDocument(Id, Name, Pins);

/// <summary>The source-generated JSON metadata of the fixture node document.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault)]
[JsonSerializable(typeof(FxNodeDocument))]
public sealed partial class FxKitJsonContext : JsonSerializerContext;

/// <summary>Converts a fixture node of <paramref name="kind"/>.</summary>
/// <param name="kind">The node kind id.</param>
/// <param name="nodeType">The CLR node type.</param>
/// <param name="create">Creates the node in a graph.</param>
public sealed class FxNodeConverter(string kind, Type nodeType, Func<NodeGraph, Node> create) : INodeDocumentConverter
{
    /// <inheritdoc />
    public string Kind => kind;

    /// <inheritdoc />
    public Type NodeType => nodeType;

    /// <inheritdoc />
    public Type DocumentType => typeof(FxNodeDocument);

    /// <inheritdoc />
    public NodeDocument ToDocument(Node node, NodeMappingContext context) => new FxNodeDocument(node.Id, null, null);

    /// <inheritdoc />
    public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context) => create(graph);
}

/// <summary>Translates a fixture node to a console write of <paramref name="text"/>, evaluated at translation time.</summary>
/// <param name="text">The text written; calling it is what runs the fixture's dependency code.</param>
public sealed class FxNodeTranslator(Func<string> text) : INodeTranslator
{
    /// <inheritdoc />
    public void Translate(IExecutionTranslationContext context, Node node, int inputExecPinIndex)
    {
        context.AppendLine($"System.Console.WriteLine(\"{text()}\");");
        context.WriteGotoOutputPinIfNecessary(node.OutputExecPins[0], node.InputExecPins[0]);
    }
}

/// <summary>Builders for the fixtures' node kinds.</summary>
public static class FxKit
{
    /// <summary>Creates a plain <see cref="FxNode"/>.</summary>
    /// <param name="graph">The graph.</param>
    /// <returns>The node.</returns>
    public static Node Create(NodeGraph graph) => new FxNode(graph);

    /// <summary>Describes one node kind.</summary>
    /// <param name="kind">The kind id.</param>
    /// <param name="nodeType">The CLR node type <paramref name="create"/> returns.</param>
    /// <param name="create">Creates the node.</param>
    /// <param name="text">The text the translator writes.</param>
    /// <returns>The descriptor.</returns>
    public static NodeKindDescriptor Kind(string kind, Type nodeType, Func<NodeGraph, Node> create, Func<string> text) =>
        new(kind, nodeType, new FxNodeConverter(kind, nodeType, create), new FxNodeTranslator(text), GraphKinds.Method | GraphKinds.Constructor,
            [new NodeSuggestion("Fixture", kind, null, graph => create(graph))]);

    /// <summary>Describes a plain <see cref="FxNode"/> kind.</summary>
    /// <param name="kind">The kind id.</param>
    /// <param name="text">The text the translator writes.</param>
    /// <returns>The descriptor.</returns>
    public static NodeKindDescriptor Kind(string kind, Func<string> text) => Kind(kind, typeof(FxNode), Create, text);
}

/// <summary>A node library of one kind.</summary>
/// <param name="id">The library id.</param>
/// <param name="kind">The kind it offers.</param>
public sealed class FxNodeLibrary(string id, NodeKindDescriptor kind) : INodeLibrary
{
    /// <inheritdoc />
    public string Id => id;

    /// <inheritdoc />
    public IReadOnlyList<NodeKindDescriptor> NodeKinds { get; } = [kind];
}
