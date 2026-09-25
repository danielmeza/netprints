using Avalonia.Controls;
using Avalonia.Input;
using NetPrints.Editor.ViewModels;

namespace NetPrints.Editor.Views.Graph;

public partial class GetSetChooserView : UserControl
{
    public GetSetChooserView()
    {
        InitializeComponent();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e) => (DataContext as GetSetChooserVM)?.CloseCommand.Execute(null);
}
