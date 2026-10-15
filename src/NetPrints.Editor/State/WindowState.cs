namespace NetPrints.Editor.State;

/// <summary>The content of <c>window.json</c>: the main window's normal bounds, in pixels, and whether it is maximized.</summary>
/// <param name="SchemaVersion">The schema version.</param>
/// <param name="X">Left edge of the normal bounds.</param>
/// <param name="Y">Top edge of the normal bounds.</param>
/// <param name="Width">Width of the normal bounds.</param>
/// <param name="Height">Height of the normal bounds.</param>
/// <param name="IsMaximized">Whether the window was maximized.</param>
/// <param name="Screen">The working area of the screen the window was on, when known.</param>
public sealed record WindowState(int SchemaVersion, int X, int Y, int Width, int Height, bool IsMaximized, ScreenBounds? Screen = null) : IStateFile
{
    /// <summary>Gets the normal bounds.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public ScreenBounds Bounds => new(X, Y, Width, Height);
}
