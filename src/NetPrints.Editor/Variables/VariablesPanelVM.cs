using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.ModelSync;
using NetPrints.Editor.UndoRedo;

namespace NetPrints.Editor.Variables;

/// <summary>
/// Owns the Variables panel's two groups (FR-030): the class's member variables ("Class") and,
/// while the opened graph is a method or constructor, that graph's local variables ("Method:
/// &lt;name&gt;", US5, sub-phase H).
/// </summary>
public sealed partial class VariablesPanelVM : ObservableObject, IDisposable
{
    private readonly ClassEditorVM owner;

    /// <summary>
    /// Creates the panel for <paramref name="owner"/> and builds its initial Method group from
    /// <see cref="ClassEditorVM.OpenedGraph"/>.
    /// </summary>
    /// <param name="owner">Class editor view model this panel belongs to.</param>
    public VariablesPanelVM(ClassEditorVM owner)
    {
        this.owner = owner;
        owner.PropertyChanged += OnOwnerPropertyChanged;
        RebuildMethodGroup();
    }

    /// <summary>The class's member variables (the "Class" group).</summary>
    public ObservableViewModelCollection<MemberVariableVM, Variable> ClassVariables => owner.Variables;

    /// <summary>
    /// The opened method's or constructor's local variables (the "Method" group), or
    /// <see langword="null"/> when no method or constructor graph is open.
    /// </summary>
    public ObservableViewModelCollection<LocalVariableVM, LocalVariable>? MethodVariables { get; private set; }

    /// <summary>Whether the "Method" group has a graph to show.</summary>
    public bool HasMethodGroup => MethodVariables is not null;

    /// <summary>The "Method: &lt;name&gt;" header text, or "" when <see cref="HasMethodGroup"/> is <see langword="false"/>.</summary>
    public string MethodGroupHeader => OpenedExecutionGraph is { } graph ? $"Method: {GraphName(graph)}" : "";

    private static string GraphName(ExecutionGraph graph) => graph is MethodGraph method ? method.Name : graph.ToString() ?? "";

    private ExecutionGraph? OpenedExecutionGraph => owner.OpenedGraph?.Graph as ExecutionGraph;

    private void OnOwnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClassEditorVM.OpenedGraph))
        {
            RebuildMethodGroup();
        }
    }

    private void RebuildMethodGroup()
    {
        MethodVariables?.Dispose();
        var graph = OpenedExecutionGraph;
        MethodVariables = graph is null
            ? null
            : new ObservableViewModelCollection<LocalVariableVM, LocalVariable>(graph.LocalVariables,
                l => new LocalVariableVM(l, graph, owner), l => l.Dispose());

        OnPropertyChanged(nameof(MethodVariables));
        OnPropertyChanged(nameof(HasMethodGroup));
        OnPropertyChanged(nameof(MethodGroupHeader));
    }

    /// <summary>Creates a local variable of type <c>object</c> with a unique default name (undoable).</summary>
    [RelayCommand]
    private void CreateLocalVariable()
    {
        if (OpenedExecutionGraph is not { } graph)
        {
            return;
        }

        var takenNames = graph.NamedArgumentTypes.Select(a => a.Name).Concat(graph.LocalVariables.Select(l => l.Name)).ToList();
        string name = NetPrintsUtil.GetUniqueName("Local", takenNames);
        owner.UndoRedo.Do(EditorCommands.AddLocalVariable(graph, name));
    }

    /// <summary>Removes a local variable (undoable); its existing getter/setter nodes are removed too.</summary>
    /// <param name="local">Local variable to remove.</param>
    public void RemoveLocalVariable(LocalVariableVM local)
    {
        if (OpenedExecutionGraph is { } graph)
        {
            owner.UndoRedo.Do(EditorCommands.RemoveLocalVariable(graph, local.Local));
        }
    }

    /// <summary>Unsubscribes from the owner and disposes the Method group.</summary>
    public void Dispose()
    {
        owner.PropertyChanged -= OnOwnerPropertyChanged;
        MethodVariables?.Dispose();
    }
}
