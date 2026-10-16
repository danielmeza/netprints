using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Extensibility.Nodes;
using NetPrints.Graph;
using NetPrints.Reflection;
using NetPrints.Serialization.Documents;

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

/// <summary>The default <see cref="INodeCreationService"/>: every node is built by a <c>Func&lt;NodeGraph, Node&gt;</c> factory.</summary>
internal sealed class NodeCreationService : INodeCreationService
{
    private readonly NodeGraphViewModel graph;
    private readonly INodeSearchPrompts prompts;

    /// <summary>Creates the service for a graph.</summary>
    /// <param name="graph">The graph the nodes are created in.</param>
    /// <param name="prompts">The prompts entries may ask.</param>
    public NodeCreationService(NodeGraphViewModel graph, INodeSearchPrompts prompts)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(prompts);
        this.graph = graph;
        this.prompts = prompts;
    }

    /// <inheritdoc/>
    public Node Create(GraphPoint position, NodePin? pin, Func<NodeGraph, Node> factory) =>
        graph.AddNode(position, pin, factory);

    /// <inheritdoc/>
    public async Task<Node?> CreateAsync(NodeSearchEntry entry, GraphPoint position, NodePin? pin)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var context = graph.Context;

        switch (entry)
        {
            case NodeKindEntry { Suggestion: var suggestion }:
                return await CreateKindAsync(suggestion, position, pin).ConfigureAwait(true);

            case MethodEntry { Method: var method }:
                return Create(position, pin, g => new CallMethodNode(g, method));

            case VariableEntry { Variable: var variable }:
                graph.GetSetChooser.Open(variable, position, pin);
                return null;

            case CustomEventEntry:
                if (graph.Graph is EventGraph { Class: { } eventClass })
                {
                    string name = MemberNames.Unique(eventClass, CustomEventSuggestion.NamePrefix);
                    return graph.AddEventEntry(position, g => new EventEntryNode(g, name));
                }

                return null;

            case OverrideEventEntry { Method: var overridden }:
                return graph.AddEventEntry(position, g => new EventEntryNode(g, overridden));

            case MakeDelegateEntry { Info: var makeDelegate }:
                {
                    var methods = context.Reflection.Provider.GetMethods(new ReflectionProviderMethodQuery()
                        .WithType(makeDelegate.Type)
                        .WithVisibleFrom(makeDelegate.FromType));

                    return await prompts.SelectMethodAsync(methods).ConfigureAwait(true) is { } chosen
                        ? Create(position, pin, g => new MakeDelegateNode(g, chosen))
                        : null;
                }

            case TypeEntry { Type: var type }:
                return Create(position, pin, g => new TypeNode(g, type));

            default:
                return null;
        }
    }

    private async Task<Node?> CreateKindAsync(NodeSuggestion suggestion, GraphPoint position, NodePin? pin)
    {
        var context = graph.Context;
        string? kind = context.Extensions.Current.NodeKinds.FirstOrDefault(k => k.Suggestions.Contains(suggestion))?.Kind;

        switch (kind)
        {
            case BuiltInNodeKinds.Constructor:
                if (await prompts.SelectTypeAsync().ConfigureAwait(true) is { } constructedType
                    && context.Reflection.Provider.GetConstructors(constructedType).FirstOrDefault() is { } constructor)
                {
                    return Create(position, pin, g => new ConstructorNode(g, constructor));
                }

                return null;

            case BuiltInNodeKinds.Literal:
                return await prompts.SelectTypeAsync().ConfigureAwait(true) is { } literalType
                    ? Create(position, pin, g => new LiteralNode(g, literalType))
                    : null;

            case BuiltInNodeKinds.Type:
                return await prompts.SelectTypeAsync().ConfigureAwait(true) is { } nodeType
                    ? Create(position, pin, g => new TypeNode(g, nodeType))
                    : null;

            default:
                return graph.AddNode(position, pin, suggestion);
        }
    }
}
