using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using NetPrints.Reflection;

namespace NetPrints.Catalog;

/// <summary>Loads catalogs as run-time type catalogs (FR-017).</summary>
public static class CatalogLoader
{
    /// <summary>Creates the type catalog of a catalog document.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The catalog, which answers the reflection queries for the types the document lists.</returns>
    public static ITypeCatalog Load(CatalogDocument document) => new CatalogTypeCatalog(document);

    /// <summary>Reads a catalog file (<c>*.npcat.json</c>) and creates its type catalog.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The catalog.</returns>
    /// <exception cref="CatalogFormatException">The file is not a supported catalog (NPC101, NPC102).</exception>
    public static ITypeCatalog LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using FileStream stream = File.OpenRead(path);
        return Load(CatalogReader.Read(stream));
    }

    /// <summary>Reads catalog JSON and creates its type catalog.</summary>
    /// <param name="json">The catalog text.</param>
    /// <returns>The catalog.</returns>
    /// <exception cref="CatalogFormatException">The text is not a supported catalog (NPC101, NPC102).</exception>
    public static ITypeCatalog LoadJson(string json) => Load(CatalogReader.Read(json));

    /// <summary>
    /// Keeps the first catalog of every id, in the order given. Every later catalog with an id already seen is dropped and reported
    /// as <c>NPC103</c> through <paramref name="logger"/>.
    /// </summary>
    /// <param name="catalogs">The catalogs, in registry order.</param>
    /// <param name="logger">Receives the NPC103 warnings.</param>
    /// <returns>The catalogs with distinct ids.</returns>
    public static IReadOnlyList<ITypeCatalog> FirstOfEachId(IEnumerable<ITypeCatalog> catalogs, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(catalogs);
        ArgumentNullException.ThrowIfNull(logger);

        List<ITypeCatalog> kept = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (ITypeCatalog catalog in catalogs)
        {
            if (ids.Add(catalog.Info.Id))
            {
                kept.Add(catalog);
            }
            else
            {
                Log.DuplicateCatalogId(logger, catalog.Info.Id, catalog.Info.Version);
            }
        }

        return kept;
    }
}
