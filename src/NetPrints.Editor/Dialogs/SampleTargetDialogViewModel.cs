using CommunityToolkit.Mvvm.Input;

namespace NetPrints.Editor.Dialogs;

/// <summary>Names the folder a sample is copied to and lets the user copy and open it, change the folder or cancel.</summary>
public sealed partial class SampleTargetDialogViewModel : DialogViewModel<SampleTargetChoice>
{
    /// <summary>Builds the prompt.</summary>
    /// <param name="sampleName">The sample's name.</param>
    /// <param name="targetFolder">The full path of the folder the copy goes in.</param>
    public SampleTargetDialogViewModel(string sampleName, string targetFolder)
    {
        ArgumentException.ThrowIfNullOrEmpty(sampleName);
        ArgumentException.ThrowIfNullOrEmpty(targetFolder);
        Message = $"{sampleName} is copied before it opens, so the bundled sample stays as it is.";
        TargetFolder = targetFolder;
    }

    /// <summary>Gets the sentence that says what happens to the sample.</summary>
    public string Message { get; }

    /// <summary>Gets the full path of the folder the copy goes in.</summary>
    public string TargetFolder { get; }

    /// <summary>Closes the dialog asking to copy the sample to <see cref="TargetFolder"/> and open it.</summary>
    [RelayCommand]
    private void CopyAndOpen() => RequestClose(SampleTargetChoice.Open);

    /// <summary>Closes the dialog asking to choose another parent folder.</summary>
    [RelayCommand]
    private void Change() => RequestClose(SampleTargetChoice.Change);

    /// <summary>Closes the dialog without copying.</summary>
    [RelayCommand]
    private void Cancel() => RequestClose(SampleTargetChoice.Cancel);
}
