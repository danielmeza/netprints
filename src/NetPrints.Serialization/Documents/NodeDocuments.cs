#nullable enable
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetPrints.Serialization.Documents;

/// <summary>
/// A pin's stored state (document-format.md §1.5): its pin reference, a user rename (only present
/// when the pin's display name differs from its stable key), and its unconnected value (only present
/// when set).
/// </summary>
/// <param name="Pin">Pin reference without a node id (document-format.md §1.4.2), e.g. <c>"in.data.value"</c>.</param>
/// <param name="Name">The pin's user-facing name, present only when it differs from its <c>keyName</c>
/// (a renamed argument or return pin).</param>
/// <param name="Value">The pin's unconnected value, present only when set.</param>
public sealed record PinStateDocument(string Pin, string? Name, TypedValue? Value);

/// <summary>
/// Polymorphic base for a node's stored form (document-format.md §1.5). Common fields (in this order
/// after the <c>$kind</c> discriminator): <see cref="Id"/>, <see cref="Name"/>, <see cref="Pins"/>.
/// </summary>
/// <param name="Id">The node's id (document-format.md §1.4.1).</param>
/// <param name="Name">The node's display name, omitted (and read back as <c>Node.DefaultName</c>) when
/// it equals it.</param>
/// <param name="Pins">Stored pin states (renames and unconnected values), or <see langword="null"/> if
/// none apply.</param>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(MethodEntryNodeDocument), "methodEntry")]
[JsonDerivedType(typeof(ConstructorEntryNodeDocument), "constructorEntry")]
[JsonDerivedType(typeof(ReturnNodeDocument), "return")]
[JsonDerivedType(typeof(ClassReturnNodeDocument), "classReturn")]
[JsonDerivedType(typeof(TypeReturnNodeDocument), "typeReturn")]
[JsonDerivedType(typeof(EventEntryNodeDocument), "eventEntry")]
[JsonDerivedType(typeof(CallMethodNodeDocument), "callMethod")]
[JsonDerivedType(typeof(ConstructorNodeDocument), "constructor")]
[JsonDerivedType(typeof(MakeDelegateNodeDocument), "makeDelegate")]
[JsonDerivedType(typeof(VariableGetterNodeDocument), "variableGetter")]
[JsonDerivedType(typeof(VariableSetterNodeDocument), "variableSetter")]
[JsonDerivedType(typeof(LiteralNodeDocument), "literal")]
[JsonDerivedType(typeof(TypeNodeDocument), "type")]
[JsonDerivedType(typeof(MakeArrayTypeNodeDocument), "makeArrayType")]
[JsonDerivedType(typeof(MakeArrayNodeDocument), "makeArray")]
[JsonDerivedType(typeof(ExplicitCastNodeDocument), "explicitCast")]
[JsonDerivedType(typeof(TypeOfNodeDocument), "typeOf")]
[JsonDerivedType(typeof(IfElseNodeDocument), "ifElse")]
[JsonDerivedType(typeof(ForLoopNodeDocument), "forLoop")]
[JsonDerivedType(typeof(TernaryNodeDocument), "ternary")]
[JsonDerivedType(typeof(AwaitNodeDocument), "await")]
[JsonDerivedType(typeof(ThrowNodeDocument), "throw")]
[JsonDerivedType(typeof(DefaultNodeDocument), "default")]
[JsonDerivedType(typeof(RerouteNodeDocument), "reroute")]
public abstract record NodeDocument(
    [property: JsonPropertyOrder(-3), JsonIgnore(Condition = JsonIgnoreCondition.Never)] string Id,
    [property: JsonPropertyOrder(-2)] string? Name,
    [property: JsonPropertyOrder(-1)] IReadOnlyList<PinStateDocument>? Pins);

/// <summary>A node of a kind this build of NetPrints does not know (document-format.md §1.5): its
/// original content is preserved and re-emitted unchanged.</summary>
/// <param name="Id">The node's id.</param>
/// <param name="Kind">The unrecognized <c>$kind</c> value.</param>
/// <param name="Raw">The node's full original JSON content, <c>$kind</c> and <c>id</c> first, other
/// properties in source order.</param>
public sealed record UnknownNodeDocument(string Id, string Kind, JsonElement Raw) : NodeDocument(Id, "", null);

/// <summary>A method's entry node (document-format.md §1.5, <c>MethodEntryNode</c>).</summary>
public sealed record MethodEntryNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins,
    int ArgumentCount, IReadOnlyList<string>? GenericArguments) : NodeDocument(Id, Name, Pins);

/// <summary>A constructor's entry node (document-format.md §1.5, <c>ConstructorEntryNode</c>).</summary>
public sealed record ConstructorEntryNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins,
    int ArgumentCount) : NodeDocument(Id, Name, Pins);

/// <summary>A method's return node (document-format.md §1.5, <c>ReturnNode</c>).</summary>
public sealed record ReturnNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins,
    int ReturnCount) : NodeDocument(Id, Name, Pins);

/// <summary>The class graph's fixed node (document-format.md §1.5, <c>ClassReturnNode</c>).</summary>
public sealed record ClassReturnNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins,
    int InterfaceCount) : NodeDocument(Id, Name, Pins);

/// <summary>A type graph's fixed node (document-format.md §1.5, <c>TypeReturnNode</c>).</summary>
public sealed record TypeReturnNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins)
    : NodeDocument(Id, Name, Pins);

