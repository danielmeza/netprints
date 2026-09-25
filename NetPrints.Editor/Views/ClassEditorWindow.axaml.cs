using Avalonia.Controls;
using Avalonia.Input;
using NetPrints.Editor.ViewModels;
using NetPrints.Editor.Views.Graph;

namespace NetPrints.Editor.Views;

/// <summary>Class editor window (PAR-22..37).</summary>
public partial class ClassEditorWindow : Window
{
    public ClassEditorWindow()
    {
        InitializeComponent();
    }

    private ClassEditorVM? ViewModel => DataContext as ClassEditorVM;

    // Single click shows the method inspector, double click opens the graph (PAR-24, 27).
    private void OnMethodTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is MethodVM method)
        {
            ViewModel?.SelectMethodCommand.Execute(method);
        }
    }

    private void OnMethodDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is MethodVM method)
        {
            ViewModel?.OpenMethodCommand.Execute(method);
        }
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
