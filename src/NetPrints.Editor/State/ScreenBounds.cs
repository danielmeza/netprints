namespace NetPrints.Editor.State;

/// <summary>A rectangle in screen pixels.</summary>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="Scaling">Device pixels per device-independent pixel of the screen this describes; not saved.</param>
public sealed record ScreenBounds(int X, int Y, int Width, int Height, [property: System.Text.Json.Serialization.JsonIgnore] double Scaling = 1.0)
{
    /// <summary>Gets the right edge, exclusive.</summary>
    public int Right => X + Width;

    /// <summary>Gets the bottom edge, exclusive.</summary>
    public int Bottom => Y + Height;

    /// <summary>Tells whether this rectangle and another overlap by at least one pixel.</summary>
    /// <param name="other">The other rectangle.</param>
    /// <returns><see langword="true"/> when they overlap.</returns>
    public bool Intersects(ScreenBounds other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;
    }
}
