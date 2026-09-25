namespace NetPrints.Testing.Ui.Hosting;

/// <summary>
/// Answers the editor's file and folder pickers. Headless, a fake picker is primed with the answer
/// before the action; on the desktop, the platform (GTK) dialog is driven after it opens.
/// </summary>
public interface IFileDialogs
{
    /// <summary>Runs <paramref name="trigger"/>, which opens a file picker titled <paramref name="title"/>, and chooses <paramref name="path"/>.</summary>
    Task OpenFileAsync(string title, string path, Func<Task> trigger, CancellationToken cancellationToken);

    /// <summary>Runs <paramref name="trigger"/>, which opens a save picker, and saves as <paramref name="path"/>.</summary>
    Task SaveFileAsync(string title, string path, Func<Task> trigger, CancellationToken cancellationToken);

    /// <summary>Runs <paramref name="trigger"/>, which opens a folder picker, and chooses <paramref name="path"/>.</summary>
    Task OpenFolderAsync(string title, string path, Func<Task> trigger, CancellationToken cancellationToken);
}
