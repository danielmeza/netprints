using NetPrints.Testing.Ui.Snapshots;
using SkiaSharp;

namespace NetPrints.Editor.UITests.Snapshots;

/// <summary>
/// <see cref="SnapshotStore.MatchStableAsync"/> against a fake frame source: frames that change a few
/// times and then settle, frames that never settle, and the frame count it records.
/// </summary>
public sealed class StableSnapshotTests : IDisposable
{
    private const string Name = "stable";

    private static readonly StableCaptureOptions NoDelay = new() { Interval = TimeSpan.Zero, MaxFrames = 6 };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly string directory = Path.Combine(Path.GetTempPath(), "netprints-stable-" + Guid.NewGuid().ToString("N"));

    private readonly SnapshotStore store;

    public StableSnapshotTests()
    {
        string baselines = Path.Combine(directory, "baselines");
        Directory.CreateDirectory(baselines);
        store = new SnapshotStore(baselines, Path.Combine(directory, "output"));
        Image(200).Save(Path.Combine(baselines, Name + ".png"));
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);

    private static UiImage Image(byte shade)
    {
        using var bitmap = new SKBitmap(8, 8);
        bitmap.Erase(new SKColor(shade, shade, shade));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return new UiImage(data.ToArray());
    }

    private static Func<CancellationToken, Task<UiImage>> Frames(params byte[] shades)
    {
        int next = 0;
        return _ => Task.FromResult(Image(shades[Math.Min(next++, shades.Length - 1)]));
    }

    [Fact]
    public async Task FramesThatChangeAndThenSettleAreMatchedOnTheSettledFrame()
    {
        var result = await store.MatchStableAsync(Name, Frames(10, 120, 200, 200), stable: NoDelay, cancellationToken: Token);

        Assert.Equal(4, result.Frames);
        Assert.Contains($"{Name}: 4 frames", File.ReadAllText(Path.Combine(store.OutputDirectory, SnapshotStore.StabilityLogFile)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFrameThatNeverChangesSettlesOnTheSecondFrame()
    {
        var result = await store.MatchStableAsync(Name, Frames(200), stable: NoDelay, cancellationToken: Token);

        Assert.Equal(2, result.Frames);
    }

    [Fact]
    public async Task ASettledFrameThatDiffersFromTheBaselineStillFails()
    {
        var thrown = await Assert.ThrowsAsync<SnapshotMismatchException>(
            () => store.MatchStableAsync(Name, Frames(10, 10), stable: NoDelay, cancellationToken: Token));

        Assert.Contains("does not match its baseline", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FramesThatNeverSettleFailWithTheFramesTakenAndTheLastDiff()
    {
        int next = 0;
        Func<CancellationToken, Task<UiImage>> flicker = _ => Task.FromResult(Image(next++ % 2 == 0 ? (byte)10 : (byte)200));

        var thrown = await Assert.ThrowsAsync<SnapshotMismatchException>(
            () => store.MatchStableAsync(Name, flicker, stable: NoDelay, cancellationToken: Token));

        Assert.Contains($"'{Name}' did not settle", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("after 6 frames", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("100% of the pixels differ", thrown.Message, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(store.OutputDirectory, Name + ".diff.png")));
    }

    [Fact]
    public async Task FramesThatNeverSettleStopAtTheTimeBudget()
    {
        int next = 0;
        Func<CancellationToken, Task<UiImage>> flicker = _ => Task.FromResult(Image(next++ % 2 == 0 ? (byte)10 : (byte)200));
        var options = new StableCaptureOptions { Interval = TimeSpan.FromMilliseconds(5), MaxFrames = int.MaxValue, Budget = TimeSpan.FromMilliseconds(100) };

        var thrown = await Assert.ThrowsAsync<SnapshotMismatchException>(
            () => store.MatchStableAsync(Name, flicker, stable: options, cancellationToken: Token));

        Assert.Contains("did not settle", thrown.Message, StringComparison.Ordinal);
        Assert.InRange(next, 2, 100);
    }

    [Fact]
    public async Task AMaskedRegionDoesNotCountAsAChange()
    {
        int next = 0;
        Func<CancellationToken, Task<UiImage>> caret = _ => Task.FromResult(Image(next++ % 2 == 0 ? (byte)190 : (byte)200));
        var masked = new SnapshotOptions { PixelThreshold = 20, Masks = [new SnapshotMask(0, 0, 8, 8)] };

        var result = await store.MatchStableAsync(Name, caret, masked, NoDelay, Token);

        Assert.Equal(2, result.Frames);
    }
}
