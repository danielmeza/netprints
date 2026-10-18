using System;
using System.Collections.Generic;

namespace NetPrints.Authoring.Contributions;

/// <summary>The gesture grammar of <c>CommandGesture.Parse</c>, for the analyzers that cannot reference the extension API.</summary>
internal static class GestureText
{
    private const char Separator = '+';

    private const int FirstFunctionKey = 1;

    private const int LastFunctionKey = 24;

    private const int ShiftFlag = 4;

    private static readonly Dictionary<string, int> Modifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"] = 1,
        ["control"] = 1,
        ["alt"] = 2,
        ["shift"] = ShiftFlag,
        ["meta"] = 8,
        ["cmd"] = 8,
        ["command"] = 8,
        ["win"] = 8,
    };

    private static readonly HashSet<string> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Escape", "Esc", "Enter", "Return", "Delete", "Del", "Insert", "Ins", "Home", "End", "PageUp", "PgUp", "PageDown", "PgDn",
        "Left", "Right", "Up", "Down", "Tab", "Space", "Back", "Backspace", ",", "Comma", "OemComma",
    };

    /// <summary>Parses a gesture such as <c>Ctrl+Shift+B</c>.</summary>
    /// <param name="text">The gesture text.</param>
    /// <param name="modifiers">The modifier flags.</param>
    /// <param name="key">The key name as written.</param>
    /// <returns><see langword="true"/> when the text is a gesture.</returns>
    public static bool TryParse(string text, out int modifiers, out string key)
    {
        modifiers = 0;
        key = string.Empty;
        string? found = null;
        foreach (string raw in text.Split(Separator))
        {
            string part = raw.Trim();
            if (part.Length == 0)
            {
                return false;
            }

            if (Modifiers.TryGetValue(part, out int flag))
            {
                if ((modifiers & flag) != 0)
                {
                    return false;
                }

                modifiers |= flag;
            }
            else if (found is null && IsKey(part))
            {
                found = part;
            }
            else
            {
                return false;
            }
        }

        if (found is null)
        {
            return false;
        }

        key = found;
        return true;
    }

    /// <summary>Whether a gesture has no modifier other than Shift.</summary>
    /// <param name="modifiers">The modifier flags.</param>
    /// <returns><see langword="true"/> for a single-key or Shift-only gesture.</returns>
    public static bool IsPlain(int modifiers) => (modifiers & ~ShiftFlag) == 0;

    /// <summary>Whether a key name is one of F1 to F24.</summary>
    /// <param name="key">The key name.</param>
    /// <returns><see langword="true"/> for a function key.</returns>
    public static bool IsFunctionKey(string key) =>
        key.Length > 1
        && (key[0] == 'F' || key[0] == 'f')
        && int.TryParse(key.Substring(1), out int number)
        && number >= FirstFunctionKey
        && number <= LastFunctionKey;

    private static bool IsAsciiLetterOrDigit(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');

    private static bool IsKey(string part) =>
        (part.Length == 1 && IsAsciiLetterOrDigit(part[0]))
        || IsFunctionKey(part)
        || NamedKeys.Contains(part);
}
