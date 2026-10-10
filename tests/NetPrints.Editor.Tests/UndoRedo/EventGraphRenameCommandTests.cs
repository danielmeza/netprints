using NetPrints.Core;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.UndoRedo;

/// <summary>US8: <see cref="EditorCommands.RenameEventGraph"/> renames an event graph as one undo step.</summary>
public sealed class EventGraphRenameCommandTests
{
    [Fact]
    public void RenamingIsOneUndoStepAndRedoes()
    {
        var cls = new ClassGraph { Name = "C", Namespace = "N" };
        var graph = new EventGraph("First") { Class = cls };
        cls.EventGraphs.Add(graph);
        var stack = new UndoRedoStack();

        stack.Do(EditorCommands.RenameEventGraph(graph, "Gameplay"));

        Assert.Equal("Gameplay", graph.Name);

        stack.Undo();

        Assert.Equal("First", graph.Name);

        stack.Redo();

        Assert.Equal("Gameplay", graph.Name);
    }

    [Fact]
    public void ADuplicateNameIsRefusedAndLeavesNothingToUndo()
    {
        var cls = new ClassGraph { Name = "C", Namespace = "N" };
        var graph = new EventGraph("First") { Class = cls };
        var other = new EventGraph("Second") { Class = cls };
        cls.EventGraphs.Add(graph);
        cls.EventGraphs.Add(other);
        var stack = new UndoRedoStack();

        ArgumentException refused = Assert.Throws<ArgumentException>(() => stack.Do(EditorCommands.RenameEventGraph(graph, "Second")));

        Assert.Contains("An event graph named 'Second' already exists", refused.Message, StringComparison.Ordinal);
        Assert.Equal("First", graph.Name);
        Assert.False(stack.CanUndo);
    }
}

/// <summary>US8 (FR-072): <see cref="EditorCommands.RenameEvent"/> renames an entry and the calls to it as one undo step.</summary>
public sealed class EventRenameCommandTests
{
    [Fact]
    public void RenamingAnEntryIsOneUndoStepThatRetargetsItsCalls()
    {
        var cls = new ClassGraph { Name = "C", Namespace = "N" };
        var events = new EventGraph("Events") { Class = cls };
        cls.EventGraphs.Add(events);
        var entry = new EventEntryNode(events, "OnHit");
        var caller = new MethodGraph("Caller") { Class = cls };
        cls.Methods.Add(caller);
        var call = new CallMethodNode(caller, new MethodSpecifier("OnHit", [], Array.Empty<BaseType>(), MethodModifiers.None, MemberVisibility.Public, cls.Type, Array.Empty<BaseType>()));
        var stack = new UndoRedoStack();

        stack.Do(EditorCommands.RenameEvent([cls], entry, "OnDamage"));

        Assert.Equal("OnDamage", entry.EventName);
        Assert.Equal("OnDamage", call.MethodName);

        stack.Undo();

        Assert.Equal("OnHit", entry.EventName);
        Assert.Equal("OnHit", call.MethodName);

        stack.Redo();

        Assert.Equal("OnDamage", call.MethodName);
    }
}

/// <summary>US8 (FR-073): <see cref="EditorCommands.SetEventArguments"/> changes an entry's arguments as one undo step that keeps connections.</summary>
public sealed class SetEventArgumentsCommandTests
{
    private static readonly TypeSpecifier Int = TypeSpecifier.FromType<int>();
    private static readonly TypeSpecifier Text = TypeSpecifier.FromType<string>();

    [Fact]
    public void ChangingTheArgumentsIsOneNamedStepAndUndoRestoresThePinsAndConnections()
    {
        var cls = new ClassGraph { Name = "C", Namespace = "N" };
        var events = new EventGraph("Events") { Class = cls };
        cls.EventGraphs.Add(events);
        var entry = new EventEntryNode(events, "OnHit");
        entry.SetArguments([new EventArgument("a", Int), new EventArgument("b", Text)]);
        var caller = new MethodGraph("Caller") { Class = cls };
        var sink = new CallMethodNode(caller, new MethodSpecifier("M", [new MethodParameter("p", Text, MethodParameterPassType.Default, false, null)],
            Array.Empty<BaseType>(), MethodModifiers.Static, MemberVisibility.Public, cls.Type, Array.Empty<BaseType>()));
        GraphUtil.ConnectDataPins(entry.OutputDataPins[1], sink.InputDataPins[0]);
        var stack = new UndoRedoStack();

        stack.Do(EditorCommands.SetEventArguments([cls], entry, "Remove argument", [new EventArgument("b", Text)], [1]));

        Assert.Equal([new EventArgument("b", Text)], entry.Arguments);
        Assert.Same(entry.OutputDataPins[0], sink.InputDataPins[0].IncomingPin);
        Assert.Equal("Remove argument", stack.UndoName);

        stack.Undo();

        Assert.Equal([new EventArgument("a", Int), new EventArgument("b", Text)], entry.Arguments);
        Assert.Same(entry.OutputDataPins[1], sink.InputDataPins[0].IncomingPin);

        stack.Redo();

        Assert.Equal([new EventArgument("b", Text)], entry.Arguments);
        Assert.Same(entry.OutputDataPins[0], sink.InputDataPins[0].IncomingPin);
    }

    [Fact]
    public void ARefusedChangeLeavesNothingToUndo()
    {
        var events = new EventGraph("Events");
        var entry = new EventEntryNode(events, "OnHit");
        entry.SetArguments([new EventArgument("a", Int)]);
        var stack = new UndoRedoStack();

        Assert.Throws<ArgumentException>(() => stack.Do(EditorCommands.SetEventArguments([], entry, "Rename argument", [new EventArgument("1x", Int)], [0])));

        Assert.Equal([new EventArgument("a", Int)], entry.Arguments);
        Assert.False(stack.CanUndo);
    }
}
