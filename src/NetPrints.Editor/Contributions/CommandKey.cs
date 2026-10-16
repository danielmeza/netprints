using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using NetPrints.SmartEnums;

namespace NetPrints.Editor.Contributions;

/// <summary>
/// A key a <see cref="CommandGesture"/> can use: a letter, a digit, <c>F1</c> to <c>F24</c> or a named key (ADR-0034). The members
/// are singletons that override what differs; <see cref="CommandKeyId"/> and <see cref="All"/> are generated from the member fields,
/// so a member is a field, a nested class where it differs, and nothing else. Texts parse and format without allocating.
/// </summary>
[SmartEnum]
public abstract partial class CommandKey : ISpanParsable<CommandKey>, ISpanFormattable
{
    /// <summary>The A key.</summary>
    public static readonly CommandKey A = new LetterKey(CommandKeyId.A);

    /// <summary>The B key.</summary>
    public static readonly CommandKey B = new LetterKey(CommandKeyId.B);

    /// <summary>The C key.</summary>
    public static readonly CommandKey C = new LetterKey(CommandKeyId.C);

    /// <summary>The D key.</summary>
    public static readonly CommandKey D = new LetterKey(CommandKeyId.D);

    /// <summary>The E key.</summary>
    public static readonly CommandKey E = new LetterKey(CommandKeyId.E);

    /// <summary>The F key.</summary>
    public static readonly CommandKey F = new LetterKey(CommandKeyId.F);

    /// <summary>The G key.</summary>
    public static readonly CommandKey G = new LetterKey(CommandKeyId.G);

    /// <summary>The H key.</summary>
    public static readonly CommandKey H = new LetterKey(CommandKeyId.H);

    /// <summary>The I key.</summary>
    public static readonly CommandKey I = new LetterKey(CommandKeyId.I);

    /// <summary>The J key.</summary>
    public static readonly CommandKey J = new LetterKey(CommandKeyId.J);

    /// <summary>The K key.</summary>
    public static readonly CommandKey K = new LetterKey(CommandKeyId.K);

    /// <summary>The L key.</summary>
    public static readonly CommandKey L = new LetterKey(CommandKeyId.L);

    /// <summary>The M key.</summary>
    public static readonly CommandKey M = new LetterKey(CommandKeyId.M);

    /// <summary>The N key.</summary>
    public static readonly CommandKey N = new LetterKey(CommandKeyId.N);

    /// <summary>The O key.</summary>
    public static readonly CommandKey O = new LetterKey(CommandKeyId.O);

    /// <summary>The P key.</summary>
    public static readonly CommandKey P = new LetterKey(CommandKeyId.P);

    /// <summary>The Q key.</summary>
    public static readonly CommandKey Q = new LetterKey(CommandKeyId.Q);

    /// <summary>The R key.</summary>
    public static readonly CommandKey R = new LetterKey(CommandKeyId.R);

    /// <summary>The S key.</summary>
    public static readonly CommandKey S = new LetterKey(CommandKeyId.S);

    /// <summary>The T key.</summary>
    public static readonly CommandKey T = new LetterKey(CommandKeyId.T);

    /// <summary>The U key.</summary>
    public static readonly CommandKey U = new LetterKey(CommandKeyId.U);

    /// <summary>The V key.</summary>
    public static readonly CommandKey V = new LetterKey(CommandKeyId.V);

    /// <summary>The W key.</summary>
    public static readonly CommandKey W = new LetterKey(CommandKeyId.W);

    /// <summary>The X key.</summary>
    public static readonly CommandKey X = new LetterKey(CommandKeyId.X);

    /// <summary>The Y key.</summary>
    public static readonly CommandKey Y = new LetterKey(CommandKeyId.Y);

    /// <summary>The Z key.</summary>
    public static readonly CommandKey Z = new LetterKey(CommandKeyId.Z);

    /// <summary>The 0 key of the top row.</summary>
    public static readonly CommandKey D0 = new DigitKey(CommandKeyId.D0);

    /// <summary>The 1 key of the top row.</summary>
    public static readonly CommandKey D1 = new DigitKey(CommandKeyId.D1);

