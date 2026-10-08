using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Core;
using NetPrints.Editor.UndoRedo;

namespace NetPrints.Editor.Events;

/// <summary>
/// An event graph in the class editor's event graph list (US4) and in the inspector (US8), the same light wrapper pattern as
/// <see cref="ClassEditor.MethodViewModel"/>: <see cref="Shell.ClassContext"/> owns creation, opening
/// and removal as undoable commands. With a rename delegate the name is changed as one undo step and a refused name
/// (<see cref="Error"/>) leaves the graph as it was.
/// </summary>
public sealed partial class EventGraphViewModel : ObservableObject, IDisposable
{
    private readonly ClassGraph cls;
    private readonly Action<EventGraph, string>? rename;
    private readonly UndoRedoStack? undoRedo;

    /// <summary>Wraps <paramref name="graph"/>.</summary>
    /// <param name="graph">The event graph.</param>
    /// <param name="cls">The class that owns the graph.</param>
    /// <param name="rename">Renames the graph as one undo step; without it the name is set on the graph alone.</param>
    /// <param name="undoRedo">The class's undo stack, whose undo and redo re-read the name; null when there is none.</param>
    public EventGraphViewModel(EventGraph graph, ClassGraph cls, Action<EventGraph, string>? rename = null, UndoRedoStack? undoRedo = null)
    {
        Graph = graph;
        this.cls = cls;
        this.rename = rename;
        this.undoRedo = undoRedo;
        if (undoRedo is not null)
        {
            undoRedo.Applied += OnUndoApplied;
        }
    }

    /// <summary>The wrapped model graph.</summary>
    public EventGraph Graph { get; }

    /// <summary>Gets the reason the last name was refused, or null when it was accepted.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Gets a value indicating whether the last name was refused.</summary>
    public bool HasError => Error is not null;

    /// <summary>
    /// The event graph's own label. Not a generated member: each entry
    /// (<see cref="NetPrints.Graph.EventEntryNode.EventName"/>) names its own generated method.
    /// </summary>
    public string Name
    {
        get => Graph.Name;
        set
        {
            if (Graph.Name != value)
            {
                if (rename is null)
                {
                    Graph.Name = value;
                    cls.MarkDirty();
                }
                else
                {
                    try
                    {
                        rename(Graph, value);
                        Error = null;
                    }
                    catch (ArgumentException refused)
                    {
                        Error = RefusalMessage.Of(refused);
                    }
                }
            }

            OnPropertyChanged();
        }
    }

    /// <summary>Stops following the class's undo stack.</summary>
    public void Dispose()
    {
        if (undoRedo is not null)
        {
            undoRedo.Applied -= OnUndoApplied;
        }
    }

    private void OnUndoApplied(object? sender, EventArgs e) => OnPropertyChanged(nameof(Name));
}
