namespace NetPrints.Editor.ViewModels;

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

    /// <summary>Snaps the point down to the grid.</summary>
    public GraphPoint SnapToGrid(double cellSize) => new(X - Mod(X, cellSize), Y - Mod(Y, cellSize));

    private static double Mod(double v, double m) => ((v % m) + m) % m;

    public override string ToString() => $"{X}, {Y}";
}
