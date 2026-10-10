using Avalonia.Controls;
using NetPrints.Editor.Hosting.Avalonia;

namespace NetPrints.Editor.Dialogs;

/// <summary>Names the folder a sample is copied to before it opens.</summary>
public partial class SampleTargetDialog : Window, IDialogResult<SampleTargetChoice>
{
    private readonly SampleTargetDialogViewModel viewModel;

    /// <summary>Loads the dialog's XAML, with no sample.</summary>
    public SampleTargetDialog() : this("Sample", "Sample")
    {
    }

    /// <summary>Loads the dialog's XAML for a sample.</summary>
    /// <param name="sampleName">The sample's name.</param>
    /// <param name="targetFolder">The full path of the folder the copy goes in.</param>
    public SampleTargetDialog(string sampleName, string targetFolder)
    {
        Title = $"Open {sampleName}";
        viewModel = new SampleTargetDialogViewModel(sampleName, targetFolder);
        DataContext = viewModel;
        InitializeComponent();
    }

    /// <summary>What the user chose; <see cref="SampleTargetChoice.Cancel"/> until then.</summary>
    public SampleTargetChoice Result => viewModel.Result;
}
