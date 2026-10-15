using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace NetPrints.Editor.Graph;

/// <summary>Converts <see cref="GraphPoint"/> to and from <see cref="Point"/>.</summary>
public sealed class GraphPointConverter : IValueConverter
{
    /// <summary>Shared, stateless instance for XAML bindings.</summary>
    public static readonly GraphPointConverter Instance = new();

    /// <summary>Converts a <see cref="GraphPoint"/> to a <see cref="Point"/>.</summary>
    /// <param name="value">Value to convert; anything but a <see cref="GraphPoint"/> yields <see cref="AvaloniaProperty.UnsetValue"/>.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>The converted <see cref="Point"/>, or <see cref="AvaloniaProperty.UnsetValue"/>.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is GraphPoint p ? new Point(p.X, p.Y) : AvaloniaProperty.UnsetValue;

    /// <summary>Converts a <see cref="Point"/> back to a <see cref="GraphPoint"/>.</summary>
    /// <param name="value">Value to convert; anything but a <see cref="Point"/> yields <see cref="AvaloniaProperty.UnsetValue"/>.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>The converted <see cref="GraphPoint"/>, or <see cref="AvaloniaProperty.UnsetValue"/>.</returns>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Point p ? new GraphPoint(p.X, p.Y) : AvaloniaProperty.UnsetValue;
}
