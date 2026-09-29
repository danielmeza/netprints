namespace NetPrints.Editor.Hosting;

/// <summary>
/// Platform file and folder pickers. All methods return a local path, or null when cancelled.
/// </summary>
public interface IFilePickerService
{
    /// <summary>Shows a native "open file" picker.</summary>
    /// <param name="title">Picker window title.</param>
    /// <param name="filters">File type filters offered by the picker.</param>
    /// <returns>The chosen local path, or <see langword="null"/> if cancelled.</returns>
    Task<string?> OpenFileAsync(string title, IReadOnlyList<FileFilter> filters);

    /// <summary>Shows a native "save file" picker.</summary>
    /// <param name="title">Picker window title.</param>
    /// <param name="suggestedName">Initial file name offered to the user.</param>
    /// <param name="defaultExtension">Extension applied if the user does not type one.</param>
    /// <param name="filters">File type filters offered by the picker.</param>
    /// <returns>The chosen local path, or <see langword="null"/> if cancelled.</returns>
    Task<string?> SaveFileAsync(string title, string suggestedName, string defaultExtension, IReadOnlyList<FileFilter> filters);

    /// <summary>Shows a native "open folder" picker.</summary>
    /// <param name="title">Picker window title.</param>
    /// <returns>The chosen local path, or <see langword="null"/> if cancelled.</returns>
    Task<string?> OpenFolderAsync(string title);
}
