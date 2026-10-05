using System.ComponentModel;
using NetPrints.Core;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Variables;

/// <summary>
/// The Variables panel: the member variables of the active document's class and, while that document is a method,
/// constructor or accessor graph, the graph's local variables (<see cref="ActiveClassPanelViewModel{TItem}.Current"/>). A
/// class gets its item, and with it its <see cref="ClassContext"/>, only once one of its documents is active.
/// </summary>
public sealed class ShellVariablesPanelViewModel : ActiveClassPanelViewModel<VariablesPanelViewModel>
{
    /// <inheritdoc/>
    protected override bool CreatesItemsOnDemand => true;

    /// <inheritdoc/>
    protected override VariablesPanelViewModel CreateItem(ClassGraph cls, PanelContext context)
    {
        ProjectSessionViewModel session = context.Shell.Session
            ?? throw new InvalidOperationException("A variables panel item is created only while a project is open.");
        ClassContext classContext = session.ContextFor(cls);
        return new VariablesPanelViewModel(classContext.Services, classContext.Variables, classContext.CreateVariable, variable => context.Shell.TreeSelection = variable);
    }

    /// <inheritdoc/>
    protected override void OnAttached(PanelContext context)
    {
        context.Shell.PropertyChanged += OnShellChanged;
        PropertyChanged += OnOwnChanged;
    }

    /// <inheritdoc/>
    protected override void OnDetached()
    {
        PropertyChanged -= OnOwnChanged;
        if (Context is { } attached)
        {
            attached.Shell.PropertyChanged -= OnShellChanged;
        }
    }

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.ActiveDocument) or nameof(ShellViewModel.Session))
        {
            FollowGraph();
        }
    }

    private void OnOwnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Current))
        {
            FollowGraph();
        }
    }

    private void FollowGraph()
    {
        if (Current is not { } panel)
        {
            return;
        }

        ExecutionGraph? graph = null;
        if (Context?.Shell is { Session: { } session, ActiveDocument.Id: { } id })
        {
            graph = CommandTargets.GraphOf(session, id) as ExecutionGraph;
        }

        panel.OnOpenedGraphChanged(graph);
    }
}
