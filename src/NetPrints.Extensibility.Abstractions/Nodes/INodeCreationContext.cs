using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Extensibility.Nodes;

/// <summary>What a node search entry may use to create its node: the graph, the prompts it may show first, and the undoable creation step.</summary>
[Experimental(ExperimentalApiIds.NodeSearchProviders, UrlFormat = ExperimentalApiIds.UrlFormat)]
public interface INodeCreationContext
{
    /// <summary>Creates a node with a factory as one undo step, clamped to the canvas and connected to <paramref name="pin"/>.</summary>
    /// <param name="position">Where the node is created (graph coordinates).</param>
    /// <param name="pin">Pin the search was opened for, or <see langword="null"/>.</param>
    /// <param name="factory">Creates the node in the graph.</param>
    /// <returns>The new node.</returns>
    Node Create(GraphPoint position, NodePin? pin, Func<NodeGraph, Node> factory);

    /// <summary>Asks for a type.</summary>
    /// <returns>The chosen type, or <see langword="null"/> when cancelled.</returns>
    Task<TypeSpecifier?> SelectTypeAsync();

    /// <summary>Asks for one of <paramref name="methods"/>.</summary>
    /// <param name="methods">The methods to choose from.</param>
    /// <returns>The chosen method, or <see langword="null"/> when cancelled.</returns>
    Task<MethodSpecifier?> SelectMethodAsync(IEnumerable<MethodSpecifier> methods);
}
