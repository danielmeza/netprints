using System;
using System.Collections.Generic;
using System.IO;
using NetPrints.Projects;

namespace NetPrints.Catalog;

/// <summary>The resolved sources of a catalog run: every reference the compilation needs and the assemblies to catalog.</summary>
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

    /// <summary>Deletes the temporary project directories; the caller does this after a successful run and keeps them after a failure.</summary>
    public void DeleteTemporaryDirectories()
    {
        foreach (string directory in TemporaryDirectories)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
