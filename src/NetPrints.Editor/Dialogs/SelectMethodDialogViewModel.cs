using NetPrints.Core;
using NetPrints.Editor.Controls;

namespace NetPrints.Editor.Dialogs;

/// <summary>
/// Chooses a method from the base types of a class (FR-095): <see cref="List"/> holds the filter and the rows, and its
/// pick and cancel close the dialog with the method or with nothing.
/// </summary>
public sealed class SelectMethodDialogViewModel : DialogViewModel<MethodSpecifier>
{
    /// <summary>The window title and shell title of the override dialog.</summary>
    public const string OverrideTitle = "Override method";

    /// <summary>The accept button's label of the override dialog.</summary>
    public const string OverrideLabel = "Override";

    private const string ObjectTypeName = "System.Object";

    /// <summary>Offers <paramref name="methods"/> grouped by their declaring type.</summary>
    /// <param name="methods">The methods to offer, nearest base type first. A method that a nearer base type already declares under the same name and parameter types is left out, since the nearer one overrides it.</param>
    /// <param name="overriddenNames">Names of the methods the class already has; their rows are dimmed and cannot be picked.</param>
    /// <param name="title">The window and shell title.</param>
    /// <param name="acceptLabel">The accept button's label.</param>
    public SelectMethodDialogViewModel(IEnumerable<MethodSpecifier> methods, IReadOnlySet<string>? overriddenNames = null, string title = OverrideTitle, string acceptLabel = OverrideLabel)
    {
        ArgumentNullException.ThrowIfNull(methods);
        Title = title;
        AcceptLabel = acceptLabel;
        IReadOnlySet<string> names = overriddenNames ?? new HashSet<string>();
        List<MethodPickerItem> items = [.. NearestFirst(methods).Select(method => MethodPickerItem.For(method, isOverridden: names.Contains(method.Name)))];
        List = new MethodPickerListViewModel(items);
        List.Picked += (_, item) => RequestClose(item.Method);
        List.Cancelled += (_, _) => RequestClose(null);
    }

    /// <summary>Gets the window and shell title.</summary>
    public string Title { get; }

    /// <summary>Gets the accept button's label.</summary>
    public string AcceptLabel { get; }

    /// <summary>Gets the filter box and the rows.</summary>
    public MethodPickerListViewModel List { get; }

    private static IEnumerable<MethodSpecifier> NearestFirst(IEnumerable<MethodSpecifier> methods)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return methods
            .OrderBy(method => string.Equals(method.DeclaringType.Name, ObjectTypeName, StringComparison.Ordinal))
            .Where(method => seen.Add(Key(method)))
            .ToList();
    }

    private static string Key(MethodSpecifier method) =>
        $"{method.Name}({string.Join(",", method.Parameters.Select(parameter => parameter.Value.FullCodeName))})";
}
