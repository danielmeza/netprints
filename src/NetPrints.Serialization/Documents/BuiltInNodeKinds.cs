#nullable enable

namespace NetPrints.Serialization.Documents;

/// <summary>
/// The <c>$kind</c> value of every built-in node document (document-format.md §1.5), declared once
/// here and referenced everywhere it would otherwise be repeated as a literal: the
/// <see cref="NodeDocument"/> <c>JsonDerivedType</c> discriminators, the built-in converters'
/// <c>Kind</c> properties, <see cref="Mapping.NodeDocumentConverterRegistry"/>'s known-kinds set, and
/// <c>NetPrints.Extensibility.Nodes.BuiltInNodeLibrary</c>'s descriptors. Values are unchanged from
/// before this class existed, so serialized documents and the committed JSON Schema are unaffected.
/// </summary>
public static class BuiltInNodeKinds
{
    /// <summary>A method's entry node (<see cref="MethodEntryNodeDocument"/>).</summary>
    public const string MethodEntry = "methodEntry";

    /// <summary>A constructor's entry node (<see cref="ConstructorEntryNodeDocument"/>).</summary>
    public const string ConstructorEntry = "constructorEntry";

    /// <summary>A method's return node (<see cref="ReturnNodeDocument"/>).</summary>
    public const string Return = "return";

    /// <summary>The class graph's fixed return node (<see cref="ClassReturnNodeDocument"/>).</summary>
    public const string ClassReturn = "classReturn";

    /// <summary>A type graph's fixed return node (<see cref="TypeReturnNodeDocument"/>).</summary>
    public const string TypeReturn = "typeReturn";

    /// <summary>An event graph's entry node (<see cref="EventEntryNodeDocument"/>, sub-phase G).</summary>
    public const string EventEntry = "eventEntry";

    /// <summary>A method call node (<see cref="CallMethodNodeDocument"/>).</summary>
    public const string CallMethod = "callMethod";

    /// <summary>A constructor call node (<see cref="ConstructorNodeDocument"/>).</summary>
    public const string Constructor = "constructor";

    /// <summary>A delegate-creation node (<see cref="MakeDelegateNodeDocument"/>).</summary>
    public const string MakeDelegate = "makeDelegate";

    /// <summary>A variable getter node (<see cref="VariableGetterNodeDocument"/>).</summary>
    public const string VariableGetter = "variableGetter";

    /// <summary>A variable setter node (<see cref="VariableSetterNodeDocument"/>).</summary>
    public const string VariableSetter = "variableSetter";

    /// <summary>A literal-value node (<see cref="LiteralNodeDocument"/>).</summary>
    public const string Literal = "literal";

    /// <summary>A type-reference node (<see cref="TypeNodeDocument"/>).</summary>
    public const string Type = "type";

    /// <summary>An array-type node (<see cref="MakeArrayTypeNodeDocument"/>).</summary>
    public const string MakeArrayType = "makeArrayType";

    /// <summary>An array-creation node (<see cref="MakeArrayNodeDocument"/>).</summary>
    public const string MakeArray = "makeArray";

    /// <summary>An explicit-cast node (<see cref="ExplicitCastNodeDocument"/>).</summary>
    public const string ExplicitCast = "explicitCast";

    /// <summary>A <c>typeof</c> node (<see cref="TypeOfNodeDocument"/>).</summary>
    public const string TypeOf = "typeOf";

    /// <summary>An if/else branch node (<see cref="IfElseNodeDocument"/>).</summary>
    public const string IfElse = "ifElse";

    /// <summary>A for-loop node (<see cref="ForLoopNodeDocument"/>).</summary>
    public const string ForLoop = "forLoop";

    /// <summary>A ternary-selection node (<see cref="TernaryNodeDocument"/>).</summary>
    public const string Ternary = "ternary";

    /// <summary>An <c>await</c> node (<see cref="AwaitNodeDocument"/>).</summary>
    public const string Await = "await";

    /// <summary>A <c>throw</c> node (<see cref="ThrowNodeDocument"/>).</summary>
    public const string Throw = "throw";

    /// <summary>A default-value node (<see cref="DefaultNodeDocument"/>).</summary>
    public const string Default = "default";

    /// <summary>An execution reroute node (<see cref="RerouteNodeDocument"/>).</summary>
    public const string Reroute = "reroute";
}
