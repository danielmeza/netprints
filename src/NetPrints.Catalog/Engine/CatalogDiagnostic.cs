namespace NetPrints.Catalog;

/// <summary>The severity of a <see cref="CatalogDiagnostic"/>.</summary>
public enum CatalogDiagnosticSeverity
{
    /// <summary>Something was skipped or ignored; the result is still produced.</summary>
    Warning,

    /// <summary>The operation failed.</summary>
    Error,
}

/// <summary>A problem found while building or loading a catalog.</summary>
/// <param name="Code">One of <see cref="CatalogDiagnosticCodes"/>.</param>
/// <param name="Severity">How serious it is.</param>
/// <param name="Message">A human-readable description.</param>
/// <param name="Source">The file, assembly or symbol it concerns, when known.</param>
public sealed record CatalogDiagnostic(string Code, CatalogDiagnosticSeverity Severity, string Message, string? Source = null);
