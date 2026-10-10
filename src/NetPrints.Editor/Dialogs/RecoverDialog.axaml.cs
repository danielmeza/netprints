using Avalonia.Controls;
using NetPrints.Editor.Hosting.Avalonia;
using NetPrints.Editor.Lifecycle;

namespace NetPrints.Editor.Dialogs;

/// <summary>Offers to restore the backed-up files of the project being opened.</summary>
public partial class RecoverDialog : Window, IDialogResult<RecoveryChoice>
{
    private readonly RecoverDialogViewModel viewModel;

    /// <summary>Loads the dialog's XAML, listing no files.</summary>
    public RecoverDialog() : this([])
    {
    }

    /// <summary>Loads the dialog's XAML for the given files.</summary>
    /// <param name="files">The backed-up files to list.</param>
    public RecoverDialog(IReadOnlyList<RecoveryFile> files)
    {
        viewModel = new RecoverDialogViewModel(files);
        DataContext = viewModel;
        InitializeComponent();
    }

    /// <summary>What the user chose; <see cref="RecoveryChoice.Later"/> until then.</summary>
    public RecoveryChoice Result => viewModel.Result;

    /// <summary>What the user answered: the button, and the files to restore when it was Restore.</summary>
    public RecoveryAnswer Answer => viewModel.Answer;
}
