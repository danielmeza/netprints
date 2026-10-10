using NetPrints.Editor.Commands;
using NetPrints.Editor.Tests.Shell;

namespace NetPrints.Editor.Tests.Commands;

public sealed class NavigationCommandsTests
{
    private readonly FakeShell shell = new();

    [Fact]
    public async Task BackIsEnabledOnlyWithAnEntryAndGoesBack()
    {
        var handler = new NavigateHistoryCommandHandler(forward: false);
        Assert.False(handler.CanExecute(shell.Context()));

        shell.Navigation.CanGoBack = true;
        Assert.True(handler.CanExecute(shell.Context()));
        await handler.ExecuteAsync(shell.Context(), TestContext.Current.CancellationToken);

        Assert.Equal(["GoBack"], shell.Navigation.Calls);
    }

    [Fact]
    public async Task ForwardIsEnabledOnlyWithAnEntryAndGoesForward()
    {
        var handler = new NavigateHistoryCommandHandler(forward: true);
        shell.Navigation.CanGoBack = true;
        Assert.False(handler.CanExecute(shell.Context()));

        shell.Navigation.CanGoForward = true;
        Assert.True(handler.CanExecute(shell.Context()));
        await handler.ExecuteAsync(shell.Context(), TestContext.Current.CancellationToken);

        Assert.Equal(["GoForward"], shell.Navigation.Calls);
    }

    [Fact]
    public async Task TheCommandPaletteCommandOpensThePalette()
    {
        var handler = new CommandPaletteCommandHandler();

        Assert.True(handler.CanExecute(shell.Context()));
        await handler.ExecuteAsync(shell.Context(), TestContext.Current.CancellationToken);

        Assert.Equal(["ShowCommandPalette"], shell.Project.Calls);
    }

    [Fact]
    public async Task TheGoToAnythingCommandOpensGoToAnything()
    {
        var handler = new GoToAnythingCommandHandler();

        Assert.True(handler.CanExecute(shell.Context()));
        await handler.ExecuteAsync(shell.Context(), TestContext.Current.CancellationToken);

        Assert.Equal(["ShowGoToAnything"], shell.Project.Calls);
    }
}
