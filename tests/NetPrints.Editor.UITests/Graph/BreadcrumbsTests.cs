using Avalonia.Headless.XUnit;
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
}
