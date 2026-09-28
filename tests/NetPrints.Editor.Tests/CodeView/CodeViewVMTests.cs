using System.Reactive.Subjects;
using Microsoft.CodeAnalysis.Text;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.CodeView;
using NetPrints.Editor.Diagnostics;
using NetPrints.Translator;

namespace NetPrints.Editor.Tests.CodeView;

/// <summary><see cref="CodeViewVM"/> (editor-services.md §3): follows one class's snapshots, ignoring others.</summary>
public sealed class CodeViewVMTests
{
    private static ClassGraph NewClass(string ns, string name) => new() { Namespace = ns, Name = name };

    [Fact]
    public void FollowsItsOwnClassAndIgnoresOthers()
    {
        using var host = new FakeCodeAnalysisHost();
        using var vm = new CodeViewVM(NewClass("N", "C"), host);

        var translated = new TranslatedClass("N.C", "public class C { public void M() {} }", SourceMap.Empty);
        var otherTranslated = new TranslatedClass("N.Other", "public class Other {}", SourceMap.Empty);
        var diagnostic = new CodeDiagnostic(CodeDiagnosticSeverity.Error, "CS0001", "boom", "N.C", null, null, null, null);
        var otherDiagnostic = diagnostic with { ClassFullName = "N.Other" };

        host.Push(new CodeAnalysisSnapshot(new Dictionary<string, TranslatedClass>(StringComparer.Ordinal)
        {
            ["N.C"] = translated,
            ["N.Other"] = otherTranslated,
        }, [diagnostic, otherDiagnostic]));

        Assert.Equal(translated.Code, vm.Code);
        CodeDiagnostic onlyDiagnostic = Assert.Single(vm.Diagnostics);
        Assert.Equal("CS0001", onlyDiagnostic.Id);
        FoldingRange onlyFolding = Assert.Single(vm.Foldings);
        Assert.Equal(RoslynFoldingStrategy.CollapsedPlaceholder, onlyFolding.Title);
    }

    [Fact]
    public void ASnapshotWithoutItsClassIsIgnored()
    {
        using var host = new FakeCodeAnalysisHost();
        using var vm = new CodeViewVM(NewClass("N", "C"), host);

        host.Push(new CodeAnalysisSnapshot(new Dictionary<string, TranslatedClass>(StringComparer.Ordinal)
        {
            ["N.Other"] = new TranslatedClass("N.Other", "public class Other { }", SourceMap.Empty),
        }, []));

        Assert.Equal(string.Empty, vm.Code);
        Assert.Empty(vm.Diagnostics);
    }

    [Fact]
    public async Task GetQuickInfoAsyncDelegatesWithItsOwnClassFullName()
    {
        using var host = new FakeCodeAnalysisHost { QuickInfoResult = new QuickInfo("void M()", "Summary.") };
        using var vm = new CodeViewVM(NewClass("N", "C"), host);

        QuickInfo? info = await vm.GetQuickInfoAsync(42, TestContext.Current.CancellationToken);

        Assert.Equal(("N.C", 42), host.LastQuickInfoRequest);
        Assert.Equal("void M()", info?.Signature);
    }

    [Fact]
    public async Task GetHoverContentAsyncReturnsNullWithNoDiagnosticOrQuickInfo()
    {
        using var host = new FakeCodeAnalysisHost();
        using var vm = new CodeViewVM(NewClass("N", "C"), host);

        Assert.Null(await vm.GetHoverContentAsync(0, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetHoverContentAsyncShowsTheDiagnosticAboveTheQuickInfoInsideItsSpan()
    {
        // OWN-02 (owner report): hovering a squiggle shows the diagnostic(s) under the cursor above
        // the symbol quick info.
        using var host = new FakeCodeAnalysisHost { QuickInfoResult = new QuickInfo("void M()", null) };
        using var vm = new CodeViewVM(NewClass("N", "C"), host);

        var translated = new TranslatedClass("N.C", "class C { void M() { } }", SourceMap.Empty);
        var span = new LinePositionSpan(new LinePosition(0, 10), new LinePosition(0, 11));
        var diagnostic = new CodeDiagnostic(CodeDiagnosticSeverity.Error, "CS0001", "boom", "N.C", null, null, null, span);
        host.Push(new CodeAnalysisSnapshot(new Dictionary<string, TranslatedClass>(StringComparer.Ordinal) { ["N.C"] = translated }, [diagnostic]));

        string? content = await vm.GetHoverContentAsync(10, TestContext.Current.CancellationToken);

        Assert.NotNull(content);
        Assert.Contains("CS0001", content, StringComparison.Ordinal);
        Assert.Contains("boom", content, StringComparison.Ordinal);
        Assert.True(content.IndexOf("boom", StringComparison.Ordinal) < content.IndexOf("void M()", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetHoverContentAsyncIgnoresADiagnosticOutsideItsSpan()
    {
        using var host = new FakeCodeAnalysisHost { QuickInfoResult = new QuickInfo("void M()", null) };
        using var vm = new CodeViewVM(NewClass("N", "C"), host);

        var translated = new TranslatedClass("N.C", "class C { void M() { } }", SourceMap.Empty);
        var span = new LinePositionSpan(new LinePosition(0, 10), new LinePosition(0, 11));
        var diagnostic = new CodeDiagnostic(CodeDiagnosticSeverity.Error, "CS0001", "boom", "N.C", null, null, null, span);
        host.Push(new CodeAnalysisSnapshot(new Dictionary<string, TranslatedClass>(StringComparer.Ordinal) { ["N.C"] = translated }, [diagnostic]));

        string? content = await vm.GetHoverContentAsync(0, TestContext.Current.CancellationToken);

        Assert.Equal("void M()", content);
    }

    private sealed class FakeCodeAnalysisHost : ICodeAnalysisHost
    {
        private readonly Subject<CodeAnalysisSnapshot> snapshots = new();

        public QuickInfo? QuickInfoResult { get; set; }

        public (string ClassFullName, int Position)? LastQuickInfoRequest { get; private set; }

        public IObservable<CodeAnalysisSnapshot> Snapshots => snapshots;

        public void Push(CodeAnalysisSnapshot snapshot) => snapshots.OnNext(snapshot);

        public void RequestAnalysis(Project project)
        {
        }

        public Task<QuickInfo?> GetQuickInfoAsync(string classFullName, int position, CancellationToken cancellationToken)
        {
            LastQuickInfoRequest = (classFullName, position);
            return Task.FromResult(QuickInfoResult);
        }

        public void Dispose() => snapshots.Dispose();
    }
}
