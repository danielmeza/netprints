using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Projects;

namespace NetPrints.Catalog;

/// <summary>
/// Resolves the sources of a catalog run through SDK projects (research R9): a <c>project</c> source is the user's project, an
/// <c>assembly</c> or <c>package</c> source goes into a temporary project under <c>obj/netprints-catalog/&lt;hash&gt;/</c> that ignores
/// inherited <c>Directory.Build.props</c> and central package management and is restored with <c>dotnet restore</c>.
/// Nothing is downloaded by the tool itself.
/// </summary>
[Experimental(ExperimentalApis.CatalogProfiles, UrlFormat = ExperimentalApis.UrlFormat)]
public sealed class CatalogSourceResolver
{
    private const string NuGetPackageRootProperty = "NuGetPackageRoot";

    private const string PackagesEnvironmentVariable = "NUGET_PACKAGES";

    private readonly IProjectSystem projects;

    private readonly IProcessRunner processes;

    /// <summary>Creates a resolver.</summary>
    /// <param name="projects">Loads projects and reads their resolved references.</param>
    /// <param name="processes">Runs <c>dotnet restore</c>.</param>
    public CatalogSourceResolver(IProjectSystem projects, IProcessRunner processes)
    {
        this.projects = projects ?? throw new ArgumentNullException(nameof(projects));
        this.processes = processes ?? throw new ArgumentNullException(nameof(processes));
    }

    /// <summary>Resolves every source of <paramref name="config"/>.</summary>
    /// <param name="config">The merged settings.</param>
    /// <param name="cancellationToken">Cancels the restore and the loads.</param>
    /// <returns>The references, the assemblies to catalog and the problems found.</returns>
    /// <exception cref="CatalogSourceException">The restore failed.</exception>
    /// <exception cref="ProjectSystemException">No SDK is registered, or a project could not be evaluated.</exception>
    public async Task<CatalogSourceSet> ResolveAsync(ResolvedCatalogConfig config, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);

        List<ResolvedAssembly> references = [];
        List<ResolvedAssembly> targets = [];
        List<CatalogDiagnostic> diagnostics = [];
        List<string> temporaryDirectories = [];

        (List<string> assemblyFiles, List<(string Id, string Version)> packages) = CollectTemporarySources(config, diagnostics);
        if (assemblyFiles.Count > 0 || packages.Count > 0)
        {
            string directory = TemporaryCatalogProject.DirectoryFor(config.BaseDirectory, config.TargetFramework, assemblyFiles, packages, config.ReferencePaths);
            temporaryDirectories.Add(directory);
            ProjectSnapshot snapshot = await LoadTemporaryProjectAsync(directory, config, assemblyFiles, packages, cancellationToken).ConfigureAwait(false);
            ThrowOnErrors(snapshot);
            references.AddRange(snapshot.References);
            SelectAssemblyTargets(snapshot, assemblyFiles, targets);
            SelectPackageTargets(snapshot, packages, targets, diagnostics);
        }

        foreach (CatalogSourceConfig source in config.Sources.Where(source => !string.IsNullOrEmpty(source.Project)))
        {
            ProjectSnapshot snapshot = await projects.LoadAsync(source.Project ?? string.Empty, cancellationToken).ConfigureAwait(false);
            ThrowOnErrors(snapshot);
            references.AddRange(snapshot.References);
            SelectProjectTargets(snapshot, source, targets, diagnostics);
        }

