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

    /// <summary>
    /// Returns a stable name for one of this class's known cursors, by reference equality: "SizeAll"
    /// for <see cref="Move"/>, "Other" for any other non-null cursor (eg. a control's own hand/text
    /// cursor), or <see langword="null"/> for no cursor (the default arrow).
    /// </summary>
    /// <param name="cursor">Cursor to name.</param>
    /// <returns>The cursor's name, or <see langword="null"/>.</returns>
    public static string? NameOf(Cursor? cursor) => cursor is null ? null : ReferenceEquals(cursor, Move) ? nameof(StandardCursorType.SizeAll) : "Other";
}
