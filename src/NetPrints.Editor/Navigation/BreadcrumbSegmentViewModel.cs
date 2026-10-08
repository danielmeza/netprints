using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NetPrints.Editor.Navigation;

/// <summary>One segment of the breadcrumbs: the project, a class or a graph.</summary>
public sealed partial class BreadcrumbSegmentViewModel : ObservableObject
{
    private readonly Func<string> readName;
    private readonly Func<object, bool> reveal;

    internal BreadcrumbSegmentViewModel(object model, Func<string> readName, Func<object, bool> reveal)
    {
        Model = model;
        this.readName = readName;
        this.reveal = reveal;
        Name = readName();
    }

    /// <summary>Gets the project, class or graph (or the variable it belongs to) the segment stands for.</summary>
    public object Model { get; }

    /// <summary>Gets the name the segment shows; it follows renames.</summary>
    [ObservableProperty]
    public partial string Name { get; private set; }

    /// <summary>Gets or sets a value indicating whether this is the last segment, which has no separator after it.</summary>
    [ObservableProperty]
    public partial bool IsLast { get; internal set; }

    internal void Refresh() => Name = readName();

    [RelayCommand]
    private void Reveal() => reveal(Model);
}
