namespace NetPrints.Catalog;

/// <summary>
/// The <see cref="System.Diagnostics.CodeAnalysis.ExperimentalAttribute"/> id and help link of the catalog profile API
/// (ADR-0017). The source generator compiles this file and cannot reference <c>NetPrints.Core</c>, so the second
/// declaration of the id lives here.
/// </summary>
internal static class ExperimentalApis
{
    /// <summary>The id of the catalog engine and profile API.</summary>
    public const string CatalogProfiles = "NPXE0004";

    /// <summary>The guide section that explains the stability promise and how to opt in.</summary>
    public const string UrlFormat = "https://danielmeza.github.io/netprints/guide/extensions#api-stability";
}
