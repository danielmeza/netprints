using Avalonia.Input;

namespace NetPrints.Editor.Hosting.Automation;

/// <summary>
/// The cursors the editor sets. Named so automation can report which one is active (a
/// <see cref="Cursor"/> instance does not expose its kind).
/// </summary>
public static class EditorCursors
{
    /// <summary>Shown while the canvas is panned (PAR-51).</summary>
    public static readonly Cursor Move = new(StandardCursorType.SizeAll);

    public static string? NameOf(Cursor? cursor) => cursor is null ? null : ReferenceEquals(cursor, Move) ? nameof(StandardCursorType.SizeAll) : "Other";
}
