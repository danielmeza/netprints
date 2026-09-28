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
        var method = (ExecutionGraph)session.GraphVM.Graph;

        // Two groups (FR-030): "Class" (always shown) and "Method: Main" (the opened method).
        Assert.True(await session.ClassEditor.VariablesClassGroup.IsVisibleAsync(Token));
        Assert.True(await session.ClassEditor.VariablesMethodGroup.IsVisibleAsync(Token));
        Assert.Equal("Method: Main", session.ClassVM.VariablesPanel.MethodGroupHeader);

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

        var methodVariables = session.ClassVM.VariablesPanel.MethodVariables
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
}
