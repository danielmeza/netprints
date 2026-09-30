using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;

namespace NetPrints.Catalog;

/// <summary>Builds a catalog of the types and members of assemblies, as a profile filter selects them.</summary>
/// <remarks>
/// Only what a compilation exposes through symbols is read, so nothing is loaded or run. Members that use a type from
/// an assembly the compilation cannot resolve are left out with NPC005, and annotations on non-public symbols are
/// ignored with NPC004 (only visible when the compilation imports non-public metadata). The result does not depend
/// on the order of references or syntax trees.
/// </remarks>
[Experimental(ExperimentalApis.CatalogProfiles, UrlFormat = ExperimentalApis.UrlFormat)]
public static class CatalogBuilder
{
    /// <summary>Builds the catalog of <paramref name="assemblies"/>.</summary>
    /// <param name="compilation">The compilation the assembly symbols belong to.</param>
    /// <param name="assemblies">The assemblies to catalog, at least one; the first gives the default id and version.</param>
    /// <param name="filter">Selects the types and members.</param>
    /// <param name="documentation">Supplies the documentation text.</param>
    /// <param name="identity">The catalog id and version, or defaults.</param>
    /// <returns>The catalog and the diagnostics raised while building it.</returns>
    /// <exception cref="ArgumentException"><paramref name="assemblies"/> is empty or the catalog id is not valid.</exception>
    public static CatalogBuildResult Build(
        RoslynCompilation compilation,
        IReadOnlyList<IAssemblySymbol> assemblies,
        ICatalogFilter filter,
        IDocumentationSource documentation,
        CatalogIdentity identity)
    {
        Guard.NotNull(compilation, nameof(compilation));
        Guard.NotNull(assemblies, nameof(assemblies));
        Guard.NotNull(filter, nameof(filter));
        Guard.NotNull(documentation, nameof(documentation));
        Guard.NotNull(identity, nameof(identity));
        if (assemblies.Count == 0)
        {
            throw new ArgumentException("At least one assembly is required.", nameof(assemblies));
        }

        IAssemblySymbol first = assemblies[0];
        string id = identity.Id ?? new string(first.Name.Select(char.ToLowerInvariant).ToArray());
        if (!CatalogIdentity.IsValidId(id))
        {
            throw new ArgumentException($"'{id}' is not a valid catalog id; it must match {CatalogIdentity.IdPattern}.", nameof(identity));
        }

        CatalogWalker walker = new(compilation, filter, documentation);
        foreach (IAssemblySymbol assembly in assemblies.OrderBy(assembly => assembly.Name, StringComparer.Ordinal))
        {
            walker.Visit(assembly.GlobalNamespace);
        }

        CatalogDocument document = new()
        {
            Id = id,
            Version = identity.Version ?? first.Identity.Version.ToString(),
            Profile = filter.ProfileId,
            Assemblies =
            [
                .. assemblies
                    .Select(assembly => new CatalogAssembly { Name = assembly.Name, Version = assembly.Identity.Version.ToString() })
                    .OrderBy(assembly => assembly.Name, StringComparer.Ordinal),
            ],
            Types = [.. walker.Types.OrderBy(type => type.Id, StringComparer.Ordinal)],
        };
        return new CatalogBuildResult(document, walker.SortedDiagnostics());
    }
}
