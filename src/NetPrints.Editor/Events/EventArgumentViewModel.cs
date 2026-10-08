using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Editor.Events;

/// <summary>One argument of a custom event entry in the entry inspector (FR-073): its name and type, with the edits that move or remove it.</summary>
public sealed partial class EventArgumentViewModel : ObservableObject
{
    private readonly EventEntryInspectorViewModel owner;
    private readonly int index;

    internal EventArgumentViewModel(EventEntryInspectorViewModel owner, int index, EventArgument argument, bool isEditable, bool isLast)
    {
        this.owner = owner;
        this.index = index;
        Type = argument.Type;
        Argument = argument;
        IsEditable = isEditable;
        CanMoveUp = isEditable && index > 0;
        CanMoveDown = isEditable && !isLast;
    }

    /// <summary>Gets the argument as the entry has it.</summary>
    public EventArgument Argument { get; }

    /// <summary>Gets or sets the argument's name; a refused name is shown by the inspector and the name stays.</summary>
    public string Name
    {
        get => Argument.Name;
        set
        {
            if (IsEditable)
            {
                owner.RenameArgument(index, value);
            }

            OnPropertyChanged();
        }
    }

    /// <summary>Gets the argument's type.</summary>
    public TypeSpecifier Type { get; }

    /// <summary>Gets the type as the short name the inspector shows.</summary>
    public string TypeName => Type.ShortName;

    /// <summary>Gets a value indicating whether the argument can be edited: a custom event's, not an override's.</summary>
    public bool IsEditable { get; }

    /// <summary>Gets a value indicating whether there is an argument before this one to move past.</summary>
    public bool CanMoveUp { get; }

    /// <summary>Gets a value indicating whether there is an argument after this one to move past.</summary>
    public bool CanMoveDown { get; }

    [RelayCommand(CanExecute = nameof(IsEditable))]
    private void Remove() => owner.RemoveArgument(index);

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp() => owner.MoveArgument(index, -1);

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown() => owner.MoveArgument(index, 1);

    [RelayCommand(CanExecute = nameof(IsEditable))]
    private Task ChangeTypeAsync() => owner.ChangeArgumentTypeAsync(index);
}
