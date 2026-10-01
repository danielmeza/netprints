using System.Reactive.Subjects;
using System.Reflection;
using Microsoft.CodeAnalysis.Text;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.CodeView;
using NetPrints.Editor.Diagnostics;
using NetPrints.Translator;

namespace NetPrints.Editor.Tests.CodeView;

/// <summary><see cref="CodeViewViewModel"/> (editor-services.md §3): follows one class's snapshots, ignoring others.</summary>
public sealed class CodeViewVMTests
{
    private static ClassGraph NewClass(string ns, string name) => new() { Namespace = ns, Name = name };

    [Fact]
    public void FollowsItsOwnClassAndIgnoresOthers()
    {
        using var host = new FakeCodeAnalysisHost();
        using var vm = new CodeViewViewModel(NewClass("N", "C"), host);

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
        using var vm = new CodeViewViewModel(NewClass("N", "C"), host);

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
        using var vm = new CodeViewViewModel(NewClass("N", "C"), host);

        QuickInfo? info = await vm.GetQuickInfoAsync(42, TestContext.Current.CancellationToken);

        Assert.Equal(("N.C", 42), host.LastQuickInfoRequest);
        Assert.Equal("void M()", info?.Signature);
    }

    [Fact]
    public async Task GetHoverContentAsyncReturnsNullWithNoDiagnosticOrQuickInfo()
    {
        using var host = new FakeCodeAnalysisHost();
        using var vm = new CodeViewViewModel(NewClass("N", "C"), host);

        Assert.Null(await vm.GetHoverContentAsync(0, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetHoverContentAsyncShowsTheDiagnosticAboveTheQuickInfoInsideItsSpan()
    {
        // OWN-02 (owner report): hovering a squiggle shows the diagnostic(s) under the cursor above
        // the symbol quick info.
        using var host = new FakeCodeAnalysisHost { QuickInfoResult = new QuickInfo("void M()", null) };
        using var vm = new CodeViewViewModel(NewClass("N", "C"), host);

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
        using var vm = new CodeViewViewModel(NewClass("N", "C"), host);

        var translated = new TranslatedClass("N.C", "class C { void M() { } }", SourceMap.Empty);
        var span = new LinePositionSpan(new LinePosition(0, 10), new LinePosition(0, 11));
        var diagnostic = new CodeDiagnostic(CodeDiagnosticSeverity.Error, "CS0001", "boom", "N.C", null, null, null, span);
        host.Push(new CodeAnalysisSnapshot(new Dictionary<string, TranslatedClass>(StringComparer.Ordinal) { ["N.C"] = translated }, [diagnostic]));

        string? content = await vm.GetHoverContentAsync(0, TestContext.Current.CancellationToken);

        Assert.Equal("void M()", content);
    }

    /// <summary>
    /// R2-12: <see cref="CodeViewViewModel.ShowQuickInfoCommand"/> cancels whatever lookup is still in flight,
    /// but a superseding call must win even if the superseded lookup's own completion is not observed
    /// through the cancellation token (an uncooperative or already-running dependency) — so
    /// <see cref="ControllableCodeAnalysisHost"/> deliberately ignores the token and only completes
    /// when the test tells it to.
    /// </summary>
    [Fact]
    public async Task ShowQuickInfoCommandNeverLetsASupersededLookupOverwriteTheCurrentOne()
    {
        using var host = new ControllableCodeAnalysisHost();
        using var vm = new CodeViewViewModel(NewClass("N", "C"), host);

        Task first = vm.ShowQuickInfoCommand.ExecuteAsync(1);
        Task second = vm.ShowQuickInfoCommand.ExecuteAsync(2);

        host.Complete(2, new QuickInfo("second", null));
        await second;
        Assert.Equal("second", vm.QuickInfoText);

        // The superseded lookup finally completes; it must not resurrect its own (now stale) content.
        host.Complete(1, new QuickInfo("first", null));
        await first;
        Assert.Equal("second", vm.QuickInfoText);
    }

    /// <summary>
    /// R2-12/OWN-01: a lookup that completes after the pointer already left must not reopen the
    /// tooltip. <see cref="CodeView.CodeView.OnPointerHoverStopped"/> calls <c>ClearQuickInfoCommand</c>;
    /// this exercises the view model half of that guarantee directly.
    /// </summary>
    [Fact]
    public async Task ClearQuickInfoStopsALateResultFromReopeningTheTooltip()
    {
        using var host = new ControllableCodeAnalysisHost();
        using var vm = new CodeViewViewModel(NewClass("N", "C"), host);

        Task lookup = vm.ShowQuickInfoCommand.ExecuteAsync(1);
        vm.ClearQuickInfoCommand.Execute(null);
        Assert.Null(vm.QuickInfoText);

        host.Complete(1, new QuickInfo("late", null)); // the pointer already left by the time this arrives
        await lookup;

        Assert.Null(vm.QuickInfoText);
    }

    /// <summary>
    /// Same swap-before-await bug as <c>ClassEditorViewModel</c>'s F-04: <c>ShowQuickInfoAsync</c> used to
    /// reinstall <c>quickInfoCancellation</c> only after awaiting the superseded lookup's
    /// <c>CancelAsync</c>, so a <see cref="CodeViewViewModel.ClearQuickInfoCommand"/> (pointer exit) landing
    /// in that await got clobbered by the very lookup that superseded it, resurrecting the tooltip.
    /// A real race depends on that await genuinely yielding, which is not reliably forceable headless;
    /// instead this registers a callback on the first lookup's own token — CancellationTokenSource
    /// guarantees a registered callback runs during Cancel/CancelAsync — so firing ClearQuickInfo from
    /// it deterministically reproduces the exact interleaving without any real thread race.
    /// </summary>
    [Fact]
    public async Task ClearQuickInfoDuringASupersedingCancelIsNotResurrectedByTheSupersedingLookup()
    {
        using var host = new ControllableCodeAnalysisHost();
        using var vm = new CodeViewViewModel(NewClass("N", "C"), host);

        Task first = vm.ShowQuickInfoCommand.ExecuteAsync(1);
        CancellationTokenSource cts1 = QuickInfoCancellationOf(vm)
            ?? throw new InvalidOperationException("No CTS was installed after the first lookup.");

        var callbackRan = new TaskCompletionSource();
        cts1.Token.Register(() =>
        {
            vm.ClearQuickInfoCommand.Execute(null); // the pointer leaves while lookup 2 is cancelling lookup 1
            callbackRan.SetResult();
        });

        var requested2 = new TaskCompletionSource();
        host.OnRequested = position =>
        {
            if (position == 2)
            {
                requested2.TrySetResult();
            }
        };

        Task second = vm.ShowQuickInfoCommand.ExecuteAsync(2);
        await callbackRan.Task;
        await requested2.Task; // lookup 2 must have actually started before it can be completed

        host.Complete(2, new QuickInfo("second", null));
        await second;
        Assert.Null(vm.QuickInfoText);

        host.Complete(1, new QuickInfo("first", null));
        await first;
        Assert.Null(vm.QuickInfoText);
    }

    /// <summary>Reads <c>CodeViewViewModel</c>'s private <c>quickInfoCancellation</c> field (no public seam
    /// exists for it) so a test can force a callback onto a specific lookup's own token.</summary>
    private static CancellationTokenSource? QuickInfoCancellationOf(CodeViewViewModel target)
    {
        FieldInfo field = typeof(CodeViewViewModel).GetField("quickInfoCancellation", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("CodeViewViewModel.quickInfoCancellation field not found; the fixture is stale.");
        return (CancellationTokenSource?)field.GetValue(target);
    }

    /// <summary>A quick-info lookup whose completion the test controls, ignoring the cancellation token
    /// so tests can exercise <see cref="CodeViewViewModel"/>'s own "still current?" guard rather than relying
    /// on the token being observed.</summary>
    private sealed class ControllableCodeAnalysisHost : ICodeAnalysisHost
    {
        private readonly Subject<CodeAnalysisSnapshot> snapshots = new();
        private readonly Dictionary<int, TaskCompletionSource<QuickInfo?>> pending = new();

        public IObservable<CodeAnalysisSnapshot> Snapshots => snapshots;

        /// <summary>Raised synchronously as each lookup is requested, so a test can wait for a specific
        /// superseding lookup to have actually started before completing it.</summary>
        public Action<int>? OnRequested { get; set; }

        public void RequestAnalysis(Project project)
        {
        }

        public Task<QuickInfo?> GetQuickInfoAsync(string classFullName, int position, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<QuickInfo?>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending[position] = completion;
            OnRequested?.Invoke(position);
            return completion.Task;
        }

        /// <summary>Completes the pending lookup at <paramref name="position"/> with <paramref name="result"/>.</summary>
        public void Complete(int position, QuickInfo? result) => pending[position].TrySetResult(result);

        public void Dispose() => snapshots.Dispose();
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
