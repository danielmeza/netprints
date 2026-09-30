using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using NetPrints.Projects;

namespace NetPrints.Catalog;

/// <summary>The resolved sources of a catalog run: every reference the compilation needs and the assemblies to catalog.</summary>
[Experimental(ExperimentalApis.CatalogProfiles, UrlFormat = ExperimentalApis.UrlFormat)]
public sealed class CatalogSourceSet
{
    /// <summary>Creates a source set.</summary>
    /// <param name="references">Every assembly the compilation references, framework and dependencies included.</param>
    /// <param name="targets">The assemblies to catalog, with their documentation files.</param>
    /// <param name="diagnostics">Problems found while resolving, such as an unreferenced assembly (NPC001).</param>
    /// <param name="temporaryDirectories">The temporary project directories to delete after a successful run.</param>
    public CatalogSourceSet(
        IReadOnlyList<ResolvedAssembly> references,
        IReadOnlyList<ResolvedAssembly> targets,
        IReadOnlyList<CatalogDiagnostic> diagnostics,
        IReadOnlyList<string> temporaryDirectories)
    {
        References = references ?? throw new ArgumentNullException(nameof(references));
        Targets = targets ?? throw new ArgumentNullException(nameof(targets));
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        TemporaryDirectories = temporaryDirectories ?? throw new ArgumentNullException(nameof(temporaryDirectories));
    }

    /// <summary>Every assembly the compilation references.</summary>
    public IReadOnlyList<ResolvedAssembly> References { get; }

    /// <summary>The assemblies to catalog.</summary>
    public IReadOnlyList<ResolvedAssembly> Targets { get; }

    /// <summary>The problems found while resolving.</summary>
    public IReadOnlyList<CatalogDiagnostic> Diagnostics { get; }

    /// <summary>The temporary project directories (<c>obj/netprints-catalog/&lt;hash&gt;</c>).</summary>
    public IReadOnlyList<string> TemporaryDirectories { get; }

    /// <summary>
    /// Deletes the temporary project directories, then <c>obj/netprints-catalog</c> and <c>obj</c> when they are left empty; the caller does this after a successful run
    /// and keeps the directories after a failure. Best effort: a file another process holds (a virus scanner, an MSBuild node) does not fail a run that already wrote its output.
    /// </summary>
    /// <returns>One message per directory that could not be deleted; empty when everything was removed.</returns>
    public IReadOnlyList<string> DeleteTemporaryDirectories()
    {
        List<string> failures = [];
        foreach (string directory in TemporaryDirectories)
        {
            TryDelete(directory, recursive: true, failures);
        }

        foreach (string parent in TemporaryDirectories.Select(Path.GetDirectoryName).OfType<string>().Distinct(StringComparer.Ordinal))
        {
            if (IsEmptyDirectory(parent) && TryDelete(parent, recursive: false, failures) && Path.GetDirectoryName(parent) is { } obj && IsEmptyDirectory(obj))
            {
                TryDelete(obj, recursive: false, failures);
            }
        }

        return failures;
    }

    private static bool IsEmptyDirectory(string directory) => Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any();

    private static bool TryDelete(string directory, bool recursive, List<string> failures)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            failures.Add($"Could not delete the temporary directory '{directory}': {ex.Message}");
            return false;
        }
    }
}
