namespace NetPrints.Editor.Graph;

/// <summary>
/// A point in graph (canvas) coordinates. Replaces UI-toolkit point types in view models.
/// </summary>
public readonly record struct GraphPoint(double X, double Y)
{
    public static readonly GraphPoint Zero = new(0, 0);

    public static GraphPoint operator +(GraphPoint a, GraphPoint b) => new(a.X + b.X, a.Y + b.Y);

    public static GraphPoint operator -(GraphPoint a, GraphPoint b) => new(a.X - b.X, a.Y - b.Y);

    public static GraphPoint operator *(GraphPoint a, double s) => new(a.X * s, a.Y * s);

    /// <summary>Linear interpolation between two points.</summary>
    public static GraphPoint Lerp(GraphPoint a, GraphPoint b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);


    public override string ToString() => $"{X}, {Y}";
}
