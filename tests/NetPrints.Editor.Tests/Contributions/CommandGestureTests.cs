using NetPrints.Editor.Contributions;

namespace NetPrints.Editor.Tests.Contributions;

/// <summary>The UI-free gesture parser and normaliser behind the registry's conflict check.</summary>
public class CommandGestureTests
{
    [Theory]
    [InlineData("Ctrl+Shift+B", "Ctrl+Shift+B")]
    [InlineData("Shift+Ctrl+B", "Ctrl+Shift+B")]
    [InlineData("ctrl+shift+b", "Ctrl+Shift+B")]
    [InlineData("Control+Alt+Delete", "Ctrl+Alt+Delete")]
    [InlineData("Cmd+S", "Meta+S")]
    [InlineData("F2", "F2")]
    [InlineData("delete", "Delete")]
    [InlineData(" Ctrl + K ", "Ctrl+K")]
    [InlineData("Esc", "Escape")]
    [InlineData("escape", "Escape")]
    [InlineData("Del", "Delete")]
    [InlineData("Ins", "Insert")]
    [InlineData("Return", "Enter")]
    [InlineData("PgUp", "PageUp")]
    [InlineData("PgDn", "PageDown")]
    [InlineData("Backspace", "Back")]
    [InlineData("3", "3")]
    [InlineData("f24", "F24")]
    public void TextNormalisesToTheCanonicalForm(string text, string canonical)
    {
        Assert.True(CommandGesture.TryParse(text, out var gesture));

        Assert.Equal(canonical, gesture.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl")]
    [InlineData("Ctrl+")]
    [InlineData("+K")]
    [InlineData("Ctrl+Ctrl+K")]
    [InlineData("Ctrl+K+L")]
    [InlineData("Ctrl++K")]
    [InlineData("Ctrl+Banana")]
    [InlineData("Ctrl+12")]
    [InlineData("F25")]
    [InlineData("F0")]
    [InlineData("Fx")]
    [InlineData("Ctrl+Fx")]
    public void AnInvalidTextIsRejected(string text) => Assert.False(CommandGesture.TryParse(text, out _));

    [Fact]
    public void ANullTextIsRejected() => Assert.False(CommandGesture.TryParse(null, out _));

    [Fact]
    public void ASingleKeyHasNoModifier()
    {
        Assert.True(CommandGesture.TryParse("F", out var single));
        Assert.True(CommandGesture.TryParse("Ctrl+F", out var chord));

        Assert.True(single.IsSingleKey);
        Assert.False(chord.IsSingleKey);
        Assert.Equal(CommandModifiers.Ctrl, chord.Modifiers);
        Assert.Same(CommandKey.F, chord.Key);
    }

    [Fact]
    public void EveryKeyWithEveryModifierSetRoundTripsThroughSpansAndFormatting()
    {
        char[] buffer = new char[64];

        foreach (CommandKey key in CommandKey.All)
        {
            for (int bits = 0; bits < 16; bits++)
            {
                CommandGesture gesture = new((CommandModifiers)bits, key);
                string text = gesture.ToString();

                Assert.True(CommandGesture.TryParse(text.AsSpan(), null, out CommandGesture? fromSpan), text);
                Assert.Equal(gesture, fromSpan);
                Assert.Equal(gesture, CommandGesture.Parse(text, null));
                Assert.True(((ISpanFormattable)gesture).TryFormat(buffer, out int written, default, null), text);
                Assert.Equal(text, new string(buffer, 0, written));
            }
        }
    }

    [Theory]
    [InlineData("Ctrl+Esc", "Ctrl+Esc")]
    [InlineData("shift+pgdn", "Shift+PgDn")]
    [InlineData("Ctrl+K", "Ctrl+K")]
    public void TheDisplayTextUsesTheShortGlyphOfTheKey(string text, string display)
    {
        Assert.Equal(display, CommandGesture.Parse(text, null).Display);
    }

    [Fact]
    public void FormattingNeedsRoomForTheWholeGesture()
    {
        CommandGesture gesture = new(CommandModifiers.Ctrl | CommandModifiers.Shift, CommandKey.B);
        Span<char> buffer = stackalloc char[11];

        Assert.False(gesture.TryFormat(buffer, out int written, default, null));
        Assert.Equal(0, written);
        Assert.True(gesture.TryFormat(buffer = stackalloc char[12], out written, default, null));
        Assert.Equal("Ctrl+Shift+B", buffer[..written].ToString());
    }

    [Fact]
    public void TheParsableInterfaceRejectsWhatTheTextParserRejects()
    {
        Assert.False(CommandGesture.TryParse("Ctrl+Banana".AsSpan(), null, out _));
        Assert.False(CommandGesture.TryParse((string?)null, null, out _));
        Assert.Throws<FormatException>(() => CommandGesture.Parse("Ctrl+K+L", null));
    }

    [Fact]
    public void ParsingASpanAllocatesOnlyTheGesture()
    {
        ReadOnlySpan<char> text = "Ctrl+Shift+PageDown";
        CommandGesture.TryParse(text, null, out _);

        long before = GC.GetAllocatedBytesForCurrentThread();
        bool parsed = CommandGesture.TryParse(text, null, out CommandGesture? gesture);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(parsed);
        Assert.Equal(new CommandGesture(CommandModifiers.Ctrl | CommandModifiers.Shift, CommandKey.PageDown), gesture);
        Assert.InRange(allocated, 0, 48);
    }

    [Theory]
    [InlineData("F1", true)]
    [InlineData("Shift+F5", true)]
    [InlineData("F24", true)]
    [InlineData("F", false)]
    [InlineData("Home", false)]
    public void FunctionKeysAreF1ToF24(string text, bool expected)
    {
        Assert.True(CommandGesture.TryParse(text, out var gesture));

        Assert.Equal(expected, gesture.IsFunctionKey);
    }

    [Theory]
    [InlineData("A", true)]
    [InlineData("Shift+A", true)]
    [InlineData("Shift+F5", true)]
    [InlineData("F2", true)]
    [InlineData("Ctrl+A", false)]
    [InlineData("Shift+Ctrl+A", false)]
    [InlineData("Home", true)]
    [InlineData("Shift+Home", true)]
    [InlineData("Alt+A", false)]
    public void APlainGestureHasNoModifierOtherThanShift(string text, bool expected)
    {
        Assert.True(CommandGesture.TryParse(text, out var gesture));

        Assert.Equal(expected, gesture.IsPlain);
    }
}
