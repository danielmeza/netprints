using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NetPrints.Editor.Graph;

namespace NetPrints.Editor.Variables;

/// <summary>A row of the Variables panel's "Method: &lt;name&gt;" group (US5, sub-phase H).</summary>
public partial class LocalVariableView : UserControl
{
    /// <summary>
    /// Loads the control's XAML and wires the name box's drag-start handlers (R2-03): attached with
    /// <see cref="RoutingStrategies.Tunnel"/> and <c>handledEventsToo: true</c> so they see the press
    /// before the <see cref="TextBox"/>'s own class handler marks it handled (the same routing
    /// <c>GraphEditorView</c> uses for its own pointer handlers, for the same reason).
    /// </summary>
    public LocalVariableView()
    {
        InitializeComponent();

        NameBox.AddHandler(PointerPressedEvent, OnNamePointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        NameBox.AddHandler(PointerMovedEvent, OnNamePointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        NameBox.AddHandler(PointerReleasedEvent, OnNamePointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
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
