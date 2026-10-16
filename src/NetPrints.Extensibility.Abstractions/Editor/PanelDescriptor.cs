using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Extensibility.Editor;

/// <summary>A dockable tool panel.</summary>
/// <param name="Id">The namespaced id.</param>
/// <param name="Title">The non-empty tab title. A plain string is a literal that takes the convention key <c>Panel.&lt;id&gt;.Title</c> and shows itself when no resource has the key.</param>
/// <param name="CreateViewModel">Creates the panel's view model; its view is found by data template.</param>
/// <param name="DefaultDock">Where the panel docks by default.</param>
/// <param name="Order">The position among the panels of the same dock.</param>
/// <param name="IconId">Icon id (see IconIds), or null.</param>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public sealed record PanelDescriptor(
    string Id,
    LocalizedText Title,
    Func<IPanelContext, object> CreateViewModel,
    PanelDock DefaultDock,
    int Order,
    string? IconId = null)
{
    /// <summary>Gets the title with its convention key (FR-027): a literal title is keyed <c>Panel.&lt;id&gt;.Title</c>.</summary>
    public LocalizedText Title { get; init; } = ConventionKeys.Panel(Id, Title);
}
