using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Testing.Ui.Dialogs;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Shell;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>The Unsaved changes dialog in the dialog shell: the Linux button order, Esc cancels and Enter takes the default (FR-088, US9).</summary>
public sealed class DialogShellKeysTests(DesktopWorkerPool pool) : ProjectEditorTestBase(pool)
{
    private const string UnsavedDialogType = "UnsavedChangesDialog";

    [Fact]
    public Task EscCancelsAndEnterSavesAndClosesTheProject() => RunScenarioAsync(async token =>
    {
        await StartAsync(token);
        await WaitForProjectAsync(token);
        string before = await File.ReadAllTextAsync(ClassFile, token);
        var prompt = new UnsavedChangesDialogPage(Driver);

        using (Step("File, Close project opens the dialog with the buttons in the Linux order"))
        {
            await AddAVariableAsync(token);
            await Shell.Menu.InvokeAsync("File", ShellCommands.CloseProject, token);
            await prompt.WaitVisibleAsync(token);
            Assert.Contains(UnsavedDialogType, await Shell.WindowTypesAsync(token));
            double cancel = (await prompt.CancelButton.GetAsync(token)).Bounds.X;
            double dontSave = (await prompt.DontSaveButton.GetAsync(token)).Bounds.X;
            double saveAll = (await prompt.SaveAllButton.GetAsync(token)).Bounds.X;
            Assert.True(cancel < dontSave && dontSave < saveAll, $"Expected Cancel, Don't save, Save all from the left; got {cancel:0}, {dontSave:0}, {saveAll:0}.");
        }

        using (Step("Esc cancels and the project stays open"))
        {
            await Driver.PressAsync("Escape", token);
            await prompt.WaitHiddenAsync(token);
            Assert.False(LeasedEditor.HasExited, "The editor exited although the prompt was cancelled.");
            await Shell.Tree.Variable("Variable").WaitVisibleAsync(token);
            Assert.Equal(before, await File.ReadAllTextAsync(ClassFile, token));
        }

        using (Step("Enter on the reopened dialog saves and closes the project"))
        {
            await Shell.Menu.InvokeAsync("File", ShellCommands.CloseProject, token);
            await prompt.WaitVisibleAsync(token);
            await Driver.PressAsync("Enter", token);
            await prompt.WaitHiddenAsync(token);
            await Shell.Tree.Project("HelloWorld").WaitHiddenAsync(token);
            Assert.Contains("Variable", await File.ReadAllTextAsync(ClassFile, token), StringComparison.Ordinal);
        }
    });
}