    /// <summary>The 2 key of the top row.</summary>
    public static readonly CommandKey D2 = new DigitKey(CommandKeyId.D2);

    /// <summary>The 3 key of the top row.</summary>
    public static readonly CommandKey D3 = new DigitKey(CommandKeyId.D3);

    /// <summary>The 4 key of the top row.</summary>
    public static readonly CommandKey D4 = new DigitKey(CommandKeyId.D4);

    /// <summary>The 5 key of the top row.</summary>
    public static readonly CommandKey D5 = new DigitKey(CommandKeyId.D5);

    /// <summary>The 6 key of the top row.</summary>
    public static readonly CommandKey D6 = new DigitKey(CommandKeyId.D6);

    /// <summary>The 7 key of the top row.</summary>
    public static readonly CommandKey D7 = new DigitKey(CommandKeyId.D7);

    /// <summary>The 8 key of the top row.</summary>
    public static readonly CommandKey D8 = new DigitKey(CommandKeyId.D8);

    /// <summary>The 9 key of the top row.</summary>
    public static readonly CommandKey D9 = new DigitKey(CommandKeyId.D9);

    /// <summary>The F1 function key.</summary>
    public static readonly CommandKey F1 = new FunctionKey(CommandKeyId.F1);

    /// <summary>The F2 function key.</summary>
    public static readonly CommandKey F2 = new FunctionKey(CommandKeyId.F2);

    /// <summary>The F3 function key.</summary>
    public static readonly CommandKey F3 = new FunctionKey(CommandKeyId.F3);

    /// <summary>The F4 function key.</summary>
    public static readonly CommandKey F4 = new FunctionKey(CommandKeyId.F4);

    /// <summary>The F5 function key.</summary>
    public static readonly CommandKey F5 = new FunctionKey(CommandKeyId.F5);

    /// <summary>The F6 function key.</summary>
    public static readonly CommandKey F6 = new FunctionKey(CommandKeyId.F6);

    /// <summary>The F7 function key.</summary>
    public static readonly CommandKey F7 = new FunctionKey(CommandKeyId.F7);

    /// <summary>The F8 function key.</summary>
    public static readonly CommandKey F8 = new FunctionKey(CommandKeyId.F8);

    /// <summary>The F9 function key.</summary>
    public static readonly CommandKey F9 = new FunctionKey(CommandKeyId.F9);

    /// <summary>The F10 function key.</summary>
    public static readonly CommandKey F10 = new FunctionKey(CommandKeyId.F10);

    /// <summary>The F11 function key.</summary>
    public static readonly CommandKey F11 = new FunctionKey(CommandKeyId.F11);

    /// <summary>The F12 function key.</summary>
    public static readonly CommandKey F12 = new FunctionKey(CommandKeyId.F12);

    /// <summary>The F13 function key.</summary>
    public static readonly CommandKey F13 = new FunctionKey(CommandKeyId.F13);

    /// <summary>The F14 function key.</summary>
    public static readonly CommandKey F14 = new FunctionKey(CommandKeyId.F14);

    /// <summary>The F15 function key.</summary>
    public static readonly CommandKey F15 = new FunctionKey(CommandKeyId.F15);

    /// <summary>The F16 function key.</summary>
    public static readonly CommandKey F16 = new FunctionKey(CommandKeyId.F16);

    /// <summary>The F17 function key.</summary>
    public static readonly CommandKey F17 = new FunctionKey(CommandKeyId.F17);

    /// <summary>The F18 function key.</summary>
    public static readonly CommandKey F18 = new FunctionKey(CommandKeyId.F18);

    /// <summary>The F19 function key.</summary>
    public static readonly CommandKey F19 = new FunctionKey(CommandKeyId.F19);

    /// <summary>The F20 function key.</summary>
    public static readonly CommandKey F20 = new FunctionKey(CommandKeyId.F20);

    /// <summary>The F21 function key.</summary>
    public static readonly CommandKey F21 = new FunctionKey(CommandKeyId.F21);

    /// <summary>The F22 function key.</summary>
    public static readonly CommandKey F22 = new FunctionKey(CommandKeyId.F22);

