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

    /// <summary>
    /// Largest number of differing pixels for images that still match. The percentage alone scales with the
    /// window (0.5% of 1600x1000 is 8000 pixels, more than a missing check box or button changes), so this
    /// bounds the absolute count; raise it per snapshot where a specific baseline needs more.
    /// </summary>
    public int MaxDiffPixels { get; init; } = 150;

    public IReadOnlyList<SnapshotMask> Masks { get; init; } = [];
}

/// <summary>The result of comparing an image with its baseline.</summary>
public sealed record SnapshotComparison(bool Matches, double DiffPercent, int DiffPixels, string? Reason, UiImage? Diff);

/// <summary>Tolerant pixel comparison: per-pixel threshold, maximum differing share and count, and masks.</summary>
public static class SnapshotComparer
{
    public static SnapshotComparison Compare(UiImage actual, UiImage baseline, SnapshotOptions options)
    {
        if (actual.Width != baseline.Width || actual.Height != baseline.Height)
        {
            return new SnapshotComparison(false, 100, actual.Width * actual.Height,
                $"size {actual.Width}x{actual.Height} differs from the baseline's {baseline.Width}x{baseline.Height}", null);
        }

        var (differing, compared) = Count(actual.Bitmap, baseline.Bitmap, options);
        double percent = compared == 0 ? 0 : 100.0 * differing / compared;
        bool matches = percent <= options.MaxDiffPercent && differing <= options.MaxDiffPixels;
        return new SnapshotComparison(matches, percent, differing,
            matches ? null : $"{differing} pixels ({percent:0.###}%) differ (max {options.MaxDiffPixels} pixels, {options.MaxDiffPercent}%)",
            matches ? null : DrawDiff(actual.Bitmap, baseline.Bitmap, options));
    }

    // Counts on the raw pixel rows: a screenshot at 2.0 has millions of pixels, and a per-pixel GetPixel pass took seconds.
    private static (int Differing, int Compared) Count(SKBitmap actual, SKBitmap baseline, SnapshotOptions options)
    {
        const int BytesPerPixel = 4;
        if (actual.BytesPerPixel != BytesPerPixel || baseline.ColorType != actual.ColorType)
        {
            throw new InvalidOperationException($"Snapshots are compared as 32-bit pixels of one layout, got {actual.ColorType} and {baseline.ColorType}.");
        }

        ReadOnlySpan<byte> a = actual.GetPixelSpan();
        ReadOnlySpan<byte> b = baseline.GetPixelSpan();
        int differing = 0, compared = 0;
        for (int y = 0; y < actual.Height; y++)
        {
            bool masked = options.Masks.Any(m => y >= m.Y && y < m.Y + m.Height);
            for (int x = 0; x < actual.Width; x++)
            {
                if (masked && options.Masks.Any(m => m.Contains(x, y)))
                {
                    continue;
                }

                compared++;
                int offset = y * actual.RowBytes + x * BytesPerPixel;
                int offsetB = y * baseline.RowBytes + x * BytesPerPixel;
                int delta = Math.Max(Math.Max(Math.Abs(a[offset] - b[offsetB]), Math.Abs(a[offset + 1] - b[offsetB + 1])),
                    Math.Max(Math.Abs(a[offset + 2] - b[offsetB + 2]), Math.Abs(a[offset + 3] - b[offsetB + 3])));
                if (delta > options.PixelThreshold)
                {
                    differing++;
                }
            }
        }

        return (differing, compared);
    }

    private static UiImage DrawDiff(SKBitmap actual, SKBitmap baseline, SnapshotOptions options)
    {
        using var diff = new SKBitmap(actual.Width, actual.Height);
        for (int y = 0; y < actual.Height; y++)
        {
            for (int x = 0; x < actual.Width; x++)
            {
                if (options.Masks.Any(m => m.Contains(x, y)))
                {
                    diff.SetPixel(x, y, new SKColor(0, 0, 255, 60));
                    continue;
                }

                var a = actual.GetPixel(x, y);
                var b = baseline.GetPixel(x, y);
                int delta = Math.Max(Math.Max(Math.Abs(a.Red - b.Red), Math.Abs(a.Green - b.Green)),
                    Math.Max(Math.Abs(a.Blue - b.Blue), Math.Abs(a.Alpha - b.Alpha)));

                // A differing pixel in red, the others as the baseline, faded, for orientation.
                diff.SetPixel(x, y, delta > options.PixelThreshold ? SKColors.Red : new SKColor(b.Red, b.Green, b.Blue, 50));
            }
        }

        using var image = SKImage.FromBitmap(diff);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return new UiImage(data.ToArray());
    }
}
