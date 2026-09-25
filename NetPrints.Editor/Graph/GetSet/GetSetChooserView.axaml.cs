using Avalonia.Controls;
using Avalonia.Input;

namespace NetPrints.Editor.Graph.GetSet;

public partial class GetSetChooserView : UserControl
{
    public GetSetChooserView()
    {
        InitializeComponent();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e) => (DataContext as GetSetChooserVM)?.CloseCommand.Execute(null);
}
