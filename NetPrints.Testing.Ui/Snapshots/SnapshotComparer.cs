using SkiaSharp;

namespace NetPrints.Testing.Ui.Snapshots;

/// <summary>A rectangle excluded from a comparison (caret, focus, dynamic text), in image pixels.</summary>
public readonly record struct SnapshotMask(int X, int Y, int Width, int Height)
{
    public bool Contains(int x, int y) => x >= X && y >= Y && x < X + Width && y < Y + Height;
}

/// <summary>How tolerant a comparison is.</summary>
public sealed record SnapshotOptions
{
    public static readonly SnapshotOptions Default = new();

    /// <summary>
    /// Largest per-channel difference (0..255) of a pixel that still counts as equal
    /// (anti-aliasing noise between runs). 32 was loose enough to miss a real theme/colour
    /// regression (a grid-tint change with a max delta of 7 still matched); raise this per
    /// snapshot, via <see cref="SnapshotOptions"/>, only where a specific baseline needs more
    /// (e.g. text-heavy renders), rather than loosening the default back up.
    /// </summary>
    public int PixelThreshold { get; init; } = 4;

    /// <summary>Largest share of differing pixels, in percent, for images that still match.</summary>
    public double MaxDiffPercent { get; init; } = 0.5;

    public IReadOnlyList<SnapshotMask> Masks { get; init; } = [];
}

/// <summary>The result of comparing an image with its baseline.</summary>
public sealed record SnapshotComparison(bool Matches, double DiffPercent, int DiffPixels, string? Reason, UiImage? Diff);

/// <summary>Tolerant pixel comparison: per-pixel threshold, maximum differing share, and masks.</summary>
public static class SnapshotComparer
{
    public static SnapshotComparison Compare(UiImage actual, UiImage baseline, SnapshotOptions options)
    {
        if (actual.Width != baseline.Width || actual.Height != baseline.Height)
        {
            return new SnapshotComparison(false, 100, actual.Width * actual.Height,
                $"size {actual.Width}x{actual.Height} differs from the baseline's {baseline.Width}x{baseline.Height}", null);
        }

        using var diff = new SKBitmap(actual.Width, actual.Height);
        int differing = 0, compared = 0;
        for (int y = 0; y < actual.Height; y++)
        {
            for (int x = 0; x < actual.Width; x++)
            {
                if (options.Masks.Any(m => m.Contains(x, y)))
                {
                    diff.SetPixel(x, y, new SKColor(0, 0, 255, 60));
                    continue;
                }

                compared++;
                var a = actual.Bitmap.GetPixel(x, y);
                var b = baseline.Bitmap.GetPixel(x, y);
                int delta = Math.Max(Math.Max(Math.Abs(a.Red - b.Red), Math.Abs(a.Green - b.Green)),
                    Math.Max(Math.Abs(a.Blue - b.Blue), Math.Abs(a.Alpha - b.Alpha)));
                if (delta > options.PixelThreshold)
                {
                    differing++;
                    diff.SetPixel(x, y, SKColors.Red);
                }
                else
                {
                    // The baseline, faded, for orientation.
                    diff.SetPixel(x, y, new SKColor(b.Red, b.Green, b.Blue, 50));
                }
            }
        }

        double percent = compared == 0 ? 0 : 100.0 * differing / compared;
        bool matches = percent <= options.MaxDiffPercent;
        using var image = SKImage.FromBitmap(diff);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return new SnapshotComparison(matches, percent, differing,
            matches ? null : $"{percent:0.###}% of the pixels differ (max {options.MaxDiffPercent}%)", new UiImage(data.ToArray()));
    }
}
