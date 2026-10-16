#nullable enable
using System;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace NetPrints.Projects;

/// <summary>
/// A C# language version (<c>12.0</c>), parsed by NetPrints and not by Roslyn so that a version newer than the editor's
/// compiler is still understood (ADR-0038 decision 1). Versions order by major, then minor.
/// </summary>
/// <param name="Major">The major version, such as 12.</param>
/// <param name="Minor">The minor version, such as 0.</param>
public readonly record struct CSharpLanguageVersion(int Major, int Minor) : IComparable<CSharpLanguageVersion>
{
    private static readonly FrozenSet<string> Keywords =
        new[] { "latest", "latestMajor", "preview", "default" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The newest version NetPrints knows. The keywords <c>latest</c>, <c>preview</c> and <c>default</c> never resolve above
    /// it, and an unknown or newer version resolves to it, so the editor, <c>netprints regen</c> and the SDK target agree
    /// whatever compiler each one runs (ADR-0038). Raised with each Roslyn bump.
    /// </summary>
    public static readonly CSharpLanguageVersion MaxKnown = new(14, 0);

    /// <summary>
    /// Parses a numeric version: <c>major</c> or <c>major.minor</c>, surrounding white space ignored.
    /// </summary>
    /// <param name="text">The text to parse; <see langword="null"/> never parses.</param>
    /// <param name="version">The parsed version, or the default value when parsing fails.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is a numeric version.</returns>
    public static bool TryParse([NotNullWhen(true)] string? text, out CSharpLanguageVersion version)
    {
        version = default;
        if (text is null)
        {
            return false;
        }

        string[] parts = text.Trim().Split('.');
        if (parts.Length > 2 || !TryParseNumber(parts[0], out int major))
        {
            return false;
        }

        int minor = 0;
        if (parts.Length == 2 && !TryParseNumber(parts[1], out minor))
        {
            return false;
        }

        version = new CSharpLanguageVersion(major, minor);
        return true;
    }

    /// <summary>
    /// Whether <paramref name="text"/> is one of the keywords <c>latest</c>, <c>latestMajor</c>, <c>preview</c> or
    /// <c>default</c>, compared without regard to case.
    /// </summary>
    /// <param name="text">The text to test.</param>
    /// <returns><see langword="true"/> for a keyword.</returns>
    public static bool IsKeyword(string? text) => text is not null && Keywords.Contains(text.Trim());

    /// <inheritdoc/>
    public int CompareTo(CSharpLanguageVersion other) => Major != other.Major ? Major.CompareTo(other.Major) : Minor.CompareTo(other.Minor);

    /// <summary>Whether <paramref name="left"/> is older than <paramref name="right"/>.</summary>
    /// <param name="left">The first version.</param>
    /// <param name="right">The second version.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is older.</returns>
    public static bool operator <(CSharpLanguageVersion left, CSharpLanguageVersion right) => left.CompareTo(right) < 0;

    /// <summary>Whether <paramref name="left"/> is newer than <paramref name="right"/>.</summary>
    /// <param name="left">The first version.</param>
    /// <param name="right">The second version.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is newer.</returns>
    public static bool operator >(CSharpLanguageVersion left, CSharpLanguageVersion right) => left.CompareTo(right) > 0;

    /// <summary>Whether <paramref name="left"/> is not newer than <paramref name="right"/>.</summary>
    /// <param name="left">The first version.</param>
    /// <param name="right">The second version.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is the same or older.</returns>
    public static bool operator <=(CSharpLanguageVersion left, CSharpLanguageVersion right) => left.CompareTo(right) <= 0;

    /// <summary>Whether <paramref name="left"/> is not older than <paramref name="right"/>.</summary>
    /// <param name="left">The first version.</param>
    /// <param name="right">The second version.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is the same or newer.</returns>
    public static bool operator >=(CSharpLanguageVersion left, CSharpLanguageVersion right) => left.CompareTo(right) >= 0;

    /// <summary>The version as <c>major.minor</c>, culture independent.</summary>
    /// <returns>The text, such as <c>12.0</c>.</returns>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}");

    private static bool TryParseNumber(string text, out int number) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);
}
