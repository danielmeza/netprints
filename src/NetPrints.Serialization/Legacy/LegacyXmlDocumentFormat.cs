#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Mapping;

namespace NetPrints.Serialization.Legacy;

/// <summary>
/// Reads legacy DataContract XML class graphs (<c>.netpc</c>, pre-P1) into their <see cref="ClassDocument"/>
/// form (document-format.md §2.2, §3). A legacy class has no node or member ids of its own, so each is
/// given deterministic ones on load: every graph's nodes (<see cref="NodeGraph.AssignLegacyNodeIds"/>)
/// and the class's members, seeded from its full name (<see cref="ClassGraph.AssignLegacyMemberIds"/>),
/// so re-importing the same file always assigns the same ids. Every graph's inferred types are then
/// settled (<see cref="GraphTypeInference.Relax"/>), matching what <c>MethodGraph.OnDeserialized</c>'s
/// relaxation loop used to do, before the class is mapped to its document form. Read-only: legacy files
/// are never written back out.
/// </summary>
public sealed class LegacyXmlDocumentFormat : IDocumentFormat
{
    private static readonly DataContractSerializer Serializer = new(typeof(ClassGraph), new DataContractSerializerSettings
    {
        PreserveObjectReferences = true,
        MaxItemsInObjectGraph = int.MaxValue,
    });

    private readonly IDocumentMapper mapper;

    /// <summary>
    /// Creates a legacy XML document format.
    /// </summary>
    /// <param name="mapper">Mapper used to convert the deserialized class to its document form.</param>
    public LegacyXmlDocumentFormat(IDocumentMapper mapper)
    {
        this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    /// <inheritdoc/>
    public string Id => "legacy-xml";

    /// <inheritdoc/>
    public IReadOnlyList<string> ClassExtensions { get; } = [".netpc"];

    /// <inheritdoc/>
    public bool CanWrite => false;

    /// <inheritdoc/>
    /// <exception cref="DocumentFormatException">The content is not a valid legacy class graph.</exception>
    public ValueTask<ClassDocument> ReadClassAsync(Stream input, DocumentId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        ClassGraph cls;
        try
        {
            cls = Serializer.ReadObject(input) as ClassGraph
                ?? throw new DocumentFormatException("The document did not deserialize to a class graph.", id);
        }
        catch (Exception ex) when (ex is SerializationException or XmlException)
        {
            throw new DocumentFormatException(ex.Message, id, inner: ex);
        }

        cls.AssignLegacyMemberIds();

        var graphs = new List<NodeGraph>(DocumentMapper.EnumerateGraphs(cls));

        foreach (NodeGraph graph in graphs)
        {
            graph.AssignLegacyNodeIds();
        }

        foreach (NodeGraph graph in graphs)
        {
            GraphTypeInference.Relax(graph);
        }

        return ValueTask.FromResult(mapper.ToDocument(cls));
    }

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">Always: legacy XML is read-only.</exception>
    public ValueTask WriteClassAsync(ClassDocument document, Stream output, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"'{Id}' is read-only.");
}
