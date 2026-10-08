using NetPrints.Core;
using NetPrints.Editor.Events;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Events;

/// <summary>US8 (FR-071 to FR-074, contracts/shell.md §7): the event entry inspector.</summary>
public sealed class EventEntryInspectorViewModelTests : IDisposable
{
    private static readonly TypeSpecifier Int = TypeSpecifier.FromType<int>();
    private static readonly TypeSpecifier Text = TypeSpecifier.FromType<string>();

    private readonly TestEditor editor;
    private readonly ClassContext context;
    private readonly ClassGraph cls;
    private readonly EventGraph events;
    private readonly EventEntryNode entry;

    public EventEntryInspectorViewModelTests(TestEditor editor)
    {
        this.editor = editor;
        cls = new ClassGraph { Name = "C", Namespace = "N" };
        events = new EventGraph("Events") { Class = cls };
        cls.EventGraphs.Add(events);
        entry = new EventEntryNode(events, "OnHit");
        context = new ClassContext(cls, editor.Context, new UndoRedoStack(), () => [cls]);
    }

    public void Dispose() => context.Dispose();

    private EventEntryInspectorViewModel Inspector(EventEntryNode? of = null) => context.EventEntryInspectorOf(of ?? entry);

    [Fact]
    public void ACustomEntryShowsItsNameKindAndArguments()
    {
        entry.SetArguments([new EventArgument("amount", Int), new EventArgument("source", Text)]);

        EventEntryInspectorViewModel inspector = Inspector();

        Assert.Equal("OnHit", inspector.Name);
        Assert.Equal("Custom event", inspector.Kind);
        Assert.False(inspector.IsOverride);
        Assert.Equal(["amount", "source"], inspector.Arguments.Select(argument => argument.Name));
        Assert.Equal([Int, Text], inspector.Arguments.Select(argument => argument.Type));
        Assert.All(inspector.Arguments, argument => Assert.True(argument.IsEditable));
    }

    [Fact]
    public void RenamingIsOneUndoStepLabelledRenameEventAndRetargetsCalls()
    {
        EventEntryInspectorViewModel inspector = Inspector();

        inspector.Name = "OnDamage";

        Assert.Equal("OnDamage", entry.EventName);
        Assert.Equal("Rename event", context.UndoRedo.UndoName);
        context.UndoRedo.Undo();
        Assert.Equal("OnHit", entry.EventName);
        Assert.Equal("OnHit", inspector.Name);
    }

    [Fact]
    public void ANameUsedByAMethodOrAnotherEntryIsRefusedAndShown()
    {
        cls.Methods.Add(new MethodGraph("Run") { Class = cls });
        _ = new EventEntryNode(events, "OnDeath");
        EventEntryInspectorViewModel inspector = Inspector();

        inspector.Name = "Run";

        Assert.Equal("'Run' is already used by method 'Run'", inspector.Error);
        Assert.Equal("OnHit", entry.EventName);
        Assert.Equal("OnHit", inspector.Name);
        Assert.False(context.UndoRedo.CanUndo);

        inspector.Name = "OnDeath";

        Assert.Equal("'OnDeath' is already used by event 'OnDeath'", inspector.Error);

        inspector.Name = "OnMiss";

        Assert.Null(inspector.Error);
        Assert.Equal("OnMiss", entry.EventName);
    }

    [Fact]
    public void AnInvalidNameIsRefused()
    {
        EventEntryInspectorViewModel inspector = Inspector();

        inspector.Name = "1bad";

        Assert.Contains("'1bad'", inspector.Error, StringComparison.Ordinal);
        Assert.Equal("OnHit", entry.EventName);
    }

    [Fact]
    public void AddingAnArgumentIsOneUndoStepLabelledAddArgument()
    {
        EventEntryInspectorViewModel inspector = Inspector();

        inspector.AddArgumentCommand.Execute(null);

        Assert.Equal([new EventArgument("arg", TypeSpecifier.FromType<object>())], entry.Arguments);
        Assert.Equal(["arg"], inspector.Arguments.Select(argument => argument.Name));
        Assert.Equal("Add argument", context.UndoRedo.UndoName);
        context.UndoRedo.Undo();
        Assert.Empty(entry.Arguments);
        Assert.Empty(inspector.Arguments);
        context.UndoRedo.Redo();
        Assert.Single(entry.Arguments);
    }

    [Fact]
    public void RemovingAnArgumentKeepsTheConnectionsOfTheOthersAndUndoRestoresThem()
    {
        entry.SetArguments([new EventArgument("a", Int), new EventArgument("b", Text)]);
        var caller = new MethodGraph("Caller") { Class = cls };
        var sink = new CallMethodNode(caller, new MethodSpecifier("M", [new MethodParameter("p", Text, MethodParameterPassType.Default, false, null), new MethodParameter("q", Int, MethodParameterPassType.Default, false, null)],
            Array.Empty<BaseType>(), MethodModifiers.Static, MemberVisibility.Public, cls.Type, Array.Empty<BaseType>()));
        NodeInputDataPin onB = sink.InputDataPins[0];
        NodeInputDataPin onA = sink.InputDataPins[1];
        GraphUtil.ConnectDataPins(entry.OutputDataPins[0], onA);
        GraphUtil.ConnectDataPins(entry.OutputDataPins[1], onB);
        EventEntryInspectorViewModel inspector = Inspector();

        inspector.Arguments[0].RemoveCommand.Execute(null);

        Assert.Equal(["b"], inspector.Arguments.Select(argument => argument.Name));
        Assert.Same(entry.OutputDataPins[0], onB.IncomingPin);
        Assert.Null(onA.IncomingPin);
        Assert.Equal("Remove argument", context.UndoRedo.UndoName);

        context.UndoRedo.Undo();

        Assert.Equal(["a", "b"], entry.Arguments.Select(argument => argument.Name));
        Assert.Same(entry.OutputDataPins[0], onA.IncomingPin);
        Assert.Same(entry.OutputDataPins[1], onB.IncomingPin);
    }

