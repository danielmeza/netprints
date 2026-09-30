using System.Collections.Generic;

namespace NetPrints.Catalog;

/// <summary>A catalog file (<c>*.npcat.json</c>, schema v1): the types and members of the covered assemblies.</summary>
public sealed record CatalogDocument
{
    /// <summary>The URL of the schema every catalog file names in its <c>$schema</c> property.</summary>
    public const string SchemaUrl = "https://danielmeza.github.io/netprints/schemas/npcat.v1.schema.json";

    /// <summary>The schema version this library reads and writes.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>The schema version of the file; readers reject a newer one.</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>The catalog id: <c>[a-z0-9][a-z0-9._-]*</c>.</summary>
    public required string Id { get; init; }

    /// <summary>The catalog version, by default the first covered assembly's version.</summary>
    public required string Version { get; init; }

    /// <summary>The id of the profile that produced the catalog.</summary>
    public string? Profile { get; init; }

    /// <summary>The covered assemblies, at least one, sorted by name.</summary>
    public required IReadOnlyList<CatalogAssembly> Assemblies { get; init; }

    /// <summary>The cataloged types, sorted by id.</summary>
    public IReadOnlyList<CatalogType> Types { get; init; } = [];
}

/// <summary>An assembly a catalog covers.</summary>
public sealed record CatalogAssembly
{
    /// <summary>The simple assembly name.</summary>
    public required string Name { get; init; }

    /// <summary>The assembly version from its metadata identity.</summary>
    public required string Version { get; init; }
}
