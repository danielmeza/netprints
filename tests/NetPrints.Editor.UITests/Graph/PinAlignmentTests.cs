using Avalonia.Headless.XUnit;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.UITests.ClassEditor;
using NetPrints.Graph;

namespace NetPrints.Editor.UITests.Graph;

/// <summary>Pin row alignment on nodes (OWN-05, owner-reported): fixed row height, connector and
/// label sharing one vertical center, and output connectors flush to a common edge.</summary>
public class PinAlignmentTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static void AssertSharedVerticalCenter(AutomationElement connector, AutomationElement label)
    {
        double connectorCenter = connector.Bounds.Y + (connector.Bounds.Height / 2);
        double labelCenter = label.Bounds.Y + (label.Bounds.Height / 2);
        Assert.Equal(connectorCenter, labelCenter, 1.0);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task EntryNodeOutputsShareAVerticalCenterAndACommonConnectorX()
    {
        // Reproduces the owner's report: adding an input parameter added a second output row (the new
        // parameter) whose connector, name and type did not line up with the Exec row above it.
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        ((MethodGraph)session.GraphVM.Graph).MethodEntryNode.AddArgument();
        await session.WaitForRenderedAsync(Token);

        var entry = session.Graph.Node("MethodEntryNode");
        var exec = entry.Output("Exec");
        var newParameter = entry.Output("Input0");

        var execConnector = await exec.Connector.GetAsync(Token);
        var parameterConnector = await newParameter.Connector.GetAsync(Token);
        AssertSharedVerticalCenter(execConnector, await exec.Label.GetAsync(Token));
        AssertSharedVerticalCenter(parameterConnector, await newParameter.Label.GetAsync(Token));
        Assert.Equal(execConnector.Bounds.X, parameterConnector.Bounds.X, 1.0);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task CallNodeInputsShareAVerticalCenterWithTheirLabels()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var callNode = session.Graph.Node("CallMethodNode");
        string valuePinName = session.GraphVM.Nodes.Single(n => n.Node is CallMethodNode).InputDataPins.Single().Pin.Name;

        var execInput = callNode.Input("Exec");
        var valueInput = callNode.Input(valuePinName);

        AssertSharedVerticalCenter(await execInput.Connector.GetAsync(Token), await execInput.Label.GetAsync(Token));
        AssertSharedVerticalCenter(await valueInput.Connector.GetAsync(Token), await valueInput.Label.GetAsync(Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task CallNodeOutputConnectorsShareACommonX()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var callNode = session.Graph.Node("CallMethodNode");

        var execConnector = await callNode.Output("Exec").Connector.GetAsync(Token);
        var catchConnector = await callNode.Output("Catch").Connector.GetAsync(Token);

        Assert.Equal(execConnector.Bounds.X, catchConnector.Bounds.X, 1.0);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task OutputConnectorsShareACommonXEvenWithVeryDifferentLabelLengths()
    {
        // ForLoopNode's own outputs range from "Loop" to "Index: Int32": the case a fixed-width,
        // right-aligned header (not one that hugs each row's own content) is needed for.
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var forLoop = session.GraphVM.AddNode<ForLoopNode>(new GraphPoint(28, 400));
        await session.WaitForRenderedAsync(Token);

        var node = session.Graph.Node(forLoop.Name);
        double loopX = (await node.Output("Loop").Connector.GetAsync(Token)).Bounds.X;
        double completedX = (await node.Output("Completed").Connector.GetAsync(Token)).Bounds.X;
        double indexX = (await node.Output("Index").Connector.GetAsync(Token)).Bounds.X;

        Assert.Equal(loopX, completedX, 1.0);
        Assert.Equal(loopX, indexX, 1.0);
    }
}