    /// <summary>The F23 function key.</summary>
    public static readonly CommandKey F23 = new FunctionKey(CommandKeyId.F23);

    /// <summary>The F24 function key.</summary>
    public static readonly CommandKey F24 = new FunctionKey(CommandKeyId.F24);

    /// <summary>The Escape key, also written <c>Esc</c>.</summary>
    public static readonly CommandKey Escape = new EscapeKey();

    /// <summary>The Enter key, also written <c>Return</c>.</summary>
    public static readonly CommandKey Enter = new EnterKey();

    /// <summary>The Delete key, also written <c>Del</c>.</summary>
    public static readonly CommandKey Delete = new DeleteKey();

    /// <summary>The Insert key, also written <c>Ins</c>.</summary>
    public static readonly CommandKey Insert = new InsertKey();

    /// <summary>The Home key.</summary>
    public static readonly CommandKey Home = new NamedKey(CommandKeyId.Home);

    /// <summary>The End key.</summary>
    public static readonly CommandKey End = new NamedKey(CommandKeyId.End);

    /// <summary>The Page Up key, also written <c>PgUp</c>.</summary>
    public static readonly CommandKey PageUp = new PageUpKey();

    /// <summary>The Page Down key, also written <c>PgDn</c>.</summary>
    public static readonly CommandKey PageDown = new PageDownKey();

    /// <summary>The Left arrow key.</summary>
    public static readonly CommandKey Left = new NamedKey(CommandKeyId.Left);

    /// <summary>The Right arrow key.</summary>
    public static readonly CommandKey Right = new NamedKey(CommandKeyId.Right);

    /// <summary>The Up arrow key.</summary>
    public static readonly CommandKey Up = new NamedKey(CommandKeyId.Up);

    /// <summary>The Down arrow key.</summary>
    public static readonly CommandKey Down = new NamedKey(CommandKeyId.Down);

    /// <summary>The Tab key.</summary>
    public static readonly CommandKey Tab = new NamedKey(CommandKeyId.Tab);

    /// <summary>The Space bar.</summary>
    public static readonly CommandKey Space = new NamedKey(CommandKeyId.Space);

    /// <summary>The Backspace key, written <c>Back</c> and also <c>Backspace</c>.</summary>
    public static readonly CommandKey Back = new BackKey();

    private static readonly FrozenDictionary<string, CommandKey> ByText = BuildLookup();

    private static readonly FrozenDictionary<string, CommandKey>.AlternateLookup<ReadOnlySpan<char>> BySpan = ByText.GetAlternateLookup<ReadOnlySpan<char>>();

    private CommandKey()
    {
    }

    /// <summary>Gets the id of this member in the generated <see cref="CommandKeyId"/> enum.</summary>
    public abstract CommandKeyId Id { get; }

    /// <summary>Gets the canonical text of the key, as <see cref="CommandGesture"/> writes it: <c>A</c>, <c>3</c>, <c>F5</c>, <c>PageDown</c>.</summary>
    public virtual string Text => Id.ToString();

    /// <summary>Gets the short label shown in menus and the shortcut help.</summary>
    public virtual string Glyph => Text;

    /// <summary>Gets a value indicating whether the key is one of <c>F1</c> to <c>F24</c>.</summary>
    public virtual bool IsFunctionKey => false;

    /// <summary>Gets the other texts that parse to this key, besides <see cref="Text"/>.</summary>
    public virtual IReadOnlyList<string> Aliases => [];

    /// <summary>Converts an id to its member.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The member.</returns>
    public static implicit operator CommandKey(CommandKeyId id) => FromId(id);

    /// <summary>Gets the member of an id.</summary>
    /// <param name="id">A defined id.</param>
    /// <returns>The member.</returns>
    public static CommandKey FromId(CommandKeyId id) => All[(int)id];

