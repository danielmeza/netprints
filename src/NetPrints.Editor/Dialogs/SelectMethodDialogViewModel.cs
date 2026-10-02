using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Core;

namespace NetPrints.Editor.Dialogs;

/// <summary>Chooses a method; the first one is preselected (PAR-59, batch X2b).</summary>
public sealed partial class SelectMethodDialogViewModel : DialogViewModel<MethodSpecifier>
{
    /// <summary>Offers <paramref name="methods"/>, preselecting the first one.</summary>
    /// <param name="methods">Methods offered by the chooser.</param>
    public SelectMethodDialogViewModel(IEnumerable<MethodSpecifier> methods)
    {
        Methods = [.. methods];
        SelectedMethod = Methods.FirstOrDefault();
    }

    /// <summary>Methods offered by the chooser.</summary>
    public IReadOnlyList<MethodSpecifier> Methods { get; }

    /// <summary>The currently selected method, or <see langword="null"/> if none is selected.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SelectCommand))]
    public partial MethodSpecifier? SelectedMethod { get; set; }

    private bool CanSelect() => SelectedMethod is not null;

    /// <summary>Closes the dialog with the selected method.</summary>
    [RelayCommand(CanExecute = nameof(CanSelect))]
    private void Select() => RequestClose(SelectedMethod);
}
