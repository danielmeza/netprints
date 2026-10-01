using Avalonia.Headless.XUnit;
using Avalonia.Input;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.UITests.ClassEditor;
using NetPrints.Graph;

namespace NetPrints.Editor.UITests.Variables;

/// <summary>US5, sub-phase H, ED-T06: the Variables panel's "Class" and "Method: &lt;name&gt;" groups.</summary>
public class LocalVariablePanelTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task GroupsCreateRenameRetypeRemoveAndDragUndoRedo()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var method = (ExecutionGraph)session.GraphViewModel.Graph;

        // Two groups (FR-030): "Class" (always shown) and "Method: Main" (the opened method).
        Assert.True(await session.ClassEditor.VariablesClassGroup.IsVisibleAsync(Token));
        Assert.True(await session.ClassEditor.VariablesMethodGroup.IsVisibleAsync(Token));
        Assert.Equal("Method: Main", session.ClassViewModel.VariablesPanel.MethodGroupHeader);

        // Create (US5): a unique default name and type (object), undoable.
        string name = await session.ClassEditor.LocalVariables.CreateAsync(Token);
        Assert.Equal("Local", name);
        Assert.Contains(method.LocalVariables, l => l.Name == name);
        Assert.Equal(TypeSpecifier.FromType<object>(), method.LocalVariables.Single().Type);

        // Rename via the row's name box (undoable, commits when it loses focus).
        await session.ClassEditor.LocalVariables.LocalVariableNameBox(name).ClickAsync(Token);
        await session.Driver.PressAsync("Ctrl+A", Token);
        await session.Driver.TypeAsync("Count", Token);
        await session.Driver.PressAsync("Tab", Token);
        Assert.Equal("Count", method.LocalVariables.Single().Name);

        await session.ClassEditor.PressUndoAsync(Token);
        Assert.Equal("Local", method.LocalVariables.Single().Name);
        await session.ClassEditor.PressRedoAsync(Token);
        Assert.Equal("Count", method.LocalVariables.Single().Name);

        var methodVariables = session.ClassViewModel.VariablesPanel.MethodVariables
            ?? throw new InvalidOperationException("No method or constructor graph is open.");
        var localVM = methodVariables.Single();

        // Retype (undoable): drives the command directly, the same way EventGraphTests drives
        // RemoveEventGraphCommand — the row's retype button (like the method/variable/event graph
        // rows' remove buttons) has no automation id.
        session.App.Dialogs.TypeAnswer = TypeSpecifier.FromType<int>();
        await localVM.RetypeCommand.ExecuteAsync(null);
        Assert.Equal(TypeSpecifier.FromType<int>(), method.LocalVariables.Single().Type);

        await session.ClassEditor.PressUndoAsync(Token);
        Assert.Equal(TypeSpecifier.FromType<object>(), method.LocalVariables.Single().Type);
        await session.ClassEditor.PressRedoAsync(Token);
        Assert.Equal(TypeSpecifier.FromType<int>(), method.LocalVariables.Single().Type);

        // Dragging the row with real pointer input (press, move, release) opens the Get/Set chooser
        // with both enabled (H1's stopgap, PAR-57): a synthetic DataTransfer+Drop skipped the row's
        // own drag source entirely, hiding the name box's TextBox swallowing the press (R2-03).
        var at = await session.Graph.EmptyPointAsync(Token);

        await session.ClassEditor.LocalVariables.LocalVariableNameBox(localVM.Name).DragToAsync(at, Token);

        await session.Graph.GetSet.WaitOpenAsync(Token);
        await session.Graph.GetSet.SetButton.ClickAsync(Token);
        await session.WaitForRenderedAsync(Token);

        var setter = method.Nodes.OfType<VariableSetterNode>().Single();
        Assert.Null(setter.TargetPin); // H1's NewValuePin index fix: no target pin pushes NewValue to 1
        Assert.Single(setter.InputDataPins);

        // Remove (undoable): also drives the command directly; removes the setter node too.
        localVM.RemoveCommand.Execute(null);
        Assert.Empty(method.LocalVariables);
        Assert.Empty(method.Nodes.OfType<VariableSetterNode>());

        await session.ClassEditor.PressUndoAsync(Token);
        Assert.Same(localVM.Local, method.LocalVariables.Single());
        Assert.Same(setter, method.Nodes.OfType<VariableSetterNode>().Single());

        await session.ClassEditor.PressRedoAsync(Token);
        Assert.Empty(method.LocalVariables);
    }

    /// <summary>
    /// R2-04: retyping a local through the panel's type chooser and pressing Ctrl+Z must restore the
    /// exact getter/setter node instances and their data connections, not just the exec pins.
    /// </summary>
    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task RetypeLocalVariableUndoRestoresDataConnections()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var method = (MethodGraph)session.GraphViewModel.Graph;

        await session.ClassEditor.LocalVariables.CreateAsync(Token);
        var localVM = (session.ClassViewModel.VariablesPanel.MethodVariables
            ?? throw new InvalidOperationException("No method or constructor graph is open.")).Single();

        // A getter wired into a call, and a setter wired from a literal on an exec chain.
        var specifier = localVM.Local.ToSpecifier();
        var getter = new VariableGetterNode(method, specifier);
        var sink = new MethodSpecifier("Sink", [new MethodParameter("value", TypeSpecifier.FromType<object>(), MethodParameterPassType.Default, false, null)],
            [], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType<object>(), []);
        var call = new CallMethodNode(method, sink);
        GraphUtil.ConnectDataPins(getter.ValuePin, call.InputDataPins[0]);

        var setter = new VariableSetterNode(method, specifier);
        var literal = new LiteralNode(method, TypeSpecifier.FromType<object>());
        GraphUtil.ConnectDataPins(literal.ValuePin, setter.NewValuePin);
        GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, setter.InputExecPins[0]);
        GraphUtil.ConnectExecPins(setter.OutputExecPins[0], method.MainReturnNode.ReturnPin);
        await session.WaitForRenderedAsync(Token);

        // Retype through the panel's real type chooser (H1/US5), then undo with a real Ctrl+Z.
        session.App.Dialogs.TypeAnswer = TypeSpecifier.FromType<int>();
        await localVM.RetypeCommand.ExecuteAsync(null);
        await session.WaitForRenderedAsync(Token);
        Assert.NotSame(getter, method.Nodes.OfType<VariableGetterNode>().Single());
        Assert.NotSame(setter, method.Nodes.OfType<VariableSetterNode>().Single());

        await session.ClassEditor.PressUndoAsync(Token);
        await session.WaitForRenderedAsync(Token);

        var restoredGetter = method.Nodes.OfType<VariableGetterNode>().Single();
        var restoredSetter = method.Nodes.OfType<VariableSetterNode>().Single();
        Assert.Same(getter, restoredGetter); // R2-04: same instance, not rebuilt
        Assert.Same(setter, restoredSetter);
        Assert.Same(call.InputDataPins[0], restoredGetter.ValuePin.OutgoingPins.Single()); // getter's data wire back
        Assert.Same(literal.ValuePin, restoredSetter.NewValuePin.IncomingPin); // setter's data wire back
        Assert.Same(restoredSetter.InputExecPins[0], method.EntryNode.InitialExecutionPin.OutgoingPin);
        Assert.Same(method.MainReturnNode.ReturnPin, restoredSetter.OutputExecPins[0].OutgoingPin);

        await session.ClassEditor.PressRedoAsync(Token);
        await session.WaitForRenderedAsync(Token);
        Assert.Equal(TypeSpecifier.FromType<int>(), localVM.Local.Type);
    }
}
