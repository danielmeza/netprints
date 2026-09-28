using System.Windows.Input;
using CommunityToolkit.Mvvm.Messaging;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Diagnostics;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.ClassEditor;

public class ClassEditorVMTests : IAsyncLifetime
{
    private readonly TestEditor editor;
    private Project? projectField;
    private ClassGraph? clsField;
    private ClassEditorVM? vmField;

    public ClassEditorVMTests(TestEditor editor)
    {
        this.editor = editor;
    }

    private Project project => projectField ?? throw new InvalidOperationException($"{nameof(InitializeAsync)} has not run yet.");
    private ClassGraph cls => clsField ?? throw new InvalidOperationException($"{nameof(InitializeAsync)} has not run yet.");
    private ClassEditorVM vm => vmField ?? throw new InvalidOperationException($"{nameof(InitializeAsync)} has not run yet.");

    public async ValueTask InitializeAsync()
    {
        projectField = await TestPaths.LoadHelloWorldCopyAsync(TestContext.Current.CancellationToken);
        clsField = projectField.Classes.Single();
        vmField = new ClassEditorVM(clsField, editor.Context);
    }

    public ValueTask DisposeAsync()
    {
        vmField?.Dispose();
        TestPaths.TryDelete(projectField?.Path);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public void ListsMethodsAndTitle()
    {
        Assert.Equal("Program", vm.Title);
        Assert.Equal("Main", vm.Methods.Single().Name);
        Assert.Empty(vm.Constructors);
        Assert.Empty(vm.Variables);
    }

    [Fact]
    public void RenamingUpdatesTheFullName()
    {
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.Name = "Renamed";
        vm.Namespace = "Other";

        Assert.Equal(2, changed.Count(p => p == nameof(ClassEditorVM.FullName)));
        Assert.Equal("Other.Renamed", vm.FullName);
    }

    [Fact]
    public void CreateMethodConnectsEntryAndReturnAndOpensGraph()
    {
        vm.CreateMethodCommand.Execute(null);
        vm.CreateMethodCommand.Execute(null);

        Assert.Equal(new[] { "Main", "Method", "Method2" }, vm.Methods.Select(m => m.Name).ToArray());
        var method = (MethodGraph)vm.Methods[2].Graph;
        Assert.Same(method.MainReturnNode.ReturnPin, method.EntryNode.InitialExecutionPin.OutgoingPin);
        Assert.Equal(0, method.EntryNode.PositionX % 28);
        Assert.Equal(0, method.MainReturnNode.PositionX % 28);
        Assert.Same(method, vm.OpenedGraph?.Graph);
    }

    [Fact]
    public void CreateConstructorIsPublicAndOpens()
    {
        vm.CreateConstructorCommand.Execute(null);

        var ctor = vm.Constructors.Single();
        Assert.True(ctor.IsConstructor);
        Assert.Equal(MemberVisibility.Public, ctor.Visibility);
        Assert.Equal(112, ctor.Graph.EntryNode.PositionX);
        Assert.Same(ctor.Graph, vm.OpenedGraph?.Graph);
    }

    [Fact]
    public void OverrideChooserCreatesOpensAndResets()
    {
        var toString = vm.OverridableMethods.First(m => m.Name == "ToString");

        vm.SelectedOverride = toString;

        var created = vm.Methods.Single(m => m.Name == "ToString");
        Assert.True(((MethodGraph)created.Graph).Modifiers.HasFlag(MethodModifiers.Override));
        Assert.Same(created.Graph, vm.OpenedGraph?.Graph);
        Assert.Null(vm.SelectedOverride);
    }

    [Fact]
    public async Task OpenMethodCommandSelectsAndOpensInOneCall()
    {
        var main = vm.Methods.Single();

        await vm.OpenMethodCommand.ExecuteAsync(main);

        Assert.Same(main, vm.SelectedMethod);
        Assert.Equal(InspectorKind.Method, vm.Inspector);
        Assert.Same(main.Graph, vm.OpenedGraph?.Graph);
    }

    [Fact]
    public async Task ReclickingTheSameMethodReopensItAfterTheCanvasSwitchedAway()
    {
        // Regression: an earlier design opened only on a SelectedItem-changed hook, which does not
        // re-fire on a second click of a method that was never deselected (EventGraphTests'
        // "Open: switch away [to another graph, not by deselecting the method], then reopen").
        var main = vm.Methods.Single();
        await vm.OpenMethodCommand.ExecuteAsync(main);

        vm.ShowClassCommand.Execute(null);
        Assert.Same(cls, vm.OpenedGraph?.Graph);
        Assert.Same(main, vm.SelectedMethod); // still the list's selection

        await vm.OpenMethodCommand.ExecuteAsync(main);

        Assert.Same(main.Graph, vm.OpenedGraph?.Graph);
    }

    [Fact]
    public async Task ClickingTheAlreadyOpenMethodDoesNotReopenIt()
    {
        var main = vm.Methods.Single();
        await vm.OpenMethodCommand.ExecuteAsync(main);
        var openedGraph = vm.OpenedGraph;

        await vm.OpenMethodCommand.ExecuteAsync(main);

        Assert.Same(openedGraph, vm.OpenedGraph);
    }

    [Fact]
    public async Task ClickingADifferentMethodOpensItInstead()
    {
        vm.CreateMethodCommand.Execute(null);
        var main = vm.Methods.First();
        var second = vm.Methods.Last();

        await vm.OpenMethodCommand.ExecuteAsync(main);
        await vm.OpenMethodCommand.ExecuteAsync(second);

        Assert.Same(second, vm.SelectedMethod);
        Assert.Same(second.Graph, vm.OpenedGraph?.Graph);
    }

    [Fact]
    public async Task OpeningAConstructorClearsTheMethodsListSelectionAndViceVersa()
    {
        // R2-16: Methods and Constructors used to two-way bind the same SelectedMethod, so opening a
        // constructor pushed it into the Methods list (which does not contain it, giving -1), which
        // flickered the Methods list's own selection to nothing and could push a stale null back into
        // SelectedMethod through the two-way binding.
        var main = vm.Methods.Single();
        await vm.OpenMethodCommand.ExecuteAsync(main);
        Assert.Same(main, vm.SelectedMethodInList);
        Assert.Null(vm.SelectedConstructorInList);

        vm.CreateConstructorCommand.Execute(null);
        var ctor = vm.Constructors.Single();

        Assert.Same(ctor, vm.SelectedConstructorInList);
        Assert.Null(vm.SelectedMethodInList); // the Methods list's own highlight, not SelectedMethod itself

        await vm.OpenMethodCommand.ExecuteAsync(main);

        Assert.Same(main, vm.SelectedMethodInList);
        Assert.Null(vm.SelectedConstructorInList);
    }

    [Fact]
    public async Task ShowsABusyIndicatorOnlyAfterTheDelay()
    {
        // Holds the open "in flight" deterministically: real background work (WarmOverloadsAsync's
        // Task.Run) is fast enough to finish before the test's own next statement in a Release build,
        // which would otherwise make this a flaky race against the scheduled busy-indicator callback.
        var gate = new TaskCompletionSource();
        vm.OpenGraphDelayForTests = () => gate.Task;
        var main = vm.Methods.Single();

        Task openTask = vm.OpenMethodCommand.ExecuteAsync(main);
        Assert.False(vm.IsOpeningGraph);

        editor.Scheduler.AdvanceBy(ClassEditorVM.BusyIndicatorDelay.Ticks);
        Assert.True(vm.IsOpeningGraph);
        Assert.Equal(main.Name, vm.OpeningGraphName);

        gate.SetResult();
        await openTask;
        Assert.False(vm.IsOpeningGraph);
        Assert.Null(vm.OpeningGraphName);
    }

    [Fact]
    public async Task ClickingASecondMethodWhileTheFirstIsStillOpeningSupersedesIt()
    {
        // R2-02: goes through ICommand (InvokeCommandAction's real path checks CanExecute, then
        // Execute), not ExecuteAsync directly — the latter bypasses CanExecute and would not have
        // caught the regression (AsyncRelayCommand.CanExecute used to return false while running).
        vm.CreateMethodCommand.Execute(null);
        var main = vm.Methods.First();
        var second = vm.Methods.Last();
        ICommand command = vm.OpenMethodCommand;

        var gate = new TaskCompletionSource();
        vm.OpenGraphDelayForTests = () => gate.Task;

        Assert.True(command.CanExecute(main));
        command.Execute(main);
        Task firstOpen = vm.OpenMethodCommand.ExecutionTask ?? Task.CompletedTask;

        Assert.True(command.CanExecute(second)); // must stay true while opening (AllowConcurrentExecutions)
        command.Execute(second);
        Task secondOpen = vm.OpenMethodCommand.ExecutionTask ?? Task.CompletedTask;

        gate.SetResult();
        await Task.WhenAll(firstOpen, secondOpen);

        Assert.Same(second, vm.SelectedMethod);
        Assert.Same(second.Graph, vm.OpenedGraph?.Graph);
        Assert.Equal(InspectorKind.Method, vm.Inspector);
    }

    [Fact]
    public async Task ClickingClassWhileAMethodIsStillOpeningShowsTheClassNotTheMethod()
    {
        // R2-02 failure scenario 2: ShowClass must cancel the pending open, or the method's load
        // finishing later overrides the class navigation the user made in the meantime.
        var main = vm.Methods.Single();
        ICommand command = vm.OpenMethodCommand;

        var gate = new TaskCompletionSource();
        vm.OpenGraphDelayForTests = () => gate.Task;

        command.Execute(main);
        Task openTask = vm.OpenMethodCommand.ExecutionTask ?? Task.CompletedTask;

        vm.ShowClassCommand.Execute(null);
        gate.SetResult();
        await openTask;

        Assert.Equal(InspectorKind.Class, vm.Inspector);
        Assert.Same(cls, vm.OpenedGraph?.Graph);
    }

    [Fact]
    public void NavigateToNodeMessageOpensTheGraphAndSelectsTheNode()
    {
        // FR-034, ED-T03: double-clicking an error row opens the graph it belongs to (even when a
        // different one is on the canvas) and reveals the node.
        var main = vm.Methods.Single();
        string graphKey = GraphKeys.For(main.Graph);
        string nodeId = ((MethodGraph)main.Graph).EntryNode.Id;
        vm.ShowClassCommand.Execute(null);
        Assert.Same(cls, vm.OpenedGraph?.Graph);

        vm.Messenger.Send(new NavigateToNodeMessage(graphKey, nodeId));

        Assert.Same(main.Graph, vm.OpenedGraph?.Graph);
        Assert.True(vm.OpenedGraph!.Nodes.Single(n => n.Node.Id == nodeId).IsSelected);
    }

    [Fact]
    public void NavigateToNodeMessageWithNoNodeIdStillOpensTheGraph()
    {
        // OWN-04: a diagnostic with a known member but no node mapping still opens the graph instead
        // of the message being dropped.
        var main = vm.Methods.Single();
        string graphKey = GraphKeys.For(main.Graph);
        vm.ShowClassCommand.Execute(null);

        vm.Messenger.Send(new NavigateToNodeMessage(graphKey, null));

        Assert.Same(main.Graph, vm.OpenedGraph?.Graph);
    }

    [Fact]
    public void NavigateToNodeMessageWithAnUnknownGraphKeyDoesNothing()
    {
        vm.Messenger.Send(new NavigateToNodeMessage("does-not-resolve", null));

        Assert.Null(vm.OpenedGraph);
    }

    [Fact]
    public async Task RemovingMethodClearsInspectorAndGraph()
    {
        var main = vm.Methods.Single();
        await vm.OpenMethodCommand.ExecuteAsync(main);
        Assert.Equal(InspectorKind.Method, vm.Inspector);
        Assert.NotNull(vm.OpenedGraph);

        vm.RemoveMethodCommand.Execute(main);

        Assert.Empty(vm.Methods);
        Assert.Null(vm.OpenedGraph);
        Assert.Null(vm.SelectedMethod);
        Assert.Equal(InspectorKind.Class, vm.Inspector);
    }

    [Fact]
    public async Task RemovingConstructorClearsInspectorAndGraph()
    {
        vm.CreateConstructorCommand.Execute(null);
        var ctor = vm.Constructors.Single();
        await vm.OpenMethodCommand.ExecuteAsync(ctor);

        vm.RemoveMethodCommand.Execute(ctor);

        Assert.Empty(vm.Constructors);
        Assert.Null(vm.OpenedGraph);
        Assert.Equal(InspectorKind.Class, vm.Inspector);
    }

    [Fact]
    public async Task InspectorSwitches()
    {
        vm.CreateVariableCommand.Execute(null);
        vm.Variables.Single().SelectCommand.Execute(null);
        Assert.Equal(InspectorKind.Variable, vm.Inspector);
        Assert.Same(vm.Variables.Single(), vm.SelectedVariable);

        await vm.OpenMethodCommand.ExecuteAsync(vm.Methods.Single());
        Assert.Equal(InspectorKind.Method, vm.Inspector);

        vm.ShowClassCommand.Execute(null);
        Assert.Equal(InspectorKind.Class, vm.Inspector);
        Assert.Same(cls, vm.OpenedGraph?.Graph);
    }

    [Fact]
    public async Task ClassInspectorEditsModelAndCodeRefreshes()
    {
        vm.Name = "Renamed";
        vm.Namespace = "Other";
        vm.Visibility = MemberVisibility.Internal;
        vm.IsSealed = true;
        vm.IsPartial = true;

        Assert.Equal("Other.Renamed", cls.FullName);
        Assert.Equal(ClassModifiers.Sealed | ClassModifiers.Partial, cls.Modifiers);
        Assert.Equal("Renamed", vm.Title);

        vm.IsSealed = false;
        Assert.Equal(ClassModifiers.Partial, cls.Modifiers);

        editor.Scheduler.AdvanceBy(CodeAnalysisHost.DebounceWindow.Ticks);
        string code = await WaitForCodeAsync(c => c.Contains("partial class Renamed", StringComparison.Ordinal));
        Assert.Contains("namespace Other", code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CodeViewFollowsModelEditsAfterTheDebounceWindow()
    {
        vm.Name = "Looped";

        editor.Scheduler.AdvanceBy(CodeAnalysisHost.DebounceWindow.Ticks);
        await WaitForCodeAsync(c => c.Contains("class Looped", StringComparison.Ordinal));

        vm.Name = "LoopedAgain";
        editor.Scheduler.AdvanceBy(CodeAnalysisHost.DebounceWindow.Ticks);
        await WaitForCodeAsync(c => c.Contains("class LoopedAgain", StringComparison.Ordinal));
    }

    /// <summary>Polls <see cref="ClassEditorVM.CodeView"/> for its debounced analysis to complete
    /// (the debounce itself is virtual-time, but <c>AnalyzeAsync</c> hops through a real <c>Task.Run</c>).</summary>
    private async Task<string> WaitForCodeAsync(Func<string, bool> matches)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            string code = vm.CodeView.Code;
            if (matches(code))
            {
                return code;
            }

            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"Code view did not match in time. Last code:\n{code}");
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public void MethodInspectorEditsModel()
    {
        var main = vm.Methods.Single();
        main.Name = "Main2";
        main.IsVirtual = true;
        main.Visibility = MemberVisibility.Protected;

        var graph = (MethodGraph)main.Graph;
        Assert.Equal("Main2", graph.Name);
        Assert.True(graph.Modifiers.HasFlag(MethodModifiers.Virtual));
        Assert.True(graph.Modifiers.HasFlag(MethodModifiers.Static), "other flags are kept");
        Assert.Equal(MemberVisibility.Protected, graph.Visibility);

        vm.CreateConstructorCommand.Execute(null);
        var ctor = vm.Constructors.Single();
        ctor.Name = "Changed";
        Assert.NotEqual("Changed", ctor.Name);
    }

    [Fact]
    public async Task DeleteKeepsEntryAndMainReturn()
    {
        await vm.OpenMethodCommand.ExecuteAsync(vm.Methods.Single());
        var graph = vm.OpenedGraph!;
        var method = (MethodGraph)graph.Graph;
        var extraReturn = new ReturnNode(method);

        graph.SelectNodes(graph.Nodes, deselectPrevious: true);
        vm.DeleteSelectedNodesCommand.Execute(null);

        Assert.True(method.Nodes.Contains(method.EntryNode));
        Assert.True(method.Nodes.Contains(method.MainReturnNode));
        Assert.False(method.Nodes.Contains(extraReturn));
        Assert.False(method.Nodes.OfType<CallMethodNode>().Any(), "other nodes are deleted");
        Assert.False(graph.SelectedNodes.Any());
    }

    [Fact]
    public void DeleteKeepsClassReturnNode()
    {
        vm.ShowClassCommand.Execute(null);
        var graph = vm.OpenedGraph!;
        graph.SelectNodes(graph.Nodes, deselectPrevious: true);

        vm.DeleteSelectedNodesCommand.Execute(null);

        Assert.True(cls.Nodes.Contains(cls.ReturnNode));
    }

    [Fact]
    public async Task SaveSavesProject()
    {
        cls.MarkDirty();
        string graphPath = project.GetGraphFilePath(cls);
        File.Delete(graphPath);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(File.Exists(graphPath));
    }

    [Fact]
    public async Task RunCompilesAndStartsProgram()
    {
        await vm.RunCommand.ExecuteAsync(null);

        Assert.True(project.LastCompilationSucceeded, string.Join("\n", project.LastDiagnostics.Select(d => d.Message)));
        Assert.Equal(1, editor.Processes.Started.Count());
    }

    [Fact(Timeout = 120000)]
    public async Task RunSwitchesToOutputOnceNotOnEveryLine()
    {
        vm.SelectedBottomTab = 0;
        var run = vm.RunCommand.ExecuteAsync(null);
        Assert.Equal(1, vm.SelectedBottomTab); // switched synchronously, when the run starts

        vm.SelectedBottomTab = 0; // the user goes back to Errors while the program keeps printing
        editor.Processes.Raise("still printing");
        editor.Scheduler.AdvanceBy(TimeSpan.FromMilliseconds(50).Ticks);
        Assert.Equal(0, vm.SelectedBottomTab); // a later line must not snap the tab back

        await run;
    }

    [Fact(Timeout = 60000)]
    public void OutputIsBatchedAndCapsRetainedLines()
    {
        for (int i = 0; i < 100_000; i++)
        {
            editor.Processes.Raise($"line {i:D6} 1234567890");
        }

        // Buffered, not rebuilt per line: nothing is flushed until the scheduler advances.
        Assert.Equal("", vm.Output);

        editor.Scheduler.AdvanceBy(TimeSpan.FromMilliseconds(50).Ticks);

        Assert.DoesNotContain("line 000000 ", vm.Output); // the oldest lines were dropped
        Assert.Contains("line 099999 ", vm.Output); // the newest line is retained
        Assert.StartsWith("… earlier output truncated …", vm.Output);
        Assert.True(vm.Output.Length < 1_100_000, $"Output length {vm.Output.Length} was not capped.");
    }

    [Fact]
    public void UndoRedoCommandsFollowStack()
    {
        Assert.False(vm.UndoCommand.CanExecute(null));
        vm.CreateVariableCommand.Execute(null);
        Assert.True(vm.UndoCommand.CanExecute(null));
        vm.UndoCommand.Execute(null);
        Assert.Empty(vm.Variables);
        Assert.True(vm.RedoCommand.CanExecute(null));
        vm.RedoCommand.Execute(null);
        Assert.Equal(1, vm.Variables.Count());
    }

    // OWN-07: event graphs share the open pipeline with methods (OpenGraphThroughPipelineAsync) —
    // one click opens, last click wins across kinds, and re-clicking an item still "selected" in its
    // own list reopens it when the canvas shows something else.

    [Fact]
    public async Task OpenEventGraphCommandOpensItImmediately()
    {
        vm.CreateEventGraphCommand.Execute(null); // also opens it (US4); switch away first
        var eventGraph = vm.EventGraphs.Single();
        await vm.OpenMethodCommand.ExecuteAsync(vm.Methods.Single());

        await vm.OpenEventGraphCommand.ExecuteAsync(eventGraph);

        Assert.Same(eventGraph.Graph, vm.OpenedGraph?.Graph);
    }

    [Fact]
    public async Task MethodOpensAfterAnEventGraphIsOpen()
    {
        // The owner's exact sequence: add an event graph, open it, then open Main.
        vm.CreateEventGraphCommand.Execute(null);
        var eventGraph = vm.EventGraphs.Single();
        var main = vm.Methods.Single();
        await vm.OpenEventGraphCommand.ExecuteAsync(eventGraph);

        await vm.OpenMethodCommand.ExecuteAsync(main);

        Assert.Same(main.Graph, vm.OpenedGraph?.Graph);
        Assert.Same(main, vm.SelectedMethod);
    }

    [Fact]
    public async Task EventGraphOpensAfterAMethodIsOpen()
    {
        vm.CreateEventGraphCommand.Execute(null);
        var eventGraph = vm.EventGraphs.Single();
        await vm.OpenMethodCommand.ExecuteAsync(vm.Methods.Single());

        await vm.OpenEventGraphCommand.ExecuteAsync(eventGraph);

        Assert.Same(eventGraph.Graph, vm.OpenedGraph?.Graph);
    }

    [Fact]
    public async Task ReclickingTheAlreadySelectedEventGraphReopensItAfterTheCanvasSwitchedAway()
    {
        vm.CreateEventGraphCommand.Execute(null);
        var eventGraph = vm.EventGraphs.Single();

        vm.ShowClassCommand.Execute(null);
        Assert.Same(cls, vm.OpenedGraph?.Graph);

        await vm.OpenEventGraphCommand.ExecuteAsync(eventGraph);

        Assert.Same(eventGraph.Graph, vm.OpenedGraph?.Graph);
    }

    [Fact]
    public async Task OpeningAMethodWhileAnEventGraphIsStillOpeningSupersedesIt()
    {
        // Last click wins across item kinds (R2-02, generalized past methods alone).
        vm.CreateEventGraphCommand.Execute(null);
        var eventGraph = vm.EventGraphs.Single();
        var main = vm.Methods.Single();
        await vm.OpenMethodCommand.ExecuteAsync(main); // switch away so the next open isn't a no-op
        ICommand eventGraphCommand = vm.OpenEventGraphCommand;
        ICommand methodCommand = vm.OpenMethodCommand;

        var gate = new TaskCompletionSource();
        vm.OpenGraphDelayForTests = () => gate.Task;

        eventGraphCommand.Execute(eventGraph);
        Task openingEventGraph = vm.OpenEventGraphCommand.ExecutionTask ?? Task.CompletedTask;

        methodCommand.Execute(main);
        Task openingMethod = vm.OpenMethodCommand.ExecutionTask ?? Task.CompletedTask;

        gate.SetResult();
        await Task.WhenAll(openingEventGraph, openingMethod);

        Assert.Same(main.Graph, vm.OpenedGraph?.Graph);
    }
}
