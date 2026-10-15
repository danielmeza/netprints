using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using NetPrints.Editor.Controls;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using Nodify.Avalonia;

namespace NetPrints.Editor.Behaviors;

/// <summary>
/// Carries out the <see cref="GraphViewRequest"/>s of the graph view model on the Nodify editor it is attached to:
/// frame the selection, fit every node and open the node search at the selected node or the canvas center. The view
/// model only raises the request, so the menu, the command palette and the keyboard behave the same (edge case
/// "shortcut Nodify handles itself"; ADR-0004: the view owns the viewport).
/// </summary>
public sealed class GraphViewRequestBehavior : StyledElementBehavior<NodifyEditor>
{
    private const double HalfDivisor = 2;

    private NodeGraphViewModel? graph;

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is { } editor)
        {
            editor.DataContextChanged += OnDataContextChanged;
            Hook(editor.DataContext);
        }
    }

    /// <inheritdoc/>
    protected override void OnDetaching()
    {
        if (AssociatedObject is { } editor)
        {
            editor.DataContextChanged -= OnDataContextChanged;
        }

        Hook(null);
        base.OnDetaching();
    }

    private void OnDataContextChanged(object? sender, EventArgs e) => Hook(AssociatedObject?.DataContext);

    private void Hook(object? context)
    {
        if (graph is not null)
        {
            graph.ViewRequested -= OnViewRequested;
        }

        graph = context as NodeGraphViewModel;

        if (graph is not null)
        {
            graph.ViewRequested += OnViewRequested;
        }
    }

    private void OnViewRequested(object? sender, GraphViewRequest request)
    {
        if (AssociatedObject is not { } editor || graph is not { } current)
        {
            return;
        }

        switch (request)
        {
            case GraphViewRequest.FitAll:
                editor.FitToScreen();
                break;
            case GraphViewRequest.FrameSelection:
                FrameSelection(editor, current);
                break;
            case GraphViewRequest.NodeSearch:
                OpenSearch(editor, current);
                break;
        }
    }

    private static void FrameSelection(NodifyEditor editor, NodeGraphViewModel current)
    {
        Rect? area = null;
        foreach (var node in current.SelectedNodes)
        {
            if (editor.ContainerFromItem(node) is ItemContainer container)
            {
                var bounds = new Rect(container.Location, container.ActualSize);
                area = area is { } union ? union.Union(bounds) : bounds;
            }
        }

        if (area is { } target)
        {
            editor.FitToScreen(target);
        }
    }

    private static void OpenSearch(NodifyEditor editor, NodeGraphViewModel current)
    {
        if (TopLevel.GetTopLevel(editor) is { } topLevel)
        {
            CanvasPointerTracker.For(topLevel).Invalidate();
        }

        GraphPoint position = current.SelectedNodes.FirstOrDefault()?.Location ?? CanvasCenter(editor);
        current.OpenSearchCommand.ExecuteAsync(position).Forget(current.Context, "Failed to open the node search");
    }

    private static GraphPoint CanvasCenter(NodifyEditor editor)
    {
        Point location = editor.ViewportLocation;
        double zoom = editor.ViewportZoom;
        return new GraphPoint(location.X + editor.Bounds.Width / HalfDivisor / zoom, location.Y + editor.Bounds.Height / HalfDivisor / zoom);
    }
}
