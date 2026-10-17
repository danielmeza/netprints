using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Extensibility.Editor;

/// <summary>A tile on the start page.</summary>
/// <param name="Id">The namespaced id.</param>
/// <param name="Title">The non-empty tile title. A plain string is a literal that takes the convention key <c>Tile.&lt;id&gt;.Title</c> and shows itself when no resource has the key.</param>
/// <param name="Order">The position on the start page.</param>
/// <param name="CreateViewModel">Creates the tile's view model; its view is found by data template.</param>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public sealed record DashboardTileDescriptor(string Id, LocalizedText Title, int Order, Func<IStartPageContext, object> CreateViewModel)
{
    /// <summary>Gets the title with its convention key (FR-027): a literal title is keyed <c>Tile.&lt;id&gt;.Title</c>.</summary>
    public LocalizedText Title { get; init; } = ConventionKeys.Tile(Id, Title);
}
