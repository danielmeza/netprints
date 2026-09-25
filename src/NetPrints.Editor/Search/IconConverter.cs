using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace NetPrints.Editor.Search;

/// <summary>Icon file name → cached 16-px bitmap from the editor assets.</summary>
public sealed class IconConverter : IValueConverter
{
    public static readonly IconConverter Instance = new();

    private static readonly Dictionary<string, Bitmap> Cache = [];

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string { Length: > 0 } key)
        {
            return null;
        }

        lock (Cache)
        {
            if (!Cache.TryGetValue(key, out var bitmap))
            {
                using var stream = AssetLoader.Open(new Uri($"avares://NetPrints.Editor/Assets/{key}"));
                bitmap = new Bitmap(stream);
                Cache[key] = bitmap;
            }

            return bitmap;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
