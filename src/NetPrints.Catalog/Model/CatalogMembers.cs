using System.Collections.Generic;

namespace NetPrints.Catalog;

/// <summary>A cataloged constructor.</summary>
public sealed record CatalogConstructor
{
    /// <summary>The documentation id, <c>M:...#ctor(...)</c>.</summary>
    public required string Id { get; init; }

    /// <summary>The visibility.</summary>
    public required CatalogVisibility Visibility { get; init; }

    /// <summary>The parameters in declared order.</summary>
    public IReadOnlyList<CatalogParameter>? Parameters { get; init; }

    /// <summary>The normalized summary.</summary>
    public string? Summary { get; init; }
}

/// <summary>A cataloged method.</summary>
public sealed record CatalogMethod
{
    /// <summary>The documentation id, <c>M:...</c>.</summary>
    public required string Id { get; init; }

    /// <summary>The metadata name (<c>op_Implicit</c> for operators).</summary>
    public required string Name { get; init; }

    /// <summary>The visibility.</summary>
    public required CatalogVisibility Visibility { get; init; }

    /// <summary>A subset of <c>static</c>, <c>abstract</c>, <c>virtual</c>, <c>override</c>, <c>sealed</c>, <c>extension</c>, <c>operator</c>, in that order.</summary>
    public IReadOnlyList<string>? Modifiers { get; init; }

    /// <summary>The declared generic parameter names.</summary>
    public IReadOnlyList<string>? GenericParameters { get; init; }

    /// <summary>The parameters in declared order; extension methods include <c>this</c>.</summary>
    public IReadOnlyList<CatalogParameter>? Parameters { get; init; }

    /// <summary>The return type; omitted for <c>void</c>.</summary>
    public CatalogTypeRef? ReturnType { get; init; }

    /// <summary>The <c>&lt;returns&gt;</c> text.</summary>
    public string? ReturnSummary { get; init; }

    /// <summary>The normalized summary.</summary>
    public string? Summary { get; init; }

    /// <summary>The <c>[Obsolete]</c> information.</summary>
    public CatalogObsoleteInfo? Obsolete { get; init; }

    /// <summary>The node hint from <c>[NetPrintsNode]</c>.</summary>
    public CatalogNodeHint? Node { get; init; }
}

/// <summary>A cataloged property or field.</summary>
public sealed record CatalogVariable
{
    /// <summary>The documentation id, <c>P:...</c> or <c>F:...</c>.</summary>
    public required string Id { get; init; }

    /// <summary>The member name.</summary>
    public required string Name { get; init; }

    /// <summary>Whether it is a property or a field.</summary>
    public required CatalogVariableKind Kind { get; init; }

    /// <summary>The variable type.</summary>
    public required CatalogTypeRef Type { get; init; }

    /// <summary>A subset of <c>static</c>, <c>readonly</c>, <c>const</c>.</summary>
    public IReadOnlyList<string>? Modifiers { get; init; }

    /// <summary>The getter visibility; omitted when not readable.</summary>
    public CatalogVisibility? Get { get; init; }

    /// <summary>The setter visibility; omitted when not writable.</summary>
    public CatalogVisibility? Set { get; init; }

    /// <summary>The normalized summary.</summary>
    public string? Summary { get; init; }
}

/// <summary>A parameter of a cataloged constructor or method.</summary>
public sealed record CatalogParameter
{
    /// <summary>The parameter name.</summary>
    public required string Name { get; init; }

    /// <summary>The parameter type.</summary>
    public required CatalogTypeRef Type { get; init; }

    /// <summary>How the parameter is passed; omitted for by value.</summary>
    public CatalogPassType? PassType { get; init; }

    /// <summary>Whether this is a <c>params</c> array.</summary>
    public bool Params { get; init; }

    /// <summary>The default value.</summary>
    public CatalogTypedValue? Default { get; init; }

    /// <summary>The <c>&lt;param&gt;</c> text.</summary>
    public string? Summary { get; init; }
}
