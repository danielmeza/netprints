using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Extensibility.Nodes;

/// <summary>
/// A node-search entry of a node kind (extension-points.md §2).
/// </summary>
/// <param name="Category">Search category the entry is grouped under.</param>
/// <param name="DisplayName">Text shown for the entry.</param>
/// <param name="IconKey">File name of the 16-px icon in the editor assets, or <see langword="null"/> for the default icon.</param>
/// <param name="Create">Creates the node in the target graph; node constructors add the node to the graph themselves.</param>
public sealed record NodeSuggestion(string Category, string DisplayName, string? IconKey, Func<NodeGraph, Node> Create);