    /// <summary>Parses a key text, case-insensitive; aliases such as <c>Esc</c> are accepted.</summary>
    /// <param name="s">The text.</param>
    /// <param name="provider">Ignored.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="s"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The text names no key.</exception>
    public static CommandKey Parse(string s, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan(), provider);
    }

    /// <summary>Parses a key text, case-insensitive; aliases such as <c>Esc</c> are accepted.</summary>
    /// <param name="s">The text.</param>
    /// <param name="provider">Ignored.</param>
    /// <returns>The key.</returns>
    /// <exception cref="FormatException">The text names no key.</exception>
    public static CommandKey Parse(ReadOnlySpan<char> s, IFormatProvider? provider) =>
        TryParse(s, provider, out CommandKey? key) ? key : throw new FormatException($"'{s}' is not a key name.");

    /// <summary>Tries to parse a key text, case-insensitive; aliases such as <c>Esc</c> are accepted.</summary>
    /// <param name="s">The text, or <see langword="null"/>.</param>
    /// <param name="provider">Ignored.</param>
    /// <param name="result">The key on success.</param>
    /// <returns><see langword="true"/> when the text names a key.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out CommandKey result)
    {
        result = null;
        return s is not null && TryParse(s.AsSpan(), provider, out result);
    }

    /// <summary>Tries to parse a key text without allocating, case-insensitive; aliases such as <c>Esc</c> are accepted.</summary>
    /// <param name="s">The text.</param>
    /// <param name="provider">Ignored.</param>
    /// <param name="result">The key on success.</param>
    /// <returns><see langword="true"/> when the text names a key.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, [MaybeNullWhen(false)] out CommandKey result) =>
        BySpan.TryGetValue(s.Trim(), out result);

    /// <summary>Gets the canonical text.</summary>
    /// <returns><see cref="Text"/>.</returns>
    public override string ToString() => Text;

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider) => Text;

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        bool fits = Text.TryCopyTo(destination);
        charsWritten = fits ? Text.Length : 0;
        return fits;
    }

    private static FrozenDictionary<string, CommandKey> BuildLookup()
    {
        Dictionary<string, CommandKey> byText = new(StringComparer.OrdinalIgnoreCase);
        foreach (CommandKey key in All)
        {
            byText.Add(key.Text, key);
            foreach (string alias in key.Aliases)
            {
                byText.Add(alias, key);
            }
        }

        return byText.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class LetterKey(CommandKeyId id) : CommandKey
    {
        public override CommandKeyId Id => id;
    }

    private sealed class DigitKey(CommandKeyId id) : CommandKey
    {
        private readonly string text = id.ToString()[1..];

        public override CommandKeyId Id => id;

        public override string Text => text;
    }

    private sealed class FunctionKey(CommandKeyId id) : CommandKey
    {
        public override CommandKeyId Id => id;

        public override bool IsFunctionKey => true;
    }

    private sealed class NamedKey(CommandKeyId id) : CommandKey
    {
        public override CommandKeyId Id => id;
    }

    private sealed class EscapeKey : CommandKey
    {
        public override CommandKeyId Id => CommandKeyId.Escape;

        public override string Glyph => "Esc";

        public override IReadOnlyList<string> Aliases { get; } = ["Esc"];
    }

    private sealed class EnterKey : CommandKey
    {
        public override CommandKeyId Id => CommandKeyId.Enter;

        public override IReadOnlyList<string> Aliases { get; } = ["Return"];
    }

    private sealed class DeleteKey : CommandKey
    {
        public override CommandKeyId Id => CommandKeyId.Delete;

        public override IReadOnlyList<string> Aliases { get; } = ["Del"];
    }

    private sealed class InsertKey : CommandKey
    {
        public override CommandKeyId Id => CommandKeyId.Insert;

        public override IReadOnlyList<string> Aliases { get; } = ["Ins"];
    }

    private sealed class PageUpKey : CommandKey
    {
        public override CommandKeyId Id => CommandKeyId.PageUp;

        public override string Glyph => "PgUp";

        public override IReadOnlyList<string> Aliases { get; } = ["PgUp"];
    }

    private sealed class PageDownKey : CommandKey
    {
        public override CommandKeyId Id => CommandKeyId.PageDown;

        public override string Glyph => "PgDn";

        public override IReadOnlyList<string> Aliases { get; } = ["PgDn"];
    }

    private sealed class BackKey : CommandKey
    {
        public override CommandKeyId Id => CommandKeyId.Back;

        public override IReadOnlyList<string> Aliases { get; } = ["Backspace"];
    }
}
