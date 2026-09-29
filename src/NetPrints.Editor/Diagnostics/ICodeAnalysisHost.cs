using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Translator;

namespace NetPrints.Editor.Diagnostics;

/// <summary>
/// Debounced background analysis of the open project's generated code (editor-services.md §2):
/// translates every class and runs it through a <see cref="CodeAnalysisSession"/> off the UI thread,
/// so the code view and error list get live diagnostics and quick info without a build.
/// </summary>
public interface ICodeAnalysisHost : IDisposable
{
    /// <summary>
    /// The latest analysis result, emitted on the UI thread. A new subscriber gets the most recent
    /// snapshot immediately (empty before the first <see cref="RequestAnalysis"/> completes).
    /// </summary>
    IObservable<CodeAnalysisSnapshot> Snapshots { get; }

    /// <summary>
    /// Requests analysis of <paramref name="project"/>'s current classes. Debounced 500 ms on the
    /// scheduler passed to the constructor; a request that arrives before the debounce window elapses
    /// cancels the pending one, and a request whose analysis is still running cancels that run too.
    /// </summary>
    /// <param name="project">Project to translate and analyze.</param>
    void RequestAnalysis(Project project);

    /// <summary>
    /// Gets the signature and documentation summary of the symbol at <paramref name="position"/> in
    /// <paramref name="classFullName"/>'s generated code, from the last analysis that completed.
    /// </summary>
    /// <param name="classFullName">Full name of the class to look the symbol up in.</param>
    /// <param name="position">Character offset into the class's generated code.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The symbol's quick info, or <see langword="null"/> if none is available.</returns>
    Task<QuickInfo?> GetQuickInfoAsync(string classFullName, int position, CancellationToken cancellationToken);
}

/// <summary>
/// One completed analysis: every class that translated successfully, and every diagnostic found
/// (translation failures of the classes that did not, plus the live compiler diagnostics of the ones
/// that did).
/// </summary>
/// <param name="Classes">Successfully translated classes, keyed by <see cref="TranslatedClass.FullName"/>.</param>
/// <param name="Diagnostics">Every diagnostic found, translation and compiler alike.</param>
public sealed record CodeAnalysisSnapshot(IReadOnlyDictionary<string, TranslatedClass> Classes, IReadOnlyList<CodeDiagnostic> Diagnostics)
{
    /// <summary>An empty snapshot, published before the first analysis completes.</summary>
    public static CodeAnalysisSnapshot Empty { get; } = new(new Dictionary<string, TranslatedClass>(StringComparer.Ordinal), []);
}
