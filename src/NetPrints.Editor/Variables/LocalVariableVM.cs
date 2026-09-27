using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.UndoRedo;

namespace NetPrints.Editor.Variables;

/// <summary>
/// A local variable in the Variables panel's "Method: &lt;name&gt;" group (US5, sub-phase H).
/// </summary>
public sealed partial class LocalVariableVM : ObservableObject, IDisposable
{
    private readonly ExecutionGraph graph;
    private readonly ClassEditorVM owner;

    /// <summary>
    /// Wraps <paramref name="local"/> and subscribes to its property-changed event.
    /// </summary>
    /// <param name="local">Local variable to wrap.</param>
    /// <param name="graph">Method or constructor graph the local belongs to.</param>
    /// <param name="owner">Class editor view model that owns the opened graph.</param>
    public LocalVariableVM(LocalVariable local, ExecutionGraph graph, ClassEditorVM owner)
    {
        Local = local;
        this.graph = graph;
        this.owner = owner;
        ((INotifyPropertyChanged)local).PropertyChanged += OnLocalPropertyChanged;
    }

    /// <summary>The wrapped model local variable.</summary>
    public LocalVariable Local { get; }

    /// <summary>A fresh snapshot of the local's specifier, for drag &amp; drop onto the canvas (PAR-57).</summary>
    public VariableSpecifier Specifier => Local.ToSpecifier();

    /// <summary>The local's type.</summary>
    public TypeSpecifier Type => Local.Type;

    /// <summary>
    /// The local's name. Setting it renames the local and retargets its existing getter/setter nodes
    /// (undoable). A name that did not change, or that <see cref="ExecutionGraph.IsLocalNameAvailable"/>
    /// rejects (a duplicate, a parameter name, or not a valid identifier), is ignored and the display
    /// reverts to the committed name.
    /// </summary>
    public string Name
    {
        get => Local.Name;
        set
        {
            if (value == Local.Name || !graph.IsLocalNameAvailable(value, Local))
            {
                OnPropertyChanged();
                return;
            }

            owner.UndoRedo.Do(EditorCommands.RenameLocalVariable(graph, Local, value));
        }
    }

    /// <summary>
    /// Opens the type-selection dialog and retypes the local (undoable); its existing getter/setter
    /// nodes are replaced with fresh ones of the new type.
    /// </summary>
    [RelayCommand]
    private async Task RetypeAsync()
    {
        var chosen = await owner.Context.Dialogs.SelectTypeAsync(owner.Context.Reflection.NonStaticTypes, Type);
        if (chosen is not null && chosen != Type)
        {
            owner.UndoRedo.Do(EditorCommands.RetypeLocalVariable(graph, Local, chosen));
        }
    }

    /// <summary>Removes the local variable (undoable); its existing getter/setter nodes are removed too.</summary>
    [RelayCommand]
    private void Remove() => owner.VariablesPanel.RemoveLocalVariable(this);

    private void OnLocalPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Local.Name):
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(Specifier));
                break;
            case nameof(Local.Type):
                OnPropertyChanged(nameof(Type));
                OnPropertyChanged(nameof(Specifier));
                break;
        }
    }

    /// <summary>Unsubscribes from the wrapped local's property-changed event.</summary>
    public void Dispose() => ((INotifyPropertyChanged)Local).PropertyChanged -= OnLocalPropertyChanged;
}
