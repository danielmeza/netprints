#nullable enable
using System.Collections.Generic;
using System.Text.Json.Serialization;
using NetPrints.Core;
using NetPrints.Serialization.Json;

namespace NetPrints.Serialization.Documents;

/// <summary>
/// One executable or type graph (document-format.md §1.4): its nodes, the connections between them,
/// and, for a method or constructor graph, its local variables.
/// </summary>
/// <param name="Nodes">The graph's nodes, in graph (translation) order.</param>
/// <param name="Connections">The graph's connections, ordinal-sorted by <see cref="ConnectionDocument.From"/>
/// then <see cref="ConnectionDocument.To"/>, or <see langword="null"/> if it has none.</param>
/// <param name="Locals">The graph's local variables (method/constructor graphs only), or
/// <see langword="null"/> if it has none.</param>
public sealed record GraphDocument(
    [property: JsonConverter(typeof(NodeListConverter))] IReadOnlyList<NodeDocument> Nodes,
    IReadOnlyList<ConnectionDocument>? Connections,
    IReadOnlyList<LocalVariableDocument>? Locals);

/// <summary>
/// A connection between an output pin and an input pin (document-format.md §1.4, §1.4.2).
/// </summary>
/// <param name="From">Endpoint of the connection's source (output) pin: <c>"&lt;nodeId&gt;/&lt;pin&gt;"</c>.</param>
/// <param name="To">Endpoint of the connection's target (input) pin: <c>"&lt;nodeId&gt;/&lt;pin&gt;"</c>.</param>
public sealed record ConnectionDocument(string From, string To);

/// <summary>
/// A class's method (document-format.md §1.4): its member id, name, visibility, modifiers and graph.
/// </summary>
/// <param name="Id">The method's member id (document-format.md §1.4.1).</param>
/// <param name="Name">Method name.</param>
/// <param name="Visibility">Method visibility.</param>
/// <param name="Modifiers">Method modifiers; omitted when <see cref="MethodModifiers.None"/>.</param>
/// <param name="Graph">The method's graph.</param>
public sealed record MethodDocument(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string Id,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] MemberVisibility Visibility,
    MethodModifiers Modifiers,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] GraphDocument Graph);

/// <summary>
/// A class's constructor (document-format.md §1.4): its member id, visibility and graph.
/// </summary>
/// <param name="Id">The constructor's member id (document-format.md §1.4.1).</param>
/// <param name="Visibility">Constructor visibility.</param>
/// <param name="Graph">The constructor's graph.</param>
public sealed record ConstructorDocument(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string Id,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] MemberVisibility Visibility,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] GraphDocument Graph);

/// <summary>
/// A variable's getter or setter (document-format.md §1.4): visibility and graph. Has no id of its
/// own; its graph key is derived from the owning <see cref="VariableDocument"/>'s id.
/// </summary>
/// <param name="Visibility">Accessor visibility.</param>
/// <param name="Graph">The accessor's graph.</param>
public sealed record AccessorDocument(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] MemberVisibility Visibility,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] GraphDocument Graph);

/// <summary>
/// A class's variable (document-format.md §1.4): its member id, name, visibility, modifiers, type
/// graph and optional getter/setter.
/// </summary>
/// <param name="Id">The variable's member id (document-format.md §1.4.1).</param>
/// <param name="Name">Variable name.</param>
/// <param name="Visibility">Variable visibility.</param>
/// <param name="Modifiers">Variable modifiers; omitted when <see cref="VariableModifiers.None"/>.</param>
/// <param name="TypeGraph">The variable's type graph (graph key <c>&lt;Id&gt;/type</c>).</param>
/// <param name="Getter">The variable's getter, if it has one (graph key <c>&lt;Id&gt;/get</c>).</param>
/// <param name="Setter">The variable's setter, if it has one (graph key <c>&lt;Id&gt;/set</c>).</param>
public sealed record VariableDocument(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string Id,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] MemberVisibility Visibility,
    VariableModifiers Modifiers,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] GraphDocument TypeGraph,
    AccessorDocument? Getter,
    AccessorDocument? Setter);

/// <summary>
/// A class's event graph (document-format.md §1.4, sub-phase G/US4): its member id, name and graph.
/// </summary>
/// <param name="Id">The event graph's member id (document-format.md §1.4.1).</param>
/// <param name="Name">Event graph name.</param>
/// <param name="Graph">The event graph's graph.</param>
public sealed record EventGraphDocument(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string Id,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] GraphDocument Graph);

/// <summary>
/// A method- or constructor-local variable (document-format.md §1.4, sub-phase H/US5): its name and
/// type. Has no id; it is identified by name within its declaring graph.
/// </summary>
/// <param name="Name">Local variable name.</param>
/// <param name="Type">Local variable type.</param>
public sealed record LocalVariableDocument(string Name, TypeRef Type);
