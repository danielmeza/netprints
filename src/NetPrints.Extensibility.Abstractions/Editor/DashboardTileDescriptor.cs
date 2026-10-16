using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Extensibility.Editor;

/// <summary>A tile on the start page.</summary>
/// <param name="Id">The namespaced id.</param>
/// <param name="Title">The non-empty tile title.</param>
/// <param name="Order">The position on the start page.</param>
/// <param name="CreateViewModel">Creates the tile's view model; its view is found by data template.</param>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public sealed record DashboardTileDescriptor(string Id, string Title, int Order, Func<IStartPageContext, object> CreateViewModel);
