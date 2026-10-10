using Avalonia.Headless.XUnit;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Testing.Ui.Dialogs;

namespace NetPrints.Editor.UITests.Dialogs;

/// <summary>The unsaved changes dialog: its buttons, its default and Esc (contracts/shell.md section 5).</summary>
public class UnsavedChangesDialogTests
{
    private static readonly UnsavedFile[] Files =
    [
        new("Program.netpc.json", UnsavedFileKind.Class, "Program"),
        new("Other.netpc.json", UnsavedFileKind.Class, "Other"),
    ];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData(UnloadChoice.Save)]
    [InlineData(UnloadChoice.Discard)]
    [InlineData(UnloadChoice.Cancel)]
    public async Task EachButtonClosesTheDialogWithItsChoice(UnloadChoice choice)
    {
        using var ui = HeadlessUi.Create();
        var dialog = ui.Show(new UnsavedChangesDialog(Files));
        var page = new UnsavedChangesDialogPage(ui.Driver);

        Assert.Equal(Files, Assert.IsType<UnsavedChangesDialogViewModel>(dialog.DataContext).Files);
        await (choice switch
        {
            UnloadChoice.Save => page.SaveAllButton,
            UnloadChoice.Discard => page.DontSaveButton,
            _ => page.CancelButton,
        }).ClickAsync(Token);

        Assert.False(dialog.IsVisible);
        Assert.Equal(choice, dialog.Result);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task EnterPressesSaveAllAndEscapeCancels()
    {
        using var ui = HeadlessUi.Create();
        var saved = ui.Show(new UnsavedChangesDialog(Files));
        await ui.Driver.PressAsync("Enter", Token);
        Assert.Equal(UnloadChoice.Save, saved.Result);

        var cancelled = ui.Show(new UnsavedChangesDialog(Files));
        await ui.Driver.PressAsync("Escape", Token);
        Assert.False(cancelled.IsVisible);
        Assert.Equal(UnloadChoice.Cancel, cancelled.Result);
    }
}
