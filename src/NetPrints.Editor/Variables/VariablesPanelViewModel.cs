using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
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
public sealed partial class VariablesPanelViewModel : ObservableObject, IDisposable, IRecipient<SelectInspectorMessage>
{
    private readonly ClassEditorServices services;
    private readonly Action createVariable;
    private readonly Action<Variable>? selectVariable;
    private readonly Action<LocalVariableViewModel?>? selectLocal;
    private ExecutionGraph? openedGraph;
    private bool syncingSelection;

    /// <summary>
    /// Creates the panel from <paramref name="classVariables"/>, with no method or constructor graph open yet (the owner
    /// pushes each change through <see cref="OnOpenedGraphChanged"/>, FR-038).
    /// </summary>
    /// <param name="services">Narrow services shared with the owning class (FR-038).</param>
    /// <param name="classVariables">View models for the class's variables (the "Class" group).</param>
    /// <param name="createVariable">Adds a variable to the class; run by <see cref="CreateVariableCommand"/>.</param>
    /// <param name="selectVariable">Called when a variable row asks for its inspector; <see langword="null"/> ignores the request.</param>
    /// <param name="selectLocal">Called with the local variable row that was selected, or <see langword="null"/> when none is; <see langword="null"/> ignores it.</param>
    public VariablesPanelViewModel(
        ClassEditorServices services,
        ObservableViewModelCollection<MemberVariableViewModel, Variable> classVariables,
        Action createVariable,
        Action<Variable>? selectVariable = null,
        Action<LocalVariableViewModel?>? selectLocal = null)
    {
        ArgumentNullException.ThrowIfNull(createVariable);
        this.services = services;
        this.createVariable = createVariable;
        this.selectVariable = selectVariable;
        this.selectLocal = selectLocal;
        ClassVariables = classVariables;
        services.Messenger.Register<SelectInspectorMessage>(this);
        RebuildMethodGroup();
    }

    /// <summary>The class's member variables (the "Class" group).</summary>
    public ObservableViewModelCollection<MemberVariableViewModel, Variable> ClassVariables { get; }

    /// <summary>
    /// The opened method's or constructor's local variables (the "Method" group), or
    /// <see langword="null"/> when no method or constructor graph is open.
    /// </summary>
    public ObservableViewModelCollection<LocalVariableViewModel, LocalVariable>? MethodVariables { get; private set; }

    /// <summary>Whether the "Method" group has a graph to show.</summary>
    public bool HasMethodGroup => MethodVariables is not null;

    /// <summary>The "Local variables: &lt;name&gt;" header text, or "" when <see cref="HasMethodGroup"/> is <see langword="false"/>.</summary>
    public string MethodGroupHeader => openedGraph is { } graph ? $"Local variables: {GraphName(graph)}" : "";

    /// <summary>Gets or sets the selected member variable row; selecting one shows its inspector and deselects the local variable.</summary>
    [ObservableProperty]
    public partial MemberVariableViewModel? SelectedClassVariable { get; set; }

    /// <summary>Gets or sets the selected local variable row; selecting one shows it in the inspector and deselects the member variable.</summary>
    [ObservableProperty]
    public partial LocalVariableViewModel? SelectedLocalVariable { get; set; }

    partial void OnSelectedClassVariableChanged(MemberVariableViewModel? value)
    {
        if (syncingSelection || value is null)
        {
            return;
        }

        syncingSelection = true;
        SelectedLocalVariable = null;
        syncingSelection = false;
        selectVariable?.Invoke(value.Variable);
    }

    partial void OnSelectedLocalVariableChanged(LocalVariableViewModel? value)
    {
        if (syncingSelection)
        {
            return;
        }

        if (value is not null)
        {
            syncingSelection = true;
            SelectedClassVariable = null;
            syncingSelection = false;
        }

        selectLocal?.Invoke(value);
    }

    /// <summary>Removes the selected member variable row (Delete), unless there is none or its name is being edited.</summary>
    [RelayCommand]
    private void RemoveSelectedClassVariable() => RunOn(SelectedClassVariable?.RemoveCommand);

    /// <summary>Renames the selected member variable row in place (F2).</summary>
    [RelayCommand]
    private void RenameSelectedClassVariable() => RunOn(SelectedClassVariable?.BeginRenameCommand);

    /// <summary>Removes the selected local variable row (Delete), unless there is none or its name is being edited.</summary>
    [RelayCommand]
    private void RemoveSelectedLocalVariable() => RunOn(SelectedLocalVariable?.RemoveCommand);

    /// <summary>Renames the selected local variable row in place (F2).</summary>
    [RelayCommand]
    private void RenameSelectedLocalVariable() => RunOn(SelectedLocalVariable?.BeginRenameCommand);

    private static void RunOn(IRelayCommand? command)
    {
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
        }
    }

    /// <summary>Highlights the row of the variable the inspector shows, without asking for the inspector again.</summary>
    /// <param name="selection">The shell's selection: a variable, a local variable row, or anything else to clear the highlight.</param>
    public void FollowSelection(object? selection)
    {
        syncingSelection = true;
        SelectedClassVariable = selection is Variable variable ? ClassVariables.FirstOrDefault(row => ReferenceEquals(row.Variable, variable)) : null;
        SelectedLocalVariable = selection is LocalVariableViewModel { IsDisposed: false } local && MethodVariables?.Contains(local) == true ? local : null;
        syncingSelection = false;
    }

    private static string GraphName(ExecutionGraph graph) => graph is MethodGraph method ? method.Name : graph.ToString() ?? "";

    /// <summary>Rebuilds the Method group for the class editor's newly opened graph (FR-038).</summary>
    /// <param name="graph">The opened graph's model, or <see langword="null"/> when none is open.</param>
    public void OnOpenedGraphChanged(ExecutionGraph? graph)
    {
        openedGraph = graph;
        RebuildMethodGroup();
    }

    private void RebuildMethodGroup()
    {
        MethodVariables?.Dispose();
        var graph = openedGraph;
        MethodVariables = graph is null
            ? null
            : new ObservableViewModelCollection<LocalVariableViewModel, LocalVariable>(graph.LocalVariables,
                l => new LocalVariableViewModel(l, graph, services), l => l.Dispose());

        OnPropertyChanged(nameof(MethodVariables));
        OnPropertyChanged(nameof(HasMethodGroup));
        OnPropertyChanged(nameof(MethodGroupHeader));
    }

    /// <inheritdoc/>
    void IRecipient<SelectInspectorMessage>.Receive(SelectInspectorMessage message) => selectVariable?.Invoke(message.Target.Variable);

    /// <summary>Creates a variable of type <c>object</c> with a unique default name in the class (undoable).</summary>
    [RelayCommand]
    private void CreateVariable() => createVariable();

    /// <summary>Creates a local variable of type <c>object</c> with a unique default name (undoable).</summary>
    [RelayCommand]
    private void CreateLocalVariable()
    {
        if (openedGraph is not { } graph)
        {
            return;
        }

        var takenNames = graph.NamedArgumentTypes.Select(a => a.Name).Concat(graph.LocalVariables.Select(l => l.Name)).ToList();
        string name = NetPrintsUtil.GetUniqueName("Local", takenNames);
        services.UndoRedo.Do(EditorCommands.AddLocalVariable(graph, name));
    }

    /// <summary>Stops listening to the class's inspector requests and disposes the Method group.</summary>
    public void Dispose()
    {
        services.Messenger.UnregisterAll(this);
        MethodVariables?.Dispose();
    }
}