/// <summary>An event graph's entry node (document-format.md §1.5, sub-phase G, <c>EventEntryNode</c>).</summary>
/// <param name="Id">The node's id.</param>
/// <param name="Name">The node's display name, if it differs from its default.</param>
/// <param name="Pins">Stored pin states, if any.</param>
/// <param name="EventName">The event's name (a custom event name, or the overridden method's name).</param>
/// <param name="Visibility">Entry visibility.</param>
/// <param name="Modifiers">Entry modifiers (omitted when <c>None</c>).</param>
/// <param name="Overrides">The overridden base method, present only for an override entry.</param>
/// <param name="ArgumentCount">Number of argument pins.</param>
public sealed record EventEntryNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins,
    string EventName, NetPrints.Core.MemberVisibility Visibility, NetPrints.Core.MethodModifiers Modifiers,
    MethodRef? Overrides, int ArgumentCount) : NodeDocument(Id, Name, Pins);

/// <summary>A method call (document-format.md §1.5, <c>CallMethodNode</c>).</summary>
public sealed record CallMethodNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins,
    MethodRef Method, int GenericArgumentCount) : NodeDocument(Id, Name, Pins);

/// <summary>A constructor call (document-format.md §1.5, <c>ConstructorNode</c>).</summary>
public sealed record ConstructorNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins,
    ConstructorRef Constructor) : NodeDocument(Id, Name, Pins);

/// <summary>Creates a delegate for a method (document-format.md §1.5, <c>MakeDelegateNode</c>).</summary>
public sealed record MakeDelegateNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins,
    MethodRef Method) : NodeDocument(Id, Name, Pins);

/// <summary>Reads a variable's value (document-format.md §1.5, <c>VariableGetterNode</c>).</summary>
public sealed record VariableGetterNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins,
    VariableRef Variable) : NodeDocument(Id, Name, Pins);

/// <summary>Writes a variable's value (document-format.md §1.5, <c>VariableSetterNode</c>).</summary>
public sealed record VariableSetterNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins,
    VariableRef Variable) : NodeDocument(Id, Name, Pins);

/// <summary>A literal value (document-format.md §1.5, <c>LiteralNode</c>).</summary>
public sealed record LiteralNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins,
    TypeRef LiteralType) : NodeDocument(Id, Name, Pins);

/// <summary>A type reference expression (document-format.md §1.5, <c>TypeNode</c>).</summary>
public sealed record TypeNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins,
    TypeRef Type) : NodeDocument(Id, Name, Pins);

/// <summary>Builds an array type from an element type (document-format.md §1.5, <c>MakeArrayTypeNode</c>).</summary>
public sealed record MakeArrayTypeNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins)
    : NodeDocument(Id, Name, Pins);

/// <summary>Creates an array (document-format.md §1.5, <c>MakeArrayNode</c>).</summary>
/// <param name="Id">The node's id.</param>
/// <param name="Name">The node's display name, if it differs from its default.</param>
/// <param name="Pins">Stored pin states, if any.</param>
/// <param name="UsePredefinedSize">Whether the array uses a runtime size pin instead of an element
/// initializer list.</param>
/// <param name="ElementCount">Number of element input pins (initializer-list mode only).</param>
public sealed record MakeArrayNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins,
    bool UsePredefinedSize, int ElementCount) : NodeDocument(Id, Name, Pins);

/// <summary>An explicit type cast (document-format.md §1.5, <c>ExplicitCastNode</c>).</summary>
public sealed record ExplicitCastNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins)
    : NodeDocument(Id, Name, Pins);

/// <summary>A C# <c>typeof</c> expression (document-format.md §1.5, <c>TypeOfNode</c>).</summary>
public sealed record TypeOfNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins)
    : NodeDocument(Id, Name, Pins);

/// <summary>An if/else branch (document-format.md §1.5, <c>IfElseNode</c>).</summary>
public sealed record IfElseNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins)
    : NodeDocument(Id, Name, Pins);

/// <summary>An integer for-loop (document-format.md §1.5, <c>ForLoopNode</c>).</summary>
public sealed record ForLoopNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins)
    : NodeDocument(Id, Name, Pins);

/// <summary>A ternary expression (document-format.md §1.5, <c>TernaryNode</c>).</summary>
public sealed record TernaryNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins)
    : NodeDocument(Id, Name, Pins);

/// <summary>An <c>await</c> expression (document-format.md §1.5, <c>AwaitNode</c>).</summary>
public sealed record AwaitNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins)
    : NodeDocument(Id, Name, Pins);

/// <summary>A <c>throw</c> statement (document-format.md §1.5, <c>ThrowNode</c>).</summary>
public sealed record ThrowNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins)
    : NodeDocument(Id, Name, Pins);

/// <summary>A type's default value (document-format.md §1.5, <c>DefaultNode</c>).</summary>
public sealed record DefaultNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins)
    : NodeDocument(Id, Name, Pins);

/// <summary>A reroute node used for layout only (document-format.md §1.5, <c>RerouteNode</c>).</summary>
/// <param name="Id">The node's id.</param>
/// <param name="Name">The node's display name, if it differs from its default.</param>
/// <param name="Pins">Stored pin states, if any.</param>
/// <param name="PinKind">Which kind of pin pair this node reroutes: <c>"exec"</c>, <c>"data"</c> or
/// <c>"type"</c>.</param>
/// <param name="Count">Number of pin pairs, for <c>"exec"</c> and <c>"type"</c> (omitted, and implied
/// by <see cref="DataTypes"/>'s length, for <c>"data"</c>).</param>
/// <param name="DataTypes">Input/output type pair for each data pin pair, for <c>"data"</c> only.</param>
public sealed record RerouteNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins,
    string PinKind, int Count, IReadOnlyList<TypeRef[]>? DataTypes) : NodeDocument(Id, Name, Pins);
