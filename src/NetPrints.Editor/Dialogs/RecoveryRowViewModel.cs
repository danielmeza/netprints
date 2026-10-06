using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Editor.Lifecycle;

namespace NetPrints.Editor.Dialogs;

/// <summary>One backed-up file of the recovery dialog, with the choice to restore it or discard its backup.</summary>
public sealed partial class RecoveryRowViewModel : ObservableObject
{
    /// <summary>Builds the row of a file. A backup older than its file starts as Discard, so restoring it never replaces newer work by accident.</summary>
    /// <param name="file">The backed-up file.</param>
    public RecoveryRowViewModel(RecoveryFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        File = file;
        restore = !file.IsOlderThanFile;
    }

    /// <summary>Gets the backed-up file.</summary>
    public RecoveryFile File { get; }

    /// <summary>Gets the class path of the file.</summary>
    public string Path => File.Path;

    /// <summary>Gets whether the file on disk was written after the backup.</summary>
    public bool IsOlderThanFile => File.IsOlderThanFile;

    /// <summary>Gets whether the project has no file there yet: the backup holds a class that was never saved.</summary>
    public bool IsNewFile => File.IsNewFile;

    /// <summary>Gets or sets whether the backup is restored (checked) or discarded (unchecked).</summary>
    [ObservableProperty]
    private bool restore;
}
