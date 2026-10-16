using Avalonia.Styling;

namespace NetPrints.Editor.State;

/// <summary>The status bar text that tells which theme a switch applied.</summary>
internal static class ThemeStatusText
{
    private const string Prefix = "Theme: ";

    /// <summary>Describes the applied theme; <see cref="EditorTheme.System"/> also names the variant the operating system resolved to.</summary>
    /// <param name="theme">The theme that was applied.</param>
    /// <param name="actual">The variant the application now shows, or null when it is not known.</param>
    /// <returns>For example <c>Theme: Light</c> or <c>Theme: System (Dark)</c>.</returns>
    public static string For(EditorTheme theme, ThemeVariant? actual)
    {
        if (theme != EditorTheme.System || actual is not { } resolved)
        {
            return Prefix + theme;
        }

        EditorTheme name = resolved == ThemeVariant.Dark ? EditorTheme.Dark : EditorTheme.Light;
        return $"{Prefix}{theme} ({name})";
    }
}
