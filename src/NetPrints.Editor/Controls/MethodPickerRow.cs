namespace NetPrints.Editor.Controls;

/// <summary>
/// A row of <see cref="MethodPickerListViewModel.Rows"/>: a group header, or a method under it. The list is flat because
/// Avalonia's <c>ListBox</c> has no grouping.
/// </summary>
public sealed class MethodPickerRow
{
    private MethodPickerRow(string text, MethodPickerItem? item)
    {
        Text = text;
        Item = item;
    }

    /// <summary>Gets the header's type name, or the method's signature.</summary>
    public string Text { get; }

    /// <summary>Gets the method this row stands for, or <see langword="null"/> on a header.</summary>
    public MethodPickerItem? Item { get; }

    /// <summary>Gets a value indicating whether this row is a group header; headers cannot be selected.</summary>
    public bool IsHeader => Item is null;

    /// <summary>Gets a value indicating whether this is the overload in use.</summary>
    public bool IsCurrent => Item?.IsCurrent ?? false;

    /// <summary>Gets a value indicating whether the class already overrides this method; the row is dimmed.</summary>
    public bool IsOverridden => Item?.IsOverridden ?? false;

    /// <summary>Gets a value indicating whether this method is abstract.</summary>
    public bool IsAbstract => Item?.IsAbstract ?? false;

    /// <summary>Gets a value indicating whether this row can be selected and picked: a method that is not overridden.</summary>
    public bool IsPickable => Item is { IsOverridden: false };

    /// <summary>Creates the header of a group.</summary>
    /// <param name="typeName">The group's type name.</param>
    /// <returns>The header row.</returns>
    public static MethodPickerRow Header(string typeName) => new(typeName, null);

    /// <summary>Creates the row of a method.</summary>
    /// <param name="item">The method.</param>
    /// <returns>The method row.</returns>
    public static MethodPickerRow ForMethod(MethodPickerItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new MethodPickerRow(item.Signature, item);
    }
}
