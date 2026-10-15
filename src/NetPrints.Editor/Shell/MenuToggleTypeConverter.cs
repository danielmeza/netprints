using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace NetPrints.Editor.Shell;

/// <summary>Maps whether a menu entry is one of a group of choices to the mark its menu item carries: a radio mark for a choice, none otherwise.</summary>
public sealed class MenuToggleTypeConverter : IValueConverter
{
    /// <summary>Shared, stateless instance for XAML bindings.</summary>
    public static readonly MenuToggleTypeConverter Instance = new();

    /// <summary>Maps <paramref name="value"/> to a toggle type.</summary>
    /// <param name="value">Whether the entry is a choice; anything but <see langword="true"/> means it is not.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns><see cref="MenuItemToggleType.Radio"/> for a choice, <see cref="MenuItemToggleType.None"/> otherwise.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? MenuItemToggleType.Radio : MenuItemToggleType.None;

    /// <summary>Not supported: this converter is one-way.</summary>
    /// <param name="value">Unused.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
