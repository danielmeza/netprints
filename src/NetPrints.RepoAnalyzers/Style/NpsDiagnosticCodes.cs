namespace NetPrints.RepoAnalyzers.Style;

/// <summary>The diagnostic ids of the repository style analyzers (contracts/analyzer-diagnostics.md §2).</summary>
internal static class NpsDiagnosticCodes
{
    /// <summary>An attribute list shares a line with its member or with another attribute list.</summary>
    public const string AttributeOnOwnLine = "NPS001";

    /// <summary>No blank line between adjacent members that need one.</summary>
    public const string BlankLineBetweenMembers = "NPS002";

    /// <summary>Grouped non-public fields; a suggestion that never fails a build.</summary>
    public const string GroupedFields = "NPS003";

    /// <summary>The category of every NPS diagnostic.</summary>
    public const string Category = "Style";
}
