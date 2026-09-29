namespace NetPrints.Editor.Graph;

/// <summary>
/// A point in graph (canvas) coordinates. Replaces UI-toolkit point types in view models.
/// </summary>
/// <param name="X">X coordinate, in graph units.</param>
/// <param name="Y">Y coordinate, in graph units.</param>
public readonly record struct GraphPoint(double X, double Y)
{
    /// <summary>The origin, (0, 0).</summary>
    public static readonly GraphPoint Zero = new(0, 0);

    /// <summary>Adds two points component-wise.</summary>
    /// <param name="a">First point.</param>
    /// <param name="b">Second point.</param>
    /// <returns>The sum.</returns>
    public static GraphPoint operator +(GraphPoint a, GraphPoint b) => new(a.X + b.X, a.Y + b.Y);

    /// <summary>Subtracts two points component-wise.</summary>
    /// <param name="a">First point.</param>
    /// <param name="b">Second point.</param>
    /// <returns>The difference.</returns>
    public static GraphPoint operator -(GraphPoint a, GraphPoint b) => new(a.X - b.X, a.Y - b.Y);

    /// <summary>Scales a point's coordinates.</summary>
    /// <param name="a">Point to scale.</param>
    /// <param name="s">Scale factor.</param>
    /// <returns>The scaled point.</returns>
    public static GraphPoint operator *(GraphPoint a, double s) => new(a.X * s, a.Y * s);

    /// <summary>Linear interpolation between two points.</summary>
    /// <param name="a">Point at <paramref name="t"/> = 0.</param>
    /// <param name="b">Point at <paramref name="t"/> = 1.</param>
    /// <param name="t">Interpolation factor, typically in [0, 1] but not clamped.</param>
    /// <returns>The interpolated point.</returns>
    public static GraphPoint Lerp(GraphPoint a, GraphPoint b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    /// <summary>Returns "X, Y".</summary>
    /// <returns>"X, Y".</returns>
    public override string ToString() => $"{X}, {Y}";
}
