using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace NetPrints.Editor.Search;

/// <summary>Icon file name → cached 16-px bitmap from the editor assets.</summary>
public sealed class IconConverter : IValueConverter
{
    /// <summary>Shared, stateless instance for XAML bindings.</summary>
    public static readonly IconConverter Instance = new();

    private static readonly Dictionary<string, Bitmap> Cache = [];

    /// <summary>
    /// Loads (or returns the cached) 16-px bitmap for an icon file name from the editor's embedded
    /// assets.
    /// </summary>
    /// <param name="value">Icon file name; anything but a non-empty string yields <see langword="null"/>.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>The bitmap, or <see langword="null"/>.</returns>
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

    /// <summary>Not supported: this converter is one-way.</summary>
    /// <param name="value">Unused.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
