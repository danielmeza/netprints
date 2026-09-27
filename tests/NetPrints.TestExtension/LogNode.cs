using System.Text.Json.Serialization;
using NetPrints.Core;
using NetPrints.Extensibility.Nodes;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Mapping;
using NetPrints.Translator;

namespace NetPrints.TestExtension;

/// <summary>A node with an exec in and out and one data input, translated to a console write.</summary>
public sealed class LogNode : Node
{
    /// <summary>Creates the node and adds it to <paramref name="graph"/>.</summary>
    /// <param name="graph">The graph.</param>
    public LogNode(NodeGraph graph)
        : base(graph)
    {
        AddInputExecPin("Exec");
        AddInputDataPin("Value", TypeSpecifier.FromType<object>());
        AddOutputExecPin("Then");
    }

    /// <summary>The value written.</summary>
    public NodeInputDataPin ValuePin => InputDataPins[0];
}

/// <summary>The document form of <see cref="LogNode"/>.</summary>
/// <param name="Id">The node id.</param>
/// <param name="Name">The node name.</param>
/// <param name="Pins">The stored pin states.</param>
public sealed record LogNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins) : NodeDocument(Id, Name, Pins);

/// <summary>Converts <see cref="LogNode"/>.</summary>
public sealed class LogNodeConverter : INodeDocumentConverter
{
    /// <summary>The kind id.</summary>
    public const string KindId = "netprints.test/Log";

    /// <inheritdoc />
    public string Kind => KindId;

    /// <inheritdoc />
    public Type NodeType => typeof(LogNode);

    /// <inheritdoc />
    public Type DocumentType => typeof(LogNodeDocument);

    /// <inheritdoc />
    public NodeDocument ToDocument(Node node, NodeMappingContext context) => new LogNodeDocument(node.Id, null, null);

    /// <inheritdoc />
    public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context) => new LogNode(graph);
}

/// <summary>Translates <see cref="LogNode"/> to <c>System.Console.WriteLine(&lt;in&gt;)</c>.</summary>
public sealed class LogNodeTranslator : INodeTranslator
{
    /// <inheritdoc />
    public void Translate(IExecutionTranslationContext context, Node node, int inputExecPinIndex)
    {
        var log = (LogNode)node;
        context.TranslateDependentPureNodes(log);
        context.AppendLine($"System.Console.WriteLine({context.GetPinIncomingValue(log.ValuePin)});");
        context.WriteGotoOutputPinIfNecessary(log.OutputExecPins[0], log.InputExecPins[0]);
    }
}

/// <summary>The node kinds of the test extension.</summary>
public sealed class TestNodeLibrary : INodeLibrary
{
    /// <inheritdoc />
    public string Id => TestExtension.Id;

    /// <inheritdoc />
    public IReadOnlyList<NodeKindDescriptor> NodeKinds { get; } =
    [
        new NodeKindDescriptor(
            LogNodeConverter.KindId,
            typeof(LogNode),
            new LogNodeConverter(),
            new LogNodeTranslator(),
            GraphKinds.Method | GraphKinds.Constructor,
            [new NodeSuggestion("Test", "Log", null, graph => new LogNode(graph))]),
    ];
}

/// <summary>The extension's own JSON metadata: its node document and its settings.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault)]
[JsonSerializable(typeof(LogNodeDocument))]
[JsonSerializable(typeof(TestSettings))]
public sealed partial class TestJsonContext : JsonSerializerContext;
