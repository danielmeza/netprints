using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Extensibility.Nodes;

/// <summary>
/// A suggestion a provider offers: the category it is grouped under, the text and icon of its row, and the value that creates the node.
/// </summary>
/// <param name="Category">Category the entry is grouped under.</param>
/// <param name="Text">Text of the row.</param>
/// <param name="IconKey">Icon id of the row.</param>
[Experimental(ExperimentalApiIds.NodeSearchProviders, UrlFormat = ExperimentalApiIds.UrlFormat)]
public abstract record NodeSearchEntry(string Category, string Text, string IconKey)
{
    /// <summary>Gets extra words the search matches besides the category and the text.</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>Gets the C# feature the entry needs, or <see langword="null"/> when it works on every target.</summary>
    [Experimental(ExperimentalApiIds.CSharpFeatures, UrlFormat = ExperimentalApiIds.UrlFormat)]
    public CSharpFeature? Requires { get; init; }

    /// <summary>Creates the node of this entry, asking for a type or method first where the entry needs one.</summary>
    /// <param name="context">The prompts and creation step the entry may use.</param>
    /// <param name="position">Where the node is created (graph coordinates).</param>
    /// <param name="pin">Pin the search was opened for, or <see langword="null"/>.</param>
    /// <returns>The new node, or <see langword="null"/> when the prompt was cancelled or the entry opens a chooser instead.</returns>
    public abstract Task<Node?> CreateAsync(INodeCreationContext context, GraphPoint position, NodePin? pin);
}
