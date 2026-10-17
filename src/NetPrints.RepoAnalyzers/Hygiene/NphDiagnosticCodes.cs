namespace NetPrints.RepoAnalyzers.Hygiene;

/// <summary>The diagnostic ids of the repository hygiene analyzers (contracts/analyzer-diagnostics.md §4).</summary>
internal static class NphDiagnosticCodes
{
    /// <summary>The null-forgiving operator, <c>null!</c> or <c>default!</c>.</summary>
    public const string NullForgiving = "NPH001";

    /// <summary>A type name that ends in, or embeds, <c>VM</c>.</summary>
    public const string ViewModelSuffix = "NPH002";

    /// <summary>A diagnostic or kind code literal outside its constants class.</summary>
    public const string RawDiagnosticCode = "NPH003";

    /// <summary>A pragma, a nullable escape or a <c>SuppressMessage</c> that the ledger does not list.</summary>
    public const string UnlistedSuppression = "NPH004";

    /// <summary><c>ConfigureAwait(false)</c> in UI-bound code.</summary>
    public const string ConfigureAwaitFalse = "NPH005";

    /// <summary>A string literal passed to a known UI sink.</summary>
    public const string LiteralUiText = "NPH006";

    /// <summary>The category of every NPH diagnostic.</summary>
    public const string Category = "Hygiene";
}
