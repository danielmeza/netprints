using Avalonia.Controls;
using Avalonia.Input;
using NetPrints.Editor.Graph;

namespace NetPrints.Editor.Variables;

/// <summary>A row of the Variables panel's "Method: &lt;name&gt;" group (US5, sub-phase H).</summary>
public partial class LocalVariableView : UserControl
{
    /// <summary>Loads the control's XAML.</summary>
    public LocalVariableView()
    {
        InitializeComponent();
    }

    private LocalVariableVM? ViewModel => DataContext as LocalVariableVM;

    // Local variables can be dragged onto the graph to open the Get/Set chooser (PAR-57, US5).
    private readonly DragSourceHelper dragSource = new();

    private void OnNamePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel is { } local)
        {
            dragSource.Pressed(e, this, local);
        }
    }

    private void OnNamePointerMoved(object? sender, PointerEventArgs e) => dragSource.Moved(e, this);

    private void OnNamePointerReleased(object? sender, PointerReleasedEventArgs e) => dragSource.Released();
}
