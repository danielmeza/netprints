namespace NetPrints.Catalog;

/// <summary>Supplies the normalized documentation text of a symbol, looked up by its documentation comment id.</summary>
public interface IDocumentationSource
{
    /// <summary>Gets the normalized <c>&lt;summary&gt;</c> of a symbol.</summary>
    /// <param name="documentationId">The documentation comment id, for example <c>T:Ns.Type</c>.</param>
    /// <returns>The text, or <see langword="null"/> when there is none.</returns>
    string? GetSummary(string documentationId);

    /// <summary>Gets the normalized <c>&lt;param&gt;</c> text of a method parameter.</summary>
    /// <param name="documentationId">The documentation comment id of the method.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <returns>The text, or <see langword="null"/> when there is none.</returns>
    string? GetParameter(string documentationId, string parameterName);

    /// <summary>Gets the normalized <c>&lt;returns&gt;</c> text of a method.</summary>
    /// <param name="documentationId">The documentation comment id of the method.</param>
    /// <returns>The text, or <see langword="null"/> when there is none.</returns>
    string? GetReturns(string documentationId);
}
