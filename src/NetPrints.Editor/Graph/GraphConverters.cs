using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Graph.Pins;

namespace NetPrints.Editor.Graph;

/// <summary>Converts <see cref="GraphPoint"/> to and from <see cref="Point"/>.</summary>
public sealed class GraphPointConverter : IValueConverter
{
    public static readonly GraphPointConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is GraphPoint p ? new Point(p.X, p.Y) : AvaloniaProperty.UnsetValue;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Point p ? new GraphPoint(p.X, p.Y) : AvaloniaProperty.UnsetValue;
}

/// <summary>
/// Maps <see cref="NodeVisualKind"/> and <see cref="PinKind"/> to brushes with the WPF editor's ARGB values.
/// </summary>
public static class GraphBrushes
{
    public static readonly IImmutableSolidColorBrush SelectedBorder = new ImmutableSolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x99, 0x00));
    public static readonly IImmutableSolidColorBrush DeselectedBorder = new ImmutableSolidColorBrush(Color.FromArgb(0xCC, 0x30, 0x30, 0x30));
    public static readonly IImmutableSolidColorBrush DefaultValueActive = new ImmutableSolidColorBrush(Color.FromArgb(0xFF, 0x10, 0xEE, 0xFF));
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

    public static IBrush NodeHeader(NodeVisualKind kind) => NodeHeaders[kind];

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
    public static readonly NodeKindBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is NodeVisualKind kind ? GraphBrushes.NodeHeader(kind) : AvaloniaProperty.UnsetValue;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>PinKind → pin/cable brush.</summary>
public sealed class PinKindBrushConverter : IValueConverter
{
    public static readonly PinKindBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is PinKind kind ? GraphBrushes.Pin(kind) : AvaloniaProperty.UnsetValue;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>(PinKind, IsDimmed) → pin fill brush.</summary>
public sealed class PinFillConverter : IMultiValueConverter
{
    public static readonly PinFillConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) =>
        values.Count == 2 && values[0] is PinKind kind && values[1] is bool dimmed
            ? (dimmed ? GraphBrushes.DimmedPin(kind) : GraphBrushes.Pin(kind))
            : AvaloniaProperty.UnsetValue;
}

/// <summary>bool (selected) → node border brush.</summary>
public sealed class SelectedBorderConverter : IValueConverter
{
    public static readonly SelectedBorderConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? GraphBrushes.SelectedBorder : GraphBrushes.DeselectedBorder;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>bool (default value active) → indicator brush (full or half alpha).</summary>
public sealed class DefaultValueBrushConverter : IValueConverter
{
    public static readonly DefaultValueBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? GraphBrushes.DefaultValueActive : GraphBrushes.DefaultValueInactive;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>bool (faint) → cable opacity (0.1 faint, 0.7 normal).</summary>
public sealed class FaintOpacityConverter : IValueConverter
{
    public static readonly FaintOpacityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is true ? 0.1 : 0.7;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>bool (faint) → cable thickness (2 faint, 4 normal).</summary>
public sealed class FaintThicknessConverter : IValueConverter
{
    public static readonly FaintThicknessConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is true ? 2.0 : 4.0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
