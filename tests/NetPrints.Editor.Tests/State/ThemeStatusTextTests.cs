using Avalonia.Styling;
using NetPrints.Editor.State;

namespace NetPrints.Editor.Tests.State;

public sealed class ThemeStatusTextTests
{
    [Fact]
    public void AnExplicitThemeIsNamedWithoutTheResolvedVariant()
    {
        Assert.Equal("Theme: Light", ThemeStatusText.For(EditorTheme.Light, ThemeVariant.Dark));
        Assert.Equal("Theme: Dark", ThemeStatusText.For(EditorTheme.Dark, null));
    }

    [Theory]
    [InlineData("Dark", "Theme: System (Dark)")]
    [InlineData("Light", "Theme: System (Light)")]
    public void SystemNamesTheVariantTheOperatingSystemResolvedTo(string variant, string expected)
    {
        ThemeVariant resolved = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

        Assert.Equal(expected, ThemeStatusText.For(EditorTheme.System, resolved));
    }

    [Fact]
    public void SystemWithoutAKnownVariantIsJustNamed() =>
        Assert.Equal("Theme: System", ThemeStatusText.For(EditorTheme.System, null));
}
