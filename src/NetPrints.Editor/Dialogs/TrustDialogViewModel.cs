using CommunityToolkit.Mvvm.Input;

namespace NetPrints.Editor.Dialogs;

/// <summary>Asks whether a project's extensions may be loaded (extension-points.md §8.3, batch X2b).</summary>
public sealed partial class TrustDialogViewModel : DialogViewModel<bool>
{
    /// <summary>Builds the prompt for a project and its extension folders.</summary>
    /// <param name="projectPath">Full path of the project.</param>
    /// <param name="extensionFolders">Full paths of the project's extension folders.</param>
    public TrustDialogViewModel(string projectPath, IReadOnlyList<string> extensionFolders)
    {
        Prompt = $"'{projectPath}' wants to load extensions. They run code in the editor and can change what it does. " +
            "Trust this project only if you trust its author.";
        ExtensionFolders = extensionFolders;
    }

    /// <summary>The warning shown above the folder list.</summary>
    public string Prompt { get; }

    /// <summary>The project's extension folders, listed as-is.</summary>
    public IReadOnlyList<string> ExtensionFolders { get; }

    /// <summary>Closes the dialog with the project trusted.</summary>
    [RelayCommand]
    private void Trust() => RequestClose(true);

    /// <summary>Closes the dialog with the project not trusted.</summary>
    [RelayCommand]
    private void DontLoad() => RequestClose(false);
}
