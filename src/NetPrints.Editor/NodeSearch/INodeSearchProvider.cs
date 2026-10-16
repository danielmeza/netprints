namespace NetPrints.Editor.NodeSearch;

/// <summary>A source of node search entries, owned by the node kind whose nodes it offers.</summary>
internal interface INodeSearchProvider
{
    /// <summary>Gets the stable id of the provider.</summary>
    string Id { get; }

    /// <summary>Gets the node kind that owns the provider, or <see cref="NodeSearchProviders.AnyKind"/> when it serves the kinds of the registry.</summary>
    string OwnerKind { get; }

    /// <summary>Gets the entries the provider offers for the search.</summary>
    /// <param name="context">The graph and pin the search is opened for.</param>
    /// <returns>The entries, in display order; empty when the provider has nothing for this pin or graph.</returns>
    IEnumerable<NodeSearchEntry> Suggest(NodeSearchContext context);
}
