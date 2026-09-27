using Avalonia.Controls;
using Avalonia.Input;
using NetPrints.Editor.ErrorList;
using NetPrints.Editor.Events;
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

    // Double click opens the event graph (US4); unlike methods/constructors, event graphs are not
    // dragged onto the canvas and have no single-click inspector.
    private void OnEventGraphDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is EventGraphVM eventGraph)
        {
            ViewModel?.OpenEventGraphCommand.Execute(eventGraph);
        }
    }

    // Double click navigates to the diagnostic's node, if it has one (FR-034, ED-T03).
    private void OnDiagnosticRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is DiagnosticRowVM row)
        {
            ViewModel?.ErrorList.NavigateCommand.Execute(row);
        }
    }
}
