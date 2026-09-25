using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Graph.Pins;

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

/// <summary>
/// Maps <see cref="NodeVisualKind"/> and <see cref="PinKind"/> to brushes with the WPF editor's ARGB values.
/// </summary>
public static class GraphBrushes
{
    /// <summary>Border brush for a selected node.</summary>
    public static readonly IImmutableSolidColorBrush SelectedBorder = new ImmutableSolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x99, 0x00));

    /// <summary>Border brush for an unselected node.</summary>
    public static readonly IImmutableSolidColorBrush DeselectedBorder = new ImmutableSolidColorBrush(Color.FromArgb(0xCC, 0x30, 0x30, 0x30));

    /// <summary>Indicator brush for a pin currently using its default/unconnected value.</summary>
    public static readonly IImmutableSolidColorBrush DefaultValueActive = new ImmutableSolidColorBrush(Color.FromArgb(0xFF, 0x10, 0xEE, 0xFF));

    /// <summary>Indicator brush for a pin not using its default/unconnected value.</summary>
    public static readonly IImmutableSolidColorBrush DefaultValueInactive = new ImmutableSolidColorBrush(Color.FromArgb(0x7F, 0x10, 0xEE, 0xFF));

    private static readonly Dictionary<NodeVisualKind, IImmutableSolidColorBrush> NodeHeaders = new()
    {
        [NodeVisualKind.Default] = Brush(0x30, 0x30, 0x30),
        [NodeVisualKind.Entry] = Brush(0x20, 0x20, 0x50),
        [NodeVisualKind.Return] = Brush(0x50, 0x20, 0x20),
        [NodeVisualKind.CallMethod] = Brush(0x20, 0x3A, 0x50),
        [NodeVisualKind.CallStatic] = Brush(0x50, 0x20, 0x3A),
        [NodeVisualKind.Constructor] = Brush(0x3A, 0x50, 0x20),
        [NodeVisualKind.MakeDelegate] = Brush(0x7A, 0x7A, 0x20),
        [NodeVisualKind.Type] = Brush(0x7A, 0x30, 0x20),
        [NodeVisualKind.VariableGetter] = Brush(0x30, 0x5A, 0x5A),
        [NodeVisualKind.VariableSetter] = Brush(0x5A, 0x5A, 0x7A),
        [NodeVisualKind.MakeArray] = Brush(0x1A, 0x5A, 0x30),
        [NodeVisualKind.Throw] = Brush(0xBB, 0x20, 0x20),
        [NodeVisualKind.Ternary] = Brush(0x40, 0x3A, 0x3A),
    };

    private static readonly Dictionary<PinKind, Color> PinColors = new()
    {
        [PinKind.Exec] = Color.FromArgb(0xFF, 0xE0, 0xFF, 0xE0),
        [PinKind.Data] = Color.FromArgb(0xFF, 0xE0, 0xE0, 0xFF),
        [PinKind.Type] = Color.FromArgb(0xFF, 0xFF, 0xE0, 0xE0),
    };

    private static IImmutableSolidColorBrush Brush(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromArgb(0xFF, r, g, b));

    /// <summary>The header brush for a node of the given visual kind.</summary>
    /// <param name="kind">Node visual kind.</param>
    /// <returns>The kind's header brush.</returns>
    /// <exception cref="KeyNotFoundException"><paramref name="kind"/> has no mapped brush.</exception>
    public static IBrush NodeHeader(NodeVisualKind kind) => NodeHeaders[kind];

    /// <summary>The fill/cable brush for a pin of the given kind, at full brightness.</summary>
    /// <param name="kind">Pin kind.</param>
    /// <returns>The kind's brush.</returns>
    public static IBrush Pin(PinKind kind) => new ImmutableSolidColorBrush(PinColors[kind]);

    /// <summary>Unconnected pins are filled at 60 % brightness.</summary>
    public static IBrush DimmedPin(PinKind kind)
    {
        var c = PinColors[kind];
        return new ImmutableSolidColorBrush(Color.FromArgb(c.A, (byte)(c.R * 0.6), (byte)(c.G * 0.6), (byte)(c.B * 0.6)));
    }
}

/// <summary>NodeVisualKind → header brush.</summary>
public sealed class NodeKindBrushConverter : IValueConverter
{
    /// <summary>Shared, stateless instance for XAML bindings.</summary>
    public static readonly NodeKindBrushConverter Instance = new();

    /// <summary>Converts a <see cref="NodeVisualKind"/> to its header brush (see <see cref="GraphBrushes.NodeHeader"/>).</summary>
    /// <param name="value">Value to convert; anything but a <see cref="NodeVisualKind"/> yields <see cref="AvaloniaProperty.UnsetValue"/>.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>The kind's header brush, or <see cref="AvaloniaProperty.UnsetValue"/>.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is NodeVisualKind kind ? GraphBrushes.NodeHeader(kind) : AvaloniaProperty.UnsetValue;

