using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Diagnostics;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Graph;
using NetPrints.Projects;
using NetPrints.Reflection;

namespace NetPrints.Editor.Tests.Diagnostics;

/// <summary>
/// <see cref="CodeAnalysisHost"/> (editor-services.md §2): ED-T02 at the host level (VM/host tests;
/// headless UI coverage of ED-T01/T03/T05 is T097's).
/// </summary>
public sealed class CodeAnalysisHostTests(TestEditor editor)
{
    /// <summary>ED-T02: a type error introduced via the model yields a diagnostic within the virtual-time debounce; fixing it clears it.</summary>
    [Fact]
    public async Task TypeErrorYieldsADiagnosticWithinTheDebounceThenFixingItClearsIt()
    {
        var cls = new ClassGraph { Name = "CodeAnalysisHostFixture", Namespace = "NetPrints.Editor.Tests.Diagnostics" };
        var method = new MethodGraph("Run") { Class = cls, Visibility = MemberVisibility.Public };

        // Guid.Parse(string) has no int overload, so wiring an int literal into its argument is a
        // genuine CS1503 once compiled (the graph model does not itself enforce pin type compatibility).
        TypeSpecifier stringType = TypeSpecifier.FromType<string>();
        var parseSpecifier = new MethodSpecifier("Parse",
            [new MethodParameter("input", stringType, MethodParameterPassType.Default, false, null)],
            [], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType<Guid>(), []);

        var callNode = new CallMethodNode(method, parseSpecifier);
        var badArgument = LiteralNode.WithValue(method, 123);
        GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, callNode.InputExecPins[0]);
        GraphUtil.ConnectExecPins(callNode.OutputExecPins[0], method.ReturnNodes.First().InputExecPins[0]);
        GraphUtil.ConnectDataPins(badArgument.ValuePin, callNode.ArgumentPins[0]);
        cls.Methods.Add(method);

        var project = Project.FromSnapshot(TestSnapshots.Empty("CodeAnalysisHostFixture", "N"));
        project.Classes.Add(cls);

        CodeAnalysisSnapshot? latest = null;
        using IDisposable subscription = editor.CodeAnalysis.Snapshots.Subscribe(snapshot => latest = snapshot);

        editor.CodeAnalysis.RequestAnalysis(project);
        editor.Scheduler.AdvanceBy(CodeAnalysisHost.DebounceWindow.Ticks);
        CodeAnalysisSnapshot withError = await WaitForAsync(() => latest, s => s.Diagnostics.Any(d => d.Id == "CS1503"));

        Assert.Contains(withError.Diagnostics, d => d.Id == "CS1503" && d.ClassFullName == cls.FullName);

        var goodArgument = LiteralNode.WithValue(method, "ok");
        GraphUtil.ConnectDataPins(goodArgument.ValuePin, callNode.ArgumentPins[0]);

        editor.CodeAnalysis.RequestAnalysis(project);
        editor.Scheduler.AdvanceBy(CodeAnalysisHost.DebounceWindow.Ticks);
        CodeAnalysisSnapshot fixedSnapshot = await WaitForAsync(() => latest, s => !s.Diagnostics.Any(d => d.Id == "CS1503"));

        Assert.DoesNotContain(fixedSnapshot.Diagnostics, d => d.Id == "CS1503");
    }

    /// <summary>A reflection reload replaces the analysis session, so the last requested project is analyzed again against the new one.</summary>
    [Fact]
    public async Task AReflectionReloadAnalyzesTheLastRequestedProjectAgain()
    {
        var reflection = new ReloadableReflectionHost(editor.Reflection);
        using var host = new CodeAnalysisHost(reflection, editor.Extensions, editor.Scheduler, editor.Dispatcher, NullLogger<CodeAnalysisHost>.Instance);
        var project = Project.FromSnapshot(TestSnapshots.Empty("ReloadFixture", "N"));
        int snapshots = 0;
        using IDisposable subscription = host.Snapshots.Subscribe(_ => snapshots++);

        host.RequestAnalysis(project);
        editor.Scheduler.AdvanceBy(CodeAnalysisHost.DebounceWindow.Ticks);
        await WaitForCountAsync(() => snapshots, 2);

        reflection.RaiseReloaded();
        editor.Scheduler.AdvanceBy(CodeAnalysisHost.DebounceWindow.Ticks);
        await WaitForCountAsync(() => snapshots, 3);
    }

    private static async Task WaitForCountAsync(Func<int> count, int expected)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (count() < expected)
        {
            Assert.True(DateTime.UtcNow < deadline, $"Expected {expected} snapshots, saw {count()}.");
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }

    private sealed class ReloadableReflectionHost(IReflectionHost inner) : IReflectionHost
    {
        public bool IsLoaded => inner.IsLoaded;

        public Task Loaded => inner.Loaded;

        public IReflectionProvider Provider => inner.Provider;

        public ProjectSnapshot? Snapshot => inner.Snapshot;

        public ReadOnlyObservableCollection<TypeSpecifier> NonStaticTypes => inner.NonStaticTypes;

        public IReadOnlyList<string> LastWarnings => inner.LastWarnings;

        public event EventHandler? Reloaded;

        public Task ReloadAsync(Project project, CancellationToken cancellationToken = default) => inner.ReloadAsync(project, cancellationToken);

        public void RaiseReloaded() => Reloaded?.Invoke(this, EventArgs.Empty);
    }

    private static async Task<CodeAnalysisSnapshot> WaitForAsync(Func<CodeAnalysisSnapshot?> current, Func<CodeAnalysisSnapshot, bool> matches)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            if (current() is { } snapshot && matches(snapshot))
            {
                return snapshot;
            }

            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Condition not reached in time.");
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }
}
