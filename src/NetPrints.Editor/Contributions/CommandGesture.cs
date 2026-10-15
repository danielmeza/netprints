using System.Globalization;

namespace NetPrints.Editor.Contributions;

/// <summary>A keyboard gesture such as <c>Ctrl+Shift+B</c>, parsed and normalised without any UI toolkit.</summary>
public sealed record CommandGesture
{
    private const int MaxFunctionKey = 24;

    private CommandGesture(CommandModifiers modifiers, string key)
    {
        Modifiers = modifiers;
        Key = key;
    }

    /// <summary>Gets the modifier keys.</summary>
    public CommandModifiers Modifiers { get; }

    /// <summary>Gets the canonical key name: a letter, a digit, <c>F1</c> to <c>F24</c> or a named key such as <c>Delete</c> or <c>Escape</c>.</summary>
    public string Key { get; }

    /// <summary>Gets a value indicating whether the gesture has no modifier.</summary>
    public bool IsSingleKey => Modifiers == CommandModifiers.None;

    /// <summary>Gets a value indicating whether the gesture has no modifier other than Shift.</summary>
    public bool IsPlain => (Modifiers & ~CommandModifiers.Shift) == CommandModifiers.None;

    /// <summary>Gets a value indicating whether the key is a function key, <c>F1</c> to <c>F24</c>.</summary>
    public bool IsFunctionKey => Key.Length > 1 && Key[0] == 'F' && int.TryParse(Key.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number is >= 1 and <= MaxFunctionKey;

    /// <summary>Parses a gesture string; modifiers and key are case-insensitive and their order is free.</summary>
    /// <param name="text">The text, such as <c>shift+ctrl+b</c>.</param>
    /// <param name="gesture">The parsed gesture on success.</param>
    /// <returns><see langword="true"/> when the text has exactly one key and only known modifiers.</returns>
    public static bool TryParse(string? text, out CommandGesture gesture)
    {
        gesture = Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var modifiers = CommandModifiers.None;
        string? key = null;
        foreach (string rawPart in text.Split('+'))
        {
            string part = rawPart.Trim();
            if (part.Length == 0)
            {
                return false;
            }

            if (TryModifier(part, out var modifier))
            {
                if (modifiers.HasFlag(modifier))
                {
                    return false;
                }

                modifiers |= modifier;
            }
            else if (key is null)
            {
                if (!TryKeyName(part, out key))
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        if (key is null)
        {
            return false;
        }

        gesture = new CommandGesture(modifiers, key);
        return true;
    }

    /// <summary>Gets the canonical text: modifiers in the order Ctrl, Alt, Shift, Meta, then the key.</summary>
    /// <returns>The canonical gesture string.</returns>
    public override string ToString()
    {
        var parts = new List<string>(5);
        foreach (var (flag, name) in ModifierNames)
        {
            if (Modifiers.HasFlag(flag))
            {
                parts.Add(name);
            }
        }

        parts.Add(Key);
        return string.Join('+', parts);
    }

    private static readonly Dictionary<string, string> KeyNames = BuildKeyNames();

    private static readonly CommandGesture Empty = new(CommandModifiers.None, string.Empty);

    private static readonly (CommandModifiers Flag, string Name)[] ModifierNames =
    [
        (CommandModifiers.Ctrl, "Ctrl"),
        (CommandModifiers.Alt, "Alt"),
        (CommandModifiers.Shift, "Shift"),
        (CommandModifiers.Meta, "Meta"),
    ];

    private static Dictionary<string, string> BuildKeyNames()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (char c = 'A'; c <= 'Z'; c++)
        {
            names[c.ToString()] = c.ToString();
        }

        for (char c = '0'; c <= '9'; c++)
        {
            names[c.ToString()] = c.ToString();
        }

        for (int number = 1; number <= MaxFunctionKey; number++)
        {
            string name = "F" + number.ToString(CultureInfo.InvariantCulture);
            names[name] = name;
        }

        foreach (string name in new[] { "Escape", "Enter", "Delete", "Insert", "Home", "End", "PageUp", "PageDown", "Left", "Right", "Up", "Down", "Tab", "Space", "Back" })
        {
            names[name] = name;
        }

        names["Esc"] = "Escape";
        names["Return"] = "Enter";
        names["Del"] = "Delete";
        names["Ins"] = "Insert";
        names["PgUp"] = "PageUp";
        names["PgDn"] = "PageDown";
        names["Backspace"] = "Back";
        return names;
    }

    private static bool TryKeyName(string part, out string? key) => KeyNames.TryGetValue(part, out key);

    private static bool TryModifier(string part, out CommandModifiers modifier)
    {
        modifier = part.ToLowerInvariant() switch
        {
            "ctrl" or "control" => CommandModifiers.Ctrl,
            "alt" => CommandModifiers.Alt,
            "shift" => CommandModifiers.Shift,
            "meta" or "cmd" or "command" or "win" => CommandModifiers.Meta,
            _ => CommandModifiers.None,
        };
        return modifier != CommandModifiers.None;
    }
}

/// <summary>The modifier keys of a <see cref="CommandGesture"/>.</summary>
[Flags]
public enum CommandModifiers
{
    /// <summary>No modifier.</summary>
    None = 0,

    /// <summary>The Control key.</summary>
    Ctrl = 1,

    /// <summary>The Alt key.</summary>
    Alt = 2,

    /// <summary>The Shift key.</summary>
    Shift = 4,

    /// <summary>The Meta key (Command on macOS, Windows key elsewhere).</summary>
    Meta = 8,
}
