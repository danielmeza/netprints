using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NetPrints.Editor.Icons;
using NetPrints.Editor.State;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Editor.UITests.Theming;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>The start page adapts to its own width: two columns from 960 DIP, one below, centred and at most 1200 wide (FR-045).</summary>
public class StartPageLayoutTests
{
    private const int WideWidth = 1600;
    private const int NarrowWidth = 900;
    private const int MaxContentWidth = 1200;
    private const int MaxWhatsNewWidth = 760;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<UiBounds> BoundsAsync(StartPageRig rig, string id) => new(AsRect((await rig.Element(id).GetAsync(Token)).Bounds));

    private static Avalonia.Rect AsRect(NetPrints.Editor.Hosting.Automation.AutomationRect bounds) => new(bounds.X, bounds.Y, bounds.Width, bounds.Height);

    private readonly record struct UiBounds(Avalonia.Rect Rect)
    {
        public double Left => Rect.X;

        public double Right => Rect.X + Rect.Width;

        public double Top => Rect.Y;

        public double Bottom => Rect.Y + Rect.Height;
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AtAWideSizeTheGetStartedCardsStandRightOfTheRecentList()
    {
        using var rig = StartPageRig.Create(WideWidth, 1000, withRecent: true);

        UiBounds recent = await BoundsAsync(rig, AutomationIds.StartPageRecentSection);
        UiBounds cards = await BoundsAsync(rig, AutomationIds.StartPageGetStarted);

        Assert.True(cards.Left >= recent.Right - 1, $"cards {cards.Rect} should be right of the recent list {recent.Rect}");
        Assert.True(Math.Abs(cards.Top - recent.Top) < 2, "both columns start on the same row");
        Assert.True(cards.Rect.Width > recent.Rect.Width, "the get started section is the wider column");
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AtANarrowSizeTheGetStartedCardsStandAboveTheRecentList()
    {
        using var rig = StartPageRig.Create(NarrowWidth, 1000, withRecent: true);

        UiBounds recent = await BoundsAsync(rig, AutomationIds.StartPageRecentSection);
        UiBounds cards = await BoundsAsync(rig, AutomationIds.StartPageGetStarted);

        Assert.True(cards.Bottom <= recent.Top + 1, $"cards {cards.Rect} should be above the recent list {recent.Rect}");
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ResizingAcrossTheBreakpointSwitchesTheLayout()
    {
        using var rig = StartPageRig.Create(NarrowWidth, 1000);
        UiBounds narrowCards = await BoundsAsync(rig, AutomationIds.StartPageGetStarted);
        UiBounds narrowRecent = await BoundsAsync(rig, AutomationIds.StartPageRecentSection);
        Assert.True(narrowCards.Bottom <= narrowRecent.Top + 1);

        rig.Window.Width = WideWidth;
        HeadlessDriver.Pump();

        UiBounds recent = await BoundsAsync(rig, AutomationIds.StartPageRecentSection);
        UiBounds cards = await BoundsAsync(rig, AutomationIds.StartPageGetStarted);
        Assert.True(cards.Left >= recent.Right - 1);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheContentIsCentredAndAtMost1200Wide()
    {
        using var rig = StartPageRig.Create(WideWidth, 1000);

        UiBounds content = await BoundsAsync(rig, AutomationIds.StartPageContent);

        Assert.True(content.Rect.Width <= MaxContentWidth + 1);
        double leftGap = content.Left;
        double rightGap = WideWidth - content.Right;
        Assert.True(Math.Abs(leftGap - rightGap) < 20, $"gaps {leftGap} and {rightGap}");
        Assert.True(leftGap > 100);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task WhatsNewIsOpenTheFirstTimeAndCollapsedOnceItsVersionWasSeen()
    {
        var store = new MemoryStateStore();
        using (var first = StartPageRig.Create(WideWidth, 1000, startStore: store))
        {
            Assert.True(await first.Element(AutomationIds.StartPageWhatsNew).IsVisibleAsync(Token));
        }

        using var second = StartPageRig.Create(WideWidth, 1000, startStore: store);
        Assert.True(await second.Element(AutomationIds.StartPageWhatsNewToggle).ExistsAsync(Token));
        Assert.False(await second.Element(AutomationIds.StartPageWhatsNew).IsVisibleAsync(Token));

        await second.Element(AutomationIds.StartPageWhatsNewToggle).ClickAsync(Token);

        Assert.True(await second.Element(AutomationIds.StartPageWhatsNew).IsVisibleAsync(Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task WhatsNewStopsAtItsMaximumWidthOnAWideWindow()
    {
        using var rig = StartPageRig.Create(WideWidth, 1000);

        UiBounds notes = await BoundsAsync(rig, AutomationIds.StartPageWhatsNew);

        Assert.True(notes.Rect.Width <= MaxWhatsNewWidth + 1, $"width {notes.Rect.Width}");
        Assert.True(notes.Rect.Width > 300);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheReopenCheckBoxSavesTheStartupSettingBothWays()
    {
        var store = new MemoryStateStore();
        using var rig = StartPageRig.Create(WideWidth, 1000, startStore: store);

        await rig.Element(AutomationIds.StartPageReopenLast).ClickAsync(Token);
        Assert.Equal(StartupBehavior.ReopenLastProject, store.Start?.StartupBehavior);

        await rig.Element(AutomationIds.StartPageReopenLast).ClickAsync(Token);
        Assert.Equal(StartupBehavior.ShowStartPage, store.Start?.StartupBehavior);
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(CanvasPaletteTests.Variants), MemberType = typeof(CanvasPaletteTests))]
    public void EveryRecentRowStartsWithAnIconTileAndANotFoundRowKeepsItsStatus(string variant)
    {
        CanvasPaletteTests.App.RequestedThemeVariant = CanvasPaletteTests.VariantOf(variant);
        try
        {
            using var rig = StartPageRig.Create(WideWidth, 1000, withRecent: true);
            ThemeVariant theme = CanvasPaletteTests.VariantOf(variant);
            double size = Assert.IsType<double>(CanvasPaletteTests.Resolve("StartPage.RecentTileSize", theme));
            CornerRadius radius = Assert.IsType<CornerRadius>(CanvasPaletteTests.Resolve("Radius.Control", theme));
            Color background = CanvasPaletteTests.ColorOf(CanvasPaletteTests.Resolve("StartPage.RecentTileBackground", theme));

            var rows = rig.Window.GetVisualDescendants().OfType<Grid>().Where(g => AutomationProperties.GetAutomationId(g) == AutomationIds.StartPageRecentRow).ToList();

            Assert.True(rows.Count >= 5, $"{rows.Count} rows");
            foreach (Grid row in rows)
            {
                var tile = Assert.IsType<Border>(row.Children.First(child => Grid.GetColumn(child) == 0));
                Assert.Equal(size, tile.Width);
                Assert.Equal(size, tile.Height);
                Assert.Equal(radius, tile.CornerRadius);
                Assert.Equal(background, CanvasPaletteTests.ColorOf(tile.Background));
                Assert.Equal(IconIds.Project, Assert.Single(tile.GetVisualDescendants().OfType<IconPresenter>()).IconId);
            }

            Grid missing = rows.Single(row => AutomationProperties.GetName(row) == "Missing");
            TextBlock status = missing.GetVisualDescendants().OfType<TextBlock>().Single(t => AutomationProperties.GetAutomationId(t) == AutomationIds.StartPageRecentStatus);
            Assert.Equal("Not found", status.Text);
            Assert.True(status.IsEffectivelyVisible);
        }
        finally
        {
            CanvasPaletteTests.App.RequestedThemeVariant = ThemeVariant.Dark;
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task EveryCardAndEveryExistingIdIsThere()
    {
        using var rig = StartPageRig.Create(WideWidth, 1000, withRecent: true);

        foreach (string id in new[]
        {
            AutomationIds.StartPageRoot, AutomationIds.StartPageRecentSearch, AutomationIds.StartPageRecentList, AutomationIds.StartPageOpenButton,
            AutomationIds.StartPageNewButton, AutomationIds.StartPageReopenLast, AutomationIds.StartPageSamplesList, AutomationIds.StartPageSampleOpen, AutomationIds.StartPageReleasesLink,
            AutomationIds.StartPageLearn, AutomationIds.StartPageLearnGuide, AutomationIds.StartPageLearnShortcuts, AutomationIds.StartPageLearnDocs,
        })
        {
            Assert.True(await rig.Element(id).ExistsAsync(Token), id);
        }
    }
}
