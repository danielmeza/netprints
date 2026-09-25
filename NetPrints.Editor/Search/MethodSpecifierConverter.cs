using System.Globalization;
using Avalonia.Data.Converters;
using NetPrints.Core;

namespace NetPrints.Editor.Search;

/// <summary>
/// Displays method specifiers like the WPF editor ("Type Name (params) : returns", operators by name).
/// </summary>
public sealed class MethodSpecifierConverter : IValueConverter
{
    public static readonly MethodSpecifierConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is MethodSpecifier method ? SuggestionItem.FormatMethod(method) : value?.ToString();

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
