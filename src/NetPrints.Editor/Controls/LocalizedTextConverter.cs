using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using NetPrints.Core;

namespace NetPrints.Editor.Controls;

/// <summary>Maps a <see cref="LocalizedText"/> to the text the editor shows for the UI culture (FR-027).</summary>
public sealed class LocalizedTextConverter : IValueConverter
{
    /// <summary>Shared, stateless instance for XAML bindings.</summary>
    public static readonly LocalizedTextConverter Instance = new();

    /// <summary>Resolves the text through the editor's resources.</summary>
    /// <param name="value">The text to convert.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">The UI culture, or null for the current one.</param>
    /// <returns>The translation, the default text or the key; <see cref="AvaloniaProperty.UnsetValue"/> for any other value.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is LocalizedText text ? text.Resolve(Strings.ResourceManager, culture) : AvaloniaProperty.UnsetValue;

    /// <summary>Not supported: this converter is one-way.</summary>
    /// <param name="value">Unused.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
