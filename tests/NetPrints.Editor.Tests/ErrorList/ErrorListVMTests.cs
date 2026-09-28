using System.Reactive.Subjects;
using CommunityToolkit.Mvvm.Messaging;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.Diagnostics;
using NetPrints.Editor.ErrorList;
using NetPrints.Translator;

namespace NetPrints.Editor.Tests.ErrorList;

/// <summary><see cref="ErrorListVM"/> (FR-032, OWN-03: severity counts in the "Errors" tab header).</summary>
public sealed class ErrorListVMTests
{
    private static ClassGraph NewClass(string ns, string name) => new() { Namespace = ns, Name = name };

    private static CodeDiagnostic Diagnostic(CodeDiagnosticSeverity severity, string id) =>
        new(severity, id, "boom", "N.C", null, null, null, null);

    [Fact]
    public void HeaderCountsErrorsAndWarningsAcrossRows()
    {
        using var host = new FakeCodeAnalysisHost();
        using var vm = new ErrorListVM(NewClass("N", "C"), host, new StrongReferenceMessenger());

        host.Push(new CodeAnalysisSnapshot(new Dictionary<string, TranslatedClass>(StringComparer.Ordinal),
        [
            Diagnostic(CodeDiagnosticSeverity.Error, "CS0001"),
            Diagnostic(CodeDiagnosticSeverity.Error, "CS0002"),
            Diagnostic(CodeDiagnosticSeverity.Warning, "CS0003"),
        ]));

        Assert.Equal(2, vm.ErrorCount);
        Assert.Equal(1, vm.WarningCount);
        Assert.Equal(0, vm.InfoCount);
        Assert.Equal("Errors (2) · Warnings (1)", vm.Header);
    }

    [Fact]
    public void HeaderAddsAnInfoSuffixOnlyWhenThereIsAtLeastOneInfoDiagnostic()
    {
        using var host = new FakeCodeAnalysisHost();
        using var vm = new ErrorListVM(NewClass("N", "C"), host, new StrongReferenceMessenger());

        host.Push(new CodeAnalysisSnapshot(new Dictionary<string, TranslatedClass>(StringComparer.Ordinal),
            [Diagnostic(CodeDiagnosticSeverity.Info, "NPT001")]));

        Assert.Equal(1, vm.InfoCount);
        Assert.Equal("Errors (0) · Warnings (0) · Info (1)", vm.Header);
    }

    [Fact]
    public void HeaderStaysAtZeroCountsBeforeAnySnapshot()
    {
        using var host = new FakeCodeAnalysisHost();
        using var vm = new ErrorListVM(NewClass("N", "C"), host, new StrongReferenceMessenger());

        Assert.Equal("Errors (0) · Warnings (0)", vm.Header);
    }

    private sealed class FakeCodeAnalysisHost : ICodeAnalysisHost
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
}
