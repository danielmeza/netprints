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

    /// <summary>Gets the key name, normalised to an initial capital then lower case (<c>B</c>, <c>Delete</c>, <c>F2</c>).</summary>
    public string Key { get; }

    /// <summary>Gets a value indicating whether the gesture has no modifier.</summary>
    public bool IsSingleKey => Modifiers == CommandModifiers.None;

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
                key = char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant();
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

    private static readonly CommandGesture Empty = new(CommandModifiers.None, string.Empty);

    private static readonly (CommandModifiers Flag, string Name)[] ModifierNames =
    [
        (CommandModifiers.Ctrl, "Ctrl"),
        (CommandModifiers.Alt, "Alt"),
        (CommandModifiers.Shift, "Shift"),
        (CommandModifiers.Meta, "Meta"),
    ];

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
