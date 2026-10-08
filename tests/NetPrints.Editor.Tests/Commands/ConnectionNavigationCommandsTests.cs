using NetPrints.Editor.Commands;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.Navigation;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Graph;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.Shell;

namespace NetPrints.Editor.Tests.Commands;

public sealed class ConnectionNavigationCommandsTests(TestEditor editor) : GraphTestBase(editor)
{
    private readonly FakeShell shell = new();

    private ConnectionViewModel Cable => Graph.Connections.First(c => c.Kind == PinKind.Exec);

    private CommandContext Context(object? parameter)
    {
        shell.OpenDocument(DocumentId.Graph("C.netpc.json", DocumentId.MethodKeyPrefix + "m"));
        return shell.Context(parameter, graph: Graph);
    }

    [Fact]
    public void BothCommandsNeedAConnection()
    {
        Assert.False(new GoToConnectionEndCommandHandler(ConnectionEnd.Source).CanExecute(Context(null)));
        Assert.False(new GoToConnectionEndCommandHandler(ConnectionEnd.Target).CanExecute(Context("not a connection")));
        Assert.True(new GoToConnectionEndCommandHandler(ConnectionEnd.Source).CanExecute(Context(Cable)));
        Assert.True(new GoToConnectionEndCommandHandler(ConnectionEnd.Target).CanExecute(Context(Cable)));
    }

    [Fact]
    public async Task GoToSourceNavigatesToTheNodeOfTheOutputEnd()
    {
        CommandContext context = Context(Cable);

        await new GoToConnectionEndCommandHandler(ConnectionEnd.Source).ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal([$"NavigateTo:{context.ActiveDocument}:{Cable.Source.Node.Node.Id}"], shell.Navigation.Calls);
    }

    [Fact]
    public async Task GoToTargetNavigatesToTheNodeOfTheInputEnd()
    {
        CommandContext context = Context(Cable);

        await new GoToConnectionEndCommandHandler(ConnectionEnd.Target).ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal([$"NavigateTo:{context.ActiveDocument}:{Cable.Target.Node.Node.Id}"], shell.Navigation.Calls);
    }

    [Fact]
    public void TheLabelsNameTheNodeAndThePin()
    {
        CommandContext context = Context(Cable);

        Assert.Equal($"Go to source ({Cable.Source.Node.Name}.{Cable.Source.Pin.Name})", new GoToConnectionEndCommandHandler(ConnectionEnd.Source).DynamicLabel(context));
        Assert.Equal($"Go to target ({Cable.Target.Node.Name}.{Cable.Target.Pin.Name})", new GoToConnectionEndCommandHandler(ConnectionEnd.Target).DynamicLabel(context));
        Assert.Null(new GoToConnectionEndCommandHandler(ConnectionEnd.Target).DynamicLabel(Context(null)));
    }

    private CommandContext SelectionContext(params NodeViewModel[] nodes)
    {
        shell.OpenDocument(DocumentId.Graph("C.netpc.json", DocumentId.MethodKeyPrefix + "m"));
        return shell.Context(null, graph: Graph, selection: new CommandSelection([.. nodes]));
    }

    [Fact]
    public void WithoutAParameterTheCommandsNeedOneSelectedNodeWithExactlyOneConnectionOnTheirSide()
    {
        var source = new GoToConnectionEndCommandHandler(ConnectionEnd.Source);
        var target = new GoToConnectionEndCommandHandler(ConnectionEnd.Target);
        NodeViewModel from = Cable.Source.Node;
        NodeViewModel to = Cable.Target.Node;

        Assert.True(target.CanExecute(SelectionContext(from)));
        Assert.False(source.CanExecute(SelectionContext(from)));
        Assert.True(source.CanExecute(SelectionContext(to)));
        Assert.False(target.CanExecute(SelectionContext(to)));
        Assert.False(source.CanExecute(SelectionContext()));
        Assert.False(target.CanExecute(SelectionContext(from, to)));
    }

    [Fact]
    public async Task WithoutAParameterTheCommandsJumpAlongTheSelectedNodesOnlyConnection()
    {
        CommandContext fromContext = SelectionContext(Cable.Source.Node);
        CommandContext toContext = SelectionContext(Cable.Target.Node);

        await new GoToConnectionEndCommandHandler(ConnectionEnd.Target).ExecuteAsync(fromContext, TestContext.Current.CancellationToken);
        await new GoToConnectionEndCommandHandler(ConnectionEnd.Source).ExecuteAsync(toContext, TestContext.Current.CancellationToken);

        Assert.Equal(
            [$"NavigateTo:{fromContext.ActiveDocument}:{Cable.Target.Node.Node.Id}", $"NavigateTo:{toContext.ActiveDocument}:{Cable.Source.Node.Node.Id}"],
            shell.Navigation.Calls);
    }

    [Fact]
    public void TheFartherEndOfACableIsTheOneAwayFromTheClick()
    {
        ConnectionViewModel cable = Cable;
        cable.Source.Anchor = new GraphPoint(0, 0);
        cable.Target.Anchor = new GraphPoint(200, 0);

        Assert.Equal(ConnectionEnd.Target, cable.FartherEnd(new GraphPoint(20, 0)));
        Assert.Equal(ConnectionEnd.Source, cable.FartherEnd(new GraphPoint(180, 0)));
    }
}
