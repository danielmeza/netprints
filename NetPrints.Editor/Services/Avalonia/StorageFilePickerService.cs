using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace NetPrints.Editor.Services.Avalonia;

/// <summary>Platform file and folder pickers through <see cref="TopLevel.StorageProvider"/>.</summary>
public sealed class StorageFilePickerService(Func<TopLevel?> topLevel) : IFilePickerService
{
    private IStorageProvider StorageProvider =>
        topLevel()?.StorageProvider ?? throw new InvalidOperationException("No window is available for the file picker.");

    private static List<FilePickerFileType> ToFileTypes(IReadOnlyList<FileFilter> filters) =>
        filters.Select(f => new FilePickerFileType(f.Name) { Patterns = f.Patterns.ToList() }).ToList();

    public async Task<string?> OpenFileAsync(string title, IReadOnlyList<FileFilter> filters)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = ToFileTypes(filters),
        });

        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> SaveFileAsync(string title, string suggestedName, string defaultExtension, IReadOnlyList<FileFilter> filters)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = defaultExtension,
            FileTypeChoices = ToFileTypes(filters),
            ShowOverwritePrompt = true,
        });

        return file?.TryGetLocalPath();
    }

    public async Task<string?> OpenFolderAsync(string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }
}
