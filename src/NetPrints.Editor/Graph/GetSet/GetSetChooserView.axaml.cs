using Avalonia.Controls;
using Avalonia.Input;

namespace NetPrints.Editor.Graph.GetSet;

/// <summary>The Get/Set chooser popup, shown when a variable is dropped onto the graph canvas.</summary>
public partial class GetSetChooserView : UserControl
{
    /// <summary>Loads the control's XAML.</summary>
    public GetSetChooserView()
    {
        InitializeComponent();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e) => (DataContext as GetSetChooserVM)?.CloseCommand.Execute(null);
}
