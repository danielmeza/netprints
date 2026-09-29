using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Search;
using NetPrints.Editor.Tests.Graph;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Search;

/// <summary>
/// Node search offers an extension's <c>NodeSuggestion</c>s in the graph kinds its <c>AllowedIn</c> names, after the
/// built-in categories (extension-points.md §2, EX-T02's editor half).
/// </summary>
public sealed class ExtensionSuggestionTests(IReflectionHost sharedReflection)
    : GraphTestBase(TestEditor.Create(_ => sharedReflection, TestExtensionFolder.CreateHost()))
{
    private List<SuggestionItem> Rows(NodeGraphVM graph, NodePin? pin = null) => graph.Search.BuildItems(pin);

    [Fact]
    public void ASuggestionOfTheTestExtensionFollowsTheBuiltInCategoriesInAMethodGraph()
    {
        List<SuggestionItem> rows = Rows(Graph);

        Assert.Equal(["NetPrints", "This Methods", "Static Methods", "Static Variables", "Test"], rows.Where(r => r.IsHeader).Select(r => r.Category).ToArray());
        Assert.Equal(["Log"], rows.Where(r => r is { IsHeader: false, Category: "Test" }).Select(r => r.Text).ToArray());
    }

    [Fact]
    public void TheBuiltInNodesAreUnchangedByALoadedExtension()
    {
        Assert.Equal(
            ["For Loop", "If Else", "Construct New Object", "Type Of", "Explicit Cast", "Return", "Make Array", "Literal", "Type", "Make Array Type", "Throw", "Await", "Ternary", "Default"],
            Rows(Graph).Where(r => r is { IsHeader: false, Category: "NetPrints" }).Select(r => r.Text).ToArray());
    }

    [Fact]
    public void ASuggestionIsNotOfferedInAGraphKindOutsideItsAllowedIn()
    {
        ClassEditor.ShowClassCommand.Execute(null);

        Assert.DoesNotContain(Rows(ClassEditor.OpenedGraph ?? throw new InvalidOperationException("No graph.")), r => r.Category == "Test");
    }

    [Fact]
    public void ASuggestionIsOfferedForAnExecPinAndNotForADataPin()
    {
        var write = new CallMethodNode(Method, ConsoleWriteLine(StringType));

        Assert.Contains(Rows(Graph, write.InputExecPins[0]), r => r is { IsHeader: false, Category: "Test" });
        Assert.DoesNotContain(Rows(Graph, write.ArgumentPins[0]), r => r.Category == "Test");
    }

    [Fact]
    public async Task ChoosingTheSuggestionCreatesTheExtensionsNodeAtThePosition()
    {
        SuggestionItem log = Rows(Graph).Single(r => r is { IsHeader: false, Category: "Test" });
        Graph.Search.Position = new GraphPoint(40, 60);

        await Graph.Search.SelectCommand.ExecuteAsync(log);

        Node node = Method.Nodes.Single(n => n.GetType().Name == "LogNode");
        Assert.Equal(40, node.PositionX);
        Assert.Equal(60, node.PositionY);
    }

    [Fact]
    public void WithoutTheExtensionTheSuggestionIsNotOffered()
    {
        var plain = new NetPrints.Editor.ClassEditor.ClassEditorVM(new ClassGraph { Name = "P", Namespace = "N" }, TestEditor.Create(_ => sharedReflection).Context);
        plain.CreateMethodCommand.Execute(null);

        Assert.DoesNotContain(Rows(plain.OpenedGraph ?? throw new InvalidOperationException("No graph.")), r => r.Category == "Test");
        plain.Dispose();
    }
}
