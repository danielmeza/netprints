using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Compilation;
using NetPrints.Editor.Diagnostics;
using NetPrints.Translator;

namespace NetPrints.Editor.CodeView;

/// <summary>
/// View model of the read-only C# code view (editor-services.md §3, FR-031..035): one class's last
/// translated code, its diagnostics and its foldings, kept live from
/// <see cref="ICodeAnalysisHost.Snapshots"/>. Depends only on <see cref="ICodeAnalysisHost"/> (FR-038:
/// no dependency on the parent editor).
/// </summary>
public sealed partial class CodeViewVM : ObservableObject, IDisposable
{
    private readonly string classFullName;
    private readonly ICodeAnalysisHost codeAnalysis;
    private readonly IDisposable subscription;

    /// <summary>
    /// Creates a code view model that follows <paramref name="classFullName"/> in
    /// <paramref name="codeAnalysis"/>'s snapshots.
    /// </summary>
    /// <param name="classFullName">Full name of the class to show, as translated (<see cref="TranslatedClass.FullName"/>).</param>
    /// <param name="codeAnalysis">Live analysis host to follow.</param>
    public CodeViewVM(string classFullName, ICodeAnalysisHost codeAnalysis)
    {
        ArgumentException.ThrowIfNullOrEmpty(classFullName);
        ArgumentNullException.ThrowIfNull(codeAnalysis);

        this.classFullName = classFullName;
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
        codeAnalysis.GetQuickInfoAsync(classFullName, position, cancellationToken);

    private void OnSnapshot(CodeAnalysisSnapshot snapshot)
    {
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
