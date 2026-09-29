using NetPrints.Compilation;

namespace NetPrints.Editor.Dialogs;

/// <summary>View model for <see cref="IssuesDialog"/>: formats the diagnostics into rows (D3/D16,
/// ADR-0007 batch F15; the dialog itself has no result, so this is not a <see cref="DialogVM{TResult}"/>).</summary>
public sealed class IssuesDialogVM
{
    /// <summary>Formats <paramref name="issues"/> for the dialog's list.</summary>
    /// <param name="issues">The diagnostics to list.</param>
    public IssuesDialogVM(IReadOnlyList<CodeDiagnostic> issues)
    {
        Issues = [.. issues.Select(issue => $"{issue.Id}: {issue.Message}")];
    }

    /// <summary>The diagnostics, formatted as <c>Id: Message</c> rows.</summary>
    public IReadOnlyList<string> Issues { get; }
}
