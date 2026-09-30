using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NetPrints.Catalog;

/// <summary>Reads <c>netprints.catalog.json</c> and merges it with the command-line options (data-model.md §3).</summary>
public static class CatalogConfigResolver
{
    /// <summary>The target framework used when neither the file nor the command line names one.</summary>
    public const string DefaultTargetFramework = "net10.0";

    private const string SchemaVersionProperty = "schemaVersion";

    private const string ProfileFileSuffix = ".npprofile.json";

    /// <summary>Reads a configuration file.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The configuration.</returns>
    /// <exception cref="CatalogConfigException">The file is unreadable, invalid or newer than supported.</exception>
    public static CatalogConfig Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (IOException exception)
        {
            throw new CatalogConfigException($"Cannot read '{path}': {exception.Message}", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new CatalogConfigException($"Cannot read '{path}': {exception.Message}", exception);
        }
    }

    /// <summary>Reads a configuration from JSON text; comments and trailing commas are allowed.</summary>
    /// <param name="json">The text.</param>
    /// <returns>The configuration.</returns>
    /// <exception cref="CatalogConfigException">The text is invalid or its schema version is newer than supported.</exception>
    public static CatalogConfig Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new CatalogConfigException("The configuration file must hold a JSON object.");
            }

            if (root.TryGetProperty(SchemaVersionProperty, out JsonElement version) && version.ValueKind == JsonValueKind.Number
                && version.TryGetInt32(out int schemaVersion) && schemaVersion > CatalogConfig.CurrentSchemaVersion)
            {
                throw new CatalogConfigException(
                    $"The configuration file has schemaVersion {schemaVersion}, but this tool reads schemaVersion {CatalogConfig.CurrentSchemaVersion}; upgrade the tool.");
            }

            return root.Deserialize(CatalogConfigJsonContext.Default.CatalogConfig)
                ?? throw new CatalogConfigException("The configuration file is empty.");
        }
        catch (JsonException exception)
        {
            throw new CatalogConfigException($"The configuration file is invalid: {exception.Message}", exception);
        }
    }

    /// <summary>Merges the file with the command line: options replace scalars and lists, except include and exclude which are appended.</summary>
    /// <param name="file">The configuration file, or null when there is none.</param>
    /// <param name="configDirectory">The directory of the file, against which its relative paths resolve; null without a file.</param>
    /// <param name="overrides">The command-line options.</param>
    /// <param name="currentDirectory">The current directory, against which command-line paths resolve.</param>
    /// <returns>The merged settings with absolute paths.</returns>
    /// <exception cref="CatalogConfigException">A source or the profile is invalid, or there is no source.</exception>
    public static ResolvedCatalogConfig Resolve(CatalogConfig? file, string? configDirectory, CatalogOverrides overrides, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        string cwd = Path.GetFullPath(currentDirectory);
        string fileDirectory = configDirectory is null ? cwd : Path.GetFullPath(configDirectory);

        IReadOnlyList<CatalogSourceConfig> sources = overrides.Sources is { Count: > 0 }
            ? Absolutize(overrides.Sources, cwd)
            : Absolutize(file?.Sources ?? [], fileDirectory);
        if (sources.Count == 0)
        {
            throw new CatalogConfigException("No source: name an assembly, package or project in the configuration file or on the command line.");
        }

        foreach (CatalogSourceConfig source in sources)
        {
            Validate(source);
        }

        (string? profileReference, string? inlineProfile) = ResolveProfile(file?.Profile, fileDirectory, overrides.Profile, cwd);
        string? outputPath = overrides.OutputPath is { Length: > 0 } cliPath
            ? Path.GetFullPath(cliPath, cwd)
            : file?.Output?.Path is { Length: > 0 } filePath ? Path.GetFullPath(filePath, fileDirectory) : null;

        return new ResolvedCatalogConfig
        {
            Sources = sources,
            ReferencePaths = overrides.ReferencePaths is { Count: > 0 }
                ? [.. overrides.ReferencePaths.Select(path => Path.GetFullPath(path, cwd))]
                : [.. (file?.ReferencePaths ?? []).Select(path => Path.GetFullPath(path, fileDirectory))],
            TargetFramework = overrides.TargetFramework ?? file?.TargetFramework ?? DefaultTargetFramework,
            Include = [.. file?.Include ?? [], .. overrides.Include ?? []],
            Exclude = [.. file?.Exclude ?? [], .. overrides.Exclude ?? []],
            ProfileReference = profileReference,
            InlineProfileJson = inlineProfile,
            Id = overrides.Id ?? file?.Id,
            Version = overrides.Version ?? file?.Version,
            Format = overrides.Format ?? file?.Output?.Format ?? CatalogOutputFormat.Catalog,
            OutputPath = outputPath,
            ClassName = overrides.ClassName ?? file?.Output?.ClassName,
            Namespace = overrides.Namespace ?? file?.Output?.Namespace,
            Extensions = overrides.Extensions is { Count: > 0 }
                ? [.. overrides.Extensions.Select(path => Path.GetFullPath(path, cwd))]
                : [.. (file?.Extensions ?? []).Select(path => Path.GetFullPath(path, fileDirectory))],
            BaseDirectory = fileDirectory,
        };
    }

    private static List<CatalogSourceConfig> Absolutize(IReadOnlyList<CatalogSourceConfig> sources, string directory) =>
    [
        .. sources.Select(source => source with
        {
            Assembly = source.Assembly is { Length: > 0 } assembly ? Path.GetFullPath(assembly, directory) : source.Assembly,
            Project = source.Project is { Length: > 0 } project ? Path.GetFullPath(project, directory) : source.Project,
        }),
    ];

    private static void Validate(CatalogSourceConfig source)
    {
        int kinds = (string.IsNullOrEmpty(source.Assembly) ? 0 : 1)
            + (string.IsNullOrEmpty(source.Package) ? 0 : 1)
            + (string.IsNullOrEmpty(source.Project) ? 0 : 1);
        if (kinds != 1)
        {
            throw new CatalogConfigException("A source must name exactly one of 'assembly', 'package' or 'project'.");
        }

        if (!string.IsNullOrEmpty(source.Package) && string.IsNullOrEmpty(source.Version))
        {
            throw new CatalogConfigException($"The package source '{source.Package}' needs a 'version'.");
        }

        if (string.IsNullOrEmpty(source.Package) && !string.IsNullOrEmpty(source.Version))
        {
            throw new CatalogConfigException("'version' belongs to a package source.");
        }

        if (!string.IsNullOrEmpty(source.Project) && source.Assemblies is not { Count: > 0 })
        {
            throw new CatalogConfigException($"The project source '{source.Project}' needs 'assemblies': the names of the references to catalog.");
        }

        if (string.IsNullOrEmpty(source.Project) && source.Assemblies is not null)
        {
            throw new CatalogConfigException("'assemblies' belongs to a project source.");
        }
    }

    private static (string? Reference, string? Inline) ResolveProfile(JsonElement? fileProfile, string fileDirectory, string? cliProfile, string cwd)
    {
        if (cliProfile is { Length: > 0 })
        {
            return (ProfileReference(cliProfile, cwd), null);
        }

        if (fileProfile is not { } profile)
        {
            return (null, null);
        }

        switch (profile.ValueKind)
        {
            case JsonValueKind.String:
                return (ProfileReference(profile.GetString() ?? string.Empty, fileDirectory), null);
            case JsonValueKind.Object:
                string json = profile.GetRawText();
                try
                {
                    ProfileJson.Parse(json);
                }
                catch (CatalogFormatException exception)
                {
                    throw new CatalogConfigException($"The inline profile is invalid: {exception.Message}", exception);
                }

                return (null, json);
            default:
                throw new CatalogConfigException("'profile' must be a profile id, the path of a profile file or a profile object.");
        }
    }

    private static string ProfileReference(string value, string directory) =>
        value.EndsWith(ProfileFileSuffix, StringComparison.OrdinalIgnoreCase) ? Path.GetFullPath(value, directory) : value;
}
