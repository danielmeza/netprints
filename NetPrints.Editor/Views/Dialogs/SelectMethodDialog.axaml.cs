using Avalonia.Controls;
using Avalonia.Interactivity;
using NetPrints.Core;
using NetPrints.Editor.Services.Avalonia;

namespace NetPrints.Editor.Views.Dialogs;

/// <summary>Chooses a method; the first one is preselected (PAR-59).</summary>
public partial class SelectMethodDialog : Window, IDialogResult<MethodSpecifier>
{
    public SelectMethodDialog()
    {
        InitializeComponent();
    }

    public SelectMethodDialog(IEnumerable<MethodSpecifier> methods) : this()
    {
        var list = methods.ToList();
        MethodBox.ItemsSource = list;
        MethodBox.SelectedItem = list.FirstOrDefault();
    }

    public MethodSpecifier? Result { get; private set; }

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
