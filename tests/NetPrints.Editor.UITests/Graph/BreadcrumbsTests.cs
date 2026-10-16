using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Editor.UITests.Graph;

/// <summary>US7 scenario 6: breadcrumbs above a graph reveal their segment in the project tree.</summary>
public class BreadcrumbsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static UiElement Segment(EditorSession session, string name) =>
        new(session.Driver, new AutomationQuery(AutomationIds.BreadcrumbSegment) { Name = name });

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheBreadcrumbsShowProjectClassAndGraphAndFollowARename()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);

        await Segment(session, "HelloWorld").WaitVisibleAsync(Token);
        await Segment(session, "Program").WaitVisibleAsync(Token);
        await Segment(session, "Main").WaitVisibleAsync(Token);

        session.Class.Name = "Renamed";
        session.App.Shell.NotifyModelRenamed();

        await Segment(session, "Renamed").WaitVisibleAsync(Token);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ChoosingASegmentRevealsItInTheProjectTree()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);

        await Segment(session, "Program").ClickAsync(Token);

        Assert.Same(session.Class, session.App.Shell.TreeSelection);
        Assert.True(session.App.Api.IsPanelVisible(NetPrints.Editor.Contributions.BuiltIn.PanelContributions.ProjectTreeId));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheBreadcrumbsFloatOverTheCanvasAndOnlyTheirSegmentsTakeThePointer()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        await Segment(session, "Main").WaitVisibleAsync(Token);
        await session.WaitForRenderedAsync(Token);

        Window window = session.Window;
        ItemsControl bar = window.GetVisualDescendants().OfType<ItemsControl>()
            .Single(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == AutomationIds.Breadcrumbs);
        Control canvas = window.GetVisualDescendants().OfType<Control>()
            .Single(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == AutomationIds.GraphEditor);
        Visual content = Assert.IsAssignableFrom<Visual>(bar.GetVisualParent());

        Assert.Null(bar.Background);
        Assert.Equal(content.Bounds.Height, canvas.Bounds.Height, 1);
        Assert.Equal(0, canvas.TranslatePoint(default, content)?.Y ?? double.NaN, 1);
        Assert.Equal(0, bar.TranslatePoint(default, content)?.Y ?? double.NaN, 1);

        Button last = bar.GetVisualDescendants().OfType<Button>().Last(button => button.IsEffectivelyVisible);
        Rect lastBox = new(last.TranslatePoint(default, window) ?? default, last.Bounds.Size);
        var empty = new Point(lastBox.Right + 120, lastBox.Center.Y);

        Visual? hitEmpty = window.InputHitTest(empty) as Visual;
        Visual? hitSegment = window.InputHitTest(lastBox.Center) as Visual;

        Assert.NotNull(hitEmpty);
        Assert.DoesNotContain(bar, hitEmpty.GetSelfAndVisualAncestors());
        Assert.Contains(canvas, hitEmpty.GetSelfAndVisualAncestors());
        Assert.NotNull(hitSegment);
        Assert.Contains(last, hitSegment.GetSelfAndVisualAncestors());
    }
}
