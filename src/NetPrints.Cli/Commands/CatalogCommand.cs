using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NetPrints.Catalog;
using NetPrints.Cli.Infrastructure;
using NetPrints.Compilation;
using NetPrints.Extensibility.Loading;
using NetPrints.Generation;
using NetPrints.Projects;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Commands;

/// <summary>
/// <c>netprints catalog</c>: builds a catalog of the types and members of assemblies, packages or a project's references, or with
/// <c>--check</c> reports that the stored file is missing or different (contracts/catalog.md §4).
/// </summary>
internal sealed class CatalogCommand(
    IAnsiConsole console,
    CliEnvironment environment,
    IMsBuildRegistration msBuild,
    ILoggerFactory loggerFactory,
    Lazy<IProjectSystem> projects,
    IProcessRunner processes) : AsyncCommand<CatalogSettings>
{
    /// <summary>The command name.</summary>
    public const string Name = "catalog";

    private const string DefaultNamespace = "NetPrints.Catalogs";
    private const string ClassSuffix = "Catalog";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <inheritdoc/>
    public override async Task<int> ExecuteAsync(CommandContext context, CatalogSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!TryResolveConfig(settings, out ResolvedCatalogConfig? config, out int failure))
        {
            return failure;
        }

        if (!msBuild.EnsureRegistered(loggerFactory.CreateLogger(nameof(IMsBuildRegistration))))
        {
            await environment.Error.WriteLineAsync("No .NET SDK could be found; no catalog was built.").ConfigureAwait(false);
            return ExitCodes.NoSdk;
        }

        try
        {
            return await RunAsync(settings, config, cancellationToken).ConfigureAwait(false);
        }
        catch (ProjectSystemException ex)
        {
            await environment.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            return ex.Code == ProjectSystemException.NoSdkRegistered ? ExitCodes.NoSdk : ExitCodes.Failed;
        }
        catch (CatalogSourceException ex)
        {
            await environment.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            return ExitCodes.Failed;
        }
    }

    private static string? ConfigPath(CatalogSettings settings, string currentDirectory)
    {
        if (settings.Config is { Length: > 0 } explicitPath)
        {
            return Path.GetFullPath(explicitPath, currentDirectory);
        }

        string candidate = Path.Combine(currentDirectory, CatalogConfig.FileName);
        return File.Exists(candidate) ? candidate : null;
    }

    private static CatalogSourceConfig? ProjectSource(ResolvedCatalogConfig config) =>
        config.Sources.FirstOrDefault(source => !string.IsNullOrEmpty(source.Project));

    private static string DefaultClassName(string id)
    {
        string name = string.Concat(id.Split(['.', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        return (char.IsDigit(name[0]) ? "_" : string.Empty) + name + ClassSuffix;
    }

    private static string Describe(CatalogDiagnostic diagnostic) =>
        $"{diagnostic.Source ?? Name}: {diagnostic.Severity.ToString().ToLowerInvariant()} {diagnostic.Code}: {diagnostic.Message}";

    private static int CountMembers(CatalogDocument document) =>
        document.Types.Sum(type => (type.Constructors?.Count ?? 0) + (type.Methods?.Count ?? 0) + (type.Variables?.Count ?? 0));

    private bool TryResolveConfig(CatalogSettings settings, [NotNullWhen(true)] out ResolvedCatalogConfig? config, out int failure)
    {
        config = null;
        failure = ExitCodes.Success;
        string currentDirectory = environment.CurrentDirectory;
        string? configPath = ConfigPath(settings, currentDirectory);
        if (configPath is null && !settings.HasSource)
        {
            environment.Error.WriteLine($"No configuration file ({CatalogConfig.FileName} in the current directory or --config) and no source (--assembly, --package or --project).");
            failure = ExitCodes.Usage;
            return false;
        }

        string? project = null;
        if (settings.Project is not null)
        {
            ProjectLocation location = ProjectLocator.Locate(settings.Project, environment);
            project = location.Path;
            if (project is null)
            {
                environment.Error.WriteLine(location.Error ?? "The project could not be resolved.");
                failure = ExitCodes.Usage;
                return false;
            }
        }

        try
        {
            CatalogConfig? file = configPath is null ? null : CatalogConfigResolver.Read(configPath);
            config = CatalogConfigResolver.Resolve(file, configPath is null ? null : Path.GetDirectoryName(configPath), ToOverrides(settings, project), currentDirectory);
        }
        catch (CatalogConfigException ex)
        {
            environment.Error.WriteLine(ex.Message);
            failure = ExitCodes.Failed;
            return false;
        }

        if (config.Id is not null && !CatalogIdentity.IsValidId(config.Id))
        {
            environment.Error.WriteLine($"'{config.Id}' is not a valid catalog id: use lower-case letters, digits, '.', '_' and '-', starting with a letter or digit.");
            failure = ExitCodes.Usage;
            return false;
        }

        return true;
    }

    private static CatalogOverrides ToOverrides(CatalogSettings settings, string? project)
    {
        List<CatalogSourceConfig> sources =
        [
            .. settings.Assemblies.Select(path => new CatalogSourceConfig { Assembly = path }),
            .. settings.Packages.Select(package =>
            {
                (string id, string version) = CatalogSettings.SplitPackage(package);
                return new CatalogSourceConfig { Package = id, Version = version };
            }),
        ];
        if (project is not null)
        {
            sources.Add(new CatalogSourceConfig { Project = project, Assemblies = settings.ProjectAssemblies });
        }

        return new CatalogOverrides
        {
            Sources = sources.Count > 0 ? sources : null,
            ReferencePaths = settings.ReferencePaths,
            TargetFramework = settings.Framework,
            Include = settings.Include,
            Exclude = settings.Exclude,
            Profile = settings.Profile,
            Id = settings.Id,
            Version = settings.CatalogVersion,
            OutputPath = settings.Output,
            Format = settings.OutputFormat,
            ClassName = settings.ClassName,
            Namespace = settings.Namespace,
            Extensions = settings.Extensions,
        };
    }

    private async Task<int> RunAsync(CatalogSettings settings, ResolvedCatalogConfig config, CancellationToken cancellationToken)
    {
        ProjectSnapshot? project = null;
        if (ProjectSource(config) is { Project: { } projectPath })
        {
            project = await projects.Value.LoadAsync(projectPath, cancellationToken).ConfigureAwait(false);
            if (ProjectMessageFormat.WriteErrors(project.Messages, environment.Error))
            {
                return ExitCodes.Failed;
            }
        }

        IReadOnlyList<string> folders = [.. config.Extensions, .. project?.ExtensionFolders ?? []];
        (ExtensionRegistry registry, IReadOnlyList<CodeDiagnostic> extensionDiagnostics) = GraphCodeGenerator.LoadExtensions(
            new GenerateRequest(string.Empty, null, string.Empty, [], [.. folders]), cancellationToken);
        await using (registry.ConfigureAwait(false))
        {
            if (extensionDiagnostics.Count > 0)
            {
                foreach (CodeDiagnostic diagnostic in extensionDiagnostics)
                {
                    console.WriteLineRaw(CodeDiagnosticFormat.ToCanonicalLine(diagnostic));
                }

                return ExitCodes.Failed;
            }

            CatalogProfile? profile = SelectProfile(config, registry, project, out int failure);
            return profile is null
                ? failure
                : await BuildAsync(settings, config, profile, cancellationToken).ConfigureAwait(false);
        }
    }

    private CatalogProfile? SelectProfile(ResolvedCatalogConfig config, ExtensionRegistry registry, ProjectSnapshot? project, out int failure)
    {
        failure = ExitCodes.Failed;
        try
        {
            CatalogProfile profile;
            if (config.InlineProfileJson is not null)
            {
                profile = ProfileJson.Parse(config.InlineProfileJson);
            }
            else if (config.ProfileReference is { } reference && reference.EndsWith(".npprofile.json", StringComparison.OrdinalIgnoreCase))
            {
                profile = ProfileJson.Parse(File.ReadAllText(reference));
            }
            else if (config.ProfileReference is { } id)
            {
                CatalogProfile? found = FindProfile(id, registry);
                if (found is null)
                {
                    environment.Error.WriteLine(UnknownProfileMessage(id, registry));
                    failure = ExitCodes.Usage;
                    return null;
                }

                profile = found;
            }
            else
            {
                CatalogProfile? projectDefault = ProjectDefaultProfile(project, registry);
                if (projectDefault is null && ProjectProfileId(project, registry) is { } missing)
                {
                    environment.Error.WriteLine($"The project's profile selects the catalog profile '{missing}', which no built-in profile or loaded extension provides.");
                    return null;
                }

                profile = projectDefault ?? BuiltInCatalogProfiles.PublicApi;
            }

            return WithGlobs(profile, config);
        }
        catch (CatalogFormatException ex)
        {
            environment.Error.WriteLine(ex.Message);
            return null;
        }
        catch (IOException ex)
        {
            environment.Error.WriteLine($"Cannot read the profile file: {ex.Message}");
            return null;
        }
    }

    private static CatalogProfile? FindProfile(string id, ExtensionRegistry registry) =>
        BuiltInCatalogProfiles.TryGet(id) ?? registry.CatalogProfiles.FirstOrDefault(profile => string.Equals(profile.Id, id, StringComparison.Ordinal));

    private static string? ProjectProfileId(ProjectSnapshot? project, ExtensionRegistry registry) =>
        project is null ? null : registry.FindProfile(project.ProfileId)?.CatalogProfileId is { Length: > 0 } id ? id : null;

    private static CatalogProfile? ProjectDefaultProfile(ProjectSnapshot? project, ExtensionRegistry registry) =>
        ProjectProfileId(project, registry) is { } id ? FindProfile(id, registry) : null;

    private static string UnknownProfileMessage(string id, ExtensionRegistry registry) =>
        $"Unknown catalog profile '{id}'. Available: {string.Join(", ", BuiltInCatalogProfiles.Ids.Concat(registry.CatalogProfiles.Select(profile => profile.Id)))}; or pass a *.npprofile.json file.";

    private static CatalogProfile WithGlobs(CatalogProfile profile, ResolvedCatalogConfig config) =>
        config.Include.Count == 0 && config.Exclude.Count == 0
            ? profile
            : profile with
            {
                IncludeTypes = [.. profile.IncludeTypes ?? [], .. config.Include],
                ExcludeTypes = [.. profile.ExcludeTypes ?? [], .. config.Exclude],
            };

    private async Task<int> BuildAsync(CatalogSettings settings, ResolvedCatalogConfig config, CatalogProfile profile, CancellationToken cancellationToken)
    {
        CatalogSourceSet sources = await new CatalogSourceResolver(projects.Value, processes).ResolveAsync(config, cancellationToken).ConfigureAwait(false);
        WriteDiagnostics(sources.Diagnostics);
        if (sources.Targets.Count == 0 || sources.Diagnostics.Any(diagnostic => diagnostic.Severity == CatalogDiagnosticSeverity.Error))
        {
            return ExitCodes.Failed;
        }

        CatalogCompilationInput input = CatalogCompilationFactory.Create(sources);
        CatalogBuildResult result = CatalogBuilder.Build(input.Compilation, input.Assemblies, new CatalogProfileFilter(profile), input.Documentation, new CatalogIdentity(config.Id, config.Version));
        WriteDiagnostics(result.Diagnostics);
        if (result.Diagnostics.Any(diagnostic => diagnostic.Severity == CatalogDiagnosticSeverity.Error))
        {
            return ExitCodes.Failed;
        }

        (string Path, string Text)? output = Render(config, result.Document);
        if (output is null)
        {
            return ExitCodes.Usage;
        }

        int exitCode = await WriteAsync(output.Value.Path, output.Value.Text, result.Document, settings.Check, cancellationToken).ConfigureAwait(false);
        if (exitCode != ExitCodes.Usage)
        {
            sources.DeleteTemporaryDirectories();
        }

        return exitCode;
    }

    private (string Path, string Text)? Render(ResolvedCatalogConfig config, CatalogDocument document)
    {
        if (config.Format == CatalogOutputFormat.Catalog)
        {
            return (config.OutputPath ?? Path.Combine(config.BaseDirectory, document.Id + ".npcat.json"), CanonicalCatalogWriter.Write(document));
        }

        string className = config.ClassName ?? DefaultClassName(document.Id);
        try
        {
            string text = CatalogCSharpEmitter.Emit(document, config.Namespace ?? DefaultNamespace, className);
            return (config.OutputPath ?? Path.Combine(config.BaseDirectory, className + ".g.cs"), text);
        }
        catch (ArgumentException ex)
        {
            environment.Error.WriteLine(ex.Message);
            return null;
        }
    }

    private async Task<int> WriteAsync(string path, string text, CatalogDocument document, bool check, CancellationToken cancellationToken)
    {
        byte[] bytes = Utf8.GetBytes(text);
        string summary = $"({document.Types.Count} types, {CountMembers(document)} members)";
        bool same = File.Exists(path) && (await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false)).AsSpan().SequenceEqual(bytes);
        if (same)
        {
            console.WriteLineRaw($"up to date: {path} {summary}");
            return ExitCodes.Success;
        }

        if (check)
        {
            console.WriteLineRaw($"stale: {path}");
            return ExitCodes.Failed;
        }

        try
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } directory)
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await environment.Error.WriteLineAsync($"Cannot write '{path}': {ex.Message}").ConfigureAwait(false);
            return ExitCodes.Failed;
        }

        console.WriteLineRaw($"wrote {path} {summary}");
        return ExitCodes.Success;
    }

    private void WriteDiagnostics(IEnumerable<CatalogDiagnostic> diagnostics)
    {
        foreach (CatalogDiagnostic diagnostic in diagnostics)
        {
            console.WriteLineRaw(Describe(diagnostic));
        }
    }
}
