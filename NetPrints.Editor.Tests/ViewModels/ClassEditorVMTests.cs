using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.Tests.Fakes;
using NetPrints.Editor.ViewModels;

namespace NetPrints.Editor.Tests.ViewModels;

[TestClass]
public class ClassEditorVMTests
{
    private Project project = null!;
    private ClassGraph cls = null!;
    private TestEditor editor = null!;
    private ClassEditorVM vm = null!;

    [TestInitialize]
    public void Setup()
    {
        editor = new TestEditor();
        project = TestPaths.LoadHelloWorldCopy();
        cls = project.Classes.Single();
        vm = new ClassEditorVM(cls, editor.Context);
    }

    [TestCleanup]
    public void Cleanup()
    {
        vm.Dispose();
        TestPaths.TryDelete(project.Path);
    }

    [TestMethod]
    public void ListsMethodsAndTitle()
    {
        Assert.AreEqual("Program", vm.Title);
        Assert.AreEqual("Main", vm.Methods.Single().Name);
        Assert.IsEmpty(vm.Constructors);
        Assert.IsEmpty(vm.Variables);
    }

    [TestMethod]
    public void CreateMethodConnectsEntryAndReturnAndOpensGraph()
    {
        vm.CreateMethodCommand.Execute(null);
        vm.CreateMethodCommand.Execute(null);

        CollectionAssert.AreEqual(new[] { "Main", "Method", "Method2" }, vm.Methods.Select(m => m.Name).ToArray());
        var method = (MethodGraph)vm.Methods[2].Graph;
        Assert.AreSame(method.MainReturnNode.ReturnPin, method.EntryNode.InitialExecutionPin.OutgoingPin);
        Assert.AreEqual(0, method.EntryNode.PositionX % 28);
        Assert.AreEqual(0, method.MainReturnNode.PositionX % 28);
        Assert.AreSame(method, vm.OpenedGraph?.Graph);
    }

    [TestMethod]
    public void CreateConstructorIsPublicAndOpens()
    {
        vm.CreateConstructorCommand.Execute(null);

        var ctor = vm.Constructors.Single();
        Assert.IsTrue(ctor.IsConstructor);
        Assert.AreEqual(MemberVisibility.Public, ctor.Visibility);
        Assert.AreEqual(112, ctor.Graph.EntryNode.PositionX);
        Assert.AreSame(ctor.Graph, vm.OpenedGraph?.Graph);
    }

    [TestMethod]
    public void OverrideChooserCreatesOpensAndResets()
    {
        var toString = vm.OverridableMethods.First(m => m.Name == "ToString");

        vm.SelectedOverride = toString;

        var created = vm.Methods.Single(m => m.Name == "ToString");
        Assert.IsTrue(((MethodGraph)created.Graph).Modifiers.HasFlag(MethodModifiers.Override));
        Assert.AreSame(created.Graph, vm.OpenedGraph?.Graph);
        Assert.IsNull(vm.SelectedOverride, "the chooser resets");
    }

    [TestMethod]
    public void RemovingMethodClearsInspectorAndGraph()
    {
        var main = vm.Methods.Single();
        vm.SelectMethodCommand.Execute(main);
        vm.OpenMethodCommand.Execute(main);
        Assert.AreEqual(InspectorKind.Method, vm.Inspector);
        Assert.IsNotNull(vm.OpenedGraph);

        vm.RemoveMethodCommand.Execute(main);

        Assert.IsEmpty(vm.Methods);
        Assert.IsNull(vm.OpenedGraph);
        Assert.IsNull(vm.SelectedMethod);
        Assert.AreEqual(InspectorKind.Class, vm.Inspector);
    }

    [TestMethod]
    public void RemovingConstructorClearsInspectorAndGraph()
    {
        vm.CreateConstructorCommand.Execute(null);
        var ctor = vm.Constructors.Single();
        vm.SelectMethodCommand.Execute(ctor);

        vm.RemoveMethodCommand.Execute(ctor);

        Assert.IsEmpty(vm.Constructors);
        Assert.IsNull(vm.OpenedGraph);
        Assert.AreEqual(InspectorKind.Class, vm.Inspector);
    }

    [TestMethod]
    public void InspectorSwitches()
    {
        vm.CreateVariableCommand.Execute(null);
        vm.Variables.Single().SelectCommand.Execute(null);
        Assert.AreEqual(InspectorKind.Variable, vm.Inspector);
        Assert.AreSame(vm.Variables.Single(), vm.SelectedVariable);

        vm.SelectMethodCommand.Execute(vm.Methods.Single());
        Assert.AreEqual(InspectorKind.Method, vm.Inspector);

        vm.ShowClassCommand.Execute(null);
        Assert.AreEqual(InspectorKind.Class, vm.Inspector);
        Assert.AreSame(cls, vm.OpenedGraph?.Graph, "the Class button opens the class graph");
    }

