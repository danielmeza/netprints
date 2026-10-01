using System.Collections.Generic;

namespace NetPrints.Catalog;

/// <summary>A reference to a type, as the live provider renders it.</summary>
public sealed record CatalogTypeRef
{
    /// <summary>The full name, or the generic parameter name when <see cref="Generic"/> is set.</summary>
    public required string Name { get; init; }

    /// <summary>Whether this is an unbound generic parameter.</summary>
    public bool Generic { get; init; }

    /// <summary>Whether the type is an enum.</summary>
    public bool IsEnum { get; init; }

    /// <summary>Whether the type is an interface.</summary>
    public bool IsInterface { get; init; }

    /// <summary>The generic arguments.</summary>
    public IReadOnlyList<CatalogTypeRef>? Args { get; init; }
}

/// <summary>Node presentation hints from <c>[NetPrintsType]</c> or <c>[NetPrintsNode]</c>.</summary>
public sealed record CatalogNodeHint
{
    /// <summary>The display name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>The palette category.</summary>
    public string? Category { get; init; }

    /// <summary>The search keywords, sorted.</summary>
    public IReadOnlyList<string>? Keywords { get; init; }
}

/// <summary>The <c>[Obsolete]</c> information of a method.</summary>
public sealed record CatalogObsoleteInfo
{
    /// <summary>The obsolete message.</summary>
    public string? Message { get; init; }

    /// <summary>Whether using the member is a compile error.</summary>
    public bool Error { get; init; }
}

/// <summary>A typed default value, the graph format's typed-value shape.</summary>
public sealed record CatalogTypedValue
{
    /// <summary>The type name.</summary>
    public required string Type { get; init; }

    /// <summary>The value rendered as text; omitted for a null value.</summary>
    public string? Value { get; init; }
}
