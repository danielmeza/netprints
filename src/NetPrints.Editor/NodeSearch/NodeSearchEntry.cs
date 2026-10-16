using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Icons;
using NetPrints.Extensibility.Nodes;
using NetPrints.Graph;
using NetPrints.Reflection;

namespace NetPrints.Editor.NodeSearch;

/// <summary>
/// A suggestion a provider offers: the category it is grouped under, the text and icon of its row, and the value that creates the node.
/// </summary>
/// <param name="Category">Category the entry is grouped under.</param>
/// <param name="Text">Text of the row.</param>
/// <param name="IconKey">Icon id of the row.</param>
internal abstract record NodeSearchEntry(string Category, string Text, string IconKey)
{
    /// <summary>Gets extra words the search matches besides the category and the text.</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>Creates the node of this entry, asking for a type or method first where the entry needs one.</summary>
    /// <param name="context">The graph, prompts and creation service the entry may use.</param>
    /// <param name="position">Where the node is created (graph coordinates).</param>
    /// <param name="pin">Pin the search was opened for, or <see langword="null"/>.</param>
    /// <returns>The new node, or <see langword="null"/> when the prompt was cancelled or the entry opens a chooser instead.</returns>
    public abstract Task<Node?> CreateAsync(NodeCreationContext context, GraphPoint position, NodePin? pin);
}

/// <summary>A method to call.</summary>
/// <param name="Category">Category of the entry.</param>
/// <param name="Method">The method.</param>
internal sealed record MethodEntry(string Category, MethodSpecifier Method)
    : NodeSearchEntry(Category, SuggestionRowViewModel.FormatMethod(Method), OperatorUtil.IsOperator(Method) ? IconIds.CategoryOperator : IconIds.CategoryMethod)
{
    /// <inheritdoc/>
    public override Task<Node?> CreateAsync(NodeCreationContext context, GraphPoint position, NodePin? pin) =>
        Task.FromResult<Node?>(context.Service.Create(position, pin, g => new CallMethodNode(g, Method)));
}

/// <summary>A variable to read or write.</summary>
/// <param name="Category">Category of the entry.</param>
/// <param name="Variable">The variable.</param>
internal sealed record VariableEntry(string Category, VariableSpecifier Variable)
    : NodeSearchEntry(Category, $"{Variable.Type} {Variable.Name} : {Variable.Type}", IconIds.CategoryProperty)
{
    /// <inheritdoc/>
    public override Task<Node?> CreateAsync(NodeCreationContext context, GraphPoint position, NodePin? pin)
    {
        context.Graph.GetSetChooser.Open(Variable, position, pin);
        return Task.FromResult<Node?>(null);
    }
}

/// <summary>A type node.</summary>
/// <param name="Category">Category of the entry.</param>
/// <param name="Type">The type.</param>
internal sealed record TypeEntry(string Category, TypeSpecifier Type)
    : NodeSearchEntry(Category, Type.FullCodeName, IconIds.CategoryType)
{
    /// <inheritdoc/>
    public override Task<Node?> CreateAsync(NodeCreationContext context, GraphPoint position, NodePin? pin) =>
        Task.FromResult<Node?>(context.Service.Create(position, pin, g => new TypeNode(g, Type)));
}

/// <summary>A node kind's own suggestion, built in or from an extension.</summary>
/// <param name="Category">Category of the entry.</param>
/// <param name="Suggestion">The suggestion of the node kind.</param>
internal sealed record NodeKindEntry(string Category, NodeSuggestion Suggestion)
    : NodeSearchEntry(Category, Suggestion.DisplayName, Suggestion.IconKey ?? IconIds.CategoryNone)
{
    /// <inheritdoc/>
    public override Task<Node?> CreateAsync(NodeCreationContext context, GraphPoint position, NodePin? pin)
    {
        string? kind = context.Graph.Context.Extensions.Current.NodeKinds.FirstOrDefault(k => k.Suggestions.Contains(Suggestion))?.Kind;
        return NodeKindCreation.For(kind).CreateAsync(context, Suggestion, position, pin);
    }
}

/// <summary>Make a delegate for a method of the type of the dragged pin.</summary>
/// <param name="Category">Category of the entry.</param>
/// <param name="Info">The type and the type the delegate is made from.</param>
internal sealed record MakeDelegateEntry(string Category, MakeDelegateTypeInfo Info)
    : NodeSearchEntry(Category, $"Make Delegate For A Method Of {Info.Type.ShortName}", IconIds.CategoryDelegate)
{
    /// <inheritdoc/>
    public override async Task<Node?> CreateAsync(NodeCreationContext context, GraphPoint position, NodePin? pin)
    {
        var methods = context.Graph.Context.Reflection.Provider.GetMethods(new ReflectionProviderMethodQuery()
            .WithType(Info.Type)
            .WithVisibleFrom(Info.FromType));

        return await context.Prompts.SelectMethodAsync(methods).ConfigureAwait(true) is { } chosen
            ? context.Service.Create(position, pin, g => new MakeDelegateNode(g, chosen))
            : null;
    }
}

/// <summary>A custom event entry of an event graph.</summary>
/// <param name="Category">Category of the entry.</param>
internal sealed record CustomEventEntry(string Category)
    : NodeSearchEntry(Category, CustomEventSuggestion.DisplayText, IconIds.CategoryNone)
{
    /// <inheritdoc/>
    public override Task<Node?> CreateAsync(NodeCreationContext context, GraphPoint position, NodePin? pin)
    {
        if (context.Graph.Graph is EventGraph { Class: { } eventClass })
        {
            string name = MemberNames.Unique(eventClass, CustomEventSuggestion.NamePrefix);
            return Task.FromResult<Node?>(context.Graph.AddEventEntry(position, g => new EventEntryNode(g, name)));
        }

        return Task.FromResult<Node?>(null);
    }
}

/// <summary>An override entry of an event graph.</summary>
/// <param name="Category">Category of the entry.</param>
/// <param name="Method">The base method to override.</param>
internal sealed record OverrideEventEntry(string Category, MethodSpecifier Method)
    : NodeSearchEntry(Category, $"Override {Method.Name}", IconIds.CategoryMethod)
{
    /// <inheritdoc/>
    public override Task<Node?> CreateAsync(NodeCreationContext context, GraphPoint position, NodePin? pin) =>
        Task.FromResult<Node?>(context.Graph.AddEventEntry(position, g => new EventEntryNode(g, Method)));
}
