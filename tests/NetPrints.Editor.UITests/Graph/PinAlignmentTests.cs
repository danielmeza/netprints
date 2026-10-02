using Avalonia.Headless.XUnit;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Graph;

namespace NetPrints.Editor.UITests.Graph;

/// <summary>Pin row alignment on nodes (OWN-05, OWN-05b, owner-reported): fixed row height, connector
/// and label sharing one vertical center, output connectors flush to a common edge, and (OWN-05b) a
/// parameter's type pin and data pin landing on the same row as each other.</summary>
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
        ((MethodGraph)session.GraphViewModel.Graph).MethodEntryNode.AddArgument();
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
    public async Task EntryNodeParameterTypeAndDataPinsShareARowForEveryParameter()
    {
        // OWN-05b: the previous fix (e9abd81) only checked a pin against its own label; it never
        // checked that a parameter's type pin (left column) and its data pin (right column) land on
        // the same row as each other. Before this fix, each added parameter drifted the two columns
        // one row further apart.
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var methodGraph = (MethodGraph)session.GraphViewModel.Graph;
        var entryNode = methodGraph.MethodEntryNode;
        entryNode.AddArgument();
        entryNode.AddArgument();
        entryNode.AddArgument();
        methodGraph.MainReturnNode.AddReturnType();
        await session.WaitForRenderedAsync(Token);

        var entry = session.Graph.Node("MethodEntryNode");
        var execConnector = await entry.Output("Exec").Connector.GetAsync(Token);

        for (int i = 0; i < 3; i++)
        {
            var typeConnector = await entry.Input($"Input{i}Type").Connector.GetAsync(Token);
            var dataConnector = await entry.Output($"Input{i}").Connector.GetAsync(Token);

            double typeCenter = typeConnector.Bounds.Y + (typeConnector.Bounds.Height / 2);
            double dataCenter = dataConnector.Bounds.Y + (dataConnector.Bounds.Height / 2);
            Assert.Equal(typeCenter, dataCenter, 1.0);
            Assert.Equal(execConnector.Bounds.X, dataConnector.Bounds.X, 1.0);
        }

        // A cable dragged onto parameter 2's connector (three rows down, the worst-misaligned one
        // before the fix) must land on parameter 2's own anchor, not a neighbor's.
        var returnValue = session.Graph.Node("ReturnNode").Input("Output0");
        await entry.Output("Input2").ConnectToAsync(returnValue, Token);
        await session.WaitForRenderedAsync(Token);

        Assert.Same(entryNode.OutputDataPins[2], methodGraph.MainReturnNode.InputDataPins[0].IncomingPin);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task CallNodeInputsShareAVerticalCenterWithTheirLabels()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var callNode = session.Graph.Node("CallMethodNode");
        string valuePinName = session.GraphViewModel.Nodes.Single(n => n.Node is CallMethodNode).InputDataPins.Single().Pin.Name;

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
        var forLoop = session.GraphViewModel.AddNode<ForLoopNode>(new GraphPoint(28, 400));
        await session.WaitForRenderedAsync(Token);

        var node = session.Graph.Node(forLoop.Name);
        double loopX = (await node.Output("Loop").Connector.GetAsync(Token)).Bounds.X;
        double completedX = (await node.Output("Completed").Connector.GetAsync(Token)).Bounds.X;
        double indexX = (await node.Output("Index").Connector.GetAsync(Token)).Bounds.X;

        Assert.Equal(loopX, completedX, 1.0);
        Assert.Equal(loopX, indexX, 1.0);
    }
}
