using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Editor.Contributions;

namespace NetPrints.Editor.Shell;

/// <summary>A registered tool panel as the shell shows it.</summary>
public sealed partial class PanelViewModel : ObservableObject
{
    /// <summary>Creates the panel for a descriptor.</summary>
    /// <param name="descriptor">The registered panel.</param>
    /// <param name="content">The view model the panel shows.</param>
    public PanelViewModel(PanelDescriptor descriptor, object content)
    {
        Id = descriptor.Id;
        Title = descriptor.Title;
        IconId = descriptor.IconId;
        DefaultDock = descriptor.DefaultDock;
        Order = descriptor.Order;
        Content = content;
    }

    /// <summary>Gets the namespaced panel id.</summary>
    public string Id { get; }

    /// <summary>Gets the tab title.</summary>
    public string Title { get; }

    /// <summary>Gets the Icon id (see IconIds), or null.</summary>
    public string? IconId { get; }

    /// <summary>Gets where the panel docks by default.</summary>
    public PanelDock DefaultDock { get; }

    /// <summary>Gets the position among the panels of its default dock.</summary>
    public int Order { get; }

    /// <summary>Gets the view model the panel shows; its view is found by data template.</summary>
    public object Content { get; }

    /// <summary>Gets or sets a value indicating whether the panel is shown.</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;
}
