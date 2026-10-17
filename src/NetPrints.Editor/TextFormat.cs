using System.Globalization;

namespace NetPrints.Editor;

/// <summary>Fills the placeholders of a text from the editor's resources (FR-027).</summary>
internal static class TextFormat
{
    /// <summary>Formats a resource text for the current culture.</summary>
    /// <param name="format">The text with <c>{0}</c>-style placeholders, usually a <see cref="Strings"/> property.</param>
    /// <param name="arguments">The values the placeholders stand for.</param>
    /// <returns>The text with the values in place.</returns>
    public static string Of(string format, params object?[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
