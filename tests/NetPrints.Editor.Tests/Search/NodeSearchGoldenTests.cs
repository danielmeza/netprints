using System.Text;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Search;
using NetPrints.Editor.Tests.Graph;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Search;

/// <summary>
/// Characterization of the node search rows (FR-097 safety net, T094a): the built rows of every graph kind for every pin kind, and
/// the filters, over the fixed member set of <see cref="FixtureReflectionHost"/>. Written against the code before the fixes.
/// </summary>
public sealed class NodeSearchGoldenTests(IReflectionHost sharedReflection)
    : GraphTestBase(new TestEditor(new FixtureReflectionHost(sharedReflection)))
{
    private static string Render(IEnumerable<SuggestionItem> rows)
    {
        var text = new StringBuilder();
        foreach (SuggestionItem row in rows)
        {
            text.Append(row.IsHeader ? $"H:{row.Category}" : $"  {row.Text} | {row.IconKey}").Append('\n');
        }

        return text.ToString();
    }

    private (string Name, NodePin? Pin)[] PinsOf(NodeGraph graph)
    {
        var write = new CallMethodNode(graph, ConsoleWriteLine(StringType));
        var upper = new CallMethodNode(graph, FindMethod(typeof(string), "ToUpperInvariant"));
        return
        [
            ("none", null),
            ("exec in", write.InputExecPins[0]),
            ("exec out", write.OutputExecPins[0]),
            ("string data in", write.ArgumentPins[0]),
            ("string data out", upper.ReturnValuePins[0]),
            ("type in", new MakeArrayTypeNode(graph).InputTypePins[0]),
            ("int type out", new TypeNode(graph, IntType).OutputTypePins[0]),
        ];
    }

    private NodeGraph GraphOfKind(string kind) => kind switch
    {
        "method" => Method,
        "constructor" => ClassContext.CreateConstructor(),
        "class" => Class,
        "event" => ClassContext.CreateEventGraph(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    [Theory]
    [InlineData("method")]
    [InlineData("constructor")]
    [InlineData("class")]
    [InlineData("event")]
    public void TheRowsOfEveryPinMatchTheGolden(string kind)
    {
        NodeGraph graph = GraphOfKind(kind);
        var pins = PinsOf(graph);
        using var view = new NodeGraphViewModel(graph, ClassContext.Services);
        var text = new StringBuilder();

        foreach (var (name, pin) in pins)
        {
            text.Append($"== {name}\n").Append(Render(view.Search.BuildItems(pin)));
        }

        SearchGolden.Check($"rows-{kind}", text.ToString());
    }

    [Theory]
    [InlineData("for")]
    [InlineData("write line")]
    [InlineData("static")]
    [InlineData("netprints loop")]
    [InlineData("zzz")]
    public async Task TheFilteredRowsMatchTheGolden(string filter)
    {
        await Graph.OpenSearchAsync(new GraphPoint(0, 0), null, TestContext.Current.CancellationToken);
        Graph.Search.SearchText = filter;
        Editor.Scheduler.AdvanceBy(Graph.Search.FilterThrottle.Ticks);

        SearchGolden.Check($"filter-{filter.Replace(' ', '-')}", Render(Graph.Search.Items));
    }
}
