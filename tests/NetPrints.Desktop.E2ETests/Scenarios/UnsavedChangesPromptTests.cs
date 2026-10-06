using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Testing.Ui.Dialogs;
using NetPrints.Testing.Ui.Shell;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>The unsaved changes prompt on the real editor: closing the window and closing the project (FR-022, SC-002).</summary>
public sealed class UnsavedChangesPromptTests(DesktopWorkerPool pool) : ProjectEditorTestBase(pool)
{
    [Fact]
    public Task CancelKeepsTheWindowOpenAndSaveAllSavesAndCloses() => RunScenarioAsync(async token =>
    {
        await StartAsync(token);
        await WaitForProjectAsync(token);
        string before = await File.ReadAllTextAsync(ClassFile, token);
        var prompt = new UnsavedChangesDialogPage(Driver);

        using (Step("edit and close, then cancel"))
        {
            await AddAVariableAsync(token);
            await Shell.CloseWindowAsync(token);
            await prompt.WaitVisibleAsync(token);
            await prompt.CancelButton.ClickAsync(token);
            await prompt.WaitHiddenAsync(token);
            Assert.False(LeasedEditor.HasExited, "The editor exited although the prompt was cancelled.");
            await Shell.Tree.Variable("Variable").WaitVisibleAsync(token);
            Assert.Equal(before, await File.ReadAllTextAsync(ClassFile, token));
        }

        using (Step("close again, save all"))
        {
            await Shell.CloseWindowAsync(token);
            await prompt.WaitVisibleAsync(token);
            await ClickAndWaitForExitAsync(prompt.SaveAllButton, token);
        }

        string saved = await File.ReadAllTextAsync(ClassFile, token);
        Assert.NotEqual(before, saved);
        Assert.Contains("Variable", saved, StringComparison.Ordinal);
    });

    [Fact]
    public Task CloseProjectAsksFirst() => RunScenarioAsync(async token =>
    {
        await StartAsync(token);
        await WaitForProjectAsync(token);
        string before = await File.ReadAllTextAsync(ClassFile, token);
        var prompt = new UnsavedChangesDialogPage(Driver);

        using (Step("edit, close project, cancel"))
        {
            await AddAVariableAsync(token);
            await Shell.Menu.InvokeAsync("File", ShellCommands.CloseProject, token);
            await prompt.WaitVisibleAsync(token);
            await prompt.CancelButton.ClickAsync(token);
            await prompt.WaitHiddenAsync(token);
            await Shell.Tree.Variable("Variable").WaitVisibleAsync(token);
        }

        using (Step("close project again, do not save"))
        {
            await Shell.Menu.InvokeAsync("File", ShellCommands.CloseProject, token);
            await prompt.WaitVisibleAsync(token);
            await prompt.DontSaveButton.ClickAsync(token);
            await Shell.Tree.Project("HelloWorld").WaitHiddenAsync(token);
        }

        Assert.Equal(before, await File.ReadAllTextAsync(ClassFile, token));
    });
}
