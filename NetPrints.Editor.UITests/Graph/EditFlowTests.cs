using Avalonia.Headless.XUnit;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.Graph;
using NetPrints.Editor.UITests.ClassEditor;

namespace NetPrints.Editor.UITests.Graph;

/// <summary>FR-017 end-to-end flow: open, edit through search, connect, save and reload.</summary>
public class EditFlowTests
{
    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task CreateIfElseConnectSaveAndReload()
    {
        using var session = await EditorSession.OpenSampleMainAsync();
        var graph = session.Graph;
        var method = (MethodGraph)graph.ViewModel.Graph;
        int containers = graph.RealizedNodeCount;

        await graph.ViewModel.OpenSearchAsync(new GraphPoint(280, 420));
        await graph.Search.WaitReadyAsync();
        await graph.ViewModel.Search.SelectCommand.ExecuteAsync(graph.ViewModel.Search.Items.First(i => i.Text == "If Else"));
        await graph.WaitForRenderedAsync();
        Assert.Equal(containers + 1, graph.RealizedNodeCount);

        var entryExec = graph.ViewModel.Nodes.Single(n => n.Node == method.EntryNode).OutputExecPins.Single();
        var ifNode = graph.ViewModel.Nodes.Single(n => n.Node is IfElseNode);
        Assert.True(entryExec.ConnectTo(ifNode.InputExecPins.Single()));
        await graph.WaitForRenderedAsync();

        await session.ClassEditor.ViewModel.SaveCommand.ExecuteAsync(null);
        var reloaded = Project.LoadFromPath(session.Sample.ProjectPath);
        var reloadedMain = reloaded.Classes.Single().Methods.Single();
        var reloadedIf = reloadedMain.Nodes.OfType<IfElseNode>().Single();
        Assert.Same(reloadedIf.InputExecPins[0], reloadedMain.EntryNode.InitialExecutionPin.OutgoingPin);
        Assert.Equal(280, reloadedIf.PositionX);
    }
}