    /// <summary>Not supported: this converter is one-way.</summary>
    /// <param name="value">Unused.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>PinKind → pin/cable brush.</summary>
public sealed class PinKindBrushConverter : IValueConverter
{
    /// <summary>Shared, stateless instance for XAML bindings.</summary>
    public static readonly PinKindBrushConverter Instance = new();

    /// <summary>Converts a <see cref="PinKind"/> to its brush (see <see cref="GraphBrushes.Pin"/>).</summary>
    /// <param name="value">Value to convert; anything but a <see cref="PinKind"/> yields <see cref="AvaloniaProperty.UnsetValue"/>.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>The kind's brush, or <see cref="AvaloniaProperty.UnsetValue"/>.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is PinKind kind ? GraphBrushes.Pin(kind) : AvaloniaProperty.UnsetValue;

    /// <summary>Not supported: this converter is one-way.</summary>
    /// <param name="value">Unused.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>(PinKind, IsDimmed) → pin fill brush.</summary>
public sealed class PinFillConverter : IMultiValueConverter
{
    /// <summary>Shared, stateless instance for XAML bindings.</summary>
    public static readonly PinFillConverter Instance = new();

    /// <summary>Converts a (<see cref="PinKind"/>, dimmed) pair to a pin fill brush.</summary>
    /// <param name="values">Exactly two values: a <see cref="PinKind"/> and a <see cref="bool"/> dimmed flag; anything else yields <see cref="AvaloniaProperty.UnsetValue"/>.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns><see cref="GraphBrushes.DimmedPin"/> if dimmed, otherwise <see cref="GraphBrushes.Pin"/>, or <see cref="AvaloniaProperty.UnsetValue"/>.</returns>
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) =>
        values.Count == 2 && values[0] is PinKind kind && values[1] is bool dimmed
            ? (dimmed ? GraphBrushes.DimmedPin(kind) : GraphBrushes.Pin(kind))
            : AvaloniaProperty.UnsetValue;
}

/// <summary>bool (selected) → node border brush.</summary>
public sealed class SelectedBorderConverter : IValueConverter
{
    /// <summary>Shared, stateless instance for XAML bindings.</summary>
    public static readonly SelectedBorderConverter Instance = new();

    /// <summary>Converts a selected flag to the node's border brush.</summary>
    /// <param name="value">Selected flag.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns><see cref="GraphBrushes.SelectedBorder"/> if <paramref name="value"/> is <see langword="true"/>, otherwise <see cref="GraphBrushes.DeselectedBorder"/>.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? GraphBrushes.SelectedBorder : GraphBrushes.DeselectedBorder;

    /// <summary>Not supported: this converter is one-way.</summary>
    /// <param name="value">Unused.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>bool (default value active) → indicator brush (full or half alpha).</summary>
public sealed class DefaultValueBrushConverter : IValueConverter
{
    /// <summary>Shared, stateless instance for XAML bindings.</summary>
    public static readonly DefaultValueBrushConverter Instance = new();

    /// <summary>Converts a default-value-active flag to the pin's indicator brush.</summary>
    /// <param name="value">Default-value-active flag.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns><see cref="GraphBrushes.DefaultValueActive"/> if <paramref name="value"/> is <see langword="true"/>, otherwise <see cref="GraphBrushes.DefaultValueInactive"/>.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? GraphBrushes.DefaultValueActive : GraphBrushes.DefaultValueInactive;

    /// <summary>Not supported: this converter is one-way.</summary>
    /// <param name="value">Unused.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>bool (faint) → cable opacity (0.1 faint, 0.7 normal).</summary>
public sealed class FaintOpacityConverter : IValueConverter
{
    /// <summary>Shared, stateless instance for XAML bindings.</summary>
    public static readonly FaintOpacityConverter Instance = new();

    /// <summary>Converts a faint flag to a cable opacity.</summary>
    /// <param name="value">Faint flag.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>0.1 if <paramref name="value"/> is <see langword="true"/>, otherwise 0.7.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is true ? 0.1 : 0.7;

    /// <summary>Not supported: this converter is one-way.</summary>
    /// <param name="value">Unused.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>bool (faint) → cable thickness (2 faint, 4 normal).</summary>
public sealed class FaintThicknessConverter : IValueConverter
{
    /// <summary>Shared, stateless instance for XAML bindings.</summary>
    public static readonly FaintThicknessConverter Instance = new();

    /// <summary>Converts a faint flag to a cable thickness in DIP.</summary>
    /// <param name="value">Faint flag.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>2.0 if <paramref name="value"/> is <see langword="true"/>, otherwise 4.0.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is true ? 2.0 : 4.0;

    /// <summary>Not supported: this converter is one-way.</summary>
    /// <param name="value">Unused.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
