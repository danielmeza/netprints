using System.Reactive.Subjects;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.Diagnostics;
using NetPrints.Translator;

namespace NetPrints.Editor.Tests.CodeView;

/// <summary>A live analysis host whose snapshots a test pushes.</summary>
public sealed class PushableCodeAnalysisHost : ICodeAnalysisHost
{
    private readonly Subject<CodeAnalysisSnapshot> snapshots = new();

    public IObservable<CodeAnalysisSnapshot> Snapshots => snapshots;

    public void Push(CodeAnalysisSnapshot snapshot) => snapshots.OnNext(snapshot);

    public void RequestAnalysis(Project project)
    {
    }

    public Task<QuickInfo?> GetQuickInfoAsync(string classFullName, int position, CancellationToken cancellationToken) =>
        Task.FromResult<QuickInfo?>(null);

    public void Dispose() => snapshots.Dispose();
}
