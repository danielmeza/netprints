using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NetPrints.Editor.Controls;

/// <summary>
/// The state of <c>MethodPickerList</c> (FR-094), shared by the override dialog and the overload flyout: a filter box
/// and one flat list of group headers and method rows. The filter runs here, not in the view. The host listens to
/// <see cref="Picked"/> and <see cref="Cancelled"/>.
/// </summary>
public sealed partial class MethodPickerListViewModel : ObservableObject
{
    private readonly List<MethodPickerGroup> groups;
    private readonly bool showGroupHeaders;

    /// <summary>Creates the list over <paramref name="items"/> and selects the first pickable row.</summary>
    /// <param name="items">The methods to offer; the groups keep the order in which their first item appears.</param>
    /// <param name="showGroupHeaders">Whether a header row names each group; a list of one type's overloads can leave them out.</param>
    public MethodPickerListViewModel(IEnumerable<MethodPickerItem> items, bool showGroupHeaders = true)
    {
        ArgumentNullException.ThrowIfNull(items);
        this.showGroupHeaders = showGroupHeaders;
        groups = [.. items.GroupBy(item => item.DeclaringTypeName, StringComparer.Ordinal).Select(group => new MethodPickerGroup(group.Key, Sorted(group)))];
        Rebuild();
    }

    /// <summary>Raised once when <see cref="PickCommand"/> runs, with the picked method.</summary>
    public event EventHandler<MethodPickerItem>? Picked;

    /// <summary>Raised when <see cref="CancelCommand"/> runs.</summary>
    public event EventHandler? Cancelled;

    /// <summary>Gets or sets the text rows are narrowed by, matched ordinally and ignoring case against the name and the signature.</summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = "";

    /// <summary>Gets the rows now shown: group headers and methods, groups in the caller's order.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial IReadOnlyList<MethodPickerRow> Rows { get; private set; } = [];

    /// <summary>Gets or sets the selected row; a header or a row that cannot be picked is refused and the selection stays.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PickCommand))]
    public partial MethodPickerRow? Selected { get; set; }

    /// <summary>Gets a value indicating whether no row matches the filter.</summary>
    public bool IsEmpty => Rows.Count == 0;

    partial void OnFilterChanged(string value) => Rebuild();

    partial void OnSelectedChanged(MethodPickerRow? oldValue, MethodPickerRow? newValue)
    {
        if (newValue is { IsPickable: false })
        {
            Selected = oldValue;
        }
    }

    private bool CanPick() => Selected is { IsPickable: true };

    /// <summary>Reports the selected method through <see cref="Picked"/>.</summary>
    [RelayCommand(CanExecute = nameof(CanPick))]
    private void Pick()
    {
        if (Selected?.Item is { } item)
        {
            Picked?.Invoke(this, item);
        }
    }

    /// <summary>Reports a cancel through <see cref="Cancelled"/>.</summary>
    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

    private static List<MethodPickerItem> Sorted(IEnumerable<MethodPickerItem> items) =>
        [.. items
            .OrderByDescending(item => item.IsCurrent)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ParameterCount)
            .ThenBy(item => item.Signature, StringComparer.Ordinal)];

    private bool Matches(MethodPickerItem item) =>
        Filter.Length == 0
        || item.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase)
        || item.Signature.Contains(Filter, StringComparison.OrdinalIgnoreCase);

    private void Rebuild()
    {
        var built = new List<MethodPickerRow>();
        foreach (MethodPickerGroup group in groups)
        {
            List<MethodPickerRow> methods = [.. group.Items.Where(Matches).Select(MethodPickerRow.ForMethod)];
            if (methods.Count == 0)
            {
                continue;
            }

            if (showGroupHeaders)
            {
                built.Add(MethodPickerRow.Header(group.TypeName));
            }

            built.AddRange(methods);
        }

        Rows = built;
        Selected = built.Find(row => row.IsPickable);
    }

    private sealed record MethodPickerGroup(string TypeName, List<MethodPickerItem> Items);
}
