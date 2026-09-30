using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace NetPrints.Catalog;

/// <summary>The id and version a built catalog gets; a value left out defaults to the first covered assembly's name (lower-cased) and version.</summary>
/// <param name="Id">The catalog id: <c>[a-z0-9][a-z0-9._-]*</c>.</param>
/// <param name="Version">The catalog version.</param>
[Experimental(ExperimentalApis.CatalogProfiles, UrlFormat = ExperimentalApis.UrlFormat)]
public sealed record CatalogIdentity(string? Id = null, string? Version = null)
{
    internal const string IdPattern = "^[a-z0-9][a-z0-9._-]*$";

    private static readonly Regex IdRegex = new(IdPattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>Returns whether <paramref name="id"/> is a valid catalog or profile id.</summary>
    /// <param name="id">The id to check.</param>
    /// <returns><see langword="true"/> when it matches <c>[a-z0-9][a-z0-9._-]*</c>.</returns>
    public static bool IsValidId(string? id) => id is not null && IdRegex.IsMatch(id);
}
