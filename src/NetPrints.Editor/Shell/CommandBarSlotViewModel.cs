using CommunityToolkit.Mvvm.ComponentModel;

namespace NetPrints.Editor.Shell;

/// <summary>One slot of the command bar: the commands that share it, and the one it shows now.</summary>
public sealed partial class CommandBarSlotViewModel : ObservableObject
{
    internal CommandBarSlotViewModel(IReadOnlyList<CommandEntryViewModel> items)
    {
        Items = items;
        Current = items[0];
    }

    /// <summary>Gets the commands sharing the slot, in registration order.</summary>
    public IReadOnlyList<CommandEntryViewModel> Items { get; }

    /// <summary>Gets the command the slot shows: the first of the slot's commands that can run, else the first.</summary>
    [ObservableProperty]
    public partial CommandEntryViewModel Current { get; private set; }

    internal void Refresh()
    {
        foreach (CommandEntryViewModel item in Items)
        {
            item.Refresh();
        }

        Current = Items.FirstOrDefault(item => item.IsEnabled) ?? Items[0];
    }
}
