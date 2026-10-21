using System.Globalization;

namespace NetPrints.Editor.Settings;

/// <summary>The values of the language drop-down: the system language and the cultures the editor ships resources for (ADR-0027: only the neutral English resources so far).</summary>
internal static class LanguageChoices
{
    /// <summary>Gets the values, the system language (empty) first.</summary>
    public static IReadOnlyList<string> Values { get; } = ["", "en"];

    /// <summary>Gets the text the drop-down shows for a value.</summary>
    /// <param name="value">A value of <see cref="Values"/>.</param>
    /// <returns>"System" for the empty value, otherwise the culture's native name.</returns>
    public static string LabelOf(string value) =>
        value.Length == 0 ? Strings.Settings_Language_System : CultureInfo.GetCultureInfo(value).NativeName;
}
