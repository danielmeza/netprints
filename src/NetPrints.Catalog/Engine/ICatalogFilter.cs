using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace NetPrints.Catalog;

/// <summary>
/// Decides which types and members a catalog contains. The builder only asks about types that are public (and
/// nested in public types) and members that are public or protected, so a filter never has to check visibility.
/// </summary>
[Experimental(ExperimentalApis.CatalogProfiles, UrlFormat = ExperimentalApis.UrlFormat)]
public interface ICatalogFilter
{
    /// <summary>Gets the id written to the catalog's <c>profile</c> property.</summary>
    string ProfileId { get; }

    /// <summary>Decides whether a type is cataloged; a nested type is only asked about when its declaring type is cataloged.</summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> to catalog the type.</returns>
    bool IncludeType(INamedTypeSymbol type);

    /// <summary>Decides whether a constructor, method, property or field is cataloged.</summary>
    /// <param name="member">The member.</param>
    /// <returns><see langword="true"/> to catalog the member.</returns>
    bool IncludeMember(ISymbol member);

    /// <summary>Describes how a type or method presents itself as a node, from its annotations.</summary>
    /// <param name="symbol">A cataloged type or method.</param>
    /// <returns>The hint, or <see langword="null"/> when there is none.</returns>
    CatalogNodeHint? DescribeNode(ISymbol symbol);
}
