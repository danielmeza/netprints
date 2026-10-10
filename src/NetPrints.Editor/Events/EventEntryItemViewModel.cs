using CommunityToolkit.Mvvm.Input;
using NetPrints.Graph;

namespace NetPrints.Editor.Events;

/// <summary>One line of the event graph inspector's entry list: the entry's name, kind and argument count, with a Select action.</summary>
public sealed partial class EventEntryItemViewModel
{
    private readonly Action<EventEntryNode> select;

    /// <summary>Wraps <paramref name="entry"/>.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="select">Selects the entry on the canvas.</param>
    public EventEntryItemViewModel(EventEntryNode entry, Action<EventEntryNode> select)
    {
        Entry = entry;
        this.select = select;
    }

    /// <summary>Gets the entry.</summary>
    public EventEntryNode Entry { get; }

    /// <summary>Gets the event's name.</summary>
    public string Name => Entry.EventName;

    /// <summary>Gets <c>Override</c> for an override of a base method, otherwise <c>Custom event</c>.</summary>
    public string Kind => Entry.OverriddenMethod is not null ? "Override" : "Custom event";

    /// <summary>Gets the number of arguments the event declares.</summary>
    public int ArgumentCount => Entry.DeclaredArguments.Count;

    /// <summary>Gets the line the list shows: name, kind and argument count.</summary>
    public string Summary => $"{Name} ({Kind}, {ArgumentCount} {(ArgumentCount == 1 ? "argument" : "arguments")})";

    /// <summary>Selects the entry on the canvas.</summary>
    [RelayCommand]
    private void Select() => select(Entry);
}
