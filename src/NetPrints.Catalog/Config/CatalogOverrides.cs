using System.Collections.Generic;

namespace NetPrints.Catalog;

/// <summary>The command-line options of <c>netprints catalog</c> that override a configuration file (data-model.md §3).</summary>
/// <remarks>Paths are relative to the current directory. Any source replaces the file's sources; <see cref="Include"/> and <see cref="Exclude"/> are appended.</remarks>
public sealed record CatalogOverrides
{
    /// <summary>Sources from <c>--assembly</c>, <c>--package</c> and <c>--project</c>.</summary>
    public IReadOnlyList<CatalogSourceConfig>? Sources { get; init; }

    /// <summary>The <c>--reference-path</c> values.</summary>
    public IReadOnlyList<string>? ReferencePaths { get; init; }

    /// <summary>The <c>--framework</c> value.</summary>
    public string? TargetFramework { get; init; }

    /// <summary>The <c>--include</c> globs.</summary>
    public IReadOnlyList<string>? Include { get; init; }

    /// <summary>The <c>--exclude</c> globs.</summary>
    public IReadOnlyList<string>? Exclude { get; init; }

    /// <summary>The <c>--profile</c> value: an id or a <c>*.npprofile.json</c> path.</summary>
    public string? Profile { get; init; }

    /// <summary>The <c>--id</c> value.</summary>
    public string? Id { get; init; }

    /// <summary>The <c>--catalog-version</c> value.</summary>
    public string? Version { get; init; }

    /// <summary>The <c>--output</c> value.</summary>
    public string? OutputPath { get; init; }

    /// <summary>The <c>--format</c> value.</summary>
    public CatalogOutputFormat? Format { get; init; }

    /// <summary>The <c>--class-name</c> value.</summary>
    public string? ClassName { get; init; }

    /// <summary>The <c>--namespace</c> value.</summary>
    public string? Namespace { get; init; }

    /// <summary>The <c>--extension</c> folders.</summary>
    public IReadOnlyList<string>? Extensions { get; init; }
}

/// <summary>The settings of a catalog run after the file and the command line are merged, with every path absolute.</summary>
public sealed record ResolvedCatalogConfig
{
    /// <summary>The sources; each has exactly one of assembly, package or project set.</summary>
    public required IReadOnlyList<CatalogSourceConfig> Sources { get; init; }

    /// <summary>The extra reference directories.</summary>
    public IReadOnlyList<string> ReferencePaths { get; init; } = [];

    /// <summary>The target framework moniker.</summary>
    public required string TargetFramework { get; init; }

    /// <summary>The included type globs.</summary>
    public IReadOnlyList<string> Include { get; init; } = [];

    /// <summary>The excluded type globs.</summary>
    public IReadOnlyList<string> Exclude { get; init; } = [];

    /// <summary>The profile id or the absolute path of a profile file; null with an inline profile or the default.</summary>
    public string? ProfileReference { get; init; }

    /// <summary>The inline profile's JSON text, already validated; null when the profile is a reference.</summary>
    public string? InlineProfileJson { get; init; }

    /// <summary>The catalog id, or null for the default.</summary>
    public string? Id { get; init; }

    /// <summary>The catalog version, or null for the default.</summary>
    public string? Version { get; init; }

    /// <summary>The output format.</summary>
    public CatalogOutputFormat Format { get; init; }

    /// <summary>The absolute output path, or null for the default next to <see cref="BaseDirectory"/>.</summary>
    public string? OutputPath { get; init; }

    /// <summary>The class name of the C# format.</summary>
    public string? ClassName { get; init; }

    /// <summary>The namespace of the C# format.</summary>
    public string? Namespace { get; init; }

    /// <summary>The absolute extension folders.</summary>
    public IReadOnlyList<string> Extensions { get; init; } = [];

    /// <summary>The configuration file's directory, or the current directory without a file.</summary>
    public required string BaseDirectory { get; init; }
}
