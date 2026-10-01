using System.Collections.Generic;

namespace NetPrints.Catalog;

/// <summary>The outcome of <c>CatalogBuilder.Build</c>: the catalog and what was skipped or ignored while building it.</summary>
/// <param name="Document">The catalog.</param>
/// <param name="Diagnostics">The warnings raised while building, sorted by source, code and message.</param>
public sealed record CatalogBuildResult(CatalogDocument Document, IReadOnlyList<CatalogDiagnostic> Diagnostics);
