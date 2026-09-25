using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Inspectors;
using NetPrints.Editor.Search;

namespace NetPrints.Editor.Tests.Ui;

[TestClass]
public class ClassWindowTests
{
    [TestMethod]
    [Timeout(90000, CooperativeCancellation = true)]
    public Task InspectorsListsAndGeneratedCode() => UiTest.RunAsync(async () =>
    {
        using var ctx = await GraphTestContext.OpenSampleMainAsync();
        var window = ctx.Window;

        Assert.IsTrue(window.Find<ClassInspectorView>("ClassInspector").IsVisible, "class inspector by default (PAR-33)");
        Assert.HasCount(5, window.Descendants<GridSplitter>().ToList(), "resizable splitters (PAR-31)");
        Assert.IsTrue(ToolTip.GetShowOnDisabled(window.Find<Button>("CompileButton")));

        // Single click on a method shows the method inspector (PAR-24).
        var methodName = window.Find<ListBox>("MethodList").Descendants<TextBlock>().First(t => t.Text == "Main");
        window.Click(methodName.CenterIn(window));
        Assert.AreEqual(InspectorKind.Method, ctx.Editor.Inspector);
        await UiTest.WaitUntilAsync(() => window.Find<MethodInspectorView>("MethodInspector").IsVisible);

        // Variables: create, then click the name for the variable inspector (PAR-29, 30).
        ctx.Editor.CreateVariableCommand.Execute(null);
        UiTest.Pump();
        var variableName = window.Find<ListBox>("VariableList").Descendants<TextBlock>().First(t => t.Text == "Variable");
        window.Click(variableName.CenterIn(window));
        await UiTest.WaitUntilAsync(() => window.Find<VariableInspectorView>("VariableInspector").IsVisible);

        // Class button: class inspector + class graph; the code preview updates as typed (PAR-34).
        window.Click(window.Find<Button>("ClassButton").CenterIn(window));
        Assert.AreSame(ctx.Editor.Class, ctx.Editor.OpenedGraph?.Graph);
        var nameBox = window.Find<ClassInspectorView>("ClassInspector").Find<TextBox>("NameBox");
        nameBox.Text = "Renamed";
        UiTest.Pump();
        Assert.AreEqual("Renamed", ctx.Editor.Class.Name, "updates as typed");
        Assert.AreEqual("Renamed", window.Title);
        await UiTest.WaitUntilAsync(() => ctx.Editor.GeneratedCode.Contains("class Renamed"), 5000, "generated code refreshes");
        var code = window.Find<ClassInspectorView>("ClassInspector").Find<TextBox>("GeneratedCodeBox");
        Assert.IsTrue(code.IsReadOnly);
        StringAssert.Contains(code.Text, "class Renamed");
    });

    [TestMethod]
    [Timeout(90000, CooperativeCancellation = true)]
    public Task OverrideChooserCreatesAndResets() => UiTest.RunAsync(async () =>
    {
        using var ctx = await GraphTestContext.OpenSampleMainAsync();
        var box = ctx.Window.Find<ComboBox>("OverrideBox");
        Assert.IsTrue(ctx.Editor.OverridableMethods.Any(m => m.Name == "ToString"), "PAR-26");

        box.SelectedItem = ctx.Editor.OverridableMethods.First(m => m.Name == "ToString");
        await UiTest.WaitUntilAsync(() => box.SelectedItem is null, 5000, "the chooser resets");

        Assert.IsTrue(ctx.Editor.Methods.Any(m => m.Name == "ToString"));
        Assert.AreEqual("ToString", ctx.Editor.OpenedGraph?.Name);
    });

    [TestMethod]
    [Timeout(90000, CooperativeCancellation = true)]
    public Task GetSetPopupCreatesNodes() => UiTest.RunAsync(async () =>
    {
        using var ctx = await GraphTestContext.OpenSampleMainAsync();
        var method = (MethodGraph)ctx.Graph.Graph;
        var variable = new VariableSpecifier("Length", TypeSpecifier.FromType<int>(), MemberVisibility.Public, MemberVisibility.Private,
            TypeSpecifier.FromType<string>(), VariableModifiers.None);

        ctx.Graph.GetSetChooser.Open(variable, new GraphPoint(56, 400));
        await UiTest.WaitUntilAsync(() => ctx.View.Find<Avalonia.Controls.Primitives.Popup>("GetSetPopup").IsOpen);
        var popupRoot = ctx.View.Find<Avalonia.Controls.Primitives.Popup>("GetSetPopup").Child!;
        var get = popupRoot.Descendants<Button>().Single(b => b.Name == "GetButton");
        var set = popupRoot.Descendants<Button>().Single(b => b.Name == "SetButton");
        Assert.IsTrue(get.IsEffectivelyEnabled);
        Assert.IsFalse(set.IsEffectivelyEnabled, "a private setter of another type is disabled (PAR-55)");

        get.Command!.Execute(null);
        await ctx.WaitForGraphAsync();
        Assert.HasCount(1, method.Nodes.OfType<VariableGetterNode>().ToList());
        Assert.IsFalse(ctx.Graph.GetSetChooser.IsOpen);
    });

