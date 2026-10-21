using System.Globalization;
using System.Text.Json.Serialization;
using Fx.Kit;
using NetPrints.Core;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Nodes;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Mapping;

namespace Fx.Fail.RoundTrip;

/// <summary>The document form of the node: the usual members and a stamp that grows with every write.</summary>
/// <param name="Id">The node id.</param>
/// <param name="Name">The node name.</param>
/// <param name="Pins">The stored pin states.</param>
/// <param name="Stamp">The number of documents written so far.</param>
public sealed record StampDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins, string Stamp) : NodeDocument(Id, Name, Pins);

/// <summary>The source-generated JSON metadata of <see cref="StampDocument"/>.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault)]
[JsonSerializable(typeof(StampDocument))]
public sealed partial class StampJsonContext : JsonSerializerContext;

/// <summary>A converter that stamps each document it writes with a number that grows.</summary>
public sealed class StampingConverter : INodeDocumentConverter
{
    private const string CounterKey = "fx.fail.roundtrip.writes";

    /// <inheritdoc />
    public string Kind => FailRoundTripExtension.PingKind;

    /// <inheritdoc />
    public Type NodeType => typeof(FxNode);

    /// <inheritdoc />
    public Type DocumentType => typeof(StampDocument);

    /// <inheritdoc />
    public NodeDocument ToDocument(Node node, NodeMappingContext context)
    {
        int writes = AppDomain.CurrentDomain.GetData(CounterKey) is int previous ? previous + 1 : 1;
        AppDomain.CurrentDomain.SetData(CounterKey, writes);
        return new StampDocument(node.Id, null, null, writes.ToString(CultureInfo.InvariantCulture));
    }

    /// <inheritdoc />
    public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context) => new FxNode(graph);
}

/// <summary>An extension whose node kind does not survive a save and load unchanged.</summary>
public sealed class FailRoundTripExtension : INetPrintsExtension
{
    /// <summary>The kind id of the node.</summary>
    public const string PingKind = "fx.fail.roundtrip/Ping";

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var kind = new NodeKindDescriptor(
            PingKind, typeof(FxNode), new StampingConverter(), new FxNodeTranslator(() => "ping"), GraphKinds.Method | GraphKinds.Constructor,
            [new NodeSuggestion("Fixture", PingKind, null, graph => new FxNode(graph))]);
        builder
            .AddNodeLibrary(new FxNodeLibrary("fx.fail.roundtrip", kind))
            .AddJsonTypeInfoResolver(StampJsonContext.Default);
    }
}
