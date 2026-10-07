using NetPrints.Editor.UITests.Hosting;
using NetPrints.Testing.Ui.Snapshots;
using SkiaSharp;

namespace NetPrints.Editor.UITests.Snapshots;

/// <summary>The default tolerance still sees one small element missing from a large window.</summary>
public class SnapshotToleranceTests
{
    [Theory]
    [InlineData("checkbox", 1066, 56, 340, 26)]
    [InlineData("releaseNotesButton", 1133, 505, 112, 30)]
    [InlineData("newProjectCardText", 1005, 136, 330, 34)]
    public void AMissingStartPageElementFailsTheDefaultComparison(string what, int x, int y, int width, int height)
    {
        byte[] png = File.ReadAllBytes(Path.Combine(UiArtifacts.Snapshots.BaselineDirectory, "start-page-wide-recent.png"));
        using SKBitmap bitmap = SKBitmap.Decode(png);
        SKColor background = bitmap.GetPixel(x - 4, y - 4);
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint { Color = background, Style = SKPaintStyle.Fill })
        {
            canvas.DrawRect(x, y, width, height, paint);
        }

        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        SnapshotComparison result = SnapshotComparer.Compare(new UiImage(data.ToArray()), new UiImage(png), SnapshotOptions.Default);

        Assert.False(result.Matches, $"{what} removed: {result.DiffPixels} px ({result.DiffPercent:0.###}%) differ");
    }

    [Fact]
    public void AFewPixelsOfNoiseStillMatch()
    {
        using var bitmap = new SKBitmap(1600, 1000);
        bitmap.Erase(SKColors.White);
        using SKImage image = SKImage.FromBitmap(bitmap);
        byte[] baseline = image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
        for (int i = 0; i < 20; i++)
        {
            bitmap.SetPixel(i * 7, i * 3, SKColors.Black);
        }

        using SKImage noisy = SKImage.FromBitmap(bitmap);
        SnapshotComparison result = SnapshotComparer.Compare(new UiImage(noisy.Encode(SKEncodedImageFormat.Png, 100).ToArray()), new UiImage(baseline), SnapshotOptions.Default);

        Assert.True(result.Matches, result.Reason);
    }
}
