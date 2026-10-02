using NetPrints.Core;
using NetPrints.Editor.Diagnostics;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Inspectors;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Shell;

/// <summary>What a class's context creates, edits and exposes: members, class inspector, code view, deletion and history.</summary>
public class ClassContextMembersTests(TestEditor editor) : IAsyncLifetime
{
    private Project? projectField;
    private ClassGraph? clsField;
    private ClassContext? contextField;

    private Project project => projectField ?? throw new InvalidOperationException($"{nameof(InitializeAsync)} has not run yet.");
    private ClassGraph cls => clsField ?? throw new InvalidOperationException($"{nameof(InitializeAsync)} has not run yet.");
    private ClassContext context => contextField ?? throw new InvalidOperationException($"{nameof(InitializeAsync)} has not run yet.");

    public async ValueTask InitializeAsync()
    {
        projectField = await TestPaths.LoadHelloWorldCopyAsync(TestContext.Current.CancellationToken);
        clsField = project.Classes.Single();
        contextField = new ClassContext(cls, editor.Context, new UndoRedoStack());
    }

    public ValueTask DisposeAsync()
    {
        contextField?.Dispose();
        TestPaths.TryDelete(projectField?.Path);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public void ListsTheClassMembers()
    {
        Assert.Equal("Main", context.Methods.Single().Name);
        Assert.Empty(context.Constructors);
        Assert.Empty(context.Variables);
    }

    [Fact]
    public void CreateMethodConnectsEntryAndReturn()
    {
        context.CreateMethod();
        MethodGraph method = context.CreateMethod();

        Assert.Equal(new[] { "Main", "Method", "Method2" }, context.Methods.Select(m => m.Name).ToArray());
        Assert.Same(method, context.Methods[2].Graph);
        Assert.Same(method.MainReturnNode.ReturnPin, method.EntryNode.InitialExecutionPin.OutgoingPin);
        Assert.Equal(0, method.EntryNode.PositionX % 28);
        Assert.Equal(0, method.MainReturnNode.PositionX % 28);
    }

    [Fact]
    public void CreateConstructorIsPublic()
    {
        ConstructorGraph created = context.CreateConstructor();

        var ctor = context.Constructors.Single();
        Assert.True(ctor.IsConstructor);
        Assert.Equal(MemberVisibility.Public, ctor.Visibility);
        Assert.Equal(112, ctor.Graph.EntryNode.PositionX);
        Assert.Same(created, ctor.Graph);
    }

    [Fact]
    public void CreateOverrideAddsTheOverridingMethod()
    {
        var toString = cls.AllBaseTypes.SelectMany(editor.Reflection.Provider.GetOverridableMethodsForType).First(m => m.Name == "ToString");

        MethodGraph? created = context.CreateOverride(toString);

        Assert.NotNull(created);
        var method = context.Methods.Single(m => m.Name == "ToString");
        Assert.True(((MethodGraph)method.Graph).Modifiers.HasFlag(MethodModifiers.Override));
        Assert.Same(created, method.Graph);
    }

    [Fact]
    public void RemovingAMethodOrConstructorTakesItOutOfTheClass()
    {
        var main = context.Methods.Single();
        ConstructorGraph created = context.CreateConstructor();

        context.RemoveMethod(main.Graph);
        context.RemoveMethod(created);

        Assert.Empty(context.Methods);
        Assert.Empty(context.Constructors);
    }

    [Fact]
    public async Task ClassInspectorEditsModelAndCodeRefreshes()
    {
        ClassInspectorViewModel inspector = context.ClassInspector;
        inspector.Name = "Renamed";
        inspector.Namespace = "Other";
        inspector.Visibility = MemberVisibility.Internal;
        inspector.IsSealed = true;
        inspector.IsPartial = true;

        Assert.Equal("Other.Renamed", cls.FullName);
        Assert.Equal(ClassModifiers.Sealed | ClassModifiers.Partial, cls.Modifiers);

        inspector.IsSealed = false;
        Assert.Equal(ClassModifiers.Partial, cls.Modifiers);

        editor.Scheduler.AdvanceBy(CodeAnalysisHost.DebounceWindow.Ticks);
        string code = await WaitForCodeAsync(c => c.Contains("partial class Renamed", StringComparison.Ordinal));
        Assert.Contains("namespace Other", code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CodeViewFollowsModelEditsAfterTheDebounceWindow()
    {
        context.ClassInspector.Name = "Looped";

        editor.Scheduler.AdvanceBy(CodeAnalysisHost.DebounceWindow.Ticks);
        await WaitForCodeAsync(c => c.Contains("class Looped", StringComparison.Ordinal));

        context.ClassInspector.Name = "LoopedAgain";
        editor.Scheduler.AdvanceBy(CodeAnalysisHost.DebounceWindow.Ticks);
        await WaitForCodeAsync(c => c.Contains("class LoopedAgain", StringComparison.Ordinal));
    }

    /// <summary>Polls the code view for its debounced analysis to complete
    /// (the debounce itself is virtual-time, but <c>AnalyzeAsync</c> hops through a real <c>Task.Run</c>).</summary>
    private async Task<string> WaitForCodeAsync(Func<string, bool> matches)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            string code = context.CodeView.Code;
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
        var main = context.Methods.Single();
        main.Name = "Main2";
        main.IsVirtual = true;
        main.Visibility = MemberVisibility.Protected;

        var graph = (MethodGraph)main.Graph;
        Assert.Equal("Main2", graph.Name);
        Assert.True(graph.Modifiers.HasFlag(MethodModifiers.Virtual));
        Assert.True(graph.Modifiers.HasFlag(MethodModifiers.Static), "other flags are kept");
        Assert.Equal(MemberVisibility.Protected, graph.Visibility);

        context.CreateConstructor();
        var ctor = context.Constructors.Single();
        ctor.Name = "Changed";
        Assert.NotEqual("Changed", ctor.Name);
    }

    [Fact]
    public void DeleteKeepsEntryAndMainReturn()
    {
        using var graph = new NodeGraphViewModel(context.Methods.Single().Graph, context.Services);
        var method = (MethodGraph)graph.Graph;
        var extraReturn = new ReturnNode(method);

        graph.SelectNodes(graph.Nodes, deselectPrevious: true);
        graph.DeleteSelectedNodes();

        Assert.True(method.Nodes.Contains(method.EntryNode));
        Assert.True(method.Nodes.Contains(method.MainReturnNode));
        Assert.False(method.Nodes.Contains(extraReturn));
        Assert.False(method.Nodes.OfType<CallMethodNode>().Any(), "other nodes are deleted");
        Assert.False(graph.SelectedNodes.Any());
    }

    [Fact]
    public void DeletingNodesInvalidatesTheSavedMarkerEvenAfterAnUndoReturnsToIt()
    {
        using var graph = new NodeGraphViewModel(context.Methods.Single().Graph, context.Services);
        context.UndoRedo.MarkSaved();
        graph.SelectNodes(graph.Nodes, deselectPrevious: true);
        graph.DeleteSelectedNodes();

        graph.AddNode<IfElseNode>(new GraphPoint(10, 10));
        context.UndoRedo.Undo();

        Assert.False(context.UndoRedo.IsAtSavedState);
    }

    [Fact]
    public void DeleteKeepsClassReturnNode()
    {
        using var graph = new NodeGraphViewModel(cls, context.Services);
        graph.SelectNodes(graph.Nodes, deselectPrevious: true);

        graph.DeleteSelectedNodes();

        Assert.True(cls.Nodes.Contains(cls.ReturnNode));
    }

    [Fact]
    public void UndoRedoFollowTheContextsStack()
    {
        Assert.False(context.UndoRedo.CanUndo);
        context.CreateVariable();
        Assert.True(context.UndoRedo.CanUndo);
        context.UndoRedo.Undo();
        Assert.Empty(context.Variables);
        Assert.True(context.UndoRedo.CanRedo);
        context.UndoRedo.Redo();
        Assert.Equal(1, context.Variables.Count());
    }
}
