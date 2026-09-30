using System.Collections.Generic;

namespace NetPrints.Catalog;

/// <summary>A cataloged type.</summary>
public sealed record CatalogType
{
    /// <summary>The documentation id, for example <c>T:Ns.Outer.Inner`1</c>; unique in the document.</summary>
    public required string Id { get; init; }

    /// <summary>The namespace; omitted for the global namespace.</summary>
    public string? Namespace { get; init; }

    /// <summary>The metadata name without the arity suffix.</summary>
    public required string Name { get; init; }

    /// <summary>The kind of type.</summary>
    public required CatalogTypeKind Kind { get; init; }

    /// <summary>A subset of <c>static</c>, <c>abstract</c>, <c>sealed</c>, in that order.</summary>
    public IReadOnlyList<string>? Modifiers { get; init; }

    /// <summary>The declared generic parameter names.</summary>
    public IReadOnlyList<string>? GenericParameters { get; init; }

    /// <summary>The documentation id of the containing type, for nested types.</summary>
    public string? DeclaringType { get; init; }

    /// <summary>The base type; omitted for interfaces and when the base is <c>System.Object</c>.</summary>
    public CatalogTypeRef? BaseType { get; init; }

    /// <summary>Every implemented interface, sorted by rendered name.</summary>
    public IReadOnlyList<CatalogTypeRef>? Interfaces { get; init; }

    /// <summary>The member names of an enum, in declared order.</summary>
    public IReadOnlyList<string>? EnumMembers { get; init; }

    /// <summary>The normalized summary.</summary>
    public string? Summary { get; init; }

    /// <summary>The node hint from <c>[NetPrintsType]</c>.</summary>
    public CatalogNodeHint? Node { get; init; }

    /// <summary>The constructors, sorted by id.</summary>
    public IReadOnlyList<CatalogConstructor>? Constructors { get; init; }

    /// <summary>The methods, sorted by id.</summary>
    public IReadOnlyList<CatalogMethod>? Methods { get; init; }

    /// <summary>The properties and fields, sorted by id.</summary>
    public IReadOnlyList<CatalogVariable>? Variables { get; init; }
}
