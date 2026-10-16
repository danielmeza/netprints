using NetPrints.Extensibility.Nodes;

namespace NetPrints.Editor.NodeSearch;

/// <summary>A source of node search entries of the editor itself, owned by the node kind whose nodes it offers.</summary>
internal interface INodeSearchProvider : INodeSuggestionProvider
{
    /// <summary>Gets the node kind that owns the provider, or <see cref="NodeSearchProviders.AnyKind"/> when it serves the kinds of the registry.</summary>
    string OwnerKind { get; }
}
