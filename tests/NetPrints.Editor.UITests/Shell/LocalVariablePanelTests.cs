using Avalonia.Headless.XUnit;
using NetPrints.Core;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Variables;
using NetPrints.Graph;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>The Variables panel's local variables of the active graph: create, rename with Ctrl+Z, retype, drag onto the canvas, remove.</summary>
public class LocalVariablePanelTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static LocalVariableViewModel OnlyLocal(EditorSession session)
    {
        var panel = Assert.IsType<ShellVariablesPanelViewModel>(session.App.Shell.FindPanel(PanelContributions.VariablesId)?.Content);
        return (panel.Current?.MethodVariables ?? throw new InvalidOperationException("No method or constructor graph is open.")).Single();
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task CreateRenameRetypeDragAndRemoveUndoRedo()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var method = (ExecutionGraph)session.GraphViewModel.Graph;
        session.App.Api.ShowPanel(PanelContributions.VariablesId);

        string name = await session.Page.Variables.AddLocalAsync(Token);
        Assert.Equal("Local", name);
        Assert.Equal(TypeSpecifier.FromType<object>(), method.LocalVariables.Single().Type);

        await session.Page.Variables.LocalVariableNameBox(name).ClickAsync(Token);
        await session.Driver.PressAsync("Ctrl+A", Token);
        await session.Driver.TypeAsync("Count", Token);
        await session.Driver.PressAsync("Tab", Token);
        Assert.Equal("Count", method.LocalVariables.Single().Name);

        await session.PressUndoAsync(Token);
        Assert.Equal("Local", method.LocalVariables.Single().Name);
        await session.PressRedoAsync(Token);
        Assert.Equal("Count", method.LocalVariables.Single().Name);

        LocalVariableViewModel local = OnlyLocal(session);
        session.App.Dialogs.TypeAnswer = TypeSpecifier.FromType<int>();
        await local.RetypeCommand.ExecuteAsync(null);
        Assert.Equal(TypeSpecifier.FromType<int>(), method.LocalVariables.Single().Type);
        await session.PressUndoAsync(Token);
        Assert.Equal(TypeSpecifier.FromType<object>(), method.LocalVariables.Single().Type);
        await session.PressRedoAsync(Token);
        Assert.Equal(TypeSpecifier.FromType<int>(), method.LocalVariables.Single().Type);

        var at = await session.Graph.EmptyPointAsync(Token);
        await session.Page.Variables.LocalVariableNameBox(local.Name).DragToAsync(at, Token);

        await session.Graph.GetSet.WaitOpenAsync(Token);
        await session.Graph.GetSet.SetButton.ClickAsync(Token);
        await session.WaitForRenderedAsync(Token);

        var setter = method.Nodes.OfType<VariableSetterNode>().Single();
        Assert.Null(setter.TargetPin);
        Assert.Single(setter.InputDataPins);

        local.RemoveCommand.Execute(null);
        Assert.Empty(method.LocalVariables);
        Assert.Empty(method.Nodes.OfType<VariableSetterNode>());

        await session.PressUndoAsync(Token);
        Assert.Same(local.Local, method.LocalVariables.Single());
        Assert.Same(setter, method.Nodes.OfType<VariableSetterNode>().Single());

        await session.PressRedoAsync(Token);
        Assert.Empty(method.LocalVariables);
    }
}
