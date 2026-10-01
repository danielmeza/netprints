using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.UITests.ClassEditor;
using NetPrints.Graph;

namespace NetPrints.Editor.UITests.Graph;

/// <summary>D5, SC-003: the documentation tooltip of a call node comes from the project's resolved references.</summary>
public class NodeTooltipTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task WriteLineNodeTooltipContainsTheSummary()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);

        NodeView view = session.ClassWindow.GetVisualDescendants().OfType<NodeView>()
            .Single(v => v.DataContext is NodeViewModel { Node: CallMethodNode });
        string? tooltip = ToolTip.GetTip(view) as string;

        Assert.False(string.IsNullOrWhiteSpace(tooltip), "WriteLine has no documentation tooltip");
        Assert.Contains("standard output stream", tooltip);
    }
}
