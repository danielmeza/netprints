using SkiaSharp;

namespace NetPrints.Testing.Ui.Snapshots;

/// <summary>A PNG screenshot with pixel access.</summary>
public sealed class UiImage
{
    private readonly SKBitmap bitmap;

    public UiImage(byte[] png)
    {
        Png = png;
        bitmap = SKBitmap.Decode(png) ?? throw new ArgumentException("Not a decodable image.", nameof(png));
    }

    public byte[] Png { get; }

    public int Width => bitmap.Width;

    public int Height => bitmap.Height;

    /// <summary>The pixel at (x, y) as ARGB.</summary>
    public uint Pixel(int x, int y)
    {
        var c = bitmap.GetPixel(Math.Clamp(x, 0, Width - 1), Math.Clamp(y, 0, Height - 1));
        return (uint)c.Alpha << 24 | (uint)c.Red << 16 | (uint)c.Green << 8 | c.Blue;
    }

    /// <summary>The pixels of a rectangle, for comparisons of a region.</summary>
    public UiImage Crop(int x, int y, int width, int height)
    {
        var rect = SKRectI.Create(x, y, width, height);
        rect.Intersect(SKRectI.Create(0, 0, Width, Height));
        using var subset = new SKBitmap(rect.Width, rect.Height);
        bitmap.ExtractSubset(subset, rect);
        using var image = SKImage.FromBitmap(subset);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return new UiImage(data.ToArray());
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Png);
    }

    internal SKBitmap Bitmap => bitmap;
}
