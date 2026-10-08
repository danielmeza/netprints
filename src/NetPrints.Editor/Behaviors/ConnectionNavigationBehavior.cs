using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Graph.Pins;
using Nodify.Avalonia;
using Nodify.Avalonia.Connections;

namespace NetPrints.Editor.Behaviors;

/// <summary>
/// Ctrl+click on a connection of the Nodify editor it is attached to moves the view to the end farther from the click (FR-062):
/// the gesture turns the press into graph units and calls the connection, which runs the registered command. A right click without
/// a drag on a connection opens the connection's context menu (the editor captures the pointer, so the menu never gets the release itself).
/// </summary>
public sealed class ConnectionNavigationBehavior : StyledElementBehavior<NodifyEditor>
{
    private const double ClickThreshold = 4;

    private Control? rightPressedConnection;
    private Point rightPressPosition;

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject?.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AssociatedObject?.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <inheritdoc/>
    protected override void OnDetaching()
    {
        AssociatedObject?.RemoveHandler(InputElement.PointerPressedEvent, OnPressed);
        AssociatedObject?.RemoveHandler(InputElement.PointerReleasedEvent, OnReleased);
        base.OnDetaching();
    }

    /// <summary>Whether the modifiers held at a click ask for go to farther end: Ctrl, or Cmd on macOS where Ctrl+click is the secondary click.</summary>
    /// <param name="modifiers">The modifiers held.</param>
    /// <param name="isMacOS">Whether the platform is macOS.</param>
    /// <returns><see langword="true"/> when only the platform's command modifier is held.</returns>
    public static bool IsGoToModifier(KeyModifiers modifiers, bool isMacOS) =>
        modifiers == (isMacOS ? KeyModifiers.Meta : KeyModifiers.Control);

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (AssociatedObject is not { } editor)
        {
            return;
        }

        PointerPointProperties properties = e.GetCurrentPoint(editor).Properties;
        Control? element = ConnectionElementOf(e.Source);
        if (properties.IsRightButtonPressed)
        {
            rightPressedConnection = element;
            rightPressPosition = e.GetPosition(editor);
            return;
        }

        if (IsGoToModifier(e.KeyModifiers, OperatingSystem.IsMacOS()) && properties.IsLeftButtonPressed && element?.DataContext is ConnectionViewModel connection)
        {
            Point position = e.GetPosition(editor);
            double zoom = editor.ViewportZoom;
            connection.GoToFartherEnd(new GraphPoint(editor.ViewportLocation.X + (position.X / zoom), editor.ViewportLocation.Y + (position.Y / zoom)));
            e.Handled = true;
        }
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (AssociatedObject is not { } editor || e.InitialPressMouseButton != MouseButton.Right || rightPressedConnection is not { } element)
        {
            return;
        }

        rightPressedConnection = null;
        Point released = e.GetPosition(editor);
        if (Math.Abs(released.X - rightPressPosition.X) < ClickThreshold && Math.Abs(released.Y - rightPressPosition.Y) < ClickThreshold)
        {
            element.ContextMenu?.Open(element);
            e.Handled = true;
        }
    }

    private static Control? ConnectionElementOf(object? source)
    {
        for (Visual? visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is Connection { DataContext: ConnectionViewModel } connection)
            {
                return connection;
            }

            if (visual is StyledElement { DataContext: NodeViewModel or NodeGraphViewModel })
            {
                return null;
            }
        }

        return null;
    }
}
