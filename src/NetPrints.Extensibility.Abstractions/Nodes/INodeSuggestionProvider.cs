using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Extensibility.Nodes;

/// <summary>
/// A source of node search entries. An extension adds one with <c>IExtensionBuilder.AddSuggestionProvider</c>; the editor builds
/// the providers once for each extension registry and asks each one on every search (FR-021).
/// </summary>
[Experimental(ExperimentalApiIds.NodeSearchProviders, UrlFormat = ExperimentalApiIds.UrlFormat)]
public interface INodeSuggestionProvider
{
    /// <summary>Gets the stable id of the provider; for an extension's provider it starts with the manifest id.</summary>
    string Id { get; }

    /// <summary>Gets the entries the provider offers for the search.</summary>
    /// <param name="context">The graph and pin the search is opened for.</param>
    /// <returns>The entries, in display order; empty when the provider has nothing for this pin or graph.</returns>
    IEnumerable<NodeSearchEntry> Suggest(NodeSearchContext context);
}
