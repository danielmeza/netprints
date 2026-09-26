#nullable enable
using NetPrints.Compilation;

namespace NetPrints.Serialization;

/// <summary>
/// Converts serialization-layer problems to the editor's structured diagnostic rows
/// (compilation-and-diagnostics.md §1).
/// </summary>
public static class DiagnosticExtensions
{
    /// <summary>
    /// Converts <paramref name="issue"/> to a <see cref="CodeDiagnostic"/>: <see cref="DocumentIssue.Code"/>
    /// becomes <see cref="CodeDiagnostic.Id"/>, <see cref="DocumentIssue.Severity"/> maps one-to-one, and
    /// <see cref="DocumentIssue.Document"/>'s path (if any) becomes <see cref="CodeDiagnostic.SourcePath"/>.
    /// The class, graph key, node id and span are not known at this level and are left
    /// <see langword="null"/>.
    /// </summary>
    /// <param name="issue">Issue to convert.</param>
    /// <returns>The equivalent diagnostic.</returns>
    public static CodeDiagnostic ToDiagnostic(this DocumentIssue issue)
    {
        return new CodeDiagnostic(
            Severity: issue.Severity switch
            {
                DocumentIssueSeverity.Info => CodeDiagnosticSeverity.Info,
                DocumentIssueSeverity.Warning => CodeDiagnosticSeverity.Warning,
                _ => CodeDiagnosticSeverity.Error,
            },
            Id: issue.Code,
            Message: issue.Message,
            ClassFullName: null,
            GraphKey: null,
            NodeId: null,
            SourcePath: issue.Document?.Path,
            Span: null);
    }
}
