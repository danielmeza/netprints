namespace NetPrints.Editor.Contributions;

/// <summary>A dockable tool panel.</summary>
/// <param name="Id">The namespaced id.</param>
/// <param name="Title">The non-empty tab title.</param>
/// <param name="CreateViewModel">Creates the panel's view model; its view is found by data template.</param>
/// <param name="DefaultDock">Where the panel docks by default.</param>
/// <param name="Order">The position among the panels of the same dock.</param>
/// <param name="IconId">Icon id (see IconIds), or null.</param>
public sealed record PanelDescriptor(
    string Id,
    string Title,
    Func<IServiceProvider, object> CreateViewModel,
    PanelDock DefaultDock,
    int Order,
    string? IconId = null);
