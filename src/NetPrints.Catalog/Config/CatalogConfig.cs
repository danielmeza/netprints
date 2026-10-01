using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetPrints.Catalog;

/// <summary>The output format of <c>netprints catalog</c>.</summary>
public enum CatalogOutputFormat
{
    /// <summary>A catalog file (<c>*.npcat.json</c>).</summary>
    Catalog,

    /// <summary>A C# file holding the catalog and a factory for its type catalog.</summary>
    Csharp,
}

/// <summary>One catalog source of a configuration file: exactly one of an assembly, a package or a project.</summary>
public sealed record CatalogSourceConfig
{
    /// <summary>Path of an assembly, or a file-name pattern with <c>*</c> and <c>?</c>; relative to the configuration file.</summary>
    public string? Assembly { get; init; }

    /// <summary>NuGet package id; needs <see cref="Version"/>.</summary>
    public string? Package { get; init; }

    /// <summary>The exact package version.</summary>
    public string? Version { get; init; }

    /// <summary>Path of a project whose resolved references are cataloged; needs <see cref="Assemblies"/>.</summary>
    public string? Project { get; init; }

    /// <summary>The simple names of the project's references to catalog.</summary>
    public IReadOnlyList<string>? Assemblies { get; init; }
}

/// <summary>The <c>output</c> section of a configuration file.</summary>
public sealed record CatalogOutputConfig
{
    /// <summary>The output path, relative to the configuration file.</summary>
    public string? Path { get; init; }

    /// <summary>The output format; defaults to <see cref="CatalogOutputFormat.Catalog"/>.</summary>
    public CatalogOutputFormat? Format { get; init; }

    /// <summary>The class name of the <see cref="CatalogOutputFormat.Csharp"/> format.</summary>
    public string? ClassName { get; init; }

    /// <summary>The namespace of the <see cref="CatalogOutputFormat.Csharp"/> format.</summary>
    public string? Namespace { get; init; }
}

/// <summary>The <c>netprints.catalog.json</c> file (schema v1, data-model.md §3).</summary>
public sealed record CatalogConfig
{
    /// <summary>The URL of the schema a configuration file names in its <c>$schema</c> property.</summary>
    public const string SchemaUrl = "https://danielmeza.github.io/netprints/schemas/netprints.catalog.v1.schema.json";

    /// <summary>The schema version this library reads.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>The default file name looked up in the current directory.</summary>
    public const string FileName = "netprints.catalog.json";

    /// <summary>The <c>$schema</c> property.</summary>
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }

    /// <summary>The schema version; absent means 1, a newer one is an error.</summary>
    public int? SchemaVersion { get; init; }

    /// <summary>The sources to catalog.</summary>
    public IReadOnlyList<CatalogSourceConfig>? Sources { get; init; }

    /// <summary>Extra directories searched for the dependencies of assembly sources.</summary>
    public IReadOnlyList<string>? ReferencePaths { get; init; }

    /// <summary>The target framework used to resolve references; defaults to <c>net10.0</c>.</summary>
    public string? TargetFramework { get; init; }

    /// <summary>Type-name globs added to the profile's included types.</summary>
    public IReadOnlyList<string>? Include { get; init; }

    /// <summary>Type-name globs added to the profile's excluded types.</summary>
    public IReadOnlyList<string>? Exclude { get; init; }

    /// <summary>The profile: an id, the path of a <c>*.npprofile.json</c> file, or an inline profile object.</summary>
    public JsonElement? Profile { get; init; }

    /// <summary>The catalog id; defaults to the first assembly's name, lower-cased.</summary>
    public string? Id { get; init; }

    /// <summary>The catalog version; defaults to the first assembly's version.</summary>
    public string? Version { get; init; }

    /// <summary>Where and how the result is written.</summary>
    public CatalogOutputConfig? Output { get; init; }

    /// <summary>Extension folders whose contributed profiles can be named.</summary>
    public IReadOnlyList<string>? Extensions { get; init; }
}
