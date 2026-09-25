using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Hosting;

namespace NetPrints.Desktop.E2ETests.Driving;

/// <summary>
/// Drives the platform file pickers (GTK dialogs on X11 without a portal): waits for the
/// dialog window by title, types the path in the location field and accepts.
/// </summary>
public sealed class GtkFileDialogs(X11Driver driver, EditorProcess editor) : IFileDialogs
{
    private Tool Tool => driver.Tool;

    private async Task<string> WaitForDialogAsync(string title, CancellationToken cancellationToken)
    {
        string pid = editor.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return await UiWait.ForAsync(driver, async () =>
        {
            var (code, output) = await Tool.TryRunAsync("xdotool", cancellationToken, "search", "--all", "--onlyvisible", "--pid", pid, "--name", $"^{title}$");
            return code == 0 ? output.Split('\n')[0].Trim() : "";
        }, id => id.Length > 0, $"the '{title}' file dialog", cancellationToken);
    }

    /// <summary>Waits until the dialog window is destroyed or no longer viewable.</summary>
    private async Task WaitForDialogClosedAsync(string title, string window, CancellationToken cancellationToken)
    {
        await UiWait.UntilAsync(driver, async () =>
        {
            var (code, info) = await Tool.TryRunAsync("xwininfo", cancellationToken, "-id", window);
            return code != 0 || !info.Contains("IsViewable", StringComparison.Ordinal);
        }, $"the '{title}' file dialog to close", cancellationToken);
    }

    private async Task FocusAsync(string window, CancellationToken cancellationToken)
    {
        await Tool.XdotoolAsync(cancellationToken, "windowactivate", "--sync", window);
        await Tool.XdotoolAsync(cancellationToken, "windowfocus", "--sync", window);
    }

    /// <summary>Opens the location field (Ctrl+L), types the path and accepts.</summary>
    private async Task ChooseAsync(string title, string path, bool folder, Func<Task> trigger, CancellationToken cancellationToken)
    {
        await trigger();
        string window = await WaitForDialogAsync(title, cancellationToken);
        await FocusAsync(window, cancellationToken);
        await Tool.XdotoolAsync(cancellationToken, "key", "--clearmodifiers", "ctrl+l");
        await Tool.XdotoolAsync(cancellationToken, "key", "--clearmodifiers", "ctrl+a");
        await Tool.XdotoolAsync(cancellationToken, "type", "--clearmodifiers", "--delay", "15", "--", folder ? path.TrimEnd('/') + "/" : path);
        await Tool.XdotoolAsync(cancellationToken, "key", "--clearmodifiers", "Return");
        if (folder)
        {
            // Return in the location field enters the folder; Return again selects it.
            await Tool.XdotoolAsync(cancellationToken, "key", "--clearmodifiers", "Return");
        }

        await WaitForDialogClosedAsync(title, window, cancellationToken);
    }

    public Task OpenFileAsync(string title, string path, Func<Task> trigger, CancellationToken cancellationToken) =>
        ChooseAsync(title, path, folder: false, trigger, cancellationToken);

    public Task OpenFolderAsync(string title, string path, Func<Task> trigger, CancellationToken cancellationToken) =>
        ChooseAsync(title, path, folder: true, trigger, cancellationToken);

    /// <summary>The save dialog focuses its name field: replace it with the full path and accept.</summary>
    public async Task SaveFileAsync(string title, string path, Func<Task> trigger, CancellationToken cancellationToken)
    {
        await trigger();
        string window = await WaitForDialogAsync(title, cancellationToken);
        await FocusAsync(window, cancellationToken);
        await Tool.XdotoolAsync(cancellationToken, "key", "--clearmodifiers", "ctrl+a");
        await Tool.XdotoolAsync(cancellationToken, "type", "--clearmodifiers", "--delay", "15", "--", path);
        await Tool.XdotoolAsync(cancellationToken, "key", "--clearmodifiers", "Return");
        await WaitForDialogClosedAsync(title, window, cancellationToken);
    }
}