        return new CatalogSourceSet(
            [.. references.DistinctBy(reference => reference.Path, PathComparer)],
            [.. targets.Select(WithDocumentation).DistinctBy(target => target.Path, PathComparer)],
            diagnostics,
            temporaryDirectories);
    }

    private static void ThrowOnErrors(ProjectSnapshot snapshot)
    {
        string[] errors = [.. snapshot.Messages.Where(message => message.Severity == ProjectMessageSeverity.Error).Select(message => message.Message)];
        if (errors.Length > 0)
        {
            throw new CatalogSourceException($"Loading '{snapshot.ProjectFilePath}' failed:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
        }
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static (List<string> Assemblies, List<(string Id, string Version)> Packages) CollectTemporarySources(ResolvedCatalogConfig config, List<CatalogDiagnostic> diagnostics)
    {
        List<string> assemblies = [];
        List<(string Id, string Version)> packages = [];
        foreach (CatalogSourceConfig source in config.Sources)
        {
            if (!string.IsNullOrEmpty(source.Assembly))
            {
                List<string> files = ExpandAssembly(source.Assembly);
                if (files.Count == 0)
                {
                    diagnostics.Add(new CatalogDiagnostic(CatalogDiagnosticCodes.UnreferencedAssembly, CatalogDiagnosticSeverity.Error, $"No assembly matches '{source.Assembly}'.", source.Assembly));
                }

                assemblies.AddRange(files);
            }
            else if (!string.IsNullOrEmpty(source.Package))
            {
                packages.Add((source.Package, source.Version ?? string.Empty));
            }
        }

        return ([.. assemblies.Distinct(PathComparer)], packages);
    }

    private static List<string> ExpandAssembly(string pattern)
    {
        string fileName = Path.GetFileName(pattern);
        if (fileName.IndexOfAny(['*', '?']) < 0)
        {
            return File.Exists(pattern) ? [pattern] : [];
        }

        string? directory = Path.GetDirectoryName(pattern);
        return directory is not null && Directory.Exists(directory)
            ? [.. Directory.EnumerateFiles(directory, fileName).Order(StringComparer.Ordinal)]
            : [];
    }

    private async Task<ProjectSnapshot> LoadTemporaryProjectAsync(
        string directory,
        ResolvedCatalogConfig config,
        List<string> assemblyFiles,
        List<(string Id, string Version)> packages,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        string projectPath = Path.Combine(directory, TemporaryCatalogProject.FileName);
        await File.WriteAllTextAsync(projectPath, TemporaryCatalogProject.Render(config.TargetFramework, assemblyFiles, packages, config.ReferencePaths), cancellationToken).ConfigureAwait(false);

        ProcessResult restore = await processes
            .RunAsync(new ProcessStartRequest("dotnet", ["restore", projectPath, "--nologo", "-v", "q"], directory), cancellationToken)
            .ConfigureAwait(false);
        if (restore.ExitCode != 0)
        {
            throw new CatalogSourceException($"dotnet restore failed for the temporary project '{projectPath}' (exit {restore.ExitCode}):{Environment.NewLine}{restore.StandardOutput}{restore.StandardError}".TrimEnd());
        }

        return await projects.LoadAsync(projectPath, cancellationToken).ConfigureAwait(false);
    }

    private static void SelectAssemblyTargets(ProjectSnapshot snapshot, List<string> assemblyFiles, List<ResolvedAssembly> targets)
    {
        foreach (string file in assemblyFiles)
        {
            ResolvedAssembly? match = snapshot.References.FirstOrDefault(reference => string.Equals(Path.GetFullPath(reference.Path), file, PathComparison));
            targets.Add(match ?? new ResolvedAssembly(file, null));
        }
    }

    private static void SelectPackageTargets(ProjectSnapshot snapshot, List<(string Id, string Version)> packages, List<ResolvedAssembly> targets, List<CatalogDiagnostic> diagnostics)
    {
        string root = snapshot.GetProperty(NuGetPackageRootProperty)
            ?? Environment.GetEnvironmentVariable(PackagesEnvironmentVariable)
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        foreach ((string id, string version) in packages)
        {
            // NuGet stores 1.0 as 1.0.0 and drops +metadata; after the restore the id folder holds the one resolved version.
            string prefix = Path.Combine(root, id.ToLowerInvariant()) + Path.DirectorySeparatorChar;
            List<ResolvedAssembly> own = [.. snapshot.References.Where(reference => reference.Path.StartsWith(prefix, PathComparison))];
            if (own.Count == 0)
            {
                diagnostics.Add(new CatalogDiagnostic(CatalogDiagnosticCodes.UnreferencedAssembly, CatalogDiagnosticSeverity.Error, $"The package '{id}' {version} has no assemblies for the target framework.", id));
            }

            targets.AddRange(own);
        }
    }

    private static void SelectProjectTargets(ProjectSnapshot snapshot, CatalogSourceConfig source, List<ResolvedAssembly> targets, List<CatalogDiagnostic> diagnostics)
    {
        foreach (string name in source.Assemblies ?? [])
        {
            ResolvedAssembly? match = snapshot.References.FirstOrDefault(reference => string.Equals(Path.GetFileNameWithoutExtension(reference.Path), name, StringComparison.Ordinal));
            if (match is null)
            {
                diagnostics.Add(new CatalogDiagnostic(CatalogDiagnosticCodes.UnreferencedAssembly, CatalogDiagnosticSeverity.Error, $"The project '{source.Project}' does not reference an assembly named '{name}'.", name));
            }
            else
            {
                targets.Add(match);
            }
        }
    }

    private static ResolvedAssembly WithDocumentation(ResolvedAssembly assembly)
    {
        if (assembly.DocumentationPath is not null)
        {
            return assembly;
        }

        string sibling = Path.ChangeExtension(assembly.Path, ".xml");
        return File.Exists(sibling) ? assembly with { DocumentationPath = sibling } : assembly;
    }
}
