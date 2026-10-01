using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NetPrints.Catalog;

/// <summary>The profiles every catalog tool knows without a file: <c>public-api</c> and <c>annotated</c>.</summary>
[Experimental(ExperimentalApis.CatalogProfiles, UrlFormat = ExperimentalApis.UrlFormat)]
public static class BuiltInCatalogProfiles
{
    /// <summary>Gets the profile over every public type and member (plus protected members of unsealed types).</summary>
    public static CatalogProfile PublicApi { get; } = new(CatalogProfile.PublicApiId, CatalogProfileBase.PublicApi);

    /// <summary>Gets the profile over symbols carrying the <c>NetPrints*</c> annotations.</summary>
    public static CatalogProfile Annotated { get; } = new(CatalogProfile.AnnotatedId, CatalogProfileBase.Annotated);

    /// <summary>Gets the ids of the built-in profiles.</summary>
    public static IReadOnlyList<string> Ids { get; } = [CatalogProfile.PublicApiId, CatalogProfile.AnnotatedId];

    /// <summary>Finds a built-in profile by id.</summary>
    /// <param name="id">The profile id, compared ordinally.</param>
    /// <returns>The profile, or <see langword="null"/> when <paramref name="id"/> is not a built-in id.</returns>
    public static CatalogProfile? TryGet(string id)
    {
        if (string.Equals(id, CatalogProfile.PublicApiId, StringComparison.Ordinal))
        {
            return PublicApi;
        }

        return string.Equals(id, CatalogProfile.AnnotatedId, StringComparison.Ordinal) ? Annotated : null;
    }
}
