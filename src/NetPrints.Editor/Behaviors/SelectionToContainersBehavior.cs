using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Nodes;
using Nodify.Avalonia;

namespace NetPrints.Editor.Behaviors;

/// <summary>
/// Pushes the graph view model's node selection into the canvas item containers, so a deselect made by the view model
/// (Back, Forward, go to anything) also clears the container that Nodify had written its own selection into.
/// </summary>
public sealed class SelectionToContainersBehavior : StyledElementBehavior<NodifyEditor>
{
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

    private void Hook(object? dataContext)
    {
        if (graph is not null)
        {
            graph.SelectionChanged -= OnSelectionChanged;
        }

        graph = dataContext as NodeGraphViewModel;
        if (graph is not null)
        {
            graph.SelectionChanged += OnSelectionChanged;
        }
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (AssociatedObject is not { } editor)
        {
            return;
        }

        foreach (ItemContainer container in editor.GetVisualDescendants().OfType<ItemContainer>())
        {
            if (container.DataContext is NodeViewModel node && container.IsSelected != node.IsSelected)
            {
                container.SetCurrentValue(ItemContainer.IsSelectedProperty, node.IsSelected);
            }
        }
    }
}
