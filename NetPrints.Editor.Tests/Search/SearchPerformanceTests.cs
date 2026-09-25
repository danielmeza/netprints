using System.Diagnostics;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Search;
using NetPrints.Editor.Tests.Hosting;

namespace NetPrints.Editor.Tests.Search;

/// <summary>
/// SC-005, measured on the cold path: a freshly loaded reflection host (as right after a project
/// opens) and the first search of a graph. Timings go to the test output; the assertions are a
/// generous regression bound so a busy CI runner does not fail the build.
/// </summary>
public class SearchPerformanceTests
{
    // Budget: open in < 2 s, filter in < 300 ms per keystroke. Asserted with a 3x margin.
    private const double OpenBoundMs = 6000;
    private const double KeystrokeBoundMs = 900;

    [Fact(Timeout = 180_000)]
    [Trait("Category", "Performance")]
    public async Task FirstSearchAfterProjectLoadIsWithinBudget()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var output = TestContext.Current.TestOutputHelper!;
        var host = new ReflectionHost(new InlineDispatcher());

        var load = Stopwatch.StartNew();
        await host.ReloadAsync(Project.CreateNew("P", "N"), cancellationToken);
        load.Stop();

        var cls = new ClassGraph { Name = "Cold", Namespace = "N" };
        using var classEditor = new ClassEditorVM(cls, new TestEditor(host).Context);
        classEditor.CreateMethodCommand.Execute(null);
        var search = classEditor.OpenedGraph!.Search;
        search.FilterThrottle = TimeSpan.Zero;

        var open = Stopwatch.StartNew();
        List<SuggestionItem> rows = search.BuildItems(null);
        search.SetItems(rows);
        open.Stop();

        var keystrokes = new[] { "w", "wr", "wri", "writ", "write", "write ", "write l", "write li", "write lin", "write line" };
        double worstMs = 0;
        var keystroke = new Stopwatch();
        foreach (var text in keystrokes)
        {
            keystroke.Restart();
            search.SearchText = text;
            worstMs = Math.Max(worstMs, keystroke.Elapsed.TotalMilliseconds);
        }

        output.WriteLine($"SC-005 cold: reflection load (incl. warm-up) {load.ElapsedMilliseconds} ms; first search {open.ElapsedMilliseconds} ms " +
            $"for {rows.Count} rows; worst keystroke {worstMs:F0} ms");

        Assert.True(rows.Count > 30_000, $"the full runtime set is searched ({rows.Count} rows)");
        Assert.Contains(search.Items, i => i.Value is MethodSpecifier { Name: "WriteLine" });
        Assert.True(open.ElapsedMilliseconds < OpenBoundMs, $"first search took {open.ElapsedMilliseconds} ms");
        Assert.True(worstMs < KeystrokeBoundMs, $"a keystroke took {worstMs:F0} ms");
    }
}
