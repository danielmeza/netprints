using System.Reactive.Subjects;
using CommunityToolkit.Mvvm.Messaging;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Diagnostics;
using NetPrints.Editor.ErrorList;
using NetPrints.Translator;

namespace NetPrints.Editor.Tests.ErrorList;

/// <summary><see cref="ErrorListViewModel"/> (FR-032, OWN-03: severity counts in the "Errors" tab header).</summary>
public sealed class ErrorListViewModelTests
{
    private static ClassGraph NewClass(string ns, string name) => new() { Namespace = ns, Name = name };

    private static CodeDiagnostic Diagnostic(CodeDiagnosticSeverity severity, string id) =>
        new(severity, id, "boom", "N.C", null, null, null, null);

    [Fact]
    public void HeaderCountsErrorsAndWarningsAcrossRows()
    {
        using var host = new FakeCodeAnalysisHost();
        using var vm = new ErrorListViewModel(NewClass("N", "C"), host, new StrongReferenceMessenger());

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
        using var vm = new ErrorListViewModel(NewClass("N", "C"), host, new StrongReferenceMessenger());

        host.Push(new CodeAnalysisSnapshot(new Dictionary<string, TranslatedClass>(StringComparer.Ordinal),
            [Diagnostic(CodeDiagnosticSeverity.Info, "NPT001")]));

        Assert.Equal(1, vm.InfoCount);
        Assert.Equal("Errors (0) · Warnings (0) · Info (1)", vm.Header);
    }

    [Fact]
    public void ARowListIsFilledFromTheLastBuildAtOnceNotOnlyAfterTheNextSnapshot()
    {
        using var host = new FakeCodeAnalysisHost();
        var project = Project.FromSnapshot(TestSnapshots.Empty("P", "N"));
        var cls = new ClassGraph { Namespace = "N", Name = "C", Project = project };
        project.LastDiagnostics = new ObservableRangeCollection<CodeDiagnostic>([Diagnostic(CodeDiagnosticSeverity.Error, "CS0001")]);

        using var vm = new ErrorListViewModel(cls, host, new StrongReferenceMessenger());

        Assert.Equal(["CS0001"], vm.Rows.Select(row => row.Id));
        Assert.Equal(1, vm.ErrorCount);
    }

    [Fact]
    public void HeaderStaysAtZeroCountsBeforeAnySnapshot()
    {
        using var host = new FakeCodeAnalysisHost();
        using var vm = new ErrorListViewModel(NewClass("N", "C"), host, new StrongReferenceMessenger());

        Assert.Equal("Errors (0) · Warnings (0)", vm.Header);
    }

    [Fact]
    public void NavigatingANavigableRowSendsTheMessage()
    {
        // FR-034, ED-T03: a diagnostic mapped to a node sends both the graph key and the node id.
        using var host = new FakeCodeAnalysisHost();
        var messenger = new StrongReferenceMessenger();
        using var vm = new ErrorListViewModel(NewClass("N", "C"), host, messenger);
        NavigateToNodeMessage? received = null;
        messenger.Register<ErrorListViewModelTests, NavigateToNodeMessage>(this, (_, m) => received = m);

        host.Push(new CodeAnalysisSnapshot(new Dictionary<string, TranslatedClass>(StringComparer.Ordinal),
            [new CodeDiagnostic(CodeDiagnosticSeverity.Error, "CS1503", "boom", "N.C", "m1", "n1", null, null)]));

        vm.NavigateCommand.Execute(vm.Rows.Single());

        Assert.Equal(new NavigateToNodeMessage("m1", "n1"), received);
    }

    [Fact]
    public void NavigatingARowWithNoNodeMappingStillSendsTheMessageOpenTheGraphOnly()
    {
        // OWN-04 (FR-034): a diagnostic with a known member but no node still asks to open the graph;
        // ErrorListViewModel.Navigate must not bail out just because NodeId is unknown.
        using var host = new FakeCodeAnalysisHost();
        var messenger = new StrongReferenceMessenger();
        using var vm = new ErrorListViewModel(NewClass("N", "C"), host, messenger);
        NavigateToNodeMessage? received = null;
        messenger.Register<ErrorListViewModelTests, NavigateToNodeMessage>(this, (_, m) => received = m);

        host.Push(new CodeAnalysisSnapshot(new Dictionary<string, TranslatedClass>(StringComparer.Ordinal),
            [new CodeDiagnostic(CodeDiagnosticSeverity.Error, "CS0161", "boom", "N.C", "m1", null, null, null)]));

        DiagnosticRowViewModel row = vm.Rows.Single();
        Assert.True(row.CanNavigate);

        vm.NavigateCommand.Execute(row);

        Assert.Equal(new NavigateToNodeMessage("m1", null), received);
    }

    [Fact]
    public void NavigatingARowWithNoGraphKeyDoesNothing()
    {
        using var host = new FakeCodeAnalysisHost();
        var messenger = new StrongReferenceMessenger();
        using var vm = new ErrorListViewModel(NewClass("N", "C"), host, messenger);
        bool received = false;
        messenger.Register<ErrorListViewModelTests, NavigateToNodeMessage>(this, (_, _) => received = true);

        host.Push(new CodeAnalysisSnapshot(new Dictionary<string, TranslatedClass>(StringComparer.Ordinal),
            [Diagnostic(CodeDiagnosticSeverity.Error, "CS0103")]));

        vm.NavigateCommand.Execute(vm.Rows.Single());

        Assert.False(received);
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
