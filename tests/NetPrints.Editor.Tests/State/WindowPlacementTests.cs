using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.State;

namespace NetPrints.Editor.Tests.State;

public sealed class WindowPlacementTests
{
    private static readonly ScreenBounds Primary = new(0, 0, 1920, 1080);
    private static readonly ScreenBounds Second = new(1920, 0, 1280, 1024);

    private static WindowState Saved(int x, int y, int width, int height, bool maximized = false) =>
        new(StateFile.CurrentVersion, x, y, width, height, maximized);

    [Fact]
    public void BoundsOnAScreenAreKept()
    {
        WindowPlacement? placement = WindowPlacement.Resolve(Saved(120, 80, 1600, 960), [Primary], Primary);

        Assert.Equal(new WindowPlacement(new ScreenBounds(120, 80, 1600, 960), false), placement);
    }

    [Fact]
    public void BoundsOnTheSecondScreenAreKept()
    {
        WindowPlacement? placement = WindowPlacement.Resolve(Saved(2000, 50, 800, 600), [Primary, Second], Primary);

        Assert.Equal(new ScreenBounds(2000, 50, 800, 600), placement?.Bounds);
    }

    [Fact]
    public void BoundsThatTouchAScreenOnlyPartlyAreKept()
    {
        WindowPlacement? placement = WindowPlacement.Resolve(Saved(1800, 1000, 800, 600), [Primary], Primary);

        Assert.Equal(new ScreenBounds(1800, 1000, 800, 600), placement?.Bounds);
    }

    [Fact]
    public void BoundsOnAScreenThatIsGoneAreCentredOnThePrimaryScreenAtTheSavedSize()
    {
        WindowPlacement? placement = WindowPlacement.Resolve(Saved(2000, 50, 800, 600), [Primary], Primary);

        Assert.Equal(new ScreenBounds(560, 240, 800, 600), placement?.Bounds);
    }

    [Fact]
    public void BoundsOffEveryScreenAreCentredOnAPrimaryScreenThatDoesNotStartAtTheOrigin()
    {
        var primary = new ScreenBounds(100, 40, 1000, 800);

        WindowPlacement? placement = WindowPlacement.Resolve(Saved(-5000, -5000, 600, 400), [primary], primary);

        Assert.Equal(new ScreenBounds(300, 240, 600, 400), placement?.Bounds);
    }

    [Fact]
    public void AWindowLargerThanThePrimaryScreenIsClampedToIt()
    {
        WindowPlacement? placement = WindowPlacement.Resolve(Saved(5000, 5000, 4000, 3000), [Primary], Primary);

        Assert.Equal(Primary, placement?.Bounds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheMaximizedStateIsRestoredOnAndOffScreen(bool maximized)
    {
        Assert.Equal(maximized, WindowPlacement.Resolve(Saved(10, 10, 800, 600, maximized), [Primary], Primary)?.IsMaximized);
        Assert.Equal(maximized, WindowPlacement.Resolve(Saved(9000, 10, 800, 600, maximized), [Primary], Primary)?.IsMaximized);
    }

    [Theory]
    [InlineData(0, 600)]
    [InlineData(800, -1)]
    public void ASavedSizeThatIsNotUsableGivesNoPlacement(int width, int height)
    {
        Assert.Null(WindowPlacement.Resolve(Saved(10, 10, width, height), [Primary], Primary));
    }

    [Fact]
    public void TheServiceRestoresWhatItSaved()
    {
        var store = new JsonEditorStateStore(new EditorDataPaths("/state-root"), new InMemoryEditorFileSystem(), NullLogger.Instance);
        var service = new WindowStateService(store);

        service.Save(new ScreenBounds(120, 80, 1600, 960), true, Primary);

        Assert.Equal(new WindowPlacement(new ScreenBounds(120, 80, 1600, 960), true), new WindowStateService(store).Restore([Primary], Primary));
        Assert.Equal(Primary, store.LoadWindow()?.Screen);
    }

    [Fact]
    public void TheServiceHasNoPlacementBeforeAnythingWasSaved()
    {
        var store = new JsonEditorStateStore(new EditorDataPaths("/state-root"), new InMemoryEditorFileSystem(), NullLogger.Instance);

        Assert.Null(new WindowStateService(store).Restore([Primary], Primary));
    }

    [Fact]
    public void TheServiceMovesAWindowSavedOnAMissingScreen()
    {
        var store = new JsonEditorStateStore(new EditorDataPaths("/state-root"), new InMemoryEditorFileSystem(), NullLogger.Instance);
        new WindowStateService(store).Save(new ScreenBounds(2000, 50, 800, 600), false, Second);

        WindowPlacement? placement = new WindowStateService(store).Restore([Primary], Primary);

        Assert.Equal(new ScreenBounds(560, 240, 800, 600), placement?.Bounds);
    }
}
