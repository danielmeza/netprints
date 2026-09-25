using Nodify.Avalonia;
using Nodify.Avalonia.Connections;
using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Ui;

/// <summary>FR-017 end-to-end flow: open, edit through search, connect, save and reload.</summary>
[TestClass]
public class EditFlowTests
{
    [TestMethod]
    [Timeout(90000, CooperativeCancellation = true)]
    public Task CreateIfElseConnectSaveAndReload() => UiTest.RunAsync(async () =>
    {
        using var ctx = await GraphTestContext.OpenSampleMainAsync();
        var method = (MethodGraph)ctx.Graph.Graph;
        int containers = ctx.Window.Descendants<ItemContainer>().Count();

        // Create an If Else node through the node search.
        await ctx.Graph.OpenSearchAsync(new NetPrints.Editor.ViewModels.GraphPoint(280, 420));
        await UiTest.WaitUntilAsync(() => ctx.Graph.Search.Items.Any(i => i.Text == "If Else"));
        var ifElseItem = ctx.Graph.Search.Items.First(i => i.Text == "If Else");
        await ctx.Graph.Search.SelectCommand.ExecuteAsync(ifElseItem);
        await ctx.WaitForGraphAsync();
        Assert.AreEqual(containers + 1, ctx.Window.Descendants<ItemContainer>().Count(), "a new container is realized");

        // Connect the entry exec output to the If input.
        var entryExec = ctx.Graph.Nodes.Single(n => n.Node == method.EntryNode).OutputExecPins.Single();
        var ifNode = ctx.Graph.Nodes.Single(n => n.Node is IfElseNode);
        Assert.IsTrue(entryExec.ConnectTo(ifNode.InputExecPins.Single()));
        await ctx.WaitForGraphAsync();
        Assert.IsTrue(ctx.Window.Descendants<Connection>().Any(c => c.DataContext is NetPrints.Editor.ViewModels.ConnectionVM vm && vm.Target == ifNode.InputExecPins.Single()));

        // Save the project and reload it from disk.
        await ctx.Editor.SaveCommand.ExecuteAsync(null);
        var reloaded = Project.LoadFromPath(ctx.ProjectPath);
        var reloadedMain = reloaded.Classes.Single().Methods.Single();
        var reloadedIf = reloadedMain.Nodes.OfType<IfElseNode>().Single();
        Assert.AreSame(reloadedIf.InputExecPins[0], reloadedMain.EntryNode.InitialExecutionPin.OutgoingPin, "node and connection persisted");
        Assert.AreEqual(280, reloadedIf.PositionX);
    });

    [TestMethod]
    [Timeout(120000, CooperativeCancellation = true)]
    public Task RunFromClassWindowCompilesAndStarts() => UiTest.RunAsync(async () =>
    {
        using var ctx = await GraphTestContext.OpenSampleMainAsync();
        var processes = (Fakes.FakeProcessLauncher)ctx.Composition.Context.Processes;

        await ctx.Editor.RunCommand.ExecuteAsync(null);
        await UiTest.WaitUntilAsync(() => processes.Started.Count == 1, 90000);

        var status = ctx.Window.Find<Avalonia.Controls.TextBlock>("StatusText");
        Assert.AreEqual("Build succeeded", status.Text, "PAR-32 status line");
    });
}
