using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.ClassEditor;

public class ClassEditorVMTests : IDisposable
{
    private readonly Project project;
    private readonly ClassGraph cls;
    private readonly TestEditor editor;
    private readonly ClassEditorVM vm;

    public ClassEditorVMTests(TestEditor editor)
    {
        this.editor = editor;
        project = TestPaths.LoadHelloWorldCopy();
        cls = project.Classes.Single();
        vm = new ClassEditorVM(cls, editor.Context);
    }

    public void Dispose()
    {
        vm.Dispose();
        TestPaths.TryDelete(project.Path);
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
    public void RemovingMethodClearsInspectorAndGraph()
    {
        var main = vm.Methods.Single();
        vm.SelectMethodCommand.Execute(main);
        vm.OpenMethodCommand.Execute(main);
        Assert.Equal(InspectorKind.Method, vm.Inspector);
        Assert.NotNull(vm.OpenedGraph);

        vm.RemoveMethodCommand.Execute(main);

        Assert.Empty(vm.Methods);
        Assert.Null(vm.OpenedGraph);
        Assert.Null(vm.SelectedMethod);
        Assert.Equal(InspectorKind.Class, vm.Inspector);
    }

    [Fact]
    public void RemovingConstructorClearsInspectorAndGraph()
    {
        vm.CreateConstructorCommand.Execute(null);
        var ctor = vm.Constructors.Single();
        vm.SelectMethodCommand.Execute(ctor);

        vm.RemoveMethodCommand.Execute(ctor);

        Assert.Empty(vm.Constructors);
        Assert.Null(vm.OpenedGraph);
        Assert.Equal(InspectorKind.Class, vm.Inspector);
    }

    [Fact]
    public void InspectorSwitches()
    {
        vm.CreateVariableCommand.Execute(null);
        vm.Variables.Single().SelectCommand.Execute(null);
        Assert.Equal(InspectorKind.Variable, vm.Inspector);
        Assert.Same(vm.Variables.Single(), vm.SelectedVariable);

        vm.SelectMethodCommand.Execute(vm.Methods.Single());
        Assert.Equal(InspectorKind.Method, vm.Inspector);

        vm.ShowClassCommand.Execute(null);
        Assert.Equal(InspectorKind.Class, vm.Inspector);
        Assert.Same(cls, vm.OpenedGraph?.Graph);
    }

    [Fact]
    public void ClassInspectorEditsModelAndCodeRefreshes()
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

        vm.RefreshGeneratedCode();
        Assert.Contains("partial class Renamed", vm.GeneratedCode);
        Assert.Contains("namespace Other", vm.GeneratedCode);
    }

    [Fact]
    public void GeneratedCodeRefreshesEverySecondInVirtualTime()
    {
        vm.StartGeneratedCodeLoop();
        vm.Name = "Looped";

        editor.Scheduler.AdvanceBy(TimeSpan.FromMilliseconds(999).Ticks);
        Assert.DoesNotContain("class Looped", vm.GeneratedCode);

        editor.Scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1).Ticks);
        Assert.Contains("class Looped", vm.GeneratedCode);

        vm.Name = "LoopedAgain";
        editor.Scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        Assert.Contains("class LoopedAgain", vm.GeneratedCode);
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
    public void DeleteKeepsEntryAndMainReturn()
    {
        vm.OpenMethodCommand.Execute(vm.Methods.Single());
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
        File.Delete(project.Path);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.True(File.Exists(project.Path));
    }

    [Fact(Timeout = 120000)]
    public async Task RunCompilesAndStartsProgram()
    {
        await vm.RunCommand.ExecuteAsync(null);

        Assert.True(project.LastCompilationSucceeded, string.Join("\n", project.LastCompileErrors));
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
}
