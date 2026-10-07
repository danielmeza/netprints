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

    [Fact]
    public void AWindowLargerThanTheScreenItLandsOnIsClampedToIt()
    {
        WindowPlacement? placement = WindowPlacement.Resolve(Saved(0, 0, 3840, 2160), [Primary], Primary);

        Assert.Equal(Primary, placement?.Bounds);
    }

    [Fact]
    public void AWindowWhoseTitleBarIsAboveEveryScreenIsMovedDown()
    {
        WindowPlacement? placement = WindowPlacement.Resolve(Saved(-1900, -700, 1920, 1080), [Primary], Primary);

        Assert.Equal(new ScreenBounds(-1820, 0, 1920, 1080), placement?.Bounds);
    }

    [Fact]
    public void AWindowWhoseTitleBarIsBelowTheScreenIsMovedUp()
    {
        WindowPlacement? placement = WindowPlacement.Resolve(Saved(100, 1070, 800, 600), [Primary], Primary);

        Assert.Equal(new ScreenBounds(100, 1048, 800, 600), placement?.Bounds);
    }

    [Fact]
    public void AWindowWithOnlyAFewPixelsOnTheScreenKeepsAWideEnoughTitleStrip()
    {
        WindowPlacement? placement = WindowPlacement.Resolve(Saved(1915, 50, 800, 600), [Primary], Primary);

        Assert.Equal(new ScreenBounds(1820, 50, 800, 600), placement?.Bounds);
    }

    [Fact]
    public void AWindowAcrossTwoScreensGoesToTheOneItOverlapsMostAndIsClampedToIt()
    {
        WindowPlacement? placement = WindowPlacement.Resolve(Saved(1500, 0, 1600, 1000), [Primary, Second], Primary);

        Assert.Equal(new ScreenBounds(1920, 0, 1280, 1000), placement?.Bounds);
    }

    [Fact]
    public void ThePlacementCarriesTheScalingOfTheScreenItLandsOn()
    {
        var sharp = new ScreenBounds(1920, 0, 2560, 1440, 2.0);
        var plain = new ScreenBounds(0, 0, 1920, 1080, 1.0);

        WindowPlacement? onSharp = WindowPlacement.Resolve(Saved(2000, 100, 1600, 900), [plain, sharp], plain);
        WindowPlacement? onPlain = WindowPlacement.Resolve(Saved(100, 100, 1600, 900), [plain, sharp], plain);
        WindowPlacement? moved = WindowPlacement.Resolve(Saved(9000, 100, 1600, 900), [plain, sharp], plain);

        Assert.Equal(2.0, onSharp?.Scaling);
        Assert.Equal(800, onSharp?.WidthInDips);
        Assert.Equal(450, onSharp?.HeightInDips);
        Assert.Equal(1.0, onPlain?.Scaling);
        Assert.Equal(1.0, moved?.Scaling);
    }

    [Fact]
    public void ARestoredSizeDoesNotDriftWhenTheSaveAndTheRestoreUseTheScalingOfTheSameScreen()
    {
        var screen = new ScreenBounds(0, 0, 3840, 2160, 1.5);
        WindowPlacement? first = WindowPlacement.Resolve(Saved(100, 100, 1500, 900), [screen], screen);
        var savedAgain = Saved(100, 100, (int)Math.Round((first?.WidthInDips ?? 0) * 1.5), (int)Math.Round((first?.HeightInDips ?? 0) * 1.5));

        WindowPlacement? second = WindowPlacement.Resolve(savedAgain, [screen], screen);

        Assert.Equal(first, second);
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
