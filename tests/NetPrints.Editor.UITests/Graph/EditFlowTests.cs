using Avalonia.Headless.XUnit;
using NetPrints.Core;
using NetPrints.Editor.UITests.ClassEditor;
using NetPrints.Graph;

namespace NetPrints.Editor.UITests.Graph;

/// <summary>FR-017 end-to-end flow: open, edit through search, connect, save and reload.</summary>
public class EditFlowTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task CreateIfElseConnectSaveAndReload()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        var method = (MethodGraph)session.GraphVM.Graph;
        session.GraphVM.Nodes.Single(n => n.Node == method.EntryNode).OutputExecPins.Single().DisconnectAll();
        await session.WaitForRenderedAsync(Token);

        var search = await (await session.Graph.RightClickEmptyAsync(Token)).WaitOpenAsync(Token);
        await search.FilterAsync("if else", "If Else", Token);
        await search.ChooseAsync("If Else", Token);
        await session.WaitForRenderedAsync(Token);
        Assert.Contains("IfElseNode", await session.Graph.NodeNamesAsync(Token));

        await session.Graph.Node("MethodEntryNode").Output("Exec").ConnectToAsync(session.Graph.Node("IfElseNode").Input("Exec"), Token);
        await session.Graph.Connection("MethodEntryNode.Exec->IfElseNode.Exec").GetAsync(Token);
        double x = method.Nodes.OfType<IfElseNode>().Single().PositionX;

        var before = LastWrite(session.Sample.Directory);
        await session.ClassEditor.SaveButton.ClickAsync(Token);
        await Testing.Ui.Driving.UiWait.UntilAsync(session.Driver, () => Task.FromResult(LastWrite(session.Sample.Directory) > before), "saved", Token);

        var reloaded = Project.LoadFromPath(session.Sample.ProjectPath);
        var reloadedMain = reloaded.Classes.Single().Methods.Single();
        var reloadedIf = reloadedMain.Nodes.OfType<IfElseNode>().Single();
        Assert.Same(reloadedIf.InputExecPins[0], reloadedMain.EntryNode.InitialExecutionPin.OutgoingPin);
        Assert.Equal(x, reloadedIf.PositionX);
    }

    private static DateTime LastWrite(string directory) => Directory.GetFiles(directory).Max(File.GetLastWriteTimeUtc);
}
