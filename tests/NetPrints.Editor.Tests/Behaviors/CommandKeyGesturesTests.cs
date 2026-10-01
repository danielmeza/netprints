using Avalonia.Input;
using NetPrints.Editor.Behaviors;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Tests.Behaviors;

/// <summary>The view-layer conversion of descriptor gestures: canonical keys only, and Ctrl as the platform command key.</summary>
public class CommandKeyGesturesTests
{
    private sealed class NoopHandler : ICommandHandler
    {
        public bool CanExecute(CommandContext context) => true;

        public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static CommandDescriptor Command(params string[] gestures) => new("netprints.command.test", "Test", new NoopHandler(), DefaultGestures: gestures);

    [Theory]
    [InlineData(false, KeyModifiers.Control)]
    [InlineData(true, KeyModifiers.Meta)]
    public void CtrlMapsToTheCommandModifierOfThePlatform(bool isMacOS, KeyModifiers expected)
    {
        var gesture = Assert.Single(CommandKeyGestures.Of(Command("Ctrl+S"), isMacOS));

        Assert.Equal(Key.S, gesture.Key);
        Assert.Equal(expected, gesture.KeyModifiers);
    }

    [Fact]
    public void OtherModifiersAreUnchangedOnMacOS()
    {
        var gesture = Assert.Single(CommandKeyGestures.Of(Command("Ctrl+Shift+Alt+B"), isMacOS: true));

        Assert.Equal(KeyModifiers.Meta | KeyModifiers.Shift | KeyModifiers.Alt, gesture.KeyModifiers);
    }

    [Theory]
    [InlineData("Esc", Key.Escape)]
    [InlineData("Return", Key.Enter)]
    [InlineData("Backspace", Key.Back)]
    [InlineData("PgDn", Key.PageDown)]
    [InlineData("7", Key.D7)]
    [InlineData("F12", Key.F12)]
    public void AKeyNameMapsToTheAvaloniaKey(string text, Key expected) =>
        Assert.Equal(expected, Assert.Single(CommandKeyGestures.Of(Command(text), isMacOS: false)).Key);
}
