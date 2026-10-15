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
public sealed partial class LocalVariableViewModel : VariableRowViewModel, IDisposable
{
    private const string RefusedNameMessage = "That name is already used in this method or is not a valid identifier.";

    private readonly ExecutionGraph graph;
    private readonly ClassEditorServices services;

    /// <summary>
    /// Wraps <paramref name="local"/> and subscribes to its property-changed event.
    /// </summary>
    /// <param name="local">Local variable to wrap.</param>
    /// <param name="graph">Method or constructor graph the local belongs to.</param>
    /// <param name="services">Narrow services shared with the owning class editor (FR-038).</param>
    public LocalVariableViewModel(LocalVariable local, ExecutionGraph graph, ClassEditorServices services)
        : base(services)
    {
        Local = local;
        this.graph = graph;
        this.services = services;
        ((INotifyPropertyChanged)local).PropertyChanged += OnLocalPropertyChanged;
    }

    /// <summary>The wrapped model local variable.</summary>
    public LocalVariable Local { get; }

    /// <summary>A fresh snapshot of the local's specifier, for drag &amp; drop onto the canvas (PAR-57).</summary>
    public VariableSpecifier Specifier => Local.ToSpecifier();

    /// <summary>Gets a value indicating whether the owning panel has disposed this row, so it no longer stands for a variable of the opened method.</summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc/>
    public override TypeSpecifier Type => Local.Type;

    /// <summary>
    /// The local's name. Setting it renames the local and retargets its existing getter/setter nodes
    /// (undoable). A name that did not change is ignored; one that <see cref="ExecutionGraph.IsLocalNameAvailable"/>
    /// rejects (a duplicate, a parameter name, or not a valid identifier) leaves the local as it was and sets <see cref="VariableRowViewModel.Error"/>.
    /// </summary>
    public override string Name
    {
        get => Local.Name;
        set
        {
            if (value == Local.Name)
            {
                OnPropertyChanged();
                return;
            }

            if (!graph.IsLocalNameAvailable(value, Local))
            {
                Error = RefusedNameMessage;
                OnPropertyChanged();
                return;
            }

            Error = null;
            services.UndoRedo.Do(EditorCommands.RenameLocalVariable(graph, Local, value));
        }
    }

    /// <summary>
    /// Opens the type-selection dialog and retypes the local (undoable); its existing getter/setter
    /// nodes are replaced with fresh ones of the new type.
    /// </summary>
    [RelayCommand]
    private async Task RetypeAsync()
    {
        var chosen = await services.Context.Dialogs.SelectTypeAsync(services.Context.Reflection.NonStaticTypes, Type);
        if (chosen is not null && chosen != Type)
        {
            services.UndoRedo.Do(EditorCommands.RetypeLocalVariable(graph, Local, chosen));
        }
    }

    /// <inheritdoc/>
    protected override void RemoveVariable() => services.UndoRedo.Do(EditorCommands.RemoveLocalVariable(graph, Local));

    private void OnLocalPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Local.Name):
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(Specifier));
                break;
            case nameof(Local.Type):
                NotifyTypeChanged();
                OnPropertyChanged(nameof(Specifier));
                break;
        }
    }

    /// <summary>Unsubscribes from the wrapped local's property-changed event.</summary>
    public void Dispose()
    {
        IsDisposed = true;
        ((INotifyPropertyChanged)Local).PropertyChanged -= OnLocalPropertyChanged;
    }
}
