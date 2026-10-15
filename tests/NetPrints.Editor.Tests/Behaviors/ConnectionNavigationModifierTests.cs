using Avalonia.Input;
using NetPrints.Editor.Behaviors;

namespace NetPrints.Editor.Tests.Behaviors;

public class ConnectionNavigationModifierTests
{
    [Theory]
    [InlineData(KeyModifiers.Control, false, true)]
    [InlineData(KeyModifiers.Meta, false, false)]
    [InlineData(KeyModifiers.Meta, true, true)]
    [InlineData(KeyModifiers.Control, true, false)]
    [InlineData(KeyModifiers.None, false, false)]
    [InlineData(KeyModifiers.Control | KeyModifiers.Shift, false, false)]
    public void TheCommandModifierOfThePlatformAsksForTheFartherEnd(KeyModifiers modifiers, bool isMacOS, bool expected) =>
        Assert.Equal(expected, ConnectionNavigationBehavior.IsGoToModifier(modifiers, isMacOS));
}
