#nullable enable
using System.Collections.Generic;
using System.Text.Json.Serialization;
using NetPrints.Core;

namespace NetPrints.Serialization.Documents;

/// <summary>
/// The graph file for one class (document-format.md §1.4): header, its class graph, members and
/// layout. <c>$schema</c> is not a member of this record; the writer inserts it as the JSON object's
/// first property and the reader strips it before deserializing (document-format.md §2.2).
/// </summary>
/// <param name="SchemaVersion">Document schema version (<c>1</c>).</param>
/// <param name="Namespace">Namespace the class is declared in, or <see langword="null"/> if it has
/// none.</param>
/// <param name="Name">Name of the class, without its namespace.</param>
/// <param name="Visibility">Visibility of the class.</param>
/// <param name="Modifiers">Modifiers of the class.</param>
/// <param name="GenericArguments">Names of the class's declared generic parameters, or
/// <see langword="null"/> if it takes none.</param>
/// <param name="ClassGraph">The class's own graph (graph key <c>class</c>): its
/// <see cref="Graph.ClassReturnNode"/> and the base type/interface pins it carries.</param>
/// <param name="Variables">The class's variables, in class order, or <see langword="null"/> if it has
/// none.</param>
/// <param name="Methods">The class's methods, in class order, or <see langword="null"/> if it has
/// none.</param>
/// <param name="Constructors">The class's constructors, in class order, or <see langword="null"/> if
/// it has none.</param>
/// <param name="EventGraphs">The class's event graphs, in class order, or <see langword="null"/> if it
/// has none.</param>
/// <param name="Layout">Node positions, by graph key then node id; a node with no entry is
/// auto-placed on load (document-format.md §2.6). <see langword="null"/> if empty.</param>
public sealed record ClassDocument(
    [property: JsonPropertyOrder(0), JsonIgnore(Condition = JsonIgnoreCondition.Never)] int SchemaVersion,
    [property: JsonPropertyOrder(1)] string? Namespace,
    [property: JsonPropertyOrder(2), JsonIgnore(Condition = JsonIgnoreCondition.Never)] string Name,
    [property: JsonPropertyOrder(3), JsonIgnore(Condition = JsonIgnoreCondition.Never)] MemberVisibility Visibility,
    [property: JsonPropertyOrder(4)] ClassModifiers Modifiers,
    [property: JsonPropertyOrder(5)] IReadOnlyList<string>? GenericArguments,
    [property: JsonPropertyOrder(6), JsonIgnore(Condition = JsonIgnoreCondition.Never)] GraphDocument ClassGraph,
    [property: JsonPropertyOrder(7)] IReadOnlyList<VariableDocument>? Variables,
    [property: JsonPropertyOrder(8)] IReadOnlyList<MethodDocument>? Methods,
    [property: JsonPropertyOrder(9)] IReadOnlyList<ConstructorDocument>? Constructors,
    [property: JsonPropertyOrder(10)] IReadOnlyList<EventGraphDocument>? EventGraphs,
    [property: JsonPropertyOrder(11)] SortedDictionary<string, SortedDictionary<string, int[]>>? Layout);
