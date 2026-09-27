using CommunityToolkit.Mvvm.ComponentModel;
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
