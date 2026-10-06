using Avalonia.Headless.XUnit;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Testing.Ui.Dialogs;

namespace NetPrints.Editor.UITests.Dialogs;

/// <summary>The recovery dialog: its buttons and its default (FR-025, contracts/shell.md section 5).</summary>
public class RecoverDialogTests
{
    private static readonly DateTime Written = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

    private static readonly RecoveryFile[] Current = [new("Program.netpc.json", Written, false)];

    private static readonly RecoveryFile[] WithOlder = [new("Program.netpc.json", Written, false), new("Other.netpc.json", Written, true)];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData(RecoveryChoice.Restore)]
    [InlineData(RecoveryChoice.Discard)]
    public async Task EachButtonClosesTheDialogWithItsChoice(RecoveryChoice choice)
    {
        using var ui = HeadlessUi.Create();
        var dialog = ui.Show(new RecoverDialog(Current));
        var page = new RecoverDialogPage(ui.Driver);

        Assert.Equal(Current, Assert.IsType<RecoverDialogViewModel>(dialog.DataContext).Files);
        await (choice == RecoveryChoice.Restore ? page.RestoreButton : page.DiscardButton).ClickAsync(Token);

        Assert.False(dialog.IsVisible);
        Assert.Equal(choice, dialog.Result);
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData(false, RecoveryChoice.Restore)]
    [InlineData(true, RecoveryChoice.Discard)]
    public async Task EnterPressesRestoreUnlessABackupIsOlderThanItsFile(bool older, RecoveryChoice expected)
    {
        using var ui = HeadlessUi.Create();
        var dialog = ui.Show(new RecoverDialog(older ? WithOlder : Current));

        await ui.Driver.PressAsync("Enter", Token);

        Assert.Equal(expected, dialog.Result);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ClosingTheWindowKeepsTheBackups()
    {
        using var ui = HeadlessUi.Create();
        var dialog = ui.Show(new RecoverDialog(Current));

        dialog.Close();

        Assert.Equal(RecoveryChoice.Later, dialog.Result);
    }
}
