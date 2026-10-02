using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Core;

namespace NetPrints.Editor.Events;

/// <summary>
/// An event graph in the class editor's event graph list (US4), the same light wrapper pattern as
/// <see cref="ClassEditor.MethodViewModel"/>: <see cref="Shell.ClassContext"/> owns creation, opening
/// and removal as undoable commands, this VM only carries the label.
/// </summary>
public sealed class EventGraphViewModel(EventGraph graph, ClassGraph cls) : ObservableObject
{
    /// <summary>The wrapped model graph.</summary>
    public EventGraph Graph { get; } = graph;

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
                Graph.Name = value;
                cls.MarkDirty();
                OnPropertyChanged();
            }
        }
    }
}
