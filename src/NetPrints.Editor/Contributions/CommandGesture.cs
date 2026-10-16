using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace NetPrints.Editor.Contributions;

/// <summary>
/// A keyboard gesture such as <c>Ctrl+Shift+B</c>, free of any UI toolkit. Text is parsed and formatted only at the boundaries
/// (key bindings, manifests, display); everywhere else the gesture is a typed value.
/// </summary>
/// <param name="Modifiers">The modifier keys.</param>
/// <param name="Key">The key.</param>
public sealed record CommandGesture(CommandModifiers Modifiers, CommandKey Key) : ISpanParsable<CommandGesture>, ISpanFormattable
{
    private const char Separator = '+';

    private static readonly (CommandModifiers Flag, string Name)[] ModifierNames =
    [
        (CommandModifiers.Ctrl, "Ctrl"),
        (CommandModifiers.Alt, "Alt"),
        (CommandModifiers.Shift, "Shift"),
        (CommandModifiers.Meta, "Meta"),
    ];

    private static readonly FrozenDictionary<string, CommandModifiers> ModifierByText = new Dictionary<string, CommandModifiers>(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"] = CommandModifiers.Ctrl,
        ["control"] = CommandModifiers.Ctrl,
        ["alt"] = CommandModifiers.Alt,
        ["shift"] = CommandModifiers.Shift,
        ["meta"] = CommandModifiers.Meta,
        ["cmd"] = CommandModifiers.Meta,
        ["command"] = CommandModifiers.Meta,
        ["win"] = CommandModifiers.Meta,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, CommandModifiers>.AlternateLookup<ReadOnlySpan<char>> ModifierBySpan = ModifierByText.GetAlternateLookup<ReadOnlySpan<char>>();

    /// <summary>Gets a value indicating whether the gesture has no modifier.</summary>
    public bool IsSingleKey => Modifiers == CommandModifiers.None;

    /// <summary>Gets a value indicating whether the gesture has no modifier other than Shift.</summary>
    public bool IsPlain => (Modifiers & ~CommandModifiers.Shift) == CommandModifiers.None;

    /// <summary>Gets a value indicating whether the key is a function key, <c>F1</c> to <c>F24</c>.</summary>
    public bool IsFunctionKey => Key.IsFunctionKey;

    /// <summary>Gets the text menus and shortcut lists show: the canonical text with the short <see cref="CommandKey.Glyph"/> of the key, such as <c>Esc</c>.</summary>
    public string Display => Key.Glyph == Key.Text ? ToString() : ToString()[..^Key.Text.Length] + Key.Glyph;

    /// <summary>Parses a gesture string; modifiers and key are case-insensitive and their order is free.</summary>
    /// <param name="s">The text, such as <c>shift+ctrl+b</c>.</param>
    /// <param name="provider">Ignored.</param>
    /// <returns>The gesture.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="s"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The text is not exactly one key and known modifiers.</exception>
    public static CommandGesture Parse(string s, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan(), provider);
    }

    /// <summary>Parses a gesture string; modifiers and key are case-insensitive and their order is free.</summary>
    /// <param name="s">The text, such as <c>shift+ctrl+b</c>.</param>
    /// <param name="provider">Ignored.</param>
    /// <returns>The gesture.</returns>
    /// <exception cref="FormatException">The text is not exactly one key and known modifiers.</exception>
    public static CommandGesture Parse(ReadOnlySpan<char> s, IFormatProvider? provider) =>
        TryParse(s, provider, out CommandGesture? gesture) ? gesture : throw new FormatException($"'{s}' is not a gesture.");

    /// <summary>Tries to parse a gesture string.</summary>
    /// <param name="s">The text, or <see langword="null"/>.</param>
    /// <param name="provider">Ignored.</param>
    /// <param name="result">The gesture on success.</param>
    /// <returns><see langword="true"/> when the text has exactly one key and only known modifiers.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out CommandGesture result)
    {
        result = null;
        return s is not null && TryParse(s.AsSpan(), provider, out result);
    }

    /// <summary>Tries to parse a gesture string.</summary>
    /// <param name="text">The text, or <see langword="null"/>.</param>
    /// <param name="gesture">The gesture on success.</param>
    /// <returns><see langword="true"/> when the text has exactly one key and only known modifiers.</returns>
    public static bool TryParse([NotNullWhen(true)] string? text, [MaybeNullWhen(false)] out CommandGesture gesture) => TryParse(text, null, out gesture);

    /// <summary>Tries to parse a gesture string without allocating beyond the gesture itself.</summary>
    /// <param name="s">The text.</param>
    /// <param name="provider">Ignored.</param>
    /// <param name="result">The gesture on success.</param>
    /// <returns><see langword="true"/> when the text has exactly one key and only known modifiers.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, [MaybeNullWhen(false)] out CommandGesture result)
    {
        result = null;
        CommandModifiers modifiers = CommandModifiers.None;
        CommandKey? key = null;
        ReadOnlySpan<char> rest = s;
        while (true)
        {
            int separator = rest.IndexOf(Separator);
            ReadOnlySpan<char> part = (separator < 0 ? rest : rest[..separator]).Trim();
            if (part.IsEmpty)
            {
                return false;
            }

            if (ModifierBySpan.TryGetValue(part, out CommandModifiers modifier))
            {
                if ((modifiers & modifier) != CommandModifiers.None)
                {
                    return false;
                }

                modifiers |= modifier;
            }
            else if (key is null && CommandKey.TryParse(part, provider, out CommandKey? parsed))
            {
                key = parsed;
            }
            else
            {
                return false;
            }

            if (separator < 0)
            {
                break;
            }

            rest = rest[(separator + 1)..];
        }

        if (key is null)
        {
            return false;
        }

        result = new CommandGesture(modifiers, key);
        return true;
    }

    /// <summary>Gets the canonical text: modifiers in the order Ctrl, Alt, Shift, Meta, then the key.</summary>
    /// <returns>The canonical gesture string.</returns>
    public override string ToString()
    {
        Span<char> buffer = stackalloc char[MaxLength];
        return TryFormat(buffer, out int written, default, null) ? new string(buffer[..written]) : string.Empty;
    }

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider) => ToString();

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        int written = 0;
        foreach ((CommandModifiers flag, string name) in ModifierNames)
        {
            if (Modifiers.HasFlag(flag) && !(Append(destination, ref written, name) && Append(destination, ref written, Separator)))
            {
                charsWritten = 0;
                return false;
            }
        }

        bool fits = Append(destination, ref written, Key.Text);
        charsWritten = fits ? written : 0;
        return fits;
    }

    private static bool Append(Span<char> destination, ref int written, ReadOnlySpan<char> text)
    {
        if (!text.TryCopyTo(destination[written..]))
        {
            return false;
        }

        written += text.Length;
        return true;
    }

    private static bool Append(Span<char> destination, ref int written, char character)
    {
        if (written >= destination.Length)
        {
            return false;
        }

        destination[written++] = character;
        return true;
    }

    private const int MaxLength = 32;
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
