using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.State;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Shell;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>
/// An E2E test that starts the editor on its private copy of the HelloWorld sample, with the per-user state
/// folder inside the test's own working folder, so nothing is shared with another test on the same display.
/// </summary>
public abstract class ProjectEditorTestBase(DesktopWorkerPool pool) : X11SmokeTestBase(pool)
{
    private const string ClassName = "Program";
    private const string ProjectName = "HelloWorld";

    /// <summary>Gets the backup delay in milliseconds the editor starts with, or <see langword="null"/> for its default.</summary>
    protected virtual int? BackupDelayMilliseconds => null;

    protected string StateDirectory => Path.Combine(Work, "state");

    protected string SampleDirectory => Path.Combine(Work, "HelloWorld");

    protected string ClassFile => Path.Combine(SampleDirectory, "HelloWorld.Program.netpc.json");

    protected ShellPage Shell => new(Driver);

    protected override EditorStart EditorStartFor(string sampleProject) => new(sampleProject, Environment(), WaitForProject: true);

    protected Dictionary<string, string> Environment()
    {
        var variables = new Dictionary<string, string> { [EditorDataPaths.StateDirectoryVariable] = StateDirectory };
        if (BackupDelayMilliseconds is { } delay)
        {
            variables[BackupService.DelayVariable] = delay.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return variables;
    }

    /// <summary>Waits until the editor shows the opened project and its class in the Project tree.</summary>
    protected async Task WaitForProjectAsync(CancellationToken cancellationToken)
    {
        await Shell.WaitForProjectAsync(ProjectName, cancellationToken);
        await Shell.Tree.Class(ClassName).WaitVisibleAsync(cancellationToken);
    }

    /// <summary>Adds a variable to the class from the Edit menu and waits until it is in the Project tree.</summary>
    protected async Task AddAVariableAsync(CancellationToken cancellationToken)
    {
        await Shell.Tree.SelectAsync(Shell.Tree.Class(ClassName), cancellationToken);
        await Shell.Menu.InvokeAsync("Edit", ShellCommands.AddVariable, cancellationToken);
        await Shell.Tree.RevealAsync(Shell.Tree.Variable("Variable"), ProjectTreePage.VariablesGroup, cancellationToken);
    }

    /// <summary>Clicks a button whose action ends the editor, then waits for the process to exit; the driver's settle request after the click loses its pipe to the exiting process.</summary>
    protected Task ClickAndWaitForExitAsync(UiElement button, CancellationToken cancellationToken) =>
        EndEditorAndWaitForExitAsync(() => button.ClickAsync(cancellationToken), cancellationToken);

    /// <summary>Closes the shell window the way its title bar button does and waits for the editor process to exit.</summary>
    protected Task CloseWindowAndWaitForExitAsync(CancellationToken cancellationToken) =>
        EndEditorAndWaitForExitAsync(() => Shell.CloseWindowAsync(cancellationToken), cancellationToken);

    private async Task EndEditorAndWaitForExitAsync(Func<Task> end, CancellationToken cancellationToken)
    {
        ExpectEditorExit();
        try
        {
            await end();
        }
        catch (Exception e) when (e is InvalidOperationException or IOException && !cancellationToken.IsCancellationRequested)
        {
            // The input was delivered; the exit is checked below.
        }

        using var exit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        exit.CancelAfter(TimeSpan.FromSeconds(30));
        await LeasedEditor.Exited.WaitAsync(exit.Token);
    }

    /// <summary>Waits, with a bound, until a file-system condition holds (the editor's own work is not visible in the UI).</summary>
    protected static async Task WaitForAsync(Func<bool> condition, string what, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting for {what}.");
            }

            await Task.Delay(50, cancellationToken);
        }
    }
}
