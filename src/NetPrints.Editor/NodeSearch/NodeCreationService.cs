using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Graph;

namespace NetPrints.Editor.NodeSearch;

/// <summary>Creates the node of a chosen search entry; the one place that turns an entry into a node.</summary>
internal interface INodeCreationService
{
    /// <summary>Creates a node with a factory as one undo step, clamped to the canvas and connected to <paramref name="pin"/>.</summary>
    /// <param name="position">Where the node is created (graph coordinates).</param>
    /// <param name="pin">Pin the search was opened for, or <see langword="null"/>.</param>
    /// <param name="factory">Creates the node in the graph.</param>
    /// <returns>The new node.</returns>
    Node Create(GraphPoint position, NodePin? pin, Func<NodeGraph, Node> factory);

    /// <summary>Creates the node of <paramref name="entry"/>, asking for a type or method first where the entry needs one.</summary>
    /// <param name="entry">The chosen entry.</param>
    /// <param name="position">Where the node is created (graph coordinates).</param>
    /// <param name="pin">Pin the search was opened for, or <see langword="null"/>.</param>
    /// <returns>The new node, or <see langword="null"/> when the prompt was cancelled or the entry opens a chooser instead.</returns>
    Task<Node?> CreateAsync(NodeSearchEntry entry, GraphPoint position, NodePin? pin);
}

/// <summary>What a search entry may use to create its node.</summary>
/// <param name="Graph">The graph view model the node is created in.</param>
/// <param name="Prompts">The questions the entry may ask first.</param>
/// <param name="Service">The creation service that adds a factory-built node as one undo step.</param>
internal sealed record NodeCreationContext(NodeGraphViewModel Graph, INodeSearchPrompts Prompts, INodeCreationService Service);

/// <summary>The questions a search entry may ask before it creates its node.</summary>
internal interface INodeSearchPrompts
{
    /// <summary>Asks for a type.</summary>
    /// <returns>The chosen type, or <see langword="null"/> when cancelled.</returns>
    Task<TypeSpecifier?> SelectTypeAsync();

    /// <summary>Asks for one of <paramref name="methods"/>.</summary>
    /// <param name="methods">The methods to choose from.</param>
    /// <returns>The chosen method, or <see langword="null"/> when cancelled.</returns>
    Task<MethodSpecifier?> SelectMethodAsync(IEnumerable<MethodSpecifier> methods);
}

/// <summary>The prompts of the node search, shown by the editor dialogs.</summary>
internal sealed class DialogNodeSearchPrompts : INodeSearchPrompts
{
    private readonly EditorContext context;

    /// <summary>Creates the prompts over the dialogs of <paramref name="context"/>.</summary>
    /// <param name="context">The editor context.</param>
    public DialogNodeSearchPrompts(EditorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        this.context = context;
    }

    /// <inheritdoc/>
    public Task<TypeSpecifier?> SelectTypeAsync() =>
        context.Dialogs.SelectTypeAsync(context.Reflection.NonStaticTypes, TypeSpecifier.FromType<object>());

    /// <inheritdoc/>
    public Task<MethodSpecifier?> SelectMethodAsync(IEnumerable<MethodSpecifier> methods) =>
        context.Dialogs.SelectMethodAsync(methods);
}

/// <summary>The default <see cref="INodeCreationService"/>: every entry creates its node through a <c>Func&lt;NodeGraph, Node&gt;</c> factory.</summary>
internal sealed class NodeCreationService : INodeCreationService
{
    private readonly NodeGraphViewModel graph;
    private readonly NodeCreationContext context;

    /// <summary>Creates the service for a graph.</summary>
    /// <param name="graph">The graph the nodes are created in.</param>
    /// <param name="prompts">The prompts entries may ask.</param>
    public NodeCreationService(NodeGraphViewModel graph, INodeSearchPrompts prompts)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(prompts);
        this.graph = graph;
        context = new NodeCreationContext(graph, prompts, this);
    }

    /// <inheritdoc/>
    public Node Create(GraphPoint position, NodePin? pin, Func<NodeGraph, Node> factory) =>
        graph.AddNode(position, pin, factory);

    /// <inheritdoc/>
    public Task<Node?> CreateAsync(NodeSearchEntry entry, GraphPoint position, NodePin? pin)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.CreateAsync(context, position, pin);
    }
}
