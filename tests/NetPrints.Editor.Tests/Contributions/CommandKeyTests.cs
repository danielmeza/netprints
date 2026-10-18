using System.Reflection;
using System.Runtime.CompilerServices;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Tests.Architecture;

namespace NetPrints.Editor.Tests.Contributions;

/// <summary>ADR-0034: <c>CommandKey</c> is a smart enum whose members carry their own behaviour, mapped one to one to the generated <c>CommandKeyId</c>.</summary>
public class CommandKeyTests
{
    private const int KeyCount = 76;

    public static TheoryData<string> Names() => [.. CommandKey.All.Select(key => key.ToString())];

    [Fact]
    public void TheVocabularyHasTheSeventySixKeysOfTheGestureTable()
    {
        Assert.Equal(KeyCount, CommandKey.All.Count);
    }

    [Theory]
    [InlineData(",")]
    [InlineData("Comma")]
    [InlineData("OemComma")]
    public void TheCommaKeyIsWrittenAsACommaAndAcceptsItsNames(string text)
    {
        Assert.True(CommandKey.TryParse(text, null, out CommandKey? key));

        Assert.Same(CommandKey.OemComma, key);
        Assert.Equal(",", key.Text);
        Assert.Equal("Ctrl+,", new CommandGesture(CommandModifiers.Ctrl, key).ToString());
        Assert.Equal(new CommandGesture(CommandModifiers.Ctrl, key), CommandGesture.Parse("Ctrl+,", null));
    }

    [Fact]
    public void EveryIdMapsToExactlyOneMemberInDeclarationOrder()
    {
        CommandKeyId[] ids = [.. Enum.GetValues<CommandKeyId>()];

        Assert.Equal(ids, CommandKey.All.Select(key => key.Id));
        Assert.Equal(ids.Length, CommandKey.All.Select(key => key.Id).Distinct().Count());
        Assert.All(ids, id => Assert.Same(CommandKey.All[(int)id], CommandKey.FromId(id)));
    }

    [Fact]
    public void EveryMemberFieldIsNamedAfterItsId()
    {
        FieldInfo[] fields = [.. typeof(CommandKey).GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => field.FieldType == typeof(CommandKey))];

        Assert.Equal(KeyCount, fields.Length);
        Assert.All(fields, field => Assert.Equal(field.Name, ((CommandKey?)field.GetValue(null))?.Id.ToString()));
    }

    [Fact]
    public void AnIdConvertsImplicitlyToItsMember()
    {
        CommandKey key = CommandKeyId.Escape;

        Assert.Same(CommandKey.Escape, key);
    }

    [Fact]
    public void EachMemberOverridesWhatDiffers()
    {
        Assert.Equal("Esc", CommandKey.Escape.Glyph);
        Assert.Equal("Delete", CommandKey.Delete.Glyph);
        Assert.Equal("3", CommandKey.D3.Glyph);
        Assert.Equal("3", CommandKey.D3.ToString());
        Assert.Equal("F2", CommandKey.F2.ToString());
        Assert.True(CommandKey.F1.IsFunctionKey);
        Assert.True(CommandKey.F24.IsFunctionKey);
        Assert.False(CommandKey.F.IsFunctionKey);
        Assert.False(CommandKey.Home.IsFunctionKey);
        Assert.Equal(24, CommandKey.All.Count(key => key.IsFunctionKey));
    }

    [Fact]
    public void NothingSwitchesOverTheKeys()
    {
        string source = File.ReadAllText(Path.Combine(RepositoryPaths.Root(), "src", "NetPrints.Extensibility.Abstractions", "Editor", "CommandKey.cs"));

        Assert.DoesNotContain("switch", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TheStaticConstructorRunsWithoutATypeInitializationException()
    {
        Exception? failure = Record.Exception(() => RuntimeHelpers.RunClassConstructor(typeof(CommandKey).TypeHandle));

        Assert.Null(failure);
        Assert.NotEmpty(CommandKey.All);
        Assert.All(CommandKey.All, key => Assert.NotNull(key));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void ATextRoundTripsThroughParsingAndFormatting(string name)
    {
        CommandKey parsed = CommandKey.Parse(name, null);
        Span<char> buffer = stackalloc char[16];

        Assert.True(CommandKey.TryParse(name.AsSpan(), null, out CommandKey? fromSpan));
        Assert.Same(parsed, fromSpan);
        Assert.Equal(name, parsed.ToString());
        Assert.True(((ISpanFormattable)parsed).TryFormat(buffer, out int written, default, null));
        Assert.Equal(name, buffer[..written].ToString());
    }

    [Theory]
    [InlineData("esc", "Escape")]
    [InlineData("ESCAPE", "Escape")]
    [InlineData("Return", "Enter")]
    [InlineData("Del", "Delete")]
    [InlineData("Ins", "Insert")]
    [InlineData("PgUp", "PageUp")]
    [InlineData("PgDn", "PageDown")]
    [InlineData("Backspace", "Back")]
    [InlineData("f24", "F24")]
    [InlineData("b", "B")]
    public void AliasesAndCaseResolveToTheCanonicalMember(string text, string canonical)
    {
        Assert.True(CommandKey.TryParse(text, null, out CommandKey? key));

        Assert.Equal(canonical, key.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Banana")]
    [InlineData("12")]
    [InlineData("F25")]
    [InlineData("F0")]
    [InlineData("Ctrl")]
    public void AnUnknownTextIsRejected(string text)
    {
        Assert.False(CommandKey.TryParse(text, null, out CommandKey? key));
        Assert.Null(key);
        Assert.Throws<FormatException>(() => CommandKey.Parse(text, null));
    }

    [Fact]
    public void ANullTextIsRejected()
    {
        Assert.False(CommandKey.TryParse((string?)null, null, out _));
    }

    [Fact]
    public void AFormatBufferThatIsTooSmallIsRejected()
    {
        Span<char> buffer = stackalloc char[2];

        Assert.False(CommandKey.PageDown.TryFormat(buffer, out int written, default, null));
        Assert.Equal(0, written);
    }

    [Fact]
    public void ParsingASpanDoesNotAllocate()
    {
        ReadOnlySpan<char> text = "PageDown";
        CommandKey.TryParse(text, null, out _);

        long before = GC.GetAllocatedBytesForCurrentThread();
        bool parsed = CommandKey.TryParse(text, null, out CommandKey? key);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(parsed);
        Assert.Same(CommandKey.PageDown, key);
        Assert.Equal(0, allocated);
    }
}
