using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using NetPrints.Testing.Ui.Snapshots;

namespace NetPrints.Editor.UITests.Snapshots;

/// <summary>One labelled image of the contact sheet.</summary>
/// <param name="Label">The caption under the image.</param>
/// <param name="Image">The image, already shrunk to the cell.</param>
internal sealed record ContactTile(string Label, UiImage Image);

/// <summary>A titled group of tiles that the sheet lays out in rows of four.</summary>
/// <param name="Title">The heading above the group.</param>
/// <param name="Tiles">The tiles in reading order.</param>
internal sealed record ContactSection(string Title, IReadOnlyList<ContactTile> Tiles);

/// <summary>Draws the sections of a contact sheet into one PNG.</summary>
internal static class ContactSheetComposer
{
    private const int Columns = 4;
    private const int Gap = 12;
    private const int CaptionHeight = 24;

    private static readonly IBrush Paper = new SolidColorBrush(Color.Parse("#FF16161A"));
    private static readonly IBrush Matte = new SolidColorBrush(Color.Parse("#FF74747E"));

    /// <summary>Lays the sections out one under the other and renders them.</summary>
    /// <param name="sections">The titled groups of tiles; each tile is at most <paramref name="cellWidth"/> by <paramref name="cellHeight"/> pixels.</param>
    /// <param name="cellWidth">The width of one cell.</param>
    /// <param name="cellHeight">The height of the image area of one cell, caption excluded.</param>
    /// <returns>The sheet.</returns>
    public static UiImage Compose(IReadOnlyList<ContactSection> sections, int cellWidth = 560, int cellHeight = 420)
    {
        ArgumentNullException.ThrowIfNull(sections);
        var bitmaps = new List<Bitmap>();
        try
        {
            var root = new StackPanel { Background = Paper, Margin = new Thickness(Gap) };
            foreach (ContactSection section in sections)
            {
                root.Children.Add(new TextBlock { Text = section.Title, Foreground = Brushes.White, FontSize = 22, Margin = new Thickness(Gap, Gap * 2, 0, Gap) });
                var grid = new UniformGrid { Columns = Columns };
                foreach (ContactTile tile in section.Tiles)
                {
                    var bitmap = new Bitmap(new MemoryStream(tile.Image.Png));
                    bitmaps.Add(bitmap);
                    grid.Children.Add(Cell(tile.Label, bitmap, cellWidth, cellHeight));
                }

                root.Children.Add(grid);
            }

            double width = Columns * (cellWidth + Gap) + Gap;
            root.Measure(new Size(width, double.PositiveInfinity));
            root.Arrange(new Rect(root.DesiredSize));
            var size = new PixelSize((int)Math.Ceiling(root.DesiredSize.Width), (int)Math.Ceiling(root.DesiredSize.Height));
            using var target = new RenderTargetBitmap(size);
            target.Render(root);
            using var stream = new MemoryStream();
            target.Save(stream, new PngBitmapEncoderOptions());
            return new UiImage(stream.ToArray());
        }
        finally
        {
            foreach (Bitmap bitmap in bitmaps)
            {
                bitmap.Dispose();
            }
        }
    }

    private static Border Cell(string label, Bitmap bitmap, int cellWidth, int cellHeight)
    {
        var image = new Image { Source = bitmap, Stretch = Stretch.None, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var picture = new Border { Width = cellWidth, Height = cellHeight, Background = Matte, Child = image };
        var caption = new TextBlock { Text = label, Foreground = Brushes.White, FontSize = 14, Height = CaptionHeight, Margin = new Thickness(2, 4, 0, 0) };
        return new Border { Margin = new Thickness(Gap / 2), Child = new StackPanel { Children = { picture, caption } } };
    }
}
