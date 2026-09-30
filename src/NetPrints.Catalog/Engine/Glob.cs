using System.Collections.Generic;

namespace NetPrints.Catalog;

/// <summary>
/// The glob dialect of catalog profiles: <c>*</c> matches any run of characters (dots included, so
/// <c>Ns.*</c> covers nested namespaces and nested types), <c>?</c> matches exactly one character, everything
/// else is a literal. Matching is ordinal and case-sensitive.
/// </summary>
public static class Glob
{
    private const char AnyRun = '*';

    private const char AnyOne = '?';

    /// <summary>Tests one name against one pattern.</summary>
    /// <param name="pattern">The glob pattern.</param>
    /// <param name="text">The name to test.</param>
    /// <returns>True when the whole <paramref name="text"/> matches.</returns>
    public static bool IsMatch(string pattern, string text)
    {
        Guard.NotNull(pattern, nameof(pattern));
        Guard.NotNull(text, nameof(text));

        int patternIndex = 0;
        int textIndex = 0;
        int starIndex = -1;
        int resumeIndex = 0;

        while (textIndex < text.Length)
        {
            if (patternIndex < pattern.Length && pattern[patternIndex] == AnyRun)
            {
                starIndex = patternIndex++;
                resumeIndex = textIndex;
            }
            else if (patternIndex < pattern.Length && (pattern[patternIndex] == AnyOne || pattern[patternIndex] == text[textIndex]))
            {
                patternIndex++;
                textIndex++;
            }
            else if (starIndex >= 0)
            {
                patternIndex = starIndex + 1;
                textIndex = ++resumeIndex;
            }
            else
            {
                return false;
            }
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == AnyRun)
        {
            patternIndex++;
        }

        return patternIndex == pattern.Length;
    }

    /// <summary>Tests one name against several patterns.</summary>
    /// <param name="patterns">The patterns; null or empty matches nothing.</param>
    /// <param name="text">The name to test.</param>
    /// <returns>True when at least one pattern matches.</returns>
    public static bool AnyMatch(IReadOnlyList<string>? patterns, string text)
    {
        if (patterns is null)
        {
            return false;
        }

        foreach (string pattern in patterns)
        {
            if (IsMatch(pattern, text))
            {
                return true;
            }
        }

        return false;
    }
}
