using Avalonia.Controls;
using Avalonia.Interactivity;
using NetPrints.Core;
using NetPrints.Editor.Hosting.Avalonia;

namespace NetPrints.Editor.Dialogs;

/// <summary>Chooses a method; the first one is preselected (PAR-59).</summary>
public partial class SelectMethodDialog : Window, IDialogResult<MethodSpecifier>
{
    /// <summary>Loads the dialog's XAML, with no choices set.</summary>
    public SelectMethodDialog()
    {
        InitializeComponent();
    }

    /// <summary>Loads the dialog's XAML with the offered methods; the first one is preselected.</summary>
    /// <param name="methods">Methods offered by the chooser.</param>
    public SelectMethodDialog(IEnumerable<MethodSpecifier> methods) : this()
    {
        var list = methods.ToList();
        MethodBox.ItemsSource = list;
        MethodBox.SelectedItem = list.FirstOrDefault();
    }

    /// <summary>The dialog's result once closed via <see cref="OnSelectClicked"/>, or <see langword="null"/> before then.</summary>
    public MethodSpecifier? Result { get; private set; }

    /// <summary>The currently selected method, or <see langword="null"/> if none is selected.</summary>
    public MethodSpecifier? SelectedMethod => MethodBox.SelectedItem as MethodSpecifier;

    private void OnSelectClicked(object? sender, RoutedEventArgs e)
    {
        Result = SelectedMethod;
        if (Result is not null)
        {
            Close(Result);
        }
    }
}
