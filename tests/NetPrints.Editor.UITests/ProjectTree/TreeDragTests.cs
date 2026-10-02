using Avalonia.Headless.XUnit;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.ProjectTree;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Graph;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Screenplay;
using NetPrints.Testing.Ui.Shell;

namespace NetPrints.Editor.UITests.ProjectTree;

/// <summary>Project tree rows are drag sources: a method, constructor or variable dragged onto the open graph's canvas creates its node (FR-017, PAR-56, PAR-57).</summary>
public class TreeDragTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(ShellApp App, ShellPage Shell, ClassGraph Class, MethodGraph Main)> OpenMainAsync()
    {
        ShellApp app = ShellApp.Start();
        await app.OpenSampleAsync(Token);
        ShellPage shell = app.Actor.Using<UseNetPrints>().Shell;
        ClassGraph cls = app.Session.Project.Classes.Single();
        MethodGraph main = cls.Methods.Single(method => method.Name == "Main");
        await shell.Tree.OpenMethodAsync("Main", Token);
        await shell.Graph.WaitRenderedAsync(Token);
        await shell.Graph.ClickEmptyAsync(Token); // ends the double click on the row, so the next press is a first click
        return (app, shell, cls, main);
    }

    private static async Task AddMemberAsync(ShellApp app, ShellPage shell, string command)
    {
        await shell.Tree.SelectAsync(shell.Tree.Class("Program"), Token);
        Assert.True(app.Commands.TryRun(app.Command(command)));
        await shell.Tree.OpenMethodAsync("Main", Token);
        await shell.Graph.WaitRenderedAsync(Token);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DraggingAMethodRowOntoTheCanvasAddsACallNodeAtTheDropPoint()
    {
        var (app, shell, _, main) = await OpenMainAsync();
        await using ShellApp owned = app;
        int nodes = await shell.Graph.NodeCountAsync(Token);
        UiTarget at = await shell.Graph.EmptyPointAsync(Token);
        (double X, double Y) expected = await shell.Graph.ToGraphAsync(at, Token);

        await shell.Tree.Method("Main").DragToAsync(at, Token);

        await UiWait.UntilAsync(app.Driver, async () => await shell.Graph.NodeCountAsync(Token) == nodes + 1, "call node dropped", Token);
        CallMethodNode call = main.Nodes.OfType<CallMethodNode>().Single(node => node.MethodSpecifier.Name == "Main");
        Assert.Equal(Math.Max(0, expected.X), call.PositionX, 0);
        Assert.Equal(Math.Max(0, expected.Y), call.PositionY, 0);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DraggingAConstructorRowOntoTheCanvasAddsAConstructorNode()
    {
        var (app, shell, cls, _) = await OpenMainAsync();
        await using ShellApp owned = app;
        await AddMemberAsync(app, shell, "addConstructor");
        int nodes = await shell.Graph.NodeCountAsync(Token);
        string name = cls.Constructors.Single().ToString() ?? string.Empty;
        UiElement row = await shell.Tree.RevealAsync(shell.Tree.Item(AutomationIds.TreeKindConstructor, name), ProjectTreePage.ConstructorsGroup, Token);

        await row.DragToAsync(await shell.Graph.EmptyPointAsync(Token), Token);

        await UiWait.UntilAsync(app.Driver, async () => await shell.Graph.NodeCountAsync(Token) == nodes + 1, "constructor node dropped", Token);
        Assert.Contains("ConstructorNode", await shell.Graph.NodeNamesAsync(Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DraggingAVariableRowOntoTheCanvasOffersGetAndSet()
    {
        var (app, shell, cls, _) = await OpenMainAsync();
        await using ShellApp owned = app;
        await AddMemberAsync(app, shell, "addVariable");
        int nodes = await shell.Graph.NodeCountAsync(Token);
        UiElement row = await shell.Tree.RevealAsync(shell.Tree.Variable(cls.Variables.Single().Name), ProjectTreePage.VariablesGroup, Token);

        await row.DragToAsync(await shell.Graph.EmptyPointAsync(Token), Token);

        await shell.Graph.GetSet.WaitOpenAsync(Token);
        await shell.Graph.GetSet.GetButton.ClickAsync(Token);
        await UiWait.UntilAsync(app.Driver, async () => await shell.Graph.NodeCountAsync(Token) == nodes + 1, "getter dropped", Token);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DraggingAMethodOfAnotherClassCallsThatClassNotTheOpenOne()
    {
        var (app, shell, cls, _) = await OpenMainAsync();
        await using ShellApp owned = app;
        ClassGraph other = app.Session.Project.CreateNewClass(DefaultProjectProfile.Instance);
        await shell.Tree.SelectAsync(shell.Tree.Class(other.Name), Token);
        Assert.True(app.Commands.TryRun(app.Command("addMethod")));
        MethodGraph helper = other.Methods.Single();
        await shell.Graph.WaitRenderedAsync(Token);
        int nodes = await shell.Graph.NodeCountAsync(Token);
        UiElement row = await shell.Tree.RevealAsync(shell.Tree.Method("Main"), ProjectTreePage.MethodsGroup, Token);

        await row.DragToAsync(await shell.Graph.EmptyPointAsync(Token), Token);

        await UiWait.UntilAsync(app.Driver, async () => await shell.Graph.NodeCountAsync(Token) == nodes + 1, "call node dropped", Token);
        CallMethodNode call = helper.Nodes.OfType<CallMethodNode>().Single();
        Assert.Equal(cls.Type, call.MethodSpecifier.DeclaringType);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DraggingAProjectClassOrGroupRowStartsNoDrag()
    {
        var (app, shell, _, _) = await OpenMainAsync();
        await using ShellApp owned = app;
        int nodes = await shell.Graph.NodeCountAsync(Token);
        UiTarget at = await shell.Graph.EmptyPointAsync(Token);

        foreach (UiElement row in new[] { shell.Tree.Project(app.Session.Project.Name), shell.Tree.Class("Program"), shell.Tree.Group(ProjectTreePage.MethodsGroup) })
        {
            await app.Driver.DragAsync(await row.OffsetAsync(60, 16, Token), at, UiButton.Left, Token);
        }

        Assert.Equal(nodes, await shell.Graph.NodeCountAsync(Token));
        Assert.False(await shell.Graph.GetSet.IsOpenAsync(Token));
    }

    [Theory]
    [InlineData(TreeItemKind.Method, true)]
    [InlineData(TreeItemKind.Constructor, true)]
    [InlineData(TreeItemKind.Variable, true)]
    [InlineData(TreeItemKind.Project, false)]
    [InlineData(TreeItemKind.Class, false)]
    [InlineData(TreeItemKind.Group, false)]
    [InlineData(TreeItemKind.EventGraph, false)]
    public void OnlyMemberRowsCanBeDragged(TreeItemKind kind, bool canDrag)
    {
        var row = new ProjectTreeItemViewModel(kind, null, () => "x", null, null, null);

        Assert.Equal(canDrag, row.CanDrag);
    }
}
