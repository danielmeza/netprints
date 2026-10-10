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
        Assert.Equal("F", chord.Key);
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
