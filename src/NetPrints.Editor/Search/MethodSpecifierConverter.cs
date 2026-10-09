using System.Globalization;
using Avalonia.Data.Converters;
using NetPrints.Core;

namespace NetPrints.Editor.Search;

/// <summary>
/// Displays method and constructor specifiers through <see cref="MethodSignatureFormatter"/> ("string ToString()"); used by the two pickers.
/// </summary>
public sealed class MethodSpecifierConverter : IValueConverter
{
    /// <summary>Shared, stateless instance for XAML bindings.</summary>
    public static readonly MethodSpecifierConverter Instance = new();

    /// <summary>Formats a <see cref="MethodSpecifier"/> or <see cref="ConstructorSpecifier"/> via <see cref="MethodSignatureFormatter"/>.</summary>
    /// <param name="value">Value to convert; anything but a <see cref="MethodSpecifier"/> falls back to <see cref="object.ToString"/>.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>The formatted method, or <paramref name="value"/>'s own string form.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        MethodSpecifier method => MethodSignatureFormatter.Format(method),
        ConstructorSpecifier constructor => MethodSignatureFormatter.Format(constructor),
        _ => value?.ToString(),
    };

    /// <summary>Not supported: this converter is one-way.</summary>
    /// <param name="value">Unused.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
