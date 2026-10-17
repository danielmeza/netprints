using System.Collections.Frozen;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Extensibility.Nodes;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;

namespace NetPrints.Editor.NodeSearch;

/// <summary>How the entry of one node kind creates its node; a kind that needs a prompt first has its own subclass.</summary>
internal abstract class NodeKindCreation
{
    private static readonly FrozenDictionary<string, NodeKindCreation> Prompted = new Dictionary<string, NodeKindCreation>
    {
        [BuiltInNodeKinds.Constructor] = new ConstructorCreation(),
        [BuiltInNodeKinds.Literal] = new LiteralCreation(),
        [BuiltInNodeKinds.Type] = new TypeCreation(),
    }.ToFrozenDictionary();

    private static readonly NodeKindCreation Plain = new PlainCreation();

    /// <summary>Gets the creation of the node kind with id <paramref name="kind"/>.</summary>
    /// <param name="kind">The node kind id, or <see langword="null"/> when no kind owns the suggestion.</param>
    /// <returns>The creation of the kind; the plain suggestion factory when the kind asks nothing.</returns>
    public static NodeKindCreation For(string? kind) =>
        kind is not null && Prompted.TryGetValue(kind, out NodeKindCreation? creation) ? creation : Plain;

    /// <summary>Creates the node of <paramref name="suggestion"/>.</summary>
    /// <param name="context">The creation context.</param>
    /// <param name="suggestion">The suggestion of the node kind.</param>
    /// <param name="position">Where the node is created.</param>
    /// <param name="pin">Pin the search was opened for, or <see langword="null"/>.</param>
    /// <returns>The new node, or <see langword="null"/> when the prompt was cancelled.</returns>
    public abstract Task<Node?> CreateAsync(NodeCreationContext context, NodeSuggestion suggestion, GraphPoint position, NodePin? pin);

    private sealed class PlainCreation : NodeKindCreation
    {
        public override Task<Node?> CreateAsync(NodeCreationContext context, NodeSuggestion suggestion, GraphPoint position, NodePin? pin) =>
            Task.FromResult<Node?>(context.Graph.AddNode(position, pin, suggestion));
    }

    private abstract class TypePromptCreation : NodeKindCreation
    {
        public override async Task<Node?> CreateAsync(NodeCreationContext context, NodeSuggestion suggestion, GraphPoint position, NodePin? pin)
        {
            if (await context.Prompts.SelectTypeAsync().ConfigureAwait(true) is not { } type)
            {
                return null;
            }

            Func<NodeGraph, Node>? factory = FactoryFor(context, type);
            return factory is null ? null : context.Service.Create(position, pin, factory);
        }

        protected abstract Func<NodeGraph, Node>? FactoryFor(NodeCreationContext context, TypeSpecifier type);
    }

    private sealed class ConstructorCreation : TypePromptCreation
    {
        protected override Func<NodeGraph, Node>? FactoryFor(NodeCreationContext context, TypeSpecifier type) =>
            context.Graph.Context.Reflection.Provider.GetConstructors(type).FirstOrDefault() is { } constructor
                ? g => new ConstructorNode(g, constructor)
                : null;
    }

    private sealed class LiteralCreation : TypePromptCreation
    {
        protected override Func<NodeGraph, Node>? FactoryFor(NodeCreationContext context, TypeSpecifier type) =>
            g => new LiteralNode(g, type);
    }

    private sealed class TypeCreation : TypePromptCreation
    {
        protected override Func<NodeGraph, Node>? FactoryFor(NodeCreationContext context, TypeSpecifier type) =>
            g => new TypeNode(g, type);
    }
}
