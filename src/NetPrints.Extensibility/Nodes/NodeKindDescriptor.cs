using NetPrints.Graph;
using NetPrints.Serialization.Mapping;
using NetPrints.Translator;

namespace NetPrints.Extensibility.Nodes;

/// <summary>
/// One node kind: its document converter, C# translator, the graphs it may appear in and its search entries
/// (extension-points.md §2). The registry rejects a descriptor whose <see cref="Kind"/> or
/// <see cref="NodeType"/> disagree with <see cref="Converter"/> (<c>NPX006</c>).
/// </summary>
/// <param name="Kind">The kind id; equals <c>Converter.Kind</c>. Built-in kinds have no <c>/</c>, extension kinds are
/// <c>"&lt;manifest id&gt;/&lt;Name&gt;"</c>.</param>
/// <param name="NodeType">The node class, derived from <see cref="Node"/>; equals <c>Converter.NodeType</c>.</param>
/// <param name="Converter">Converts the node to and from its document form.</param>
/// <param name="Translator">Translates the node to C#.</param>
/// <param name="AllowedIn">The graph kinds the node may be added to.</param>
/// <param name="Suggestions">The node-search entries; empty for kinds created by other means.</param>
public sealed record NodeKindDescriptor(
    string Kind,
    Type NodeType,
    INodeDocumentConverter Converter,
    INodeTranslator Translator,
    GraphKinds AllowedIn,
    IReadOnlyList<NodeSuggestion> Suggestions);
