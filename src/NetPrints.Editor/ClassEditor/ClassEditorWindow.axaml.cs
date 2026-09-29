using Avalonia.Controls;
using Avalonia.Input;
using NetPrints.Editor.Graph;

namespace NetPrints.Editor.ClassEditor;

/// <summary>Class editor window (PAR-22..37).</summary>
public partial class ClassEditorWindow : Window
{
    /// <summary>Loads the window's XAML.</summary>
    public ClassEditorWindow()
    {
        InitializeComponent();
    }

    // Methods and constructors can be dragged onto the graph (PAR-56).
    private readonly DragSourceHelper dragSource = new();

    private void OnMethodPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if ((sender as Control)?.DataContext is MethodVM method)
        {
            dragSource.Pressed(e, this, method);
        }
    }

    private void OnMethodPointerMoved(object? sender, PointerEventArgs e) => dragSource.Moved(e, this);

    private void OnMethodPointerReleased(object? sender, PointerReleasedEventArgs e) => dragSource.Released();
}
