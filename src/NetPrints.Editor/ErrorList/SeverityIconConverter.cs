using System.Globalization;
using Avalonia.Data.Converters;
using NetPrints.Compilation;
using NetPrints.Editor.Icons;

namespace NetPrints.Editor.ErrorList;

/// <summary>Maps a <see cref="CodeDiagnosticSeverity"/> to the icon shown next to an error-list row (FR-032, OWN-03).</summary>
public sealed class SeverityIconConverter : IValueConverter
{
    /// <summary>Shared, stateless instance for XAML bindings.</summary>
    public static readonly SeverityIconConverter Instance = new();

    /// <summary>Maps <paramref name="value"/> to its icon id.</summary>
    /// <param name="value">Severity to convert; anything but a <see cref="CodeDiagnosticSeverity"/> yields <see langword="null"/>.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>The severity's icon id, or <see langword="null"/>.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        CodeDiagnosticSeverity.Error => IconIds.SeverityError,
        CodeDiagnosticSeverity.Warning => IconIds.SeverityWarning,
        CodeDiagnosticSeverity.Info => IconIds.SeverityInfo,
        _ => null,
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
