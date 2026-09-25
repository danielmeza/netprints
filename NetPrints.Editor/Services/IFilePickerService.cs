namespace NetPrints.Editor.Services;

/// <summary>
/// Platform file and folder pickers. All methods return a local path, or null when cancelled.
/// </summary>
public interface IFilePickerService
{
    Task<string?> OpenFileAsync(string title, IReadOnlyList<FileFilter> filters);

    Task<string?> SaveFileAsync(string title, string suggestedName, string defaultExtension, IReadOnlyList<FileFilter> filters);

    Task<string?> OpenFolderAsync(string title);
}