    [TestMethod]
    public void ClassInspectorEditsModelAndCodeRefreshes()
    {
        vm.Name = "Renamed";
        vm.Namespace = "Other";
        vm.Visibility = MemberVisibility.Internal;
        vm.IsSealed = true;
        vm.IsPartial = true;

        Assert.AreEqual("Other.Renamed", cls.FullName);
        Assert.AreEqual(ClassModifiers.Sealed | ClassModifiers.Partial, cls.Modifiers);
        Assert.AreEqual("Renamed", vm.Title);

        vm.IsSealed = false;
        Assert.AreEqual(ClassModifiers.Partial, cls.Modifiers, "modifier flags toggle independently");

        vm.RefreshGeneratedCode();
        StringAssert.Contains(vm.GeneratedCode, "partial class Renamed");
        StringAssert.Contains(vm.GeneratedCode, "namespace Other");
    }

    [TestMethod]
    [Timeout(30000, CooperativeCancellation = true)]
    public async Task GeneratedCodeLoopRefreshesWithinTwoSeconds()
    {
        vm.StartGeneratedCodeLoop();
        vm.Name = "Looped";

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!vm.GeneratedCode.Contains("class Looped") && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        StringAssert.Contains(vm.GeneratedCode, "class Looped");
    }

    [TestMethod]
    public void MethodInspectorEditsModel()
    {
        var main = vm.Methods.Single();
        main.Name = "Main2";
        main.IsVirtual = true;
        main.Visibility = MemberVisibility.Protected;

        var graph = (MethodGraph)main.Graph;
        Assert.AreEqual("Main2", graph.Name);
        Assert.IsTrue(graph.Modifiers.HasFlag(MethodModifiers.Virtual));
        Assert.IsTrue(graph.Modifiers.HasFlag(MethodModifiers.Static), "other flags are kept");
        Assert.AreEqual(MemberVisibility.Protected, graph.Visibility);

        vm.CreateConstructorCommand.Execute(null);
        var ctor = vm.Constructors.Single();
        ctor.Name = "Changed";
        Assert.AreNotEqual("Changed", ctor.Name, "constructor names are read-only");
    }

    [TestMethod]
    public void DeleteKeepsEntryAndMainReturn()
    {
        vm.OpenMethodCommand.Execute(vm.Methods.Single());
        var graph = vm.OpenedGraph!;
        var method = (MethodGraph)graph.Graph;
        var extraReturn = new ReturnNode(method);

        graph.SelectNodes(graph.Nodes, deselectPrevious: true);
        vm.DeleteSelectedNodesCommand.Execute(null);

        Assert.IsTrue(method.Nodes.Contains(method.EntryNode));
        Assert.IsTrue(method.Nodes.Contains(method.MainReturnNode));
        Assert.IsFalse(method.Nodes.Contains(extraReturn));
        Assert.IsFalse(method.Nodes.OfType<CallMethodNode>().Any(), "other nodes are deleted");
        Assert.IsFalse(graph.SelectedNodes.Any());
    }

    [TestMethod]
    public void DeleteKeepsClassReturnNode()
    {
        vm.ShowClassCommand.Execute(null);
        var graph = vm.OpenedGraph!;
        graph.SelectNodes(graph.Nodes, deselectPrevious: true);

        vm.DeleteSelectedNodesCommand.Execute(null);

        Assert.IsTrue(cls.Nodes.Contains(cls.ReturnNode));
    }

    [TestMethod]
    public async Task SaveSavesProject()
    {
        File.Delete(project.Path);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.IsTrue(File.Exists(project.Path));
    }

    [TestMethod]
    [Timeout(120000, CooperativeCancellation = true)]
    public async Task RunCompilesAndStartsProgram()
    {
        await vm.RunCommand.ExecuteAsync(null);

        Assert.IsTrue(project.LastCompilationSucceeded, string.Join("\n", project.LastCompileErrors));
        Assert.HasCount(1, editor.Processes.Started);
    }

    [TestMethod]
    public void UndoRedoCommandsFollowStack()
    {
        Assert.IsFalse(vm.UndoCommand.CanExecute(null));
        vm.CreateVariableCommand.Execute(null);
        Assert.IsTrue(vm.UndoCommand.CanExecute(null));
        vm.UndoCommand.Execute(null);
        Assert.IsEmpty(vm.Variables);
        Assert.IsTrue(vm.RedoCommand.CanExecute(null));
        vm.RedoCommand.Execute(null);
        Assert.HasCount(1, vm.Variables);
    }
}
