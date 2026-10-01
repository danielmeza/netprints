using System;
using System.ComponentModel;
using System.Linq;
using NetPrints.Catalog;
using NetPrints.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Commands;

/// <summary>Settings of <c>catalog</c>: the sources, the profile and the output (contracts/catalog.md §4).</summary>
internal sealed class CatalogSettings : CommandSettingsBase
{
    private const char PackageSeparator = '@';

    /// <summary>Gets the configuration file; <c>./netprints.catalog.json</c> when omitted.</summary>
    [CommandOption("--config <file>")]
    [Description("The configuration file. Defaults to ./netprints.catalog.json when it exists.")]
    public string? Config { get; init; }

    /// <summary>Gets the assembly files or globs to catalog.</summary>
    [CommandOption("--assembly <path>")]
    [Description("An assembly (or a glob of file names) to catalog (repeatable).")]
    public string[] Assemblies { get; init; } = [];

    /// <summary>Gets the NuGet packages to catalog, as <c>id@version</c>.</summary>
    [CommandOption("--package <id@version>")]
    [Description("A NuGet package to catalog, restored from the configured sources (repeatable).")]
    public string[] Packages { get; init; } = [];

    /// <summary>Gets the project whose references are cataloged: a <c>.csproj</c> or the directory holding one.</summary>
    [CommandOption("--project <path>")]
    [Description("A project whose referenced assemblies are cataloged (see --assemblies).")]
    public string? Project { get; init; }

    /// <summary>Gets the simple names of the project's references to catalog.</summary>
    [CommandOption("--assemblies <name>")]
    [Description("The name of a reference of --project to catalog (repeatable).")]
    public string[] ProjectAssemblies { get; init; } = [];

    /// <summary>Gets the extra directories the assemblies' dependencies are searched in.</summary>
    [CommandOption("--reference-path <dir>")]
    [Description("A directory to search for dependencies (repeatable).")]
    public string[] ReferencePaths { get; init; } = [];

    /// <summary>Gets the target framework the temporary project restores for.</summary>
    [CommandOption("--framework <tfm>")]
    [Description("The target framework moniker (default net10.0).")]
    public string? Framework { get; init; }

    /// <summary>Gets the type globs appended to the profile's included types.</summary>
    [CommandOption("--include <glob>")]
    [Description("Only catalog types whose full name matches the glob (repeatable).")]
    public string[] Include { get; init; } = [];

    /// <summary>Gets the type globs appended to the profile's excluded types.</summary>
    [CommandOption("--exclude <glob>")]
    [Description("Leave out types whose full name matches the glob (repeatable).")]
    public string[] Exclude { get; init; } = [];

    /// <summary>Gets the profile id or the <c>*.npprofile.json</c> file.</summary>
    [CommandOption("--profile <id|file>")]
    [Description("The profile: a built-in or contributed id, or a *.npprofile.json file. Defaults to the project profile's, else public-api.")]
    public string? Profile { get; init; }

    /// <summary>Gets the catalog id.</summary>
    [CommandOption("--id <id>")]
    [Description("The catalog id (default: the first assembly's name, lower-cased).")]
    public string? Id { get; init; }

    /// <summary>Gets the catalog version.</summary>
    [CommandOption("--catalog-version <version>")]
    [Description("The catalog version (default: the first assembly's version).")]
    public string? CatalogVersion { get; init; }

    /// <summary>Gets the output file.</summary>
    [CommandOption("--output <path>")]
    [Description("The file to write. Defaults to <id>.npcat.json (or <class>.g.cs) next to the configuration file.")]
    public string? Output { get; init; }

    /// <summary>Gets the output format.</summary>
    [CommandOption("--format <catalog|csharp>")]
    [Description("catalog (a .npcat.json file) or csharp (a class holding the catalog).")]
    public string? Format { get; init; }

    /// <summary>Gets the class name of the C# format.</summary>
    [CommandOption("--class-name <name>")]
    [Description("The class of the csharp format.")]
    public string? ClassName { get; init; }

    /// <summary>Gets the namespace of the C# format.</summary>
    [CommandOption("--namespace <ns>")]
    [Description("The namespace of the csharp format.")]
    public string? Namespace { get; init; }

    /// <summary>Gets the extension folders that contribute profiles.</summary>
    [CommandOption("--extension <folder>")]
    [Description("An extension folder whose catalog profiles can be selected by id (repeatable).")]
    public string[] Extensions { get; init; } = [];

    /// <summary>Gets a value indicating whether nothing is written and a missing or different output fails the command.</summary>
    [CommandOption("--check")]
    [Description("Write nothing; exit 1 when the output is missing or differs.")]
    public bool Check { get; init; }

    /// <summary>Gets a value indicating whether a source was named on the command line.</summary>
    public bool HasSource => Assemblies.Length > 0 || Packages.Length > 0 || Project is not null;

    /// <summary>Gets the parsed output format, or <see langword="null"/> when none was given.</summary>
    public CatalogOutputFormat? OutputFormat => Format switch
    {
        null => null,
        _ when string.Equals(Format, "csharp", StringComparison.Ordinal) => CatalogOutputFormat.Csharp,
        _ => CatalogOutputFormat.Catalog,
    };

    /// <inheritdoc/>
    public override ValidationResult Validate()
    {
        if (Format is not null && !string.Equals(Format, "catalog", StringComparison.Ordinal) && !string.Equals(Format, "csharp", StringComparison.Ordinal))
        {
            return ValidationResult.Error($"--format must be 'catalog' or 'csharp', not '{Format}'.");
        }

        string? badPackage = Packages.FirstOrDefault(package => !IsPackageReference(package));
        if (badPackage is not null)
        {
            return ValidationResult.Error($"--package needs '<id>@<version>', not '{badPackage}'.");
        }

        return ProjectAssemblies.Length > 0 && Project is null
            ? ValidationResult.Error("--assemblies names references of a --project.")
            : base.Validate();
    }

    /// <summary>Splits <c>id@version</c>.</summary>
    /// <param name="package">The <c>--package</c> value.</param>
    /// <returns>The id and the version.</returns>
    public static (string Id, string Version) SplitPackage(string package)
    {
        ArgumentNullException.ThrowIfNull(package);
        int at = package.IndexOf(PackageSeparator, StringComparison.Ordinal);
        return (package[..at], package[(at + 1)..]);
    }

    private static bool IsPackageReference(string package)
    {
        int at = package.IndexOf(PackageSeparator, StringComparison.Ordinal);
        return at > 0 && at < package.Length - 1;
    }
}
