using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CodeAnalysis.Text;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.Diagnostics;
using NetPrints.Translator;

namespace NetPrints.Editor.CodeView;

/// <summary>
/// View model of the read-only C# code view (editor-services.md §3, FR-031..035): one class's last
/// translated code, its diagnostics and its foldings, kept live from
/// <see cref="ICodeAnalysisHost.Snapshots"/>. Depends only on the class graph and
/// <see cref="ICodeAnalysisHost"/> (FR-038: no dependency on the parent editor). Reads
/// <see cref="ClassGraph.FullName"/> fresh on every snapshot rather than capturing it once, so
/// renaming the class does not stop the view from following it.
/// </summary>
public sealed partial class CodeViewVM : ObservableObject, IDisposable
{
    private readonly ClassGraph cls;
    private readonly ICodeAnalysisHost codeAnalysis;
    private readonly IDisposable subscription;

    /// <summary>
    /// Creates a code view model that follows <paramref name="cls"/> in
    /// <paramref name="codeAnalysis"/>'s snapshots.
    /// </summary>
    /// <param name="cls">Class to show the generated code of.</param>
    /// <param name="codeAnalysis">Live analysis host to follow.</param>
    public CodeViewVM(ClassGraph cls, ICodeAnalysisHost codeAnalysis)
    {
        ArgumentNullException.ThrowIfNull(cls);
        ArgumentNullException.ThrowIfNull(codeAnalysis);

        this.cls = cls;
        this.codeAnalysis = codeAnalysis;
        subscription = codeAnalysis.Snapshots.Subscribe(OnSnapshot);
    }

    /// <summary>The class's last translated C#, or empty before a snapshot has included it.</summary>
    [ObservableProperty]
    public partial string Code { get; set; } = string.Empty;

    /// <summary>The class's diagnostics from the last snapshot (translation and compiler alike).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<CodeDiagnostic> Diagnostics { get; set; } = [];

    /// <summary>Collapsible regions of <see cref="Code"/> (types and member bodies).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<FoldingRange> Foldings { get; set; } = [];

    /// <summary>
    /// Gets the signature and documentation summary of the symbol at <paramref name="position"/> in
    /// <see cref="Code"/>, from the last analysis that completed.
    /// </summary>
    /// <param name="position">Character offset into <see cref="Code"/>.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The symbol's quick info, or <see langword="null"/> if none is available.</returns>
    public Task<QuickInfo?> GetQuickInfoAsync(int position, CancellationToken cancellationToken) =>
        codeAnalysis.GetQuickInfoAsync(cls.FullName, position, cancellationToken);

    /// <summary>
    /// Gets the hover content for <paramref name="position"/> in <see cref="Code"/> (FR-035, ED-T05;
    /// OWN-01/OWN-02, owner report): any <see cref="Diagnostics"/> whose span covers the position,
    /// formatted above the symbol's quick info (from the last analysis that completed) when both apply.
    /// </summary>
    /// <param name="position">Character offset into <see cref="Code"/>.</param>
    /// <param name="cancellationToken">Cancels the quick-info lookup.</param>
    /// <returns>The hover text to show, or <see langword="null"/> if there is nothing to show.</returns>
    public async Task<string?> GetHoverContentAsync(int position, CancellationToken cancellationToken)
    {
        string? diagnosticsText = DiagnosticsAt(position) is { Count: > 0 } diagnostics
            ? string.Join('\n', diagnostics.Select(FormatDiagnostic))
            : null;

        QuickInfo? info = await GetQuickInfoAsync(position, cancellationToken);
        string? quickInfoText = info is null ? null : info.Summary is null ? info.Signature : $"{info.Signature}\n{info.Summary}";

        return (diagnosticsText, quickInfoText) switch
        {
            (null, null) => null,
            (null, _) => quickInfoText,
            (_, null) => diagnosticsText,
            _ => $"{diagnosticsText}\n\n{quickInfoText}",
        };
    }

    /// <summary>Every diagnostic in <see cref="Diagnostics"/> whose span covers <paramref name="position"/>.</summary>
    private IReadOnlyList<CodeDiagnostic> DiagnosticsAt(int position)
    {
        if (Diagnostics.Count == 0)
        {
            return [];
        }

        TextLineCollection lines = SourceText.From(Code).Lines;
        return [.. Diagnostics.Where(d => d.Span is { } span && Covers(lines, span, position))];
    }

    private static bool Covers(TextLineCollection lines, LinePositionSpan span, int position)
    {
        if (span.Start.Line < 0 || span.End.Line >= lines.Count)
        {
            return false;
        }

        int start = lines.GetPosition(span.Start);
        int end = lines.GetPosition(span.End);
        return position >= start && position < end;
    }

    private static string FormatDiagnostic(CodeDiagnostic diagnostic) => $"{diagnostic.Severity} {diagnostic.Id}: {diagnostic.Message}";

    private void OnSnapshot(CodeAnalysisSnapshot snapshot)
    {
        string classFullName = cls.FullName;
        if (!snapshot.Classes.TryGetValue(classFullName, out TranslatedClass? translated))
        {
            return;
        }

        Code = translated.Code;
        Diagnostics = [.. snapshot.Diagnostics.Where(d => string.Equals(d.ClassFullName, classFullName, StringComparison.Ordinal))];
        Foldings = RoslynFoldingStrategy.ComputeFoldings(translated.Code);
    }

    /// <summary>Unsubscribes from <see cref="ICodeAnalysisHost.Snapshots"/>.</summary>
    public void Dispose() => subscription.Dispose();
}