    [Fact]
    public void MovingAnArgumentUpOrDownIsOneUndoStepAndTheConnectionFollowsIt()
    {
        entry.SetArguments([new EventArgument("a", Int), new EventArgument("b", Text), new EventArgument("c", Int)]);
        var caller = new MethodGraph("Caller") { Class = cls };
        var sink = new CallMethodNode(caller, new MethodSpecifier("M", [new MethodParameter("p", Int, MethodParameterPassType.Default, false, null)],
            Array.Empty<BaseType>(), MethodModifiers.Static, MemberVisibility.Public, cls.Type, Array.Empty<BaseType>()));
        GraphUtil.ConnectDataPins(entry.OutputDataPins[0], sink.InputDataPins[0]);
        EventEntryInspectorViewModel inspector = Inspector();
        Assert.False(inspector.Arguments[0].CanMoveUp);
        Assert.False(inspector.Arguments[2].CanMoveDown);

        inspector.Arguments[0].MoveDownCommand.Execute(null);

        Assert.Equal(["b", "a", "c"], entry.Arguments.Select(argument => argument.Name));
        Assert.Same(entry.OutputDataPins[1], sink.InputDataPins[0].IncomingPin);
        Assert.Equal("Move argument down", context.UndoRedo.UndoName);

        inspector.Arguments[1].MoveUpCommand.Execute(null);

        Assert.Equal(["a", "b", "c"], entry.Arguments.Select(argument => argument.Name));
        Assert.Equal("Move argument up", context.UndoRedo.UndoName);

        context.UndoRedo.Undo();
        context.UndoRedo.Undo();
        Assert.Equal(["a", "b", "c"], entry.Arguments.Select(argument => argument.Name));
        Assert.Same(entry.OutputDataPins[0], sink.InputDataPins[0].IncomingPin);
    }

    [Fact]
    public void RenamingAnArgumentIsOneUndoStepAndADuplicateIsRefused()
    {
        entry.SetArguments([new EventArgument("a", Int), new EventArgument("b", Text)]);
        EventEntryInspectorViewModel inspector = Inspector();

        inspector.Arguments[0].Name = "amount";

        Assert.Equal(["amount", "b"], entry.Arguments.Select(argument => argument.Name));
        Assert.Equal("Rename argument", context.UndoRedo.UndoName);

        inspector.Arguments[1].Name = "amount";

        Assert.Contains("'amount'", inspector.Error, StringComparison.Ordinal);
        Assert.Equal(["amount", "b"], entry.Arguments.Select(argument => argument.Name));
        Assert.Equal("b", inspector.Arguments[1].Name);

        context.UndoRedo.Undo();
        Assert.Equal(["a", "b"], entry.Arguments.Select(argument => argument.Name));
    }

    [Fact]
    public async Task RetypingAnArgumentAsksTheTypePickerAndIsOneUndoStep()
    {
        entry.SetArguments([new EventArgument("a", Int)]);
        editor.Dialogs.TypeAnswer = Text;
        EventEntryInspectorViewModel inspector = Inspector();

        await inspector.Arguments[0].ChangeTypeCommand.ExecuteAsync(null);

        Assert.Equal([Int], editor.Dialogs.SelectTypeCalls);
        Assert.Equal([new EventArgument("a", Text)], entry.Arguments);
        Assert.Equal("Change argument type", context.UndoRedo.UndoName);
        context.UndoRedo.Undo();
        Assert.Equal([new EventArgument("a", Int)], entry.Arguments);
        Assert.Equal(Int, inspector.Arguments[0].Type);
    }

    [Fact]
    public async Task ACancelledTypePickerChangesNothing()
    {
        entry.SetArguments([new EventArgument("a", Int)]);
        editor.Dialogs.TypeAnswer = null;
        EventEntryInspectorViewModel inspector = Inspector();

        await inspector.Arguments[0].ChangeTypeCommand.ExecuteAsync(null);

        Assert.False(context.UndoRedo.CanUndo);
    }

    [Fact]
    public void AnOverrideShowsTheNameAndBaseSignatureReadOnlyWithTheNote()
    {
        var overridden = new MethodSpecifier("Hit", [new MethodParameter("amount", Int, MethodParameterPassType.Default, false, null)],
            Array.Empty<BaseType>(), MethodModifiers.Virtual, MemberVisibility.Public, TypeSpecifier.FromType<Exception>(), Array.Empty<BaseType>());
        var overrideEntry = new EventEntryNode(events, overridden);

        EventEntryInspectorViewModel inspector = Inspector(overrideEntry);

        Assert.Equal("Override", inspector.Kind);
        Assert.True(inspector.IsOverride);
        Assert.True(inspector.IsNameReadOnly);
        Assert.Equal("Hit", inspector.Name);
        Assert.Equal("Signature comes from Exception.Hit", inspector.BaseSignatureNote);
        Assert.Equal(["amount"], inspector.Arguments.Select(argument => argument.Name));
        Assert.All(inspector.Arguments, argument => Assert.False(argument.IsEditable));
        inspector.Name = "Other";
        Assert.Equal("Hit", overrideEntry.EventName);
        Assert.False(inspector.AddArgumentCommand.CanExecute(null));
        Assert.False(context.UndoRedo.CanUndo);
    }
}