    [TestMethod]
    [Timeout(90000, CooperativeCancellation = true)]
    public Task NodeChromeOverloadsPureAndPinButtons() => UiTest.RunAsync(async () =>
    {
        using var ctx = await GraphTestContext.OpenSampleMainAsync();
        var method = (MethodGraph)ctx.Graph.Graph;
        var write = ctx.Graph.Nodes.Single(n => n.Node is CallMethodNode);
        var writeView = ctx.ContainerOf(write);

        var overloads = writeView.Descendants<ComboBox>().Single(c => c.Name == "Overloads");
        Assert.IsTrue(overloads.IsVisible, "call nodes show the overload chooser (PAR-40)");
        var intOverload = write.Overloads.OfType<MethodSpecifier>().First(m => m.Parameters.Count == 1 && m.Parameters[0].Value == TypeSpecifier.FromType<int>());
        overloads.SelectedItem = intOverload;
        await ctx.WaitForGraphAsync();
        Assert.AreEqual(intOverload, method.Nodes.OfType<CallMethodNode>().Single().MethodSpecifier);
        Assert.AreSame(method.Nodes.OfType<CallMethodNode>().Single().InputExecPins[0], method.EntryNode.InitialExecutionPin.OutgoingPin,
            "exec connections survive the overload change");

        ctx.Window.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, null);
        await ctx.WaitForGraphAsync();
        Assert.AreEqual(TypeSpecifier.FromType<string>(), method.Nodes.OfType<CallMethodNode>().Single().MethodSpecifier.Parameters[0].Value, "undo restores it");

        // Pure checkbox (PAR-41) and +/- buttons on the entry node (PAR-42).
        var entry = ctx.Graph.Nodes.Single(n => n.Node == method.EntryNode);
        var plus = ctx.ContainerOf(entry).Descendants<Button>().First(b => ToolTip.GetTip(b) as string == "Add method parameter");
        ctx.Window.Click(plus.CenterIn(ctx.Window));
        Assert.HasCount(1, method.ArgumentTypes);

        var pure = ctx.ContainerOf(ctx.Graph.Nodes.Single(n => n.Node is CallMethodNode)).Descendants<CheckBox>().Single(c => c.IsEffectivelyVisible && !c.Classes.Contains("pinValue"));
        pure.IsChecked = true;
        await ctx.WaitForGraphAsync();
        Assert.IsTrue(method.Nodes.OfType<CallMethodNode>().Single().IsPure);
    });

    [TestMethod]
    [Timeout(90000, CooperativeCancellation = true)]
    public Task SearchLiteralAsksForType() => UiTest.RunAsync(async () =>
    {
        using var ctx = await GraphTestContext.OpenSampleMainAsync();
        var method = (MethodGraph)ctx.Graph.Graph;
        ctx.Dialogs.TypeAnswer = TypeSpecifier.FromType<int>();

        ctx.Window.Click(ctx.EmptyCanvasPoint(), MouseButton.Right);
        await UiTest.WaitUntilAsync(() => ctx.Graph.Search.IsOpen && !ctx.Graph.Search.IsLoading && ctx.Graph.Search.Items.Count > 0);
        ctx.Window.KeyTextInput("literal");
        await UiTest.WaitUntilAsync(() => ctx.Graph.Search.Items.Any(i => i.Text == "Literal") && ctx.Graph.Search.Items.Count < 50);

        var list = ctx.View.Find<NetPrints.Editor.Search.NodeSearchView>("SearchView").Find<ListBox>("ResultList");
        var literalRow = list.Descendants<TextBlock>().First(t => t.Text == "Literal");
        var image = ((Avalonia.Visual)literalRow.Parent!).GetVisualChildrenOfType<Image>().FirstOrDefault();
        Assert.IsNotNull(image?.Source, "rows have 16-px icons");
        ctx.Window.Click(literalRow.CenterIn(ctx.Window));

        await UiTest.WaitUntilAsync(() => method.Nodes.OfType<LiteralNode>().Any());
        Assert.HasCount(1, ctx.Dialogs.SelectTypeCalls, "Literal asks for a type (PAR-54, 58)");
        Assert.AreEqual(TypeSpecifier.FromType<int>(), method.Nodes.OfType<LiteralNode>().Single().LiteralType);
    });
}

internal static class VisualExtensions
{
    public static IEnumerable<T> GetVisualChildrenOfType<T>(this Avalonia.Visual visual) where T : Avalonia.Visual =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(visual).OfType<T>();
}
